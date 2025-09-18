using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Domain.Auth;
using AlmaApp.Domain.Rooms;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.ClassRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    // GET /api/v1/class-requests?clientId=&staffId=&from=&to=&roomId=&status=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassRequestListItemDto>>> Search(
        [FromQuery] Guid? clientId, [FromQuery] Guid? staffId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? roomId,
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var q = db.ClassRequests.AsNoTracking();

        if (clientId is { } cid) q = q.Where(x => x.ClientId == cid);
        if (staffId  is { } sid) q = q.Where(x => x.StaffId == sid);
        if (roomId   is { } rid) q = q.Where(x => x.RoomId == rid);
        if (from     is { } f)   q = q.Where(x => x.ProposedStartUtc >= DateTime.SpecifyKind(f, DateTimeKind.Utc));
        if (to       is { } t)   q = q.Where(x => x.ProposedStartUtc <  DateTime.SpecifyKind(t, DateTimeKind.Utc));
        if (status   is { } st)  q = q.Where(x => (int)x.Status == st);

        var total = await q.CountAsync(ct);

        var items = await q.OrderBy(x => x.ProposedStartUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClassRequestListItemDto(
                x.Id, x.ClientId, x.StaffId, x.RoomId, x.ProposedStartUtc, x.DurationMinutes, x.Notes, (int)x.Status))
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
            x.Id, x.ClientId, x.StaffId, x.RoomId, x.ProposedStartUtc, x.DurationMinutes, x.Notes,
            (int)x.Status, x.CreatedByUid, x.CreatedAtUtc));
    }

    // POST /api/v1/class-requests  (STAFF cria pedido para um CLIENTE, já com RoomId)
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

        // 2) Resolver e validar o ClientId
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

        // 2.1) Validar RoomId e existência
        if (body.RoomId == Guid.Empty)
            return BadRequest(new { detail = "roomId é obrigatório." });
        var roomExists = await db.Rooms.AsNoTracking().AnyAsync(r => r.Id == body.RoomId, ct);
        if (!roomExists)
            return BadRequest(new { detail = "roomId inválido ou inexistente." });

        // 3) Validações de negócio
        if (body.DurationMinutes < 15 || body.DurationMinutes > 180)
            return BadRequest(new { detail = "durationMinutes deve estar entre 15 e 180." });

        var bStart = DateTime.SpecifyKind(body.ProposedStartUtc, DateTimeKind.Utc);
        var bDur   = body.DurationMinutes;

        if (bStart <= DateTime.UtcNow)
            return Problem(statusCode: 400, title: "Data inválida",
                           detail: "proposedStartUtc deve ser no futuro (UTC).");

        // 4) Conflitos por STAFF (Pending + Scheduled)
        var conflictPendingStaff = await db.ClassRequests
            .Where(c => c.StaffId == staffId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, bStart) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, c.ProposedStartUtc) < bDur,
                ct);

        if (conflictPendingStaff)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe um pedido pendente para este staff no mesmo horário.");

        var conflictClassesStaff = await db.Classes
            .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, bStart) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, k.StartUtc) < bDur,
                ct);

        if (conflictClassesStaff)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe uma aula agendada para este staff no mesmo horário.");

        // 5) Conflitos por ROOM (Pending + Scheduled)
        var conflictPendingRoom = await db.ClassRequests
            .Where(c => c.RoomId == body.RoomId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, bStart) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, c.ProposedStartUtc) < bDur,
                ct);

        if (conflictPendingRoom)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe um pedido pendente para esta sala no mesmo horário.");

        var conflictClassesRoom = await db.Classes
            .Where(k => k.RoomId == body.RoomId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, bStart) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, k.StartUtc) < bDur,
                ct);

        if (conflictClassesRoom)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe uma aula agendada para esta sala no mesmo horário.");

        // 6) Criar o pedido (Pending)
        var req = new ClassRequest(
            clientId: clientId,
            staffId:  staffId,
            roomId:   body.RoomId,
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
                c.Id, c.StaffId, c.RoomId, c.ProposedStartUtc, c.DurationMinutes, c.Status, c.Notes
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    // PUT — agora permite alterar RoomId (opcional)
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

        // RoomId pode vir a null no update => mantém o atual
        var newRoomId = body.RoomId ?? req.RoomId;

        // existência de Staff/Client/Room
        var okClient = await db.Clients.AnyAsync(c => c.Id == body.ClientId, ct);
        var okStaff  = await db.Staff.AnyAsync(s => s.Id == body.StaffId, ct);
        var okRoom   = await db.Rooms.AnyAsync(r => r.Id == newRoomId, ct);
        if (!(okClient && okStaff && okRoom))
            return BadRequest(new { detail = "ClientId/StaffId/RoomId inválido(s) ou inexistente(s)." });

        var bDur = body.DurationMinutes;

        // overlap com PENDENTES (outros) do mesmo Staff
        var overlapPendingStaff = await db.ClassRequests
            .Where(r => r.Id != id &&
                        r.Status == ClassRequestStatus.Pending &&
                        r.StaffId == body.StaffId)
            .AnyAsync(r =>
                EF.Functions.DateDiffMinute(r.ProposedStartUtc, startUtc) < r.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, r.ProposedStartUtc) < bDur,
                ct);
        if (overlapPendingStaff)
            return Conflict(new ProblemDetails
            {
                Title = "Conflito de agenda",
                Detail = "Já existe um pedido pendente para este staff no mesmo horário."
            });

        // conflitos com aulas do Staff
        var conflictClassesStaff = await db.Classes
            .Where(k => k.StaffId == body.StaffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < bDur,
                ct);
        if (conflictClassesStaff)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe uma aula agendada para este staff no mesmo horário.");

        // conflitos por Room (Pending + Scheduled)
        var overlapPendingRoom = await db.ClassRequests
            .Where(c => c.Id != id && c.RoomId == newRoomId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, startUtc) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, c.ProposedStartUtc) < bDur,
                ct);
        if (overlapPendingRoom)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe um pedido pendente para esta sala no mesmo horário.");

        var conflictClassesRoom = await db.Classes
            .Where(k => k.RoomId == newRoomId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < bDur,
                ct);
        if (conflictClassesRoom)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe uma aula agendada para esta sala no mesmo horário.");

        req.Update(body.ClientId, body.StaffId, newRoomId, startUtc, body.DurationMinutes, body.Notes);
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

        // Permitir que o autor (Staff) cancele; Admin também poderá cancelar via role
        var isAdmin = await user.IsInRoleAsync(RoleName.Admin, ct);
        if (!isAdmin && req.CreatedByUid != user.Uid) return Forbid();

        req.Cancel();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // === Aprovar pedido (CLIENTE/Admin) — RoomId vem do pedido; sem body ===
    // POST /api/v1/class-requests/{id}/approve
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        // 1) Carregar pedido
        var req = await db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return NotFound();

        // 2) Autorizar: Admin OU Cliente dono do pedido
        var isAdmin = await user.IsInRoleAsync(RoleName.Admin, ct);
        if (!isAdmin)
        {
            var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.FirebaseUid == user.Uid, ct);
            if (client is null || client.Id != req.ClientId) return Forbid();
        }

        // 3) Estado correto
        if (req.Status != ClassRequestStatus.Pending)
            return Problem(statusCode: 409, title: "Pedido inválido", detail: "Só pedidos pendentes podem ser aprovados.");

        // 3.1) Room obrigatório no pedido
        if (req.RoomId == Guid.Empty)
            return Problem(statusCode: 400, title: "Pedido sem sala",
                detail: "O pedido não tem uma sala atribuída.");

        // 4) Revalidar conflitos (por Staff e por Room)
        var bStart  = req.ProposedStartUtc; // já em UTC no domínio
        var bDur    = req.DurationMinutes;
        var staffId = req.StaffId;
        var roomId  = req.RoomId;

        // Staff
        var conflictPendingStaff = await db.ClassRequests
            .Where(c => c.Id != req.Id && c.StaffId == staffId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, bStart) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, c.ProposedStartUtc) < bDur,
                ct);
        if (conflictPendingStaff)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe um pedido pendente para este staff no mesmo horário.");

        var conflictClassesStaff = await db.Classes
            .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, bStart) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, k.StartUtc) < bDur,
                ct);
        if (conflictClassesStaff)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe uma aula agendada para este staff no mesmo horário.");

        // Room
        var conflictPendingRoom = await db.ClassRequests
            .Where(c => c.Id != req.Id && c.RoomId == roomId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, bStart) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, c.ProposedStartUtc) < bDur,
                ct);
        if (conflictPendingRoom)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe um pedido pendente para esta sala no mesmo horário.");

        var conflictClassesRoom = await db.Classes
            .Where(k => k.RoomId == roomId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, bStart) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(bStart, k.StartUtc) < bDur,
                ct);
        if (conflictClassesRoom)
            return Problem(statusCode: 409, title: "Conflito de agenda",
                detail: "Já existe uma aula agendada para esta sala no mesmo horário.");

        // 5) Criar aula (Scheduled) e marcar request como Approved
        var klass = new Class(
            clientId: req.ClientId,
            staffId:  req.StaffId,
            roomId:   req.RoomId,
            startUtc: bStart,
            durationMinutes: bDur,
            createdByUid: user.Uid!,
            linkedRequestId: req.Id);

        db.Classes.Add(klass);
        req.Approve();

        await db.SaveChangesAsync(ct);

        // 6) Resposta com pedido aprovado + resumo da aula criada
        var response = new ClassRequestApprovedResponse(
            RequestId: req.Id,
            ClientId: req.ClientId,
            StaffId: req.StaffId,
            ProposedStartUtc: req.ProposedStartUtc,
            DurationMinutes: req.DurationMinutes,
            Notes: req.Notes,
            Status: (int)req.Status,
            CreatedByUid: req.CreatedByUid,
            CreatedAtUtc: req.CreatedAtUtc,
            ClassId: klass.Id,
            ClassStartUtc: klass.StartUtc,
            ClassDurationMinutes: klass.DurationMinutes,
            ClassRoomId: klass.RoomId,
            ClassStatus: (int)klass.Status
        );

        return Ok(response);
    }

    // === Rejeitar/Cancelar pelo CLIENTE (ou Admin) enquanto Pending ===
    // POST /api/v1/class-requests/{id}/reject
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var req = await db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return NotFound();

        // Autorizar: Admin OU Cliente dono
        var isAdmin = await user.IsInRoleAsync(RoleName.Admin, ct);
        if (!isAdmin)
        {
            var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.FirebaseUid == user.Uid, ct);
            if (client is null || client.Id != req.ClientId) return Forbid();
        }

        if (req.Status != ClassRequestStatus.Pending)
            return Problem(statusCode: 409, title: "Pedido inválido", detail: "Só pedidos pendentes podem ser rejeitados.");

        req.Cancel();
        await db.SaveChangesAsync(ct);

        return Ok(new ClassRequestResponse(
            req.Id, req.ClientId, req.StaffId, req.RoomId, req.ProposedStartUtc, req.DurationMinutes,
            req.Notes, (int)req.Status, req.CreatedByUid, req.CreatedAtUtc));
    }

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
