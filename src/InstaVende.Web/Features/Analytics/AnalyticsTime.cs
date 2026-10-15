namespace InstaVende.Web.Features.Analytics;

/// <summary>
/// Utilidades de tiempo para la analítica. Todas las fechas de la BD están en UTC; los "días" que ve el
/// comercio son días locales de su zona horaria (<c>Tenant.TimeZone</c>).
/// <para>
/// Estrategia de agrupación: las consultas agrupan en SQL por <b>hora UTC</b> relativa a un origen alineado a la
/// hora (<c>DATEDIFF(hour, @origen, CreatedAt)</c>) y aquí se asigna cada hora a su día local con la zona horaria
/// real. Así se respetan los cambios de horario (Chile, Paraguay) sin traer filas individuales a memoria:
/// como máximo llega una fila por hora con actividad.
/// </para>
/// </summary>
internal static class AnalyticsTime
{
    /// <summary>Trunca un instante UTC al inicio de su hora.</summary>
    public static DateTime FloorHour(DateTime utc)
        => new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerHour, DateTimeKind.Utc);

    /// <summary>Día local del negocio para un instante UTC.</summary>
    public static DateOnly LocalDay(DateTime utc, TimeZoneInfo tz)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz));

    /// <summary>
    /// Inicio (00:00 local) de <paramref name="day"/> expresado en UTC. Si la medianoche no existe por un
    /// cambio de horario, el día empieza en el primer instante válido (nunca lanza).
    /// </summary>
    public static DateTime StartOfDayUtc(DateOnly day, TimeZoneInfo tz)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        for (var i = 0; i < 4 && tz.IsInvalidTime(local); i++) local = local.AddMinutes(30);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, tz), DateTimeKind.Utc);
    }

    /// <summary>Instante UTC en el que empieza la hora <paramref name="hour"/> contada desde <paramref name="originUtc"/>.</summary>
    public static DateTime BucketStart(DateTime originUtc, int hour) => originUtc.AddHours(hour);

    /// <summary>Lista de días consecutivos [from, to].</summary>
    public static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1)) yield return d;
    }

    /// <summary>Porcentaje 0–100 redondeado a 1 decimal (0 si el denominador es 0).</summary>
    public static double Percent(double part, double total)
        => total <= 0 ? 0 : Math.Round(part / total * 100, 1);

    /// <summary>Promedio monetario redondeado a 2 decimales (0 si no hay elementos).</summary>
    public static decimal Average(decimal total, int count)
        => count <= 0 ? 0 : Math.Round(total / count, 2);
}
