using System.Text.Json.Serialization;

namespace TicketFlow.Api.Errors;

// Formato único de erro da Api (§17 do documento):
// { "error": { "code": "...", "message": "...", "details": [ ... ] } }
public record ErrorResponse(ErrorBody Error);

public record ErrorBody(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ErrorDetail>? Details = null);

public record ErrorDetail(string Field, string Message);
