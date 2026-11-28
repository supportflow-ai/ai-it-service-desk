namespace ServiceDesk.Modules.Ticketing.Application.ReconcileAttachments;

public sealed record AttachmentReconciliationCommand(string Prefix, string? ContinuationToken, TimeSpan Retention);
public sealed record AttachmentReconciliationResult(int Examined, int Deleted, int Failed, int Pruned, string? ContinuationToken);

public interface IReconcileAttachments
{
    Task<AttachmentReconciliationResult> ExecuteAsync(AttachmentReconciliationCommand command, CancellationToken cancellationToken = default);
}
