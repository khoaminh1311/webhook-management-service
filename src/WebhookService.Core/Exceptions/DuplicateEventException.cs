namespace WebhookService.Core.Exceptions;

public class DuplicateEventException : Exception
{
    public DuplicateEventException(string message) : base(message)
    {
    }

    public DuplicateEventException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
