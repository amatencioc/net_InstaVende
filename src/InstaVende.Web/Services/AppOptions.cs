namespace InstaVende.Web.Services;

/// <summary>Opciones generales de la aplicación (sección "App").</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// URL pública de la aplicación (p.ej. "https://app.instavende.com"). Se usa para construir
    /// enlaces absolutos (imágenes de productos que el bot comparte, URLs de webhooks).
    /// Si está vacía se usa la URL de la petición actual cuando existe.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public string? Absolute(string? relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute)) return null;
        if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out _)) return relativeOrAbsolute;
        return string.IsNullOrWhiteSpace(PublicBaseUrl) ? null : PublicBaseUrl.TrimEnd('/') + "/" + relativeOrAbsolute.TrimStart('/');
    }
}
