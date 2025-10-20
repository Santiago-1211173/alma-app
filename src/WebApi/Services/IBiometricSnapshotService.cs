using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Biometrics;

namespace AlmaApp.WebApi.Services
{

    public interface IBiometricSnapshotService
    {
        Task<BiometricSnapshotDto> CreateSnapshotAsync(string firebaseUid, CreateBiometricSnapshotRequest request, CancellationToken ct);

        Task<PagedResult<BiometricSnapshotDto>> GetMySnapshotsAsync(string firebaseUid, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct);

        Task<PagedResult<BiometricSnapshotDto>> GetSnapshotsByClientIdAsync(Guid clientId, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct);
    }
}