using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Staff;

namespace AlmaApp.WebApi.Services;

public interface IAdminStaffService
{
    Task<ServiceResult<AdminStaffDto>> CreateAsync(CreateAdminStaffRequest request, CancellationToken ct);

    Task<ServiceResult<AdminStaffDto>> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ServiceResult> LinkFirebaseAsync(Guid id, LinkFirebaseRequest request, CancellationToken ct);
}
