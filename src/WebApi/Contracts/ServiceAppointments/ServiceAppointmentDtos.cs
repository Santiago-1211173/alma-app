using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.ServiceAppointments;

public sealed record ServiceAppointmentListItemDto(
    Guid Id,
    Guid ClientId,
    Guid? SecondClientId,
    Guid StaffId,
    Guid RoomId,
    int ServiceType,
    DateTime StartLocal,
    DateTime StartUtc,
    int DurationMinutes,
    int Status
    );

public sealed record ServiceAppointmentResponse(
    Guid Id,
    Guid ClientId,
    Guid? SecondClientId,
    Guid StaffId,
    Guid RoomId,
    int ServiceType,
    DateTime StartLocal,
    DateTime StartUtc,
    int DurationMinutes,
    int Status,
    string CreatedByUid,
    DateTime CreatedAtUtc);
/// 
public sealed class CreateServiceAppointmentRequestDto
{
    [Required] public Guid ClientId { get; set; }
    public Guid? SecondClientId { get; set; }
    [Required] public Guid StaffId { get; set; }
    [Required] public Guid RoomId { get; set; }
    [Required] public int ServiceType { get; set; }
    [Required] public DateTime Start { get; set; } // hora local
    [Range(15, 240)] public int DurationMinutes { get; set; } = 60;
}

/// <summary>
/// Representa os dados para atualizar uma marcação existente.  Todos os
/// campos são obrigatórios porque a marcação é sobrescrita (excepto o
/// SecondClientId que continua opcional).  As validações são iguais às
/// utilizadas na criação.
/// </summary>
public sealed class UpdateServiceAppointmentRequestDto
{
    [Required] public Guid ClientId { get; set; }
    public Guid? SecondClientId { get; set; }
    [Required] public Guid StaffId { get; set; }
    [Required] public Guid RoomId { get; set; }
    [Required] public int ServiceType { get; set; }
    [Required] public DateTime Start { get; set; } // hora local
    [Range(15, 240)] public int DurationMinutes { get; set; } = 60;
}