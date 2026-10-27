using BallastLane.Domain.Exceptions;

namespace BallastLane.Domain.Entities;

public sealed class User
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 80;
    public const int EmailMaxLength = 254;

    private User(
        Guid id,
        string email,
        string displayName,
        string passwordHash,
        string passwordSalt,
        int passwordIterations,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        PasswordIterations = passwordIterations;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public string PasswordHash { get; private set; }

    public string PasswordSalt { get; private set; }

    public int PasswordIterations { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(
        string email,
        string displayName,
        string passwordHash,
        string passwordSalt,
        int passwordIterations,
        DateTimeOffset now)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedDisplayName = NormalizeDisplayName(displayName);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordSalt))
        {
            throw new DomainException("Password salt is required.");
        }

        if (passwordIterations < 10_000)
        {
            throw new DomainException("Password iteration count is too low.");
        }

        return new User(
            Guid.NewGuid(),
            normalizedEmail,
            normalizedDisplayName,
            passwordHash,
            passwordSalt,
            passwordIterations,
            now);
    }

    public static User Rehydrate(
        Guid id,
        string email,
        string displayName,
        string passwordHash,
        string passwordSalt,
        int passwordIterations,
        DateTimeOffset createdAt)
        => new(id, email, displayName, passwordHash, passwordSalt, passwordIterations, createdAt);

    public void Rename(string displayName)
        => DisplayName = NormalizeDisplayName(displayName);

    private static string NormalizeEmail(string email)
    {
        var candidate = (email ?? string.Empty).Trim().ToLowerInvariant();

        if (candidate.Length is < 5 or > EmailMaxLength)
        {
            throw new DomainException("Email length is not valid.");
        }

        var atIndex = candidate.IndexOf('@', StringComparison.Ordinal);

        if (atIndex <= 0 || atIndex != candidate.LastIndexOf('@'))
        {
            throw new DomainException("Email must contain a single @ character.");
        }

        var domain = candidate[(atIndex + 1)..];

        if (domain.Length < 3 || !domain.Contains('.', StringComparison.Ordinal))
        {
            throw new DomainException("Email domain is not valid.");
        }

        if (candidate.Any(char.IsWhiteSpace))
        {
            throw new DomainException("Email must not contain whitespace.");
        }

        return candidate;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        var candidate = (displayName ?? string.Empty).Trim();

        if (candidate.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            throw new DomainException(
                $"Display name must be between {DisplayNameMinLength} and {DisplayNameMaxLength} characters.");
        }

        return candidate;
    }
}
