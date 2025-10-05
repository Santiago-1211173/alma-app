using System;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Activities;

/// <summary>
/// DTO minimal para listagens (hora local devolvida para UX).
/// </summary>
public sealed record ActivityListItemDto(
    Guid Id, Guid RoomId, string Title, DateTime Start /*local*/, int DurationMinutes, int Status);

/// <summary>
/// Response completo. Inclui ambas as horas para clareza (local e UTC).
/// </summary>
public sealed record ActivityResponse(
    Guid Id,
    Guid RoomId,
    string Title,
    string? Description,
    DateTime StartLocal,
    DateTime StartUtc,
    int DurationMinutes,
    int Status,
    string CreatedByUid,
    DateTime CreatedAtUtc);

/// <summary>
/// Requests recebem hora em **hora de Portugal (local)**, sem 'Z'.
/// Exemplo: "2025-09-30T18:00:00"
/// </summary>
public sealed class CreateActivityRequestDto
{
    [Required] public Guid RoomId { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = default!;
    [MaxLength(2000)] public string? Description { get; set; }
    [Required] public DateTime Start /*local*/ { get; set; }
    [Range(15,240)] public int DurationMinutes { get; set; } = 60;
}

public sealed class UpdateActivityRequestDto
{
    [Required] public Guid RoomId { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = default!;
    [MaxLength(2000)] public string? Description { get; set; }
    [Required] public DateTime Start /*local*/ { get; set; }
    [Range(15,240)] public int DurationMinutes { get; set; } = 60;
}
