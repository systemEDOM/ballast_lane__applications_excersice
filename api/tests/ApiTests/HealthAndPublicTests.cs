using System.Net;
using Shouldly;
using Xunit;

namespace BallastLane.ApiTests;

public sealed class HealthAndPublicTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthAndPublicTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_WithoutToken_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.ReadJsonAsync();
        body.GetProperty("status").GetString().ShouldBe("ok");
    }

    [Fact]
    public async Task PublicInfo_WithoutToken_ReturnsApiMetadata()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/public/info");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.ReadJsonAsync();
        body.GetProperty("name").GetString().ShouldBe("Ballast Lane Tasks API");
        body.GetProperty("version").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("serverTimeUtc").GetDateTimeOffset()
            .ShouldBeLessThan(DateTimeOffset.UtcNow.AddMinutes(1));
    }
}
