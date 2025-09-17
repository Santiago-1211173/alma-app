using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.Domain.Classes;

public enum ClassStatus { Scheduled = 0, Completed = 1, Canceled = 9 }

public class Class
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid StaffId  { get; private set; }
    public Guid RoomId   { get; private set; }

    public DateTime StartUtc { get; private set; }
    public int DurationMinutes { get; private set; }

    public ClassStatus Status { get; private set; }
    public string CreatedByUid { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? LinkedRequestId { get; private set; }  // opcional

    public byte[] RowVersion { get; private set; } = default!;

    private Class() { } // EF

    public Class(Guid clientId, Guid staffId, Guid roomId,
                 DateTime startUtc, int durationMinutes,
                 string createdByUid, Guid? linkedRequestId = null)
    {
        if (durationMinutes < 15 || durationMinutes > 180)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));

        Id = Guid.NewGuid();
        ClientId = clientId;
        StaffId  = staffId;
        RoomId   = roomId;
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        DurationMinutes = durationMinutes;
        Status = ClassStatus.Scheduled;
        CreatedByUid = createdByUid;
        CreatedAtUtc = DateTime.UtcNow;
        LinkedRequestId = linkedRequestId;
    }

    public void Reschedule(DateTime startUtc, int durationMinutes, Guid roomId)
    {
        if (Status != ClassStatus.Scheduled)
            throw new InvalidOperationException("Só aulas agendadas podem ser reagendadas.");
        if (durationMinutes < 15 || durationMinutes > 180)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));

        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        DurationMinutes = durationMinutes;
        RoomId = roomId;
    }

    public void Cancel()
    {
        if (Status == ClassStatus.Canceled) return;
        Status = ClassStatus.Canceled;
    }

    public void Complete()
    {
        if (Status != ClassStatus.Scheduled)
            throw new InvalidOperationException("Só aulas agendadas podem ser concluídas.");
        Status = ClassStatus.Completed;
    }

    public DateTime EndUtc => StartUtc.AddMinutes(DurationMinutes);
}
