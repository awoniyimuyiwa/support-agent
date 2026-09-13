using Application.Contracts.BackgroundJobs;
using Domain.Shared.BackgroundJobs;

namespace Application.BackgroundJobs.Handlers;

[BackgroundJobHandler(BackgroundJobType.Email)]
class EmailBackgroundJobHandler : BackgroundJobHandler<EmailBackgroundJobHandlerDto>
{
    protected override Task Handle(
        Guid id,
        EmailBackgroundJobHandlerDto data, 
        CancellationToken cancellationToken = default)
    {
        var metadata = new { IdempotencyKey = id.ToString() };

        return Task.CompletedTask;
    }
}
