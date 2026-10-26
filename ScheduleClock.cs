using System.Globalization;

namespace XtreamStrmOrganizer;

public static class ScheduleClock
{
    public static string ZoneName => TimeZoneInfo.Local.Id;

    public static string EffectiveTime(ConnectionSettings x)
    {
        if (!string.IsNullOrWhiteSpace(x.RunTimeLocal)) return x.RunTimeLocal;
        // Migração: conserva o horário de uma agenda já salva em v1.5.
        return x.NextRunUtc.HasValue ? TimeZoneInfo.ConvertTime(x.NextRunUtc.Value, TimeZoneInfo.Local)
            .ToString("HH:mm", CultureInfo.InvariantCulture) : "03:00";
    }

    public static bool IsValid(string? hhmm) => hhmm is { Length: 5 } &&
        TimeOnly.TryParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static string Display(DateTimeOffset? stamp) => stamp.HasValue
        ? TimeZoneInfo.ConvertTime(stamp.Value, TimeZoneInfo.Local).ToString("dd/MM/yyyy 'às' HH:mm:ss", CultureInfo.GetCultureInfo("pt-BR"))
        : "Não programada";

    public static DateTimeOffset NextAfter(DateTimeOffset nowUtc, int intervalHours, string hhmm)
    {
        if (intervalHours is not (12 or 24) || !IsValid(hhmm))
            throw new ArgumentException("Agendamento exige intervalo 12/24h e horário HH:mm válido.");
        var tz = TimeZoneInfo.Local;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz).DateTime;
        var at = TimeOnly.ParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture);
        // Referência diária. Em 12h: HH:mm e HH+12:mm. Em 24h: HH:mm.
        var localCandidate = nowLocal.Date.Add(at.ToTimeSpan());
        if (intervalHours == 12 && at.Hour >= 12)
            localCandidate = localCandidate.AddHours(-12);
        for (int i = 0; i < 8; i++)
        {
            var validLocal = localCandidate;
            // Quando o relógio avança no horário de verão, execute no primeiro minuto válido.
            for (int m = 0; tz.IsInvalidTime(validLocal) && m < 180; m++)
                validLocal = validLocal.AddMinutes(1);
            var offset = tz.IsAmbiguousTime(validLocal)
                ? tz.GetAmbiguousTimeOffsets(validLocal).Max()
                : tz.GetUtcOffset(validLocal);
            var candidateUtc = new DateTimeOffset(validLocal, offset).ToUniversalTime();
            if (candidateUtc > nowUtc) return candidateUtc;
            localCandidate = localCandidate.AddHours(intervalHours);
        }
        throw new InvalidOperationException("Não foi possível calcular a próxima execução.");
    }
}
