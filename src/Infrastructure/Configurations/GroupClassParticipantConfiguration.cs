using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.GroupClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations
{
    public sealed class GroupClassParticipantConfiguration : IEntityTypeConfiguration<GroupClassParticipant>
    {
        public void Configure(EntityTypeBuilder<GroupClassParticipant> b)
        {
            b.ToTable("GroupClassParticipants");
            b.HasKey(x => new { x.GroupClassId, x.ClientId });

            b.Property(x => x.JoinedAtLocal).IsRequired();
            b.Property(x => x.Status).HasConversion<int>().IsRequired();

            b.HasIndex(x => x.ClientId);
        }
    }
}
