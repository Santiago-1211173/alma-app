using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using AlmaApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Common.Auth;

public sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;

    public UserContext(IHttpContextAccessor http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    private ClaimsPrincipal User => _http.HttpContext?.User ?? new ClaimsPrincipal();

    public string? Uid =>
        User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? Email => User.FindFirst(ClaimTypes.Email)?.Value;

    public string? DisplayName
    {
        get
        {
            // Firebase costuma emitir "name" quando existe displayName no utilizador
            var name = User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrWhiteSpace(name)) return name;

            // fallback: deriva do e-mail (ex.: "ana.silva" -> "ana silva")
            if (string.IsNullOrWhiteSpace(Email)) return null;
            var basePart = Email.Split('@')[0].Replace('.', ' ').Replace('_', ' ');
            return string.Join(' ', basePart.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        }
    }

    public bool EmailVerified
        => (User.FindFirst("email_verified")?.Value) is "true" or "True" or "1";

    public async Task<bool> IsInRoleAsync(RoleName role, CancellationToken ct = default)
    {
        var uid = Uid;
        if (string.IsNullOrEmpty(uid)) return false;

        return await _db.RoleAssignments
            .AnyAsync(r => r.FirebaseUid == uid && r.Role == role, ct);
    }

    public async Task<IReadOnlyList<RoleName>> GetRolesAsync(CancellationToken ct = default)
    {
        var uid = Uid;
        if (string.IsNullOrEmpty(uid)) return Array.Empty<RoleName>();

        return await _db.RoleAssignments
            .Where(r => r.FirebaseUid == uid)
            .Select(r => r.Role)
            .ToListAsync(ct);
    }
}
