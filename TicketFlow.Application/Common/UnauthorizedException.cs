namespace TicketFlow.Application.Common;

// Credencial ausente, inválida ou expirada; a Api traduz para HTTP 401.
public class UnauthorizedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
