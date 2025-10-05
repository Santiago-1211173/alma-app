using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class MeService : IMeService
{
    private readonly AppDbContext _db;
    private readonly IUserContext _user;

    public MeService(AppDbContext db, IUserContext user)
    {
        _db = db;
        _user = user;
    }

    public async Task<ServiceResult<MeResponse>> GetAsync(CancellationToken ct)
    {
        var uid = _user.Uid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            return ServiceResult<MeResponse>.Fail(new ServiceError(401, "Missing user id."));
        }

        var email = _user.Email;
        var emailVerified = _user.EmailVerified;

        var clientId = await _db.Clients
            .AsNoTracking()
            .Where(c => c.FirebaseUid == uid)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        var staffId = await _db.Staff
            .AsNoTracking()
            .Where(s => s.FirebaseUid == uid)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

        var roles = await _db.RoleAssignments
            .AsNoTracking()
            .Where(r => r.FirebaseUid == uid)
            .Select(r => r.Role.ToString())
            .ToArrayAsync(ct);

        var response = new MeResponse(uid, email, emailVerified, clientId, staffId, roles);
        return ServiceResult<MeResponse>.Ok(response);
    }
}
