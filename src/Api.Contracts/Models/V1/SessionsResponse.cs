namespace Api.Contracts.Models.V1;

public record SessionsResponse
{
    public  IEnumerable<UserSessionResponse> Sessions { get; init; } = [];
}
