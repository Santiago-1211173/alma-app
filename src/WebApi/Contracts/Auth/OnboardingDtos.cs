using System;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Auth;

public sealed record CreateClientSelf(
    [property: Required] string FirstName,
    [property: Required] string LastName,
    [property: Required] string CitizenCardNumber,
    [property: Required, EmailAddress] string Email,
    string? Phone,
    DateOnly? BirthDate);

public sealed record ClaimStaffBody(
    [property: Required] string StaffNumber,
    string? Email);

public sealed record OnboardingResult(string Message, Guid? StaffId = null, Guid? ClientId = null);
