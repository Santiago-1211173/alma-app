using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.ServiceAppointments;

namespace AlmaApp.WebApi.Services;

public interface IServiceAppointmentService
{
    Task<PagedResult<ServiceAppointmentListItemDto>> SearchAsync(
        Guid? clientId,
        Guid? staffId,
        Guid? roomId,
        int? serviceType,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct);
    Task<ServiceAppointmentResponse?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ServiceAppointmentResponse> CreateAsync(CreateServiceAppointmentRequestDto dto, string currentUid, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateServiceAppointmentRequestDto dto, string currentUid, CancellationToken ct);
    Task CancelAsync(Guid id, string currentUid, CancellationToken ct);
    Task CompleteAsync(Guid id, string currentUid, CancellationToken ct);
}