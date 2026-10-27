using System.Text.Json.Serialization;

namespace XtreamStrmOrganizer;

public sealed class ConnectionSettings
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Minha lista";
    // Primeiro perfil importado da v1.3 preserva a mesma pasta da biblioteca anterior.
    public bool LegacyRoot { get; set; }
    public string FolderName { get; set; } = "";
    public bool AutoUpdate { get; set; }
    public int IntervalHours { get; set; } = 24;
    // Horário de referência, HH:mm, no fuso local do servidor Docker.
    public string RunTimeLocal { get; set; } = "";
    public bool AutoLive { get; set; } = true;
    public bool AutoMovies { get; set; } = true;
    public bool AutoSeries { get; set; } = true;
    // Null preserves the previous behavior: inherit manual metadata options.
    public bool? AutoLiveImages { get; set; }
    public bool? AutoLiveNfo { get; set; }
    public bool? AutoMovieImages { get; set; }
    public bool? AutoMovieNfo { get; set; }
    public bool? AutoSeriesImages { get; set; }
    public bool? AutoSeriesNfo { get; set; }
    public DateTimeOffset? NextRunUtc { get; set; }
    public DateTimeOffset? LastRunUtc { get; set; }
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool Live { get; set; } = true;
    public bool Movies { get; set; } = true;
    public bool Series { get; set; } = true;
    public string LiveExtension { get; set; } = "ts";
    public bool LiveImages { get; set; }
    public bool LiveNfo { get; set; }
    public bool MovieImages { get; set; }
    public bool MovieNfo { get; set; }
    public bool SeriesImages { get; set; }
    public bool SeriesNfo { get; set; }
    public int SeriesConcurrency { get; set; } = 8;
}

public sealed class PublicSettings
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Minha lista";
    // Primeiro perfil importado da v1.3 preserva a mesma pasta da biblioteca anterior.
    public bool LegacyRoot { get; set; }
    public string FolderName { get; set; } = "";
    public bool AutoUpdate { get; set; }
    public int IntervalHours { get; set; } = 24;
    // Horário de referência, HH:mm, no fuso local do servidor Docker.
    public string RunTimeLocal { get; set; } = "";
    public bool AutoLive { get; set; } = true;
    public bool AutoMovies { get; set; } = true;
    public bool AutoSeries { get; set; } = true;
    public bool AutoLiveImages { get; set; }
    public bool AutoLiveNfo { get; set; }
    public bool AutoMovieImages { get; set; }
    public bool AutoMovieNfo { get; set; }
    public bool AutoSeriesImages { get; set; }
    public bool AutoSeriesNfo { get; set; }
    public DateTimeOffset? NextRunUtc { get; set; }
    public DateTimeOffset? LastRunUtc { get; set; }
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string? NextRunServer { get; set; }
    public string? LastRunServer { get; set; }
    public string ServerTimeZone { get; set; } = "";
    public bool PasswordSaved { get; set; }
    public bool Live { get; set; }
    public bool Movies { get; set; }
    public bool Series { get; set; }
    public string LiveExtension { get; set; } = "ts";
    public bool LiveImages { get; set; }
    public bool LiveNfo { get; set; }
    public bool MovieImages { get; set; }
    public bool MovieNfo { get; set; }
    public bool SeriesImages { get; set; }
    public bool SeriesNfo { get; set; }
    public int SeriesConcurrency { get; set; } = 8;
}

public sealed class SyncStatus
{
    public string ProfileName { get; set; } = "";
    public bool Automatic { get; set; }
    public bool Running { get; set; }
    public string Phase { get; set; } = "Aguardando";
    public int Channels { get; set; }
    public int Movies { get; set; }
    public int Series { get; set; }
    public int Episodes { get; set; }
    public int Errors { get; set; }
    public int SeriesTotal { get; set; }
    public int SeriesProcessed { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int SeriesCached { get; set; }
    public int SeriesFetched { get; set; }
    public int FilesReused { get; set; }
    public int FilesWritten { get; set; }
    public int FilesPending { get; set; }
    public int WriteWorkers { get; set; }
    public int RequestsRetried { get; set; }
    public double? EtaSeconds { get; set; }
    public double? SeriesPerSecond { get; set; }
    public DateTimeOffset? SeriesStartedAt { get; set; }
    public string[] Messages { get; set; } = [];
}
