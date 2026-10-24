namespace Api.Contracts;

public class Constants
{
    public const string AlreadyExistsErrorMessage = "Already exists";

    public const string DuplicateInRequestErrorMessage = "Duplicate found in request";

    public const string InvalidErrorMessage = "Invalid";

    public const string AuthCookieName = "__Host-Auth";

    public const string AntiforgeryJSReadableCookieName = "XSRF-TOKEN";

    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    public const string IdempotencyKeyHeaderName = "Idempotency-Key";

    public const int MaxLengthOfList = 100;

    public const int TextMaxLength = 4000;
}
