using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Classes;
using AlmaApp.WebApi.Common.Auth;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class ClassesService : IClassesService
{
    private readonly AppDbContext _db;
    private readonly IUserContext _userContext;

    public ClassesService(AppDbContext db, IUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<ServiceResult<PagedResult<ClassListItemDto>>> SearchAsync(
        Guid? clientId,
        Guid? staffId,
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

        var query = _db.Classes.AsNoTracking();

        if (clientId is Guid cid)
        {
            query = query.Where(x => x.ClientId == cid);
        }

        if (staffId is Guid sid)
        {
            query = query.Where(x => x.StaffId == sid);
        }

        if (roomId is Guid rid)
        {
            query = query.Where(x => x.RoomId == rid);
        }

        if (from is DateTime fromValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromValue, DateTimeKind.Utc);
            query = query.Where(x => x.StartUtc >= fromUtc);
        }

        if (to is DateTime toValue)
        {
            var toUtc = DateTime.SpecifyKind(toValue, DateTimeKind.Utc);
            query = query.Where(x => x.StartUtc < toUtc);
        }

        if (status is int st)
        {
            query = query.Where(x => (int)x.Status == st);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.StartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ClassListItemDto(
                x.Id,
                x.ClientId,
                x.StaffId,
                x.RoomId,
                x.StartUtc,
                x.DurationMinutes,
                (int)x.Status))
            .ToListAsync(ct);

        var paged = PagedResult<ClassListItemDto>.Create(items, page, pageSize, total);
        return ServiceResult<PagedResult<ClassListItemDto>>.Ok(paged);
    }

    public async Task<ServiceResult<ClassResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var cls = await _db.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (cls is null)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(404, "Class not found"));
        }

        return ServiceResult<ClassResponse>.Ok(MapToResponse(cls));
    }

    public async Task<ServiceResult<ClassResponse>> CreateAsync(CreateClassRequestDto request, CancellationToken ct)
    {
        if (!await ValidateParticipantsAsync(request.ClientId, request.StaffId, request.RoomId, ct))
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(400, "ClientId/StaffId/RoomId inválido(s)."));
        }

        var start = DateTime.SpecifyKind(request.StartUtc, DateTimeKind.Utc);
        var end = start.AddMinutes(request.DurationMinutes);

        var staffConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.StaffId == request.StaffId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes), ct);
        if (staffConflict)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(409, "Conflito de agenda (Staff)"));
        }

        var roomConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == request.RoomId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes), ct);
        if (roomConflict)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(409, "Conflito de agenda (Room)"));
        }

        var uid = _userContext.Uid ?? throw new InvalidOperationException("Missing user id.");

        Class cls;
        try
        {
            cls = new Class(request.ClientId, request.StaffId, request.RoomId, start, request.DurationMinutes, uid);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(400, ex.Message));
        }

        await _db.Classes.AddAsync(cls, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ClassResponse>.Ok(MapToResponse(cls));
    }

    public async Task<ServiceResult<ClassResponse>> CreateFromRequestAsync(
        Guid requestId,
        CreateClassFromRequestDto request,
        CancellationToken ct)
    {
        var classRequest = await _db.ClassRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (classRequest is null)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(404, "Class request not found"));
        }

        if (classRequest.Status != ClassRequestStatus.Pending)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(409, "Só pedidos pendentes podem originar uma aula."));
        }

        if (!await ValidateParticipantsAsync(classRequest.ClientId, classRequest.StaffId, request.RoomId, ct))
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(400, "Client/Staff/Room inválido(s)."));
        }

        var start = classRequest.ProposedStartUtc;
        var end = start.AddMinutes(classRequest.DurationMinutes);

        var staffConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.StaffId == classRequest.StaffId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes), ct);
        if (staffConflict)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(409, "Conflito de agenda (Staff)"));
        }

        var roomConflict = await _db.Classes
            .Where(x => x.Status == ClassStatus.Scheduled && x.RoomId == request.RoomId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes), ct);
        if (roomConflict)
        {
            return ServiceResult<ClassResponse>.Fail(new ServiceError(409, "Conflito de agenda (Room)"));
        }

        var uid = _userContext.Uid ?? throw new InvalidOperationException("Missing user id.");

        var cls = new Class(
            classRequest.ClientId,
            classRequest.StaffId,
            request.RoomId,
            start,
            classRequest.DurationMinutes,
            uid,
            classRequest.Id);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            _db.Classes.Add(cls);
            classRequest.Approve();
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return ServiceResult<ClassResponse>.Ok(MapToResponse(cls));
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, UpdateClassRequestDto request, CancellationToken ct)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cls is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Class not found"));
        }

        if (cls.Status != ClassStatus.Scheduled)
        {
            return ServiceResult.Fail(new ServiceError(409, "Só aulas agendadas podem ser editadas."));
        }

        var start = DateTime.SpecifyKind(request.StartUtc, DateTimeKind.Utc);
        var end = start.AddMinutes(request.DurationMinutes);

        var staffConflict = await _db.Classes
            .Where(x => x.Id != id && x.Status == ClassStatus.Scheduled && x.StaffId == cls.StaffId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes), ct);
        if (staffConflict)
        {
            return ServiceResult.Fail(new ServiceError(409, "Conflito de agenda (Staff)"));
        }

        var roomConflict = await _db.Classes
            .Where(x => x.Id != id && x.Status == ClassStatus.Scheduled && x.RoomId == request.RoomId)
            .AnyAsync(x => x.StartUtc < end && start < x.StartUtc.AddMinutes(x.DurationMinutes), ct);
        if (roomConflict)
        {
            return ServiceResult.Fail(new ServiceError(409, "Conflito de agenda (Room)"));
        }

        try
        {
            cls.Reschedule(start, request.DurationMinutes, request.RoomId);
        }
        catch (ArgumentOutOfRangeException ex)
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

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cls is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Class not found"));
        }

        cls.Cancel();
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CompleteAsync(Guid id, CancellationToken ct)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cls is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Class not found"));
        }

        try
        {
            cls.Complete();
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult.Fail(new ServiceError(409, ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private async Task<bool> ValidateParticipantsAsync(Guid clientId, Guid staffId, Guid roomId, CancellationToken ct)
    {
        var clientExists = await _db.Clients.AnyAsync(x => x.Id == clientId, ct);
        if (!clientExists)
        {
            return false;
        }

        var staffExists = await _db.Staff.AnyAsync(x => x.Id == staffId, ct);
        if (!staffExists)
        {
            return false;
        }

        var roomExists = await _db.Rooms.AnyAsync(x => x.Id == roomId, ct);
        return roomExists;
    }

    private static ClassResponse MapToResponse(Class cls)
        => new(
            cls.Id,
            cls.ClientId,
            cls.StaffId,
            cls.RoomId,
            cls.StartUtc,
            cls.DurationMinutes,
            (int)cls.Status,
            cls.LinkedRequestId,
            cls.CreatedByUid,
            cls.CreatedAtUtc);
}
