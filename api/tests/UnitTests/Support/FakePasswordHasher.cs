using BallastLane.Application.Abstractions;

namespace BallastLane.UnitTests.Support;

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string HashValue { get; init; } = "hashed-password";

    public string SaltValue { get; init; } = "generated-salt";

    public int IterationsValue { get; init; } = 210_000;

    public bool VerifyResult { get; set; } = true;

    public int HashCalls { get; private set; }

    public string? LastHashedPassword { get; private set; }

    public string? LastVerifiedPassword { get; private set; }

    public string Hash(string password, out string salt, out int iterations)
    {
        HashCalls++;
        LastHashedPassword = password;
        salt = SaltValue;
        iterations = IterationsValue;

        return HashValue;
    }

    public bool Verify(string password, string hash, string salt, int iterations)
    {
        LastVerifiedPassword = password;

        return VerifyResult;
    }
}
