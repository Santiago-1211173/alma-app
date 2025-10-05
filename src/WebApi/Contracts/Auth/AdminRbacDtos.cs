using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AlmaApp.Domain.Auth;

namespace AlmaApp.WebApi.Contracts.Auth;

public sealed record AssignRoleRequest([property: Required] RoleName Role);

public sealed record UserRolesResponse(string UserUid, IEnumerable<RoleName> Roles);
