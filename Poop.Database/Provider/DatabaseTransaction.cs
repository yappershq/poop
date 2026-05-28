using System.Threading.Tasks;
using SqlSugar;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

internal sealed class DatabaseTransaction(ISqlSugarClient db) : IDatabaseTransaction
{
    private bool _completed;

    public async Task CommitAsync()
    {
        await db.Ado.CommitTranAsync();
        _completed = true;
    }

    public async Task RollbackAsync()
    {
        await db.Ado.RollbackTranAsync();
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            await db.Ado.RollbackTranAsync();
        }
    }
}
