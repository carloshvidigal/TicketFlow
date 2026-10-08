using Microsoft.AspNetCore.Mvc;

namespace TicketFlow.Api.Errors;

// Corpo ausente, JSON malformado ou parâmetro inválido são barrados pelo
// próprio MVC antes de chegar ao controller. Sem isto, esses casos voltariam
// num formato diferente (ProblemDetails) do resto da Api.
public static class InvalidModelStateResponse
{
    private const string GenericMessage = "The value is invalid.";

    public static IActionResult Create(ActionContext context)
    {
        var details = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
                new ErrorDetail(FieldName(entry.Key), SafeMessage(entry.Key, error))))
            .ToList();

        var body = new ErrorBody("VALIDATION_ERROR", "One or more validation errors occurred.", details);

        return new BadRequestObjectResult(new ErrorResponse(body));
    }

    // Erros de JSON chegam com o caminho no estilo "$.location"; o cliente
    // enxerga o campo simplesmente como "location".
    private static string FieldName(string key) =>
        GlobalExceptionHandler.ToCamelCase(key.TrimStart('$').TrimStart('.'));

    // A mensagem de um erro de desserialização de JSON cita tipos internos
    // ("...to TicketFlow.Api.Contracts.Events.CreateEventRequest") e posições
    // do corpo: é informação do servidor que o cliente não precisa ver. Para
    // esses casos (e para qualquer erro que traga uma exceção), a resposta
    // diz só o essencial. Mensagens de parâmetros de rota e de query ("The
    // value 'abc' is not valid.") só ecoam o que o próprio cliente enviou.
    private static string SafeMessage(string key, Microsoft.AspNetCore.Mvc.ModelBinding.ModelError error)
    {
        var fromJsonBody = key.StartsWith('$');
        var leaksInternals = error.ErrorMessage.Contains("TicketFlow.", StringComparison.Ordinal)
            || error.ErrorMessage.Contains("System.", StringComparison.Ordinal);

        return fromJsonBody || error.Exception is not null || leaksInternals || string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? GenericMessage
            : error.ErrorMessage;
    }
}
