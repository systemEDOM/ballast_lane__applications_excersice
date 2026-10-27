using System.Net;
using System.Net.Http.Json;
using BallastLane.Application.Dtos;
using Shouldly;
using Xunit;

namespace BallastLane.ApiTests;

public sealed class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreatedWithToken()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestHelpers.UniqueEmail();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "New User", "Password123"),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var auth = await response.ReadAsync<AuthResultDto>();
        auth.ShouldNotBeNull();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        auth.User.Email.ShouldBe(email);
        auth.User.DisplayName.ShouldBe("New User");
        auth.User.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var email = ApiTestHelpers.UniqueEmail("duplicate");
        var request = new RegisterRequest(email, "First User", "Password123");

        using var first = await client.PostAsJsonAsync("/api/auth/register", request, ApiTestHelpers.JsonOptions);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var second = await client.PostAsJsonAsync("/api/auth/register", request, ApiTestHelpers.JsonOptions);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await second.ReadJsonAsync();
        problem.ReadCode().ShouldBe("conflict");
    }

    [Theory]
    [InlineData("not-an-email", "Password123", "New User", "email")]
    [InlineData("valid.user@example.test", "short", "New User", "password")]
    [InlineData("valid.user@example.test", "Password123", null, "displayName")]
    public async Task Register_WithInvalidInput_ReturnsValidationProblem(
        string email,
        string password,
        string? displayName,
        string expectedField)
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, displayName, password),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.ReadJsonAsync();
        problem.ReadCode().ShouldBe("validation_failed");
        problem.HasErrorFor(expectedField).ShouldBeTrue();
    }

    [Fact]
    public async Task Login_WithSeededCredentials_ReturnsOkWithToken()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiTestHelpers.DemoEmail, ApiTestHelpers.DemoPassword),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await response.ReadAsync<AuthResultDto>();
        auth.ShouldNotBeNull();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.Email.ShouldBe(ApiTestHelpers.DemoEmail);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiTestHelpers.DemoEmail, "WrongPassword123"),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.ReadJsonAsync();
        problem.ReadCode().ShouldBe("invalid_credentials");
        response.Headers.TryGetValues("WWW-Authenticate", out var challenge).ShouldBeTrue();
        string.Join(",", challenge!).ShouldContain("Bearer");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsSameUnauthorizedMessage()
    {
        using var client = _factory.CreateClient();

        using var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiTestHelpers.DemoEmail, "WrongPassword123"),
            ApiTestHelpers.JsonOptions);

        using var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiTestHelpers.UniqueEmail("missing"), "Password123"),
            ApiTestHelpers.JsonOptions);

        unknownEmail.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var first = await wrongPassword.ReadJsonAsync();
        var second = await unknownEmail.ReadJsonAsync();

        first.ReadCode().ShouldBe("invalid_credentials");
        second.ReadCode().ShouldBe("invalid_credentials");
        first.ReadDetail().ShouldNotBeNullOrWhiteSpace();
        second.ReadDetail().ShouldBe(first.ReadDetail());
    }

    [Fact]
    public async Task Login_WithBlankCredentials_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(" ", string.Empty),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.ReadJsonAsync();
        problem.ReadCode().ShouldBe("validation_failed");
        problem.HasErrorFor("email").ShouldBeTrue();
        problem.HasErrorFor("password").ShouldBeTrue();
    }

    [Fact]
    public async Task Me_WithBearerToken_ReturnsProfile()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.ReadAsync<UserDto>();
        profile.ShouldNotBeNull();
        profile!.Email.ShouldBe(ApiTestHelpers.DemoEmail);
        profile.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
