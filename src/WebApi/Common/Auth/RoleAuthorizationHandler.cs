using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using Microsoft.AspNetCore.Authorization;

namespace AlmaApp.WebApi.Common.Auth;

public class RoleAuthorizationHandler(IUserContext user) : AuthorizationHandler<RoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        if (await user.IsInRoleAsync(requirement.Role))
            context.Succeed(requirement);
    }
}
