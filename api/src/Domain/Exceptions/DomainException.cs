namespace BallastLane.Domain.Exceptions;

/// <summary>
/// Raised when an operation would leave an entity in an invalid state.
/// Input coming from a client is validated before it reaches the domain,
/// so this exception signals a programming error rather than user error.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
