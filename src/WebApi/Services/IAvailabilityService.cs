using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Availability;

namespace AlmaApp.WebApi.Services;

public interface IAvailabilityService
{
    Task<ServiceResult<IEnumerable<StaffAvailabilityRuleDto>>> GetRulesAsync(Guid staffId, CancellationToken ct);

    Task<ServiceResult<StaffAvailabilityRuleDto>> CreateRuleAsync(Guid staffId, UpsertStaffAvailabilityRuleDto request, CancellationToken ct);

    Task<ServiceResult> UpdateRuleAsync(Guid id, UpsertStaffAvailabilityRuleDto request, CancellationToken ct);

    Task<ServiceResult> DeleteRuleAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<IEnumerable<StaffTimeOffDto>>> GetTimeOffAsync(Guid staffId, CancellationToken ct);

    Task<ServiceResult<StaffTimeOffDto>> CreateTimeOffAsync(Guid staffId, UpsertStaffTimeOffDto request, CancellationToken ct);

    Task<ServiceResult> UpdateTimeOffAsync(Guid id, UpsertStaffTimeOffDto request, CancellationToken ct);

    Task<ServiceResult> DeleteTimeOffAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<IEnumerable<RoomClosureDto>>> GetRoomClosuresAsync(Guid roomId, CancellationToken ct);

    Task<ServiceResult<RoomClosureDto>> CreateRoomClosureAsync(Guid roomId, UpsertRoomClosureDto request, CancellationToken ct);

    Task<ServiceResult> UpdateRoomClosureAsync(Guid id, UpsertRoomClosureDto request, CancellationToken ct);

    Task<ServiceResult> DeleteRoomClosureAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<CheckAvailabilityResponse>> CheckAvailabilityAsync(CheckAvailabilityRequest request, CancellationToken ct);

    Task<ServiceResult<IEnumerable<SlotDto>>> FindSlotsAsync(FindSlotsRequest request, CancellationToken ct);
}
