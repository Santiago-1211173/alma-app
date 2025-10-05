using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Clients;
using AlmaApp.WebApi.Features.Clients;
namespace AlmaApp.WebApi.Services;

public interface IClientsService
{
    Task<ServiceResult<PagedResult<ClientListItemDto>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<ServiceResult<ClientResponse>> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<ClientResponse>> CreateAsync(CreateClientRequest request, CancellationToken ct);

    Task<ServiceResult> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct);
}
