using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class AdminRbacService : IAdminRbacService
{
    private readonly AppDbContext _db;

    public AdminRbacService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<UserRolesResponse>> GetRolesAsync(string uid, CancellationToken ct)
    {
        var (ok, problem) = ValidateUid(uid);
        if (!ok)
        {
            return ServiceResult<UserRolesResponse>.Fail(problem!);
        }

        var roles = await _db.RoleAssignments
            .Where(r => r.FirebaseUid == uid.Trim())
            .Select(r => r.Role)
            .ToListAsync(ct);

        return ServiceResult<UserRolesResponse>.Ok(new UserRolesResponse(uid.Trim(), roles));
    }

    public async Task<ServiceResult> AssignRoleAsync(string uid, AssignRoleRequest request, CancellationToken ct)
    {
        var (ok, problem) = ValidateUid(uid);
        if (!ok)
        {
            return ServiceResult.Fail(problem!);
        }

        uid = uid.Trim();

        var exists = await _db.RoleAssignments
            .AnyAsync(r => r.FirebaseUid == uid && r.Role == request.Role, ct);

        if (exists)
        {
            return ServiceResult.Fail(new ServiceError(409, "Role já atribuída a este utilizador."));
        }

        var entity = new RoleAssignment(uid, request.Role);
        await _db.RoleAssignments.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveRoleAsync(string uid, RoleName role, CancellationToken ct)
    {
        var (ok, problem) = ValidateUid(uid);
        if (!ok)
        {
            return ServiceResult.Fail(problem!);
        }

        uid = uid.Trim();

        var entity = await _db.RoleAssignments
            .FirstOrDefaultAsync(r => r.FirebaseUid == uid && r.Role == role, ct);

        if (entity is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Role assignment not found"));
        }

        _db.RoleAssignments.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return ServiceResult.Ok();
    }

    private static (bool ok, ServiceError? error) ValidateUid(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return (false, new ServiceError(400, "Parâmetro 'uid' é obrigatório."));
        }

        var trimmed = uid.Trim();

        if (trimmed.Length > 128)
        {
            return (false, new ServiceError(400, "O 'uid' excede o tamanho máximo (128)."));
        }

        var dotCount = trimmed.Count(c => c == '.');
        var looksLikeJwt = dotCount >= 2 || (trimmed.StartsWith("eyJ", StringComparison.Ordinal) && dotCount >= 1);

        if (looksLikeJwt)
        {
            return (false, new ServiceError(400, "Foi detetado um token JWT no lugar do Firebase UID."));
        }

        return (true, null);
    }
}
