using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class AccountConfigController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;

    public AccountConfigController(AppDbContext db, CurrentUserService cu)
    {
        _db = db;
        _cu = cu;
    }

    public async Task<IActionResult> Index(string tab = "organizacion")
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");

        var users = await _db.BusinessUsers
            .Include(bu => bu.User)
            .Where(bu => bu.BusinessId == biz.Id)
            .ToListAsync();

        var invitations = await _db.UserInvitations
            .Where(i => i.BusinessId == biz.Id && i.Status == InvitationStatus.Pending)
            .ToListAsync();

        var notifEmails = await _db.NotificationEmails
            .Where(n => n.BusinessId == biz.Id)
            .OrderBy(n => n.Id)
            .ToListAsync();

        var vm = new AccountConfigViewModel
        {
            ActiveTab = tab,
            Organizacion = new OrganizacionViewModel
            {
                Name = biz.Name,
                Country = biz.Country,
                Currency = biz.Currency
            },
            Usuarios = new UsuariosViewModel
            {
                MaxUsers = 1,
                Users = users.Select(bu => new BusinessUserItemViewModel
                {
                    UserId = bu.UserId,
                    Email = bu.User?.UserName,
                    Phone = bu.User?.PhoneNumber,
                    Role = bu.Role
                }).ToList(),
                PendingInvitations = invitations.Select(i => new PendingInvitationViewModel
                {
                    Id = i.Id,
                    Email = i.Email,
                    Role = i.Role,
                    ExpiresAt = i.ExpiresAt
                }).ToList()
            },
            Notificaciones = new NotificacionesEmailViewModel
            {
                Emails = notifEmails.Select(n => new NotificationEmailItemViewModel
                {
                    Id = n.Id,
                    Email = n.Email,
                    IsActive = n.IsActive
                }).ToList()
            }
        };

        // Ensure owner appears in users list
        if (!vm.Usuarios.Users.Any(u => u.UserId == biz.UserId))
        {
            var owner = await _db.Users.FindAsync(biz.UserId);
            vm.Usuarios.Users.Insert(0, new BusinessUserItemViewModel
            {
                UserId = biz.UserId,
                Email = owner?.UserName,
                Phone = owner?.PhoneNumber,
                Role = UserRole.Admin
            });
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOrganizacion([FromBody] OrganizacionViewModel model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        biz.Name = model.Name;
        biz.Country = model.Country;
        biz.Currency = model.Currency;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(model.Email))
            return BadRequest(new { error = "Email requerido." });

        var existing = await _db.UserInvitations
            .FirstOrDefaultAsync(i => i.BusinessId == biz.Id && i.Email == model.Email && i.Status == InvitationStatus.Pending);
        if (existing != null)
            return BadRequest(new { error = "Ya existe una invitación pendiente para este email." });

        _db.UserInvitations.Add(new UserInvitation
        {
            BusinessId = biz.Id,
            Email = model.Email,
            Token = Guid.NewGuid().ToString("N"),
            Role = UserRole.Member,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInvitation([FromBody] AccountConfigIdRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var inv = await _db.UserInvitations.FirstOrDefaultAsync(i => i.Id == model.Id && i.BusinessId == biz.Id);
        if (inv == null) return NotFound();
        inv.Status = InvitationStatus.Cancelled;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNotificationEmail([FromBody] AccountConfigEmailRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(model.Email))
            return BadRequest(new { error = "Email requerido." });

        _db.NotificationEmails.Add(new NotificationEmail
        {
            BusinessId = biz.Id,
            Email = model.Email,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveNotificationEmail([FromBody] AccountConfigIdRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var ne = await _db.NotificationEmails.FirstOrDefaultAsync(n => n.Id == model.Id && n.BusinessId == biz.Id);
        if (ne == null) return NotFound();
        _db.NotificationEmails.Remove(ne);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
}

public class InviteUserRequest { public string Email { get; set; } = string.Empty; }
public class AccountConfigIdRequest { public int Id { get; set; } }
public class AccountConfigEmailRequest { public string Email { get; set; } = string.Empty; }
