namespace TicketFlow.Application.Auth;

// Porta: o algoritmo concreto (Argon2, decidido na stack) fica na
// Infrastructure; a Application e o Domain só conhecem este contrato.
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
