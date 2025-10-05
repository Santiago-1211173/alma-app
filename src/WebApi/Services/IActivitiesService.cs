using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Activities;

namespace AlmaApp.WebApi.Services;

public interface IActivitiesService
{
    Task<ServiceResult<PagedResult<ActivityListItemDto>>> SearchAsync(
        Guid? roomId,
        DateTime? from,
        DateTime? to,
        int? status,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<ServiceResult<ActivityResponse>> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<ActivityResponse>> CreateAsync(CreateActivityRequestDto request, CancellationToken ct);

    Task<ServiceResult> UpdateAsync(Guid id, UpdateActivityRequestDto request, CancellationToken ct);

    Task<ServiceResult> CancelAsync(Guid id, CancellationToken ct);

    Task<ServiceResult> CompleteAsync(Guid id, CancellationToken ct);
}
