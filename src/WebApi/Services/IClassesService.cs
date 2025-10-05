using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Classes;

namespace AlmaApp.WebApi.Services;

public interface IClassesService
{
    Task<ServiceResult<PagedResult<ClassListItemDto>>> SearchAsync(
        Guid? clientId,
        Guid? staffId,
        Guid? roomId,
        DateTime? from,
        DateTime? to,
        int? status,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<ServiceResult<ClassResponse>> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<ClassResponse>> CreateAsync(CreateClassRequestDto request, CancellationToken ct);

    Task<ServiceResult<ClassResponse>> CreateFromRequestAsync(Guid requestId, CreateClassFromRequestDto request, CancellationToken ct);

    Task<ServiceResult> UpdateAsync(Guid id, UpdateClassRequestDto request, CancellationToken ct);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct);

    Task<ServiceResult> CompleteAsync(Guid id, CancellationToken ct);
}
