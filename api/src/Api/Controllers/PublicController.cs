using System.Reflection;
using BallastLane.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BallastLane.Api.Controllers;

public sealed record PublicInfoResponse(string Name, string Version, DateTimeOffset ServerTimeUtc);

[ApiController]
[Route("api/[controller]")]
public sealed class PublicController : ControllerBase
{
    private const string ApiName = "Ballast Lane Tasks API";

    private readonly IClock _clock;

    public PublicController(IClock clock)
    {
        _clock = clock;
    }

    [HttpGet("info")]
    [AllowAnonymous]
    public ActionResult<PublicInfoResponse> GetInfo()
    {
        return Ok(new PublicInfoResponse(ApiName, ResolveVersion(), _clock.UtcNow));
    }

    private static string ResolveVersion()
    {
        var informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "1.0.0";
        }

        var metadataIndex = informational.IndexOf('+', StringComparison.Ordinal);

        return metadataIndex > 0 ? informational[..metadataIndex] : informational;
    }
}
