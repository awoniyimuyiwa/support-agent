namespace Infrastructure.EntityFrameworkCore.Infrastructure.EntityFrameworkCore
{
    [Serializable]
    internal class IdempotentOperationConflictException : Exception
    {
        public IdempotentOperationConflictException()
        {
        }

        public IdempotentOperationConflictException(string? message) : base(message)
        {
        }

        public IdempotentOperationConflictException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}