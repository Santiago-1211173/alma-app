using System;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Staff;

public sealed record CreateAdminStaffRequest(
    [property: Required] string FirstName,
    [property: Required] string LastName,
    [property: Required, EmailAddress] string Email,
    [property: Required] string Phone,
    [property: Required] string StaffNumber,
    string? Speciality
);

public sealed record AdminStaffDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string StaffNumber,
    string? Speciality,
    string? FirebaseUid
);

public sealed record LinkFirebaseRequest([property: Required] string Uid);
