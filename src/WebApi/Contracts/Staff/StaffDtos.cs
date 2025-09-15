using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Staff;

public sealed record StaffListItemDto(
    Guid Id, string FirstName, string LastName, string Email, string Phone, string StaffNumber, string? Speciality);

public sealed record StaffResponse(
    Guid Id, string FirstName, string LastName, string Email, string Phone, string StaffNumber, string? Speciality, DateTime CreatedAtUtc);

public sealed class CreateStaffRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = default!;
    [Required, StringLength(100)] public string LastName  { get; set; } = default!;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = default!;
    [Required, StringLength(30, MinimumLength = 6)] public string Phone { get; set; } = default!;
    [Required, StringLength(30)] public string StaffNumber { get; set; } = default!;
    [StringLength(100)] public string? Speciality { get; set; }
}

// Sem herança
public sealed class UpdateStaffRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = default!;
    [Required, StringLength(100)] public string LastName  { get; set; } = default!;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = default!;
    [Required, StringLength(30, MinimumLength = 6)] public string Phone { get; set; } = default!;
    [Required, StringLength(30)] public string StaffNumber { get; set; } = default!;
    [StringLength(100)] public string? Speciality { get; set; }
}
