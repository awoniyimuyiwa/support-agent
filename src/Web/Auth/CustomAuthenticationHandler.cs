using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Web.Client;

namespace Web.Auth;

public class CustomAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ApiClient apiClient,
    IHttpContextAccessor httpContextAccessor) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string AuthSchemeName = "BFFAuth";

    readonly ILogger<CustomAuthenticationHandler> _logger = loggerFactory.CreateLogger<CustomAuthenticationHandler>();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return AuthenticateResult.NoResult();
        }

        var path = httpContext.Request.Path.Value ?? string.Empty;

        // By default, app.UseAuthentication() forces the handler to evaluate every request.
        // Short-circuit structural static files, health signals, and downstream API requests.
        // CRITICAL: DO NOT bypass /_blazor or /_framework here because .NET 10 needs this handler
        // to authenticate the circuit request so it can execute state serialization
        if (path.Contains('.')
            || path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        // From this point down, the request is a valid top-level Blazor navigation or state payload evaluation pass
        if (httpContext.Request.Headers.ContainsKey("Cookie"))
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("[SERVER MIDDLEWARE] Processing security handshake context for: {Path}", path);
                }

                var userInfo = await apiClient.GetUserInfo();
                if (userInfo is not null)
                {
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, userInfo.Id.ToString()),
                        new(ClaimTypes.GivenName, userInfo.FirstName),
                        new(ClaimTypes.Surname, userInfo.LastName),
                        new(ClaimTypes.Email, userInfo.Email ?? string.Empty),
                        new("email_confirmed", userInfo.IsEmailConfirmed.ToString().ToLowerInvariant())
                    };

                    if (!string.IsNullOrWhiteSpace(userInfo.UserName)) claims.Add(new Claim(ClaimTypes.Name, userInfo.UserName));
                    if (userInfo.LockoutEnd.HasValue) claims.Add(new Claim("lockout_end", userInfo.LockoutEnd.Value.ToString("o")));

                    if (userInfo.Roles is not null)
                    {
                        foreach (var role in userInfo.Roles)
                        {
                            if (!string.IsNullOrWhiteSpace(role))
                            {
                                claims.Add(new Claim(ClaimTypes.Role, role));
                            }
                        }
                    }

                    var identity = new ClaimsIdentity(
                        claims,
                        AuthSchemeName,
                        ClaimTypes.Name,
                        ClaimTypes.Role);

                    var principal = new ClaimsPrincipal(identity);
                    var ticket = new AuthenticationTicket(principal, AuthSchemeName);

                    // Returning success here is what enables .NET 10 to auto-serialize user info data down to WASM
                    return AuthenticateResult.Success(ticket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[SERVER MIDDLEWARE] Session token validation failed: {Message}", ex.Message);
            }
        }

        return AuthenticateResult.NoResult();
    }
}
