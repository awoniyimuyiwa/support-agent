using System.Net.Mime;

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

    /// <summary>
    /// Centralized list of supported MIME types permitted for file uploads and stream processing.
    /// Restricted strictly to formats with deterministic magic byte headers (PDF and DOCX) to ensure robust signature validation.
    /// </summary>
    public static readonly string[] AllowedFileTypes = [
        MediaTypeNames.Application.Pdf,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ];
}
