using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.ClassRequests;

namespace AlmaApp.WebApi.Services;

public interface IClassRequestsService
{
    Task<ServiceResult<PagedResult<ClassRequestListItemDto>>> SearchAsync(
        Guid? clientId,
        Guid? staffId,
        Guid? roomId,
        DateTime? from,
        DateTime? to,
        int? status,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<ServiceResult<ClassRequestResponse>> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<Guid>> CreateForClientAsync(CreateClassRequestByStaff request, CancellationToken ct);

    Task<ServiceResult<IReadOnlyList<ClientClassRequestSummaryDto>>> GetMyClientRequestsAsync(CancellationToken ct);

    Task<ServiceResult> UpdateAsync(Guid id, UpdateClassRequest request, CancellationToken ct);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<ClassRequestApprovedResponse>> ApproveAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<ClassRequestResponse>> RejectAsync(Guid id, CancellationToken ct);
}
