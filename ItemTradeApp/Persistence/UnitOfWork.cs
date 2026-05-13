using Microsoft.EntityFrameworkCore.Storage;

namespace ItemTradeApp.Persistence;

public interface IUnitOfWork
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => dbContext.Database.BeginTransactionAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => dbContext.SaveChangesAsync(ct);
}