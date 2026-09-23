using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Reservations;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Infrastructure.Database.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Consulta central do fluxo de reserva: "quantos tickets disponíveis
        // existem para este setor?". Fica pronta desde já; a estratégia de
        // locking em cima dela (ADR-005) ainda é uma decisão em aberto.
        builder.HasIndex(t => new { t.SectionId, t.Status });

        builder.HasIndex(t => t.ReservationId);

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(t => t.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
