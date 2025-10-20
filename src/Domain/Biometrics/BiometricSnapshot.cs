using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System;

namespace AlmaApp.Domain.Biometrics
{
    public sealed class BiometricSnapshot
    {
        public Guid Id { get; private set; }
        public Guid ClientId { get; private set; }
        public DateTime TakenAtUtc { get; private set; }
        public string CreatedByUid { get; private set; } = default!;
        public byte[] RowVersion { get; private set; } = default!;

        // Measurements (all optional)
        public double? WeightMinKg { get; private set; }
        public double? WeightMaxKg { get; private set; }
        public double? BodyFatKg { get; private set; }
        public double? LeanMassKg { get; private set; }
        public double? VisceralFatIndex { get; private set; }
        public double? BodyMassIndex { get; private set; }
        public double? HeightCm { get; private set; }
        public int? Age { get; private set; }
        public Gender? Gender { get; private set; }
        public string? Pathologies { get; private set; }
        public string? Allergens { get; private set; }
        public string? DietPlan { get; private set; }
        public double? SleepHours { get; private set; }
        public double? ChestCm { get; private set; }
        public double? WaistCm { get; private set; }
        public double? AbdomenCm { get; private set; }
        public double? HipsCm { get; private set; }
        public string? Notes { get; private set; }

        private BiometricSnapshot() { }

        public BiometricSnapshot(
            Guid clientId,
            DateTime takenAtUtc,
            string createdByUid,
            double? weightMinKg = null,
            double? weightMaxKg = null,
            double? bodyFatKg = null,
            double? leanMassKg = null,
            double? visceralFatIndex = null,
            double? bodyMassIndex = null,
            double? heightCm = null,
            int? age = null,
            Gender? gender = null,
            string? pathologies = null,
            string? allergens = null,
            string? dietPlan = null,
            double? sleepHours = null,
            double? chestCm = null,
            double? waistCm = null,
            double? abdomenCm = null,
            double? hipsCm = null,
            string? notes = null)
        {
            if (clientId == Guid.Empty) throw new ArgumentException("ClientId must be provided", nameof(clientId));
            if (string.IsNullOrWhiteSpace(createdByUid)) throw new ArgumentException("createdByUid must be provided", nameof(createdByUid));
            Id = Guid.NewGuid();
            ClientId = clientId;
            TakenAtUtc = DateTime.SpecifyKind(takenAtUtc, DateTimeKind.Utc);
            CreatedByUid = createdByUid;
            WeightMinKg = weightMinKg;
            WeightMaxKg = weightMaxKg;
            BodyFatKg = bodyFatKg;
            LeanMassKg = leanMassKg;
            VisceralFatIndex = visceralFatIndex;
            BodyMassIndex = bodyMassIndex;
            HeightCm = heightCm;
            Age = age;
            Gender = gender;
            Pathologies = string.IsNullOrWhiteSpace(pathologies) ? null : pathologies.Trim();
            Allergens = string.IsNullOrWhiteSpace(allergens) ? null : allergens.Trim();
            DietPlan = string.IsNullOrWhiteSpace(dietPlan) ? null : dietPlan.Trim();
            SleepHours = sleepHours;
            ChestCm = chestCm;
            WaistCm = waistCm;
            AbdomenCm = abdomenCm;
            HipsCm = hipsCm;
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        }
    }
}