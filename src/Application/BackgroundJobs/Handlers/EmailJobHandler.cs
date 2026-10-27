using Application.Contracts.BackgroundJobs;
using Domain.Shared.BackgroundJobs;

namespace Application.BackgroundJobs.Handlers;

[BackgroundJobHandler(BackgroundJobType.Email)]
class EmailJobHandler : BackgroundJobHandler<EmailJobPayload>
{
    protected override Task Handle(
        Guid id,
        EmailJobPayload data, 
        CancellationToken cancellationToken = default)
    {
        var metadata = new { IdempotencyKey = id.ToString() };

        return Task.CompletedTask;
    }
}
