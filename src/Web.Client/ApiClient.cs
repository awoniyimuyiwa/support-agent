using Api.Contracts;
using Api.Contracts.Models.V1;
using System.Net.Http.Json;
using Web.Client.Models;

namespace Web.Client;

public class ApiClient(HttpClient httpClient)
{
    /// <summary>
    /// Calls server to set the anti-forgery token cookie
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task<HttpResponseMessage> GetAntiforgeryToken(
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetAsync(
            "v1/auth/antiforgery-token",
            cancellationToken);
    }

    /// <summary>
    /// Fetches the current user authentication context and permission maps.
    /// </summary>
    public async Task<UserInfoRespone?> GetUserInfo(
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<UserInfoRespone>(
            "v1/account/info",
            cancellationToken);
    }

    public async Task<HttpResponseMessage> GetUserInfoResponse(
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetAsync( 
            "v1/account/info", 
            cancellationToken);
    }

    public async Task<WeatherForecastSyncItem[]> StreamWeatherForecasts(
        int maxItems = 10, 
        CancellationToken cancellationToken = default)
    {
        List<WeatherForecastSyncItem>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<WeatherForecastSyncItem>("v1/weather-forecasts", cancellationToken))
        {
            if (forecasts?.Count >= maxItems)
            {
                break;
            }
            if (forecast is not null)
            {
                forecasts ??= [];
                forecasts.Add(forecast);
            }
        }

        return forecasts?.ToArray() ?? [];
    }

    public async Task<HttpResponseMessage> Login(
        LoginModel loginModel,
        CancellationToken cancellationToken = default)
    {
        return await httpClient.PostAsJsonAsync(
            "v1/auth/login?useCookies=true",
            loginModel,
            cancellationToken);
    }

    public async Task<HttpResponseMessage> Logout(
        CancellationToken cancellationToken = default)
    {
        return await httpClient.PostAsync(
            "v1/auth/logout",
            null,
            cancellationToken);
    }

    /// <summary>
    /// Fetches incremental ticket adjustments and tombstones from the server using an opaque encrypted cursor token.
    /// </summary>
    public async Task<HttpResponseMessage> StreamTicketDeltas(
        string? next,
        CancellationToken token = default)
    {
        var url = $"v1/tickets/stream-deltas?pageSize={Domain.Shared.Constants.MaxPageSize}";

        if (!string.IsNullOrWhiteSpace(next))
        {
            // Escape the encrypted string safely to prevent special base64 characters (+, /, =) from breaking URL parsing
            url += $"&next={Uri.EscapeDataString(next)}";
        }

        return await httpClient.GetAsync(url, token);
    }

    /// <summary>
    /// Fetches incremental weather adjustments and tombstones from the server using an opaque encrypted cursor token.
    /// </summary>
    public async Task<HttpResponseMessage> StreamWeatherForecastDeltas(
        string? next,
        CancellationToken cancellationToken = default)
    {
        var url = $"v1/weather-forecasts/stream-deltas?pageSize={Domain.Shared.Constants.MaxPageSize}";

        if (!string.IsNullOrWhiteSpace(next))
        {
            url += $"&next={Uri.EscapeDataString(next)}";
        }

        return await httpClient.GetAsync(url, cancellationToken);
    }

    public async Task<HttpResponseMessage> SyncWeatherForecasts(
        WeatherForecastSyncRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/weather-forecasts/sync")
        {
            Content = JsonContent.Create(batch)
        };

        request.Headers.Add(
            Constants.IdempotencyKeyHeaderName, 
            idempotencyKey.ToString());

        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> SyncTickets(
        TicketSyncRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/tickets/sync")
        {
            Content = JsonContent.Create(batch)
        };

        request.Headers.Add(
            Constants.IdempotencyKeyHeaderName, 
            idempotencyKey.ToString());

        return await httpClient.SendAsync(request, cancellationToken);
    }
}
