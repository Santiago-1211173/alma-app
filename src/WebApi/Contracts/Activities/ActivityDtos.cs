using System;
using System.ComponentModel.DataAnnotations;
using AlmaApp.Domain.Activities;

namespace AlmaApp.WebApi.Contracts.Activities
{
    public sealed record ActivityListItemDto(
        Guid Id,
        Guid RoomId,
        Guid InstructorId,
        ActivityCategory Category,
        string Title,
        DateTime StartLocal,
        int DurationMinutes,
        int MaxParticipants,
        int AvailableSlots,
        ActivityStatus Status
    );

    public sealed record ActivityResponse(
        Guid Id,
        Guid RoomId,
        Guid InstructorId,
        ActivityCategory Category,
        string Title,
        string? Description,
        DateTime StartLocal,
        int DurationMinutes,
        int MaxParticipants,
        int AvailableSlots,
        ActivityStatus Status,
        string? CreatedByUid,
        DateTime CreatedAtLocal
    );

    public sealed class CreateActivityRequestDto
    {
        [Required] public Guid RoomId { get; set; }
        [Required] public Guid InstructorId { get; set; }
        [Required] public ActivityCategory Category { get; set; }
        [Required, MaxLength(200)] public string Title { get; set; } = default!;
        [MaxLength(2000)] public string? Description { get; set; }
        [Required] public DateTime StartLocal { get; set; }
        [Range(1, 24 * 60)] public int DurationMinutes { get; set; }
        [Range(1, 500)] public int MaxParticipants { get; set; }
    }

    public sealed class UpdateActivityRequestDto
    {
        [Required] public Guid RoomId { get; set; }
        [Required] public Guid InstructorId { get; set; }
        [Required] public ActivityCategory Category { get; set; }
        [Required, MaxLength(200)] public string Title { get; set; } = default!;
        [MaxLength(2000)] public string? Description { get; set; }
        [Required] public DateTime StartLocal { get; set; }
        [Range(1, 24 * 60)] public int DurationMinutes { get; set; }
        [Range(1, 500)] public int MaxParticipants { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public sealed class JoinActivityRequestDto
    {
        [Required] public Guid ClientId { get; set; }
    }
}
