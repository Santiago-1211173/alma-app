using AlmaApp.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> e)
    {
        e.ToTable("Clients");
        e.HasKey(x => x.Id);

        e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        e.Property(x => x.BirthDate).HasColumnType("date");

        e.Property(x => x.CitizenCardNumber).HasMaxLength(20).IsRequired();
        e.Property(x => x.Email).HasMaxLength(200).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(30).IsRequired();

        // ÚNICOS (sem duplicar)
        e.HasIndex(x => x.CitizenCardNumber).IsUnique();
        e.HasIndex(x => x.Email).IsUnique();
        e.HasIndex(x => x.Phone).IsUnique();
    }
}
