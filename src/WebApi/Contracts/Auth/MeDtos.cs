using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace AlmaApp.WebApi.Contracts.Auth;

public sealed record MeResponse(
    string Uid,
    string? Email,
    bool EmailVerified,
    Guid? ClientId,
    Guid? StaffId,
    string[] Roles);
