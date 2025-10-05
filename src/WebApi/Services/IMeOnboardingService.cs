using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Auth;

namespace AlmaApp.WebApi.Services;

public interface IMeOnboardingService
{
    Task<ServiceResult<OnboardingResult>> CreateClientAsync(CreateClientSelf request, CancellationToken ct);

    Task<ServiceResult<OnboardingResult>> ClaimStaffAsync(ClaimStaffBody request, CancellationToken ct);
}
