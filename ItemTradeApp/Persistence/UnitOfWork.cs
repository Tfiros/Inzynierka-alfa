using Microsoft.EntityFrameworkCore.Storage;

namespace ItemTradeApp.Persistence;

public interface IUnitOfWork
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<bool> RunInSavepointAsync(string savepointName, Func<Task<bool>> work, CancellationToken ct = default);
}

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => dbContext.Database.BeginTransactionAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => dbContext.SaveChangesAsync(ct);

    public async Task<bool> RunInSavepointAsync(string savepointName, Func<Task<bool>> work, CancellationToken ct = default)
    {
        var tx = dbContext.Database.CurrentTransaction;
        if (tx is null)
        {
            throw new InvalidOperationException($"RunInSavepointAsync must be called inside a transaction - issue for {savepointName}");
        }
        await tx.CreateSavepointAsync(savepointName, ct);
        try
        {
            var result = await work();
            if (!result)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
            }
            return result;
        }
        catch
        {
            await tx.RollbackToSavepointAsync(savepointName, ct);
            throw;
        }
    }
}