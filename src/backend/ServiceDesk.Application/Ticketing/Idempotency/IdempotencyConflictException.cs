namespace ServiceDesk.Application.Ticketing.Idempotency;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("Idempotency-Key was already used with a different ticket payload.") { }
}

public sealed class IdempotencyInProgressException : Exception
{
    public IdempotencyInProgressException(Exception innerException)
        : base("A request with this Idempotency-Key is still being processed. Retry with the same key and payload.", innerException) { }
}
