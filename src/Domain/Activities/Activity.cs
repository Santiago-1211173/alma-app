using System;

namespace AlmaApp.Domain.Activities;

/// <summary>
/// Representa uma actividade extra aula definida pelo staff. As actividades têm
/// um título, uma descrição opcional, sala associada e hora de início/duração.
/// Não existe fluxo de pedido (request) para actividades; são marcadas de
/// forma directa e os clientes podem inscrever‑se externamente através da UI.
/// </summary>
public enum ActivityStatus { Scheduled = 0, Completed = 1, Canceled = 9 }

public class Activity
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid RoomId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public int DurationMinutes { get; private set; }
    public ActivityStatus Status { get; private set; }
    public string CreatedByUid { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    // EF requires a parameterless constructor
    private Activity() { }

    /// <summary>
    /// Cria uma nova actividade. A duração deve estar entre 15 e 180 minutos.
    /// </summary>
    public Activity(string title, string? description, Guid roomId,
                    DateTime startUtc, int durationMinutes, string createdByUid)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException(nameof(title));
        if (durationMinutes < 15 || durationMinutes > 180)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));

        Id = Guid.NewGuid();
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        RoomId = roomId;
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        DurationMinutes = durationMinutes;
        Status = ActivityStatus.Scheduled;
        CreatedByUid = createdByUid.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Actualiza os dados da actividade enquanto estiver agendada. Não permite
    /// editar actividades já concluídas ou canceladas.
    /// </summary>
    public void Update(string title, string? description, DateTime startUtc, int durationMinutes, Guid roomId)
    {
        if (Status != ActivityStatus.Scheduled)
            throw new InvalidOperationException("Só actividades agendadas podem ser editadas.");
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException(nameof(title));
        if (durationMinutes < 15 || durationMinutes > 180)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        DurationMinutes = durationMinutes;
        RoomId = roomId;
    }

    /// <summary>
    /// Cancela a actividade. Se já estiver cancelada, não faz nada.
    /// </summary>
    public void Cancel()
    {
        if (Status == ActivityStatus.Canceled) return;
        Status = ActivityStatus.Canceled;
    }

    /// <summary>
    /// Marca a actividade como concluída. Apenas actividades agendadas podem
    /// ser concluídas.
    /// </summary>
    public void Complete()
    {
        if (Status != ActivityStatus.Scheduled)
            throw new InvalidOperationException("Só actividades agendadas podem ser concluídas.");
        Status = ActivityStatus.Completed;
    }

    /// <summary>
    /// Data/hora de fim calculada a partir da duração.
    /// </summary>
    public DateTime EndUtc => StartUtc.AddMinutes(DurationMinutes);
}