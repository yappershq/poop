using System;
using System.Collections.Generic;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Database;
using Prefix.Poop.Messages;
using Prefix.Poop.Modules.Lifecycle;
using Prefix.Poop.Shared.Models;
using Prefix.Poop.Utils;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Vector = Sharp.Shared.Types.Vector;

namespace Prefix.Poop.Modules;

/// <summary>
/// Handles spawning of poop entities with physics, colours, and size variations.
/// Provides two entry points:
/// <list type="bullet">
///   <item><see cref="SpawnPoop"/> — low-level, position + size + colour only.</item>
///   <item><see cref="SpawnPoopWithFullLogic"/> — high-level, derives position from the player pawn,
///   auto-detects victim, plays sounds, shows messages, and logs to the database.</item>
/// </list>
/// </summary>
internal sealed class PoopSpawner : IModule
{
    private readonly InterfaceBridge                  _bridge;
    private readonly ILogger<PoopSpawner>             _logger;
    private readonly DeadPlayerRegistry               _deadPlayerRegistry;
    private readonly RagdollRegistry                  _ragdollRegistry;
    private readonly PoopLifecycleManager             _lifecycleManager;
    private readonly RainbowController                _rainbowController;
    private readonly PoopConfig                       _config;
    private readonly PoopLocale                       _locale;
    private readonly SizeGenerator                    _sizeGenerator;
    private readonly PoopStatsRepository              _stats;
    private readonly IPublisher<PoopSpawnedMessage>   _poopSpawned;
    private readonly Random                           _random = new();

    public PoopSpawner(
        InterfaceBridge                  bridge,
        ILogger<PoopSpawner>             logger,
        DeadPlayerRegistry               deadPlayerRegistry,
        RagdollRegistry                  ragdollRegistry,
        PoopLifecycleManager             lifecycleManager,
        RainbowController                rainbowController,
        PoopConfig                       config,
        PoopLocale                       locale,
        SizeGenerator                    sizeGenerator,
        PoopStatsRepository              stats,
        IPublisher<PoopSpawnedMessage>   poopSpawned)
    {
        _bridge             = bridge;
        _logger             = logger;
        _deadPlayerRegistry = deadPlayerRegistry;
        _ragdollRegistry    = ragdollRegistry;
        _lifecycleManager   = lifecycleManager;
        _rainbowController  = rainbowController;
        _config             = config;
        _locale             = locale;
        _sizeGenerator      = sizeGenerator;
        _stats              = stats;
        _poopSpawned        = poopSpawned;
    }

    public bool Init() => true;

    // ── Low-level spawn ──────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a <c>prop_physics</c> poop entity at <paramref name="position"/>.
    /// </summary>
    /// <param name="position">World spawn position.</param>
    /// <param name="size">Scale multiplier; pass ≤ 0 for a random size.</param>
    /// <param name="color">Colour override; null uses the configured default.</param>
    public SpawnPoopResult SpawnPoop(Vector position, float size = -1f, PoopColorPreference? color = null)
    {
        try
        {
            float poopSize   = size > 0 ? size : _sizeGenerator.GetRandomSize();
            float massFactor = poopSize * 0.05f;
            var   poopColor  = ResolveColor(color);

            int spawnFlags = (int)(
                SpawnFlags.PhysPropDebris |
                SpawnFlags.PhysPropTouch  |
                SpawnFlags.PhysPropForceTouchTriggers);

            var entity = _bridge.EntityManager.SpawnEntitySync<IBaseModelEntity>("prop_physics",
                new Dictionary<string, KeyValuesVariantValueItem>
                {
                    { "model",       _config.Assets.PoopModel },
                    { "spawnflags",  spawnFlags },
                    { "origin",      $"{position.X} {position.Y} {position.Z}" },
                    { "scale",       poopSize },
                    { "massscale",   massFactor },
                    { "inertiascale", massFactor },
                    { "rendercolor", $"{poopColor.Red} {poopColor.Green} {poopColor.Blue}" },
                });

            if (entity == null)
                return new SpawnPoopResult { Entity = null, Size = 0, Position = position };

            entity.SetCollisionGroup(CollisionGroupType.InteractiveDebris);
            entity.CollisionRulesChanged();

            _lifecycleManager.TrackPoop(entity);

            if (poopColor.IsRainbow)
                _rainbowController.TrackRainbowPoop(entity);

            return new SpawnPoopResult { Entity = entity, Size = poopSize, Position = position };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Poop] Error spawning poop entity");
            return new SpawnPoopResult { Entity = null, Size = 0, Position = position };
        }
    }

    // ── High-level spawn ─────────────────────────────────────────────────────

    /// <summary>
    /// Full poop pipeline: derives position from the player's pawn, auto-detects nearest victim,
    /// plays sounds, shows chat messages, logs to the database, and publishes a
    /// <see cref="PoopSpawnedMessage"/>. Must be called on the game thread.
    /// </summary>
    public SpawnPoopResult SpawnPoopWithFullLogic(
        IGameClient?          player,
        float                 size,
        PoopColorPreference   colorPreference,
        bool                  playSounds    = true,
        bool                  showMessages  = true)
    {
        if (player == null || !player.IsValid)
        {
            _logger.LogWarning("[Poop] SpawnPoopWithFullLogic called with invalid player");
            return new SpawnPoopResult();
        }

        var controller = player.GetPlayerController();
        if (controller == null || !controller.IsValid())
        {
            _logger.LogWarning("[Poop] SpawnPoopWithFullLogic: no valid controller for {Name}", player.Name);
            return new SpawnPoopResult();
        }

        var pawn = controller.GetPlayerPawn();
        if (pawn == null || !pawn.IsValid())
        {
            _logger.LogWarning("[Poop] SpawnPoopWithFullLogic: no valid pawn for {Name}", player.Name);
            return new SpawnPoopResult();
        }

        var position       = pawn.GetAbsOrigin();
        var playerName     = player.Name;
        var playerSteamId  = player.SteamId;

        var victimInfo  = FindNearestDeadPlayer(player);
        var victim      = victimInfo?.Player;
        var victimName  = victim?.Name;
        var victimSteamId = victim?.SteamId;

        // Spawn
        var result = SpawnPoop(position, size, colorPreference);

        // Publish outcome regardless of success so command layer can react
        _poopSpawned.Publish(new PoopSpawnedMessage(
            Player:   player,
            Position: position,
            Size:     size,
            Victim:   victim,
            Success:  result.Entity != null));

        if (result.Entity == null)
        {
            _logger.LogError("[Poop] Failed to spawn poop at {Position}", position);
            return result;
        }

        float poopSize = result.Size ?? size;
        bool  isMassive = poopSize >= _config.Size.MassiveAnnouncementThreshold;
        string sizeDesc = _locale.Raw(_sizeGenerator.GetSizeCategoryKey(poopSize));

        // Sounds
        if (playSounds && _config.Sound.EnableSounds)
        {
            PlayPoopSound(result.Entity);

            if (_config.Sound.EnableTauntSounds && victim is { IsFakeClient: false })
                PlayTauntSound(victim);
        }

        // Messages
        if (showMessages && _config.Gameplay.ShowMessageOnPoop)
        {
            if (isMassive)
            {
                _locale.PrintToAll("poop.spawned_massive", playerName, sizeDesc, poopSize);
            }
            else if (victim != null && victimName != null)
            {
                _locale.PrintToAll("poop.spawned_on_player", playerName, victimName, sizeDesc, poopSize);
            }
            else
            {
                _locale.PrintToClient(controller, "poop.spawned_self", sizeDesc, poopSize);
            }
        }

        // DB logging — async, game-thread marshalled
        var currentMap      = _bridge.ModSharp.GetMapName() ?? "unknown";
        var capturedVName   = victimName;
        var capturedVSteamId = victimSteamId;

        _ = _bridge.ModSharp.InvokeFrameActionAsync(async () =>
        {
            try
            {
                var entry = new PoopLogEntity
                {
                    PlayerName    = playerName,
                    PlayerSteamId = playerSteamId.ToString(),
                    TargetName    = capturedVName,
                    TargetSteamId = capturedVSteamId?.ToString(),
                    MapName       = currentMap,
                    PoopSize      = poopSize,
                    PoopColorR    = colorPreference.Red,
                    PoopColorG    = colorPreference.Green,
                    PoopColorB    = colorPreference.Blue,
                    IsRainbow     = colorPreference.IsRainbow,
                    PlayerX       = position.X,
                    PlayerY       = position.Y,
                    PlayerZ       = position.Z,
                    Timestamp     = DateTime.UtcNow,
                };

                var logId = await _stats.LogPoopAsync(entry);
                _logger.LogDebug("[Poop] Logged poop #{Id} for {Player}", logId, playerName);

                // Victim count broadcast
                if (capturedVSteamId.HasValue && capturedVName != null)
                {
                    int count = await _stats.GetVictimPoopCountAsync(capturedVSteamId.Value.ToString());
                    if (count > 0)
                        _locale.PrintToAll("leaderboard.victim_total_count", capturedVName, count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Poop] DB log failed for {Player}", playerName);
            }
        });

        return result;
    }

    // ── Victim detection ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the nearest dead player to <paramref name="pooper"/>, using ragdoll or
    /// traditional detection as configured.
    /// </summary>
    public DeadPlayerInfo? FindNearestDeadPlayer(IGameClient? pooper)
        => _config.VictimDetection.UseRagdollVictimDetection
            ? FindNearestDeadPlayerRagdoll(pooper)
            : FindNearestDeadPlayerTraditional(pooper);

    private DeadPlayerInfo? FindNearestDeadPlayerRagdoll(IGameClient? pooper)
    {
        if (pooper == null || !pooper.IsValid)
            return null;

        var origin = pooper.GetPlayerController()?.GetAbsOrigin() ?? new Vector(0, 0, 0);

        try
        {
            DeadPlayerInfo? closest    = null;
            float           closestSq  = float.MaxValue;
            float           maxSq      = _config.VictimDetection.RagdollDetectionDistance *
                                         _config.VictimDetection.RagdollDetectionDistance;

            foreach (var (client, info) in _ragdollRegistry.Ragdolls)
            {
                if (client == pooper) continue;
                if (!info.Ragdoll.IsValid()) continue;

                float distSq = origin.DistanceSquared(info.Ragdoll.GetAbsOrigin());
                if (distSq < maxSq && distSq < closestSq)
                {
                    closest   = new DeadPlayerInfo(info.Ragdoll.GetAbsOrigin(), client);
                    closestSq = distSq;
                }
            }

            if (closest != null)
                _logger.LogInformation("[Poop] Ragdoll detection: found '{Name}' at {Dist:F1} u",
                    closest.Player?.Name ?? "?", MathF.Sqrt(closestSq));
            else
                _logger.LogDebug("[Poop] Ragdoll detection: none within {MaxDist} u (tracked {Count})",
                    _config.VictimDetection.RagdollDetectionDistance, _ragdollRegistry.Ragdolls.Count);

            return closest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Poop] Error in ragdoll victim detection");
            return null;
        }
    }

    private DeadPlayerInfo? FindNearestDeadPlayerTraditional(IGameClient? pooper)
    {
        if (pooper == null || !pooper.IsValid)
            return null;

        var controller = pooper.GetPlayerController();
        if (controller == null || !controller.IsValid())
            return null;

        var pawn = controller.GetPlayerPawn();
        if (pawn == null || !pawn.IsValid())
            return null;

        var position = pawn.GetAbsOrigin();

        try
        {
            DeadPlayerInfo? closest   = null;
            float           closestSq = float.MaxValue;
            float           maxSq     = _config.VictimDetection.MaxDeadPlayerDistance *
                                        _config.VictimDetection.MaxDeadPlayerDistance;

            foreach (var (client, info) in _deadPlayerRegistry.DeadPlayers)
            {
                if (client == pooper) continue;

                float distSq = position.DistanceSquared(info.Position);
                if (distSq < maxSq && distSq < closestSq)
                {
                    closestSq = distSq;
                    closest   = info;
                }
            }

            if (closest != null)
                _logger.LogInformation("[Poop] Traditional detection: found '{Name}' at {Dist:F1} u",
                    closest.Player?.Name ?? "?", MathF.Sqrt(closestSq));
            else
                _logger.LogDebug("[Poop] Traditional detection: none within {MaxDist} u (tracked {Count})",
                    _config.VictimDetection.MaxDeadPlayerDistance, _deadPlayerRegistry.DeadPlayers.Count);

            return closest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Poop] Error in traditional victim detection");
            return null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private PoopColorPreference ResolveColor(PoopColorPreference? colorOverride)
    {
        if (colorOverride != null)
            return colorOverride;

        var parsed = ColorUtils.ParseRgbString(_config.Color.DefaultPoopColor);
        return parsed.HasValue
            ? new PoopColorPreference(parsed.Value.r, parsed.Value.g, parsed.Value.b)
            : new PoopColorPreference(139, 69, 19);
    }

    private void PlayPoopSound(IBaseEntity entity)
    {
        var sounds = _config.Sound.PoopSounds;
        if (sounds.Count == 0) return;

        var entry  = sounds[_random.Next(sounds.Count)];
        var volume = entry.Volume ?? _config.Sound.SoundVolume;
        var filter = new RecipientFilter();
        _bridge.SoundManager.StartSoundEvent(entry.SoundEvent, entity, volume, filter);
    }

    private void PlayTauntSound(IGameClient victim)
    {
        var sounds = _config.Sound.TauntSounds;
        if (sounds.Count == 0) return;

        var victimController = victim.GetPlayerController();
        if (victimController == null) return;

        var entry  = sounds[_random.Next(sounds.Count)];
        var volume = entry.Volume ?? _config.Sound.SoundVolume;
        var filter = new RecipientFilter(victimController.PlayerSlot);
        _bridge.SoundManager.StartSoundEvent(entry.SoundEvent, victimController, volume, filter);
    }
}
