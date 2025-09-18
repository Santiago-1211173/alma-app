using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Availability;
using AlmaApp.Domain.Auth;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.Availability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/availability")]
public sealed class AvailabilityController(AppDbContext db, IUserContext user) : ControllerBase
{
    // ===========================
    // Helpers
    // ===========================
    private static bool TryParseTime(string s, out TimeOnly t)
        => TimeOnly.TryParseExact(s, "HH':'mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t);

    private static DateTime EnsureUtc(DateTime dt)
        => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private async Task<bool> IsAdminAsync(CancellationToken ct)
        => await user.IsInRoleAsync(RoleName.Admin, ct);

    private async Task<Guid?> MyStaffIdAsync(CancellationToken ct)
    {
        var uid = user.Uid;
        if (string.IsNullOrWhiteSpace(uid)) return null;

        var staff = await db.Staff
            .AsNoTracking()
            .Select(s => new { s.Id, s.FirebaseUid })
            .FirstOrDefaultAsync(s => s.FirebaseUid == uid, ct);

        return staff?.Id;
    }

    private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        => aStart < bEnd && bStart < aEnd;

    // ===========================
    // Staff Availability Rules
    // ===========================

    // GET /api/v1/availability/staff/{staffId}/rules
    [HttpGet("staff/{staffId:guid}/rules")]
    [Authorize(Policy = "Staff, Admin")]
    public async Task<IActionResult> ListRules(Guid staffId, CancellationToken ct)
    {
        var rows = await db.StaffAvailabilityRules.AsNoTracking()
            .Where(r => r.StaffId == staffId)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartTimeUtc)
            .ToListAsync(ct);

        var items = rows.Select(r => new StaffAvailabilityRuleDto(
            r.Id, r.StaffId, r.DayOfWeek,
            TimeOnly.FromTimeSpan(r.StartTimeUtc).ToString("HH:mm"),
            TimeOnly.FromTimeSpan(r.EndTimeUtc).ToString("HH:mm"),
            r.Active)).ToList();

        return Ok(items);
    }

    // POST /api/v1/availability/staff/{staffId}/rules
    [HttpPost("staff/{staffId:guid}/rules")]
    [Authorize(Policy = "Staff, Admin")]
    public async Task<IActionResult> CreateRule(Guid staffId, [FromBody] UpsertStaffAvailabilityRuleDto body, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct))
        {
            var myStaffId = await MyStaffIdAsync(ct);
            if (myStaffId is null || myStaffId.Value != staffId) return Forbid();
        }

        if (body.DayOfWeek < 0 || body.DayOfWeek > 6)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "dayOfWeek deve estar entre 0 e 6 (Sunday..Saturday).");

        if (!TryParseTime(body.StartTimeUtc, out var startTod) || !TryParseTime(body.EndTimeUtc, out var endTod))
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "StartTimeUtc/EndTimeUtc devem ter formato HH:mm.");

        if (endTod <= startTod)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "EndTimeUtc deve ser maior que StartTimeUtc no mesmo dia.");

        var rule = new StaffAvailabilityRule
        {
            Id = Guid.NewGuid(),
            StaffId = staffId,
            DayOfWeek = body.DayOfWeek,
            StartTimeUtc = startTod.ToTimeSpan(),
            EndTimeUtc = endTod.ToTimeSpan(),
            Active = body.Active
        };

        db.StaffAvailabilityRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListRules), new { staffId }, new StaffAvailabilityRuleDto(
            rule.Id, rule.StaffId, rule.DayOfWeek,
            TimeOnly.FromTimeSpan(rule.StartTimeUtc).ToString("HH:mm"),
            TimeOnly.FromTimeSpan(rule.EndTimeUtc).ToString("HH:mm"),
            rule.Active));
    }

    // PUT /api/v1/availability/staff/{staffId}/rules/{ruleId}
    [HttpPut("staff/{staffId:guid}/rules/{ruleId:guid}")]
    [Authorize(Policy = "Staff, Admin")]
    public async Task<IActionResult> UpdateRule(Guid staffId, Guid ruleId, [FromBody] UpsertStaffAvailabilityRuleDto body, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct))
        {
            var myStaffId = await MyStaffIdAsync(ct);
            if (myStaffId is null || myStaffId.Value != staffId) return Forbid();
        }

        if (body.DayOfWeek < 0 || body.DayOfWeek > 6)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "dayOfWeek deve estar entre 0 e 6.");

        if (!TryParseTime(body.StartTimeUtc, out var startTod) || !TryParseTime(body.EndTimeUtc, out var endTod))
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "StartTimeUtc/EndTimeUtc devem ter formato HH:mm.");

        if (endTod <= startTod)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "EndTimeUtc deve ser maior que StartTimeUtc.");

        var affected = await db.StaffAvailabilityRules
            .Where(r => r.Id == ruleId && r.StaffId == staffId)
            .ExecuteUpdateAsync(up => up
                .SetProperty(r => r.DayOfWeek, body.DayOfWeek)
                .SetProperty(r => r.StartTimeUtc, startTod.ToTimeSpan())
                .SetProperty(r => r.EndTimeUtc, endTod.ToTimeSpan())
                .SetProperty(r => r.Active, body.Active), ct);

        if (affected == 0) return NotFound();
        return NoContent();
    }

    // DELETE /api/v1/availability/staff/{staffId}/rules/{ruleId}
    [HttpDelete("staff/{staffId:guid}/rules/{ruleId:guid}")]
    [Authorize(Policy = "Staff, Admin")]
    public async Task<IActionResult> DeleteRule(Guid staffId, Guid ruleId, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct))
        {
            var myStaffId = await MyStaffIdAsync(ct);
            if (myStaffId is null || myStaffId.Value != staffId) return Forbid();
        }

        var affected = await db.StaffAvailabilityRules
            .Where(r => r.Id == ruleId && r.StaffId == staffId)
            .ExecuteDeleteAsync(ct);

        if (affected == 0) return NotFound();
        return NoContent();
    }

    // ===========================
    // Staff Time Off
    // ===========================

    // GET /api/v1/availability/staff/{staffId}/time-off
    [HttpGet("staff/{staffId:guid}/time-off")]
    public async Task<IActionResult> ListTimeOff(Guid staffId, CancellationToken ct)
    {
        var items = await db.StaffTimeOffs.AsNoTracking()
            .Where(t => t.StaffId == staffId)
            .OrderByDescending(t => t.FromUtc)
            .Select(t => new StaffTimeOffDto(t.Id, t.StaffId, t.FromUtc, t.ToUtc, t.Reason))
            .ToListAsync(ct);

        return Ok(items);
    }

    // POST /api/v1/availability/staff/{staffId}/time-off
    [HttpPost("staff/{staffId:guid}/time-off")]
    [Authorize(Policy = "Staff, Admin")]
    public async Task<IActionResult> CreateTimeOff(Guid staffId, [FromBody] UpsertStaffTimeOffDto body, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct))
        {
            var myStaffId = await MyStaffIdAsync(ct);
            if (myStaffId is null || myStaffId.Value != staffId) return Forbid();
        }

        var from = EnsureUtc(body.FromUtc);
        var to   = EnsureUtc(body.ToUtc);
        if (to <= from)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "ToUtc deve ser posterior a FromUtc.");

        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason!.Trim();

        var t = new StaffTimeOff
        {
            Id = Guid.NewGuid(),
            StaffId = staffId,
            FromUtc = from,
            ToUtc = to,
            Reason = reason
        };

        db.StaffTimeOffs.Add(t);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListTimeOff), new { staffId }, new StaffTimeOffDto(t.Id, t.StaffId, t.FromUtc, t.ToUtc, t.Reason));
    }

    // PUT /api/v1/availability/staff/{staffId}/time-off/{id}
    [HttpPut("staff/{staffId:guid}/time-off/{id:guid}")]
    [Authorize(Policy = "Staff, Admin")]
    public async Task<IActionResult> UpdateTimeOff(Guid staffId, Guid id, [FromBody] UpsertStaffTimeOffDto body, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct))
        {
            var myStaffId = await MyStaffIdAsync(ct);
            if (myStaffId is null || myStaffId.Value != staffId) return Forbid();
        }

        var from = EnsureUtc(body.FromUtc);
        var to   = EnsureUtc(body.ToUtc);
        if (to <= from)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "ToUtc deve ser posterior a FromUtc.");

        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason!.Trim();

        var affected = await db.StaffTimeOffs
            .Where(t => t.Id == id && t.StaffId == staffId)
            .ExecuteUpdateAsync(up => up
                .SetProperty(t => t.FromUtc, from)
                .SetProperty(t => t.ToUtc, to)
                .SetProperty(t => t.Reason, reason), ct);

        if (affected == 0) return NotFound();
        return NoContent();
    }

    // DELETE /api/v1/availability/staff/{staffId}/time-off/{id}
    [HttpDelete("staff/{staffId:guid}/time-off/{id:guid}")]
    [Authorize(Policy = "Staff, Admin")]
    public async Task<IActionResult> DeleteTimeOff(Guid staffId, Guid id, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct))
        {
            var myStaffId = await MyStaffIdAsync(ct);
            if (myStaffId is null || myStaffId.Value != staffId) return Forbid();
        }

        var affected = await db.StaffTimeOffs
            .Where(t => t.Id == id && t.StaffId == staffId)
            .ExecuteDeleteAsync(ct);

        if (affected == 0) return NotFound();
        return NoContent();
    }

    // ===========================
    // Room Closures (ADMIN only)
    // ===========================

    // GET /api/v1/availability/rooms/{roomId}/closures
    [HttpGet("rooms/{roomId:guid}/closures")]
    public async Task<IActionResult> ListClosures(Guid roomId, CancellationToken ct)
    {
        var items = await db.RoomClosures.AsNoTracking()
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.FromUtc)
            .Select(r => new RoomClosureDto(r.Id, r.RoomId, r.FromUtc, r.ToUtc, r.Reason))
            .ToListAsync(ct);

        return Ok(items);
    }

    // POST /api/v1/availability/rooms/{roomId}/closures
    [HttpPost("rooms/{roomId:guid}/closures")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CreateClosure(Guid roomId, [FromBody] UpsertRoomClosureDto body, CancellationToken ct)
    {
        var from = EnsureUtc(body.FromUtc);
        var to   = EnsureUtc(body.ToUtc);
        if (to <= from)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "ToUtc deve ser posterior a FromUtc.");

        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason!.Trim();

        var cl = new RoomClosure
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            FromUtc = from,
            ToUtc = to,
            Reason = reason
        };

        db.RoomClosures.Add(cl);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListClosures), new { roomId }, new RoomClosureDto(cl.Id, cl.RoomId, cl.FromUtc, cl.ToUtc, cl.Reason));
    }

    // PUT /api/v1/availability/rooms/{roomId}/closures/{id}
    [HttpPut("rooms/{roomId:guid}/closures/{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> UpdateClosure(Guid roomId, Guid id, [FromBody] UpsertRoomClosureDto body, CancellationToken ct)
    {
        var from = EnsureUtc(body.FromUtc);
        var to   = EnsureUtc(body.ToUtc);
        if (to <= from)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "ToUtc deve ser posterior a FromUtc.");

        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason!.Trim();

        var affected = await db.RoomClosures
            .Where(r => r.Id == id && r.RoomId == roomId)
            .ExecuteUpdateAsync(up => up
                .SetProperty(r => r.FromUtc, from)
                .SetProperty(r => r.ToUtc, to)
                .SetProperty(r => r.Reason, reason), ct);

        if (affected == 0) return NotFound();
        return NoContent();
    }

    // DELETE /api/v1/availability/rooms/{roomId}/closures/{id}
    [HttpDelete("rooms/{roomId:guid}/closures/{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteClosure(Guid roomId, Guid id, CancellationToken ct)
    {
        var affected = await db.RoomClosures
            .Where(r => r.Id == id && r.RoomId == roomId)
            .ExecuteDeleteAsync(ct);

        if (affected == 0) return NotFound();
        return NoContent();
    }

    // ===========================
    // IsAvailable
    // ===========================

    // POST /api/v1/availability/is-available
    [HttpPost("is-available")]
    public async Task<ActionResult<CheckAvailabilityResponse>> IsAvailable([FromBody] CheckAvailabilityRequest body, CancellationToken ct)
    {
        if (body.DurationMinutes < 15 || body.DurationMinutes > 240)
            return Problem(statusCode: 400, title: "Dados inválidos", detail: "durationMinutes deve estar entre 15 e 240.");

        var start = EnsureUtc(body.StartUtc);
        var end = start.AddMinutes(body.DurationMinutes);

        // —— STAFF: regras + folgas + conflitos
        if (body.StaffId is Guid staffId)
        {
            var dow = (int)start.DayOfWeek;

            if (start.Date != end.Date)
                return Ok(new CheckAvailabilityResponse(false, "Intervalo atravessa a meia-noite — não suportado para regras diárias."));

            var startTod = TimeOnly.FromDateTime(start);
            var endTod   = TimeOnly.FromDateTime(end);
            var startSpan = startTod.ToTimeSpan();
            var endSpan   = endTod.ToTimeSpan();

            var hasAnyRules = await db.StaffAvailabilityRules
                .AnyAsync(r => r.StaffId == staffId && r.Active, ct);

            if (hasAnyRules)
            {
                var fitsAnyRule = await db.StaffAvailabilityRules
                    .Where(r => r.StaffId == staffId && r.Active && r.DayOfWeek == dow)
                    .AnyAsync(r => r.StartTimeUtc <= startSpan && endSpan <= r.EndTimeUtc, ct);

                if (!fitsAnyRule)
                    return Ok(new CheckAvailabilityResponse(false, "Fora do horário de trabalho do staff."));
            }

            var timeOff = await db.StaffTimeOffs
                .Where(t => t.StaffId == staffId)
                .AnyAsync(t => Overlaps(start, end, t.FromUtc, t.ToUtc), ct);

            if (timeOff)
                return Ok(new CheckAvailabilityResponse(false, "Staff em ausência (time-off) nesse período."));

            var conflictClassStaff = await db.Classes
                .Where(c => c.StaffId == staffId && c.Status == ClassStatus.Scheduled)
                .AnyAsync(c =>
                    EF.Functions.DateDiffMinute(c.StartUtc, start) < c.DurationMinutes &&
                    EF.Functions.DateDiffMinute(start, c.StartUtc) < body.DurationMinutes, ct);

            if (conflictClassStaff)
                return Ok(new CheckAvailabilityResponse(false, "Staff já tem aula marcada nesse período."));

            var conflictReqStaff = await db.ClassRequests
                .Where(r => r.StaffId == staffId && r.Status == Domain.ClassRequests.ClassRequestStatus.Pending)
                .AnyAsync(r =>
                    EF.Functions.DateDiffMinute(r.ProposedStartUtc, start) < r.DurationMinutes &&
                    EF.Functions.DateDiffMinute(start, r.ProposedStartUtc) < body.DurationMinutes, ct);

            if (conflictReqStaff)
                return Ok(new CheckAvailabilityResponse(false, "Existe pedido pendente para o staff nesse período."));
        }

        // —— ROOM: encerramentos + conflitos
        if (body.RoomId is Guid roomId)
        {
            var roomClosed = await db.RoomClosures
                .Where(c => c.RoomId == roomId)
                .AnyAsync(c => Overlaps(start, end, c.FromUtc, c.ToUtc), ct);

            if (roomClosed)
                return Ok(new CheckAvailabilityResponse(false, "Sala encerrada/indisponível nesse período."));

            var conflictClassRoom = await db.Classes
                .Where(c => c.RoomId == roomId && c.Status == ClassStatus.Scheduled)
                .AnyAsync(c =>
                    EF.Functions.DateDiffMinute(c.StartUtc, start) < c.DurationMinutes &&
                    EF.Functions.DateDiffMinute(start, c.StartUtc) < body.DurationMinutes, ct);

            if (conflictClassRoom)
                return Ok(new CheckAvailabilityResponse(false, "Sala ocupada por outra aula."));

            var conflictReqRoom = await db.ClassRequests
                .Where(r => r.RoomId == roomId && r.Status == Domain.ClassRequests.ClassRequestStatus.Pending)
                .AnyAsync(r =>
                    EF.Functions.DateDiffMinute(r.ProposedStartUtc, start) < r.DurationMinutes &&
                    EF.Functions.DateDiffMinute(start, r.ProposedStartUtc) < body.DurationMinutes, ct);

            if (conflictReqRoom)
                return Ok(new CheckAvailabilityResponse(false, "Sala reservada por pedido pendente."));
        }

        return Ok(new CheckAvailabilityResponse(true, null));
    }
}
