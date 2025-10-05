using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Rooms;

namespace AlmaApp.WebApi.Services;

public interface IRoomsService
{
    Task<ServiceResult<PagedResult<RoomListItemDto>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        bool? onlyActive,
        CancellationToken ct);

    Task<ServiceResult<RoomResponse>> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<RoomResponse>> CreateAsync(CreateRoomRequest request, CancellationToken ct);

    Task<ServiceResult> UpdateAsync(Guid id, UpdateRoomRequest request, CancellationToken ct);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct);
}
