using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace XtreamStrmOrganizer;

public sealed class SettingsStore
{
    private readonly object _gate = new();
    private readonly string _file;
    private readonly string _legacy;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public SettingsStore(IConfiguration config)
    {
        _legacy = Path.GetFullPath(config["CONFIG_FILE"] ?? "/app/data/config.json");
        _file = Path.Combine(Path.GetDirectoryName(_legacy)!, "connections.json");
    }
    private List<ConnectionSettings> ReadAllUnsafe()
    {
        if (File.Exists(_file)) return JsonSerializer.Deserialize<List<ConnectionSettings>>(File.ReadAllText(_file), JsonOptions) ?? [];
        if (!File.Exists(_legacy)) return [];
        // Migração sem excluir o arquivo da instalação anterior.
        var old = JsonSerializer.Deserialize<ConnectionSettings>(File.ReadAllText(_legacy), JsonOptions);
        if (old is null || string.IsNullOrWhiteSpace(old.Url)) return [];
        old.Id = Guid.NewGuid().ToString("N");
        old.Name = "Lista importada";
        old.LegacyRoot = false;
        old.FolderName = PathRules.SafeName(old.Name, "Lista");
        WriteAllUnsafe([old]);
        return [old];
    }
    private void WriteAllUnsafe(List<ConnectionSettings> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var tmp = _file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(items, JsonOptions), new UTF8Encoding(false));
            FilePermissions.Private(tmp);
            File.Move(tmp, _file, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    public List<PublicSettings> Profiles()
    {
        lock (_gate) return ReadAllUnsafe().Select(ToPublic).ToList();
    }
    private static PublicSettings ToPublic(ConnectionSettings x) => new()
    {
        Id = x.Id, Name = x.Name, Url = x.Url, Username = x.Username,
        PasswordSaved = !string.IsNullOrWhiteSpace(x.Password),
        Live = x.Live, Movies = x.Movies, Series = x.Series,
        LiveImages = x.LiveImages, LiveNfo = x.LiveNfo, MovieImages = x.MovieImages, MovieNfo = x.MovieNfo, SeriesImages = x.SeriesImages, SeriesNfo = x.SeriesNfo,
        LiveExtension = x.LiveExtension, SeriesConcurrency = x.SeriesConcurrency,
        AutoUpdate = x.AutoUpdate, AutoLive = x.AutoLive, AutoMovies = x.AutoMovies,
        AutoLiveImages = x.AutoLiveImages ?? x.LiveImages,
        AutoLiveNfo = x.AutoLiveNfo ?? x.LiveNfo,
        AutoMovieImages = x.AutoMovieImages ?? x.MovieImages,
        AutoMovieNfo = x.AutoMovieNfo ?? x.MovieNfo,
        AutoSeriesImages = x.AutoSeriesImages ?? x.SeriesImages,
        AutoSeriesNfo = x.AutoSeriesNfo ?? x.SeriesNfo,
        AutoSeries = x.AutoSeries, IntervalHours = x.IntervalHours,
        NextRunUtc = x.NextRunUtc, LastRunUtc = x.LastRunUtc, LegacyRoot = false, FolderName = PathRules.SafeName(x.Name, "Lista"),
        RunTimeLocal = ScheduleClock.EffectiveTime(x), NextRunServer = ScheduleClock.Display(x.NextRunUtc),
        LastRunServer = ScheduleClock.Display(x.LastRunUtc), ServerTimeZone = ScheduleClock.ZoneName
    };
    public ConnectionSettings Read(string? id = null)
    {
        lock (_gate)
        {
            var all = ReadAllUnsafe();
            var p = string.IsNullOrEmpty(id) ? all.FirstOrDefault() : all.FirstOrDefault(x => x.Id == id);
            return p ?? throw new ArgumentException("Conexão Xtream não encontrada. Cadastre uma conexão.");
        }
    }
    public PublicSettings Public(string? id = null) => ToPublic(Read(id));
    public PublicSettings Save(ConnectionSettings input)
    {
        lock (_gate)
        {
            var profiles = ReadAllUnsafe();
            var old = profiles.FirstOrDefault(x => x.Id == input.Id);
            input.AutoLiveImages ??= old?.AutoLiveImages;
            input.AutoLiveNfo ??= old?.AutoLiveNfo;
            input.AutoMovieImages ??= old?.AutoMovieImages;
            input.AutoMovieNfo ??= old?.AutoMovieNfo;
            input.AutoSeriesImages ??= old?.AutoSeriesImages;
            input.AutoSeriesNfo ??= old?.AutoSeriesNfo;
            if (string.IsNullOrWhiteSpace(input.Password)) input.Password = old?.Password ?? "";
            input.Url = XtreamClient.FixBaseUrl(input.Url);
            input.Username = (input.Username ?? "").Trim();
            input.Name = (input.Name ?? "").Trim();
            if (input.Name.Length is < 1 or > 60) throw new ArgumentException("Nome da conexão: máximo de 60 caracteres.");
            if (input.SeriesConcurrency is not (4 or 8 or 16)) throw new ArgumentException("Escolha 4, 8 ou 16 séries simultâneas.");
            input.LiveExtension = (input.LiveExtension ?? "ts").TrimStart('.').ToLowerInvariant();
            if (input.LiveExtension is not ("ts" or "m3u8")) input.LiveExtension = "ts";
            if (string.IsNullOrWhiteSpace(input.Username) || string.IsNullOrWhiteSpace(input.Password))
                throw new ArgumentException("Informe o usuário e a senha Xtream.");
            if (!input.Live && !input.Movies && !input.Series) throw new ArgumentException("Marque ao menos um conteúdo.");
            if (input.IntervalHours is not (12 or 24)) throw new ArgumentException("Intervalo: 12 ou 24 horas.");
            input.RunTimeLocal = (input.RunTimeLocal ?? "").Trim();
            if (input.RunTimeLocal.Length == 0) input.RunTimeLocal = old is null ? "03:00" : ScheduleClock.EffectiveTime(old);
            if (!ScheduleClock.IsValid(input.RunTimeLocal)) throw new ArgumentException("Informe um horário válido entre 00:00 e 23:59.");
            if (input.AutoUpdate && !(input.AutoLive && input.Live || input.AutoMovies && input.Movies || input.AutoSeries && input.Series))
                throw new ArgumentException("Escolha TV, Filmes ou Séries para a atualização automática.");
            input.Id = old?.Id ?? Guid.NewGuid().ToString("N");
            // A pasta fica diretamente em Biblioteca_IPTV com o nome visível da conexão.
            // Nenhum sufixo por ID nem pasta intermediária "Conexoes".
            input.LegacyRoot = false;
            input.FolderName = PathRules.SafeName(input.Name, "Lista");
            if (profiles.Any(p => p.Id != input.Id &&
                    string.Equals(PathRules.SafeName(p.Name, "Lista"), input.FolderName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Já existe outra conexão com esse nome de pasta. Escolha um nome diferente.");
            input.LastRunUtc = old?.LastRunUtc;
            // Conserva a agenda ao salvar outros campos; só recalcula se frequência/hora mudarem.
            input.NextRunUtc = input.AutoUpdate
                ? (old is not null && old.AutoUpdate && old.IntervalHours == input.IntervalHours &&
                   ScheduleClock.EffectiveTime(old) == input.RunTimeLocal && old.NextRunUtc.HasValue
                    ? old.NextRunUtc : ScheduleClock.NextAfter(DateTimeOffset.UtcNow, input.IntervalHours, input.RunTimeLocal))
                : null;
            if (old is null) profiles.Add(input);
            else profiles[profiles.IndexOf(old)] = input;
            WriteAllUnsafe(profiles);
            return ToPublic(input);
        }
    }
    public bool Delete(string id)
    {
        lock (_gate)
        {
            var items = ReadAllUnsafe();
            var changed = items.RemoveAll(x => x.Id == id) != 0;
            if (changed) WriteAllUnsafe(items);
            return changed;
        }
    }
    public List<ConnectionSettings> Due(DateTimeOffset now)
    {
        lock (_gate) return ReadAllUnsafe().Where(x => x.AutoUpdate && x.NextRunUtc is { } next && next <= now).OrderBy(x => x.NextRunUtc).ToList();
    }
    public void MarkScheduled(string id, DateTimeOffset now)
    {
        lock (_gate)
        {
            var all = ReadAllUnsafe();
            var profile = all.FirstOrDefault(x => x.Id == id);
            if (profile is null || !profile.AutoUpdate) return;
            profile.LastRunUtc = now;
            profile.NextRunUtc = ScheduleClock.NextAfter(now, profile.IntervalHours, ScheduleClock.EffectiveTime(profile));
            WriteAllUnsafe(all);
        }
    }
}

public sealed class Organizer
{
    private readonly SettingsStore _store;
    private readonly IHttpClientFactory _http;
    private readonly string _outputBase;
    private readonly string _configFile;
    private readonly string _activityFile;
    private readonly WarningLog _warnings;
    public string WarningLogPath => _warnings.FilePath;
    private bool _warningLogFailed;
    private readonly object _gate = new();
    private SyncStatus _status = new();
    private readonly List<string> _messages = [];
    private CancellationTokenSource? _cancel;
    private readonly int _writeWorkers;

    public Organizer(SettingsStore store, IHttpClientFactory http, IConfiguration config)
    {
        _store = store;
        _http = http;
        _outputBase = Path.GetFullPath(config["OUTPUT_ROOT"] ?? "/media");
        _configFile = Path.GetFullPath(config["CONFIG_FILE"] ?? "/app/data/config.json");
        _warnings = new WarningLog(_configFile);
        _activityFile = Path.Combine(Path.GetDirectoryName(_configFile)!, "activity.log");
        _writeWorkers = int.TryParse(config["STRM_WRITE_WORKERS"], out var workers) && workers is >= 1 and <= 16 ? workers : 4;
        try
        {
            if (File.Exists(_activityFile))
                _messages.AddRange(File.ReadLines(_activityFile).TakeLast(50));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void Update(Action<SyncStatus> action)
    {
        lock (_gate) action(_status);
    }
    private void Say(string message)
    {
        lock (_gate)
        {
            var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
            _messages.Add(entry);
            if (_messages.Count > 50) _messages.RemoveAt(0);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_activityFile)!);
                File.AppendAllText(_activityFile, entry + Environment.NewLine);
                if (new FileInfo(_activityFile).Length > 150_000)
                    File.WriteAllLines(_activityFile, _messages);
                FilePermissions.Private(_activityFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
    public SyncStatus Snapshot()
    {
        lock (_gate)
        {
            double? rate = null, eta = null;
            if (_status.SeriesStartedAt is { } started && _status.SeriesProcessed > 0)
            {
                var elapsed = (DateTimeOffset.UtcNow - started).TotalSeconds;
                if (elapsed > 0.1)
                {
                    rate = _status.SeriesProcessed / elapsed;
                    var remaining = Math.Max(0, _status.SeriesTotal - _status.SeriesProcessed);
                    eta = rate > 0 ? Math.Ceiling(remaining / rate.Value) : null;
                }
            }
            return new SyncStatus
            {
                Running = _status.Running, Phase = _status.Phase, ProfileName = _status.ProfileName, Automatic = _status.Automatic,
                Channels = _status.Channels, Movies = _status.Movies, Series = _status.Series,
                Episodes = _status.Episodes, Errors = _status.Errors,
                SeriesProcessed = _status.SeriesProcessed, SeriesTotal = _status.SeriesTotal,
                SeriesCached = _status.SeriesCached, SeriesFetched = _status.SeriesFetched,
                FilesReused = _status.FilesReused, FilesWritten = _status.FilesWritten, FilesPending = _status.FilesPending, WriteWorkers = _writeWorkers,
                RequestsRetried = _status.RequestsRetried, SeriesStartedAt = _status.SeriesStartedAt,
                EtaSeconds = eta, SeriesPerSecond = rate,
                StartedAt = _status.StartedAt, FinishedAt = _status.FinishedAt,
                Messages = _messages.ToArray()
            };
        }
    }
    public bool Start(bool forceRefresh = false, string? profileId = null, bool automatic = false)
    {
        ConnectionSettings settings;
        lock (_gate)
        {
            if (_status.Running) return false;
            settings = _store.Read(profileId);
            if (automatic)
            {
                settings.Live = settings.Live && settings.AutoLive;
                settings.Movies = settings.Movies && settings.AutoMovies;
                settings.Series = settings.Series && settings.AutoSeries;
                settings.LiveImages = settings.AutoLiveImages ?? settings.LiveImages;
                settings.LiveNfo = settings.AutoLiveNfo ?? settings.LiveNfo;
                settings.MovieImages = settings.AutoMovieImages ?? settings.MovieImages;
                settings.MovieNfo = settings.AutoMovieNfo ?? settings.MovieNfo;
                settings.SeriesImages = settings.AutoSeriesImages ?? settings.SeriesImages;
                settings.SeriesNfo = settings.AutoSeriesNfo ?? settings.SeriesNfo;
            }
            if (string.IsNullOrWhiteSpace(settings.Url) || string.IsNullOrWhiteSpace(settings.Username)
                || string.IsNullOrWhiteSpace(settings.Password))
                throw new InvalidOperationException("Salve sua conexão Xtream antes de iniciar.");
            _cancel?.Dispose();
            _cancel = new CancellationTokenSource();
            _status = new SyncStatus { Running = true, Phase = "Iniciando", StartedAt = DateTimeOffset.Now, ProfileName = settings.Name, Automatic = automatic };
        }
        Say($"{(automatic ? "[AUTOMÁTICO] " : "[MANUAL] ")}{settings.Name}: iniciando {(forceRefresh ? "consulta completa" : "sincronização incremental")}.");
        var token = _cancel.Token;
        _ = Task.Run(() => RunAsync(settings, forceRefresh, token));
        return true;
    }
    public bool Cancel()
    {
        lock (_gate)
        {
            if (!_status.Running || _cancel is null) return false;
            _cancel.Cancel();
            _messages.Add($"{DateTime.Now:HH:mm:ss}  Cancelamento solicitado...");
            return true;
        }
    }

    public async Task VerifySavedAsync(CancellationToken token, string? profileId = null)
    {
        var settings = _store.Read(profileId);
        var api = new XtreamClient(_http.CreateClient("xtream"), settings);
        await api.VerifyAsync(token);
    }

    private async Task RunAsync(ConnectionSettings settings, bool forceRefresh, CancellationToken token)
    {
        // OUTPUT_ROOT já corresponde à pasta Biblioteca_IPTV no host.
        // A pasta da lista recebe somente o nome seguro da conexão.
        var root = Path.Combine(_outputBase, PathRules.SafeName(settings.Name, "Lista"));
        try
        {
            await using var writer = new StrmWriteQueue(_writeWorkers, token,
                ExistingFileSkipped,
                () =>
                {
                    int written = 0;
                    Update(s => { s.FilesWritten++; written = s.FilesWritten; });
                    if (written % 2000 == 0) Say($"Novos arquivos .strm criados até agora: {written}.");
                },
                ex => IncrementError("gravando .strm", ex),
                count => Update(s => s.FilesPending += count));
            var api = new XtreamClient(_http.CreateClient("xtream"), settings)
            { OnRetry = () => Update(s => s.RequestsRetried++) };
            SetPhase("Verificando conexão Xtream");
            await api.VerifyAsync(token);
            Say("Conexão validada.");
            // Modo permanente e não destrutivo: nunca substituímos ou limpamos a biblioteca.
            Directory.CreateDirectory(root);
            Say($"Gravação otimizada: {_writeWorkers} gravadores simultâneos, fila limitada e arquivos .strm existentes preservados.");
            var metadata = new MetadataWriter(_http.CreateClient("artwork"),
                ex => IncrementError("metadados/capas", ex));
            if (settings.Live && (settings.LiveImages || settings.LiveNfo))
                Say("TV: capas/NFOs são auxiliares para biblioteca de vídeos; não criam canais ou EPG no Live TV do Emby.");
            if (settings.Live) await GenerateFlatAsync(writer, api, root, "TV ao vivo", "get_live_categories", "get_live_streams", "live", settings.LiveExtension, settings, metadata, token);
            if (settings.Movies) await GenerateFlatAsync(writer, api, root, "Filmes", "get_vod_categories", "get_vod_streams", "movie", "mp4", settings, metadata, token);
            if (settings.Series) await GenerateSeriesAsync(writer, api, root, settings, metadata, forceRefresh, token);
            SetPhase("Finalizando gravação dos arquivos .strm");
            await writer.CompleteAsync();
            token.ThrowIfCancellationRequested();
            var snapshot = Snapshot();
            Say($"Pronto: {snapshot.Channels} canais, {snapshot.Movies} filmes, {snapshot.Series} séries e {snapshot.Episodes} episódios processados.");
            Say($"Arquivos existentes ignorados e preservados: {snapshot.FilesReused}; arquivos novos criados: {snapshot.FilesWritten}.");
            Say($"Cache: {snapshot.SeriesCached} séries reaproveitadas, {snapshot.SeriesFetched} consultadas.");
            Say("Arquivos em: " + root);
            SetPhase(snapshot.Errors == 0 ? "Concluído" : "Concluído com avisos");
        }
        catch (OperationCanceledException)
        {
            Say("Operação cancelada. Todos os arquivos anteriores e os novos já criados permanecem salvos.");
            SetPhase("Cancelado");
        }
        catch (Exception error)
        {
            Say("Erro: " + CleanError(error.Message, settings));
            Say("Nenhum arquivo existente foi apagado ou substituído, mesmo com erro.");
            SetPhase("Erro — arquivos preservados");
        }
        finally
        {
            Update(s => { s.Running = false; s.FinishedAt = DateTimeOffset.Now; s.FilesPending = 0; });
        }
    }

    private static string CleanError(string text, ConnectionSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.Password)) text = text.Replace(settings.Password, "***", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(settings.Username)) text = text.Replace(settings.Username, "***", StringComparison.Ordinal);
        return text;
    }
    private void SetPhase(string phase) => Update(s => s.Phase = phase);
    private void IncrementError(string context, Exception ex)
    {
        Update(s => s.Errors++);
        var message = WarningText.Format(context, ex);
        Say(message);
        if (!_warnings.Append(message))
        {
            lock (_gate)
            {
                if (!_warningLogFailed)
                    Say("Aviso: não foi possível salvar log_avisos.txt. Confira as permissões e o espaço livre da pasta config. Os avisos continuam aparecendo na tela.");
                _warningLogFailed = true;
            }
        }
        else { lock (_gate) _warningLogFailed = false; }
    }

    private static async Task<Dictionary<string, string>> Categories(XtreamClient api, string action, CancellationToken token)
    {
        using var doc = await api.RequestAsync(action, token);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cat in JsonUtil.Array(doc.RootElement).EnumerateArray())
        {
            var id = JsonUtil.String(cat, "category_id");
            var name = PathRules.SafeName(JsonUtil.String(cat, "category_name"), "Sem categoria");
            if (!string.IsNullOrWhiteSpace(id)) result[id] = name;
        }
        return result;
    }

    private static IEnumerable<string> Folders(string root, string top, JsonElement item, IReadOnlyDictionary<string, string> categories)
    {
        // Sem filtros: todas as categorias Xtream são tratadas da mesma maneira.
        // Mesmo uma categoria chamada XXX ou 18+ fica na sua pasta original.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in JsonUtil.CategoryIds(item))
        {
            var tag = categories.TryGetValue(id, out var result) ? result : "Sem categoria";
            var folder = Path.Combine(root, top, tag);
            if (seen.Add(folder)) yield return folder;
        }
    }

    private async Task GenerateFlatAsync(StrmWriteQueue writer, XtreamClient api, string root, string section, string categoriesAction,
        string streamsAction, string kind, string defaultExtension, ConnectionSettings settings, MetadataWriter metadata, CancellationToken token)
    {
        SetPhase("Lendo " + section);
        Say("Consultando categorias e itens de " + section + "...");
        var categories = await Categories(api, categoriesAction, token);
        using var doc = await api.RequestAsync(streamsAction, token);
        var items = JsonUtil.Array(doc.RootElement);
        foreach (var item in items.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            var id = JsonUtil.String(item, "stream_id");
            if (!IsValidId(id))
            {
                IncrementError("STRM", new MediaWarningException("o STRM " + WarningText.Item(kind == "live" ? "canal" : "filme", JsonUtil.String(item, "name", "Sem nome")),
                    "não foi criado", new MediaInputException("O provedor não informou um identificador válido para este item.")));
                continue;
            }
            var name = PathRules.SafeName(JsonUtil.String(item, "name"), kind == "live" ? "Canal" : "Filme");
            var extension = kind == "movie" ? JsonUtil.String(item, "container_extension", defaultExtension) : defaultExtension;
            var url = api.StreamUrl(kind, id, extension);
            var images = kind == "live" ? settings.LiveImages : settings.MovieImages;
            var nfo = kind == "live" ? settings.LiveNfo : settings.MovieNfo;
            JsonElement details = default;
            var detailsLoaded = false;
            foreach (var folder in Folders(root, section, item, categories))
            {
                var path = await writer.EnqueueAsync(folder, name, id, url, token, WarningText.Item(kind == "live" ? "canal" : "filme", JsonUtil.String(item, "name", name)));
                if (!images && !nfo) continue;
                if (kind == "movie" && !detailsLoaded && metadata.NeedsFlat(path, images, nfo))
                {
                    detailsLoaded = true;
                    try
                    {
                        using var info = await api.RequestAsync("get_vod_info", token, "vod_id", id);
                        details = info.RootElement.Clone();
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception ex) { IncrementError("a consulta dos dados " + WarningText.Item("filme", JsonUtil.String(item, "name", name)) + " (o NFO poderá conter apenas os dados básicos disponíveis)", ex); }
                }
                await metadata.FlatAsync(path, item, details, images, nfo, token, kind == "live" ? "canal" : "filme");
            }
            if (kind == "live") Update(s => s.Channels++);
            else Update(s => s.Movies++);
        }
        Say($"{section}: {items.GetArrayLength()} itens enviados ao processamento (gravação continua na fila).");
    }

    private async Task GenerateSeriesAsync(StrmWriteQueue writer, XtreamClient api, string root,
        ConnectionSettings settings, MetadataWriter metadata, bool forceRefresh, CancellationToken token)
    {
        SetPhase("Lendo catálogo de séries");
        var cats = await Categories(api, "get_series_categories", token);
        using var doc = await api.RequestAsync("get_series", token);
        var entries = JsonUtil.Array(doc.RootElement).EnumerateArray().Select(e => e.Clone()).ToArray();
        var concurrency = settings.SeriesConcurrency is 4 or 8 or 16 ? settings.SeriesConcurrency : 8;
        var cache = new SeriesCache(_configFile, settings);
        Update(s => { s.SeriesTotal = entries.Length; s.SeriesStartedAt = DateTimeOffset.UtcNow; });
        Say($"Séries encontradas: {entries.Length}. Até {concurrency} consultas simultâneas; cache de até 24h.");
        if (forceRefresh) Say("Atualização completa selecionada: ignorando cache fresco.");
        var seriesPaths = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(entries, new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = token },
            async (item, ct) =>
            {
                var id = JsonUtil.String(item, "series_id");
                try
                {
                    if (!IsValidId(id)) throw new InvalidOperationException("Identificador de série inválido.");
                    var name = PathRules.SafeName(JsonUtil.String(item, "name"), "Serie");
                    // Mudanças anunciadas em last_modified invalidam o cache imediatamente.
                    var sourceTag = JsonUtil.String(item, "last_modified");
                    var old = cache.Read(id);
                    JsonElement seasons;
                    JsonElement seriesDetails = default;
                    if (!forceRefresh && old is not null && SeriesCache.IsFresh(old, sourceTag, DateTimeOffset.UtcNow)
                        && (!(settings.SeriesImages || settings.SeriesNfo) || old.Details.HasValue))
                    {
                        seasons = old.Episodes;
                        seriesDetails = old.Details ?? default;
                        Update(s => s.SeriesCached++);
                    }
                    else
                    {
                        try
                        {
                            using var details = await api.RequestAsync("get_series_info", ct, "series_id", id);
                            if (!details.RootElement.TryGetProperty("episodes", out var e) || e.ValueKind != JsonValueKind.Object)
                                throw new InvalidOperationException("Temporadas não disponíveis na API.");
                            seasons = e.Clone();
                            seriesDetails = details.RootElement.Clone();
                            Update(s => s.SeriesFetched++);
                            try { cache.Save(id, sourceTag, seasons, seriesDetails); }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                            { IncrementError("a gravação do cache da série \"" + WarningText.Name(JsonUtil.String(item, "name")) + "\"", ex); }
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception ex) when (old is not null)
                        {
                            // Um servidor temporariamente indisponível não apaga os episódios do cache anterior.
                            IncrementError("a atualização dos dados da série \"" + WarningText.Name(JsonUtil.String(item, "name")) + "\" (os dados anteriores serão usados)", ex);
                            seasons = old.Episodes;
                            seriesDetails = old.Details ?? default;
                            Update(s => s.SeriesCached++);
                        }
                    }

                    if (seasons.ValueKind != JsonValueKind.Object)
                        throw new InvalidOperationException("Resposta de episódios inválida.");
                    var paths = Folders(root, "Series", item, cats).ToArray();
                    foreach (var baseFolder in paths)
                    {
                        var proposed = Path.Combine(baseFolder, name);
                        var owner = seriesPaths.GetOrAdd(proposed, id);
                        var seriesFolder = owner == id ? proposed : Path.Combine(baseFolder, name + " [" + id + "]");
                        await metadata.SeriesAsync(seriesFolder, item, seriesDetails, settings.SeriesImages, settings.SeriesNfo, ct);
                        foreach (var season in seasons.EnumerateObject())
                        {
                            ct.ThrowIfCancellationRequested();
                            if (season.Value.ValueKind != JsonValueKind.Array) continue;
                            var seasonNumber = int.TryParse(season.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                                ? Math.Max(0, parsed) : 0;
                            var folder = Path.Combine(seriesFolder, $"Temporada {seasonNumber:00}");
                            await metadata.SeasonAsync(folder, seasonNumber, seriesDetails, settings.SeriesImages, settings.SeriesNfo, ct, JsonUtil.String(item, "name", name));
                            var episodeWidth = Math.Max(2, season.Value.EnumerateArray().Select(e =>
                                int.TryParse(JsonUtil.String(e, "episode_num"), out var n) ? n : 0)
                                .DefaultIfEmpty(0).Max().ToString(CultureInfo.InvariantCulture).Length);
                            var idx = 0;
                            foreach (var episode in season.Value.EnumerateArray())
                            {
                                ct.ThrowIfCancellationRequested();
                                idx++;
                                var epId = JsonUtil.String(episode, "id", JsonUtil.String(episode, "stream_id"));
                                if (!IsValidId(epId)) throw new MediaWarningException(
                                    "o STRM " + WarningText.Episode(JsonUtil.String(item, "name"), JsonUtil.String(episode, "title"), seasonNumber,
                                        int.TryParse(JsonUtil.String(episode, "episode_num"), out var invalidNumber) ? invalidNumber : idx),
                                    "não foi criado", new MediaInputException("O provedor não informou um identificador válido para este episódio."));
                                var number = int.TryParse(JsonUtil.String(episode, "episode_num"), out var n) ? n : idx;
                                var title = PathRules.SafeName(JsonUtil.String(episode, "title"), "Episodio");
                                var filename = $"S{seasonNumber:00}E{number.ToString("D" + episodeWidth, CultureInfo.InvariantCulture)} - {title}";
                                var ext = JsonUtil.String(episode, "container_extension", "mp4");
                                var episodePath = await writer.EnqueueAsync(folder, filename, epId, api.StreamUrl("series", epId, ext), ct, WarningText.Episode(JsonUtil.String(item, "name", name), JsonUtil.String(episode, "title", title), seasonNumber, number));
                                await metadata.EpisodeAsync(episodePath, episode, JsonUtil.String(item, "name"), seasonNumber, number, settings.SeriesImages, settings.SeriesNfo, ct);
                                if (baseFolder == paths[0]) Update(s => s.Episodes++);
                            }
                        }
                    }
                    Update(s => s.Series++);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    IncrementError("a organização da série \"" + WarningText.Name(JsonUtil.String(item, "name")) + "\"; alguns STRMs/NFOs podem não ter sido criados", ex);
                    // Interrompe a consulta desta execução sem remover nenhum .strm já existente ou criado.
                    throw new InvalidOperationException($"Não foi possível organizar a série {id}; os arquivos .strm existentes permanecem salvos.", ex);
                }
                finally { Update(s => s.SeriesProcessed++); }
            });
        var summary = Snapshot();
        Say($"Séries analisadas: {summary.SeriesProcessed}/{entries.Length}; aguardando finalização das gravações pendentes.");
    }

    private static bool IsValidId(string s) => s.Length > 0 && s.Length < 32 && s.All(char.IsAsciiDigit);

    private void ExistingFileSkipped(string filepath)
    {
        int skipped;
        lock (_gate)
        {
            _status.FilesReused++;
            skipped = _status.FilesReused;
        }
        // Não gera milhares de mensagens durante um catálogo grande. O contador é sempre atualizado.
        if (skipped <= 6)
            Say($"Ignorado (já existe, preservado): {Path.GetFileName(filepath)}");
        else if (skipped % 2000 == 0)
            Say($"Arquivos .strm existentes ignorados até agora: {skipped}.");
    }

}
