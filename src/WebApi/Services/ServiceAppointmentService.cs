using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.ServiceAppointments;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.ServiceAppointments;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

/// <summary>
/// Implementação da camada de aplicação para marcações de serviços
/// individuais.  Inclui verificações de conflito de agenda com base em
/// marcações existentes e validações de regras de negócio (máximo de dois
/// clientes para PT, validação de durações, etc.).  Esta classe assume
/// responsabilidade de converter datas locais para UTC via AppTime.
/// </summary>
public sealed class ServiceAppointmentService : IServiceAppointmentService
{
    private readonly AppDbContext _db;

    public ServiceAppointmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ServiceAppointmentListItemDto>> SearchAsync(
        Guid? clientId,
        Guid? staffId,
        Guid? roomId,
        int? serviceType,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        var q = _db.ServiceAppointments.AsNoTracking().AsQueryable();

        if (clientId.HasValue)
        {
            var cid = clientId.Value;
            q = q.Where(a => a.ClientId == cid || a.SecondClientId == cid);
        }
        if (staffId.HasValue) q = q.Where(a => a.StaffId == staffId.Value);
        if (roomId.HasValue) q = q.Where(a => a.RoomId == roomId.Value);
        if (serviceType.HasValue) q = q.Where(a => (int)a.ServiceType == serviceType.Value);
        if (from.HasValue) q = q.Where(a => a.StartUtc >= AppTime.ToUtcFromLocal(from.Value));
        if (to.HasValue) q = q.Where(a => a.StartUtc < AppTime.ToUtcFromLocal(to.Value));

        var total = await q.CountAsync(ct);

        var items = await q.OrderBy(a => a.StartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ServiceAppointmentListItemDto(
                a.Id,
                a.ClientId,
                a.SecondClientId,
                a.StaffId,
                a.RoomId,
                (int)a.ServiceType,
                AppTime.ToLocalFromUtc(a.StartUtc),
                a.StartUtc,
                a.DurationMinutes,
                (int)a.Status))
            .ToListAsync(ct);

        return PagedResult<ServiceAppointmentListItemDto>.Create(items, page, pageSize, total);
    }

    public async Task<ServiceAppointmentResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.ServiceAppointments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a == null) return null;
        return new ServiceAppointmentResponse(
            a.Id,
            a.ClientId,
            a.SecondClientId,
            a.StaffId,
            a.RoomId,
            (int)a.ServiceType,
            AppTime.ToLocalFromUtc(a.StartUtc),
            a.StartUtc,
            a.DurationMinutes,
            (int)a.Status,
            a.CreatedByUid,
            a.CreatedAtUtc);
    }

    public async Task<ServiceAppointmentResponse> CreateAsync(CreateServiceAppointmentRequestDto dto, string currentUid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentUid))
            throw new ArgumentException("UID do utilizador em falta.");

        // valida existência de entidades
        var clientExists = await _db.Clients.AnyAsync(c => c.Id == dto.ClientId, ct);
        if (!clientExists) throw new ArgumentException("ClientId inválido.");
        if (dto.SecondClientId.HasValue)
        {
            var exists2 = await _db.Clients.AnyAsync(c => c.Id == dto.SecondClientId.Value, ct);
            if (!exists2) throw new ArgumentException("SecondClientId inválido.");
            if (dto.SecondClientId.Value == dto.ClientId)
                throw new ArgumentException("ClientId e SecondClientId não podem ser iguais.");
        }
        var staffExists = await _db.Staff.AnyAsync(s => s.Id == dto.StaffId, ct);
        if (!staffExists) throw new ArgumentException("StaffId inválido.");
        var roomExists = await _db.Rooms.AnyAsync(r => r.Id == dto.RoomId, ct);
        if (!roomExists) throw new ArgumentException("RoomId inválido.");

        // converter hora local para UTC
        var startUtc = AppTime.ToUtcFromLocal(dto.Start);
        var endUtc = startUtc.AddMinutes(dto.DurationMinutes);

        var sType = (ServiceType)dto.ServiceType;

        // verificar conflito com outras marcações do mesmo staff
        var staffConflict = await _db.ServiceAppointments
            .Where(a => a.Status == ServiceAppointmentStatus.Scheduled && a.StaffId == dto.StaffId)
            .AnyAsync(a => a.StartUtc < endUtc && startUtc < a.StartUtc.AddMinutes(a.DurationMinutes), ct);
        if (staffConflict) throw new InvalidOperationException("Conflito de agenda (Staff). O staff está ocupado nesse horário.");

        // verificar conflito com outras marcações na mesma sala
        var roomConflict = await _db.ServiceAppointments
            .Where(a => a.Status == ServiceAppointmentStatus.Scheduled && a.RoomId == dto.RoomId)
            .AnyAsync(a => a.StartUtc < endUtc && startUtc < a.StartUtc.AddMinutes(a.DurationMinutes), ct);
        if (roomConflict) throw new InvalidOperationException("Conflito de agenda (Sala). A sala está ocupada nesse horário.");

        // verificar conflito com classes e activities existentes (opcional)
        // evita que staff ou sala esteja duplicada entre marcações e aulas 1:1 ou actividades extra
        var classConflictStaff = await _db.Classes
            .Where(c => c.Status == AlmaApp.Domain.Classes.ClassStatus.Scheduled && c.StaffId == dto.StaffId)
            .AnyAsync(c => c.StartUtc < endUtc && startUtc < c.StartUtc.AddMinutes(c.DurationMinutes), ct);
        if (classConflictStaff) throw new InvalidOperationException("Conflito de agenda (Classe/Staff). O staff tem uma aula marcada nesse horário.");
        var classConflictRoom = await _db.Classes
            .Where(c => c.Status == AlmaApp.Domain.Classes.ClassStatus.Scheduled && c.RoomId == dto.RoomId)
            .AnyAsync(c => c.StartUtc < endUtc && startUtc < c.StartUtc.AddMinutes(c.DurationMinutes), ct);
        if (classConflictRoom) throw new InvalidOperationException("Conflito de agenda (Classe/Sala). A sala tem uma aula marcada nesse horário.");

        // Not checking Activities because Activities do not track staff.

        var appointment = new ServiceAppointment(
            dto.ClientId,
            dto.SecondClientId,
            dto.StaffId,
            dto.RoomId,
            sType,
            startUtc,
            dto.DurationMinutes,
            currentUid);

        _db.ServiceAppointments.Add(appointment);
        await _db.SaveChangesAsync(ct);

        return new ServiceAppointmentResponse(
            appointment.Id,
            appointment.ClientId,
            appointment.SecondClientId,
            appointment.StaffId,
            appointment.RoomId,
            (int)appointment.ServiceType,
            AppTime.ToLocalFromUtc(appointment.StartUtc),
            appointment.StartUtc,
            appointment.DurationMinutes,
            (int)appointment.Status,
            appointment.CreatedByUid,
            appointment.CreatedAtUtc);
    }

    public async Task UpdateAsync(Guid id, UpdateServiceAppointmentRequestDto dto, string currentUid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentUid))
            throw new ArgumentException("UID do utilizador em falta.");
        var appointment = await _db.ServiceAppointments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (appointment == null) throw new InvalidOperationException("Marcaçao não encontrada.");

        // validar existência de entidades
        var clientExists = await _db.Clients.AnyAsync(c => c.Id == dto.ClientId, ct);
        if (!clientExists) throw new ArgumentException("ClientId inválido.");
        if (dto.SecondClientId.HasValue)
        {
            var exists2 = await _db.Clients.AnyAsync(c => c.Id == dto.SecondClientId.Value, ct);
            if (!exists2) throw new ArgumentException("SecondClientId inválido.");
            if (dto.SecondClientId.Value == dto.ClientId)
                throw new ArgumentException("ClientId e SecondClientId não podem ser iguais.");
        }
        var staffExists = await _db.Staff.AnyAsync(s => s.Id == dto.StaffId, ct);
        if (!staffExists) throw new ArgumentException("StaffId inválido.");
        var roomExists = await _db.Rooms.AnyAsync(r => r.Id == dto.RoomId, ct);
        if (!roomExists) throw new ArgumentException("RoomId inválido.");

        // converter hora local para UTC
        var startUtc = AppTime.ToUtcFromLocal(dto.Start);
        var endUtc = startUtc.AddMinutes(dto.DurationMinutes);

        var sType = (ServiceType)dto.ServiceType;

        // verificar conflitos, excluindo a própria marcação
        var staffConflict = await _db.ServiceAppointments
            .Where(a => a.Id != id && a.Status == ServiceAppointmentStatus.Scheduled && a.StaffId == dto.StaffId)
            .AnyAsync(a => a.StartUtc < endUtc && startUtc < a.StartUtc.AddMinutes(a.DurationMinutes), ct);
        if (staffConflict) throw new InvalidOperationException("Conflito de agenda (Staff) ao editar.");
        var roomConflict = await _db.ServiceAppointments
            .Where(a => a.Id != id && a.Status == ServiceAppointmentStatus.Scheduled && a.RoomId == dto.RoomId)
            .AnyAsync(a => a.StartUtc < endUtc && startUtc < a.StartUtc.AddMinutes(a.DurationMinutes), ct);
        if (roomConflict) throw new InvalidOperationException("Conflito de agenda (Sala) ao editar.");

        // conflitos com Classes
        var classConflictStaff = await _db.Classes
            .Where(c => c.Status == AlmaApp.Domain.Classes.ClassStatus.Scheduled && c.StaffId == dto.StaffId)
            .AnyAsync(c => c.StartUtc < endUtc && startUtc < c.StartUtc.AddMinutes(c.DurationMinutes), ct);
        if (classConflictStaff) throw new InvalidOperationException("Conflito de agenda (Classe/Staff) ao editar.");
        var classConflictRoom = await _db.Classes
            .Where(c => c.Status == AlmaApp.Domain.Classes.ClassStatus.Scheduled && c.RoomId == dto.RoomId)
            .AnyAsync(c => c.StartUtc < endUtc && startUtc < c.StartUtc.AddMinutes(c.DurationMinutes), ct);
        if (classConflictRoom) throw new InvalidOperationException("Conflito de agenda (Classe/Sala) ao editar.");

        appointment.Update(
            dto.ClientId,
            dto.SecondClientId,
            dto.StaffId,
            dto.RoomId,
            sType,
            startUtc,
            dto.DurationMinutes);

        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid id, string currentUid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentUid))
            throw new ArgumentException("UID do utilizador em falta.");
        var appointment = await _db.ServiceAppointments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (appointment == null) throw new InvalidOperationException("Marcaçao não encontrada.");
        appointment.Cancel();
        await _db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid id, string currentUid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentUid))
            throw new ArgumentException("UID do utilizador em falta.");
        var appointment = await _db.ServiceAppointments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (appointment == null) throw new InvalidOperationException("Marcaçao não encontrada.");
        appointment.Complete();
        await _db.SaveChangesAsync(ct);
    }
}