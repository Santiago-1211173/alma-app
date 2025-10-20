using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations
{
    public sealed class ActivityParticipantConfiguration : IEntityTypeConfiguration<ActivityParticipant>
    {
        public void Configure(EntityTypeBuilder<ActivityParticipant> b)
        {
            b.ToTable("ActivityParticipants");
            b.HasKey(p => new { p.ActivityId, p.ClientId });

            b.Property(p => p.JoinedAtLocal).IsRequired();
            b.Property(p => p.Status).HasConversion<int>().IsRequired();
            b.HasIndex(p => p.ClientId);
        }
    }
}
