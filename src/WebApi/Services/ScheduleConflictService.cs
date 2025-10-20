using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Activities;
using AlmaApp.Domain.GroupClasses;
using AlmaApp.Domain.ServiceAppointments;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common; // para AppTime
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services
{
    public sealed class ScheduleConflictService : IScheduleConflictService
    {
        private readonly AppDbContext _db;

        public ScheduleConflictService(AppDbContext db) => _db = db;

        public async Task<bool> HasConflictAsync(
            Guid? staffId,
            Guid? roomId,
            Guid? clientId,
            DateTime startLocal,
            DateTime endLocal,
            Guid? excludeId = null,
            CancellationToken ct = default)
        {
            // Workshops (Activities)
            if (staffId.HasValue || roomId.HasValue || clientId.HasValue)
            {
                var q = _db.Activities.AsNoTracking()
                    .Where(a => a.Status == ActivityStatus.Scheduled &&
                                startLocal < a.StartLocal.AddMinutes(a.DurationMinutes) &&
                                a.StartLocal < endLocal);

                if (excludeId.HasValue) q = q.Where(a => a.Id != excludeId.Value);

                if (staffId.HasValue) q = q.Where(a => a.InstructorId == staffId.Value);
                if (roomId.HasValue)  q = q.Where(a => a.RoomId == roomId.Value);
                if (clientId.HasValue)
                {
                    q = q.Where(a => a.Participants.Any(p => p.ClientId == clientId.Value && p.Status == ActivityParticipantStatus.Active));
                }

                if (await q.AnyAsync(ct)) return true;
            }

            // Aulas de grupo (GroupClasses)
            {
                var q = _db.GroupClasses.AsNoTracking()
                    .Where(g => g.Status == GroupClassStatus.Scheduled &&
                                startLocal < g.StartLocal.AddMinutes(g.DurationMinutes) &&
                                g.StartLocal < endLocal);

                if (excludeId.HasValue) q = q.Where(g => g.Id != excludeId.Value);

                if (staffId.HasValue) q = q.Where(g => g.InstructorId == staffId.Value);
                if (roomId.HasValue)  q = q.Where(g => g.RoomId == roomId.Value);
                if (clientId.HasValue)
                {
                    q = q.Where(g => g.Participants.Any(p => p.ClientId == clientId.Value && p.Status == GroupClassParticipantStatus.Active));
                }

                if (await q.AnyAsync(ct)) return true;
            }

            // Serviços individuais (ServiceAppointment) – StartUtc convertido para local
            {
                var q = _db.ServiceAppointments.AsNoTracking()
                    .Where(s => s.Status == ServiceAppointmentStatus.Scheduled &&
                                startLocal < AppTime.ToLocalFromUtc(s.StartUtc.AddMinutes(s.DurationMinutes)) &&
                                AppTime.ToLocalFromUtc(s.StartUtc) < endLocal);

                if (excludeId.HasValue) q = q.Where(s => s.Id != excludeId.Value);

                if (staffId.HasValue) q = q.Where(s => s.StaffId == staffId.Value);
                if (roomId.HasValue)  q = q.Where(s => s.RoomId == roomId.Value);
                if (clientId.HasValue)
                {
                    q = q.Where(s => s.ClientId == clientId.Value || s.SecondClientId == clientId.Value);
                }

                if (await q.AnyAsync(ct)) return true;
            }

            return false;
        }
    }
}
