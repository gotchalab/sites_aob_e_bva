namespace AOB.Admin.Services;

/// Conversao segura entre UTC (formato de storage/API) e hora local Portugal
/// para renderizacao e input no backoffice. Usar SEMPRE em vez de
/// <see cref="DateTime.ToLocalTime"/>/<see cref="DateTime.ToUniversalTime"/>,
/// que dependem do timezone do PROCESSO (VPS Linux corre em UTC). Sem isto,
/// datetime-local inputs sao gravados com offset errado (ex.: 23:59 escrito
/// pelo utilizador acaba como 23:59 UTC, aparecendo 00:59 do dia seguinte
/// para browsers em Lisboa no verao WEST +1).
public static class LisbonTime
{
    // Windows usa "GMT Standard Time"; Linux/macOS usam IANA "Europe/Lisbon".
    // O DST (WET/WEST) e tratado automaticamente pelo TimeZoneInfo.
    public static readonly TimeZoneInfo Tz = OperatingSystem.IsWindows()
        ? TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")
        : TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    /// UTC → hora local Portugal (para display ou pre-preencher inputs).
    public static DateTime FromUtc(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            : utc.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, Tz);
    }

    /// Hora local Portugal → UTC (para gravar em BD/enviar a API).
    /// Aceita DateTime Local, Unspecified ou Utc — em qualquer caso, trata o
    /// valor como se fosse hora local Portugal, exceto se ja for Utc.
    public static DateTime ToUtc(DateTime local)
    {
        if (local.Kind == DateTimeKind.Utc) return local;
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Tz);
    }
}
