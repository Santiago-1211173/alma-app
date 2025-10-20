using System;
using System.Threading;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Services
{

    public interface IScheduleConflictService
    {
        
        Task<bool> HasConflictAsync(
            Guid? staffId,
            Guid? roomId,
            Guid? clientId,
            DateTime startLocal,
            DateTime endLocal,
            Guid? excludeId = null,
            CancellationToken ct = default);
    }
}
