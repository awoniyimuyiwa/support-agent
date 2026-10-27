using Domain.Shared.BackgroundJobs;

namespace Application.Contracts.BackgroundJobs;

public interface IBackgroundJobTypeRegistry
{
    BackgroundJobType GetType(BackgroundJobPayload dto);
}
