using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XtreamStrmOrganizer;

// O cache só guarda as informações dos episódios. Credenciais não são gravadas no nome dos arquivos.
public sealed class SeriesCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _directory;

    public SeriesCache(string configFile, ConnectionSettings settings)
    {
        var key = $"{XtreamClient.FixBaseUrl(settings.Url)}\n{settings.Username}\n{settings.Password}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        _directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configFile))!, "cache", digest);
    }

    private string Filename(string id) => Path.Combine(_directory, id + ".json");

    public CachedSeries? Read(string id)
    {
        try
        {
            var file = Filename(id);
            if (!File.Exists(file)) return null;
            var entry = JsonSerializer.Deserialize<CachedSeries>(File.ReadAllText(file), JsonOptions);
            if (entry is null || entry.Schema != 1 || entry.SeriesId != id ||
                entry.Episodes.ValueKind != JsonValueKind.Object) return null;
            return entry;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { return null; }
    }

    public static bool IsFresh(CachedSeries entry, string sourceTag, DateTimeOffset now)
    {
        // Revalidação de pelo menos uma vez por dia, mesmo que o provedor nunca mude last_modified.
        return entry.SourceTag == sourceTag && entry.FetchedAt <= now &&
               now - entry.FetchedAt < TimeSpan.FromHours(24);
    }

    public void Save(string id, string tag, JsonElement episodes)
    {
        Directory.CreateDirectory(_directory);
        var entry = new CachedSeries
        {
            SeriesId = id,
            SourceTag = tag,
            FetchedAt = DateTimeOffset.UtcNow,
            Episodes = episodes.Clone()
        };
        var file = Filename(id);
        var tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(entry, JsonOptions), new UTF8Encoding(false));
            if (OperatingSystem.IsLinux())
            {
                try { File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            File.Move(tmp, file, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}

public sealed class CachedSeries
{
    public int Schema { get; set; } = 1;
    public string SeriesId { get; set; } = "";
    public string SourceTag { get; set; } = "";
    public DateTimeOffset FetchedAt { get; set; }
    public JsonElement Episodes { get; set; }
}
