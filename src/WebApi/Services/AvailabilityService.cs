using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Availability;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Availability;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly AppDbContext _db;

    private static readonly TimeZoneInfo TzLisbon =
        TryGetTimeZone("Europe/Lisbon") ??
        TryGetTimeZone("GMT Standard Time") ??
        TimeZoneInfo.Local;

    public AvailabilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<IEnumerable<StaffAvailabilityRuleDto>>> GetRulesAsync(Guid staffId, CancellationToken ct)
    {
        if (staffId == Guid.Empty)
        {
            return ServiceResult<IEnumerable<StaffAvailabilityRuleDto>>.Fail(new ServiceError(400, "staffId é obrigatório."));
        }

        var items = await _db.StaffAvailabilityRules
            .AsNoTracking()
            .Where(r => r.StaffId == staffId)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartTimeUtc)
            .Select(r => new StaffAvailabilityRuleDto(
                r.Id,
                r.StaffId,
                r.DayOfWeek,
                FormatHm(r.StartTimeUtc),
                FormatHm(r.EndTimeUtc),
                r.Active))
            .ToListAsync(ct);

        return ServiceResult<IEnumerable<StaffAvailabilityRuleDto>>.Ok(items);
    }

    public async Task<ServiceResult<StaffAvailabilityRuleDto>> CreateRuleAsync(Guid staffId, UpsertStaffAvailabilityRuleDto request, CancellationToken ct)
    {
        if (staffId == Guid.Empty)
        {
            return ServiceResult<StaffAvailabilityRuleDto>.Fail(new ServiceError(400, "staffId inválido."));
        }

        var (start, end, error) = ValidateRuleRequest(request);
        if (error is not null)
        {
            return ServiceResult<StaffAvailabilityRuleDto>.Fail(error);
        }

        var entity = new StaffAvailabilityRule
        {
            StaffId = staffId,
            DayOfWeek = request.DayOfWeek,
            StartTimeUtc = start,
            EndTimeUtc = end,
            Active = request.Active
        };

        await _db.StaffAvailabilityRules.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        var dto = new StaffAvailabilityRuleDto(entity.Id, entity.StaffId, entity.DayOfWeek, FormatHm(entity.StartTimeUtc), FormatHm(entity.EndTimeUtc), entity.Active);
        return ServiceResult<StaffAvailabilityRuleDto>.Ok(dto);
    }

    public async Task<ServiceResult> UpdateRuleAsync(Guid id, UpsertStaffAvailabilityRuleDto request, CancellationToken ct)
    {
        var existing = await _db.StaffAvailabilityRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Regra não encontrada."));
        }

        var (start, end, error) = ValidateRuleRequest(request);
        if (error is not null)
        {
            return ServiceResult.Fail(error);
        }

        var updated = new StaffAvailabilityRule
        {
            Id = existing.Id,
            StaffId = existing.StaffId,
            DayOfWeek = request.DayOfWeek,
            StartTimeUtc = start,
            EndTimeUtc = end,
            Active = request.Active
        };

        _db.StaffAvailabilityRules.Remove(existing);
        await _db.StaffAvailabilityRules.AddAsync(updated, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteRuleAsync(Guid id, CancellationToken ct)
    {
        var existing = await _db.StaffAvailabilityRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Regra não encontrada."));
        }

        _db.StaffAvailabilityRules.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IEnumerable<StaffTimeOffDto>>> GetTimeOffAsync(Guid staffId, CancellationToken ct)
    {
        if (staffId == Guid.Empty)
        {
            return ServiceResult<IEnumerable<StaffTimeOffDto>>.Fail(new ServiceError(400, "staffId é obrigatório."));
        }

        var items = await _db.StaffTimeOffs
            .AsNoTracking()
            .Where(t => t.StaffId == staffId)
            .OrderByDescending(t => t.FromUtc)
            .Select(t => new StaffTimeOffDto(t.Id, t.StaffId, t.FromUtc, t.ToUtc, t.Reason))
            .ToListAsync(ct);

        return ServiceResult<IEnumerable<StaffTimeOffDto>>.Ok(items);
    }

    public async Task<ServiceResult<StaffTimeOffDto>> CreateTimeOffAsync(Guid staffId, UpsertStaffTimeOffDto request, CancellationToken ct)
    {
        if (staffId == Guid.Empty)
        {
            return ServiceResult<StaffTimeOffDto>.Fail(new ServiceError(400, "staffId inválido."));
        }

        var (fromUtc, toUtc, error) = NormalizeRange(request.FromUtc, request.ToUtc);
        if (error is not null)
        {
            return ServiceResult<StaffTimeOffDto>.Fail(error);
        }

        var entity = new StaffTimeOff
        {
            StaffId = staffId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Reason = request.Reason
        };

        await _db.StaffTimeOffs.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        var dto = new StaffTimeOffDto(entity.Id, entity.StaffId, entity.FromUtc, entity.ToUtc, entity.Reason);
        return ServiceResult<StaffTimeOffDto>.Ok(dto);
    }

    public async Task<ServiceResult> UpdateTimeOffAsync(Guid id, UpsertStaffTimeOffDto request, CancellationToken ct)
    {
        var existing = await _db.StaffTimeOffs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Registo de folga não encontrado."));
        }

        var (fromUtc, toUtc, error) = NormalizeRange(request.FromUtc, request.ToUtc);
        if (error is not null)
        {
            return ServiceResult.Fail(error);
        }

        var updated = new StaffTimeOff
        {
            Id = existing.Id,
            StaffId = existing.StaffId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Reason = request.Reason
        };

        _db.StaffTimeOffs.Remove(existing);
        await _db.StaffTimeOffs.AddAsync(updated, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteTimeOffAsync(Guid id, CancellationToken ct)
    {
        var existing = await _db.StaffTimeOffs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Registo de folga não encontrado."));
        }

        _db.StaffTimeOffs.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IEnumerable<RoomClosureDto>>> GetRoomClosuresAsync(Guid roomId, CancellationToken ct)
    {
        if (roomId == Guid.Empty)
        {
            return ServiceResult<IEnumerable<RoomClosureDto>>.Fail(new ServiceError(400, "roomId é obrigatório."));
        }

        var items = await _db.RoomClosures
            .AsNoTracking()
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.FromUtc)
            .Select(r => new RoomClosureDto(r.Id, r.RoomId, r.FromUtc, r.ToUtc, r.Reason))
            .ToListAsync(ct);

        return ServiceResult<IEnumerable<RoomClosureDto>>.Ok(items);
    }

    public async Task<ServiceResult<RoomClosureDto>> CreateRoomClosureAsync(Guid roomId, UpsertRoomClosureDto request, CancellationToken ct)
    {
        if (roomId == Guid.Empty)
        {
            return ServiceResult<RoomClosureDto>.Fail(new ServiceError(400, "roomId inválido."));
        }

        var (fromUtc, toUtc, error) = NormalizeRange(request.FromUtc, request.ToUtc);
        if (error is not null)
        {
            return ServiceResult<RoomClosureDto>.Fail(error);
        }

        var entity = new RoomClosure
        {
            RoomId = roomId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Reason = request.Reason
        };

        await _db.RoomClosures.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        var dto = new RoomClosureDto(entity.Id, entity.RoomId, entity.FromUtc, entity.ToUtc, entity.Reason);
        return ServiceResult<RoomClosureDto>.Ok(dto);
    }

    public async Task<ServiceResult> UpdateRoomClosureAsync(Guid id, UpsertRoomClosureDto request, CancellationToken ct)
    {
        var existing = await _db.RoomClosures.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Encerramento de sala não encontrado."));
        }

        var (fromUtc, toUtc, error) = NormalizeRange(request.FromUtc, request.ToUtc);
        if (error is not null)
        {
            return ServiceResult.Fail(error);
        }

        var updated = new RoomClosure
        {
            Id = existing.Id,
            RoomId = existing.RoomId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Reason = request.Reason
        };

        _db.RoomClosures.Remove(existing);
        await _db.RoomClosures.AddAsync(updated, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteRoomClosureAsync(Guid id, CancellationToken ct)
    {
        var existing = await _db.RoomClosures.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Encerramento de sala não encontrado."));
        }

        _db.RoomClosures.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<CheckAvailabilityResponse>> CheckAvailabilityAsync(CheckAvailabilityRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return ServiceResult<CheckAvailabilityResponse>.Fail(new ServiceError(400, "Pedido inválido."));
        }

        if (request.DurationMinutes <= 0)
        {
            return ServiceResult<CheckAvailabilityResponse>.Fail(new ServiceError(400, "durationMinutes inválido."));
        }

        var staffId = request.StaffId;
        var roomId = request.RoomId;

        var startUtc = LocalToUtc(request.StartLocal);
        var endUtc = startUtc.AddMinutes(request.DurationMinutes);
        var duration = request.DurationMinutes;

        var rules = await _db.StaffAvailabilityRules
            .AsNoTracking()
            .Where(r => r.StaffId == staffId && r.Active && r.DayOfWeek == (int)startUtc.DayOfWeek)
            .ToListAsync(ct);

        var t = startUtc.TimeOfDay;
        var dur = TimeSpan.FromMinutes(duration);
        var insideRule = rules.Any(r => r.StartTimeUtc <= t && t + dur <= r.EndTimeUtc);

        if (!insideRule)
        {
            return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(false, "Fora das regras de disponibilidade do staff para esse dia."));
        }

        var hasTimeOff = await _db.StaffTimeOffs
            .AsNoTracking()
            .Where(s => s.StaffId == staffId && s.FromUtc < endUtc && s.ToUtc > startUtc)
            .AnyAsync(ct);

        if (hasTimeOff)
        {
            return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(false, "O staff está em folga/ausência nesse horário."));
        }

        if (roomId is Guid rid)
        {
            var roomClosed = await _db.RoomClosures
                .AsNoTracking()
                .Where(rc => rc.RoomId == rid && rc.FromUtc < endUtc && rc.ToUtc > startUtc)
                .AnyAsync(ct);

            if (roomClosed)
            {
                return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(false, "A sala está indisponível (encerramento) nesse horário."));
            }
        }

        var staffClassConflict = await _db.Classes
            .AsNoTracking()
            .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < duration, ct);

        if (staffClassConflict)
        {
            return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(false, "Conflito com outra aula do staff."));
        }

        var staffReqConflict = await _db.ClassRequests
            .AsNoTracking()
            .Where(c => c.StaffId == staffId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, startUtc) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, c.ProposedStartUtc) < duration, ct);

        if (staffReqConflict)
        {
            return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(false, "Conflito com um pedido pendente do staff."));
        }

        if (roomId is Guid roomCheckId)
        {
            var roomClassConflict = await _db.Classes
                .AsNoTracking()
                .Where(k => k.RoomId == roomCheckId && k.Status == ClassStatus.Scheduled)
                .AnyAsync(k =>
                    EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                    EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < duration, ct);

            if (roomClassConflict)
            {
                return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(false, "Conflito: a sala já tem uma aula nesse horário."));
            }

            var roomReqConflict = await _db.ClassRequests
                .AsNoTracking()
                .Where(c => c.RoomId == roomCheckId && c.Status == ClassRequestStatus.Pending)
                .AnyAsync(c =>
                    EF.Functions.DateDiffMinute(c.ProposedStartUtc, startUtc) < c.DurationMinutes &&
                    EF.Functions.DateDiffMinute(startUtc, c.ProposedStartUtc) < duration, ct);

            if (roomReqConflict)
            {
                return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(false, "Conflito: já existe um pedido pendente para a sala nesse horário."));
            }
        }

        return ServiceResult<CheckAvailabilityResponse>.Ok(new CheckAvailabilityResponse(true, null));
    }

    public async Task<ServiceResult<IEnumerable<SlotDto>>> FindSlotsAsync(FindSlotsRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return ServiceResult<IEnumerable<SlotDto>>.Fail(new ServiceError(400, "Pedido inválido."));
        }

        if (request.DurationMinutes <= 0)
        {
            return ServiceResult<IEnumerable<SlotDto>>.Fail(new ServiceError(400, "durationMinutes inválido."));
        }

        if (request.SlotMinutes <= 0)
        {
            return ServiceResult<IEnumerable<SlotDto>>.Fail(new ServiceError(400, "slotMinutes inválido."));
        }

        var fromUtc = LocalToUtc(request.FromLocal);
        var toUtc = LocalToUtc(request.ToLocal);
        if (toUtc <= fromUtc)
        {
            return ServiceResult<IEnumerable<SlotDto>>.Fail(new ServiceError(400, "Intervalo inválido (to <= from)."));
        }

        var staffId = request.StaffId;
        var roomId = request.RoomId;
        var dur = TimeSpan.FromMinutes(request.DurationMinutes);
        var step = TimeSpan.FromMinutes(request.SlotMinutes);

        var rules = await _db.StaffAvailabilityRules
            .AsNoTracking()
            .Where(r => r.StaffId == staffId && r.Active)
            .ToListAsync(ct);

        var offs = await _db.StaffTimeOffs
            .AsNoTracking()
            .Where(s => s.StaffId == staffId && s.FromUtc < toUtc && s.ToUtc > fromUtc)
            .ToListAsync(ct);

        var roomClosures = roomId is Guid rid
            ? await _db.RoomClosures.AsNoTracking()
                .Where(rc => rc.RoomId == rid && rc.FromUtc < toUtc && rc.ToUtc > fromUtc)
                .ToListAsync(ct)
            : new List<RoomClosure>();

        var staffClasses = await _db.Classes
            .AsNoTracking()
            .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled &&
                        k.StartUtc < toUtc && k.StartUtc.AddMinutes(k.DurationMinutes) > fromUtc)
            .Select(k => new SpanBlock { StartUtc = k.StartUtc, DurationMinutes = k.DurationMinutes })
            .ToListAsync(ct);

        var staffRequests = await _db.ClassRequests
            .AsNoTracking()
            .Where(c => c.StaffId == staffId && c.Status == ClassRequestStatus.Pending &&
                        c.ProposedStartUtc < toUtc && c.ProposedStartUtc.AddMinutes(c.DurationMinutes) > fromUtc)
            .Select(c => new SpanBlock { StartUtc = c.ProposedStartUtc, DurationMinutes = c.DurationMinutes })
            .ToListAsync(ct);

        var roomClasses = roomId is Guid rid2
            ? await _db.Classes.AsNoTracking()
                .Where(k => k.RoomId == rid2 && k.Status == ClassStatus.Scheduled &&
                            k.StartUtc < toUtc && k.StartUtc.AddMinutes(k.DurationMinutes) > fromUtc)
                .Select(k => new SpanBlock { StartUtc = k.StartUtc, DurationMinutes = k.DurationMinutes })
                .ToListAsync(ct)
            : new List<SpanBlock>();

        var roomRequests = roomId is Guid rid3
            ? await _db.ClassRequests.AsNoTracking()
                .Where(c => c.RoomId == rid3 && c.Status == ClassRequestStatus.Pending &&
                            c.ProposedStartUtc < toUtc && c.ProposedStartUtc.AddMinutes(c.DurationMinutes) > fromUtc)
                .Select(c => new SpanBlock { StartUtc = c.ProposedStartUtc, DurationMinutes = c.DurationMinutes })
                .ToListAsync(ct)
            : new List<SpanBlock>();

        var result = new List<SlotDto>();

        for (var cursorLocal = request.FromLocal; cursorLocal.Add(dur) <= request.ToLocal; cursorLocal = cursorLocal.Add(step))
        {
            var startUtc = LocalToUtc(cursorLocal);
            var endUtc = startUtc.Add(dur);

            var dayRules = rules.Where(r => r.DayOfWeek == (int)startUtc.DayOfWeek).ToList();
            if (dayRules.Count == 0)
            {
                continue;
            }

            var t = startUtc.TimeOfDay;
            var insideRule = dayRules.Any(r => r.StartTimeUtc <= t && t + dur <= r.EndTimeUtc);
            if (!insideRule)
            {
                continue;
            }

            if (offs.Any(o => Overlaps(startUtc, endUtc, o.FromUtc, o.ToUtc)))
            {
                continue;
            }

            if (staffClasses.Any(b => Overlaps(startUtc, endUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes))))
            {
                continue;
            }

            if (staffRequests.Any(b => Overlaps(startUtc, endUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes))))
            {
                continue;
            }

            if (roomClosures.Any(rc => Overlaps(startUtc, endUtc, rc.FromUtc, rc.ToUtc)))
            {
                continue;
            }

            if (roomClasses.Any(b => Overlaps(startUtc, endUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes))))
            {
                continue;
            }

            if (roomRequests.Any(b => Overlaps(startUtc, endUtc, b.StartUtc, b.StartUtc.AddMinutes(b.DurationMinutes))))
            {
                continue;
            }

            var endLocal = cursorLocal.Add(dur);
            result.Add(new SlotDto(cursorLocal, endLocal));
        }

        return ServiceResult<IEnumerable<SlotDto>>.Ok(result);
    }

    private static (TimeSpan start, TimeSpan end, ServiceError? error) ValidateRuleRequest(UpsertStaffAvailabilityRuleDto request)
    {
        if (request.DayOfWeek is < 0 or > 6)
        {
            return (default, default, new ServiceError(400, "dayOfWeek deve estar entre 0..6."));
        }

        try
        {
            var start = TimeSpan.ParseExact(request.StartTimeUtc, @"hh\:mm", CultureInfo.InvariantCulture);
            var end = TimeSpan.ParseExact(request.EndTimeUtc, @"hh\:mm", CultureInfo.InvariantCulture);
            if (end <= start)
            {
                return (default, default, new ServiceError(400, "EndTimeUtc deve ser > StartTimeUtc."));
            }

            return (start, end, null);
        }
        catch (FormatException)
        {
            return (default, default, new ServiceError(400, "Formato de hora inválido."));
        }
    }

    private static (DateTime fromUtc, DateTime toUtc, ServiceError? error) NormalizeRange(DateTime from, DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);
        if (toUtc <= fromUtc)
        {
            return (default, default, new ServiceError(400, "ToUtc deve ser > FromUtc."));
        }

        return (fromUtc, toUtc, null);
    }

    private static string FormatHm(TimeSpan t)
        => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        => aStart < bEnd && aEnd > bStart;

    private static DateTime LocalToUtc(DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TzLisbon);

    private static TimeZoneInfo? TryGetTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return null;
        }
    }

    private sealed class SpanBlock
    {
        public DateTime StartUtc { get; init; }
        public int DurationMinutes { get; init; }
    }
}
