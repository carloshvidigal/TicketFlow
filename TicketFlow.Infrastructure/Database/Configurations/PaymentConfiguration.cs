using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Orders;
using TicketFlow.Domain.Payments;

namespace TicketFlow.Infrastructure.Database.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.ProviderReference)
            .HasMaxLength(200);

        // Um pedido pode ter mais de uma tentativa de pagamento (recusada,
        // expirada, e então uma nova tentativa) — por isso não é 1:1.
        builder.HasIndex(p => p.OrderId);

        // Suporte direto ao cenário crítico "webhook duplicado não produz
        // efeitos duplicados" (ADR-006, ainda em aberto): a mesma referência
        // do provider não pode aparecer duas vezes.
        builder.HasIndex(p => p.ProviderReference)
            .IsUnique()
            .HasFilter("\"ProviderReference\" IS NOT NULL");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
