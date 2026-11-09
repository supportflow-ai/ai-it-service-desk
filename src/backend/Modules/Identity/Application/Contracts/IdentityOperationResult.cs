namespace ServiceDesk.Modules.Identity.Application.Contracts;

public sealed record IdentityOperationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static IdentityOperationResult Success() => new(true, []);
    public static IdentityOperationResult Failed(params string[] errors) => new(false, errors);
    public static IdentityOperationResult Failed(IEnumerable<string> errors) => new(false, errors.ToArray());
}
