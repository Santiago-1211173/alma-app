using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.GroupClasses;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.GroupClasses;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services
{
    public sealed class GroupClassService : IGroupClassService
    {
        private readonly AppDbContext _db;

        public GroupClassService(AppDbContext db) => _db = db;

        public async Task<PagedResult<GroupClassListItemDto>> SearchAsync(
            GroupClassCategory? category, Guid? instructorId, Guid? roomId,
            DateTime? fromLocal, DateTime? toLocal, int page, int pageSize,
            CancellationToken ct)
        {
            var q = _db.GroupClasses.AsNoTracking().AsQueryable();

            if (category.HasValue) q = q.Where(x => x.Category == category.Value);
            if (instructorId.HasValue) q = q.Where(x => x.InstructorId == instructorId.Value);
            if (roomId.HasValue) q = q.Where(x => x.RoomId == roomId.Value);
            if (fromLocal.HasValue) q = q.Where(x => x.StartLocal >= fromLocal.Value);
            if (toLocal.HasValue) q = q.Where(x => x.StartLocal < toLocal.Value);

            q = q.OrderBy(x => x.StartLocal);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new GroupClassListItemDto(
                    x.Id, x.Category, x.Title, x.InstructorId, x.RoomId,
                    x.StartLocal, x.DurationMinutes, x.MaxParticipants,
                    Math.Max(0, x.MaxParticipants - x.Participants.Count(p => p.Status == GroupClassParticipantStatus.Active)),
                    x.Status))
                .ToListAsync(ct);

            return PagedResult<GroupClassListItemDto>.Create(items, page, pageSize, total);
        }

        public async Task<GroupClassResponse?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var x = await _db.GroupClasses
                .AsNoTracking()
                .Include(g => g.Participants)
                .FirstOrDefaultAsync(g => g.Id == id, ct);

            return x == null
                ? null
                : new GroupClassResponse(
                    x.Id, x.Category, x.Title, x.InstructorId, x.RoomId,
                    x.StartLocal, x.DurationMinutes, x.MaxParticipants,
                    Math.Max(0, x.MaxParticipants - x.Participants.Count(p => p.Status == GroupClassParticipantStatus.Active)),
                    x.Status,
                    x.Participants.Count(p => p.Status == GroupClassParticipantStatus.Active));
        }

        public async Task<GroupClassResponse> CreateAsync(CreateGroupClassRequestDto req, string? createdByUid, CancellationToken ct)
        {
            var gc = new GroupClass(
                req.InstructorId, req.RoomId, req.Category, req.Title,
                req.StartLocal, req.DurationMinutes, req.MaxParticipants,
                createdByUid, DateTime.Now);

            // conflitos de agenda (instrutor/sala)
            await EnsureNoConflictsAsync(gc.InstructorId, gc.RoomId, gc.StartLocal, gc.EndLocal, excludeId: null, ct);

            _db.GroupClasses.Add(gc);
            await _db.SaveChangesAsync(ct);

            return await GetByIdAsync(gc.Id, ct) ?? throw new InvalidOperationException("Erro ao ler aula criada.");
        }

        public async Task<GroupClassResponse> UpdateAsync(Guid id, UpdateGroupClassRequestDto req, CancellationToken ct)
        {
            var gc = await _db.GroupClasses.Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (gc == null) throw new KeyNotFoundException("Aula não encontrada.");

            // concorrência optimista
            if (req.RowVersion != null && !gc.RowVersion.SequenceEqual(req.RowVersion))
                throw new DbUpdateConcurrencyException("RowVersion desactualizado.");

            // valida conflitos
            var newEnd = req.StartLocal.AddMinutes(req.DurationMinutes);
            await EnsureNoConflictsAsync(req.InstructorId, req.RoomId, req.StartLocal, newEnd, id, ct);

            gc.Update(req.InstructorId, req.RoomId, req.Category, req.Title, req.StartLocal, req.DurationMinutes, req.MaxParticipants);
            await _db.SaveChangesAsync(ct);

            return await GetByIdAsync(gc.Id, ct) ?? throw new InvalidOperationException("Erro ao ler aula actualizada.");
        }

        public async Task CancelAsync(Guid id, CancellationToken ct)
        {
            var gc = await _db.GroupClasses.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (gc == null) return;
            gc.Cancel();
            await _db.SaveChangesAsync(ct);
        }

        public async Task CompleteAsync(Guid id, CancellationToken ct)
        {
            var gc = await _db.GroupClasses.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (gc == null) return;
            gc.Complete();
            await _db.SaveChangesAsync(ct);
        }

        public async Task JoinAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct)
        {
            var gc = await _db.GroupClasses.Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (gc == null) throw new KeyNotFoundException("Aula não encontrada.");

            // proibir dupla inscrição activa
            if (gc.Participants.Any(p => p.ClientId == clientId && p.Status == GroupClassParticipantStatus.Active))
                throw new InvalidOperationException("Cliente já inscrito.");

            if (gc.AvailableSlots <= 0)
                throw new InvalidOperationException("Aula sem vagas.");

            gc.AddParticipant(clientId, nowLocal);
            await _db.SaveChangesAsync(ct);
        }

        public async Task LeaveAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct)
        {
            var gc = await _db.GroupClasses.Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (gc == null) return;
            gc.RemoveParticipant(clientId, nowLocal);
            await _db.SaveChangesAsync(ct);
        }

        private async Task EnsureNoConflictsAsync(Guid instructorId, Guid roomId, DateTime startLocal, DateTime endLocal, Guid? excludeId, CancellationToken ct)
        {
            bool conflictInstructor = await _db.GroupClasses
                .AnyAsync(x =>
                    x.Status == GroupClassStatus.Scheduled &&
                    x.InstructorId == instructorId &&
                    Overlaps(startLocal, endLocal, x.StartLocal, x.StartLocal.AddMinutes(x.DurationMinutes)) &&
                    (excludeId == null || x.Id != excludeId.Value), ct);

            if (conflictInstructor) throw new InvalidOperationException("Conflito com agenda do instrutor.");

            bool conflictRoom = await _db.GroupClasses
                .AnyAsync(x =>
                    x.Status == GroupClassStatus.Scheduled &&
                    x.RoomId == roomId &&
                    Overlaps(startLocal, endLocal, x.StartLocal, x.StartLocal.AddMinutes(x.DurationMinutes)) &&
                    (excludeId == null || x.Id != excludeId.Value), ct);

            if (conflictRoom) throw new InvalidOperationException("Conflito com agenda da sala.");
        }

        private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
            => aStart < bEnd && bStart < aEnd;
    }
}
