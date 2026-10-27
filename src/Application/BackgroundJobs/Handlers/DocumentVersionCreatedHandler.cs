using Application.Contracts.BackgroundJobs;
using Domain.Shared.BackgroundJobs;

namespace Application.BackgroundJobs.Handlers;

[BackgroundJobHandler(BackgroundJobType.DocumentVersionCreated)]
class DocumentVersionCreatedHandler : BackgroundJobHandler<DocumentVersionCreated>
{
    protected override Task Handle(
        Guid id,
        DocumentVersionCreated data,
        CancellationToken cancellationToken = default)
    {
        var metadata = new { IdempotencyKey = id.ToString() };

        return Task.CompletedTask;
    }
}
