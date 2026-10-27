using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace XtreamStrmOrganizer;

// All library writes are additive. A failed/cancelled transfer never leaves a final file.
public sealed class MetadataWriter(HttpClient http, Action<Exception> warning)
{
    private const int MaxImageBytes = 20 * 1024 * 1024;
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".tbn"];
    private static string Stem(string path) => Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));
    private static bool HasImage(string stem) => ImageExtensions.Any(ext => File.Exists(stem + ext));
    public bool NeedsFlat(string path, bool images, bool nfo) =>
        (images && !HasImage(Stem(path) + "-poster")) || (nfo && !File.Exists(Path.ChangeExtension(path, ".nfo")));

    private static JsonElement Child(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var child) ? child : default;
    private static string First(JsonElement value, params string[] keys) =>
        keys.Select(k => JsonUtil.String(value, k)).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
    private static string Choose(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a;
    // Preserve provider fields not present in a detail response; no invented metadata.
    private static JsonElement Merge(JsonElement primary, JsonElement fallback)
    {
        var fields = new Dictionary<string, JsonElement>();
        if (fallback.ValueKind == JsonValueKind.Object)
            foreach (var p in fallback.EnumerateObject()) fields[p.Name] = p.Value;
        if (primary.ValueKind == JsonValueKind.Object)
            foreach (var p in primary.EnumerateObject())
                if (p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                    && !(p.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(p.Value.GetString())))
                    fields[p.Name] = p.Value;
        return JsonSerializer.SerializeToElement(fields);
    }
    private static string Clean(string value)
    {
        // XML 1.0 cannot contain provider control characters.
        return new string(value.Where(c => XmlConvert.IsXmlChar(c)).ToArray());
    }
    private static void Add(XElement root, string tag, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) root.Add(new XElement(tag, Clean(value)));
    }
    private static XElement Nfo(string kind, JsonElement info, string title)
    {
        var root = new XElement(kind);
        Add(root, "title", title);
        Add(root, "originaltitle", First(info, "o_name", "original_name", "original_title"));
        Add(root, "plot", First(info, "plot", "description", "overview"));
        Add(root, "tagline", First(info, "tagline"));
        var date = First(info, "releasedate", "releaseDate", "release_date", "air_date");
        if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            Add(root, kind == "episodedetails" ? "aired" : "premiered", parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Add(root, "year", parsed.Year.ToString(CultureInfo.InvariantCulture));
        }
        else if (int.TryParse(First(info, "year"), out var year) && year is >= 1800 and <= 3000)
            Add(root, "year", year.ToString(CultureInfo.InvariantCulture));
        if (double.TryParse(First(info, "rating"), NumberStyles.Float, CultureInfo.InvariantCulture, out var rating)
            && double.IsFinite(rating) && rating is >= 0 and <= 10)
            Add(root, "rating", rating.ToString(CultureInfo.InvariantCulture));
        if (double.TryParse(First(info, "duration_secs"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && double.IsFinite(seconds) && seconds > 0)
            Add(root, "runtime", Math.Ceiling(seconds / 60).ToString(CultureInfo.InvariantCulture));
        else if (TimeSpan.TryParse(First(info, "duration"), CultureInfo.InvariantCulture, out var duration) && duration.TotalMinutes > 0)
            Add(root, "runtime", Math.Ceiling(duration.TotalMinutes).ToString(CultureInfo.InvariantCulture));
        foreach (var genre in First(info, "genre").Split([',', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            Add(root, "genre", genre);
        foreach (var director in First(info, "director").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            Add(root, "director", director);
        foreach (var actor in First(info, "cast", "actors").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            root.Add(new XElement("actor", new XElement("name", Clean(actor))));
        var tmdb = First(info, "tmdb_id", "tmdb");
        if (long.TryParse(tmdb, out var tmdbId) && tmdbId > 0)
        {
            Add(root, "tmdbid", tmdb);
            root.Add(new XElement("uniqueid", new XAttribute("type", "tmdb"), tmdb));
        }
        var imdb = First(info, "imdb_id");
        if (imdb.StartsWith("tt", StringComparison.Ordinal) && imdb[2..].Length > 0 && imdb[2..].All(char.IsAsciiDigit))
        {
            Add(root, "imdbid", imdb);
            root.Add(new XElement("uniqueid", new XAttribute("type", "imdb"), imdb));
        }
        Add(root, "country", First(info, "country"));
        return root;
    }

    public async Task FlatAsync(string path, JsonElement item, JsonElement details, bool images, bool nfo, CancellationToken token, string kind = "filme")
    {
        var target = WarningText.Item(kind, Choose(First(item, "name", "title"), Path.GetFileNameWithoutExtension(path)));
        var info = Merge(Child(details, "info"), Merge(Child(details, "movie_data"), item));
        if (nfo) await SafeAsync("o NFO " + target, "não foi criado", () => WriteNfoAsync(Path.ChangeExtension(path, ".nfo"),
            Nfo("movie", info, Choose(First(info, "name", "title"), Path.GetFileNameWithoutExtension(path))), token), token);
        if (images) await SafeAsync("a capa " + target, "não foi baixada", () => ImageAsync(Stem(path) + "-poster",
            First(info, "movie_image", "cover_big", "cover", "stream_icon"), token), token);
    }
    public async Task SeriesAsync(string folder, JsonElement item, JsonElement details, bool images, bool nfo, CancellationToken token)
    {
        var target = "da série \"" + WarningText.Name(First(item, "name", "title")) + "\"";
        var info = Merge(Child(details, "info"), item);
        if (nfo) await SafeAsync("o NFO " + target, "não foi criado", () => WriteNfoAsync(Path.Combine(folder, "tvshow.nfo"),
            Nfo("tvshow", info, First(info, "name", "title")), token), token);
        if (images) await SafeAsync("a capa " + target, "não foi baixada", () => ImageAsync(Path.Combine(folder, "poster"),
            First(info, "cover", "cover_big", "movie_image"), token), token);
    }
    public async Task SeasonAsync(string folder, int number, JsonElement details, bool images, bool nfo, CancellationToken token, string? show = null)
    {
        var target = $"da temporada {number:00} da série \"{WarningText.Name(show ?? First(Child(details, "info"), "name", "title"))}\"";
        var seasons = Child(details, "seasons");
        var info = default(JsonElement);
        if (seasons.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            var entries = seasons.ValueKind == JsonValueKind.Array ? seasons.EnumerateArray().ToArray()
                : seasons.EnumerateObject().Select(p => p.Value).ToArray();
            info = entries.FirstOrDefault(x => int.TryParse(First(x, "season_number"), out var n) && n == number);
        }
        if (nfo)
        {
            var xml = Nfo("season", info, Choose(First(info, "name"), $"Temporada {number:00}"));
            Add(xml, "seasonnumber", number.ToString(CultureInfo.InvariantCulture));
            await SafeAsync("o NFO " + target, "não foi criado", () => WriteNfoAsync(Path.Combine(folder, "season.nfo"), xml, token), token);
        }
        // If the provider has no season art, use its series poster as a fallback.
        if (images) await SafeAsync("a capa " + target, "não foi baixada", () => ImageAsync(Path.Combine(folder, "poster"),
            Choose(First(info, "cover", "cover_big"), First(Child(details, "info"), "cover", "cover_big")), token), token);
    }
    public async Task EpisodeAsync(string path, JsonElement episode, string show, int season, int number,
        bool images, bool nfo, CancellationToken token)
    {
        var target = WarningText.Episode(show, First(episode, "title", "name"), season, number);
        var info = Merge(Child(episode, "info"), episode);
        if (nfo)
        {
            var xml = Nfo("episodedetails", info, Choose(First(episode, "title", "name"), $"Episódio {number}"));
            Add(xml, "showtitle", show);
            Add(xml, "season", season.ToString(CultureInfo.InvariantCulture));
            Add(xml, "episode", number.ToString(CultureInfo.InvariantCulture));
            await SafeAsync("o NFO " + target, "não foi criado", () => WriteNfoAsync(Path.ChangeExtension(path, ".nfo"), xml, token), token);
        }
        if (images) await SafeAsync("a miniatura " + target, "não foi baixada", () => ImageAsync(Stem(path) + "-thumb",
            First(info, "movie_image", "cover_big", "cover", "stream_icon"), token), token);
    }

    private async Task SafeAsync(string subject, string failure, Func<Task> action, CancellationToken token)
    {
        try { token.ThrowIfCancellationRequested(); await action(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { warning(new MediaWarningException(subject, failure, ex)); }
    }
    private static Task WriteNfoAsync(string path, XElement root, CancellationToken token) =>
        WriteNewAsync(path, Encoding.UTF8.GetBytes(new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString()), token);

    internal static async Task WriteNewAsync(string path, byte[] bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".xtream-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token);
            token.ThrowIfCancellationRequested();
            try { File.Move(temporary, path, false); }
            catch (IOException) when (File.Exists(path)) { }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private async Task ImageAsync(string stem, string source, CancellationToken token)
    {
        var gate = _locks[(int)((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(stem) % (uint)_locks.Length)];
        await gate.WaitAsync(token);
        try
        {
            if (HasImage(stem)) return;
            if (string.IsNullOrWhiteSpace(source))
                throw new MediaInputException("O provedor não informou uma imagem para este item.");
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new MediaInputException("O endereço da imagem informado pelo provedor é inválido.");
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxImageBytes)
                throw new MediaInputException("A imagem ultrapassa o limite de 20 MB permitido pelo programa.");
            // HttpClient's headers-only timeout does not cover the response body.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, deadline.Token)) > 0)
            {
                if (output.Length + count > MaxImageBytes) throw new MediaInputException("A imagem ultrapassa o limite de 20 MB permitido pelo programa.");
                await output.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
            }
            var bytes = output.ToArray();
            // Do not save HTML error pages or disguise WebP/GIF as JPEG.
            string extension;
            if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) extension = ".png";
            else if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) extension = ".jpg";
            else throw new MediaInputException("O servidor não enviou uma imagem JPEG ou PNG válida. Pode ter retornado uma página de erro ou outro formato.");
            await WriteNewAsync(stem + extension, bytes, token);
        }
        finally { gate.Release(); }
    }
}
