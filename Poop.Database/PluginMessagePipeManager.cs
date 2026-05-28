using System;
using MessagePipe;
using Microsoft.Extensions.Logging;

namespace Prefix.Poop.Database;

/// <summary>
/// Plugin-scoped message pipe subscription manager for Poop.Database
/// Manages DisposableBag independently from the core Poop MessagePipeManager
/// Each plugin has its own bag for subscription tracking and cleanup
/// </summary>
internal sealed class PluginMessagePipeManager(ILogger<PluginMessagePipeManager> _logger)
    : IDisposable
{
    private readonly DisposableBagBuilder _disposableBag = DisposableBag.CreateBuilder();
    private IDisposable? _builtBag;

    /// <summary>
    /// Gets the DisposableBag builder for adding subscriptions.
    /// Usage: subscriber.Subscribe(x => {}).AddTo(manager.SubscriptionBag);
    /// All subscriptions added to this bag will be disposed on plugin shutdown.
    /// </summary>
    public DisposableBagBuilder SubscriptionBag
    {
        get { return _disposableBag; }
    }

    public void Dispose()
    {
        if (_builtBag != null)
            return;

        try
        {
            _builtBag = _disposableBag.Build();
            _builtBag.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Poop.Database] Error disposing plugin subscriptions");
        }
    }
}
