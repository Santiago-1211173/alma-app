using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Activities;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.Activities;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class ActivitiesService : IActivitiesService
{
    private readonly AppDbContext _db;
    private readonly IUserContext _userContext;

    public ActivitiesService(AppDbContext db, IUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<ServiceResult<PagedResult<ActivityListItemDto>>> SearchAsync(
        Guid? roomId,
        DateTime? from,
        DateTime? to,
        int? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var query = _db.Activities.AsNoTracking();

        if (roomId.HasValue)
        {
            query = query.Where(x => x.RoomId == roomId.Value);
        }

        if (from.HasValue)
        {
            var fromUtc = AppTime.ToUtcFromLocal(from.Value);
            query = query.Where(x => x.StartUtc >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = AppTime.ToUtcFromLocal(to.Value);
            query = query.Where(x => x.StartUtc < toUtc);
        }

        if (status.HasValue)
        {
            query = query.Where(x => (int)x.Status == status.Value);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.StartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ActivityListItemDto(
                x.Id,
                x.RoomId,
                x.Title,
                AppTime.ToLocalFromUtc(x.StartUtc),
                x.DurationMinutes,
                (int)x.Status))
            .ToListAsync(ct);

        var paged = PagedResult<ActivityListItemDto>.Create(items, page, pageSize, total);
        return ServiceResult<PagedResult<ActivityListItemDto>>.Ok(paged);
    }

    public async Task<ServiceResult<ActivityResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var activity = await _db.Activities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (activity is null)
        {
            return ServiceResult<ActivityResponse>.Fail(new ServiceError(404, "Activity not found"));
        }

        return ServiceResult<ActivityResponse>.Ok(MapToResponse(activity));
    }

    public async Task<ServiceResult<ActivityResponse>> CreateAsync(CreateActivityRequestDto request, CancellationToken ct)
    {
        var roomExists = await _db.Rooms.AnyAsync(x => x.Id == request.RoomId, ct);
        if (!roomExists)
        {
            return ServiceResult<ActivityResponse>.Fail(new ServiceError(400, "RoomId invalido."));
        }

        var startLocal = request.Start;
        var startUtc = AppTime.ToUtcFromLocal(startLocal);
        var endUtc = startUtc.AddMinutes(request.DurationMinutes);

        var hasActivityConflict = await _db.Activities
            .Where(x => x.Status == ActivityStatus.Scheduled && x.RoomId == request.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes), ct);

        if (hasActivityConflict)
        {
            return ServiceResult<ActivityResponse>.Fail(new ServiceError(409, "Conflito de agenda (Activity/Room)"));
        }

        var hasClassConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == request.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes), ct);

        if (hasClassConflict)
        {
            return ServiceResult<ActivityResponse>.Fail(new ServiceError(409, "Conflito de agenda (Class/Room)"));
        }

        var uid = _userContext.Uid ?? throw new InvalidOperationException("Missing user id.");

        Activity activity;
        try
        {
            activity = new Activity(
                title: request.Title,
                description: request.Description,
                roomId: request.RoomId,
                startUtc: startUtc,
                durationMinutes: request.DurationMinutes,
                createdByUid: uid);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ServiceResult<ActivityResponse>.Fail(new ServiceError(400, ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<ActivityResponse>.Fail(new ServiceError(400, ex.Message));
        }

        await _db.Activities.AddAsync(activity, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ActivityResponse>.Ok(MapToResponse(activity));
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, UpdateActivityRequestDto request, CancellationToken ct)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (activity is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Activity not found"));
        }

        var roomExists = await _db.Rooms.AnyAsync(x => x.Id == request.RoomId, ct);
        if (!roomExists)
        {
            return ServiceResult.Fail(new ServiceError(400, "RoomId invalido."));
        }

        var startLocal = request.Start;
        var startUtc = AppTime.ToUtcFromLocal(startLocal);
        var endUtc = startUtc.AddMinutes(request.DurationMinutes);

        var hasActivityConflict = await _db.Activities
            .Where(x => x.Id != id && x.Status == ActivityStatus.Scheduled && x.RoomId == request.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes), ct);

        if (hasActivityConflict)
        {
            return ServiceResult.Fail(new ServiceError(409, "Conflito de agenda (Activity/Room)"));
        }

        var hasClassConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == request.RoomId)
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.StartUtc.AddMinutes(x.DurationMinutes), ct);

        if (hasClassConflict)
        {
            return ServiceResult.Fail(new ServiceError(409, "Conflito de agenda (Class/Room)"));
        }

        try
        {
            activity.Update(
                title: request.Title,
                description: request.Description,
                startUtc: startUtc,
                durationMinutes: request.DurationMinutes,
                roomId: request.RoomId);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ServiceResult.Fail(new ServiceError(400, ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ServiceResult.Fail(new ServiceError(400, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult.Fail(new ServiceError(409, ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelAsync(Guid id, CancellationToken ct)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (activity is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Activity not found"));
        }

        activity.Cancel();
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CompleteAsync(Guid id, CancellationToken ct)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (activity is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Activity not found"));
        }

        try
        {
            activity.Complete();
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult.Fail(new ServiceError(409, ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static ActivityResponse MapToResponse(Activity activity)
        => new(
            activity.Id,
            activity.RoomId,
            activity.Title,
            activity.Description,
            AppTime.ToLocalFromUtc(activity.StartUtc),
            activity.StartUtc,
            activity.DurationMinutes,
            (int)activity.Status,
            activity.CreatedByUid,
            activity.CreatedAtUtc);
}
