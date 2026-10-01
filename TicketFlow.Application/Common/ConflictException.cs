namespace TicketFlow.Application.Common;

// O pedido é válido, mas conflita com o estado atual do sistema (ex.: e-mail
// já cadastrado). Diferente de DomainException, que é uma regra de negócio
// violada dentro de uma entidade; a Api traduz esta para HTTP 409.
public class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
