using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Authentication;
using TicketFlow.Api.Contracts.Users;
using TicketFlow.Api.Errors;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Users;

namespace TicketFlow.Api.Controllers;

// Administração de usuários: exclusivo do papel Admin.
[ApiController]
[Authorize(Policy = Policies.AdminOnly)]
[Route("users")]
public class UsersController(
    IValidator<UserLookupQuery> lookupValidator,
    IValidator<ChangeRoleRequest> changeRoleValidator,
    FindUserByEmailHandler findByEmail,
    ChangeUserRoleHandler changeRole) : ControllerBase
{
    /// <summary>Localiza um usuário pelo e-mail exato (para descobrir o id de quem terá o papel alterado).</summary>
    [HttpGet]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindByEmail([FromQuery] UserLookupQuery query, CancellationToken cancellationToken)
    {
        await lookupValidator.ValidateAndThrowAsync(query, cancellationToken);

        var profile = await findByEmail.HandleAsync(User.ToActor(), query.Email!, cancellationToken);

        return Ok(UserResponse.From(profile));
    }

    /// <summary>Muda o papel de um usuário entre Customer e Organizer.</summary>
    /// <remarks>
    /// O papel de um Admin não pode ser alterado pela API, e Admin não pode ser atribuído por ela.
    /// A mudança vale para os tokens emitidos depois dela: o token atual do usuário continua com o
    /// papel antigo por até 15 minutos.
    /// </remarks>
    [HttpPut("{id}/role")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        await changeRoleValidator.ValidateAndThrowAsync(request, cancellationToken);

        var role = Enum.Parse<UserRole>(request.Role.Trim(), ignoreCase: true);
        var profile = await changeRole.HandleAsync(new ChangeUserRoleCommand(User.ToActor(), id, role), cancellationToken);

        return Ok(UserResponse.From(profile));
    }
}
