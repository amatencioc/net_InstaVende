using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly ImageService _img;

    public ProductsController(AppDbContext db, CurrentUserService cu, ImageService img)
    { _db = db; _cu = cu; _img = img; }

    public async Task<IActionResult> Index()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return RedirectToAction("Register", "Account");
        var cats = await _db.ProductCategories.Where(c => c.BusinessId == bid).ToListAsync();
        var products = await _db.Products.Include(p => p.Category).Where(p => p.BusinessId == bid).OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(new ProductListViewModel
        {
            Products = products.Select(Map),
            Categories = cats.Select(c => new CategoryViewModel { Id = c.Id, Name = c.Name, Description = c.Description })
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetList(string? search, int? categoryId)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        var q = _db.Products.Include(p => p.Category).Where(p => p.BusinessId == bid && p.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
        return Json((await q.OrderBy(p => p.Name).ToListAsync()).Select(Map));
    }

    [HttpGet] public async Task<IActionResult> Create()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return RedirectToAction("Register", "Account");
        ViewBag.Categories = await _db.ProductCategories.Where(c => c.BusinessId == bid).ToListAsync();
        return View(new ProductViewModel());
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        if (!ModelState.IsValid) { ViewBag.Categories = await _db.ProductCategories.Where(c => c.BusinessId == bid).ToListAsync(); return View(model); }
        _db.Products.Add(new Product
        {
            BusinessId = bid.Value, Name = model.Name, Description = model.Description, Price = model.Price,
            Stock = model.Stock, CategoryId = model.CategoryId, IsActive = model.IsActive, IsFeatured = model.IsFeatured,
            ImageUrl = model.ImageFile != null ? await _img.SaveImageAsync(model.ImageFile) : null
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Producto creado exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet] public async Task<IActionResult> Edit(int id)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == bid);
        if (p == null) return NotFound();
        ViewBag.Categories = await _db.ProductCategories.Where(c => c.BusinessId == bid).ToListAsync();
        return View(Map(p));
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == bid);
        if (p == null) return NotFound();
        if (!ModelState.IsValid) { ViewBag.Categories = await _db.ProductCategories.Where(c => c.BusinessId == bid).ToListAsync(); return View(model); }
        if (model.ImageFile != null) { _img.DeleteImage(p.ImageUrl); p.ImageUrl = await _img.SaveImageAsync(model.ImageFile); }
        p.Name = model.Name; p.Description = model.Description; p.Price = model.Price; p.Stock = model.Stock;
        p.CategoryId = model.CategoryId; p.IsActive = model.IsActive; p.IsFeatured = model.IsFeatured; p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Producto actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == bid);
        if (p == null) return NotFound();
        _img.DeleteImage(p.ImageUrl); _db.Products.Remove(p); await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        if (model.Id == 0) _db.ProductCategories.Add(new ProductCategory { BusinessId = bid.Value, Name = model.Name, Description = model.Description });
        else
        {
            var cat = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == model.Id && c.BusinessId == bid);
            if (cat == null) return NotFound();
            cat.Name = model.Name; cat.Description = model.Description;
        }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    private static ProductViewModel Map(Product p) => new()
    {
        Id = p.Id, Name = p.Name, Description = p.Description, Price = p.Price, Stock = p.Stock,
        CategoryId = p.CategoryId, CategoryName = p.Category?.Name, ImageUrl = p.ImageUrl,
        IsActive = p.IsActive, IsFeatured = p.IsFeatured, CreatedAt = p.CreatedAt
    };
}
