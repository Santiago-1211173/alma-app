using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/me")]
public sealed class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IUserContext _user;

    public MeController(AppDbContext db, IUserContext user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get()
    {
        // UID/Email/Verified a partir do contexto (Firebase JWT + DB)
        var uid = _user.Uid ?? throw new InvalidOperationException("Missing user id.");
        var email = _user.Email;
        var emailVerified = _user.EmailVerified;

        // Mapeamento para domínio
        var clientId = await _db.Clients
            .AsNoTracking()
            .Where(c => c.FirebaseUid == uid)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        var staffId = await _db.Staff
            .AsNoTracking()
            .Where(s => s.FirebaseUid == uid)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync();

        // Roles atuais (tabela RoleAssignments)
        var roles = await _db.RoleAssignments
            .AsNoTracking()
            .Where(r => r.FirebaseUid == uid)
            .Select(r => r.Role) // se for enum, converte para string
            .Select(r => r.ToString())
            .ToArrayAsync();

        return Ok(new MeResponse(uid, email, emailVerified, clientId, staffId, roles));
    }
}
