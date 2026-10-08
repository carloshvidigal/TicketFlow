using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Authentication;
using TicketFlow.Api.Contracts.Users;
using TicketFlow.Api.Errors;
using TicketFlow.Application.Users;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("me")]
public class MeController(GetCurrentUserHandler getCurrentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var profile = await getCurrentUser.HandleAsync(User.GetUserId(), cancellationToken);

        return Ok(new MeResponse(profile.Id, profile.Email, profile.Role.ToString()));
    }
}
