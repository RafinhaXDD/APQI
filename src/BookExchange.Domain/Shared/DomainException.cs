namespace BookExchange.Domain.Shared;

/// <summary>
/// Thrown when an operation would break a business rule (e.g. an illegal state transition).
/// The API maps it to 409 Conflict. Messages must be safe to show to the caller.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
