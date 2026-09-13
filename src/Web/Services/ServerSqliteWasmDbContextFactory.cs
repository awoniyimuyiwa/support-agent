using Microsoft.EntityFrameworkCore;
using SqliteWasmHelper;
using System;
using System.Threading.Tasks;

namespace Web.Services;

/// <summary>
/// Passive mock to satisfy property injection checks on the server for components using SqliteWasmHelper.
/// </summary>
public class ServerSqliteWasmDbContextFactory<TContext>(IDbContextFactory<TContext> dbContextFactory)
    : ISqliteWasmDbContextFactory<TContext> where TContext : DbContext
{
    public Task<TContext> CreateDbContextAsync() => dbContextFactory.CreateDbContextAsync();

    public TContext CreateDbContext() => dbContextFactory.CreateDbContext();

    /// <summary>
    /// Passive implementation of the restore routine to satisfy package contract upgrades on the server.
    /// </summary>
    /// <returns>A completed task representing zero records modified on the server preview.</returns>
    public Task<int> ManualRestore(byte[] arrayBuffer)
    {
        // Safely returns a completed Task containing 0 to ensure server pre-rendering remains crash-free.
        return Task.FromResult(0);
    }
}
