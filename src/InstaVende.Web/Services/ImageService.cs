namespace InstaVende.Web.Services;

/// <summary>Resultado de una subida: URL pública relativa o mensaje de error para el usuario.</summary>
public sealed record ImageUploadResult(string? Url, string? Error)
{
    public bool Success => Url != null;
}

/// <summary>
/// Almacena imágenes subidas por los comercios en <c>wwwroot/uploads/{negocio}/{carpeta}/</c>.
/// Valida tamaño, extensión y firma real del archivo (la extensión debe coincidir con el contenido)
/// y genera nombres aleatorios; nunca usa el nombre enviado por el cliente.
/// </summary>
public class ImageService
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedFolders = ["products", "payments", "vendor", "avatars", "logos"];

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageService> _logger;

    public ImageService(IWebHostEnvironment env, ILogger<ImageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    private string UploadsRoot => Path.GetFullPath(Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads"));

    public async Task<ImageUploadResult> SaveAsync(IFormFile? file, int businessId, string folder, CancellationToken ct = default)
    {
        if (!AllowedFolders.Contains(folder)) throw new ArgumentException($"Carpeta de subida no permitida: {folder}", nameof(folder));
        if (file == null || file.Length == 0) return new(null, "Selecciona una imagen.");
        if (file.Length > MaxBytes) return new(null, "La imagen supera el tamaño máximo de 5 MB.");

        var header = new byte[12];
        await using (var peek = file.OpenReadStream())
        {
            if (await peek.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct) < 4)
                return new(null, "El archivo no es una imagen válida.");
        }

        var ext = DetectExtension(header);
        if (ext == null) return new(null, "Formato no permitido. Usa JPG, PNG, WEBP o GIF.");

        var dir = Path.Combine(UploadsRoot, businessId.ToString(), folder);
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using (var stream = new FileStream(Path.Combine(dir, fileName), FileMode.CreateNew))
            await file.CopyToAsync(stream, ct);

        return new($"/uploads/{businessId}/{folder}/{fileName}", null);
    }

    /// <summary>Borra una imagen previamente guardada. Ignora rutas fuera de la carpeta de subidas.</summary>
    public void Delete(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/", StringComparison.Ordinal)) return;
        var path = Path.GetFullPath(Path.Combine(UploadsRoot, "..", imageUrl.TrimStart('/')));
        if (!path.StartsWith(UploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Intento de borrar un archivo fuera de uploads: {Url}", imageUrl);
            return;
        }
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "No se pudo borrar la imagen {Url}", imageUrl);
        }
    }

    /// <summary>Extensión según la firma binaria (magic bytes); null si no es una imagen permitida.</summary>
    private static string? DetectExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (h.Length >= 6 && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38) return ".gif";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
