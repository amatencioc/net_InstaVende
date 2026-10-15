using System.ComponentModel.DataAnnotations;

namespace InstaVende.Web.Features.SellerData;

/// <summary>Valida que el texto sea una URL absoluta http/https (los vacíos se consideran válidos).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public HttpUrlAttribute() : base("Ingresa un enlace válido que empiece con http:// o https://.") { }

    public override bool IsValid(object? value) =>
        value is not string text || string.IsNullOrWhiteSpace(text) || IsHttpUrl(text.Trim());

    public static bool IsHttpUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);
}

/// <summary>Utilidades comunes de los recursos ordenables del módulo.</summary>
internal static class SellerDataText
{
    /// <summary>Recorta y convierte en null los textos vacíos.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Valida un nuevo orden: sin duplicados y solo ids existentes. Devuelve la lista final de ids
    /// (los que no se enviaron conservan su orden relativo y quedan al final) o un mensaje de error.
    /// </summary>
    public static (IReadOnlyList<int>? Order, string? Error) ResolveOrder(IReadOnlyList<int> requested, IReadOnlyList<int> currentOrder)
    {
        if (requested.Count != requested.Distinct().Count())
            return (null, "La lista de orden contiene elementos repetidos.");
        var existing = currentOrder.ToHashSet();
        if (requested.Any(id => !existing.Contains(id)))
            return (null, "Algunos elementos no existen o fueron eliminados. Recarga la página e inténtalo de nuevo.");
        var sent = requested.ToHashSet();
        return ([.. requested, .. currentOrder.Where(id => !sent.Contains(id))], null);
    }
}
