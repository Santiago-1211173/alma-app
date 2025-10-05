using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Clients;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Clients;
using AlmaApp.WebApi.Features.Clients;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class ClientsService : IClientsService
{
    private readonly AppDbContext _db;

    public ClientsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<PagedResult<ClientListItemDto>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var clients = _db.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            clients = clients.Where(c =>
                EF.Functions.Like(c.FirstName, $"%{term}%") ||
                EF.Functions.Like(c.LastName, $"%{term}%") ||
                EF.Functions.Like(c.Email, $"%{term}%") ||
                EF.Functions.Like(c.Phone ?? string.Empty, $"%{term}%"));
        }

        var total = await clients.CountAsync(ct);

        var items = await clients
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientListItemDto(c.Id, c.FirstName, c.LastName, c.Email, c.Phone))
            .ToListAsync(ct);

        var paged = PagedResult<ClientListItemDto>.Create(items, page, pageSize, total);
        return ServiceResult<PagedResult<ClientListItemDto>>.Ok(paged);
    }

    public async Task<ServiceResult<ClientResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (client is null)
        {
            return ServiceResult<ClientResponse>.Fail(new ServiceError(404, "Client not found"));
        }

        return ServiceResult<ClientResponse>.Ok(MapToResponse(client));
    }

    public async Task<ServiceResult<ClientResponse>> CreateAsync(CreateClientRequest request, CancellationToken ct)
    {
        Client client;
        try
        {
            client = new Client(
                id: Guid.NewGuid(),
                firstName: request.FirstName.Trim(),
                lastName: request.LastName.Trim(),
                email: request.Email.Trim().ToLowerInvariant(),
                citizenCardNumber: request.CitizenCardNumber.Trim(),
                phone: request.Phone?.Trim(),
                birthDate: request.BirthDate);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<ClientResponse>.Fail(new ServiceError(400, ex.Message));
        }

        await _db.Clients.AddAsync(client, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ClientResponse>.Ok(MapToResponse(client));
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (client is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Client not found"));
        }

        try
        {
            client.Update(
                request.FirstName.Trim(),
                request.LastName.Trim(),
                request.Email.Trim().ToLowerInvariant(),
                request.CitizenCardNumber.Trim(),
                request.Phone?.Trim(),
                request.BirthDate);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult.Fail(new ServiceError(400, ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (client is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Client not found"));
        }

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static ClientResponse MapToResponse(Client client)
        => new(
            client.Id,
            client.FirstName,
            client.LastName,
            client.Email,
            client.CitizenCardNumber ?? string.Empty,
            client.Phone,
            client.BirthDate,
            client.CreatedAtUtc);
}
