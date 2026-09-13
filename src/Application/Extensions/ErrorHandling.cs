using Domain.Shared;

namespace Application.Extensions;

public static class ErrorHandling
{
    extension(Exception exception)
    {
        /// <summary>
        /// Gets a safely truncated string representation of the exception to prevent log bloat.
        /// </summary>
        public string TruncatedString
        {
            get
            {
                var exceptionString = exception.ToString();

                return exceptionString.Length > Constants.MaxExceptionLength
                    ? string.Concat(
                        exceptionString.AsSpan(0, Constants.MaxExceptionLength - Constants.TruncationIndicator.Length),
                        Constants.TruncationIndicator)
                    : exceptionString;
            }
        }
    }
}

