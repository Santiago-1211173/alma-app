using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Rooms; // <- certifica-te que este namespace é o correto para a tua entidade Room
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class ClassRequestConfiguration : IEntityTypeConfiguration<ClassRequest>
{
    public void Configure(EntityTypeBuilder<ClassRequest> b)
    {
        b.ToTable("ClassRequests");
        b.HasKey(x => x.Id);

        b.Property(x => x.ClientId).IsRequired();
        b.Property(x => x.StaffId).IsRequired();
        b.Property(x => x.RoomId).IsRequired();               // RoomId obrigatório
        b.Property(x => x.ProposedStartUtc).IsRequired();
        b.Property(x => x.DurationMinutes).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedByUid).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.Property(x => x.Notes).HasMaxLength(500);

        // FK correta para Room (usar o tipo real da entidade)
        // Se a tua entidade de domínio tiver navegação (ex.: public Room Room { get; private set; }),
        // podes trocar ".HasOne<Room>()" por ".HasOne(x => x.Room)".
        b.HasOne<Room>()
         .WithMany()
         .HasForeignKey(x => x.RoomId)
         .OnDelete(DeleteBehavior.Restrict);

        // Índices úteis para pesquisa e conflitos
        b.HasIndex(x => new { x.StaffId, x.ProposedStartUtc });
        b.HasIndex(x => new { x.ClientId, x.ProposedStartUtc });
        b.HasIndex(x => new { x.RoomId,  x.ProposedStartUtc }); // índice por sala
        b.HasIndex(x => x.Status);
    }
}
