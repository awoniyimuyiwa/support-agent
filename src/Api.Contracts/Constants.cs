using System.Runtime.InteropServices.JavaScript;

namespace Api.Contracts;

public class Constants
{
    public const string AuthCookieName = "__Host-Auth";

    public const string AntiforgeryJSReadableCookieName = "XSRF-TOKEN";

    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    public const string IdempotencyKeyHeaderName = "Idempotency-Key";

    public const int MaxLengthOfList = 100;
}
