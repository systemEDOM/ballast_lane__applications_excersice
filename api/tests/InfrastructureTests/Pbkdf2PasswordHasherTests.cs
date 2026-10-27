using BallastLane.Infrastructure.Security;
using Shouldly;
using Xunit;

namespace BallastLane.InfrastructureTests;

public sealed class Pbkdf2PasswordHasherTests
{
    private const string Password = "Demo12345!";

    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_CorrectPasswordReturnsTrue()
    {
        var hash = _hasher.Hash(Password, out var salt, out var iterations);

        iterations.ShouldBe(210_000);
        Convert.FromBase64String(hash).Length.ShouldBe(32);
        Convert.FromBase64String(salt).Length.ShouldBe(16);
        _hasher.Verify(Password, hash, salt, iterations).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WrongPasswordReturnsFalse()
    {
        var hash = _hasher.Hash(Password, out var salt, out var iterations);

        _hasher.Verify("Demo12345?", hash, salt, iterations).ShouldBeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentSaltsAndHashes()
    {
        var firstHash = _hasher.Hash(Password, out var firstSalt, out var firstIterations);
        var secondHash = _hasher.Hash(Password, out var secondSalt, out var secondIterations);

        firstSalt.ShouldNotBe(secondSalt);
        firstHash.ShouldNotBe(secondHash);
        _hasher.Verify(Password, firstHash, firstSalt, firstIterations).ShouldBeTrue();
        _hasher.Verify(Password, secondHash, secondSalt, secondIterations).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WithDifferentIterationCount_ReturnsFalse()
    {
        var hash = _hasher.Hash(Password, out var salt, out var iterations);

        _hasher.Verify(Password, hash, salt, iterations / 2).ShouldBeFalse();
        _hasher.Verify(Password, hash, salt, iterations).ShouldBeTrue();
    }
}
