using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Contracts.Clients;

public sealed record ClientResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string CitizenCardNumber,
    string? Phone,
    DateOnly? BirthDate,
    DateTime CreatedAtUtc
);
