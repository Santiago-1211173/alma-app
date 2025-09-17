using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Classes;

public sealed record ClassListItemDto(
    Guid Id, Guid ClientId, Guid StaffId, Guid RoomId,
    DateTime StartUtc, int DurationMinutes, int Status);

public sealed record ClassResponse(
    Guid Id, Guid ClientId, Guid StaffId, Guid RoomId,
    DateTime StartUtc, int DurationMinutes, int Status,
    Guid? LinkedRequestId, string CreatedByUid, DateTime CreatedAtUtc);

public sealed class CreateClassRequestDto
{
    [Required] public Guid ClientId { get; set; }
    [Required] public Guid StaffId  { get; set; }
    [Required] public Guid RoomId   { get; set; }
    [Required] public DateTime StartUtc { get; set; }
    [Range(15,180)] public int DurationMinutes { get; set; } = 60;
}

public sealed class CreateClassFromRequestDto
{
    [Required] public Guid RoomId { get; set; }
}

public sealed class UpdateClassRequestDto
{
    [Required] public Guid RoomId { get; set; }
    [Required] public DateTime StartUtc { get; set; }
    [Range(15,180)] public int DurationMinutes { get; set; } = 60;
}
