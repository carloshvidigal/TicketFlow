using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Users;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }

    // EF Core precisa de um construtor sem parâmetros (pode ser privado).
    private User() { }

    // O hash já vem pronto de fora (Argon2, decidido na stack) — o Domain
    // não conhece o algoritmo de hashing, isso é responsabilidade da
    // Application/Infrastructure. Formato de e-mail é validado na fronteira
    // da aplicação (FluentValidation), não aqui: o Domain só garante o
    // invariante real — que o campo não está vazio.
    public User(string email, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("USER_EMAIL_REQUIRED", "Email is required.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("USER_PASSWORD_HASH_REQUIRED", "Password hash is required.");

        Id = Guid.NewGuid();
        Email = NormalizeEmail(email);
        PasswordHash = passwordHash;
        Role = role;
    }

    // Regra única de normalização, usada tanto ao criar o usuário quanto ao
    // procurar e-mails já cadastrados — assim "A@x.com" e "a@x.com" são o
    // mesmo e-mail em qualquer lugar do sistema.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("USER_PASSWORD_HASH_REQUIRED", "Password hash is required.");

        PasswordHash = passwordHash;
    }

    // Quem pode mudar o papel de quem é regra da Application (só Admin, e só
    // entre Customer e Organizer); aqui fica apenas a garantia de que o valor é um papel real.
    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
            throw new DomainException("USER_ROLE_INVALID", "Role is invalid.");

        Role = role;
    }
}
