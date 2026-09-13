using System.Collections.Immutable;

namespace Domain.Shared;

public class Constants
{
    public const string AdminRoleName = "Admin";

    public const string AuditLogJsonSerializationOptionsName = "AuditLogJsonSerializationOptions";

    public const string BinaryDataMask = "[Binary Data Omitted]";

    public const string Mask = "***";

    public const int MaxExceptionLength = 2000;

    public const int MaxPageSize = 100;

    public const int MaxRowsToSkip = 50000;

    public const int MinPageNumber = 1;
    public const int MinPageSize = 1;
   
    /// <summary>
    /// Min length for string fields
    /// </summary>
    public const int StringMinLength = 3;

    /// <summary>
    /// Max length for string fields
    /// </summary>
    public const int StringMaxLength = 255;

    public const string TruncationIndicator = "...";

    public static readonly ImmutableHashSet<string> SensitiveFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "access_token",
        "acessToken",
        "creditCard",
        "cvv",
        "password",
        "passwordConfirm",
        "paswordHash",
        "secret",
        "securityStamp",
        "token"
    }.ToImmutableHashSet();
}

