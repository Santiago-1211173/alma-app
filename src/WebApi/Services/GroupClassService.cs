using System;
using System.Linq;
using System.Threading;
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
        private readonly IScheduleConflictService _conflict;

        public GroupClassService(AppDbContext db, IScheduleConflictService conflict)
        {
            _db = db;
            _conflict = conflict;
        }

        public async Task<PagedResult<GroupClassListItemDto>> SearchAsync(
            GroupClassCategory? category, Guid? instructorId, Guid? roomId,
            DateTime? fromLocal, DateTime? toLocal, int page, int pageSize,
            CancellationToken ct)
        {
            var q = _db.GroupClasses.AsNoTracking().AsQueryable();

            if (category.HasValue) q = q.Where(g => g.Category == category.Value);
            if (instructorId.HasValue) q = q.Where(g => g.InstructorId == instructorId.Value);
            if (roomId.HasValue) q = q.Where(g => g.RoomId == roomId.Value);
            if (fromLocal.HasValue) q = q.Where(g => g.StartLocal >= fromLocal.Value);
            if (toLocal.HasValue) q = q.Where(g => g.StartLocal < toLocal.Value);

            q = q.OrderBy(g => g.StartLocal);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(g => new GroupClassListItemDto(
                    g.Id, g.Category, g.Title, g.InstructorId, g.RoomId, g.StartLocal,
                    g.DurationMinutes, g.MaxParticipants,
                    Math.Max(0, g.MaxParticipants - g.Participants.Count(p => p.Status == GroupClassParticipantStatus.Active)),
                    g.Status))
                .ToListAsync(ct);

            return PagedResult<GroupClassListItemDto>.Create(items, page, pageSize, total);
        }

        public async Task<GroupClassResponse?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var g = await _db.GroupClasses.AsNoTracking().Include(gc => gc.Participants).FirstOrDefaultAsync(gc => gc.Id == id, ct);
            return g == null ? null : new GroupClassResponse(
                g.Id, g.Category, g.Title, g.InstructorId, g.RoomId, g.StartLocal,
                g.DurationMinutes, g.MaxParticipants,
                Math.Max(0, g.MaxParticipants - g.Participants.Count(p => p.Status == GroupClassParticipantStatus.Active)),
                g.Status, g.Participants.Count(p => p.Status == GroupClassParticipantStatus.Active));
        }

        public async Task<GroupClassResponse> CreateAsync(CreateGroupClassRequestDto req, string? createdByUid, CancellationToken ct)
        {
            var start = req.StartLocal;
            var end   = start.AddMinutes(req.DurationMinutes);

            var conflict = await _conflict.HasConflictAsync(
                staffId: req.InstructorId,
                roomId: req.RoomId,
                clientId: null,
                startLocal: start,
                endLocal: end,
                excludeId: null,
                ct: ct);

            if (conflict)
                throw new InvalidOperationException("Conflito de agenda (instrutor ou sala).");

            var gc = new GroupClass(
                req.InstructorId, req.RoomId, req.Category, req.Title,
                req.StartLocal, req.DurationMinutes, req.MaxParticipants,
                createdByUid, DateTime.Now);

            _db.GroupClasses.Add(gc);
            await _db.SaveChangesAsync(ct);
            return (await GetByIdAsync(gc.Id, ct))!;
        }

        public async Task<GroupClassResponse> UpdateAsync(Guid id, UpdateGroupClassRequestDto req, CancellationToken ct)
        {
            var gc = await _db.GroupClasses.Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (gc == null) throw new KeyNotFoundException("Aula não encontrada.");

            if (req.RowVersion != null && !gc.RowVersion.SequenceEqual(req.RowVersion))
                throw new DbUpdateConcurrencyException("RowVersion desactualizado.");

            var start = req.StartLocal;
            var end   = start.AddMinutes(req.DurationMinutes);

            var conflict = await _conflict.HasConflictAsync(
                staffId: req.InstructorId,
                roomId: req.RoomId,
                clientId: null,
                startLocal: start,
                endLocal: end,
                excludeId: id,
                ct: ct);

            if (conflict)
                throw new InvalidOperationException("Conflito de agenda (instrutor ou sala).");

            gc.Update(req.InstructorId, req.RoomId, req.Category, req.Title, req.StartLocal, req.DurationMinutes, req.MaxParticipants);
            await _db.SaveChangesAsync(ct);
            return (await GetByIdAsync(gc.Id, ct))!;
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

            var start = gc.StartLocal;
            var end   = gc.EndLocal;

            // Verifica se o cliente já está noutra agenda
            var conflict = await _conflict.HasConflictAsync(
                staffId: null,
                roomId: null,
                clientId: clientId,
                startLocal: start,
                endLocal: end,
                excludeId: id,
                ct: ct);

            if (conflict)
                throw new InvalidOperationException("Cliente já tem outra marcação neste horário.");

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
    }
}
