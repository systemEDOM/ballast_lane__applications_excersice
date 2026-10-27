using BallastLane.Domain.Entities;

namespace BallastLane.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password, out string salt, out int iterations);

    bool Verify(string password, string hash, string salt, int iterations);
}

public interface ITokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(User user);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }
}

/// <summary>
/// Creates the schema and loads demo data when the application starts.
/// </summary>
public interface IAppInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
