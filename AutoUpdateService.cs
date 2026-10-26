namespace XtreamStrmOrganizer;

// A agenda fica no volume persistente; o worker só dispara uma execução por vez.
public sealed class AutoUpdateService(SettingsStore settings, Organizer organizer, ILogger<AutoUpdateService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Agenda resumida é informada no log do Docker sem expor URL/credenciais Xtream.
        foreach (var p in settings.Profiles().Where(p => p.AutoUpdate))
            logger.LogInformation("Agenda {Profile}: intervalo {Hours}h, relógio {Time} ({Zone}), próxima execução {Next}",
                p.Name, p.IntervalHours, p.RunTimeLocal, p.ServerTimeZone, p.NextRunServer);
        // Uma execução inicial curta permite recuperar agendas vencidas após reiniciar o contêiner.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!organizer.Snapshot().Running)
                {
                    var next = settings.Due(DateTimeOffset.UtcNow).FirstOrDefault();
                    if (next is not null)
                    {
                        // Uma execução programada sempre consulta os episódios; arquivos STRM existentes continuam intocados.
                        if (organizer.Start(forceRefresh: true, profileId: next.Id, automatic: true))
                        {
                            settings.MarkScheduled(next.Id, DateTimeOffset.UtcNow);
                            logger.LogInformation("Atualização automática iniciada para {Profile} às {Now}. Próxima execução: {Next}",
                                next.Name, ScheduleClock.Display(DateTimeOffset.UtcNow),
                                settings.Public(next.Id).NextRunServer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Falha ao verificar agenda automática: {Type}", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
