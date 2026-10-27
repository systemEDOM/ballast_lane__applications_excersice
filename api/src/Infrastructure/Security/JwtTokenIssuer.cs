using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BallastLane.Application.Abstractions;
using BallastLane.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace BallastLane.Infrastructure.Security;

public sealed class JwtTokenIssuer : ITokenIssuer
{
    private const int MinimumKeyLength = 32;

    private readonly SigningCredentials _signingCredentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _lifetime;
    private readonly IClock _clock;

    public JwtTokenIssuer(string key, string issuer, string audience, TimeSpan lifetime, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length < MinimumKeyLength)
        {
            throw new ArgumentException(
                $"The JWT signing key must be at least {MinimumKeyLength} characters long.",
                nameof(key));
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentException("A JWT issuer is required.", nameof(issuer));
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new ArgumentException("A JWT audience is required.", nameof(audience));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "The token lifetime must be positive.");
        }

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        _issuer = issuer;
        _audience = audience;
        _lifetime = lifetime;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public (string Token, DateTimeOffset ExpiresAt) Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = _clock.UtcNow;
        var expiresAt = issuedAt.Add(_lifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D", CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _signingCredentials);

        var handler = new JwtSecurityTokenHandler();

        return (handler.WriteToken(token), expiresAt);
    }
}
