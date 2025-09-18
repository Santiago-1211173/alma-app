using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AlmaApp.Domain.Auth;
using Microsoft.AspNetCore.Authorization;

namespace AlmaApp.WebApi.Common.Auth;

public sealed class RolesAnyAuthorizationHandler(IUserContext user)
    : AuthorizationHandler<RolesAnyRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RolesAnyRequirement requirement)
    {
        foreach (var role in requirement.Roles)
        {
            if (await user.IsInRoleAsync(role))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
