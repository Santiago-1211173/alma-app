using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System;

namespace AlmaApp.WebApi.Common;

/// <summary>
/// Utilitário de fuso horário da aplicação. Assume Europe/Lisbon para parsing de horas sem offset.
/// Funciona em Windows e Linux (fallback para Local se não encontrar).
/// </summary>
public static class AppTime
{
    public static readonly TimeZoneInfo Tz =
        TryFind("Europe/Lisbon") ??
        TryFind("GMT Standard Time") ?? // Windows fallback (Lisboa/Londres)
        TimeZoneInfo.Local;

    private static TimeZoneInfo? TryFind(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return null; }
    }

    /// <summary>Converte uma hora local (sem Z) para UTC.</summary>
    public static DateTime ToUtcFromLocal(DateTime local)
    {
        if (local.Kind == DateTimeKind.Utc) return local;
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Tz);
    }

    /// <summary>Converte uma hora UTC para a hora local configurada.</summary>
    public static DateTime ToLocalFromUtc(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Tz);
    }
}
