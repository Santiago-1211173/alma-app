using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Availability;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Contracts.Availability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/availability")]
public sealed class AvailabilityController(AppDbContext db) : ControllerBase
{
    // ===== Timezone Portugal-only (Europe/Lisbon), com fallback no Windows =====
    private static readonly TimeZoneInfo TzLisbon =
        TryGetTimeZone("Europe/Lisbon") ??
        TryGetTimeZone("GMT Standard Time") ?? // Windows
        TimeZoneInfo.Local;

    private static TimeZoneInfo? TryGetTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return null; }
    }

    private static DateTime LocalToUtcPT(DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TzLisbon);

    private static DateTime UtcToLocalPT(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TzLisbon);

    private static TimeSpan ParseHm(string s)
        => TimeSpan.ParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture);

    private static string Hm(TimeSpan t)
        => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        => aStart < bEnd && aEnd > bStart;

    // Bloco genérico de ocupação (aula ou pedido), sempre em UTC
    private sealed class SpanBlock
    {
        public DateTime StartUtc { get; init; }
        public int DurationMinutes { get; init; }
    }


    // ========================================================================
    // 1) Staff Availability Rules (CRUD simples)
    // ========================================================================

    // GET /api/v1/availability/rules?staffId=
    [HttpGet("rules")]
    public async Task<ActionResult<IEnumerable<StaffAvailabilityRuleDto>>> GetRules(
        [FromQuery] Guid staffId, CancellationToken ct)
    {
        if (staffId == Guid.Empty) return BadRequest(new { detail = "staffId é obrigatório." });

        var items = await db.StaffAvailabilityRules
            .AsNoTracking()
            .Where(r => r.StaffId == staffId)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartTimeUtc)
            .Select(r => new StaffAvailabilityRuleDto(
                r.Id, r.StaffId, r.DayOfWeek, Hm(r.StartTimeUtc), Hm(r.EndTimeUtc), r.Active))
            .ToListAsync(ct);

        return Ok(items);
    }

    // POST /api/v1/availability/rules/{staffId}
    [HttpPost("rules/{staffId:guid}")]
    public async Task<IActionResult> CreateRule(Guid staffId,
        [FromBody] UpsertStaffAvailabilityRuleDto body, CancellationToken ct)
    {
        if (staffId == Guid.Empty) return BadRequest(new { detail = "staffId inválido." });
        if (body is null) return BadRequest();

        var start = ParseHm(body.StartTimeUtc);
        var end   = ParseHm(body.EndTimeUtc);
        if (end <= start) return BadRequest(new { detail = "EndTimeUtc deve ser > StartTimeUtc." });
        if (body.DayOfWeek is < 0 or > 6) return BadRequest(new { detail = "dayOfWeek deve estar entre 0..6." });

        var entity = new StaffAvailabilityRule
        {
            StaffId      = staffId,
            DayOfWeek    = body.DayOfWeek,
            StartTimeUtc = start,
            EndTimeUtc   = end,
            Active       = body.Active
        };

        db.StaffAvailabilityRules.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = new StaffAvailabilityRuleDto(entity.Id, entity.StaffId, entity.DayOfWeek, Hm(entity.StartTimeUtc), Hm(entity.EndTimeUtc), entity.Active);
        return CreatedAtAction(nameof(GetRules), new { staffId }, dto);
    }

    // PUT /api/v1/availability/rules/{id}
    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id,
        [FromBody] UpsertStaffAvailabilityRuleDto body, CancellationToken ct)
    {
        if (body is null) return BadRequest();

        var existing = await db.StaffAvailabilityRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null) return NotFound();

        var start = ParseHm(body.StartTimeUtc);
        var end   = ParseHm(body.EndTimeUtc);
        if (end <= start) return BadRequest(new { detail = "EndTimeUtc deve ser > StartTimeUtc." });
        if (body.DayOfWeek is < 0 or > 6) return BadRequest(new { detail = "dayOfWeek deve estar entre 0..6." });

        // Respeitar propriedades init: recriar a entidade (mesmo Id) e substituir
        var updated = new StaffAvailabilityRule
        {
            Id           = existing.Id,
            StaffId      = existing.StaffId,
            DayOfWeek    = body.DayOfWeek,
            StartTimeUtc = start,
            EndTimeUtc   = end,
            Active       = body.Active
        };

        db.StaffAvailabilityRules.Remove(existing);
        db.StaffAvailabilityRules.Add(updated);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // DELETE /api/v1/availability/rules/{id}
    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var existing = await db.StaffAvailabilityRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null) return NotFound();

        db.StaffAvailabilityRules.Remove(existing);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ========================================================================
    // 2) Staff Time Off (CRUD simples)  — **DTOs já são em UTC**
    // ========================================================================

    // GET /api/v1/availability/time-off?staffId=
    [HttpGet("time-off")]
    public async Task<ActionResult<IEnumerable<StaffTimeOffDto>>> GetTimeOff(
        [FromQuery] Guid staffId, CancellationToken ct)
    {
        if (staffId == Guid.Empty) return BadRequest(new { detail = "staffId é obrigatório." });

        var items = await db.StaffTimeOffs
            .AsNoTracking()
            .Where(t => t.StaffId == staffId)
            .OrderByDescending(t => t.FromUtc)
            .Select(t => new StaffTimeOffDto(t.Id, t.StaffId, t.FromUtc, t.ToUtc, t.Reason))
            .ToListAsync(ct);

        return Ok(items);
    }

    // POST /api/v1/availability/time-off/{staffId}
    [HttpPost("time-off/{staffId:guid}")]
    public async Task<IActionResult> CreateTimeOff(Guid staffId,
        [FromBody] UpsertStaffTimeOffDto body, CancellationToken ct)
    {
        if (staffId == Guid.Empty) return BadRequest(new { detail = "staffId inválido." });
        if (body is null) return BadRequest();

        var fromUtc = DateTime.SpecifyKind(body.FromUtc, DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(body.ToUtc,   DateTimeKind.Utc);
        if (toUtc <= fromUtc) return BadRequest(new { detail = "ToUtc deve ser > FromUtc." });

        var entity = new StaffTimeOff
        {
            StaffId = staffId,
            FromUtc = fromUtc,
            ToUtc   = toUtc,
            Reason  = body.Reason
        };

        db.StaffTimeOffs.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = new StaffTimeOffDto(entity.Id, entity.StaffId, entity.FromUtc, entity.ToUtc, entity.Reason);
        return CreatedAtAction(nameof(GetTimeOff), new { staffId }, dto);
    }

    // PUT /api/v1/availability/time-off/{id}
    [HttpPut("time-off/{id:guid}")]
    public async Task<IActionResult> UpdateTimeOff(Guid id,
        [FromBody] UpsertStaffTimeOffDto body, CancellationToken ct)
    {
        var existing = await db.StaffTimeOffs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null) return NotFound();

        var fromUtc = DateTime.SpecifyKind(body.FromUtc, DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(body.ToUtc,   DateTimeKind.Utc);
        if (toUtc <= fromUtc) return BadRequest(new { detail = "ToUtc deve ser > FromUtc." });

        var updated = new StaffTimeOff
        {
            Id      = existing.Id,
            StaffId = existing.StaffId,
            FromUtc = fromUtc,
            ToUtc   = toUtc,
            Reason  = body.Reason
        };

        db.StaffTimeOffs.Remove(existing);
        db.StaffTimeOffs.Add(updated);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/v1/availability/time-off/{id}
    [HttpDelete("time-off/{id:guid}")]
    public async Task<IActionResult> DeleteTimeOff(Guid id, CancellationToken ct)
    {
        var existing = await db.StaffTimeOffs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null) return NotFound();

        db.StaffTimeOffs.Remove(existing);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ========================================================================
    // 3) Room Closures (CRUD simples) — **DTOs já são em UTC**
    // ========================================================================

    // GET /api/v1/availability/room-closures?roomId=
    [HttpGet("room-closures")]
    public async Task<ActionResult<IEnumerable<RoomClosureDto>>> GetRoomClosures(
        [FromQuery] Guid roomId, CancellationToken ct)
    {
        if (roomId == Guid.Empty) return BadRequest(new { detail = "roomId é obrigatório." });

        var items = await db.RoomClosures
            .AsNoTracking()
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.FromUtc)
            .Select(r => new RoomClosureDto(r.Id, r.RoomId, r.FromUtc, r.ToUtc, r.Reason))
            .ToListAsync(ct);

        return Ok(items);
    }

    // POST /api/v1/availability/room-closures/{roomId}
    [HttpPost("room-closures/{roomId:guid}")]
    public async Task<IActionResult> CreateRoomClosure(Guid roomId,
        [FromBody] UpsertRoomClosureDto body, CancellationToken ct)
    {
        if (roomId == Guid.Empty) return BadRequest(new { detail = "roomId inválido." });
        if (body is null) return BadRequest();

        var fromUtc = DateTime.SpecifyKind(body.FromUtc, DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(body.ToUtc,   DateTimeKind.Utc);
        if (toUtc <= fromUtc) return BadRequest(new { detail = "ToUtc deve ser > FromUtc." });

        var entity = new RoomClosure
        {
            RoomId  = roomId,
            FromUtc = fromUtc,
            ToUtc   = toUtc,
            Reason  = body.Reason
        };

        db.RoomClosures.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = new RoomClosureDto(entity.Id, entity.RoomId, entity.FromUtc, entity.ToUtc, entity.Reason);
        return CreatedAtAction(nameof(GetRoomClosures), new { roomId }, dto);
    }

    // PUT /api/v1/availability/room-closures/{id}
    [HttpPut("room-closures/{id:guid}")]
    public async Task<IActionResult> UpdateRoomClosure(Guid id,
        [FromBody] UpsertRoomClosureDto body, CancellationToken ct)
    {
        var existing = await db.RoomClosures.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null) return NotFound();

        var fromUtc = DateTime.SpecifyKind(body.FromUtc, DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(body.ToUtc,   DateTimeKind.Utc);
        if (toUtc <= fromUtc) return BadRequest(new { detail = "ToUtc deve ser > FromUtc." });

        var updated = new RoomClosure
        {
            Id      = existing.Id,
            RoomId  = existing.RoomId,
            FromUtc = fromUtc,
            ToUtc   = toUtc,
            Reason  = body.Reason
        };

        db.RoomClosures.Remove(existing);
        db.RoomClosures.Add(updated);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/v1/availability/room-closures/{id}
    [HttpDelete("room-closures/{id:guid}")]
    public async Task<IActionResult> DeleteRoomClosure(Guid id, CancellationToken ct)
    {
        var existing = await db.RoomClosures.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null) return NotFound();

        db.RoomClosures.Remove(existing);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ========================================================================
    // 4) Check Disponibilidade (Portugal local -> UTC)
    // ========================================================================

    // POST /api/v1/availability/is-available
    [HttpPost("is-available")]
    public async Task<ActionResult<CheckAvailabilityResponse>> IsAvailable(
        [FromBody] CheckAvailabilityRequest body, CancellationToken ct)
    {
        if (body is null) return BadRequest();
        if (body.DurationMinutes <= 0) return BadRequest(new { detail = "durationMinutes inválido." });

        var staffId = body.StaffId;
        var roomId  = body.RoomId;

        var startUtc = LocalToUtcPT(body.StartLocal);
        var endUtc   = startUtc.AddMinutes(body.DurationMinutes);

        // 1) Regra semanal do staff (em UTC, dia/horas em UTC)
        var rules = await db.StaffAvailabilityRules
            .AsNoTracking()
            .Where(r => r.StaffId == staffId && r.Active && r.DayOfWeek == (int)startUtc.DayOfWeek)
            .ToListAsync(ct);

        var t = startUtc.TimeOfDay;
        var dur = TimeSpan.FromMinutes(body.DurationMinutes);
        var insideRule = rules.Any(r => r.StartTimeUtc <= t && t + dur <= r.EndTimeUtc);

        if (!insideRule)
            return Ok(new CheckAvailabilityResponse(false, "Fora das regras de disponibilidade do staff para esse dia."));

        // 2) Folgas do staff
        var hasTimeOff = await db.StaffTimeOffs
            .AsNoTracking()
            .Where(s => s.StaffId == staffId &&
                        s.FromUtc < endUtc &&
                        s.ToUtc   > startUtc)
            .AnyAsync(ct);

        if (hasTimeOff)
            return Ok(new CheckAvailabilityResponse(false, "O staff está em folga/ausência nesse horário."));

        // 3) Encerramentos da sala (se fornecida)
        if (roomId is Guid rid)
        {
            var roomClosed = await db.RoomClosures
                .AsNoTracking()
                .Where(rc => rc.RoomId == rid &&
                             rc.FromUtc < endUtc &&
                             rc.ToUtc   > startUtc)
                .AnyAsync(ct);

            if (roomClosed)
                return Ok(new CheckAvailabilityResponse(false, "A sala está indisponível (encerramento) nesse horário."));
        }

        // 4) Conflitos com Aulas (staff)
        var staffClassConflict = await db.Classes
            .AsNoTracking()
            .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < body.DurationMinutes, ct);

        if (staffClassConflict)
            return Ok(new CheckAvailabilityResponse(false, "Conflito com outra aula do staff."));

        // 5) Conflitos com Pedidos Pendentes (staff)
        var staffReqConflict = await db.ClassRequests
            .AsNoTracking()
            .Where(c => c.StaffId == staffId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, startUtc) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, c.ProposedStartUtc) < body.DurationMinutes, ct);

        if (staffReqConflict)
            return Ok(new CheckAvailabilityResponse(false, "Conflito com um pedido pendente do staff."));

        // 6) Conflitos por SALA (se fornecida) — considerar qualquer staff
        if (roomId is Guid roomCheckId)
        {
            var roomClassConflict = await db.Classes
                .AsNoTracking()
                .Where(k => k.RoomId == roomCheckId && k.Status == ClassStatus.Scheduled)
                .AnyAsync(k =>
                    EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                    EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < body.DurationMinutes, ct);

            if (roomClassConflict)
                return Ok(new CheckAvailabilityResponse(false, "Conflito: a sala já tem uma aula nesse horário."));

            var roomReqConflict = await db.ClassRequests
                .AsNoTracking()
                .Where(c => c.RoomId == roomCheckId && c.Status == ClassRequestStatus.Pending)
                .AnyAsync(c =>
                    EF.Functions.DateDiffMinute(c.ProposedStartUtc, startUtc) < c.DurationMinutes &&
                    EF.Functions.DateDiffMinute(startUtc, c.ProposedStartUtc) < body.DurationMinutes, ct);

            if (roomReqConflict)
                return Ok(new CheckAvailabilityResponse(false, "Conflito: já existe um pedido pendente para a sala nesse horário."));
        }

        return Ok(new CheckAvailabilityResponse(true, null));
    }

    // ========================================================================
    // 5) Find Slots (Portugal local -> lista de janelas locais)
    // ========================================================================

    // POST /api/v1/availability/find-slots
    [HttpPost("find-slots")]
    public async Task<ActionResult<IEnumerable<SlotDto>>> FindSlots(
        [FromBody] FindSlotsRequest body, CancellationToken ct)
    {
        if (body is null) return BadRequest();
        if (body.DurationMinutes <= 0) return BadRequest(new { detail = "durationMinutes inválido." });
        if (body.SlotMinutes <= 0) return BadRequest(new { detail = "slotMinutes inválido." });

        var staffId = body.StaffId;
        var roomId  = body.RoomId;

        var fromUtc = LocalToUtcPT(body.FromLocal);
        var toUtc   = LocalToUtcPT(body.ToLocal);
        if (toUtc <= fromUtc) return BadRequest(new { detail = "Intervalo inválido (to <= from)." });

        var dur = TimeSpan.FromMinutes(body.DurationMinutes);
        var step = TimeSpan.FromMinutes(body.SlotMinutes);

        // Pré-carregar tudo para o intervalo (para não fazer N queries)
        var rules = await db.StaffAvailabilityRules
            .AsNoTracking()
            .Where(r => r.StaffId == staffId && r.Active)
            .ToListAsync(ct);

        var offs = await db.StaffTimeOffs
            .AsNoTracking()
            .Where(s => s.StaffId == staffId && s.FromUtc < toUtc && s.ToUtc > fromUtc)
            .ToListAsync(ct);

        var roomClosures = roomId is Guid rid
            ? await db.RoomClosures.AsNoTracking()
                .Where(rc => rc.RoomId == rid && rc.FromUtc < toUtc && rc.ToUtc > fromUtc)
                .ToListAsync(ct)
            : new List<RoomClosure>();

        // Aulas do staff (qualquer sala)
var staffClasses = await db.Classes
    .AsNoTracking()
    .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled &&
                k.StartUtc < toUtc && k.StartUtc.AddMinutes(k.DurationMinutes) > fromUtc)
    .Select(k => new SpanBlock { StartUtc = k.StartUtc, DurationMinutes = k.DurationMinutes })
    .ToListAsync(ct);

// Pedidos pendentes do staff
var staffRequests = await db.ClassRequests
    .AsNoTracking()
    .Where(c => c.StaffId == staffId && c.Status == ClassRequestStatus.Pending &&
                c.ProposedStartUtc < toUtc && c.ProposedStartUtc.AddMinutes(c.DurationMinutes) > fromUtc)
    .Select(c => new SpanBlock { StartUtc = c.ProposedStartUtc, DurationMinutes = c.DurationMinutes })
    .ToListAsync(ct);

// Conflitos por sala (aulas)
var roomClasses = roomId is Guid rid2
    ? await db.Classes.AsNoTracking()
        .Where(k => k.RoomId == rid2 && k.Status == ClassStatus.Scheduled &&
                    k.StartUtc < toUtc && k.StartUtc.AddMinutes(k.DurationMinutes) > fromUtc)
        .Select(k => new SpanBlock { StartUtc = k.StartUtc, DurationMinutes = k.DurationMinutes })
        .ToListAsync(ct)
    : new List<SpanBlock>();

// Conflitos por sala (pedidos)
var roomRequests = roomId is Guid rid3
    ? await db.ClassRequests.AsNoTracking()
        .Where(c => c.RoomId == rid3 && c.Status == ClassRequestStatus.Pending &&
                    c.ProposedStartUtc < toUtc && c.ProposedStartUtc.AddMinutes(c.DurationMinutes) > fromUtc)
        .Select(c => new SpanBlock { StartUtc = c.ProposedStartUtc, DurationMinutes = c.DurationMinutes })
        .ToListAsync(ct)
    : new List<SpanBlock>();

        var result = new List<SlotDto>();

        // Iterar em HORA LOCAL (como pediste). Para cada passo, validar tudo em UTC.
        for (var cursorLocal = body.FromLocal; cursorLocal.Add(dur) <= body.ToLocal; cursorLocal = cursorLocal.Add(step))
        {
            var sUtc = LocalToUtcPT(cursorLocal);
            var eUtc = sUtc.Add(dur);

            // 1) Regra semanal (UTC)
            var dayRules = rules.Where(r => r.DayOfWeek == (int)sUtc.DayOfWeek).ToList();
            if (dayRules.Count == 0) continue;

            var t = sUtc.TimeOfDay;
            var insideRule = dayRules.Any(r => r.StartTimeUtc <= t && t + dur <= r.EndTimeUtc);
            if (!insideRule) continue;

            // 2) Folgas
            if (offs.Any(o => Overlaps(sUtc, eUtc, o.FromUtc, o.ToUtc))) continue;

            // 3) Aulas/Pedidos do staff
            // Staff: aulas
            if (staffClasses.Any(b =>
                Overlaps(sUtc, eUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes)))) continue;

            // Staff: pedidos pendentes
            if (staffRequests.Any(b =>
                Overlaps(sUtc, eUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes)))) continue;

            // Sala: aulas
            if (roomClasses.Any(b =>
                Overlaps(sUtc, eUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes)))) continue;

            // Sala: pedidos pendentes
            if (roomRequests.Any(b =>
                Overlaps(sUtc, eUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes)))) continue;


            // Slot válido — devolver SEM 'Z', em local time
            var endLocal = cursorLocal.Add(dur);
            result.Add(new SlotDto(cursorLocal, endLocal));
        }

        return Ok(result);
    }
}
