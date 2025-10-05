using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Auth;

namespace AlmaApp.WebApi.Services;

public interface IAdminRbacService
{
    Task<ServiceResult<UserRolesResponse>> GetRolesAsync(string uid, CancellationToken ct);

    Task<ServiceResult> AssignRoleAsync(string uid, AssignRoleRequest request, CancellationToken ct);

    Task<ServiceResult> RemoveRoleAsync(string uid, RoleName role, CancellationToken ct);
}
