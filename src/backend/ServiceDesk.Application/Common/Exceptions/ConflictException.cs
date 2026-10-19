namespace ServiceDesk.Application.Common.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message = "Conflict") : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
