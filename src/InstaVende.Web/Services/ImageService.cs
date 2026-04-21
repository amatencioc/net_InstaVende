namespace InstaVende.Web.Services;

public class ImageService
{
    private readonly IWebHostEnvironment _env;

    public ImageService(IWebHostEnvironment env) { _env = env; }

    public async Task<string?> SaveImageAsync(IFormFile file, string folder = "products")
    {
        if (file == null || file.Length == 0) return null;
        var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExt.Contains(ext) || file.Length > 5 * 1024 * 1024) return null;

        // Validate magic bytes to prevent extension spoofing
        using var peek = file.OpenReadStream();
        var header = new byte[4];
        if (await peek.ReadAsync(header) < 4) return null;
        if (!IsAllowedImageHeader(header)) return null;

        var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid()}{ext}";
        await using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/{folder}/{fileName}";
    }

    private static bool IsAllowedImageHeader(byte[] h) =>
        (h[0] == 0xFF && h[1] == 0xD8) ||                          // JPEG
        (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) || // PNG
        (h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46) ||          // GIF
        (h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46);   // WebP (RIFF)

    public void DeleteImage(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        var path = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
        if (File.Exists(path)) File.Delete(path);
    }
}
