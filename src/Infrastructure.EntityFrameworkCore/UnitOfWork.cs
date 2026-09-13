using Domain;
using Domain.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Infrastructure.EntityFrameworkCore;

class UnitOfWork(ApplicationDbContext applicationDbContext) : IUnitOfWork, IDisposable
{
    readonly ApplicationDbContext _applicationDbContext = applicationDbContext;

    IDbContextTransaction? _currentTransaction;

    public async Task BeginTransaction(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null) return;

        _currentTransaction = await _applicationDbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransaction(CancellationToken cancellationToken = default)
    {
        try
        {
            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            if (_currentTransaction is not null) await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await RollbackTransaction(cancellationToken);
            HandleConcurrencyException(ex);
            throw;
        }
        catch (DbUpdateException ex) when (ex.InnerException is NpgsqlException npgEx)
        {
            await RollbackTransaction(cancellationToken);
            HandleTriggerAndCheckConstraintError(npgEx);
            throw;
        }
        catch
        {
            await RollbackTransaction(cancellationToken);
            throw;
        }
        finally
        {
            DisposeTransaction();
        }
    }

    public bool HasBackgroundJobs()
    {
        return _applicationDbContext.ChangeTracker.Entries<BackgroundJob>()
            .Any(e => e.State == EntityState.Added);
    }

    public async Task RollbackTransaction(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            DisposeTransaction();
        }
    }

    public async Task SaveChanges(CancellationToken cancellationToken = default)
    {
        try
        {
            await _applicationDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            HandleConcurrencyException(ex);
            throw;
        }
        catch (DbUpdateException ex) when (ex.InnerException is NpgsqlException sqlEx)
        {
            HandleTriggerAndCheckConstraintError(sqlEx);
            throw;
        }
    }

    public void Dispose() => _applicationDbContext.Dispose();

    private static void HandleTriggerAndCheckConstraintError(NpgsqlException npgEx)
    {
        // Do necessary checks here to determine if the exception is due to a trigger or check constraint violation
        // and convert to a more meaningful domain exception
        // e.g throw new DomainException("Message");
        throw new DomainException($"Database error: {npgEx.Message}");
    }

    private static void HandleConcurrencyException(DbUpdateConcurrencyException ex)
    {
        var entry = ex.Entries[0];
        var entityName = entry.Entity.GetType().Name;
        var entityId = entry.Property("ExternalId").CurrentValue;
        var databaseValues = entry.GetDatabaseValues();

        throw new ConcurrencyException(
            entityName,
            entityId ?? "",
            databaseValues?.ToObject() ?? "");
    }

    private void DisposeTransaction()
    {
        _currentTransaction?.Dispose();

        _currentTransaction = null;
    }
}
