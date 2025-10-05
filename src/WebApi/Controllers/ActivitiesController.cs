using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AlmaApp.Domain.Activities;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Activities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/activities")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ActivitiesController(AppDbContext db) => _db = db;

    private string CurrentUid =>
        User.FindFirst("user_id")?.Value ??
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
        throw new InvalidOperationException("Missing user id.");

    // GET /api/v1/activities?roomId=&from=&to=&status=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<PagedResult<ActivityListItemDto>>> Search(
        [FromQuery] Guid? roomId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var q = _db.Activities.AsNoTracking();

        if (roomId is { } rid) q = q.Where(x => x.RoomId == rid);
        if (from   is { } f)   q = q.Where(x => x.StartUtc >= AppTime.ToUtcFromLocal(f));
        if (to     is { } t)   q = q.Where(x => x.StartUtc <  AppTime.ToUtcFromLocal(t));
        if (status is { } st)  q = q.Where(x => (int)x.Status == st);

        var total = await q.CountAsync();

        var items = await q.OrderBy(x => x.StartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ActivityListItemDto(
                x.Id, x.RoomId, x.Title, AppTime.ToLocalFromUtc(x.StartUtc), x.DurationMinutes, (int)x.Status))
            .ToListAsync();

        return Ok(PagedResult<ActivityListItemDto>.Create(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var a = await _db.Activities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();

        return Ok(new ActivityResponse(
            a.Id, a.RoomId, a.Title, a.Description,
            AppTime.ToLocalFromUtc(a.StartUtc), a.StartUtc, a.DurationMinutes,
            (int)a.Status, a.CreatedByUid, a.CreatedAtUtc));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityRequestDto body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var roomExists = await _db.Rooms.AnyAsync(x => x.Id == body.RoomId);
        if (!roomExists) return Problem("RoomId inválido.", statusCode: 400);

        var startLocal = body.Start;
        var startUtc   = AppTime.ToUtcFromLocal(startLocal);
        var endUtc     = startUtc.AddMinutes(body.DurationMinutes);

        // Conflitos com outras Activities
        var activityConflict = await _db.Activities
            .Where(x => x.Status == ActivityStatus.Scheduled && x.RoomId == body.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (activityConflict) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Activity/Room)" });

        // Conflitos com Classes
        var classConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == body.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (classConflict) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Class/Room)" });

        var a = new Activity(
            roomId:          body.RoomId,
            title:           body.Title,
            description:     body.Description,
            startUtc:        startUtc,
            durationMinutes: body.DurationMinutes,
            createdByUid:    CurrentUid);

        _db.Activities.Add(a);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = a.Id },
            new ActivityResponse(
                a.Id, a.RoomId, a.Title, a.Description,
                AppTime.ToLocalFromUtc(a.StartUtc), a.StartUtc, a.DurationMinutes,
                (int)a.Status, a.CreatedByUid, a.CreatedAtUtc));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityRequestDto body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var a = await _db.Activities.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();

        var roomExists = await _db.Rooms.AnyAsync(x => x.Id == body.RoomId);
        if (!roomExists) return Problem("RoomId inválido.", statusCode: 400);

        var startLocal = body.Start;
        var startUtc   = AppTime.ToUtcFromLocal(startLocal);
        var endUtc     = startUtc.AddMinutes(body.DurationMinutes);

        var activityConflict = await _db.Activities
            .Where(x => x.Id != id && x.Status == ActivityStatus.Scheduled && x.RoomId == body.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (activityConflict) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Activity/Room)" });

        var classConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == body.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes));
        if (classConflict) return Conflict(new ProblemDetails { Title = "Conflito de agenda (Class/Room)" });

        a.Update(
            roomId:          body.RoomId,
            title:           body.Title,
            description:     body.Description,
            startUtc:        startUtc,
            durationMinutes: body.DurationMinutes);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var a = await _db.Activities.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        a.Cancel();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var a = await _db.Activities.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        a.Complete();
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
