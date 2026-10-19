namespace ServiceDesk.Application.Common.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message = "Conflict") : base(message)
    {
    }
}
