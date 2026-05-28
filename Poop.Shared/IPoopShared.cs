// ReSharper disable InconsistentNaming, UnusedMember.Global, UnusedMemberInSuper.Global

using System;
using System.Threading.Tasks;
using Prefix.Poop.Shared.Events;
using Prefix.Poop.Shared.Models;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace Prefix.Poop.Shared;

/// <summary>
/// Public API for the Poop plugin. Other plugins resolve this via
/// <c>GetRequiredSharpModuleInterface&lt;IPoopShared&gt;(IPoopShared.Identity)</c>.
/// </summary>
public interface IPoopShared
{
    static string Identity => typeof(IPoopShared).FullName ?? nameof(IPoopShared);

    #region Events

    /// <summary>
    /// Fired when a player runs a poop command, BEFORE any validation. Set
    /// <see cref="PoopCommandEventArgs.Cancel"/> to block (e.g. donator/admin gating).
    /// </summary>
    event Action<PoopCommandEventArgs>? OnPoopCommand;

    /// <summary>Fired after every poop spawn attempt (success or failure).</summary>
    event Action<PoopSpawnedEventArgs>? OnPoopSpawned;

    #endregion

    #region Spawn API

    /// <summary>
    /// Spawn a poop with full logic (sounds, messages, logging). Position is taken from the
    /// player's pawn and the victim is auto-detected; the <paramref name="position"/> and
    /// <paramref name="victim"/> arguments are accepted for API compatibility.
    /// </summary>
    SpawnPoopResult SpawnPoop(
        IGameClient? player,
        Vector position,
        float size = -1.0f,
        PoopColorPreference? color = null,
        IGameClient? victim = null,
        bool playSounds = true);

    /// <summary>
    /// Force a poop from a player's position (bypasses cooldowns/restrictions). Uses the
    /// player's saved color preference when <paramref name="color"/> is null.
    /// </summary>
    SpawnPoopResult? ForcePlayerPoop(
        IGameClient? player,
        float size = -1.0f,
        PoopColorPreference? color = null,
        bool playSounds = true);

    #endregion

    #region Statistics API

    /// <summary>Statistics for a specific player (null if none).</summary>
    Task<PoopStats?> GetPlayerStatsAsync(SteamID steamId);

    /// <summary>Top N players who placed the most poops.</summary>
    Task<PoopStats[]> GetTopPoopersAsync(int limit = 10);

    /// <summary>Top N players who were pooped on the most.</summary>
    Task<PoopStats[]> GetTopVictimsAsync(int limit = 10);

    /// <summary>Total poops spawned on the server (all time).</summary>
    Task<int> GetTotalPoopsCountAsync();

    #endregion

    #region Player Color Preferences

    /// <summary>Get a player's preferred poop color (null if unset).</summary>
    Task<PoopColorPreference?> GetPlayerColorPreferenceAsync(SteamID steamId);

    /// <summary>Set a player's poop color preference.</summary>
    Task SetPlayerColorPreferenceAsync(SteamID steamId, PoopColorPreference color);

    #endregion
}
