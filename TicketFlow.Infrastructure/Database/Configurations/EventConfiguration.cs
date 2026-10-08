using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Users;

namespace TicketFlow.Infrastructure.Database.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Location)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Consulta da listagem pública: eventos publicados, por data.
        builder.HasIndex(e => new { e.Status, e.Date });

        builder.HasIndex(e => e.OrganizerId);

        // Concorrência otimista via coluna de sistema xmin do Postgres (mesma
        // técnica do RefreshToken, ADR-0011): se outra transação alterou o
        // evento depois da leitura, o UPDATE não afeta linha alguma e o EF
        // lança DbUpdateConcurrencyException.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.OrganizerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<Section>()
            .WithOne()
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
