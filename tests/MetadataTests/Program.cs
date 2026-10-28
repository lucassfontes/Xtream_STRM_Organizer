using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using XtreamStrmOrganizer;

static class Tests
{
    static int assertions;
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();
    static async Task Run(Organizer organizer, string id, bool force = false, bool automatic = false)
    {
        Check(organizer.Start(force, id, automatic), "Start");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var previousPercent = 0;
        while (organizer.Snapshot().Running)
        {
            var progress = organizer.Snapshot();
            Check(progress.ProgressPercent >= previousPercent, "progress never regresses within a run");
            Check(progress.Completed || progress.ProgressPercent < 100, "100 only after completion");
            Check(progress.ItemsProcessed <= progress.ItemsTotal, "processed count bounded by fixed catalog total");
            previousPercent = progress.ProgressPercent;
            await Task.Delay(5, timeout.Token);
        }
        var finished = organizer.Snapshot();
        Check(finished.Completed && finished.ProgressPercent == 100 && finished.FilesPending == 0, "100 only after queue drained");
        Check(finished.ItemsProcessed == finished.ItemsTotal, "all selected catalog items processed");
        Check(organizer.Snapshot().Phase == "Concluído", string.Join("\n", organizer.Snapshot().Messages));
    }
    static string[] Files(string folder, string pattern) => Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
    static Dictionary<string, string> Hashes(string folder) => Files(folder, "*").ToDictionary(x => x,
        x => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(x))));
    public static async Task Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--probe")
        {
            try { LibraryAccess.Verify(args[1], true, true, true, CancellationToken.None); }
            catch (MediaInputException ex) { Console.Error.WriteLine(ex.Reason); Environment.ExitCode = 2; }
            return;
        }
        var root = Path.Combine(Path.GetTempPath(), "xtream-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Check(new SyncStatus { Running = true }.ProgressIndeterminate, "unknown catalog uses indeterminate progress");
            Check(new SyncStatus { Running = true, CatalogReady = true, ItemsTotal = 10, ItemsProcessed = 5 }.ProgressPercent == 50, "actual catalog ratio");
            Check(new SyncStatus { Running = true, CatalogReady = true, ItemsTotal = 10, ItemsProcessed = 10, FilesPending = 1 }.ProgressPercent == 99, "pending writer cannot show 100");
            Check(new SyncStatus { CatalogReady = true, ItemsTotal = 10, ItemsProcessed = 10 }.ProgressPercent == 99, "cancelled/error never claims completed");
            Check(new SyncStatus { CatalogReady = true, Completed = true }.ProgressPercent == 100, "empty catalog completes");
            var probeRoot = Path.Combine(root, "probe");
            var nested = Path.Combine(probeRoot, "Series", "Exemplo", "Temporada 01");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "original.strm"), "preservar");
            var beforeProbe = Hashes(probeRoot);
            LibraryAccess.Verify(probeRoot, true, true, true, CancellationToken.None);
            Check(Hashes(probeRoot).SequenceEqual(beforeProbe), "Preflight preserves files and removes temporary probes");
            using (var probeCancelled = new CancellationTokenSource())
            {
                probeCancelled.Cancel();
                try { LibraryAccess.Verify(probeRoot, true, true, true, probeCancelled.Token); Check(false, "cancel required"); }
                catch (OperationCanceledException) { Check(true, "preflight cancelled"); }
            }
            var conflict = Path.Combine(root, "file-in-place-of-folder");
            File.WriteAllText(conflict, "original");
            try { LibraryAccess.Verify(conflict, true, true, true, CancellationToken.None); Check(false, "invalid folder required"); }
            catch (MediaInputException ex) { Check(ex.Reason.Contains("pasta") && ex.Reason.Contains("preservados"), "friendly preflight error"); }
            Check(File.ReadAllText(conflict) == "original", "conflicting file preserved");
            // Adult movie category is a sibling; migration preserves bytes and refuses conflicts.
            {
                var adultRoot = Path.Combine(root, "adult-migration");
                var oldAdult = Path.Combine(adultRoot, "Filmes", "FILMES ADULTOS");
                Directory.CreateDirectory(oldAdult);
                File.WriteAllText(Path.Combine(oldAdult, "Exemplo.strm"), "stream-original");
                File.WriteAllText(Path.Combine(oldAdult, "Exemplo.nfo"), "nfo-original");
                File.WriteAllBytes(Path.Combine(oldAdult, "Exemplo-poster.jpg"), [1, 2, 3]);
                var originalBytes = Directory.GetFiles(oldAdult).ToDictionary(x => Path.GetFileName(x)!, File.ReadAllBytes);
                MovieFolders.Migrate(adultRoot, _ => { }, CancellationToken.None);
                var newAdult = Path.Combine(adultRoot, MovieFolders.Adults);
                Check(!Directory.Exists(oldAdult), "old adult folder removed after move");
                foreach (var pair in originalBytes)
                    Check(File.ReadAllBytes(Path.Combine(newAdult, pair.Key!)).SequenceEqual(pair.Value), "moved bytes preserved");
                MovieFolders.Migrate(adultRoot, _ => { }, CancellationToken.None);
                Check(Directory.GetFiles(newAdult).Length == 3, "adult migration idempotent");
                Check(MovieFolders.CategoryPath(adultRoot, "Filmes", " filmes   adultos ") == newAdult, "adult category normalized");
                Check(MovieFolders.CategoryPath(adultRoot, "TV ao vivo", "FILMES ADULTOS") != newAdult, "TV routing unchanged");
                Check(MovieFolders.CategoryPath(adultRoot, "Filmes", "FILMES ADULTOS OUTROS") != newAdult, "no broad adult filtering");
                Directory.CreateDirectory(oldAdult);
                File.WriteAllText(Path.Combine(oldAdult, "Exemplo.strm"), "conflicting-source");
                File.WriteAllText(Path.Combine(oldAdult, "Outro.strm"), "must-stay");
                try { MovieFolders.Migrate(adultRoot, _ => { }, CancellationToken.None); Check(false, "conflict must stop migration"); }
                catch (MediaInputException ex) { Check(ex.Reason.Contains("Nenhum arquivo foi sobrescrito"), "clear conflict explanation"); }
                Check(File.ReadAllText(Path.Combine(oldAdult, "Outro.strm")) == "must-stay", "no partial move on detected conflict");
                Check(File.ReadAllText(Path.Combine(newAdult, "Exemplo.strm")) == "stream-original", "destination conflict preserved");
                File.Move(Path.Combine(oldAdult, "Exemplo.strm"), Path.Combine(oldAdult, "Renomeado.strm"));
                MovieFolders.Migrate(adultRoot, _ => { }, CancellationToken.None);
                Check(!Directory.Exists(oldAdult) && File.Exists(Path.Combine(newAdult, "Outro.strm")), "merge existing destination without overwrite");
            }
            foreach (var autoAdult in new[] { false, true })
            {
                var adultDir = Path.Combine(root, "adult-run-" + autoAdult);
                var adultConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                    ["CONFIG_FILE"] = Path.Combine(adultDir, "config/config.json"), ["OUTPUT_ROOT"] = Path.Combine(adultDir, "media")
                }).Build();
                var adultStore = new SettingsStore(adultConfig);
                var adultProfile = adultStore.Save(new ConnectionSettings {
                    Name = "Teste", Url = "http://fixture", Username = "user", Password = "secret",
                    Live = false, Series = false, Movies = true, MovieImages = true, MovieNfo = true,
                    AutoMovies = true, AutoLive = false, AutoSeries = false, AutoMovieImages = true, AutoMovieNfo = true
                });
                var adultFixture = new Fixture { AdultCategory = true };
                var adultOrganizer = new Organizer(adultStore, adultFixture, adultConfig);
                await Run(adultOrganizer, adultProfile.Id, automatic: autoAdult);
                var adultOutput = Path.Combine(adultDir, "media", "Teste");
                var adultDestination = Path.Combine(adultOutput, MovieFolders.Adults);
                Check(Files(adultDestination, "*.strm").Length == 2, "adult STRMs beside Filmes, manual and scheduled");
                Check(Files(adultDestination, "*.nfo").Length == 2 && Files(adultDestination, "*.png").Length == 2, "adult sidecars follow movie options");
                Check(!Directory.Exists(Path.Combine(adultOutput, "Filmes", MovieFolders.Adults)), "old location not recreated");
                var adultHashes = Hashes(adultDestination);
                var adultRequests = adultFixture.ImageRequests;
                await Run(adultOrganizer, adultProfile.Id, automatic: autoAdult);
                Check(Hashes(adultDestination).OrderBy(x => x.Key).SequenceEqual(adultHashes.OrderBy(x => x.Key)), "adult files not overwritten");
                Check(adultFixture.ImageRequests == adultRequests, "adult artwork not downloaded twice");
            }
            foreach (var gateAction in new[] { "get_live_streams", "/image", "empty" })
            {
                var progressDir = Path.Combine(root, "progress-" + gateAction.Replace("/", ""));
                var progressConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                    ["CONFIG_FILE"] = Path.Combine(progressDir, "config/config.json"), ["OUTPUT_ROOT"] = Path.Combine(progressDir, "media")
                }).Build();
                var progressStore = new SettingsStore(progressConfig);
                var progressProfile = progressStore.Save(new ConnectionSettings {
                    Name = "Teste", Url = "http://fixture", Username = "user", Password = "secret",
                    Live = true, Movies = false, Series = false, LiveImages = true
                });
                var progressFixture = new Fixture { GateAction = gateAction, EmptyCatalog = gateAction == "empty" };
                var progressOrganizer = new Organizer(progressStore, progressFixture, progressConfig);
                if (gateAction == "empty") { await Run(progressOrganizer, progressProfile.Id); continue; }
                Check(progressOrganizer.Start(false, progressProfile.Id), "progress gated start");
                await progressFixture.GateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var during = progressOrganizer.Snapshot();
                Check(during.Running && !during.Completed && during.ProgressPercent == 0, "no fabricated progress while waiting");
                Check(during.ProgressIndeterminate == (gateAction == "get_live_streams"), "indeterminate only before catalog known");
                if (gateAction == "/image")
                {
                    Check(during.ItemsTotal == 1 && during.ItemsProcessed == 0, "image must finish before item counted");
                    progressOrganizer.Cancel();
                }
                progressFixture.Gate.TrySetResult();
                using var progressTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (progressOrganizer.Snapshot().Running) await Task.Delay(5, progressTimeout.Token);
                var after = progressOrganizer.Snapshot();
                Check(gateAction == "/image" ? !after.Completed && after.ProgressPercent < 100 : after.Completed && after.ProgressPercent == 100,
                    "cancel does not show 100; success does");
            }
            // Full integration matrix: settings persistence -> Organizer -> fake Xtream -> real filesystem.
            for (var mask = 0; mask < 64; mask++)
            {
                var dir = Path.Combine(root, mask.ToString());
                var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                    ["CONFIG_FILE"] = Path.Combine(dir, "config/config.json"), ["OUTPUT_ROOT"] = Path.Combine(dir, "media")
                }).Build();
                var store = new SettingsStore(config);
                var settings = new ConnectionSettings { Name = "Teste", Url = "http://fixture", Username = "user", Password = "secret",
                    LiveImages = (mask & 1) != 0, LiveNfo = (mask & 2) != 0, MovieImages = (mask & 4) != 0,
                    MovieNfo = (mask & 8) != 0, SeriesImages = (mask & 16) != 0, SeriesNfo = (mask & 32) != 0,
                    AutoUpdate = true, RunTimeLocal = "03:00"
                };
                var profile = store.Save(settings);
                var restored = new SettingsStore(config);
                Check(restored.Public(profile.Id).SeriesNfo == settings.SeriesNfo, "Public persistence");
                settings.Password = "";
                restored.Save(settings);
                Check(restored.Read(profile.Id).Password == "secret", "Saved password retained");
                var fixture = new Fixture();
                var organizer = new Organizer(restored, fixture, config);
                await Run(organizer, profile.Id);
                var media = Path.Combine(dir, "media", "Teste");
                var live = Path.Combine(media, "TV ao vivo");
                var movies = Path.Combine(media, "Filmes");
                var series = Path.Combine(media, "Series");
                Check(Files(media, "*.strm").Length == 6, "STRM count and collision handling");
                Check(Files(live, "*.png").Length == (settings.LiveImages ? 1 : 0), "Live images independent");
                Check(Files(live, "*.nfo").Length == (settings.LiveNfo ? 1 : 0), "Live NFO independent");
                Check(Files(movies, "*.png").Length == (settings.MovieImages ? 2 : 0), "Movie images independent");
                Check(Files(movies, "*.nfo").Length == (settings.MovieNfo ? 2 : 0), "Movie NFO independent");
                Check(Files(series, "*.png").Length == (settings.SeriesImages ? 6 : 0), "Series/season/episode images");
                Check(Files(series, "*.nfo").Length == (settings.SeriesNfo ? 6 : 0), "Series/season/episode NFOs");
                Check(fixture.VodRequests == (settings.MovieImages || settings.MovieNfo ? 2 : 0), "VOD details only if requested");
                foreach (var path in Files(media, "*.nfo"))
                {
                    var xml = XDocument.Load(path);
                    Check(xml.Root != null, "Valid XML");
                    if (xml.Root!.Name == "episodedetails")
                    {
                        Check(File.Exists(Path.ChangeExtension(path, ".strm")), "Episode NFO adjacent to STRM");
                        Check(xml.Root.Element("plot")!.Value == "História & ação <teste>", "Escaped plot");
                        Check(xml.Root.Element("runtime")!.Value == "2", "Duration seconds converted");
                        Check(xml.Root.Element("showtitle")!.Value == "Série Teste", "Show title");
                    }
                }
                // Existing custom files must remain byte-identical, including arbitrary STRM content.
                var strm = Files(media, "*.strm")[0];
                File.WriteAllText(strm, "http://custom.example/preserved\n");
                var before = Hashes(media);
                var imagesBefore = fixture.ImageRequests;
                var vodBefore = fixture.VodRequests;
                await Run(organizer, profile.Id);
                Check(before.All(kv => File.ReadAllBytes(kv.Key).Length > 0 && Hashes(media)[kv.Key] == kv.Value), "No overwrite");
                Check(fixture.ImageRequests == imagesBefore, "No repeat image downloads");
                Check(fixture.VodRequests == vodBefore, "Skip VOD details when complete");
                Check(organizer.Snapshot().SeriesCached == 1, "Metadata retained in cache");
                // Fill absent sidecars around existing STRMs; all six options can be enabled later.
                if (mask == 0)
                {
                    settings.LiveImages = settings.LiveNfo = settings.MovieImages = settings.MovieNfo = settings.SeriesImages = settings.SeriesNfo = true;
                    restored.Save(settings);
                    await Run(organizer, profile.Id);
                    Check(Files(media, "*.nfo").Length == 9 && Files(media, "*.png").Length == 9, "Backfill existing STRMs");
                    Check(File.ReadAllText(strm) == "http://custom.example/preserved\n", "Backfill preserves STRM");
                }
                if (mask == 63)
                {
                    // force refresh/automatic must still never overwrite or redownload art.
                    await Run(organizer, profile.Id, true, true);
                    Check(fixture.ImageRequests == imagesBefore, "Automatic/force preservation");
                    var thumb = Files(series, "*-thumb.png")[0];
                    File.Delete(thumb); // Deliberately simulate a missing sidecar, only in the test fixture.
                    await Run(organizer, profile.Id);
                    Check(File.Exists(thumb) && fixture.ImageRequests == imagesBefore + 1, "Only missing image fetched");
                    // Legacy cache entries have no Details: one refresh upgrades them without losing episodes.
                    var cache = new SeriesCache(Path.Combine(dir, "config/config.json"), restored.Read(profile.Id));
                    var old = cache.Read("301")!;
                    cache.Save("301", old.SourceTag, old.Episodes);
                    await Run(organizer, profile.Id);
                    Check(organizer.Snapshot().SeriesFetched == 1 && cache.Read("301")!.Details.HasValue, "Legacy cache upgrade");
                    var ep = XDocument.Load(Files(series, "*E100*.nfo").Single());
                    Check(ep.Root!.Element("episode")!.Value == "100", "Episode 100");
                    Check(Files(series, "*S00E01*.strm").Length == 1, "Season zero");
                }
            }
            Console.WriteLine("PASS: all 64 combinations, persistence, Xtream, cache, collisions, backfill and repeat runs.");

            // Separate scheduled choices must win over the opposite manual choices.
            var optionNames = new[] { "LiveImages", "LiveNfo", "MovieImages", "MovieNfo", "SeriesImages", "SeriesNfo" };
            for (var mask = 0; mask < 64; mask++)
            {
                var dir = Path.Combine(root, "schedule-" + mask);
                var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                    ["CONFIG_FILE"] = Path.Combine(dir, "config/config.json"), ["OUTPUT_ROOT"] = Path.Combine(dir, "media")
                }).Build();
                var store = new SettingsStore(config);
                var settings = new ConnectionSettings { Name = "Agenda", Url = "http://fixture", Username = "user", Password = "secret", AutoUpdate = true };
                for (int i = 0; i < optionNames.Length; i++)
                {
                    typeof(ConnectionSettings).GetProperty(optionNames[i])!.SetValue(settings, (mask & (1 << i)) == 0);
                    typeof(ConnectionSettings).GetProperty("Auto" + optionNames[i])!.SetValue(settings, (mask & (1 << i)) != 0);
                }
                var profile = store.Save(settings);
                var next = profile.NextRunUtc;
                var reloaded = new SettingsStore(config);
                var read = reloaded.Read(profile.Id);
                var publicProfile = reloaded.Public(profile.Id);
                for (int i = 0; i < optionNames.Length; i++)
                {
                    Check((bool)typeof(PublicSettings).GetProperty("Auto" + optionNames[i])!.GetValue(publicProfile)! == ((mask & (1 << i)) != 0), "Scheduled persistence");
                    Check((bool)typeof(ConnectionSettings).GetProperty(optionNames[i])!.GetValue(read)! == ((mask & (1 << i)) == 0), "Manual choices unchanged");
                }
                // Legacy clients omitting schedule fields do not erase saved schedule choices.
                foreach (var name in optionNames) typeof(ConnectionSettings).GetProperty("Auto" + name)!.SetValue(read, null);
                reloaded.Save(read);
                Check(reloaded.Public(profile.Id).NextRunUtc == next, "Saving choices retains next run");
                var fixture = new Fixture();
                var organizer = new Organizer(reloaded, fixture, config);
                if (mask == 63)
                {
                    // Exercise the actual BackgroundService with an overdue persisted schedule.
                    var connectionFile = Path.Combine(dir, "config/connections.json");
                    var profiles = JsonSerializer.Deserialize<List<ConnectionSettings>>(File.ReadAllText(connectionFile))!;
                    profiles[0].NextRunUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
                    File.WriteAllText(connectionFile, JsonSerializer.Serialize(profiles));
                    var worker = new AutoUpdateService(reloaded, organizer, Microsoft.Extensions.Logging.Abstractions.NullLogger<AutoUpdateService>.Instance);
                    await worker.StartAsync(default);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    while (organizer.Snapshot().FinishedAt is null) await Task.Delay(5, timeout.Token);
                    await worker.StopAsync(default);
                    worker.Dispose();
                    Check(organizer.Snapshot().Phase == "Concluído", "Scheduled worker completed");
                    Check(reloaded.Read(profile.Id).LastRunUtc.HasValue && reloaded.Read(profile.Id).NextRunUtc > DateTimeOffset.UtcNow, "Worker persists next run");
                }
                else await Run(organizer, profile.Id, true, true);
                var media = Path.Combine(dir, "media/Agenda");
                var folders = new[] { "TV ao vivo", "Filmes", "Series" };
                var counts = new[] { 1, 2, 6 };
                for (int i = 0; i < 3; i++)
                {
                    Check(Files(Path.Combine(media, folders[i]), "*.png").Length == ((mask & (1 << (i * 2))) != 0 ? counts[i] : 0), "Scheduled image choice");
                    Check(Files(Path.Combine(media, folders[i]), "*.nfo").Length == ((mask & (1 << (i * 2 + 1))) != 0 ? counts[i] : 0), "Scheduled NFO choice");
                }
                Check(organizer.Snapshot().Automatic, "Run marked automatic");
                for (int i = 0; i < optionNames.Length; i++)
                    Check((bool)typeof(ConnectionSettings).GetProperty(optionNames[i])!.GetValue(reloaded.Read(profile.Id))! == ((mask & (1 << i)) == 0), "Automatic execution preserves manual settings");
            }
            Console.WriteLine("PASS: 64 separate schedule combinations and actual background worker, including persistence/migration.");
            var edge = Path.Combine(root, "edge");
            Directory.CreateDirectory(edge);
            var handler = new Fixture();
            var errors = new List<Exception>();
            var writer = new MetadataWriter(handler.CreateClient("art"), errors.Add);
            var item = Json("""{"name":"Teste","stream_icon":"http://fixture/image"}""");
            var testPath = Path.Combine(edge, "Teste.strm");
            await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => writer.FlatAsync(testPath, item, default, true, true, default)));
            Check(handler.ImageRequests == 1, "Concurrent same image downloaded once");
            Check(Files(edge, "*.tmp").Length == 0, "No temporary files after concurrent writes");
            // Recognize existing alternate extension before contacting provider.
            File.Move(Path.Combine(edge, "Teste-poster.png"), Path.Combine(edge, "Teste-poster.jpeg"));
            await writer.FlatAsync(testPath, item, default, true, true, default);
            Check(handler.ImageRequests == 1, "Existing alternate extension preserved");
            handler.BadImage = true;
            await writer.FlatAsync(Path.Combine(edge, "Bad.strm"), item, default, true, true, default);
            Check(errors.Count == 1 && !Files(edge, "Bad-poster.*").Any(), "HTML rejected without final image");
            Check(File.Exists(Path.Combine(edge, "Bad.nfo")), "Artwork failure does not block NFO");
            handler.BadImage = false;
            await writer.FlatAsync(Path.Combine(edge, "Bad.strm"), item, default, true, false, default);
            Check(File.Exists(Path.Combine(edge, "Bad-poster.png")), "Failed image retry succeeds");
            var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await writer.FlatAsync(Path.Combine(edge, "Cancel.strm"), item, default, true, true, cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
            Check(!Files(edge, "Cancel*").Any(), "Cancellation leaves no output");
            await writer.FlatAsync(Path.Combine(edge, "Missing.strm"), Json("""{"name":"Missing"}"""), default, true, true, default);
            Check(!Files(edge, "Missing-poster.*").Any(), "Missing artwork is not fabricated");
            Check(errors.Count == 2 && WarningText.Format("", errors[^1]).Contains("não informou uma imagem"), "Missing artwork warning");
            // Friendly warnings: HTTP status, media identity, no raw secret URL.
            foreach (var code in new[] { 401, 403, 404, 429, 500, 503 })
            {
                var message = WarningText.Format("", new MediaWarningException("a capa do filme \"Meu Filme\"", "não foi baixada",
                    new HttpRequestException("https://provider/user/SECRET", null, (HttpStatusCode)code)));
                Check(message.Contains("Meu Filme") && message.Contains("HTTP " + code) && !message.Contains("SECRET"), "Safe HTTP explanation");
            }
            Check(WarningText.Reason(new TaskCanceledException()).Contains("tempo limite"), "Timeout explanation");
            Check(WarningText.Reason(new UnauthorizedAccessException()).Contains("permissão"), "Permissions explanation");
            var warningDir = Path.Combine(root, "warning-details");
            var warningErrors = new List<Exception>();
            var warningWriter = new MetadataWriter(new HttpClient(new FailingImageHandler()), warningErrors.Add);
            await warningWriter.FlatAsync(Path.Combine(warningDir,"Filme.strm"), Json("""{"name":"Meu Filme","stream_icon":"http://fixture/image"}"""), default, true, false, default);
            await warningWriter.SeriesAsync(Path.Combine(warningDir,"Serie"), Json("""{"name":"Minha Série","cover":"http://fixture/image"}"""), default, true, false, default);
            await warningWriter.SeasonAsync(Path.Combine(warningDir,"Serie/Temporada 02"), 2, Json("""{"info":{"cover":"http://fixture/image"}}"""), true, false, default, "Minha Série");
            await warningWriter.EpisodeAsync(Path.Combine(warningDir,"Serie/Temporada 02/ep.strm"), Json("""{"title":"O início","info":{"movie_image":"http://fixture/image"}}"""), "Minha Série", 2, 3, true, false, default);
            var readable = warningErrors.Select(e => WarningText.Format("", e)).ToArray();
            Check(readable.Length == 4 && readable.All(x=>x.Contains("HTTP 404")), "Four real failed image requests");
            Check(readable[0].Contains("filme \"Meu Filme\""), "Movie title");
            Check(readable[1].Contains("série \"Minha Série\""), "Series title");
            Check(readable[2].Contains("temporada 02") && readable[2].Contains("Minha Série"), "Season context");
            Check(readable[3].Contains("miniatura") && readable[3].Contains("S02E03") && readable[3].Contains("Minha Série"), "Episode context");

            // Trigger real filesystem failures in both STRM and NFO, plus HTTP artwork failure.
            var warningConfigPath = Path.Combine(root, "warning-integration/config/config.json");
            var warningOutput = Path.Combine(root, "warning-integration/media");
            var warningConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["CONFIG_FILE"] = warningConfigPath, ["OUTPUT_ROOT"] = warningOutput
            }).Build();
            var warningStore = new SettingsStore(warningConfig);
            var wp = warningStore.Save(new ConnectionSettings { Name="Avisos",Url="http://fixture",Username="user",Password="secret",
                Live=true,Movies=false,Series=false,LiveImages=true,LiveNfo=true });
            var channelDir = Path.Combine(warningOutput,"Avisos/TV ao vivo/Geral");
            Directory.CreateDirectory(Path.Combine(channelDir,"Canal.strm"));
            Directory.CreateDirectory(Path.Combine(channelDir,"Canal.nfo"));
            var failingFactory = new Fixture { BadImage=true };
            var warningOrganizer = new Organizer(warningStore,failingFactory,warningConfig);
            Check(warningOrganizer.Start(false,wp.Id), "Start failure fixture");
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                while(warningOrganizer.Snapshot().Running) await Task.Delay(5,deadline.Token);
            Check(warningOrganizer.Snapshot().Phase == "Concluído com avisos", "Failure run completes");
            var log = File.ReadAllText(warningOrganizer.WarningLogPath);
            Check(log.Contains("o STRM do canal \"Canal\" não foi criado"), "STRM failure in persistent log");
            Check(log.Contains("o NFO do canal \"Canal\" não foi criado"), "NFO failure in persistent log");
            Check(log.Contains("a capa do canal \"Canal\" não foi baixada"), "Artwork failure in persistent log");
            Check(!log.Contains("secret") && !log.Contains("http://"), "Log hides credential URLs");
            var persisted = new WarningLog(warningConfigPath);
            Check(persisted.Append("Aviso: teste após reiniciar."), "Append after restart");
            Check(File.ReadAllText(persisted.FilePath).StartsWith(log), "Previous warning history retained");
            var beforeLines = File.ReadAllLines(persisted.FilePath).Length;
            var appendResults = await Task.WhenAll(Enumerable.Range(0,100).Select(i=>Task.Run(()=>persisted.Append("Aviso concorrente " + i))));
            Check(appendResults.All(x=>x), "Concurrent append");
            Check(File.ReadAllLines(persisted.FilePath).Length == beforeLines+100, "Concurrent log lines intact");
            var invalidLogRoot = Path.Combine(root,"blocked-log");
            File.WriteAllText(invalidLogRoot,"block directory creation");
            Check(!new WarningLog(Path.Combine(invalidLogRoot,"config.json")).Append("Aviso"), "Log failure reported without crashing");
            var invalidProfile = warningStore.Save(new ConnectionSettings { Name="ID inválido",Url="http://fixture",Username="user",Password="secret",Live=true,Movies=false,Series=false });
            var invalidOrganizer = new Organizer(warningStore,new Fixture { InvalidLiveId=true },warningConfig);
            Check(invalidOrganizer.Start(false,invalidProfile.Id), "Invalid ID starts");
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                while(invalidOrganizer.Snapshot().Running) await Task.Delay(5,deadline.Token);
            Check(File.ReadAllText(invalidOrganizer.WarningLogPath).Contains("identificador válido para este item"), "Invalid STRM identifier logged with context");
            Console.WriteLine("PASS: friendly warnings, actual failed STRM/NFO writes, HTTP artwork failures, log history and concurrency.");
            Console.WriteLine($"PASS: {assertions} assertions. No real credentials or provider required.");
        }
        finally { Directory.Delete(root, true); }
    }
}

sealed class Fixture : HttpMessageHandler, IHttpClientFactory
{
    public int ImageRequests;
    public int VodRequests;
    public bool BadImage;
    public bool InvalidLiveId;
    public bool AdultCategory;
    public bool EmptyCatalog;
    public string? GateAction;
    public TaskCompletionSource Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource GateEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private async Task<HttpResponseMessage> Deliver(HttpResponseMessage response, string action, CancellationToken token)
    {
        if (GateAction == action)
        {
            GateEntered.TrySetResult();
            await Gate.Task.WaitAsync(token);
        }
        return response;
    }
    public HttpClient CreateClient(string name) => new(this, false);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var uri = request.RequestUri!;
        if (uri.AbsolutePath == "/image")
        {
            Interlocked.Increment(ref ImageRequests);
            return Deliver(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(BadImage ? Encoding.UTF8.GetBytes("<html>Error</html>") :
                    Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aU1sAAAAASUVORK5CYII="))
            }, "/image", token);
        }
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (query["username"] != "user" || query["password"] != "secret")
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var action = query["action"] ?? "";
        if (action == "get_vod_info") Interlocked.Increment(ref VodRequests);
        var json = action switch
        {
            "" => """{"user_info":{"auth":1,"status":"Active"}}""",
            "get_live_categories" or "get_vod_categories" or "get_series_categories" => """[{"category_id":"1","category_name":"Geral"}]""",
            "get_live_streams" => """[{"stream_id":101,"name":"Canal","category_id":"1","stream_icon":"http://fixture/image"}]""",
            "get_vod_streams" => """[{"stream_id":201,"name":"Filme","category_id":"1"},{"stream_id":202,"name":"Filme","category_id":"1"}]""",
            "get_vod_info" => """{"info":{"name":"Filme","plot":"Cinema & arte","movie_image":"http://fixture/image","releasedate":"2020-01-02","tmdb_id":123,"cast":"Ana, João"}}""",
            "get_series" => """[{"series_id":301,"name":"Série Teste","category_id":"1","last_modified":"1","cover":"http://fixture/image"}]""",
            "get_series_info" => """
            {"info":{"name":"Série Teste","plot":"Sinopse","cover":"http://fixture/image"},
             "seasons":[{"season_number":0,"name":"Especiais","cover":"http://fixture/image"},{"season_number":"1","name":"Temporada 1","cover":"http://fixture/image"}],
             "episodes":{"0":[{"id":4000,"episode_num":1,"title":"Especial","info":{"plot":"História & ação <teste>","duration_secs":120,"movie_image":"http://fixture/image"}}],
                         "1":[{"id":4001,"episode_num":"1","title":"Início","info":{"plot":"História & ação <teste>","duration_secs":"120","movie_image":"http://fixture/image"}},
                              {"id":"4100","episode_num":100,"title":"Final","info":{"plot":"História & ação <teste>","duration_secs":120,"movie_image":"http://fixture/image"}}]}}
            """,
            _ => throw new Exception("Unexpected action: " + action)
        };
        if (EmptyCatalog && action is "get_live_streams" or "get_vod_streams" or "get_series") json = "[]";
        if (AdultCategory && action == "get_vod_categories") json = json.Replace("Geral", "FILMES ADULTOS");
        if (InvalidLiveId && action == "get_live_streams") json = json.Replace("\"stream_id\":101", "\"stream_id\":\"invalid\"");
        return Deliver(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }, action, token);
    }
}


sealed class FailingImageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
}
