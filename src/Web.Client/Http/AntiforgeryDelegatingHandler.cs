using Api.Contracts;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Web.Client.Http;

/// <summary>
/// Attaches the existing browser antiforgery cookie to state-changing HTTP requests.
///
/// ARCHITECTURAL NOTE:
/// 400 Bad Request retry interceptors for mid-session token invalidation (such as if server Data Protection 
/// keys are improperly reset or wiped across deployments without preserving the existing key ring) are intentionally not implemented. 
/// Keeping this handler simple and letting a standard reload or re-authentication resolve rare invalidations 
/// avoids unnecessary complexity.
/// Missing token seeding on startup is handled in Routes.razor, and identity rotations occur on login/logout.
/// </summary>
public class AntiforgeryDelegatingHandler(
    IJSRuntime jsRuntime,
    ILogger<AntiforgeryDelegatingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Antiforgery delegating handler intercepting request processing stream.");

        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        if (request.Method != HttpMethod.Get
            && request.Method != HttpMethod.Head
            && request.Method != HttpMethod.Options
            && request.Method != HttpMethod.Trace)
        {
            if (OperatingSystem.IsBrowser())
            {
                logger.LogInformation("Attempting retrieval of antiforgery verification token out of browser cookie store.");
                try
                {
                    // Fast safety check to ensure JavaScript engine has initialized the method
                    var isFunctionRegistered = await jsRuntime.InvokeAsync<bool>(
                        "eval",
                        cancellationToken,
                        "typeof window.getAntiforgeryToken === 'function'");

                    if (isFunctionRegistered)
                    {
                        var token = await jsRuntime.InvokeAsync<string>("getAntiforgeryToken", cancellationToken);

                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            var headerName = Api.Contracts.Constants.AntiforgeryHeaderName;

                            if (request.Headers.Contains(headerName))
                            {
                                request.Headers.Remove(headerName);
                            }

                            logger.LogInformation("Injecting antiforgery validation header field: {HeaderName}", headerName);
                            request.Headers.Add(headerName, token);
                        }
                        else
                        {
                            logger.LogInformation("Antiforgery token evaluation function executed, but target security cookie token does not exist yet.");
                        }
                    }
                    else
                    {
                        logger.LogInformation("JavaScript method 'window.getAntiforgeryToken' is not physically registered in browser memory layer yet.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInformation("Execution pass for antiforgery security header injection aborted: {ErrorMessage}", ex.Message);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
