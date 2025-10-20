using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Memberships;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Contracts.Memberships;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services
{
    /// <summary>
    /// Service responsible for managing client memberships. Implements
    /// operations to create, cancel and query memberships. This service
    /// contains business rules to ensure a client has at most one active
    /// membership.
    /// </summary>
    public sealed class ClientMembershipService : IClientMembershipService
    {
        private readonly AppDbContext _db;
        public ClientMembershipService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<MembershipResponse?> GetMyMembershipAsync(string firebaseUid, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(firebaseUid)) throw new ArgumentException("firebaseUid must be provided", nameof(firebaseUid));

            var client = await _db.Clients
                .AsNoTracking()
                .Where(c => c.FirebaseUid == firebaseUid)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);
            if (client == null) return null;

            var membership = await _db.ClientMemberships
                .AsNoTracking()
                .Where(m => m.ClientId == client.Id && m.Status == MembershipStatus.Active)
                .FirstOrDefaultAsync(ct);
            if (membership == null) return null;

            return new MembershipResponse(
                membership.Id,
                membership.ClientId,
                membership.StartUtc,
                membership.EndUtc,
                (int)membership.Status,
                (int)membership.BillingPeriod,
                membership.Nif);
        }

        public async Task<MembershipResponse> CreateMembershipAsync(Guid clientId, DateTime startUtc, BillingPeriod billingPeriod, string? nif, string createdByUid, CancellationToken ct)
        {
            if (clientId == Guid.Empty) throw new ArgumentException("clientId must be provided", nameof(clientId));
            if (string.IsNullOrWhiteSpace(createdByUid)) throw new ArgumentException("createdByUid must be provided", nameof(createdByUid));

            // check if client exists
            var exists = await _db.Clients.AnyAsync(c => c.Id == clientId, ct);
            if (!exists)
            {
                throw new InvalidOperationException($"Client {clientId} not found.");
            }

            // check for existing active membership
            var active = await _db.ClientMemberships
                .Where(m => m.ClientId == clientId && m.Status == MembershipStatus.Active)
                .FirstOrDefaultAsync(ct);
            if (active != null)
            {
                throw new InvalidOperationException("Client already has an active membership.");
            }

            var membership = new ClientMembership(clientId, startUtc, billingPeriod, nif, createdByUid);
            _db.ClientMemberships.Add(membership);
            await _db.SaveChangesAsync(ct);

            return new MembershipResponse(
                membership.Id,
                membership.ClientId,
                membership.StartUtc,
                membership.EndUtc,
                (int)membership.Status,
                (int)membership.BillingPeriod,
                membership.Nif);
        }

        public async Task CancelMembershipAsync(Guid clientId, string cancelledByUid, CancellationToken ct)
        {
            if (clientId == Guid.Empty) throw new ArgumentException("clientId must be provided", nameof(clientId));
            if (string.IsNullOrWhiteSpace(cancelledByUid)) throw new ArgumentException("cancelledByUid must be provided", nameof(cancelledByUid));

            var membership = await _db.ClientMemberships
                .Where(m => m.ClientId == clientId && m.Status == MembershipStatus.Active)
                .FirstOrDefaultAsync(ct);
            if (membership == null) return;

            membership.Cancel();
            await _db.SaveChangesAsync(ct);
        }
    }
}