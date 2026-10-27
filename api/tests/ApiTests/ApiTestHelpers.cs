using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BallastLane.Application.Dtos;
using Shouldly;

namespace BallastLane.ApiTests;

internal static class ApiTestHelpers
{
    public const string DemoEmail = "demo@ballastlane.test";
    public const string DemoPassword = "Demo12345!";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string UniqueEmail(string prefix = "user")
        => $"{prefix}.{Guid.NewGuid():N}@example.test";

    public static async Task<string> LoginAsync(
        this HttpClient client,
        string email = DemoEmail,
        string password = DemoPassword)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password),
            JsonOptions);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions);
        auth.ShouldNotBeNull();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();

        return auth.AccessToken;
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var token = await client.LoginAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public static async Task<T?> ReadAsync<T>(this HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>(JsonOptions);

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        return document.RootElement.Clone();
    }

    public static string? ReadDetail(this JsonElement problem)
        => problem.TryGetProperty("detail", out var detail) ? detail.GetString() : null;

    public static string? ReadCode(this JsonElement problem)
        => problem.TryGetProperty("code", out var code) ? code.GetString() : null;

    public static bool HasErrorFor(this JsonElement problem, string field)
        => problem.TryGetProperty("errors", out var errors)
           && errors.ValueKind == JsonValueKind.Object
           && errors.EnumerateObject().Any(property =>
               string.Equals(property.Name, field, StringComparison.OrdinalIgnoreCase));
}
