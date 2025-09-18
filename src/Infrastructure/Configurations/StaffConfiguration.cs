using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> b)
    {
        b.ToTable("Staff");
        b.HasKey(x => x.Id);

        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        b.Property(x => x.StaffNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Speciality).HasMaxLength(100);

        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(s => s.FirebaseUid).HasMaxLength(128);
        b.HasIndex(s => s.FirebaseUid)
               .IsUnique()
               .HasFilter("[FirebaseUid] IS NOT NULL");

        // Unicidades pedidas pelo RFP
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.Phone).IsUnique();
        b.HasIndex(x => x.StaffNumber).IsUnique();
    }
}
