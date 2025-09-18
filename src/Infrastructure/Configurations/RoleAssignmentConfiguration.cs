using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> b)
    {
        b.ToTable("RoleAssignments");
        b.HasKey(x => x.Id);

        b.Property(x => x.FirebaseUid)
            .IsRequired()
            .HasMaxLength(128);

        b.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        // Não queremos duplicados para o mesmo UID/Role
        b.HasIndex(x => new { x.FirebaseUid, x.Role })
            .IsUnique();

        // Útil para “one role each” se quiseres forçar uma única role por utilizador:
        // b.HasIndex(x => x.FirebaseUid).IsUnique();
    }
}
