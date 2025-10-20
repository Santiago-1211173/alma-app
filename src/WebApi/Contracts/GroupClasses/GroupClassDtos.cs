using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.ComponentModel.DataAnnotations;
using AlmaApp.Domain.GroupClasses;

namespace AlmaApp.WebApi.Contracts.GroupClasses
{
    public sealed record GroupClassListItemDto(
        Guid Id,
        GroupClassCategory Category,
        string? Title,
        Guid InstructorId,
        Guid RoomId,
        DateTime StartLocal,
        int DurationMinutes,
        int MaxParticipants,
        int AvailableSlots,
        GroupClassStatus Status
    );

    public sealed record GroupClassResponse(
        Guid Id,
        GroupClassCategory Category,
        string? Title,
        Guid InstructorId,
        Guid RoomId,
        DateTime StartLocal,
        int DurationMinutes,
        int MaxParticipants,
        int AvailableSlots,
        GroupClassStatus Status,
        int ActiveParticipants
    );

    public sealed class CreateGroupClassRequestDto
    {
        [Required] public GroupClassCategory Category { get; set; }
        [MaxLength(200)] public string? Title { get; set; }
        [Required] public Guid InstructorId { get; set; }
        [Required] public Guid RoomId { get; set; }
        [Required] public DateTime StartLocal { get; set; } // Europe/Lisbon
        [Range(1, 24 * 60)] public int DurationMinutes { get; set; }
        [Range(1, 500)] public int MaxParticipants { get; set; }
    }

    public sealed class UpdateGroupClassRequestDto
    {
        [Required] public GroupClassCategory Category { get; set; }
        [MaxLength(200)] public string? Title { get; set; }
        [Required] public Guid InstructorId { get; set; }
        [Required] public Guid RoomId { get; set; }
        [Required] public DateTime StartLocal { get; set; }
        [Range(1, 24 * 60)] public int DurationMinutes { get; set; }
        [Range(1, 500)] public int MaxParticipants { get; set; }
        public byte[]? RowVersion { get; set; } // para concorrência
    }

    public sealed class JoinGroupClassRequestDto
    {
        [Required] public Guid ClientId { get; set; }
    }
}
