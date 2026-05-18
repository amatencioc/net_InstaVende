using System.Security.Claims;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    // Per-request cache — service is registered Scoped so this is safe
    private Business? _cachedBusiness;
    private bool _businessLoaded;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public string? GetUserId()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<Business?> GetBusinessAsync()
    {
        if (_businessLoaded) return _cachedBusiness;

        var userId = GetUserId();
        _cachedBusiness = userId is null
            ? null
            : await _db.Businesses.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == userId);

        _businessLoaded = true;
        return _cachedBusiness;
    }

    public async Task<int?> GetBusinessIdAsync()
    {
        var business = await GetBusinessAsync();
        return business?.Id;
    }
}
