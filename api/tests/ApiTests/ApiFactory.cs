using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace BallastLane.ApiTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "api-tests-only-signing-key-4f8b1d9c2e7a";
    public const string JwtIssuer = "ballast-lane";
    public const string JwtAudience = "ballast-lane-client";

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"ballast-lane-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // Host settings are merged before the entry point reads configuration, which keeps the
        // per-factory database and signing key isolated from the defaults in appsettings.json.
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath}");
        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var path = _databasePath + suffix;

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
