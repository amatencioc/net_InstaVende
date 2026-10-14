namespace InstaVende.Core.Domain;

/// <summary>
/// Moneda y zona horaria de un negocio a partir de los valores que guarda la UI
/// (<c>Business.Country</c> = "Perú", <c>Business.Currency</c> = "PEN - Sol Peruano").
/// Todas las fechas se persisten en UTC; esta clase solo se usa para mostrar
/// y para razonar sobre "hoy" u "horario de atención" en la hora del negocio.
/// </summary>
public static class BusinessLocale
{
    public const string DefaultCountry  = "Perú";
    public const string DefaultCurrency = "PEN";

    public static readonly IReadOnlyList<string> Countries =
        ["Perú", "México", "Colombia", "Argentina", "Chile", "Ecuador", "Bolivia", "Uruguay", "Paraguay", "Venezuela"];

    public static readonly IReadOnlyList<string> Currencies =
        ["PEN - Sol Peruano", "MXN - Peso Mexicano", "COP - Peso Colombiano", "ARS - Peso Argentino",
         "CLP - Peso Chileno", "USD - Dólar Americano"];

    private static readonly Dictionary<string, string> TimeZoneByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Perú"]      = "America/Lima",
        ["México"]    = "America/Mexico_City",
        ["Colombia"]  = "America/Bogota",
        ["Argentina"] = "America/Argentina/Buenos_Aires",
        ["Chile"]     = "America/Santiago",
        ["Ecuador"]   = "America/Guayaquil",
        ["Bolivia"]   = "America/La_Paz",
        ["Uruguay"]   = "America/Montevideo",
        ["Paraguay"]  = "America/Asuncion",
        ["Venezuela"] = "America/Caracas",
    };

    private static readonly Dictionary<string, string> SymbolByCurrency = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PEN"] = "S/",
        ["USD"] = "US$",
        ["MXN"] = "MX$",
        ["COP"] = "COL$",
        ["ARS"] = "AR$",
        ["CLP"] = "CLP$",
    };

    /// <summary>Código ISO de la moneda ("PEN") a partir del valor guardado ("PEN - Sol Peruano").</summary>
    public static string CurrencyCode(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return DefaultCurrency;
        var code = currency.Split('-', 2)[0].Trim();
        return code.Length == 3 ? code.ToUpperInvariant() : DefaultCurrency;
    }

    public static string CurrencySymbol(string? currency)
        => SymbolByCurrency.TryGetValue(CurrencyCode(currency), out var s) ? s : CurrencyCode(currency);

    public static string FormatMoney(decimal amount, string? currency)
        => $"{CurrencySymbol(currency)} {amount.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}";

    /// <summary>Zona horaria del negocio; nunca lanza (cae a UTC si el sistema no tiene datos tz).</summary>
    public static TimeZoneInfo TimeZone(string? country)
    {
        var id = country != null && TimeZoneByCountry.TryGetValue(country.Trim(), out var tz) ? tz : "America/Lima";
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    public static DateTime ToLocal(DateTime utc, string? country)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone(country));

    /// <summary>Convierte una fecha "de pared" del negocio (p.ej. input datetime-local) a UTC.</summary>
    public static DateTime ToUtc(DateTime local, string? country)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone(country));

    /// <summary>Inicio del día actual del negocio expresado en UTC (para filtros "hoy").</summary>
    public static DateTime StartOfTodayUtc(string? country)
    {
        var local = ToLocal(DateTime.UtcNow, country).Date;
        return ToUtc(local, country);
    }
}
