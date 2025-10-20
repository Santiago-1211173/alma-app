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
    public sealed class ActivityService : IActivityService
    {
        private readonly AppDbContext _db;

        public ActivityService(AppDbContext db) => _db = db;

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
                a.Status, a.CreatedByUid, a.CreatedAtLocal
            );
        }

        public async Task<ActivityResponse> CreateAsync(CreateActivityRequestDto dto, string? createdByUid, CancellationToken ct)
        {
            // validações de existência de sala/instrutor omitidas (adiciona se necessário)
            var act = new Activity(
                dto.RoomId, dto.InstructorId, dto.Title, dto.Description,
                dto.Category, dto.StartLocal, dto.DurationMinutes, dto.MaxParticipants,
                createdByUid, DateTime.Now);

            // Verificar conflitos (sala/instrutor) com outras Activities
            await EnsureNoConflictsAsync(act.RoomId, act.InstructorId, act.StartLocal, act.EndLocal, excludeId: null, ct);

            _db.Activities.Add(act);
            await _db.SaveChangesAsync(ct);

            return (await GetByIdAsync(act.Id, ct))!;
        }

        public async Task<ActivityResponse> UpdateAsync(Guid id, UpdateActivityRequestDto dto, CancellationToken ct)
        {
            var act = await _db.Activities.Include(x => x.Participants).FirstOrDefaultAsync(a => a.Id == id, ct);
            if (act == null) throw new KeyNotFoundException("Activity not found.");

            if (dto.RowVersion != null && !act.RowVersion.SequenceEqual(dto.RowVersion))
                throw new DbUpdateConcurrencyException("RowVersion outdated.");

            var newEnd = dto.StartLocal.AddMinutes(dto.DurationMinutes);
            await EnsureNoConflictsAsync(dto.RoomId, dto.InstructorId, dto.StartLocal, newEnd, id, ct);

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

        private async Task EnsureNoConflictsAsync(Guid roomId, Guid instructorId,
            DateTime startLocal, DateTime endLocal, Guid? excludeId, CancellationToken ct)
        {
            bool conflictRoom = await _db.Activities
                .AnyAsync(a =>
                    a.Status == ActivityStatus.Scheduled &&
                    a.RoomId == roomId &&
                    Overlaps(startLocal, endLocal, a.StartLocal, a.StartLocal.AddMinutes(a.DurationMinutes)) &&
                    (excludeId == null || a.Id != excludeId.Value), ct);

            if (conflictRoom) throw new InvalidOperationException("Conflito com agenda da sala.");

            bool conflictInstructor = await _db.Activities
                .AnyAsync(a =>
                    a.Status == ActivityStatus.Scheduled &&
                    a.InstructorId == instructorId &&
                    Overlaps(startLocal, endLocal, a.StartLocal, a.StartLocal.AddMinutes(a.DurationMinutes)) &&
                    (excludeId == null || a.Id != excludeId.Value), ct);

            if (conflictInstructor) throw new InvalidOperationException("Conflito com agenda do instrutor.");
        }

        private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
            => aStart < bEnd && bStart < aEnd;
    }
}
