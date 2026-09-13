using Microsoft.AspNetCore.Antiforgery;

namespace Api.Extensions;

public static class HttpPipeline
{
    extension(HttpContext httpContext)
    {
        /// <summary>
        /// Generates and stores the antiforgery tokens within the current request.
        /// This operation is idempotent for a single session; calling this multiple times 
        /// will yield valid tokens as long as the user's identity (e.g., NameIdentifier claim) 
        /// remains unchanged.
        /// </summary>
        public void GetAndStoreAntiforgeryToken()
        {
            var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
            var tokens = antiforgery.GetAndStoreTokens(httpContext);

            httpContext.Response.Cookies.Append(
                Contracts.Constants.AntiforgeryJSReadableCookieName,
                tokens.RequestToken!,
                new CookieOptions
                {
                    HttpOnly = false,
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                    Secure = true
                });
        }
    }
}

