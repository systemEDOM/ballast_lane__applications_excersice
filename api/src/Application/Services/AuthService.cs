using BallastLane.Application.Abstractions;
using BallastLane.Application.Common;
using BallastLane.Application.Dtos;
using BallastLane.Application.Validation;
using BallastLane.Domain.Entities;

namespace BallastLane.Application.Services;

public sealed class AuthService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";
    private const string UserNotFoundMessage = "User not found.";
    private const string EmailTakenMessage = "An account with this email already exists.";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenIssuer _tokens;
    private readonly IClock _clock;

    public AuthService(IUserRepository users, IPasswordHasher hasher, ITokenIssuer tokens, IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<Result<AuthResultDto>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var errors = RequestValidator.ValidateRegister(request);

        if (errors.Count > 0)
        {
            return Result<AuthResultDto>.Fail(Error.Validation(errors));
        }

        var email = RequestValidator.NormalizeEmail(request.Email);

        if (await _users.EmailExistsAsync(email, ct))
        {
            return Result<AuthResultDto>.Fail(Error.Conflict(EmailTakenMessage));
        }

        var passwordHash = _hasher.Hash(request.Password!, out var salt, out var iterations);
        var user = User.Create(email, request.DisplayName!, passwordHash, salt, iterations, _clock.UtcNow);

        await _users.AddAsync(user, ct);

        return Result<AuthResultDto>.Ok(CreateAuthResult(user));
    }

    public async Task<Result<AuthResultDto>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var errors = RequestValidator.ValidateLogin(request);

        if (errors.Count > 0)
        {
            return Result<AuthResultDto>.Fail(Error.Validation(errors));
        }

        var email = RequestValidator.NormalizeEmail(request.Email);
        var user = await _users.FindByEmailAsync(email, ct);

        if (user is null ||
            !_hasher.Verify(request.Password!, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            return Result<AuthResultDto>.Fail(Error.Unauthorized(InvalidCredentialsMessage));
        }

        return Result<AuthResultDto>.Ok(CreateAuthResult(user));
    }

    public async Task<Result<UserDto>> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct);

        if (user is null)
        {
            return Result<UserDto>.Fail(Error.NotFound(UserNotFoundMessage));
        }

        return Result<UserDto>.Ok(ToDto(user));
    }

    private AuthResultDto CreateAuthResult(User user)
    {
        var (token, expiresAt) = _tokens.Issue(user);

        return new AuthResultDto(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(User user)
        => new(user.Id, user.Email, user.DisplayName, user.CreatedAt);
}
