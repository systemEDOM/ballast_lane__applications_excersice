using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using BallastLane.Domain.Entities;
using BallastLane.Infrastructure.Security;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace BallastLane.InfrastructureTests;

public sealed class JwtTokenIssuerTests
{
    private const string Key = "ballast-lane-development-signing-key-0123456789";
    private const string Issuer = "ballast-lane";
    private const string Audience = "ballast-lane-client";

    [Fact]
    public void Issue_ReturnsSignedTokenWithExpectedClaimsAndExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var issuer = new JwtTokenIssuer(Key, Issuer, Audience, TimeSpan.FromHours(8), new FixedClock(now));
        var user = User.Create("demo@ballastlane.test", "Demo User", "hash-value", "salt-value", 210_000, now.AddDays(-1));

        var (token, expiresAt) = issuer.Issue(user);

        token.ShouldNotBeNullOrWhiteSpace();
        expiresAt.ShouldBe(now.AddHours(8));

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(token, parameters, out var validatedToken);
        var jwt = validatedToken as JwtSecurityToken;
        jwt.ShouldNotBeNull();

        principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value
            .ShouldBe(user.Id.ToString("D", CultureInfo.InvariantCulture));
        principal.FindFirst(JwtRegisteredClaimNames.Email)!.Value.ShouldBe("demo@ballastlane.test");
        principal.FindFirst(JwtRegisteredClaimNames.Name)!.Value.ShouldBe("Demo User");

        jwt!.Issuer.ShouldBe(Issuer);
        jwt.Audiences.ShouldContain(Audience);
        jwt.ValidTo.ShouldBe(expiresAt.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_KeyShorterThan32Characters_Throws()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        Should.Throw<ArgumentException>(
            () => new JwtTokenIssuer(new string('k', 31), Issuer, Audience, TimeSpan.FromHours(1), clock));
    }
}
