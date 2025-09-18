using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AlmaApp.Domain.Auth;
using Microsoft.AspNetCore.Authorization;

namespace AlmaApp.WebApi.Common.Auth;

public sealed class RolesAnyRequirement : IAuthorizationRequirement
{
    public IReadOnlyCollection<RoleName> Roles { get; }

    public RolesAnyRequirement(params RoleName[] roles)
        => Roles = roles;
}
