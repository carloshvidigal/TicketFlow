using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using TicketFlow.Application.Auth;

namespace TicketFlow.Infrastructure.Auth;

// Argon2id com os parâmetros mínimos recomendados pela OWASP (19 MiB de
// memória, 2 iterações, 1 de paralelismo). Os parâmetros vão dentro do
// próprio hash (formato PHC), então dá para endurecê-los no futuro sem
// invalidar as senhas já guardadas: o Verify lê o que veio no hash.
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySizeKb = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int Version = 19;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(password, salt, MemorySizeKb, Iterations, Parallelism, HashSize);

        return $"$argon2id$v={Version}$m={MemorySizeKb},t={Iterations},p={Parallelism}${Encode(salt)}${Encode(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return false;

        // "" | "argon2id" | "v=19" | "m=..,t=..,p=.." | salt | hash
        var parts = passwordHash.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != $"v={Version}")
            return false;

        var parameters = parts[3].Split(',');
        if (parameters.Length != 3
            || !TryReadParameter(parameters[0], "m", out var memorySizeKb)
            || !TryReadParameter(parameters[1], "t", out var iterations)
            || !TryReadParameter(parameters[2], "p", out var parallelism))
            return false;

        byte[] salt, expectedHash;
        try
        {
            salt = Decode(parts[4]);
            expectedHash = Decode(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Compute(password, salt, memorySizeKb, iterations, parallelism, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Compute(string password, byte[] salt, int memorySizeKb, int iterations, int parallelism, int hashSize)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memorySizeKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(hashSize);
    }

    private static bool TryReadParameter(string text, string name, out int value)
    {
        value = 0;
        var prefix = name + "=";

        return text.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(text.AsSpan(prefix.Length), out value)
            && value > 0;
    }

    // Base64 sem padding, como no formato PHC.
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] Decode(string text) =>
        Convert.FromBase64String(text.PadRight(text.Length + (4 - text.Length % 4) % 4, '='));
}
