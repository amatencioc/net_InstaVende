using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InstaVende.Web.Services.Bot;

public enum BotCommand { None, Catalog, Help, Cart, Order, ClearCart, Status, AddToCart }

/// <summary>
/// Detecta comandos rápidos (sin coste de IA). Solo se activan con "#comando" o cuando
/// el mensaje completo coincide con una frase corta conocida, para no secuestrar
/// preguntas naturales como "necesito ayuda con mi talla" o "qué hay de envíos".
/// </summary>
public static partial class BotCommandRouter
{
    private static readonly Dictionary<string, BotCommand> HashCommands = new(StringComparer.Ordinal)
    {
        ["#catalogo"] = BotCommand.Catalog, ["#productos"] = BotCommand.Catalog,
        ["#carrito"]  = BotCommand.Cart,
        ["#pedido"]   = BotCommand.Order,   ["#confirmar"] = BotCommand.Order,
        ["#limpiar"]  = BotCommand.ClearCart, ["#vaciar"] = BotCommand.ClearCart,
        ["#ayuda"]    = BotCommand.Help,    ["#help"] = BotCommand.Help, ["#comandos"] = BotCommand.Help,
        ["#estado"]   = BotCommand.Status,
    };

    // Frases que solo cuentan si son el mensaje completo (normalizado)
    private static readonly Dictionary<string, BotCommand> ExactPhrases = new(StringComparer.Ordinal)
    {
        ["catalogo"] = BotCommand.Catalog, ["ver catalogo"] = BotCommand.Catalog, ["ver productos"] = BotCommand.Catalog,
        ["mostrar productos"] = BotCommand.Catalog, ["lista de productos"] = BotCommand.Catalog,
        ["mi carrito"] = BotCommand.Cart, ["ver carrito"] = BotCommand.Cart, ["ver mi carrito"] = BotCommand.Cart,
        ["vaciar carrito"] = BotCommand.ClearCart, ["limpiar carrito"] = BotCommand.ClearCart,
        ["ayuda"] = BotCommand.Help, ["comandos"] = BotCommand.Help,
        ["estado de mi pedido"] = BotCommand.Status, ["donde esta mi pedido"] = BotCommand.Status,
    };

    [GeneratedRegex(@"^(?:agregar|anadir|quiero el|dame el|producto)\s+(\d{1,3})(?:\s*x\s*(\d{1,2}))?$")]
    private static partial Regex AddToCartRegex();

    public static (BotCommand Command, int ProductNumber, int Quantity) Detect(string message)
    {
        var normalized = Normalize(message);
        if (normalized.Length == 0) return (BotCommand.None, 0, 0);

        if (normalized[0] == '#')
        {
            var head = normalized.Split(' ', 2)[0];
            return HashCommands.TryGetValue(head, out var cmd) ? (cmd, 0, 0) : (BotCommand.None, 0, 0);
        }

        if (ExactPhrases.TryGetValue(normalized, out var phrase)) return (phrase, 0, 0);

        var m = AddToCartRegex().Match(normalized);
        if (m.Success)
        {
            var qty = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
            return (BotCommand.AddToCart, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Math.Clamp(qty, 1, 99));
        }

        return (BotCommand.None, 0, 0);
    }

    /// <summary>Coincidencia por palabra completa (no subcadena) de una frase configurada.</summary>
    public static bool ContainsPhrase(string message, string phrase)
    {
        var text = $" {Normalize(message)} ";
        var p = Normalize(phrase);
        return p.Length > 0 && text.Contains($" {p} ", StringComparison.Ordinal);
    }

    /// <summary>Minúsculas, sin tildes, sin signos de puntuación y con espacios simples.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastSpace = false;
        foreach (var c in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c) || c == '#') { sb.Append(c); lastSpace = false; }
            else if (!lastSpace && sb.Length > 0) { sb.Append(' '); lastSpace = true; }
        }
        return sb.ToString().TrimEnd();
    }
}
