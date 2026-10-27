namespace BallastLane.Application.Dtos;

public sealed record UserDto(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

public sealed record AuthResultDto(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public sealed record RegisterRequest(string? Email, string? DisplayName, string? Password);

public sealed record LoginRequest(string? Email, string? Password);
