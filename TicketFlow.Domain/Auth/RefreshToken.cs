using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Auth;

// Guarda só o hash do refresh token (nunca o valor em si): quem tiver acesso
// de leitura ao banco não consegue se passar por um usuário. O valor real só
// existe no cliente.
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt, DateTime now)
    {
        if (userId == Guid.Empty)
            throw new DomainException("REFRESH_TOKEN_USER_REQUIRED", "User is required.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("REFRESH_TOKEN_HASH_REQUIRED", "Token hash is required.");

        if (expiresAt <= now)
            throw new DomainException("REFRESH_TOKEN_EXPIRATION_INVALID", "Expiration must be in the future.");

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTime now) => now >= ExpiresAt;

    public bool IsActive(DateTime now) => !IsRevoked && !IsExpired(now);

    public void Revoke(DateTime now)
    {
        if (IsRevoked)
            throw new DomainException("REFRESH_TOKEN_ALREADY_REVOKED", "Refresh token is already revoked.");

        RevokedAt = now;
    }
}
