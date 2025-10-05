using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Rooms;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Rooms;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class RoomsService : IRoomsService
{
    private readonly AppDbContext _db;

    public RoomsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<PagedResult<RoomListItemDto>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        bool? onlyActive,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var rooms = _db.Rooms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            rooms = rooms.Where(r => EF.Functions.Like(r.Name, $"%{term}%"));
        }

        if (onlyActive is true)
        {
            rooms = rooms.Where(r => r.IsActive);
        }

        var total = await rooms.CountAsync(ct);

        var items = await rooms
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoomListItemDto(r.Id, r.Name, r.Capacity, r.IsActive))
            .ToListAsync(ct);

        var paged = PagedResult<RoomListItemDto>.Create(items, page, pageSize, total);
        return ServiceResult<PagedResult<RoomListItemDto>>.Ok(paged);
    }

    public async Task<ServiceResult<RoomResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (room is null)
        {
            return ServiceResult<RoomResponse>.Fail(new ServiceError(404, "Room not found"));
        }

        return ServiceResult<RoomResponse>.Ok(MapToResponse(room));
    }

    public async Task<ServiceResult<RoomResponse>> CreateAsync(CreateRoomRequest request, CancellationToken ct)
    {
        Room room;
        try
        {
            room = new Room(request.Name, request.Capacity, request.IsActive);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<RoomResponse>.Fail(new ServiceError(400, ex.Message));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ServiceResult<RoomResponse>.Fail(new ServiceError(400, ex.Message));
        }

        await _db.Rooms.AddAsync(room, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<RoomResponse>.Ok(MapToResponse(room));
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, UpdateRoomRequest request, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (room is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Room not found"));
        }

        try
        {
            room.Update(request.Name, request.Capacity, request.IsActive);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult.Fail(new ServiceError(400, ex.Message));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ServiceResult.Fail(new ServiceError(400, ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (room is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Room not found"));
        }

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static RoomResponse MapToResponse(Room room)
        => new(room.Id, room.Name, room.Capacity, room.IsActive, room.CreatedAtUtc);
}
