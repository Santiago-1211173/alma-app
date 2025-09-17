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
        if (staffId  is { } sid) q = q.Where(x => x.StaffId  == sid);
        if (from     is { } f  ) q = q.Where(x => x.ProposedStartUtc >= DateTime.SpecifyKind(f, DateTimeKind.Utc));
        if (to       is { } t  ) q = q.Where(x => x.ProposedStartUtc <  DateTime.SpecifyKind(t, DateTimeKind.Utc));
        if (status   is { } st ) q = q.Where(x => (int)x.Status == st);

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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // (sanity) garantir que Client/Staff existem
        var exists = await db.Clients.AnyAsync(c => c.Id == body.ClientId) &&
                     await db.Staff.AnyAsync(s => s.Id == body.StaffId);
        if (!exists) return Problem("ClientId ou StaffId inválido(s).", statusCode: 400);

        var startUtc = DateTime.SpecifyKind(body.ProposedStartUtc, DateTimeKind.Utc);
        if (startUtc < DateTime.UtcNow.AddMinutes(-1)) return Problem("ProposedStartUtc no passado.", statusCode: 400);
        var endUtc   = startUtc.AddMinutes(body.DurationMinutes);

        // Regra de conflito simples: outro pedido PENDING do mesmo staff em overlap
        var overlap = await db.ClassRequests
        .Where(r => r.Status == ClassRequestStatus.Pending && r.StaffId == body.StaffId)
        .AnyAsync(r =>
            r.ProposedStartUtc < endUtc &&
            startUtc < r.ProposedStartUtc.AddMinutes(r.DurationMinutes)
        );

        if (overlap)
            return Conflict(new ProblemDetails { Title = "Conflito de agenda", Detail = "Já existe um pedido pendente para este staff no mesmo horário." });

        var req = new ClassRequest(body.ClientId, body.StaffId, startUtc, body.DurationMinutes, body.Notes, CurrentUid);

        db.ClassRequests.Add(req);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = req.Id },
            new ClassRequestResponse(req.Id, req.ClientId, req.StaffId, req.ProposedStartUtc, req.DurationMinutes, req.Notes, (int)req.Status, req.CreatedByUid, req.CreatedAtUtc));
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
        var endUtc   = startUtc.AddMinutes(body.DurationMinutes);

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
}
