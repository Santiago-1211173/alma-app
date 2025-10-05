using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.ClassRequests;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class ClassRequestsService : IClassRequestsService
{
    private readonly AppDbContext _db;
    private readonly IUserContext _userContext;

    public ClassRequestsService(AppDbContext db, IUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<ServiceResult<PagedResult<ClassRequestListItemDto>>> SearchAsync(
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

        var query = _db.ClassRequests.AsNoTracking();

        if (clientId is Guid cid) query = query.Where(x => x.ClientId == cid);
        if (staffId is Guid sid) query = query.Where(x => x.StaffId == sid);
        if (roomId is Guid rid) query = query.Where(x => x.RoomId == rid);
        if (from is DateTime f)
        {
            var fromUtc = DateTime.SpecifyKind(f, DateTimeKind.Utc);
            query = query.Where(x => x.ProposedStartUtc >= fromUtc);
        }
        if (to is DateTime t)
        {
            var toUtc = DateTime.SpecifyKind(t, DateTimeKind.Utc);
            query = query.Where(x => x.ProposedStartUtc < toUtc);
        }
        if (status is int st)
        {
            query = query.Where(x => (int)x.Status == st);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.ProposedStartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ClassRequestListItemDto(
                x.Id,
                x.ClientId,
                x.StaffId,
                x.RoomId,
                x.ProposedStartUtc,
                x.DurationMinutes,
                x.Notes,
                (int)x.Status))
            .ToListAsync(ct);

        var paged = PagedResult<ClassRequestListItemDto>.Create(items, page, pageSize, total);
        return ServiceResult<PagedResult<ClassRequestListItemDto>>.Ok(paged);
    }

    public async Task<ServiceResult<ClassRequestResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var request = await _db.ClassRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
        {
            return ServiceResult<ClassRequestResponse>.Fail(new ServiceError(404, "Class request not found"));
        }

        return ServiceResult<ClassRequestResponse>.Ok(MapToResponse(request));
    }

    public async Task<ServiceResult<Guid>> CreateForClientAsync(CreateClassRequestByStaff request, CancellationToken ct)
    {
        var uid = _userContext.Uid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            return ServiceResult<Guid>.Fail(new ServiceError(403, "User not authenticated."));
        }

        var staff = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.FirebaseUid == uid, ct);
        if (staff is null)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(403, "Utilizador sem associação a Staff."));
        }

        Guid clientId;
        try
        {
            clientId = await ResolveClientIdAsync(request, ct);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(400, ex.Message));
        }

        var clientExists = await _db.Clients.AnyAsync(c => c.Id == clientId, ct);
        if (!clientExists)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(400, "ClientId inválido ou inexistente."));
        }

        if (request.RoomId == Guid.Empty)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(400, "roomId é obrigatório."));
        }

        var roomExists = await _db.Rooms.AsNoTracking().AnyAsync(r => r.Id == request.RoomId, ct);
        if (!roomExists)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(400, "roomId inválido ou inexistente."));
        }

        if (request.DurationMinutes < 15 || request.DurationMinutes > 180)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(400, "durationMinutes deve estar entre 15 e 180."));
        }

        var proposedStartUtc = DateTime.SpecifyKind(request.ProposedStartUtc, DateTimeKind.Utc);
        if (proposedStartUtc <= DateTime.UtcNow)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(400, "proposedStartUtc deve ser no futuro (UTC)."));
        }

        var duration = request.DurationMinutes;

        var conflictPendingStaff = await _db.ClassRequests
            .Where(c => c.StaffId == staff.Id && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, proposedStartUtc) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(proposedStartUtc, c.ProposedStartUtc) < duration,
                ct);
        if (conflictPendingStaff)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(409, "Já existe um pedido pendente para este staff no mesmo horário."));
        }

        var conflictClassesStaff = await _db.Classes
            .Where(k => k.StaffId == staff.Id && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, proposedStartUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(proposedStartUtc, k.StartUtc) < duration,
                ct);
        if (conflictClassesStaff)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(409, "Já existe uma aula agendada para este staff no mesmo horário."));
        }

        var conflictPendingRoom = await _db.ClassRequests
            .Where(c => c.RoomId == request.RoomId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, proposedStartUtc) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(proposedStartUtc, c.ProposedStartUtc) < duration,
                ct);
        if (conflictPendingRoom)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(409, "Já existe um pedido pendente para esta sala no mesmo horário."));
        }

        var conflictClassesRoom = await _db.Classes
            .Where(k => k.RoomId == request.RoomId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, proposedStartUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(proposedStartUtc, k.StartUtc) < duration,
                ct);
        if (conflictClassesRoom)
        {
            return ServiceResult<Guid>.Fail(new ServiceError(409, "Já existe uma aula agendada para esta sala no mesmo horário."));
        }

        var classRequest = new ClassRequest(
            clientId: clientId,
            staffId: staff.Id,
            roomId: request.RoomId,
            proposedStartUtc: proposedStartUtc,
            durationMinutes: request.DurationMinutes,
            notes: request.Notes,
            createdByUid: uid!);

        await _db.ClassRequests.AddAsync(classRequest, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<Guid>.Ok(classRequest.Id);
    }

    public async Task<ServiceResult<IReadOnlyList<ClientClassRequestSummaryDto>>> GetMyClientRequestsAsync(CancellationToken ct)
    {
        var uid = _userContext.Uid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            return ServiceResult<IReadOnlyList<ClientClassRequestSummaryDto>>.Fail(new ServiceError(403, "User not authenticated."));
        }

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.FirebaseUid == uid, ct);
        if (client is null)
        {
            return ServiceResult<IReadOnlyList<ClientClassRequestSummaryDto>>.Fail(new ServiceError(403, "Cliente não encontrado para o utilizador atual."));
        }

        var items = await _db.ClassRequests.AsNoTracking()
            .Where(c => c.ClientId == client.Id)
            .OrderByDescending(c => c.ProposedStartUtc)
            .Select(c => new ClientClassRequestSummaryDto(
                c.Id,
                c.StaffId,
                c.RoomId,
                c.ProposedStartUtc,
                c.DurationMinutes,
                (int)c.Status,
                c.Notes))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<ClientClassRequestSummaryDto>>.Ok(items);
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, UpdateClassRequest request, CancellationToken ct)
    {
        var classRequest = await _db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (classRequest is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Class request not found"));
        }

        if (classRequest.Status != ClassRequestStatus.Pending)
        {
            return ServiceResult.Fail(new ServiceError(409, "Só pedidos pendentes podem ser editados."));
        }

        if (!string.Equals(classRequest.CreatedByUid, _userContext.Uid, StringComparison.Ordinal))
        {
            return ServiceResult.Fail(new ServiceError(403, "Sem permissões para editar este pedido."));
        }

        if (request.DurationMinutes < 15 || request.DurationMinutes > 180)
        {
            return ServiceResult.Fail(new ServiceError(400, "durationMinutes deve estar entre 15 e 180."));
        }

        var startUtc = DateTime.SpecifyKind(request.ProposedStartUtc, DateTimeKind.Utc);
        if (startUtc <= DateTime.UtcNow)
        {
            return ServiceResult.Fail(new ServiceError(400, "proposedStartUtc deve ser no futuro (UTC)."));
        }

        var newRoomId = request.RoomId ?? classRequest.RoomId;

        var clientOk = await _db.Clients.AnyAsync(c => c.Id == request.ClientId, ct);
        var staffOk = await _db.Staff.AnyAsync(s => s.Id == request.StaffId, ct);
        var roomOk = await _db.Rooms.AnyAsync(r => r.Id == newRoomId, ct);
        if (!(clientOk && staffOk && roomOk))
        {
            return ServiceResult.Fail(new ServiceError(400, "ClientId/StaffId/RoomId inválido(s) ou inexistente(s)."));
        }

        var duration = request.DurationMinutes;

        var overlapPendingStaff = await _db.ClassRequests
            .Where(r => r.Id != id && r.Status == ClassRequestStatus.Pending && r.StaffId == request.StaffId)
            .AnyAsync(r =>
                EF.Functions.DateDiffMinute(r.ProposedStartUtc, startUtc) < r.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, r.ProposedStartUtc) < duration,
                ct);
        if (overlapPendingStaff)
        {
            return ServiceResult.Fail(new ServiceError(409, "Já existe um pedido pendente para este staff no mesmo horário."));
        }

        var conflictClassesStaff = await _db.Classes
            .Where(k => k.StaffId == request.StaffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < duration,
                ct);
        if (conflictClassesStaff)
        {
            return ServiceResult.Fail(new ServiceError(409, "Já existe uma aula agendada para este staff no mesmo horário."));
        }

        var overlapPendingRoom = await _db.ClassRequests
            .Where(c => c.Id != id && c.RoomId == newRoomId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, startUtc) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, c.ProposedStartUtc) < duration,
                ct);
        if (overlapPendingRoom)
        {
            return ServiceResult.Fail(new ServiceError(409, "Já existe um pedido pendente para esta sala no mesmo horário."));
        }

        var conflictClassesRoom = await _db.Classes
            .Where(k => k.RoomId == newRoomId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, startUtc) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(startUtc, k.StartUtc) < duration,
                ct);
        if (conflictClassesRoom)
        {
            return ServiceResult.Fail(new ServiceError(409, "Já existe uma aula agendada para esta sala no mesmo horário."));
        }

        classRequest.Update(request.ClientId, request.StaffId, newRoomId, startUtc, request.DurationMinutes, request.Notes);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var classRequest = await _db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (classRequest is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Class request not found"));
        }

        if (classRequest.Status != ClassRequestStatus.Pending)
        {
            return ServiceResult.Fail(new ServiceError(409, "Só pedidos pendentes podem ser cancelados."));
        }

        var isAdmin = await _userContext.IsInRoleAsync(RoleName.Admin, ct);
        if (!isAdmin && !string.Equals(classRequest.CreatedByUid, _userContext.Uid, StringComparison.Ordinal))
        {
            return ServiceResult.Fail(new ServiceError(403, "Sem permissões para cancelar este pedido."));
        }

        classRequest.Cancel();
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<ClassRequestApprovedResponse>> ApproveAsync(Guid id, CancellationToken ct)
    {
        var classRequest = await _db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (classRequest is null)
        {
            return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(404, "Class request not found"));
        }

        var isAdmin = await _userContext.IsInRoleAsync(RoleName.Admin, ct);
        if (!isAdmin)
        {
            var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.FirebaseUid == _userContext.Uid, ct);
            if (client is null || client.Id != classRequest.ClientId)
            {
                return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(403, "Sem permissões para aprovar este pedido."));
            }
        }

        if (classRequest.Status != ClassRequestStatus.Pending)
        {
            return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(409, "Só pedidos pendentes podem ser aprovados."));
        }

        if (classRequest.RoomId == Guid.Empty)
        {
            return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(400, "O pedido não tem uma sala atribuída."));
        }

        var start = classRequest.ProposedStartUtc;
        var duration = classRequest.DurationMinutes;
        var staffId = classRequest.StaffId;
        var roomId = classRequest.RoomId;

        var conflictPendingStaff = await _db.ClassRequests
            .Where(c => c.Id != classRequest.Id && c.StaffId == staffId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, start) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(start, c.ProposedStartUtc) < duration,
                ct);
        if (conflictPendingStaff)
        {
            return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(409, "Já existe um pedido pendente para este staff no mesmo horário."));
        }

        var conflictClassesStaff = await _db.Classes
            .Where(k => k.StaffId == staffId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, start) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(start, k.StartUtc) < duration,
                ct);
        if (conflictClassesStaff)
        {
            return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(409, "Já existe uma aula agendada para este staff no mesmo horário."));
        }

        var conflictPendingRoom = await _db.ClassRequests
            .Where(c => c.Id != classRequest.Id && c.RoomId == roomId && c.Status == ClassRequestStatus.Pending)
            .AnyAsync(c =>
                EF.Functions.DateDiffMinute(c.ProposedStartUtc, start) < c.DurationMinutes &&
                EF.Functions.DateDiffMinute(start, c.ProposedStartUtc) < duration,
                ct);
        if (conflictPendingRoom)
        {
            return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(409, "Já existe um pedido pendente para esta sala no mesmo horário."));
        }

        var conflictClassesRoom = await _db.Classes
            .Where(k => k.RoomId == roomId && k.Status == ClassStatus.Scheduled)
            .AnyAsync(k =>
                EF.Functions.DateDiffMinute(k.StartUtc, start) < k.DurationMinutes &&
                EF.Functions.DateDiffMinute(start, k.StartUtc) < duration,
                ct);
        if (conflictClassesRoom)
        {
            return ServiceResult<ClassRequestApprovedResponse>.Fail(new ServiceError(409, "Já existe uma aula agendada para esta sala no mesmo horário."));
        }

        var uid = _userContext.Uid ?? throw new InvalidOperationException("Missing user id.");
        var klass = new Class(
            clientId: classRequest.ClientId,
            staffId: classRequest.StaffId,
            roomId: classRequest.RoomId,
            startUtc: start,
            durationMinutes: duration,
            createdByUid: uid,
            linkedRequestId: classRequest.Id);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Classes.Add(klass);
            classRequest.Approve();
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        var response = new ClassRequestApprovedResponse(
            classRequest.Id,
            classRequest.ClientId,
            classRequest.StaffId,
            classRequest.ProposedStartUtc,
            classRequest.DurationMinutes,
            classRequest.Notes,
            (int)classRequest.Status,
            classRequest.CreatedByUid,
            classRequest.CreatedAtUtc,
            klass.Id,
            klass.StartUtc,
            klass.DurationMinutes,
            klass.RoomId,
            (int)klass.Status);

        return ServiceResult<ClassRequestApprovedResponse>.Ok(response);
    }

    public async Task<ServiceResult<ClassRequestResponse>> RejectAsync(Guid id, CancellationToken ct)
    {
        var classRequest = await _db.ClassRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (classRequest is null)
        {
            return ServiceResult<ClassRequestResponse>.Fail(new ServiceError(404, "Class request not found"));
        }

        var isAdmin = await _userContext.IsInRoleAsync(RoleName.Admin, ct);
        if (!isAdmin)
        {
            var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.FirebaseUid == _userContext.Uid, ct);
            if (client is null || client.Id != classRequest.ClientId)
            {
                return ServiceResult<ClassRequestResponse>.Fail(new ServiceError(403, "Sem permissões para rejeitar este pedido."));
            }
        }

        if (classRequest.Status != ClassRequestStatus.Pending)
        {
            return ServiceResult<ClassRequestResponse>.Fail(new ServiceError(409, "Só pedidos pendentes podem ser rejeitados."));
        }

        classRequest.Cancel();
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ClassRequestResponse>.Ok(MapToResponse(classRequest));
    }

    private async Task<Guid> ResolveClientIdAsync(CreateClassRequestByStaff request, CancellationToken ct)
    {
        var provided = new[]
        {
            request.ClientId is not null,
            !string.IsNullOrWhiteSpace(request.ClientEmail),
            !string.IsNullOrWhiteSpace(request.ClientUid)
        }.Count(x => x);

        if (provided != 1)
        {
            throw new ArgumentException("Indica exatamente um de: clientId, clientEmail ou clientUid.");
        }

        if (request.ClientId is Guid clientId)
        {
            return clientId;
        }

        var query = _db.Clients.AsNoTracking().Select(c => new { c.Id, c.Email, c.FirebaseUid });

        if (!string.IsNullOrWhiteSpace(request.ClientEmail))
        {
            var email = request.ClientEmail.Trim().ToLowerInvariant();
            var found = await query.FirstOrDefaultAsync(c => c.Email == email, ct);
            if (found is null)
            {
                throw new ArgumentException("ClientEmail não encontrado.");
            }

            return found.Id;
        }

        var uid = request.ClientUid!.Trim();
        var foundUid = await query.FirstOrDefaultAsync(c => c.FirebaseUid == uid, ct);
        if (foundUid is null)
        {
            throw new ArgumentException("ClientUid não encontrado.");
        }

        return foundUid.Id;
    }

    private static ClassRequestResponse MapToResponse(ClassRequest request)
        => new(
            request.Id,
            request.ClientId,
            request.StaffId,
            request.RoomId,
            request.ProposedStartUtc,
            request.DurationMinutes,
            request.Notes,
            (int)request.Status,
            request.CreatedByUid,
            request.CreatedAtUtc);
}
