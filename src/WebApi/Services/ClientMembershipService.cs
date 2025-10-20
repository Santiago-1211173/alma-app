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
                .FirstOrDefaultAsync(c => c.FirebaseUid == firebaseUid, ct);
            if (client == null || client.CurrentMembershipId is null) return null;

            var membership = await _db.ClientMemberships
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == client.CurrentMembershipId.Value, ct);
            if (membership == null || membership.Status != MembershipStatus.Active)
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                client.ClearCurrentMembership();
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return null;
            }

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

            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct);
            if (client is null)
            {
                throw new InvalidOperationException($"Client {clientId} not found.");
            }

            if (client.CurrentMembershipId.HasValue)
            {
                throw new InvalidOperationException("Client already has an active membership.");
            }

            var membership = new ClientMembership(clientId, startUtc, billingPeriod, nif, createdByUid);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            _db.ClientMemberships.Add(membership);
            client.SetCurrentMembership(membership.Id);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

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

            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct);
            if (client is null) return;

            if (client.CurrentMembershipId is null)
            {
                return;
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var membership = await _db.ClientMemberships
                .FirstOrDefaultAsync(m => m.Id == client.CurrentMembershipId.Value, ct);

            if (membership is null || membership.Status != MembershipStatus.Active)
            {
                client.ClearCurrentMembership();
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }

            membership.Cancel();
            client.ClearCurrentMembership();

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public async Task ExpireMembershipAsync(Guid membershipId, CancellationToken ct)
        {
            if (membershipId == Guid.Empty) throw new ArgumentException("membershipId must be provided", nameof(membershipId));

            var membership = await _db.ClientMemberships.FirstOrDefaultAsync(m => m.Id == membershipId, ct);
            if (membership is null) return;

            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == membership.ClientId, ct);
            if (client is null) return;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            membership.Expire();
            if (client.CurrentMembershipId == membership.Id)
            {
                client.ClearCurrentMembership();
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}