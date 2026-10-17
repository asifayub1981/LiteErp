using LiteErp.Core.Entities;
using LiteErp.Core.Identity;
using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Api.Controllers;

/// <summary>User management: create users and assign per-form rights.
/// Firm rule: users are never deleted — set inactive instead.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public UsersController(ErpDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db; _users = users;
    }

    public record UserListDto(string Id, string UserName, string DisplayName,
        int? CompanyId, string? CompanyName, bool IsActive);

    public record UserDetailDto(string Id, string UserName, string DisplayName,
        int? CompanyId, bool IsActive);

    public record SaveUserDto(string UserName, string DisplayName, string? Password,
        int? CompanyId, bool IsActive);

    public record RightDto(string FormCode, string FormLabel,
        bool CanView, bool CanAdd, bool CanEdit, bool CanDelete);

    public record SaveRightDto(string FormCode,
        bool CanView, bool CanAdd, bool CanEdit, bool CanDelete);

    // ---------- List ----------
    [HttpGet]
    public async Task<ActionResult<List<UserListDto>>> List([FromQuery] string? search)
    {
        var q = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.UserName!.Contains(search) || u.DisplayName.Contains(search));

        var users = await q.OrderBy(u => u.DisplayName).ToListAsync();
        var companyIds = users.Where(u => u.CompanyId.HasValue).Select(u => u.CompanyId!.Value).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CompanyName);

        return users.Select(u => new UserListDto(
            u.Id, u.UserName!, u.DisplayName, u.CompanyId,
            u.CompanyId.HasValue && companies.TryGetValue(u.CompanyId.Value, out var n) ? n : null,
            u.LockoutEnd == null)).ToList();
    }

    // ---------- Detail ----------
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailDto>> Detail(string id)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        return new UserDetailDto(u.Id, u.UserName!, u.DisplayName, u.CompanyId, u.LockoutEnd == null);
    }

    // ---------- Create ----------
    [HttpPost]
    public async Task<ActionResult<UserDetailDto>> Create(SaveUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "Username and password are required." });

        var user = new ApplicationUser
        {
            UserName = dto.UserName.Trim(),
            DisplayName = dto.DisplayName.Trim(),
            CompanyId = dto.CompanyId,
        };
        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        // Default: grant all rights (admin creates users, adjusts afterwards)
        foreach (var (code, _) in FormCodes.All)
            _db.UserFormRights.Add(new UserFormRight
            {
                UserId = user.Id, FormCode = code,
                CanView = true, CanAdd = true, CanEdit = true, CanDelete = true,
            });
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Detail), new { id = user.Id },
            new UserDetailDto(user.Id, user.UserName!, user.DisplayName, user.CompanyId, true));
    }

    // ---------- Update ----------
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, SaveUserDto dto)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.DisplayName = dto.DisplayName.Trim();
        user.CompanyId = dto.CompanyId;

        // Active/inactive via lockout
        if (dto.IsActive && user.LockoutEnd != null)
            await _users.SetLockoutEndDateAsync(user, null);
        else if (!dto.IsActive && user.LockoutEnd == null)
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var pr = await _users.ResetPasswordAsync(user, token, dto.Password);
            if (!pr.Succeeded)
                return BadRequest(new { message = string.Join("; ", pr.Errors.Select(e => e.Description)) });
        }

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
        return NoContent();
    }

    // ---------- Rights ----------
    [HttpGet("{id}/rights")]
    public async Task<ActionResult<List<RightDto>>> GetRights(string id)
    {
        var existing = await _db.UserFormRights.AsNoTracking()
            .Where(r => r.UserId == id).ToDictionaryAsync(r => r.FormCode);

        return FormCodes.All.Select(f =>
        {
            existing.TryGetValue(f.Code, out var r);
            return new RightDto(f.Code, f.Label,
                r?.CanView ?? false, r?.CanAdd ?? false, r?.CanEdit ?? false, r?.CanDelete ?? false);
        }).ToList();
    }

    [HttpPut("{id}/rights")]
    public async Task<ActionResult> SaveRights(string id, List<SaveRightDto> rights)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var validCodes = FormCodes.All.Select(f => f.Code).ToHashSet();
        foreach (var dto in rights)
        {
            if (!validCodes.Contains(dto.FormCode)) continue;
            var existing = await _db.UserFormRights
                .FirstOrDefaultAsync(r => r.UserId == id && r.FormCode == dto.FormCode);
            if (existing == null)
            {
                _db.UserFormRights.Add(new UserFormRight
                {
                    UserId = id, FormCode = dto.FormCode,
                    CanView = dto.CanView, CanAdd = dto.CanAdd,
                    CanEdit = dto.CanEdit, CanDelete = dto.CanDelete,
                });
            }
            else
            {
                existing.CanView = dto.CanView; existing.CanAdd = dto.CanAdd;
                existing.CanEdit = dto.CanEdit; existing.CanDelete = dto.CanDelete;
            }
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- My rights (for the logged-in user) ----------
    [HttpGet("my-rights")]
    public async Task<ActionResult<List<RightDto>>> MyRights()
    {
        var userId = _users.GetUserId(User);
        if (userId == null) return Unauthorized();
        return await GetRights(userId);
    }
}
