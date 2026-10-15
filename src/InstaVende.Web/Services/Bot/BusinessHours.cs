using System.Text.RegularExpressions;

namespace InstaVende.Web.Services.Bot;

/// <summary>
/// Interpreta horarios en texto libre como "Lunes a Viernes 9:00-18:00" o "09:00 - 23:30".
/// Devuelve null cuando el texto no se puede interpretar (el prompt lo indica como desconocido).
/// </summary>
public static partial class BusinessHours
{
    private static readonly string[] Days = ["domingo", "lunes", "martes", "miercoles", "jueves", "viernes", "sabado"];

    [GeneratedRegex(@"(\d{1,2})(?::(\d{2}))?\s*(?:h|hrs)?\s*(?:-|–|a|hasta)\s*(\d{1,2})(?::(\d{2}))?", RegexOptions.CultureInvariant)]
    private static partial Regex TimeRange();

    [GeneratedRegex(@"(domingo|lunes|martes|miercoles|jueves|viernes|sabado)\s*(?:a|-|–|al)\s*(domingo|lunes|martes|miercoles|jueves|viernes|sabado)", RegexOptions.CultureInvariant)]
    private static partial Regex DayRange();

    public static bool? IsOpen(string? text, DateTime localNow)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = Normalize(text);

        var days = DayRange().Match(normalized);
        if (days.Success)
        {
            var from = Array.IndexOf(Days, days.Groups[1].Value);
            var to   = Array.IndexOf(Days, days.Groups[2].Value);
            var today = (int)localNow.DayOfWeek;
            var inRange = from <= to ? today >= from && today <= to : today >= from || today <= to;
            if (!inRange) return false;
        }

        var time = TimeRange().Match(normalized);
        if (!time.Success) return days.Success ? true : null;

        var start = int.Parse(time.Groups[1].Value) * 60 + (time.Groups[2].Success ? int.Parse(time.Groups[2].Value) : 0);
        var end   = int.Parse(time.Groups[3].Value) * 60 + (time.Groups[4].Success ? int.Parse(time.Groups[4].Value) : 0);
        if (start >= 24 * 60 || end > 24 * 60) return null;

        var now = localNow.Hour * 60 + localNow.Minute;
        return end > start ? now >= start && now < end : now >= start || now < end; // rango que cruza medianoche
    }

    /// <summary>Minúsculas y sin tildes, conservando dígitos, letras, ':' y guiones.</summary>
    private static string Normalize(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c is ':' or '-' or '–' ? c : ' ');
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}
