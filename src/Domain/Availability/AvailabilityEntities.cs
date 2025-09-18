using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlmaApp.Domain.Availability;

/// <summary>
/// Regra semanal recorrente de disponibilidade do Staff (em UTC).
/// Ex.: Segunda 09:00–18:00.
/// </summary>
public sealed class StaffAvailabilityRule
{
    [Key] public Guid Id { get; init; } = Guid.NewGuid();

    [Required] public Guid StaffId { get; init; }

    /// <summary>0=Sunday … 6=Saturday (igual enum DayOfWeek)</summary>
    [Range(0, 6)] public int DayOfWeek { get; init; }

    /// <summary>Hora de início (UTC) para o dia.</summary>
    [Required] public TimeSpan StartTimeUtc { get; init; }

    /// <summary>Hora de fim (UTC) para o dia (deve ser > StartTime).</summary>
    [Required] public TimeSpan EndTimeUtc { get; init; }

    public bool Active { get; init; } = true;
}

/// <summary>Folga/ausência pontual do Staff (UTC).</summary>
public sealed class StaffTimeOff
{
    [Key] public Guid Id { get; init; } = Guid.NewGuid();

    [Required] public Guid StaffId { get; init; }

    [Required] public DateTime FromUtc { get; init; }  // inclusive
    [Required] public DateTime ToUtc   { get; init; }  // exclusive

    [MaxLength(400)] public string? Reason { get; init; }
}

/// <summary>Encerramento pontual de uma Sala (UTC).</summary>
public sealed class RoomClosure
{
    [Key] public Guid Id { get; init; } = Guid.NewGuid();

    [Required] public Guid RoomId { get; init; }

    [Required] public DateTime FromUtc { get; init; }  // inclusive
    [Required] public DateTime ToUtc   { get; init; }  // exclusive

    [MaxLength(400)] public string? Reason { get; init; }
}
