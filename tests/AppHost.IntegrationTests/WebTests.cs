using Microsoft.Extensions.Logging;

namespace AppHost.IntegrationTests;

public class WebTests
{
    /// <summary>
    /// Allows at least 5 minutes to give the cloud pipeline runner the headroom it needs 
    /// to download and extract custom KeyVault and AppConfig emulator container layers safely.
    /// </summary>   
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task GetWebResourceRoot_ReturnsOk()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
            args: [
                "--environment=Testing",
                
                // Force Docker to isolate these test containers away from that of dev environment
                "--app-host-project-name=supportagent-tests",

                // Pass the exact parameter override formats. This tells Aspire:
                // "I am providing the passwords explicitly. Do not generate random ones and do not write to secrets.json
                "Parameters:postgres-password=SecretTestPassword123!",
                "Parameters:redis-password=SecretTestPassword123!"],

            cancellationToken);

        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
            // To output logs to the xUnit.net ITestOutputHelper, consider adding a package from https://www.nuget.org/packages?q=xunit+logging
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost
            .BuildAsync(cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        await app.StartAsync(cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        var httpClient = app.CreateHttpClient("web");
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("web", cancellationToken)
            .WaitAsync(DefaultTimeout, 
            cancellationToken);

        // Act        
        var response = await httpClient.GetAsync("/", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
