using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace TicketFlow.Application.Auth;

// O refresh token é um valor opaco e aleatório de 256 bits, não um JWT. Como
// já tem entropia alta, um SHA-256 simples basta para guardá-lo (Argon2 é
// para senhas escolhidas por humanos) — e permite buscar o token pelo hash.
public static class RefreshTokenSecret
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
