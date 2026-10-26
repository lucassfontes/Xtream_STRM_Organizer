using System.Net;
using System.Text.Json;

namespace XtreamStrmOrganizer;

public sealed class XtreamClient(HttpClient client, ConnectionSettings settings)
{
    private readonly string _baseUrl = FixBaseUrl(settings.Url);

    public static string FixBaseUrl(string raw)
    {
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var url)
            || (url.Scheme != "http" && url.Scheme != "https")
            || string.IsNullOrWhiteSpace(url.Host)
            || !string.IsNullOrEmpty(url.UserInfo))
            throw new ArgumentException("URL inválida: informe http://servidor:porta ou https://servidor:porta.");

        var path = url.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/player_api.php", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/player_api.php".Length];
        else if (path.EndsWith("/get.php", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/get.php".Length];
        var builder = new UriBuilder(url) { Path = path + "/", Query = "", Fragment = "" };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    public Action? OnRetry { get; init; }

    public async Task<JsonDocument> RequestAsync(string? action, CancellationToken token,
        string? additionalKey = null, string? additionalValue = null)
    {
        var query = "username=" + Uri.EscapeDataString(settings.Username)
                  + "&password=" + Uri.EscapeDataString(settings.Password);
        if (action is not null) query += "&action=" + Uri.EscapeDataString(action);
        if (additionalKey is not null && additionalValue is not null)
            query += "&" + additionalKey + "=" + Uri.EscapeDataString(additionalValue);
        var uri = _baseUrl + "/player_api.php?" + query;
        // Até 3 tentativas, com recuo, apenas para falhas transitórias (429, 5xx, rede e timeout).
        // Nunca repete 401/403, evitando insistir em credenciais inválidas.
        for (var attempt = 1; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"A API Xtream respondeu HTTP {(int)response.StatusCode}.",
                        null, response.StatusCode);
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                try { return await JsonDocument.ParseAsync(stream, cancellationToken: token); }
                catch (JsonException) { throw new InvalidOperationException("O servidor não retornou JSON válido da API Xtream."); }
            }
            catch (Exception ex) when (attempt < 3 && !token.IsCancellationRequested && IsTransient(ex))
            {
                OnRetry?.Invoke();
                await Task.Delay(TimeSpan.FromMilliseconds(attempt * 850), token);
            }
        }
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        HttpRequestException http => http.StatusCode is null
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout,
        TaskCanceledException => true, // Timeout do HttpClient, desde que token externo não tenha sido cancelado
        IOException => true,
        _ => false
    };

    public async Task VerifyAsync(CancellationToken token)
    {
        using var doc = await RequestAsync(null, token);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("user_info", out var info))
            throw new InvalidOperationException("Resposta de autenticação Xtream inválida (user_info ausente).");
        var auth = JsonUtil.String(info, "auth");
        if (auth != "1") throw new UnauthorizedAccessException("Credenciais Xtream recusadas (auth != 1).");
        var status = JsonUtil.String(info, "status");
        if (status.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
         || status.Equals("Banned", StringComparison.OrdinalIgnoreCase)
         || status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A conta Xtream está inativa: " + status);
    }

    public string StreamUrl(string kind, string id, string extension)
    {
        if (kind is not ("live" or "movie" or "series")) throw new ArgumentException("Tipo não permitido");
        extension = extension.TrimStart('.').ToLowerInvariant();
        if (extension is not ("ts" or "m3u8" or "mp4" or "mkv" or "avi" or "mov" or "webm" or "flv"))
            extension = kind == "live" ? "ts" : "mp4";
        return _baseUrl + "/" + kind + "/" + Uri.EscapeDataString(settings.Username) + "/"
             + Uri.EscapeDataString(settings.Password) + "/" + Uri.EscapeDataString(id) + "." + extension;
    }
}

public static class JsonUtil
{
    public static string String(JsonElement item, string name, string fallback = "")
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var prop)) return fallback;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? fallback,
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    public static JsonElement Array(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var arr)
            && arr.ValueKind == JsonValueKind.Array) return arr;
        throw new InvalidOperationException("A API Xtream não retornou a lista esperada.");
    }

    public static IEnumerable<string> CategoryIds(JsonElement element)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object) return ids;
        if (element.TryGetProperty("category_ids", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in array.EnumerateArray())
            {
                var id = x.ValueKind == JsonValueKind.String ? x.GetString() : x.ValueKind == JsonValueKind.Number ? x.GetRawText() : null;
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
        }
        var single = String(element, "category_id");
        if (!string.IsNullOrWhiteSpace(single)) ids.Add(single);
        if (ids.Count == 0) ids.Add("");
        return ids;
    }
}
