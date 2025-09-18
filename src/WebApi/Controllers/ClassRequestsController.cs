using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.ClassRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AlmaApp.WebApi.Common.Auth;

namespace AlmaApp.WebApi.Controllers;

[Authorize] // policy EmailVerified opcional aqui
[ApiController]
[Route("api/v1/class-requests")]
public sealed class ClassRequestsController(AppDbContext db, IHttpContextAccessor http) : ControllerBase
{
    private string CurrentUid =>
        http.HttpContext?.User.FindFirst("user_id")?.Value
        ?? http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("Missing user id.");

    // GET /api/v1/class-requests?clientId=&staffId=&from=&to=&status=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassRequestListItemDto>>> Search(
        [FromQuery] Guid? clientId, [FromQuery] Guid? staffId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var q = db.ClassRequests.AsNoTracking();

        if (clientId is { } cid) q = q.Where(x => x.ClientId == cid);
        if (staffId is { } sid) q = q.Where(x => x.StaffId == sid);
        if (from is { } f) q = q.Where(x => x.ProposedStartUtc >= DateTime.SpecifyKind(f, DateTimeKind.Utc));
        if (to is { } t) q = q.Where(x => x.ProposedStartUtc < DateTime.SpecifyKind(t, DateTimeKind.Utc));
        if (status is { } st) q = q.Where(x => (int)x.Status == st);

        var total = await q.CountAsync();

        var items = await q.OrderBy(x => x.ProposedStartUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClassRequestListItemDto(x.Id, x.ClientId, x.StaffId, x.ProposedStartUtc, x.DurationMinutes, x.Notes, (int)x.Status))
            .ToListAsync();

        return Ok(PagedResult<ClassRequestListItemDto>.Create(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var x = await db.ClassRequests.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (x is null) return NotFound();
        return Ok(new ClassRequestResponse(x.Id, x.ClientId, x.StaffId, x.ProposedStartUtc, x.DurationMinutes, x.Notes, (int)x.Status, x.CreatedByUid, x.CreatedAtUtc));
    }

    // POST /api/v1/class-requests  (STAFF cria pedido para um CLIENTE)
    [HttpPost]
    [Authorize(Policy = "Staff, Admin" )]
    public async Task<IActionResult> CreateForClient(
        [FromBody] CreateClassRequestByStaff body,
        [FromServices] AppDbContext db,
        [FromServices] IUserContext user,
        CancellationToken ct)
    {
        // 1) staffId a partir do token
        var staffId = await user.RequireStaffIdAsync(db, ct);

        // 2) clientId a partir do corpo
        Guid clientId;
        try
        {
            clientId = await ResolveClientIdAsync(body, db, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }

        // 3) validações simples
        if (body.DurationMinutes < 15 || body.DurationMinutes > 180)
            return BadRequest(new { detail = "durationMinutes deve estar entre 15 e 180." });

        // 4) conflito de agenda (100% traduzível p/ SQL)
        var bStart = DateTime.SpecifyKind(body.ProposedStartUtc, DateTimeKind.Utc);
        var bEnd   = bStart.AddMinutes(body.DurationMinutes);

        var hasConflict = await db.ClassRequests
            .Where(c => c.Status == AlmaApp.Domain.ClassRequests.ClassRequestStatus.Pending
                    && c.StaffId == staffId
                    && c.ProposedStartUtc < bEnd) // a outra aula começa antes do fim da nova
            .AnyAsync(c =>
                // minutos entre o início da outra aula e o início da nova
                // têm de ser < duração da outra aula para haver sobreposição
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, bStart) < c.DurationMinutes,
                ct);

        if (hasConflict)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe um pedido pendente para este staff no mesmo horário.");

        // 5) criar pedido
        var req = new AlmaApp.Domain.ClassRequests.ClassRequest(
            clientId: clientId,
            staffId:  staffId,
            proposedStartUtc: bStart,
            durationMinutes:  body.DurationMinutes,
            notes: body.Notes,
            createdByUid: user.Uid!);

        db.ClassRequests.Add(req);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = req.Id }, new { req.Id });
    }

    // GET /api/v1/me/class-requests  (Cliente vê os seus pedidos)
    [HttpGet("me/client")]
    [Authorize(Policy = "Client, Admin")]
    public async Task<IActionResult> MyClientRequests(AppDbContext db, IUserContext user, CancellationToken ct)
    {
        var clientId = await user.RequireClientIdAsync(db, ct);
        var items = await db.ClassRequests.AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.ProposedStartUtc)
            .Select(c => new {
                c.Id, c.StaffId, c.ProposedStartUtc, c.DurationMinutes, c.Status, c.Notes
            })
            .ToListAsync(ct);

        return Ok(items);
    }


    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var req = await db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return NotFound();
        if (req.Status != ClassRequestStatus.Pending) return Problem("Só pedidos pendentes podem ser editados.", statusCode: 409);
        if (req.CreatedByUid != CurrentUid) return Forbid();

        var startUtc = DateTime.SpecifyKind(body.ProposedStartUtc, DateTimeKind.Utc);
        var endUtc = startUtc.AddMinutes(body.DurationMinutes);

        var overlap = await db.ClassRequests
        .Where(r => r.Id != id &&
                    r.Status == ClassRequestStatus.Pending &&
                    r.StaffId == body.StaffId)
        .AnyAsync(r =>
            r.ProposedStartUtc < endUtc &&
            startUtc < r.ProposedStartUtc.AddMinutes(r.DurationMinutes)
        );

        if (overlap)
            return Conflict(new ProblemDetails { Title = "Conflito de agenda", Detail = "Já existe um pedido pendente para este staff no mesmo horário." });

        req.Update(body.ClientId, body.StaffId, startUtc, body.DurationMinutes, body.Notes);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var req = await db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return NotFound();
        if (req.Status != ClassRequestStatus.Pending) return Problem("Só pedidos pendentes podem ser cancelados.", statusCode: 409);
        if (req.CreatedByUid != CurrentUid) return Forbid();

        req.Cancel();
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateClassRequestByStaff(
        Guid? ClientId,
        string? ClientEmail,
        string? ClientUid,
        DateTime ProposedStartUtc,
        int DurationMinutes,
        string? Notes
    );
    
    private static async Task<Guid> ResolveClientIdAsync(
        CreateClassRequestByStaff body,
        AppDbContext db,
        CancellationToken ct)
    {
        // garantir que só vem um identificador
        var provided = new[] {
            body.ClientId is not null,
            !string.IsNullOrWhiteSpace(body.ClientEmail),
            !string.IsNullOrWhiteSpace(body.ClientUid)
        }.Count(x => x);

        if (provided != 1)
            throw new ArgumentException("Indica exatamente um de: clientId, clientEmail ou clientUid.");

        if (body.ClientId is Guid id) return id;

        var query = db.Clients.AsNoTracking().Select(c => new { c.Id, c.Email, c.FirebaseUid });

        if (!string.IsNullOrWhiteSpace(body.ClientEmail))
        {
            var email = body.ClientEmail!.Trim().ToLowerInvariant();
            var found = await query.FirstOrDefaultAsync(c => c.Email == email, ct);
            if (found is null) throw new ArgumentException("ClientEmail não encontrado.");
            return found.Id;
        }

        // ClientUid
        var uid = body.ClientUid!.Trim();
        var foundUid = await query.FirstOrDefaultAsync(c => c.FirebaseUid == uid, ct);
        if (foundUid is null) throw new ArgumentException("ClientUid não encontrado.");
        return foundUid.Id;
    }


}
