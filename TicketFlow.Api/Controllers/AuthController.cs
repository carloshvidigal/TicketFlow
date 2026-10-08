using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketFlow.Api.Contracts.Auth;
using TicketFlow.Api.Errors;
using TicketFlow.Api.RateLimiting;
using TicketFlow.Application.Auth;

namespace TicketFlow.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public class AuthController(
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshTokenRequest> refreshTokenValidator,
    RegisterUserHandler registerUser,
    LoginHandler login,
    RefreshTokenHandler refresh,
    LogoutHandler logout) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        // Validação na fronteira da aplicação: nada que o cliente mandou é
        // confiável até passar por aqui.
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await registerUser.HandleAsync(
            new RegisterUserCommand(request.Email, request.Password), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new RegisterResponse(result.Id, result.Email, result.Role.ToString()));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tokens = await login.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);

        return Ok(TokenResponse.From(tokens));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tokens = await refresh.HandleAsync(new RefreshTokenCommand(request.RefreshToken), cancellationToken);

        return Ok(TokenResponse.From(tokens));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        await logout.HandleAsync(new LogoutCommand(request.RefreshToken), cancellationToken);

        return NoContent();
    }
}
