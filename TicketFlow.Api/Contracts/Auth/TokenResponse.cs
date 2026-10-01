using TicketFlow.Application.Auth;

namespace TicketFlow.Api.Contracts.Auth;

public record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt)
{
    public static TokenResponse From(AuthTokens tokens) => new(
        tokens.AccessToken,
        tokens.AccessTokenExpiresAt,
        tokens.RefreshToken,
        tokens.RefreshTokenExpiresAt);
}
