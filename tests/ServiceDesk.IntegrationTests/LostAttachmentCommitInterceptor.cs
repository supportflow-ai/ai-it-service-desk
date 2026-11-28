using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.IntegrationTests;

internal sealed class LostAttachmentCommitInterceptor : DbTransactionInterceptor
{
    private int _lost;
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<TicketAttachment>().Any() && Interlocked.Exchange(ref _lost, 1) == 0)
            throw new IOException("injected response loss after committed attachment");
        return Task.CompletedTask;
    }
}
