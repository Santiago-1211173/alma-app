using System;

namespace AlmaApp.Domain.Memberships
{
    public enum MembershipStatus
    {
        Active = 0,
        Cancelled = 1,
        Expired = 2
    }

    /// <summary>
    /// Defines the available billing periods for the membership plan.
    /// </summary>
    public enum BillingPeriod
    {
        Month = 0,
        Year = 1
    }

    public sealed class ClientMembership
    {
        public Guid Id { get; private set; }
        public Guid ClientId { get; private set; }
        public DateTime StartUtc { get; private set; }
        public DateTime? EndUtc { get; private set; }
        public MembershipStatus Status { get; private set; }
        public BillingPeriod BillingPeriod { get; private set; }
        public string? Nif { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public string CreatedByUid { get; private set; } = default!;
        public byte[] RowVersion { get; private set; } = default!;

        private ClientMembership() { }

        /// <summary>
        /// Creates a new membership starting at the specified UTC time. The membership
        /// is immediately active. The UID of the creator (admin) is stored for audit.
        /// </summary>
        public ClientMembership(Guid clientId, DateTime startUtc, BillingPeriod billingPeriod, string? nif, string createdByUid)
        {
            if (clientId == Guid.Empty) throw new ArgumentException("ClientId must be provided", nameof(clientId));
            if (string.IsNullOrWhiteSpace(createdByUid)) throw new ArgumentException("Creator UID must be provided", nameof(createdByUid));
            Id = Guid.NewGuid();
            ClientId = clientId;
            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
            BillingPeriod = billingPeriod;
            Nif = string.IsNullOrWhiteSpace(nif) ? null : nif.Trim();
            Status = MembershipStatus.Active;
            CreatedByUid = createdByUid;
            CreatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Cancels an active membership, setting its status to Cancelled and
        /// recording the end date. Cancellation is idempotent.
        /// </summary>
        public void Cancel()
        {
            if (Status != MembershipStatus.Active) return;
            Status = MembershipStatus.Cancelled;
            EndUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Expires an active membership. This should be called when a membership
        /// naturally reaches the end of its billing period and is not renewed.
        /// </summary>
        public void Expire()
        {
            if (Status != MembershipStatus.Active) return;
            Status = MembershipStatus.Expired;
            EndUtc = DateTime.UtcNow;
        }
    }
}