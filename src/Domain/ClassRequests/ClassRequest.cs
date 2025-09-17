// src/Domain/ClassRequests/ClassRequest.cs
using System;

namespace AlmaApp.Domain.ClassRequests;

public enum ClassRequestStatus { Pending = 0, Approved = 1, Canceled = 9 }

public class ClassRequest
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid StaffId  { get; private set; }
    public DateTime ProposedStartUtc { get; private set; }
    public int DurationMinutes { get; private set; } // 15..180
    public string? Notes { get; private set; }

    public ClassRequestStatus Status { get; private set; }
    public string CreatedByUid { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = default!; // concurrency

    private ClassRequest() { } // EF

    public ClassRequest(Guid clientId, Guid staffId, DateTime proposedStartUtc, int durationMinutes, string? notes, string createdByUid)
    {
        Id = Guid.NewGuid();
        Update(clientId, staffId, proposedStartUtc, durationMinutes, notes);
        Status = ClassRequestStatus.Pending;
        CreatedByUid = createdByUid;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(Guid clientId, Guid staffId, DateTime proposedStartUtc, int durationMinutes, string? notes)
    {
        if (durationMinutes < 15 || durationMinutes > 180)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));

        ClientId = clientId;
        StaffId  = staffId;
        ProposedStartUtc = DateTime.SpecifyKind(proposedStartUtc, DateTimeKind.Utc);
        DurationMinutes  = durationMinutes;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void Approve()
    {
        if (Status != ClassRequestStatus.Pending)
            throw new InvalidOperationException("Só pedidos pendentes podem ser aprovados.");
        Status = ClassRequestStatus.Approved;
    }

    public void Cancel()
    {
        if (Status != ClassRequestStatus.Pending)
            throw new InvalidOperationException("Só pedidos pendentes podem ser cancelados.");
        Status = ClassRequestStatus.Canceled;
    }

    // util
    public DateTime ProposedEndUtc => ProposedStartUtc.AddMinutes(DurationMinutes);

    public static bool Overlaps(DateTime aStartUtc, int aMin, DateTime bStartUtc, int bMin)
    {
        var aEnd = aStartUtc.AddMinutes(aMin);
        var bEnd = bStartUtc.AddMinutes(bMin);
        return aStartUtc < bEnd && bStartUtc < aEnd;
    }
}
