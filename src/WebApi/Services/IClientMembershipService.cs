using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Contracts.Memberships;
using AlmaApp.Domain.Memberships;

namespace AlmaApp.WebApi.Services
{
    
    public interface IClientMembershipService
    {
       
        Task<MembershipResponse?> GetMyMembershipAsync(string firebaseUid, CancellationToken ct);

       
        Task<MembershipResponse> CreateMembershipAsync(Guid clientId, DateTime startUtc, BillingPeriod billingPeriod, string? nif, string createdByUid, CancellationToken ct);

        
        Task CancelMembershipAsync(Guid clientId, string cancelledByUid, CancellationToken ct);

        Task ExpireMembershipAsync(Guid membershipId, CancellationToken ct);
    }
}