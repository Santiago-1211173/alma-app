using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/classes")]
public sealed class ClassesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ClassesController(AppDbContext db) => _db = db;

    private string CurrentUid =>
        User.FindFirst("user_id")?.Value ??
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
        throw new InvalidOperationException("Missing user id.");

    // GET /api/v1/classes?clientId=&staffId=&roomId=&from=&to=&status=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassListItemDto>>> Search(
        [FromQuery] Guid? clientId, [FromQuery] Guid? staffId, [FromQuery] Guid? roomId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var q = _db.Classes.AsNoTracking();

        if (clientId is { } cid) q = q.Where(x => x.ClientId == cid);
        if (staffId  is { } sid) q = q.Where(x => x.StaffId  == sid);
        if (roomId   is { } rid) q = q.Where(x => x.RoomId   == rid);
        if (from     is { } f  ) q = q.Where(x => x.StartUtc >= DateTime.SpecifyKind(f, DateTimeKind.Utc));
        if (to       is { } t  ) q = q.Where(x => x.StartUtc <  DateTime.SpecifyKind(t, DateTimeKind.Utc));
        if (status   is { } st ) q = q.Where(x => (int)x.Status == st);

        var total = await q.CountAsync();

        var items = await q.OrderBy(x => x.StartUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClassListItemDto(
                x.Id, x.ClientId, x.StaffId, x.RoomId, x.StartUtc, x.DurationMinutes, (int)x.Status))
            .ToListAsync();

        return Ok(PagedResult<ClassListItemDto>.Create(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await _db.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        return Ok(new ClassResponse(
            c.Id, c.ClientId, c.StaffId, c.RoomId, c.StartUtc, c.DurationMinutes, (int)c.Status,
            c.LinkedRequestId, c.CreatedByUid, c.CreatedAtUtc));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassRequestDto body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ok = await _db.Clients.AnyAsync(x => x.Id == body.ClientId) &&
                 await _db.Staff.AnyAsync(x => x.Id == body.StaffId)   &&
                 await _db.Rooms.AnyAsync(x => x.Id == body.RoomId);
        if (!ok) return Problem("ClientId/StaffId/RoomId inválido(s).", statusCode: 400);

        var start = DateTime.SpecifyKind(body.StartUtc, DateTimeKind.Utc);
        var end   = start.AddMinutes(body.DurationMinutes);

        // Conflitos com Staff
        var staffBusy = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.StaffId == body.StaffId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes));

        if (staffBusy) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Staff)" });

        // Conflitos com Room
        var roomBusy = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == body.RoomId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes));

        if (roomBusy) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Room)" });

        var cls = new Class(body.ClientId, body.StaffId, body.RoomId, start, body.DurationMinutes, CurrentUid);

        _db.Classes.Add(cls);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = cls.Id },
            new ClassResponse(cls.Id, cls.ClientId, cls.StaffId, cls.RoomId, cls.StartUtc, cls.DurationMinutes,
                              (int)cls.Status, cls.LinkedRequestId, cls.CreatedByUid, cls.CreatedAtUtc));
    }

    // POST /api/v1/classes/from-request/{requestId}
    [HttpPost("from-request/{requestId:guid}")]
    public async Task<IActionResult> CreateFromRequest(Guid requestId, [FromBody] CreateClassFromRequestDto body)
    {
        var req = await _db.ClassRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (req is null) return NotFound();

        if (req.Status != ClassRequestStatus.Pending)
            return Problem("Só pedidos pendentes podem originar uma aula.", statusCode: 409);

        // validações de existência
        var ok = await _db.Clients.AnyAsync(x => x.Id == req.ClientId) &&
                 await _db.Staff.AnyAsync(x => x.Id == req.StaffId)     &&
                 await _db.Rooms.AnyAsync(x => x.Id == body.RoomId);
        if (!ok) return Problem("Client/Staff/Room inválido(s).", statusCode: 400);

        var start = req.ProposedStartUtc;
        var end   = start.AddMinutes(req.DurationMinutes);

        var staffBusy = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.StaffId == req.StaffId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (staffBusy) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Staff)" });

        var roomBusy = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == body.RoomId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (roomBusy) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Room)" });

        var cls = new Class(req.ClientId, req.StaffId, body.RoomId, start, req.DurationMinutes, CurrentUid, req.Id);
        _db.Classes.Add(cls);

        req.Approve();
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = cls.Id },
            new ClassResponse(cls.Id, cls.ClientId, cls.StaffId, cls.RoomId, cls.StartUtc, cls.DurationMinutes,
                              (int)cls.Status, cls.LinkedRequestId, cls.CreatedByUid, cls.CreatedAtUtc));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequestDto body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id);
        if (cls is null) return NotFound();
        if (cls.Status != ClassStatus.Scheduled) return Problem("Só aulas agendadas podem ser editadas.", statusCode: 409);

        var start = DateTime.SpecifyKind(body.StartUtc, DateTimeKind.Utc);
        var end   = start.AddMinutes(body.DurationMinutes);

        var staffBusy = await _db.Classes
            .Where(x => x.Id != id && x.Status == ClassStatus.Scheduled && x.StaffId == cls.StaffId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (staffBusy) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Staff)" });

        var roomBusy = await _db.Classes
            .Where(x => x.Id != id && x.Status == ClassStatus.Scheduled && x.RoomId == body.RoomId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (roomBusy) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Room)" });

        cls.Reschedule(start, body.DurationMinutes, body.RoomId);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id);
        if (cls is null) return NotFound();
        cls.Cancel();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id);
        if (cls is null) return NotFound();
        cls.Complete();
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
