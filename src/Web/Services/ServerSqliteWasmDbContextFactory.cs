using Microsoft.EntityFrameworkCore;
using SqliteWasmHelper;

namespace Web.Services;

/// <summary>
/// Mock to satisfy dependency injection on the server for components using SqliteWasmHelper.
/// </summary>
public class ServerSqliteWasmDbContextFactory<TContext>(IDbContextFactory<TContext> dbContextFactory)
    : ISqliteWasmDbContextFactory<TContext> where TContext : DbContext
{
    public Task<TContext> CreateDbContextAsync() => dbContextFactory.CreateDbContextAsync();

    public TContext CreateDbContext() => dbContextFactory.CreateDbContext();
  
    public Task<int> ManualRestore(byte[] arrayBuffer)
    {
        // Safely returns a completed Task containing 0 to ensure server pre-rendering remains crash-free.
        return Task.FromResult(0);
    }
}
