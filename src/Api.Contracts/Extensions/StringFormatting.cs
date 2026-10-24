using System.Text.RegularExpressions;

namespace Api.Contracts.Extensions;

public static partial class StringFormatting
{
    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex PascalCaseSplitterRegex();

    extension(string? input)
    {
        /// <summary>
        /// Inserts a space before every capital letter to split clean PascalCase strings.
        /// </summary>
        public string AddSpacesToPascalCase
        {
            get
            {
                if (string.IsNullOrWhiteSpace(input)) return string.Empty;

                return PascalCaseSplitterRegex().Replace(input, "$1 $2").Trim();
            }
        }
    }
}
