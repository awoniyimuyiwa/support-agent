namespace Web.Http;

public class CookieDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        // If an active cookie header exists in the incoming browser request, append it to the backend call!
        if (httpContext is not null 
            && httpContext.Request.Headers.TryGetValue("Cookie", out var cookies))
        {
            request.Headers.Add("Cookie", [.. cookies]);
        }

        return base.SendAsync(request, cancellationToken);
    }
}