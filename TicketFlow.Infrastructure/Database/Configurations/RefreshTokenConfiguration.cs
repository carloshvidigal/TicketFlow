using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Auth;
using TicketFlow.Domain.Users;

namespace TicketFlow.Infrastructure.Database.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        // SHA-256 em hexadecimal.
        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        // O token é sempre buscado pelo hash, e um hash nunca se repete.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.HasIndex(t => t.UserId);

        // Concorrência otimista usando a coluna de sistema xmin do Postgres:
        // se outra transação alterou a linha depois da leitura, o UPDATE não
        // afeta nenhuma linha e o EF lança DbUpdateConcurrencyException. É o
        // que garante que um refresh token só pode ser rotacionado uma vez.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
