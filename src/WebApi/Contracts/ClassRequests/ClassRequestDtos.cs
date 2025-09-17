using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.ClassRequests;

public sealed record ClassRequestListItemDto(
    Guid Id, Guid ClientId, Guid StaffId, DateTime ProposedStartUtc, int DurationMinutes, string? Notes, int Status);

public sealed record ClassRequestResponse(
    Guid Id, Guid ClientId, Guid StaffId, DateTime ProposedStartUtc, int DurationMinutes, string? Notes,
    int Status, string CreatedByUid, DateTime CreatedAtUtc);

public sealed class CreateClassRequest
{
    [Required] public Guid ClientId { get; set; }
    [Required] public Guid StaffId  { get; set; }
    [Required] public DateTime ProposedStartUtc { get; set; } // enviar em UTC (ex.: 2025-09-16T10:00:00Z)
    [Range(15, 180)] public int DurationMinutes { get; set; } = 60;
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class UpdateClassRequest
{
    [Required] public Guid ClientId { get; set; }
    [Required] public Guid StaffId  { get; set; }
    [Required] public DateTime ProposedStartUtc { get; set; }
    [Range(15, 180)] public int DurationMinutes { get; set; } = 60;
    [StringLength(500)] public string? Notes { get; set; }
}
