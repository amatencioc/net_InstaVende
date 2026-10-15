using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using InstaVende.Web.Services;

namespace InstaVende.Web.Features.Settings;

/// <summary>
/// Reglas del equipo y de los enlaces de invitación.
/// <list type="bullet">
///   <item>El token es aleatorio (32 bytes, base64url) y solo viaja en el enlace: en la BD se guarda su hash SHA-256,
///         así una filtración de la tabla no permite aceptar invitaciones.</item>
///   <item>El límite de usuarios del plan se lee de <c>Team:MaxUsers</c> (por defecto <see cref="DefaultMaxUsers"/>).</item>
/// </list>
/// </summary>
internal static class TeamRules
{
    public const int DefaultMaxUsers = 5;
    public const int MaxNotificationEmails = 5;
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private const int TokenBytes = 32;
    // 32 bytes en base64url sin relleno = 43 caracteres
    private const int TokenLength = 43;

    public static int MaxUsers(IConfiguration configuration)
        => Math.Max(1, configuration.GetValue("Team:MaxUsers", DefaultMaxUsers));

    /// <summary>Genera un token nuevo y su hash (lo único que se persiste).</summary>
    public static (string Token, string Hash) NewToken()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return (token, Hash(token));
    }

    /// <summary>Hash del token recibido o null si el formato no es válido (evita consultas inútiles).</summary>
    public static string? TryHash(string? token)
    {
        if (token is null || token.Length != TokenLength) return null;
        foreach (var c in token)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return null;
        return Hash(token);
    }

    private static string Hash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    /// <summary>Email normalizado para guardar y comparar invitaciones y notificaciones.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Enlace público del SPA: <c>App:PublicBaseUrl</c> si está configurada; si no, el <c>Origin</c>
    /// de la petición (el SPA puede servirse desde otro origen en desarrollo) o, en última instancia, el host de la API.
    /// </summary>
    public static string InviteUrl(HttpRequest request, AppOptions app, string token)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(app.PublicBaseUrl) ? app.PublicBaseUrl
            : IsHttpOrigin(request.Headers.Origin.ToString()) ? request.Headers.Origin.ToString()
            : $"{request.Scheme}://{request.Host}";
        return $"{baseUrl.TrimEnd('/')}/invite/{token}";
    }

    private static bool IsHttpOrigin(string? origin)
        => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
           && uri.AbsolutePath == "/";
}
