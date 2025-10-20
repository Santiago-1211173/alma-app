using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Activities;
using AlmaApp.Domain.Activities;

namespace AlmaApp.WebApi.Services
{
    public interface IActivityService
    {
        Task<PagedResult<ActivityListItemDto>> SearchAsync(
            Guid? roomId, Guid? instructorId, ActivityCategory? category,
            DateTime? fromLocal, DateTime? toLocal, ActivityStatus? status,
            int page, int pageSize, CancellationToken ct);

        Task<ActivityResponse?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<ActivityResponse> CreateAsync(CreateActivityRequestDto dto, string? createdByUid, CancellationToken ct);
        Task<ActivityResponse> UpdateAsync(Guid id, UpdateActivityRequestDto dto, CancellationToken ct);
        Task CancelAsync(Guid id, CancellationToken ct);
        Task CompleteAsync(Guid id, CancellationToken ct);
        Task JoinAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct);
        Task LeaveAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct);
    }
}
