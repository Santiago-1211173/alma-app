using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Auth;

namespace AlmaApp.WebApi.Services;

public interface IMeService
{
    Task<ServiceResult<MeResponse>> GetAsync(CancellationToken ct);
}
