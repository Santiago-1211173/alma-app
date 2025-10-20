using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
 using AlmaApp.Domain.Activities;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Activities;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services
{
    public sealed class ActivitiesService : IActivitiesService
    {
        private readonly AppDbContext _db;
        private readonly IScheduleConflictService _conflict;

        public ActivitiesService(AppDbContext db, IScheduleConflictService conflict)
        {
            _db = db;
            _conflict = conflict;
        }

        public async Task<PagedResult<ActivityListItemDto>> SearchAsync(
            Guid? roomId, Guid? instructorId, ActivityCategory? category,
            DateTime? fromLocal, DateTime? toLocal, ActivityStatus? status,
            int page, int pageSize, CancellationToken ct)
        {
            var q = _db.Activities.AsNoTracking().AsQueryable();

            if (roomId.HasValue) q = q.Where(a => a.RoomId == roomId.Value);
            if (instructorId.HasValue) q = q.Where(a => a.InstructorId == instructorId.Value);
            if (category.HasValue) q = q.Where(a => a.Category == category.Value);
            if (status.HasValue) q = q.Where(a => a.Status == status.Value);
            if (fromLocal.HasValue) q = q.Where(a => a.StartLocal >= fromLocal.Value);
            if (toLocal.HasValue) q = q.Where(a => a.StartLocal < toLocal.Value);

            q = q.OrderBy(a => a.StartLocal);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new ActivityListItemDto(
                    a.Id, a.RoomId, a.InstructorId, a.Category, a.Title,
                    a.StartLocal, a.DurationMinutes, a.MaxParticipants,
                    Math.Max(0, a.MaxParticipants - a.Participants.Count(p => p.Status == ActivityParticipantStatus.Active)),
                    a.Status))
                .ToListAsync(ct);

            return PagedResult<ActivityListItemDto>.Create(items, page, pageSize, total);
        }

        public async Task<ActivityResponse?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var a = await _db.Activities.AsNoTracking().Include(x => x.Participants).FirstOrDefaultAsync(a => a.Id == id, ct);
            return a == null ? null : new ActivityResponse(
                a.Id, a.RoomId, a.InstructorId, a.Category, a.Title, a.Description,
                a.StartLocal, a.DurationMinutes, a.MaxParticipants,
                Math.Max(0, a.MaxParticipants - a.Participants.Count(p => p.Status == ActivityParticipantStatus.Active)),
                a.Status, a.CreatedByUid, a.CreatedAtLocal);
        }

        public async Task<ActivityResponse> CreateAsync(CreateActivityRequestDto dto, string? createdByUid, CancellationToken ct)
        {
            var start = dto.StartLocal;
            var end   = start.AddMinutes(dto.DurationMinutes);

            // validar conflitos (sala/instrutor)
            var hasConflict = await _conflict.HasConflictAsync(
                staffId: dto.InstructorId,
                roomId: dto.RoomId,
                clientId: null,
                startLocal: start,
                endLocal: end,
                excludeId: null,
                ct: ct);

            if (hasConflict)
                throw new InvalidOperationException("Conflito de agenda (sala ou instrutor).");

            var act = new Activity(
                dto.RoomId, dto.InstructorId, dto.Title, dto.Description,
                dto.Category, dto.StartLocal, dto.DurationMinutes, dto.MaxParticipants,
                createdByUid, DateTime.Now);

            _db.Activities.Add(act);
            await _db.SaveChangesAsync(ct);
            return (await GetByIdAsync(act.Id, ct))!;
        }

        public async Task<ActivityResponse> UpdateAsync(Guid id, UpdateActivityRequestDto dto, CancellationToken ct)
        {
            var act = await _db.Activities.Include(a => a.Participants).FirstOrDefaultAsync(a => a.Id == id, ct);
            if (act == null) throw new KeyNotFoundException("Activity not found.");

            if (dto.RowVersion != null && !act.RowVersion.SequenceEqual(dto.RowVersion))
                throw new DbUpdateConcurrencyException("RowVersion outdated.");

            var start = dto.StartLocal;
            var end   = start.AddMinutes(dto.DurationMinutes);

            var hasConflict = await _conflict.HasConflictAsync(
                staffId: dto.InstructorId,
                roomId: dto.RoomId,
                clientId: null,
                startLocal: start,
                endLocal: end,
                excludeId: id,
                ct: ct);

            if (hasConflict)
                throw new InvalidOperationException("Conflito de agenda (sala ou instrutor).");

            act.Update(dto.RoomId, dto.InstructorId, dto.Title, dto.Description, dto.Category,
                       dto.StartLocal, dto.DurationMinutes, dto.MaxParticipants);
            await _db.SaveChangesAsync(ct);
            return (await GetByIdAsync(act.Id, ct))!;
        }

        public async Task CancelAsync(Guid id, CancellationToken ct)
        {
            var act = await _db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (act == null) return;
            act.Cancel();
            await _db.SaveChangesAsync(ct);
        }

        public async Task CompleteAsync(Guid id, CancellationToken ct)
        {
            var act = await _db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (act == null) return;
            act.Complete();
            await _db.SaveChangesAsync(ct);
        }

        public async Task JoinAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct)
        {
            var act = await _db.Activities.Include(a => a.Participants).FirstOrDefaultAsync(a => a.Id == id, ct);
            if (act == null) throw new KeyNotFoundException("Activity not found.");

            // Garantir que o cliente não tem outro agendamento noutra entidade
            var start = act.StartLocal;
            var end   = act.EndLocal;

            var clientConflict = await _conflict.HasConflictAsync(
                staffId: null,
                roomId: null,
                clientId: clientId,
                startLocal: start,
                endLocal: end,
                excludeId: id,
                ct: ct);

            if (clientConflict)
                throw new InvalidOperationException("O cliente já tem outro agendamento nesta hora.");

            act.AddParticipant(clientId, nowLocal);
            await _db.SaveChangesAsync(ct);
        }

        public async Task LeaveAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct)
        {
            var act = await _db.Activities.Include(a => a.Participants).FirstOrDefaultAsync(a => a.Id == id, ct);
            if (act == null) return;
            act.RemoveParticipant(clientId, nowLocal);
            await _db.SaveChangesAsync(ct);
        }
    }
}
