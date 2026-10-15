using System.ComponentModel.DataAnnotations;

namespace InstaVende.Web.Features.Settings;

/// <summary>Patrones de validación compartidos por los DTOs de configuración.</summary>
internal static class SettingsPatterns
{
    /// <summary>Teléfono internacional flexible: "+51 999 888 777", "(01) 555-1234".</summary>
    public const string Phone = @"^\+?[0-9\s().\-]{6,25}$";
    public const string PhoneMessage = "Ingresa un teléfono válido (solo números, espacios, paréntesis o guiones).";
}

/// <summary>URL absoluta con esquema http o https (rechaza javascript:, data:, ftp:, rutas relativas…).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public HttpUrlAttribute() : base("Ingresa una URL válida que empiece por http:// o https://.") { }

    public override bool IsValid(object? value)
        => value is null
           || (value is string s && Uri.TryCreate(s.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
               && !string.IsNullOrEmpty(uri.Host));
}

// ── Perfil ───────────────────────────────────────────────────────────────────

public sealed record SettingsProfileDto(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? AvatarUrl,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record SettingsProfileUpdateRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")] string FirstName,
    [Required(ErrorMessage = "El apellido es obligatorio."), StringLength(100, ErrorMessage = "El apellido admite hasta 100 caracteres.")] string LastName,
    [RegularExpression(SettingsPatterns.Phone, ErrorMessage = SettingsPatterns.PhoneMessage)] string? PhoneNumber);

public sealed record SettingsImageDto(string? Url);

// ── Seguridad ────────────────────────────────────────────────────────────────

public sealed record SettingsChangePasswordRequest(
    [Required(ErrorMessage = "Ingresa tu contraseña actual.")] string CurrentPassword,
    [Required(ErrorMessage = "Ingresa la nueva contraseña."), StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres.")] string NewPassword);

public sealed record SettingsChangeEmailRequest(
    [Required(ErrorMessage = "Ingresa el nuevo email."), EmailAddress(ErrorMessage = "Ingresa un email válido."), StringLength(256)] string NewEmail,
    [Required(ErrorMessage = "Confirma con tu contraseña actual.")] string CurrentPassword);

// ── Negocio ──────────────────────────────────────────────────────────────────

public sealed record SettingsBusinessDto(
    string Name,
    string? Description,
    string? Sector,
    string? Country,
    string? Currency,
    string? Phone,
    string? Email,
    string? WebsiteUrl,
    string? SocialMedia,
    string? WhatsAppNumber,
    string? LogoUrl,
    IReadOnlyList<string> CountryOptions,
    IReadOnlyList<string> CurrencyOptions,
    IReadOnlyList<string> SectorOptions);

public sealed record SettingsBusinessUpdateRequest(
    [Required(ErrorMessage = "El nombre del negocio es obligatorio."), StringLength(200, ErrorMessage = "El nombre admite hasta 200 caracteres.")] string Name,
    [StringLength(1000, ErrorMessage = "La descripción admite hasta 1000 caracteres.")] string? Description,
    [StringLength(100, ErrorMessage = "El sector admite hasta 100 caracteres.")] string? Sector,
    [Required(ErrorMessage = "Selecciona el país.")] string Country,
    [Required(ErrorMessage = "Selecciona la moneda.")] string Currency,
    [RegularExpression(SettingsPatterns.Phone, ErrorMessage = SettingsPatterns.PhoneMessage)] string? Phone,
    [EmailAddress(ErrorMessage = "Ingresa un email válido."), StringLength(256)] string? Email,
    [HttpUrl, StringLength(500, ErrorMessage = "La URL admite hasta 500 caracteres.")] string? WebsiteUrl,
    [StringLength(500, ErrorMessage = "Las redes sociales admiten hasta 500 caracteres.")] string? SocialMedia,
    [RegularExpression(SettingsPatterns.Phone, ErrorMessage = SettingsPatterns.PhoneMessage)] string? WhatsAppNumber);

// ── Notificaciones por email ─────────────────────────────────────────────────

public sealed record SettingsNotificationEmailDto(int Id, string Email, bool IsActive, DateTime CreatedAt);

public sealed record SettingsNotificationEmailsDto(IReadOnlyList<SettingsNotificationEmailDto> Items, int MaxEmails);

public sealed record SettingsNotificationEmailCreateRequest(
    [Required(ErrorMessage = "Ingresa un email."), EmailAddress(ErrorMessage = "Ingresa un email válido."), StringLength(256)] string Email);

public sealed record SettingsNotificationEmailUpdateRequest(bool IsActive);
