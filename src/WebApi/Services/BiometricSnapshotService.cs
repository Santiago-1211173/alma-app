using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Biometrics;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Biometrics;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services
{
    
    public sealed class BiometricSnapshotService : IBiometricSnapshotService
    {
        private readonly AppDbContext _db;
        public BiometricSnapshotService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<BiometricSnapshotDto> CreateSnapshotAsync(string firebaseUid, CreateBiometricSnapshotRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(firebaseUid)) throw new ArgumentException("firebaseUid must be provided", nameof(firebaseUid));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var client = await _db.Clients
                .Where(c => c.FirebaseUid == firebaseUid)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);
            if (client == null) throw new InvalidOperationException("Client not found for current user.");

            // Convert Taken to UTC; assume unspecified or local values represent local time.
            var takenUtc = request.Taken.Kind == DateTimeKind.Utc
                ? request.Taken
                : DateTime.SpecifyKind(request.Taken, DateTimeKind.Utc);

            var snapshot = new BiometricSnapshot(
                clientId: client.Id,
                takenAtUtc: takenUtc,
                createdByUid: firebaseUid,
                weightMinKg: request.WeightMinKg,
                weightMaxKg: request.WeightMaxKg,
                bodyFatKg: request.BodyFatKg,
                leanMassKg: request.LeanMassKg,
                visceralFatIndex: request.VisceralFatIndex,
                bodyMassIndex: request.BodyMassIndex,
                heightCm: request.HeightCm,
                age: request.Age,
                gender: request.Gender,
                pathologies: request.Pathologies,
                allergens: request.Allergens,
                dietPlan: request.DietPlan,
                sleepHours: request.SleepHours,
                chestCm: request.ChestCm,
                waistCm: request.WaistCm,
                abdomenCm: request.AbdomenCm,
                hipsCm: request.HipsCm,
                notes: request.Notes
            );

            _db.BiometricSnapshots.Add(snapshot);
            await _db.SaveChangesAsync(ct);

            return MapToDto(snapshot);
        }

        public async Task<PagedResult<BiometricSnapshotDto>> GetMySnapshotsAsync(string firebaseUid, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(firebaseUid)) throw new ArgumentException("firebaseUid must be provided", nameof(firebaseUid));
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

            var client = await _db.Clients
                .Where(c => c.FirebaseUid == firebaseUid)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);
            if (client == null) return PagedResult<BiometricSnapshotDto>.Create(Array.Empty<BiometricSnapshotDto>(), page, pageSize, 0);

            var q = _db.BiometricSnapshots
                .AsNoTracking()
                .Where(s => s.ClientId == client.Id);
            if (fromUtc.HasValue)
            {
                var from = fromUtc.Value;
                q = q.Where(s => s.TakenAtUtc >= from);
            }
            if (toUtc.HasValue)
            {
                var to = toUtc.Value;
                q = q.Where(s => s.TakenAtUtc < to);
            }
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(s => s.TakenAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => MapToDto(s))
                .ToListAsync(ct);

            return PagedResult<BiometricSnapshotDto>.Create(items, page, pageSize, total);
        }

        public async Task<PagedResult<BiometricSnapshotDto>> GetSnapshotsByClientIdAsync(Guid clientId, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct)
        {
            if (clientId == Guid.Empty) throw new ArgumentException("clientId must be provided", nameof(clientId));
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);
            var q = _db.BiometricSnapshots
                .AsNoTracking()
                .Where(s => s.ClientId == clientId);
            if (fromUtc.HasValue)
            {
                q = q.Where(s => s.TakenAtUtc >= fromUtc.Value);
            }
            if (toUtc.HasValue)
            {
                q = q.Where(s => s.TakenAtUtc < toUtc.Value);
            }
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(s => s.TakenAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => MapToDto(s))
                .ToListAsync(ct);
            return PagedResult<BiometricSnapshotDto>.Create(items, page, pageSize, total);
        }

        private static BiometricSnapshotDto MapToDto(BiometricSnapshot s)
        {
            return new BiometricSnapshotDto(
                s.Id,
                s.TakenAtUtc,
                s.WeightMinKg,
                s.WeightMaxKg,
                s.BodyFatKg,
                s.LeanMassKg,
                s.VisceralFatIndex,
                s.BodyMassIndex,
                s.HeightCm,
                s.Age,
                s.Gender.HasValue ? (int?)s.Gender.Value : null,
                s.Pathologies,
                s.Allergens,
                s.DietPlan,
                s.SleepHours,
                s.ChestCm,
                s.WaistCm,
                s.AbdomenCm,
                s.HipsCm,
                s.Notes
            );
        }
    }
}