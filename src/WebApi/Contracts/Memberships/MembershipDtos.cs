using System;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Memberships
{
    public sealed record MembershipResponse(
        Guid? MembershipId,
        Guid? ClientId,
        DateTime? StartUtc,
        DateTime? EndUtc,
        int? Status,
        int? BillingPeriod,
        string? Nif);

    public sealed class CreateMembershipRequest
    {
        [Required]
        public DateTime Start { get; set; }
        public string? Nif { get; set; }
        [Range(0, 1)]
        public int BillingPeriod { get; set; } = 0;
    }

    public sealed class CancelMembershipRequest
    {
        public string? Reason { get; set; }
    }
}