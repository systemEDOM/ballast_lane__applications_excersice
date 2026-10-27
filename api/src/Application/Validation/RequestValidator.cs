using BallastLane.Application.Dtos;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;

namespace BallastLane.Application.Validation;

/// <summary>
/// Field-level validation for requests entering the application layer.
/// Keys are the camelCase JSON names used by the HTTP API.
/// </summary>
public static class RequestValidator
{
    public const string EmailField = "email";
    public const string DisplayNameField = "displayName";
    public const string PasswordField = "password";
    public const string TitleField = "title";
    public const string DescriptionField = "description";
    public const string DueDateField = "dueDate";
    public const string StatusField = "status";

    public const string EmailRequiredMessage = "Email is required.";
    public const string EmailTooLongMessage = "Email must be at most 254 characters.";
    public const string EmailSingleAtMessage = "Email must contain a single @ character.";
    public const string EmailDomainMessage = "Email domain is not valid.";
    public const string EmailWhitespaceMessage = "Email must not contain whitespace.";
    public const string PasswordRequiredMessage = "Password is required.";
    public const string PasswordLengthMessage = "Password must be between 8 and 128 characters.";
    public const string PasswordComplexityMessage = "Password must contain at least one letter and one digit.";
    public const string DisplayNameMessage = "Display name must be between 2 and 80 characters.";
    public const string TitleMessage = "Title must be between 3 and 120 characters.";
    public const string DescriptionMessage = "Description must be at most 1000 characters.";
    public const string DueDateMessage = "Due date cannot be in the past.";
    public const string StatusRequiredMessage = "Status is required.";
    public const string StatusUnknownMessage = "Status must be one of: Todo, InProgress, Done.";

    private const int PasswordMinLength = 8;
    private const int PasswordMaxLength = 128;
    private const int EmailMinLength = 5;

    public static IReadOnlyDictionary<string, string[]> ValidateRegister(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        ValidateEmail(request.Email, errors);
        ValidatePassword(request.Password, errors);
        ValidateDisplayName(request.DisplayName, errors);

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[EmailField] = [EmailRequiredMessage];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors[PasswordField] = [PasswordRequiredMessage];
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateTaskDetails(
        string? title,
        string? description,
        DateOnly? dueDate,
        DateOnly today,
        DateOnly? currentDueDate)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var candidateTitle = (title ?? string.Empty).Trim();

        if (candidateTitle.Length is < TaskItem.TitleMinLength or > TaskItem.TitleMaxLength)
        {
            errors[TitleField] = [TitleMessage];
        }

        var candidateDescription = description?.Trim();

        if (candidateDescription is not null && candidateDescription.Length > TaskItem.DescriptionMaxLength)
        {
            errors[DescriptionField] = [DescriptionMessage];
        }

        if (dueDate is not null && dueDate.Value < today && dueDate != currentDueDate)
        {
            errors[DueDateField] = [DueDateMessage];
        }

        return errors;
    }

    public static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool TryParseStatus(string? value, out TaskItemStatus status)
    {
        status = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();

        foreach (var name in Enum.GetNames<TaskItemStatus>())
        {
            if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
            {
                status = Enum.Parse<TaskItemStatus>(name);
                return true;
            }
        }

        return false;
    }

    private static void ValidateEmail(string? email, IDictionary<string, string[]> errors)
    {
        var candidate = (email ?? string.Empty).Trim();

        if (candidate.Length == 0)
        {
            errors[EmailField] = [EmailRequiredMessage];
            return;
        }

        if (candidate.Length > User.EmailMaxLength)
        {
            errors[EmailField] = [EmailTooLongMessage];
            return;
        }

        if (candidate.Any(char.IsWhiteSpace))
        {
            errors[EmailField] = [EmailWhitespaceMessage];
            return;
        }

        var atIndex = candidate.IndexOf('@', StringComparison.Ordinal);

        if (atIndex <= 0 || atIndex != candidate.LastIndexOf('@'))
        {
            errors[EmailField] = [EmailSingleAtMessage];
            return;
        }

        var domain = candidate[(atIndex + 1)..];

        if (candidate.Length < EmailMinLength || domain.Length < 3 || !domain.Contains('.', StringComparison.Ordinal))
        {
            errors[EmailField] = [EmailDomainMessage];
        }
    }

    private static void ValidatePassword(string? password, IDictionary<string, string[]> errors)
    {
        var candidate = password ?? string.Empty;

        if (candidate.Length == 0)
        {
            errors[PasswordField] = [PasswordRequiredMessage];
            return;
        }

        if (candidate.Length is < PasswordMinLength or > PasswordMaxLength)
        {
            errors[PasswordField] = [PasswordLengthMessage];
            return;
        }

        if (!candidate.Any(char.IsLetter) || !candidate.Any(char.IsDigit))
        {
            errors[PasswordField] = [PasswordComplexityMessage];
        }
    }

    private static void ValidateDisplayName(string? displayName, IDictionary<string, string[]> errors)
    {
        var candidate = (displayName ?? string.Empty).Trim();

        if (candidate.Length is < User.DisplayNameMinLength or > User.DisplayNameMaxLength)
        {
            errors[DisplayNameField] = [DisplayNameMessage];
        }
    }
}
