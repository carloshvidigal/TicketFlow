namespace TicketFlow.Application.Common;

// Outra requisição alterou o mesmo registro entre a leitura e a gravação
// (concorrência otimista). Quem captura decide o que isso significa no seu
// contexto — ex.: no refresh de token, quem perde a corrida é rejeitado.
public class ConcurrentUpdateException() : Exception("The record was modified by another request.");
