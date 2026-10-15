using System.Security.Claims;
using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services;

/// <summary>
/// Datos del negocio activo del usuario autenticado, resueltos una sola vez por petición.
/// </summary>
public sealed record TenantInfo(int BusinessId, string BusinessName, string? Country, string? Currency, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
    public string CurrencySymbol => BusinessLocale.CurrencySymbol(Currency);
    public TimeZoneInfo TimeZone => BusinessLocale.TimeZone(Country);
    public DateTime ToLocal(DateTime utc) => BusinessLocale.ToLocal(utc, Country);
    public DateTime ToUtc(DateTime local) => BusinessLocale.ToUtc(local, Country);
}

/// <summary>
/// Resuelve el negocio (tenant) del usuario actual: como propietario
/// (<see cref="Business.UserId"/>) o como miembro invitado (<see cref="BusinessUser"/>).
/// Registrado como Scoped: el resultado se cachea durante la petición.
/// </summary>
public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    private TenantInfo? _tenant;
    private bool _tenantLoaded;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public string? GetUserId()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<TenantInfo?> GetTenantAsync()
    {
        if (_tenantLoaded) return _tenant;
        _tenantLoaded = true;

        var userId = GetUserId();
        if (userId is null) return null;
        return _tenant = await GetTenantAsync(userId);
    }

    /// <summary>Descarta el tenant cacheado (p.ej. tras crear el negocio en la misma petición).</summary>
    public void Reset()
    {
        _tenant = null;
        _tenantLoaded = false;
    }

    /// <summary>Resuelve el tenant de un usuario concreto sin usar la caché de la petición.</summary>
    public async Task<TenantInfo?> GetTenantAsync(string userId)
    {
        var tenant = await _db.Businesses.AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => new TenantInfo(b.Id, b.Name, b.Country, b.Currency, UserRole.Admin))
            .FirstOrDefaultAsync();

        tenant ??= await _db.BusinessUsers.AsNoTracking()
            .Where(bu => bu.UserId == userId)
            .OrderBy(bu => bu.JoinedAt)
            .Select(bu => new TenantInfo(bu.BusinessId, bu.Business.Name, bu.Business.Country, bu.Business.Currency, bu.Role))
            .FirstOrDefaultAsync();

        return tenant;
    }

    public async Task<int?> GetBusinessIdAsync() => (await GetTenantAsync())?.BusinessId;
}
