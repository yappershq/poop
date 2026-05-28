using System.Threading.Tasks;
using LiteDB;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

internal sealed class LiteDbTransaction : IDatabaseTransaction
{
    private readonly LiteDatabase _db;
    private bool _completed;

    public LiteDbTransaction(LiteDatabase db)
    {
        _db = db;
        _db.BeginTrans();
    }

    public Task CommitAsync()
    {
        return Task.Run(() =>
        {
            _db.Commit();
            _completed = true;
        });
    }

    public Task RollbackAsync()
    {
        return Task.Run(() =>
        {
            _db.Rollback();
            _completed = true;
        });
    }

    public ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            _db.Rollback();
        }

        return ValueTask.CompletedTask;
    }
}
