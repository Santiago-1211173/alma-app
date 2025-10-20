using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.GroupClasses;
using AlmaApp.Domain.GroupClasses;

namespace AlmaApp.WebApi.Services
{
    public interface IGroupClassService
    {
        Task<PagedResult<GroupClassListItemDto>> SearchAsync(
            GroupClassCategory? category, Guid? instructorId, Guid? roomId,
            DateTime? fromLocal, DateTime? toLocal, int page, int pageSize,
            CancellationToken ct);

        Task<GroupClassResponse?> GetByIdAsync(Guid id, CancellationToken ct);

        Task<GroupClassResponse> CreateAsync(CreateGroupClassRequestDto req, string? createdByUid, CancellationToken ct);
        Task<GroupClassResponse> UpdateAsync(Guid id, UpdateGroupClassRequestDto req, CancellationToken ct);

        Task CancelAsync(Guid id, CancellationToken ct);
        Task CompleteAsync(Guid id, CancellationToken ct);

        Task JoinAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct);
        Task LeaveAsync(Guid id, Guid clientId, DateTime nowLocal, CancellationToken ct);
    }
}
