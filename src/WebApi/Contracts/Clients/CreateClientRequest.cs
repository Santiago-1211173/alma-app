using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Clients;

public sealed class CreateClientRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = default!;
    [Required, StringLength(100)] public string LastName  { get; set; } = default!;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = default!;
    [Required, StringLength(20, MinimumLength = 8)] public string CitizenCardNumber { get; set; } = default!;
    [Phone, StringLength(30)] public string? Phone { get; set; }
    [DataType(DataType.Date)] public DateOnly? BirthDate { get; set; }
}
