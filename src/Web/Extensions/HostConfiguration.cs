using Azure.Core;

namespace Web.Extensions;

public static class HostConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddCustomAzureAppConfiguration(
            string appConfigConnectionString,
            TokenCredential tokenCredential,
            bool isAspireEmulator = false)
        {
            builder.Configuration.AddAzureAppConfiguration(options =>
            {
                // Works with emulator in Aspire via AccessKey validation, and production via TokenCredentials
                if (isAspireEmulator)
                {
                    options.Connect(appConfigConnectionString);
                }
                else
                {
                    options.Connect(new Uri(appConfigConnectionString), tokenCredential)       
                    .ConfigureKeyVault(kv => kv.SetCredential(tokenCredential));
                }

                options.ConfigureRefresh(refresh =>
                {
                    // Watch a specific "sentinel" key (e.g., "Settings:Sentinel").        
                    // If its value changes, all other configuration values refresh instantly
                    refresh.Register("Settings:Sentinel", refreshAll: true)
                           .SetRefreshInterval(TimeSpan.FromSeconds(30));
                });
            });

            // Register the native IConfigurationRefresher into DI container so the middleware can find it
            builder.Services.AddAzureAppConfiguration();

            return builder;
        }
    }
}

