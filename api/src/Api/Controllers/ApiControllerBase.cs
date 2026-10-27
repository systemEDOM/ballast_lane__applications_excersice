using System.Security.Claims;
using BallastLane.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BallastLane.Api.Controllers;

/// <summary>
/// Shared translation between application results and HTTP responses.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> FromResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);

    protected IActionResult FromResult(Result result)
        => result.IsSuccess ? NoContent() : ToProblem(result.Error!);

    protected ObjectResult UnauthorizedProblem()
        => ToProblem(new Error("unauthorized", "A valid bearer token is required.", ErrorType.Unauthorized));

    protected bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }

    private ObjectResult ToProblem(Error error)
    {
        var status = StatusFor(error.Type);

        if (error.Type == ErrorType.Validation)
        {
            var errors = error.FieldErrors is { Count: > 0 }
                ? error.FieldErrors.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, string[]> { ["request"] = [error.Message] };

            var validation = new ValidationProblemDetails(errors)
            {
                Status = status,
                Title = "One or more validation errors occurred.",
                Type = $"https://httpstatuses.io/{status}"
            };

            validation.Extensions["code"] = error.Code;

            return new BadRequestObjectResult(validation);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(error.Type),
            Detail = error.Message,
            Type = $"https://httpstatuses.io/{status}"
        };

        problem.Extensions["code"] = error.Code;

        if (error.Type == ErrorType.Unauthorized)
        {
            Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
        }

        return new ObjectResult(problem) { StatusCode = status };
    }

    private static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "One or more validation errors occurred.",
        ErrorType.NotFound => "The requested resource was not found.",
        ErrorType.Conflict => "The request conflicts with the current state of the resource.",
        ErrorType.Unauthorized => "Authentication is required.",
        ErrorType.Forbidden => "You are not allowed to perform this action.",
        _ => "An unexpected error occurred."
    };
}
