using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Orders;
using TicketFlow.Domain.Reservations;
using TicketFlow.Domain.Users;

namespace TicketFlow.Infrastructure.Database.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Amount)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(o => o.CustomerId);

        // Uma reserva só pode virar um pedido — impede duas confirmações de
        // checkout para a mesma reserva.
        builder.HasIndex(o => o.ReservationId).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(o => o.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
