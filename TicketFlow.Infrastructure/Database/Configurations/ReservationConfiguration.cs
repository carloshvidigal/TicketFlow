using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Reservations;
using TicketFlow.Domain.Users;

namespace TicketFlow.Infrastructure.Database.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ExpiresAt)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(r => r.CustomerId);

        // Consulta central do job de expiração (ADR-008, ainda em aberto):
        // "quais reservas ativas já passaram do expires_at?".
        builder.HasIndex(r => new { r.Status, r.ExpiresAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
