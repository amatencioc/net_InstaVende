using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Seller;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Personalidad del vendedor IA (entidad <c>VendorConfig</c>).</summary>
/// <param name="UpdatedAt">Null mientras el negocio no haya guardado nunca la personalidad (se muestran valores sugeridos).</param>
public sealed record SellerProfileDto(
    string VendorName,
    string? VendorGender,
    string Country,
    string? BusinessDescription,
    string? TargetAudience,
    string? Rules,
    string? CommunicationStyle,
    string? SalesStyle,
    int ResponseLength,
    bool UseEmojis,
    IReadOnlyList<string> EmojiPalette,
    bool UseOpeningPunctuation,
    IReadOnlyList<string> WordsToAvoid,
    string WelcomeMessage,
    string? PurchaseConfirmationMessage,
    IReadOnlyList<string> HumanHandoffSituations,
    bool AutoPauseOnHandoff,
    string? HandoffMessage,
    DateTime? UpdatedAt);

/// <summary>Estilo predefinido que la UI ofrece en el menú "Plantillas".</summary>
public sealed record SellerProfileTemplateDto(
    string Id,
    string Name,
    string Description,
    string CommunicationStyle,
    string SalesStyle,
    int ResponseLength,
    bool UseEmojis,
    IReadOnlyList<string> EmojiPalette,
    bool UseOpeningPunctuation);

/// <summary>Personalidad + catálogos para construir el formulario.</summary>
public sealed record SellerProfileResponse(
    SellerProfileDto Profile,
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Genders,
    IReadOnlyList<SellerProfileTemplateDto> Templates);

public sealed record SellerProfileUpdateRequest(
    [Required(ErrorMessage = "El nombre del vendedor es obligatorio."), StringLength(60, ErrorMessage = "El nombre admite como máximo 60 caracteres.")]
    string VendorName,
    [RegularExpression("^(Masculino|Femenino)$", ErrorMessage = "Selecciona un género válido.")]
    string? VendorGender,
    [Required(ErrorMessage = "Selecciona el país de operación.")]
    string Country,
    [StringLength(1000, ErrorMessage = "La descripción admite como máximo 1000 caracteres.")]
    string? BusinessDescription,
    [StringLength(300, ErrorMessage = "La audiencia admite como máximo 300 caracteres.")]
    string? TargetAudience,
    [StringLength(2000, ErrorMessage = "Las reglas admiten como máximo 2000 caracteres.")]
    string? Rules,
    [StringLength(200, ErrorMessage = "El estilo de comunicación admite como máximo 200 caracteres.")]
    string? CommunicationStyle,
    [StringLength(210, ErrorMessage = "El estilo de ventas admite como máximo 210 caracteres.")]
    string? SalesStyle,
    [Range(1, 5, ErrorMessage = "La longitud de respuesta debe estar entre 1 y 5.")]
    int ResponseLength,
    bool UseEmojis,
    [MaxLength(24, ErrorMessage = "La paleta admite como máximo 24 emojis.")]
    IReadOnlyList<string>? EmojiPalette,
    bool UseOpeningPunctuation,
    [MaxLength(50, ErrorMessage = "Puedes indicar como máximo 50 palabras a evitar.")]
    IReadOnlyList<string>? WordsToAvoid,
    [Required(ErrorMessage = "El mensaje de bienvenida es obligatorio."), StringLength(500, ErrorMessage = "El mensaje de bienvenida admite como máximo 500 caracteres.")]
    string WelcomeMessage,
    [StringLength(500, ErrorMessage = "El mensaje de confirmación admite como máximo 500 caracteres.")]
    string? PurchaseConfirmationMessage,
    [MaxLength(20, ErrorMessage = "Puedes indicar como máximo 20 situaciones de derivación.")]
    IReadOnlyList<string>? HumanHandoffSituations,
    bool AutoPauseOnHandoff,
    [StringLength(300, ErrorMessage = "El mensaje de derivación admite como máximo 300 caracteres.")]
    string? HandoffMessage) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!BusinessLocale.Countries.Contains(Country?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            yield return new ValidationResult("Selecciona un país de la lista.", [nameof(Country)]);
        if (EmojiPalette?.Any(e => string.IsNullOrWhiteSpace(e) || e.Trim().Length > 16) == true)
            yield return new ValidationResult("La paleta contiene un emoji no válido.", [nameof(EmojiPalette)]);
        if (WordsToAvoid?.Any(w => w?.Trim().Length > 60) == true)
            yield return new ValidationResult("Cada palabra a evitar admite como máximo 60 caracteres.", [nameof(WordsToAvoid)]);
        if (HumanHandoffSituations?.Any(s => s?.Trim().Length > 200) == true)
            yield return new ValidationResult("Cada situación de derivación admite como máximo 200 caracteres.", [nameof(HumanHandoffSituations)]);
    }
}

// ── Controlador ──────────────────────────────────────────────────────────────

/// <summary>Personalidad del vendedor IA: identidad, tono, mensajes y derivación humana.</summary>
[Route("api/seller/profile")]
public sealed class SellerProfileController(AppDbContext db) : TenantController
{
    private static readonly IReadOnlyList<string> Genders = ["Masculino", "Femenino"];

    private static readonly IReadOnlyList<SellerProfileTemplateDto> Templates =
    [
        new("amigable", "Amigable", "Cercano y cálido, ideal para tiendas de barrio y emprendimientos.",
            "Hablo de tú, con calidez y cercanía. Uso frases cortas, saludo con entusiasmo y me intereso por lo que necesita el cliente.",
            "Recomiendo con naturalidad, sin presionar. Destaco beneficios concretos y ofrezco ayuda para elegir la mejor opción.",
            3, true, ["😊", "🙌", "✨", "🛍️", "💚"], false),
        new("profesional", "Profesional", "Formal y preciso, para servicios o productos de alto valor.",
            "Me comunico de forma cordial y profesional. Respondo con precisión, sin modismos, y confirmo los datos importantes.",
            "Asesoro con información clara: características, garantías y condiciones. Propongo el siguiente paso sin insistir.",
            3, false, [], true),
        new("experto", "Vendedor experto", "Consultivo y persuasivo, orientado a cerrar ventas.",
            "Escucho primero, hago preguntas para entender la necesidad y respondo con seguridad y empatía.",
            "Detecto la necesidad, recomiendo el producto ideal, sugiero complementos y cierro con una llamada a la acción clara.",
            4, true, ["🔥", "✅", "👉", "💯"], true),
        new("juvenil", "Juvenil", "Relajado y divertido, para marcas de moda o público joven.",
            "Hablo relajado y con buena onda, como un amigo que conoce la tienda. Uso expresiones actuales sin exagerar.",
            "Genero emoción por las novedades, recomiendo combinaciones y aviso cuando quedan pocas unidades.",
            2, true, ["😎", "🔥", "🤩", "💥", "🛒"], false),
    ];

    /// <summary>Personalidad actual. Si nunca se configuró devuelve valores sugeridos (sin guardarlos).</summary>
    [HttpGet]
    [ProducesResponseType<SellerProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileResponse>> Get(CancellationToken ct)
    {
        var vendor = await db.VendorConfigs.AsNoTracking().FirstOrDefaultAsync(v => v.BusinessId == BusinessId, ct);
        return await BuildResponseAsync(vendor, ct);
    }

    /// <summary>Guarda la personalidad (crea la configuración si no existe).</summary>
    [HttpPut, RequireBusinessAdmin]
    [ProducesResponseType<SellerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SellerProfileResponse>> Update(SellerProfileUpdateRequest request, CancellationToken ct)
    {
        var vendor = await db.VendorConfigs.FirstOrDefaultAsync(v => v.BusinessId == BusinessId, ct);
        if (vendor == null)
        {
            vendor = new VendorConfig { BusinessId = BusinessId, CreatedAt = DateTime.UtcNow };
            db.VendorConfigs.Add(vendor);
        }

        vendor.VendorName = request.VendorName.Trim();
        vendor.VendorGender = SellerText.Clean(request.VendorGender);
        vendor.Country = BusinessLocale.Countries.First(c => string.Equals(c, request.Country.Trim(), StringComparison.OrdinalIgnoreCase));
        vendor.BusinessDescription = SellerText.Clean(request.BusinessDescription);
        vendor.TargetAudience = SellerText.Clean(request.TargetAudience);
        vendor.Rules = SellerText.Clean(request.Rules);
        vendor.CommunicationStyle = SellerText.Clean(request.CommunicationStyle);
        vendor.SalesStyle = SellerText.Clean(request.SalesStyle);
        vendor.ResponseLength = request.ResponseLength;
        vendor.UseEmojis = request.UseEmojis;
        var palette = SellerText.CleanList(request.EmojiPalette, splitCommas: false);
        vendor.EmojiPalette = palette.Count == 0 ? null : JsonSerializer.Serialize(palette);
        vendor.UseOpeningPunctuation = request.UseOpeningPunctuation;
        vendor.WordsToAvoid = SellerText.JoinOrNull(SellerText.CleanList(request.WordsToAvoid), ", ");
        vendor.WelcomeMessage = request.WelcomeMessage.Trim();
        vendor.PurchaseConfirmationMessage = SellerText.Clean(request.PurchaseConfirmationMessage);
        vendor.HumanHandoffSituations = SellerText.JoinOrNull(SellerText.CleanList(request.HumanHandoffSituations), "\n");
        vendor.AutoPauseOnHandoff = request.AutoPauseOnHandoff;
        vendor.HandoffExampleMessage = SellerText.Clean(request.HandoffMessage);
        vendor.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (db.Entry(vendor).State == EntityState.Added)
        {
            // Dos guardados simultáneos del primer registro: el índice único por negocio rechaza el segundo.
            return ConflictProblem("Otro usuario guardó la personalidad al mismo tiempo. Recarga la página e inténtalo de nuevo.");
        }

        return await BuildResponseAsync(vendor, ct);
    }

    private async Task<SellerProfileResponse> BuildResponseAsync(VendorConfig? vendor, CancellationToken ct)
    {
        var business = await db.Businesses.AsNoTracking()
            .Where(b => b.Id == BusinessId)
            .Select(b => new { b.Name, b.Country, b.Description })
            .FirstAsync(ct);

        var country = SellerText.Clean(vendor?.Country) ?? SellerText.Clean(business.Country) ?? BusinessLocale.DefaultCountry;
        var profile = vendor == null
            ? new SellerProfileDto(
                VendorName: "Asistente", VendorGender: null, Country: country,
                BusinessDescription: SellerText.Clean(business.Description), TargetAudience: null, Rules: null,
                CommunicationStyle: null, SalesStyle: null, ResponseLength: 3,
                UseEmojis: true, EmojiPalette: [], UseOpeningPunctuation: false, WordsToAvoid: [],
                WelcomeMessage: $"¡Hola! 👋 Bienvenido a {business.Name}. ¿En qué puedo ayudarte hoy?",
                PurchaseConfirmationMessage: null, HumanHandoffSituations: [], AutoPauseOnHandoff: true,
                HandoffMessage: null, UpdatedAt: null)
            : new SellerProfileDto(
                vendor.VendorName, vendor.VendorGender, country, vendor.BusinessDescription, vendor.TargetAudience, vendor.Rules,
                vendor.CommunicationStyle, vendor.SalesStyle, Math.Clamp(vendor.ResponseLength, 1, 5),
                vendor.UseEmojis, SellerText.ParseEmojiPalette(vendor.EmojiPalette), vendor.UseOpeningPunctuation,
                SellerText.SplitList(vendor.WordsToAvoid), vendor.WelcomeMessage ?? string.Empty, vendor.PurchaseConfirmationMessage,
                SellerText.SplitList(vendor.HumanHandoffSituations), vendor.AutoPauseOnHandoff, vendor.HandoffExampleMessage,
                vendor.UpdatedAt);

        return new SellerProfileResponse(profile, BusinessLocale.Countries, Genders, Templates);
    }
}

/// <summary>
/// Normalización de textos y listas del vendedor. Las listas se interpretan igual que en
/// <c>BotContextProvider</c> (separadores salto de línea, coma o punto y coma) para que la UI
/// muestre exactamente lo que el bot entiende.
/// </summary>
internal static class SellerText
{
    private static readonly char[] ListSeparators = ['\n', ',', ';'];

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? JoinOrNull(IReadOnlyList<string> items, string separator)
        => items.Count == 0 ? null : string.Join(separator, items);

    /// <summary>Recorta, elimina vacíos y duplicados (sin distinguir mayúsculas). Opcionalmente separa por comas.</summary>
    public static List<string> CleanList(IEnumerable<string?>? items, bool splitCommas = true)
    {
        if (items == null) return [];
        var separators = splitCommas ? ListSeparators : ['\n'];
        return items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .SelectMany(i => i!.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(i => i.TrimStart('-', '•', '*', ' ').ReplaceLineEndings(" ").Trim())
            .Where(i => i.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> SplitList(string? text)
        => string.IsNullOrWhiteSpace(text) ? [] : CleanList(text.Split(ListSeparators));

    public static List<string> ParseEmojiPalette(string? palette)
    {
        if (string.IsNullOrWhiteSpace(palette)) return [];
        try
        {
            if (palette.TrimStart().StartsWith('['))
                return CleanList(JsonSerializer.Deserialize<List<string>>(palette), splitCommas: false);
        }
        catch (JsonException) { /* formato libre heredado */ }
        return CleanList(palette.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries), splitCommas: false);
    }

    /// <summary>Frases disparadoras guardadas como JSON (o texto separado por comas en datos antiguos).</summary>
    public static List<string> ParsePhrases(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return [];
        try
        {
            if (stored.TrimStart().StartsWith('['))
                return CleanList(JsonSerializer.Deserialize<List<string>>(stored), splitCommas: false);
        }
        catch (JsonException) { /* formato libre heredado */ }
        return SplitList(stored);
    }
}
