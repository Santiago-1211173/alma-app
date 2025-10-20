using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Biometrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations
{

    public sealed class BiometricSnapshotConfiguration : IEntityTypeConfiguration<BiometricSnapshot>
    {
        public void Configure(EntityTypeBuilder<BiometricSnapshot> builder)
        {
            builder.ToTable("BiometricSnapshots");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClientId).IsRequired();
            builder.Property(x => x.TakenAtUtc).IsRequired();
            builder.Property(x => x.CreatedByUid).HasMaxLength(128).IsRequired();
            builder.Property(x => x.RowVersion).IsRowVersion();

            // Optional properties can specify max lengths where appropriate
            builder.Property(x => x.Pathologies).HasMaxLength(2000);
            builder.Property(x => x.Allergens).HasMaxLength(2000);
            builder.Property(x => x.DietPlan).HasMaxLength(2000);
            builder.Property(x => x.Notes).HasMaxLength(4000);

            // Index to support queries for a client's snapshots ordered by date
            builder.HasIndex(x => new { x.ClientId, x.TakenAtUtc });
        }
    }
}