using System.Security.Cryptography;
using BallastLane.Application.Abstractions;

namespace BallastLane.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing with a per-password random salt.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int DefaultIterations = 210_000;

    private const int SaltSize = 16;
    private const int KeySize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password, out string salt, out int iterations)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("A password is required.", nameof(password));
        }

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var keyBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, DefaultIterations, Algorithm, KeySize);

        salt = Convert.ToBase64String(saltBytes);
        iterations = DefaultIterations;

        return Convert.ToBase64String(keyBytes);
    }

    public bool Verify(string password, string hash, string salt, int iterations)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(hash) ||
            string.IsNullOrEmpty(salt) ||
            iterations <= 0)
        {
            return false;
        }

        var expected = Convert.FromBase64String(hash);
        var saltBytes = Convert.FromBase64String(salt);

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, Algorithm, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
