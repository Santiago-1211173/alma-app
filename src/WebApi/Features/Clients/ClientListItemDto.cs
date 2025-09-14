using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Features.Clients;

public sealed record ClientListItemDto(Guid Id, string FirstName, string LastName, string Email, string Phone);
