using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System;

namespace AlmaApp.Domain.ServiceAppointments;


public class ServiceAppointment
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? SecondClientId { get; private set; }
    public Guid StaffId { get; private set; }
    public Guid RoomId { get; private set; }
    public ServiceType ServiceType { get; private set; }
    public DateTime StartUtc { get; private set; }
    public int DurationMinutes { get; private set; }
    public ServiceAppointmentStatus Status { get; private set; }
    public string CreatedByUid { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    // EF Core requires a parameterless constructor
    private ServiceAppointment() { }

    public ServiceAppointment(Guid clientId, Guid? secondClientId, Guid staffId, Guid roomId,
                              ServiceType serviceType, DateTime startUtc, int durationMinutes,
                              string createdByUid)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("clientId");
        if (secondClientId == Guid.Empty) throw new ArgumentException("secondClientId");
        if (staffId == Guid.Empty) throw new ArgumentException("staffId");
        if (roomId == Guid.Empty) throw new ArgumentException("roomId");
        if (string.IsNullOrWhiteSpace(createdByUid)) throw new ArgumentException(nameof(createdByUid));
        if (durationMinutes < 15 || durationMinutes > 240)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "A duração deve estar entre 15 e 240 minutos.");

        // Para serviços que não são de PT, não é permitido um segundo cliente
        bool isPt = serviceType == ServiceType.PT || serviceType == ServiceType.PTYoga ||
                    serviceType == ServiceType.PTPilates || serviceType == ServiceType.PTBarre;
        if (!isPt && secondClientId.HasValue)
            throw new ArgumentException("Só é permitido um segundo cliente para serviços de PT.");

        Id = Guid.NewGuid();
        ClientId = clientId;
        SecondClientId = secondClientId;
        StaffId = staffId;
        RoomId = roomId;
        ServiceType = serviceType;
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        DurationMinutes = durationMinutes;
        Status = ServiceAppointmentStatus.Scheduled;
        CreatedByUid = createdByUid.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(Guid clientId, Guid? secondClientId, Guid staffId, Guid roomId,
                       ServiceType serviceType, DateTime startUtc, int durationMinutes)
    {
        if (Status != ServiceAppointmentStatus.Scheduled)
            throw new InvalidOperationException("Só marcações agendadas podem ser editadas.");
        if (clientId == Guid.Empty) throw new ArgumentException("clientId");
        if (secondClientId == Guid.Empty) throw new ArgumentException("secondClientId");
        if (staffId == Guid.Empty) throw new ArgumentException("staffId");
        if (roomId == Guid.Empty) throw new ArgumentException("roomId");
        if (durationMinutes < 15 || durationMinutes > 240)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "A duração deve estar entre 15 e 240 minutos.");

        bool isPt = serviceType == ServiceType.PT || serviceType == ServiceType.PTYoga ||
                    serviceType == ServiceType.PTPilates || serviceType == ServiceType.PTBarre;
        if (!isPt && secondClientId.HasValue)
            throw new ArgumentException("Só é permitido um segundo cliente para serviços de PT.");

        ClientId = clientId;
        SecondClientId = secondClientId;
        StaffId = staffId;
        RoomId = roomId;
        ServiceType = serviceType;
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        DurationMinutes = durationMinutes;
    }

    public void Cancel()
    {
        if (Status == ServiceAppointmentStatus.Canceled) return;
        Status = ServiceAppointmentStatus.Canceled;
    }

    public void Complete()
    {
        if (Status != ServiceAppointmentStatus.Scheduled)
            throw new InvalidOperationException("Só marcações agendadas podem ser concluídas.");
        Status = ServiceAppointmentStatus.Completed;
    }

    public DateTime EndUtc => StartUtc.AddMinutes(DurationMinutes);
}