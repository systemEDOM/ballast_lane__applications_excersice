using BallastLane.Application.Abstractions;
using BallastLane.Application.Common;
using BallastLane.Application.Dtos;
using BallastLane.Application.Services;
using BallastLane.Domain.Entities;
using BallastLane.UnitTests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BallastLane.UnitTests.Services;

public sealed class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ITokenIssuer _tokens = Substitute.For<ITokenIssuer>();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeClock _clock = new(TestData.Now);
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_users, _hasher, _tokens, _clock);
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_ReturnsIssuedTokenAndCreatedUser()
    {
        var expiresAt = TestData.Now.AddHours(8);
        User? stored = null;

        _users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        _users.AddAsync(Arg.Do<User>(user => stored = user), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _tokens.Issue(Arg.Any<User>()).Returns(("issued-token", expiresAt));

        var result = await _sut.RegisterAsync(
            new RegisterRequest("  Demo@BallastLane.test ", " Demo User ", "Secret123"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("issued-token");
        result.Value.ExpiresAt.ShouldBe(expiresAt);
        result.Value.User.Email.ShouldBe("demo@ballastlane.test");
        result.Value.User.DisplayName.ShouldBe("Demo User");
        result.Value.User.CreatedAt.ShouldBe(TestData.Now);
        stored.ShouldNotBeNull();
        stored!.Email.ShouldBe("demo@ballastlane.test");
        stored.PasswordHash.ShouldBe("hashed-password");
        stored.PasswordSalt.ShouldBe("generated-salt");
        stored.PasswordIterations.ShouldBe(210_000);
        _hasher.HashCalls.ShouldBe(1);
        _hasher.LastHashedPassword.ShouldBe("Secret123");
        _ = _users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        _ = _tokens.Received(1).Issue(Arg.Is<User>(user => user.Email == "demo@ballastlane.test"));
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsConflict()
    {
        _users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var result = await _sut.RegisterAsync(
            new RegisterRequest(" Demo@BallastLane.test ", "Demo User", "Secret123"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error!.Message.ShouldBe("An account with this email already exists.");
        _hasher.HashCalls.ShouldBe(0);
        _ = _users.Received(1).EmailExistsAsync("demo@ballastlane.test", Arg.Any<CancellationToken>());
        _ = _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithMalformedEmail_ReturnsValidationError()
    {
        var result = await _sut.RegisterAsync(
            new RegisterRequest("not-an-email", "Demo User", "Secret123"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!.ContainsKey("email").ShouldBeTrue();
        result.Error!.FieldErrors!["email"].ShouldContain("Email must contain a single @ character.");
        _ = _users.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordWithoutDigit_ReturnsValidationError()
    {
        var result = await _sut.RegisterAsync(
            new RegisterRequest("demo@ballastlane.test", "Demo User", "onlyletters"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!.ContainsKey("password").ShouldBeTrue();
        result.Error!.FieldErrors!["password"].ShouldContain("Password must contain at least one letter and one digit.");
        _hasher.HashCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RegisterAsync_WithTooShortDisplayName_ReturnsValidationError()
    {
        var result = await _sut.RegisterAsync(
            new RegisterRequest("demo@ballastlane.test", " A ", "Secret123"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!.ContainsKey("displayName").ShouldBeTrue();
        result.Error!.FieldErrors!["displayName"].ShouldContain("Display name must be between 2 and 80 characters.");
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResult()
    {
        var user = TestData.NewUser();
        var expiresAt = TestData.Now.AddHours(8);

        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(user));
        _tokens.Issue(Arg.Any<User>()).Returns(("issued-token", expiresAt));

        var result = await _sut.LoginAsync(
            new LoginRequest(" Demo@BallastLane.test ", "Secret123"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("issued-token");
        result.Value.ExpiresAt.ShouldBe(expiresAt);
        result.Value.User.Id.ShouldBe(user.Id);
        result.Value.User.Email.ShouldBe("demo@ballastlane.test");
        _hasher.LastVerifiedPassword.ShouldBe("Secret123");
        _ = _users.Received(1).FindByEmailAsync("demo@ballastlane.test", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ReturnsUnauthorized()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(null));

        var result = await _sut.LoginAsync(
            new LoginRequest("missing@ballastlane.test", "Secret123"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error!.Message.ShouldBe("Invalid email or password.");
        _ = _tokens.DidNotReceive().Issue(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsUnauthorized()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(TestData.NewUser()));
        _hasher.VerifyResult = false;

        var result = await _sut.LoginAsync(
            new LoginRequest("demo@ballastlane.test", "WrongPassword1"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error!.Message.ShouldBe("Invalid email or password.");
        _ = _tokens.DidNotReceive().Issue(Arg.Any<User>());
    }

    [Fact]
    public async Task GetProfileAsync_WithExistingUser_ReturnsUser()
    {
        var user = TestData.NewUser();
        _users.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<User?>(user));

        var result = await _sut.GetProfileAsync(user.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.Email.ShouldBe("demo@ballastlane.test");
        result.Value.DisplayName.ShouldBe("Demo User");
        result.Value.CreatedAt.ShouldBe(TestData.Now);
    }

    [Fact]
    public async Task GetProfileAsync_WithUnknownUser_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _users.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<User?>(null));

        var result = await _sut.GetProfileAsync(userId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error!.Message.ShouldBe("User not found.");
    }
}
