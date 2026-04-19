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

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public string? GetUserId()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<Business?> GetBusinessAsync()
    {
        var userId = GetUserId();
        if (userId == null) return null;
        return await _db.Businesses.FirstOrDefaultAsync(b => b.UserId == userId);
    }

    public async Task<int?> GetBusinessIdAsync()
    {
        var business = await GetBusinessAsync();
        return business?.Id;
    }
}
