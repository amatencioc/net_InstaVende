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

        var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid()}{ext}";
        await using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/{folder}/{fileName}";
    }

    public void DeleteImage(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        var path = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
        if (File.Exists(path)) File.Delete(path);
    }
}
