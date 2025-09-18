using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.ClassRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize] // policy EmailVerified opcional aqui
[ApiController]
[Route("api/v1/class-requests")]
public sealed class ClassRequestsController(AppDbContext db, IUserContext user, IHttpContextAccessor http) : ControllerBase
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
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var q = db.ClassRequests.AsNoTracking();

        if (clientId is { } cid) q = q.Where(x => x.ClientId == cid);
        if (staffId  is { } sid) q = q.Where(x => x.StaffId == sid);
        if (from     is { } f)   q = q.Where(x => x.ProposedStartUtc >= DateTime.SpecifyKind(f, DateTimeKind.Utc));
        if (to       is { } t)   q = q.Where(x => x.ProposedStartUtc <  DateTime.SpecifyKind(t, DateTimeKind.Utc));
        if (status   is { } st)  q = q.Where(x => (int)x.Status == st);

        var total = await q.CountAsync(ct);

        var items = await q.OrderBy(x => x.ProposedStartUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClassRequestListItemDto(
                x.Id, x.ClientId, x.StaffId, x.ProposedStartUtc, x.DurationMinutes, x.Notes, (int)x.Status))
            .ToListAsync(ct);

        return Ok(PagedResult<ClassRequestListItemDto>.Create(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var x = await db.ClassRequests.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (x is null) return NotFound();

        return Ok(new ClassRequestResponse(
            x.Id, x.ClientId, x.StaffId, x.ProposedStartUtc, x.DurationMinutes, x.Notes,
            (int)x.Status, x.CreatedByUid, x.CreatedAtUtc));
    }

    // POST /api/v1/class-requests  (STAFF cria pedido para um CLIENTE)
    [HttpPost]
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> CreateForClient(
        [FromBody] CreateClassRequestByStaff body,
        CancellationToken ct)
    {
        // 1) Garantir que o utilizador está mapeado a um Staff (UID -> Staff.FirebaseUid)
        var uid = user.Uid;
        if (string.IsNullOrWhiteSpace(uid)) return Forbid();

        var staff = await db.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.FirebaseUid == uid, ct);

        if (staff is null)
            return Forbid(); // tem role Staff no token mas não está mapeado na BD

        var staffId = staff.Id;

        // 2) Resolver e validar o ClientId (existência obrigatória)
        Guid clientId;
        try
        {
            clientId = await ResolveClientIdAsync(body, db, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }

        var clientExists = await db.Clients.AnyAsync(c => c.Id == clientId, ct);
        if (!clientExists)
            return BadRequest(new { detail = "ClientId inválido ou inexistente." });

        // 3) Validações de negócio (duração + data futura) e normalização para UTC
        if (body.DurationMinutes < 15 || body.DurationMinutes > 180)
            return BadRequest(new { detail = "durationMinutes deve estar entre 15 e 180." });

        var bStart = DateTime.SpecifyKind(body.ProposedStartUtc, DateTimeKind.Utc);
        var bDur   = body.DurationMinutes;

        if (bStart <= DateTime.UtcNow)
            return Problem(statusCode: 400, title: "Data inválida",
                           detail: "proposedStartUtc deve ser no futuro (UTC).");

        // 4) Conflitos: Pedidos pendentes do mesmo Staff (100% traduzível p/ SQL)
        var conflictPending = await db.ClassRequests
            .Where(c => c.StaffId == staffId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, bStart) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, c.ProposedStartUtc) < bDur,
                ct);

        if (conflictPending)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe um pedido pendente para este staff no mesmo horário.");

        // 5) Conflitos: Aulas agendadas do mesmo Staff (100% traduzível p/ SQL)
        var conflictClasses = await db.Classes
            .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, bStart) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, k.StartUtc) < bDur,
                ct);

        if (conflictClasses)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe uma aula agendada para este staff no mesmo horário.");

        // 6) Criar o pedido (Pending)
        var req = new AlmaApp.Domain.ClassRequests.ClassRequest(
            clientId: clientId,
            staffId:  staffId,
            proposedStartUtc: bStart,
            durationMinutes:  bDur,
            notes: body.Notes,
            createdByUid: uid!);

        db.ClassRequests.Add(req);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = req.Id }, new { req.Id });
    }

    // GET /api/v1/me/class-requests  (Cliente vê os seus pedidos)
    [HttpGet("me/client")]
    [Authorize(Policy = "Client")]
    public async Task<IActionResult> MyClientRequests(CancellationToken ct)
    {
        var client = await db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.FirebaseUid == user.Uid, ct);

        if (client is null) return Forbid();

        var items = await db.ClassRequests.AsNoTracking()
            .Where(c => c.ClientId == client.Id)
            .OrderByDescending(c => c.ProposedStartUtc)
            .Select(c => new {
                c.Id, c.StaffId, c.ProposedStartUtc, c.DurationMinutes, c.Status, c.Notes
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var req = await db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return NotFound();
        if (req.Status != ClassRequestStatus.Pending)
            return Problem("Só pedidos pendentes podem ser editados.", statusCode: 409);
        if (req.CreatedByUid != user.Uid) return Forbid();

        // normalizar e validar duração
        if (body.DurationMinutes < 15 || body.DurationMinutes > 180)
            return BadRequest(new { detail = "durationMinutes deve estar entre 15 e 180." });

        var startUtc = DateTime.SpecifyKind(body.ProposedStartUtc, DateTimeKind.Utc);
        if (startUtc <= DateTime.UtcNow)
            return Problem(statusCode: 400, title: "Data inválida",
                           detail: "proposedStartUtc deve ser no futuro (UTC).");

        var bDur = body.DurationMinutes;

        // overlap só com PENDENTES de outro registo, para o mesmo Staff
        var overlap = await db.ClassRequests
            .Where(r => r.Id != id &&
                        r.Status == ClassRequestStatus.Pending &&
                        r.StaffId == body.StaffId)
            .AnyAsync(r =>
                EF.Functions.DateDiffMinute(r.ProposedStartUtc, startUtc) < r.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, r.ProposedStartUtc) < bDur,
                ct);

        if (overlap)
            return Conflict(new ProblemDetails
            {
                Title = "Conflito de agenda",
                Detail = "Já existe um pedido pendente para este staff no mesmo horário."
            });

        req.Update(body.ClientId, body.StaffId, startUtc, body.DurationMinutes, body.Notes);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var req = await db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return NotFound();
        if (req.Status != ClassRequestStatus.Pending)
            return Problem("Só pedidos pendentes podem ser cancelados.", statusCode: 409);
        if (req.CreatedByUid != user.Uid) return Forbid();

        req.Cancel();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // === DTO de criação pelo staff ===
    public record CreateClassRequestByStaff(
        Guid?     ClientId,
        string?   ClientEmail,
        string?   ClientUid,
        DateTime  ProposedStartUtc,
        int       DurationMinutes,
        string?   Notes
    );

    // === Resolver clientId a partir de um (e só um) identificador ===
    private static async Task<Guid> ResolveClientIdAsync(
        CreateClassRequestByStaff body,
        AppDbContext db,
        CancellationToken ct)
    {
        // garantir que só vem um identificador
        var provided = new[]
        {
            body.ClientId is not null,
            !string.IsNullOrWhiteSpace(body.ClientEmail),
            !string.IsNullOrWhiteSpace(body.ClientUid)
        }.Count(x => x);

        if (provided != 1)
            throw new ArgumentException("Indica exatamente um de: clientId, clientEmail ou clientUid.");

        if (body.ClientId is Guid idFromBody) return idFromBody;

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
