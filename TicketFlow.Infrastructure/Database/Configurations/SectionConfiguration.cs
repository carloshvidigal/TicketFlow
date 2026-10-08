using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Infrastructure.Database.Configurations;

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("sections");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Capacity)
            .IsRequired();

        builder.Property(s => s.Price)
            .HasPrecision(10, 2)
            .IsRequired();

        // Um nome de setor por evento. Também serve às consultas por EventId.
        // (A checagem sem diferenciar maiúsculas/minúsculas acontece dentro da
        // transação de criação do setor; este índice é a última barreira.)
        builder.HasIndex(s => new { s.EventId, s.Name }).IsUnique();

        builder.HasMany<Ticket>()
            .WithOne()
            .HasForeignKey(t => t.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
