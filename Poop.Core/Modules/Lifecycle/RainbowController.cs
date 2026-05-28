using System;
using System.Collections.Generic;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Messages;
using Sharp.Shared.CStrike;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Prefix.Poop.Modules.Lifecycle;

/// <summary>
/// Drives rainbow colour cycling for poop entities that requested it.
/// Runs a 0.1 s repeating timer that advances a global HSV hue and writes
/// m_clrRender + NetworkStateChanged on every tracked entity.
/// </summary>
internal sealed class RainbowController : IModule
{
    private readonly InterfaceBridge              _bridge;
    private readonly ILogger<RainbowController>   _logger;
    private readonly PoopConfig                   _config;

    // Active rainbow poops: entity → spawn time (for stale cleanup)
    private readonly Dictionary<IBaseEntity, DateTime> _rainbowPoops = new();

    // Repeating timer handle; null when idle
    private Guid? _timerHandle;

    // HSV hue shared across all rainbow poops this frame
    private float _currentHue;

    // Cache 360 integer-degree buckets to avoid recalculating per frame
    private static readonly Dictionary<int, Color32> HsvCache    = new();
    private static readonly object                   HsvCacheLock = new();

    private const double UpdateInterval     = 0.1;  // seconds
    private const double MaxPoopAgeMinutes  = 5.0;

    private System.IDisposable? _subscriptions;

    public RainbowController(
        InterfaceBridge              bridge,
        ILogger<RainbowController>   logger,
        PoopConfig                   config,
        ISubscriber<RoundEndMessage> roundEnd)
    {
        _bridge = bridge;
        _logger = logger;
        _config = config;

        var bag = DisposableBag.CreateBuilder();
        roundEnd.Subscribe(_ => ClearAll()).AddTo(bag);
        _subscriptions = bag.Build();
    }

    public bool Init() => true;

    public void Shutdown()
    {
        StopTimer();
        _rainbowPoops.Clear();
        _currentHue = 0f;
        _subscriptions?.Dispose();
        _subscriptions = null;
    }

    // ── public surface ───────────────────────────────────────────────────────

    /// <summary>
    /// Begin cycling colours on <paramref name="entity"/>. No-op if rainbow is disabled in config.
    /// </summary>
    public void TrackRainbowPoop(IBaseEntity entity)
    {
        if (!_config.Color.EnableRainbowPoops)
            return;

        if (!entity.IsValid())
            return;

        _rainbowPoops[entity] = DateTime.UtcNow;
        StartTimerIfNeeded();
    }

    /// <summary>Number of currently tracked rainbow poops.</summary>
    public int TrackedCount => _rainbowPoops.Count;

    /// <summary>Clears all rainbow tracking (called on round end and shutdown).</summary>
    public void ClearAll()
    {
        _rainbowPoops.Clear();
        _currentHue = 0f;
        StopTimer();
    }

    // ── timer ────────────────────────────────────────────────────────────────

    private void StartTimerIfNeeded()
    {
        if (_timerHandle.HasValue)
            return;

        _timerHandle = _bridge.ModSharp.PushTimer(() =>
        {
            UpdateRainbowColors();
            return TimerAction.Continue;
        }, UpdateInterval, GameTimerFlags.Repeatable | GameTimerFlags.StopOnRoundEnd);
    }

    private void StopTimer()
    {
        if (!_timerHandle.HasValue)
            return;

        if (_bridge.ModSharp.IsValidTimer(_timerHandle.Value))
            _bridge.ModSharp.StopTimer(_timerHandle.Value);

        _timerHandle = null;
    }

    // ── colour update ────────────────────────────────────────────────────────

    private void UpdateRainbowColors()
    {
        if (_rainbowPoops.Count == 0)
        {
            StopTimer();
            return;
        }

        float speedMul    = GetSpeedMultiplierForHue(_currentHue);
        float hueInc      = _config.Color.RainbowAnimationSpeed * speedMul;
        _currentHue      += hueInc;
        if (_currentHue >= 360f) _currentHue = 0f;

        var color = HsvToRgbCached(_currentHue, 1f, 1f);
        var now   = DateTime.UtcNow;
        var stale = new List<IBaseEntity>();

        foreach (var kvp in _rainbowPoops)
        {
            var entity    = kvp.Key;
            var spawnTime = kvp.Value;

            if (!entity.IsValid())
            {
                stale.Add(entity);
                continue;
            }

            if ((now - spawnTime).TotalMinutes > MaxPoopAgeMinutes)
            {
                _logger.LogDebug("[Poop] Rainbow poop stale ({Age:F1} min), removing", (now - spawnTime).TotalMinutes);
                stale.Add(entity);
                continue;
            }

            try
            {
                if (entity is IBaseModelEntity modelEntity)
                {
                    modelEntity.RenderColor = color;

                    if (entity is ISchemaObject schemaObj)
                        schemaObj.NetworkStateChanged("m_clrRender");
                }
                else
                {
                    _logger.LogWarning("[Poop] Rainbow entity is not IBaseModelEntity, dropping");
                    stale.Add(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Poop] Error updating rainbow colour for entity {Handle}", entity.Handle);
                stale.Add(entity);
            }
        }

        foreach (var e in stale)
            _rainbowPoops.Remove(e);

        if (_rainbowPoops.Count == 0)
            StopTimer();
    }

    // ── colour math ──────────────────────────────────────────────────────────

    private static float GetSpeedMultiplierForHue(float hue) => hue switch
    {
        >= 0   and < 60  => 0.6f,
        >= 60  and < 120 => 2.0f,
        >= 120 and < 180 => 1.5f,
        >= 180 and < 240 => 1.5f,
        >= 240 and < 300 => 1.5f,
        >= 300 and < 360 => 0.5f,
        _                => 0.1f,
    };

    private static Color32 HsvToRgbCached(float h, float s, float v)
    {
        int key = (int)MathF.Round(h);
        lock (HsvCacheLock)
        {
            if (HsvCache.TryGetValue(key, out var cached))
                return cached;

            var c = HsvToRgb(h, s, v);
            HsvCache[key] = c;
            if (HsvCache.Count > 360) HsvCache.Clear();
            return c;
        }
    }

    private static Color32 HsvToRgb(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1 - MathF.Abs((h / 60f) % 2 - 1));
        float m = v - c;

        float r, g, b;
        if      (h < 60)  { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else              { r = c; g = 0; b = x; }

        return new Color32(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255),
            255);
    }
}
