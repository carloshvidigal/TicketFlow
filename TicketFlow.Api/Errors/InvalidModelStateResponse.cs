using Microsoft.AspNetCore.Mvc;

namespace TicketFlow.Api.Errors;

// Corpo ausente, JSON malformado ou campo obrigatório faltando são barrados
// pelo próprio MVC antes de chegar ao controller. Sem isto, esses casos
// voltariam num formato diferente (ProblemDetails) do resto da Api.
public static class InvalidModelStateResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var details = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ErrorDetail(
                GlobalExceptionHandler.ToCamelCase(entry.Key),
                // Mensagens de parsing de JSON trazem detalhes internos; nesses
                // casos o ErrorMessage vem vazio e usamos um texto genérico.
                string.IsNullOrEmpty(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage)))
            .ToList();

        var body = new ErrorBody("VALIDATION_ERROR", "One or more validation errors occurred.", details);

        return new BadRequestObjectResult(new ErrorResponse(body));
    }
}
