using BallastLane.Domain.Entities;
using BallastLane.Domain.Exceptions;
using BallastLane.UnitTests.Support;
using Shouldly;
using Xunit;

namespace BallastLane.UnitTests.Domain;

public sealed class UserTests
{
    private const string ValidHash = "hash-value";
    private const string ValidSalt = "salt-value";
    private const int ValidIterations = 210_000;

    [Fact]
    public void Create_WithPaddedUppercaseValues_NormalizesEmailAndDisplayName()
    {
        var user = User.Create(
            "  Demo@BallastLane.TEST ",
            " Demo User ",
            ValidHash,
            ValidSalt,
            ValidIterations,
            TestData.Now);

        user.Email.ShouldBe("demo@ballastlane.test");
        user.DisplayName.ShouldBe("Demo User");
        user.PasswordHash.ShouldBe(ValidHash);
        user.PasswordSalt.ShouldBe(ValidSalt);
        user.PasswordIterations.ShouldBe(ValidIterations);
        user.CreatedAt.ShouldBe(TestData.Now);
        user.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign.example")]
    [InlineData("user@@example.com")]
    [InlineData("user@examplecom")]
    [InlineData("user@exa mple.com")]
    public void Create_WithInvalidEmail_ThrowsDomainException(string email)
    {
        Should.Throw<DomainException>(() =>
        {
            User.Create(email, "Demo User", ValidHash, ValidSalt, ValidIterations, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithEmailLongerThan254Characters_ThrowsDomainException()
    {
        var email = new string('a', 250) + "@example.com";

        Should.Throw<DomainException>(() =>
        {
            User.Create(email, "Demo User", ValidHash, ValidSalt, ValidIterations, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithDisplayNameShorterThanTwoCharacters_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() =>
        {
            User.Create("demo@ballastlane.test", " A ", ValidHash, ValidSalt, ValidIterations, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithDisplayNameAtLengthBounds_AcceptsEightyAndRejectsEightyOne()
    {
        var maximum = new string('n', 80);
        var tooLong = new string('n', 81);

        var user = User.Create(
            "demo@ballastlane.test",
            maximum,
            ValidHash,
            ValidSalt,
            ValidIterations,
            TestData.Now);

        user.DisplayName.ShouldBe(maximum);
        Should.Throw<DomainException>(() =>
        {
            User.Create("demo@ballastlane.test", tooLong, ValidHash, ValidSalt, ValidIterations, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithoutPasswordSecret_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() =>
        {
            User.Create("demo@ballastlane.test", "Demo User", "  ", ValidSalt, ValidIterations, TestData.Now);
        });
        Should.Throw<DomainException>(() =>
        {
            User.Create("demo@ballastlane.test", "Demo User", ValidHash, string.Empty, ValidIterations, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithTooFewPasswordIterations_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() =>
        {
            User.Create("demo@ballastlane.test", "Demo User", ValidHash, ValidSalt, 9_999, TestData.Now);
        });
    }
}
