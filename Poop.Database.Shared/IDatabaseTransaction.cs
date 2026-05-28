using System;
using System.Threading.Tasks;

namespace Prefix.Poop.Database.Shared;

/// <summary>
/// Database transaction scope. Auto-rolls back on dispose if not committed.
/// </summary>
public interface IDatabaseTransaction : IAsyncDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
}
