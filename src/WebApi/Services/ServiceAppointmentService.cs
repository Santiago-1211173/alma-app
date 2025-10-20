using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.ServiceAppointments;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.ServiceAppointments;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services
{
    public sealed class ServiceAppointmentService : IServiceAppointmentService
    {
        private readonly AppDbContext _db;
        private readonly IScheduleConflictService _conflict;

        public ServiceAppointmentService(AppDbContext db, IScheduleConflictService conflict)
        {
            _db = db;
            _conflict = conflict;
        }

        public async Task<PagedResult<ServiceAppointmentListItemDto>> SearchAsync(
            Guid? staffId,
            Guid? roomId,
            Guid? clientId,
            ServiceType? type,
            DateTime? fromLocal,
            DateTime? toLocal,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var q = _db.ServiceAppointments.AsNoTracking().AsQueryable();

            if (staffId.HasValue) q = q.Where(a => a.StaffId == staffId.Value);
            if (roomId.HasValue)  q = q.Where(a => a.RoomId  == roomId.Value);
            if (clientId.HasValue) q = q.Where(a => a.ClientId == clientId.Value || (a.SecondClientId.HasValue && a.SecondClientId.Value == clientId.Value));
            if (type.HasValue) q = q.Where(a => a.ServiceType == type.Value);
            if (fromLocal.HasValue) q = q.Where(a => AppTime.ToLocalFromUtc(a.StartUtc) >= fromLocal.Value);
            if (toLocal.HasValue)   q = q.Where(a => AppTime.ToLocalFromUtc(a.StartUtc) <  toLocal.Value);

            q = q.OrderBy(a => a.StartUtc);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new ServiceAppointmentListItemDto(
                    a.Id,
                    a.ClientId,
                    a.SecondClientId,
                    a.StaffId,
                    a.RoomId,
                    (int)a.ServiceType,
                    AppTime.ToLocalFromUtc(a.StartUtc),
                    AppTime.ToLocalFromUtc(a.StartUtc).AddMinutes(a.DurationMinutes),
                    a.DurationMinutes,
                    (int)a.Status
                ))
                .ToListAsync(ct);

            return PagedResult<ServiceAppointmentListItemDto>.Create(items, page, pageSize, total);
        }

        public async Task<ServiceAppointmentResponse?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var a = await _db.ServiceAppointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (a == null) return null;

            var startLocal = AppTime.ToLocalFromUtc(a.StartUtc);
            var endLocal   = startLocal.AddMinutes(a.DurationMinutes);

            return new ServiceAppointmentResponse(
                a.Id,
                a.ClientId,
                a.SecondClientId,
                a.StaffId,
                a.RoomId,
                (int)a.ServiceType,
                startLocal,
                endLocal,
                a.DurationMinutes,
                (int)a.Status,
                a.CreatedByUid!,
                a.CreatedAtUtc
            );
        }

        public async Task<ServiceAppointmentResponse> CreateAsync(CreateServiceAppointmentRequestDto dto, string? createdByUid, CancellationToken ct)
        {
            var startLocal = dto.Start;
            var endLocal   = startLocal.AddMinutes(dto.DurationMinutes);
            var startUtc   = AppTime.ToUtcFromLocal(startLocal);

            // Verifica conflito staff/sala/cliente
            var conflict = await _conflict.HasConflictAsync(
                staffId: dto.StaffId,
                roomId:  dto.RoomId,
                clientId: dto.ClientId,
                startLocal: startLocal,
                endLocal:   endLocal,
                excludeId: null,
                ct: ct);

            if (dto.SecondClientId.HasValue)
            {
                var conflictSecond = await _conflict.HasConflictAsync(
                    staffId: null,
                    roomId:  null,
                    clientId: dto.SecondClientId.Value,
                    startLocal: startLocal,
                    endLocal:   endLocal,
                    excludeId: null,
                    ct: ct);
                conflict = conflict || conflictSecond;
            }

            if (conflict)
                throw new InvalidOperationException("Conflito de agenda (staff, sala ou cliente).");

            if (string.IsNullOrWhiteSpace(createdByUid))
                throw new InvalidOperationException("createdByUid é necessário para concluir a operação.");

            var appt = new ServiceAppointment(
                dto.ClientId,
                dto.SecondClientId,
                dto.StaffId,
                dto.RoomId,
                (ServiceType)dto.ServiceType,
                startUtc,
                dto.DurationMinutes,
                createdByUid
            );

            _db.ServiceAppointments.Add(appt);
            await _db.SaveChangesAsync(ct);

            // Constrói resposta
            var createdStartLocal = AppTime.ToLocalFromUtc(appt.StartUtc);
            var createdEndLocal   = createdStartLocal.AddMinutes(appt.DurationMinutes);

            return new ServiceAppointmentResponse(
                appt.Id,
                appt.ClientId,
                appt.SecondClientId,
                appt.StaffId,
                appt.RoomId,
                (int)appt.ServiceType,
                createdStartLocal,
                createdEndLocal,
                appt.DurationMinutes,
                (int)appt.Status,
                appt.CreatedByUid!,
                appt.CreatedAtUtc
            );
        }

        public async Task<ServiceAppointmentResponse> UpdateAsync(Guid id, UpdateServiceAppointmentRequestDto dto, CancellationToken ct)
        {
            var appt = await _db.ServiceAppointments.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (appt == null) throw new KeyNotFoundException("Serviço não encontrado.");

            var startLocal = dto.Start;
            var endLocal   = startLocal.AddMinutes(dto.DurationMinutes);
            var startUtc   = AppTime.ToUtcFromLocal(startLocal);

            // Verifica conflito staff/sala/cliente, excluindo a própria marcação
            var conflict = await _conflict.HasConflictAsync(
                staffId: dto.StaffId,
                roomId:  dto.RoomId,
                clientId: dto.ClientId,
                startLocal: startLocal,
                endLocal:   endLocal,
                excludeId: id,
                ct: ct);

            if (dto.SecondClientId.HasValue)
            {
                var conflictSecond = await _conflict.HasConflictAsync(
                    staffId: null,
                    roomId:  null,
                    clientId: dto.SecondClientId.Value,
                    startLocal: startLocal,
                    endLocal:   endLocal,
                    excludeId: id,
                    ct: ct);
                conflict = conflict || conflictSecond;
            }

            if (conflict)
                throw new InvalidOperationException("Conflito de agenda (staff, sala ou cliente).");

            appt.Update(
                dto.ClientId,
                dto.SecondClientId,
                dto.StaffId,
                dto.RoomId,
                (ServiceType)dto.ServiceType,
                startUtc,
                dto.DurationMinutes
            );

            await _db.SaveChangesAsync(ct);

            var updatedStartLocal = AppTime.ToLocalFromUtc(appt.StartUtc);
            var updatedEndLocal   = updatedStartLocal.AddMinutes(appt.DurationMinutes);

            return new ServiceAppointmentResponse(
                appt.Id,
                appt.ClientId,
                appt.SecondClientId,
                appt.StaffId,
                appt.RoomId,
                (int)appt.ServiceType,
                updatedStartLocal,
                updatedEndLocal,
                appt.DurationMinutes,
                (int)appt.Status,
                appt.CreatedByUid!,
                appt.CreatedAtUtc
            );
        }

        public async Task CancelAsync(Guid id, CancellationToken ct)
        {
            var appt = await _db.ServiceAppointments.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (appt == null) return;
            appt.Cancel();
            await _db.SaveChangesAsync(ct);
        }

        public async Task CompleteAsync(Guid id, CancellationToken ct)
        {
            var appt = await _db.ServiceAppointments.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (appt == null) return;
            appt.Complete();
            await _db.SaveChangesAsync(ct);
        }

        public Task<PagedResult<ServiceAppointmentListItemDto>> SearchAsync(Guid? clientId, Guid? staffId, Guid? roomId, int? serviceType, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(Guid id, UpdateServiceAppointmentRequestDto dto, string currentUid, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task CancelAsync(Guid id, string currentUid, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task CompleteAsync(Guid id, string currentUid, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
