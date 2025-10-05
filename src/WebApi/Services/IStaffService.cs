using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Staff;

namespace AlmaApp.WebApi.Services;

public interface IStaffService
{
    Task<ServiceResult<PagedResult<StaffListItemDto>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<ServiceResult<StaffResponse>> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<StaffResponse>> CreateAsync(CreateStaffRequest request, CancellationToken ct);

    Task<ServiceResult> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken ct);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct);
}
