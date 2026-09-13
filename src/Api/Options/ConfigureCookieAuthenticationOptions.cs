using Api.Contracts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Api.Options;

public class ConfigureCookieAuthenticationOptions(
    IOptions<SecurityOptions> securityOptions,
    ITicketStore sessionStore)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(
        string? name,
        CookieAuthenticationOptions options)
    {
        options.Cookie.Name = Api.Contracts.Constants.AuthCookieName;

        options.Cookie.Path = "/";

        // Forces Secure = true (HTTPS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // Lax Permits safe cross-site deep links (bookmarks, email notifications) to attach the session cookie on entry.
        options.Cookie.SameSite = SameSiteMode.Strict; 

        options.ExpireTimeSpan = securityOptions.Value.CookieTimeout;

        options.SessionStore = sessionStore;

        // Fires when the user explicitly logs out or the session is invalidated.
        options.Events.OnSigningOut = context =>
        {
            // Manually delete the JS-readable token
            context.HttpContext.Response.Cookies.Delete(Contracts.Constants.AntiforgeryJSReadableCookieName);

            // Manually delete the internal Antiforgery cookie 
            // (Don't trust the framework to do it 'automatically' with __Host- prefixes)
            context.HttpContext.Response.Cookies.Delete(
                Constants.AntiforgeryCookieName,
                new CookieOptions
                {
                    HttpOnly = true,
                    Path = "/",
                    Secure = true
                });

            return Task.CompletedTask;
        };
    }
}

