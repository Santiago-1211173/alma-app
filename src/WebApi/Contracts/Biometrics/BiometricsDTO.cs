using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using AlmaApp.Domain.Biometrics;

namespace AlmaApp.WebApi.Contracts.Biometrics
{
    
    public sealed class CreateBiometricSnapshotRequest
    {
        
        [Required]
        public DateTime Taken { get; set; }

        [Range(0.0, 1000.0)]
        public double? WeightMinKg { get; set; }
        [Range(0.0, 1000.0)]
        public double? WeightMaxKg { get; set; }
        [Range(0.0, 1000.0)]
        public double? BodyFatKg { get; set; }
        [Range(0.0, 1000.0)]
        public double? LeanMassKg { get; set; }
        [Range(0.0, 50.0)]
        public double? VisceralFatIndex { get; set; }
        [Range(0.0, 100.0)]
        public double? BodyMassIndex { get; set; }
        [Range(0.0, 300.0)]
        public double? HeightCm { get; set; }
        [Range(0, 150)]
        public int? Age { get; set; }
        [Range(0, 3)]
        public Gender? Gender { get; set; }
        [MaxLength(2000)]
        public string? Pathologies { get; set; }
        [MaxLength(2000)]
        public string? Allergens { get; set; }
        [MaxLength(2000)]
        public string? DietPlan { get; set; }
        [Range(0.0, 24.0)]
        public double? SleepHours { get; set; }
        [Range(0.0, 500.0)]
        public double? ChestCm { get; set; }
        [Range(0.0, 500.0)]
        public double? WaistCm { get; set; }
        [Range(0.0, 500.0)]
        public double? AbdomenCm { get; set; }
        [Range(0.0, 500.0)]
        public double? HipsCm { get; set; }
        [MaxLength(4000)]
        public string? Notes { get; set; }
    }

   
    public sealed record BiometricSnapshotDto(
        Guid Id,
        DateTime TakenUtc,
        double? WeightMinKg,
        double? WeightMaxKg,
        double? BodyFatKg,
        double? LeanMassKg,
        double? VisceralFatIndex,
        double? BodyMassIndex,
        double? HeightCm,
        int? Age,
        int? Gender,
        string? Pathologies,
        string? Allergens,
        string? DietPlan,
        double? SleepHours,
        double? ChestCm,
        double? WaistCm,
        double? AbdomenCm,
        double? HipsCm,
        string? Notes
    );
}