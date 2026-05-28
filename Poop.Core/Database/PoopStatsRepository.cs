using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Database.Shared;
using Prefix.Poop.Shared.Models;
using Sharp.Shared.Units;

namespace Prefix.Poop.Database;

/// <summary>
/// Poop's domain persistence: writes placement logs and computes leaderboards on top of the
/// generic <see cref="IDatabaseProvider"/>. The provider may be absent (DB plugin not loaded) —
/// in that case every method degrades gracefully to a no-op / empty result.
/// </summary>
internal sealed class PoopStatsRepository
{
    private readonly ILogger<PoopStatsRepository> _logger;
    private IDatabaseProvider? _db;

    public PoopStatsRepository(ILogger<PoopStatsRepository> logger)
        => _logger = logger;

    public bool Available => _db is not null;

    /// <summary>Bind the provider (resolved in OnAllModulesLoaded) and ensure the table exists.</summary>
    public void Attach(IDatabaseProvider? provider)
    {
        _db = provider;
        if (_db is null)
        {
            _logger.LogWarning("[Poop] Database provider unavailable — stats/leaderboards disabled");
            return;
        }

        try
        {
            _db.InitTables(typeof(PoopLogEntity));
            _logger.LogInformation("[Poop] poop_logs table ready");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to init poop_logs table");
            _db = null;
        }
    }

    public async Task<int> LogPoopAsync(PoopLogEntity entry)
    {
        if (_db is null)
            return 0;

        try
        {
            return await _db.InsertAsync(entry);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to log poop for {Player}", entry.PlayerName);
            return 0;
        }
    }

    public async Task<int> GetTotalPoopsCountAsync()
    {
        if (_db is null)
            return 0;

        try
        {
            return await _db.Queryable<PoopLogEntity>().CountAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to count total poops");
            return 0;
        }
    }

    public async Task<int> GetPlayerPoopCountAsync(string steamId)
    {
        if (_db is null)
            return 0;

        try
        {
            return await _db.Queryable<PoopLogEntity>().Where(x => x.PlayerSteamId == steamId).CountAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to count poops for {SteamId}", steamId);
            return 0;
        }
    }

    public async Task<int> GetVictimPoopCountAsync(string steamId)
    {
        if (_db is null)
            return 0;

        try
        {
            return await _db.Queryable<PoopLogEntity>().Where(x => x.TargetSteamId == steamId).CountAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to count victim poops for {SteamId}", steamId);
            return 0;
        }
    }

    /// <summary>Top N players by poops placed (grouped in memory — modest data volume).</summary>
    public async Task<PoopStats[]> GetTopPoopersAsync(int limit)
    {
        if (_db is null)
            return [];

        try
        {
            var rows = await _db.Queryable<PoopLogEntity>().ToListAsync();
            return rows
                .GroupBy(x => x.PlayerSteamId)
                .Select(g => ToStats(g.Key, MostRecentName(g, victim: false), placed: g.Count(), pooped: 0))
                .Where(s => s is not null)
                .Select(s => s!)
                .OrderByDescending(s => s.PoopsPlaced)
                .Take(limit)
                .ToArray();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to get top poopers");
            return [];
        }
    }

    /// <summary>Top N players by times pooped on.</summary>
    public async Task<PoopStats[]> GetTopVictimsAsync(int limit)
    {
        if (_db is null)
            return [];

        try
        {
            var rows = await _db.Queryable<PoopLogEntity>().Where(x => x.TargetSteamId != null).ToListAsync();
            return rows
                .Where(x => !string.IsNullOrEmpty(x.TargetSteamId))
                .GroupBy(x => x.TargetSteamId!)
                .Select(g => ToStats(g.Key, MostRecentName(g, victim: true), placed: 0, pooped: g.Count()))
                .Where(s => s is not null)
                .Select(s => s!)
                .OrderByDescending(s => s.TimesPoopedOn)
                .Take(limit)
                .ToArray();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to get top victims");
            return [];
        }
    }

    private static string MostRecentName(IEnumerable<PoopLogEntity> group, bool victim)
        => group.OrderByDescending(x => x.Timestamp)
                .Select(x => victim ? x.TargetName : x.PlayerName)
                .FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "Unknown";

    private static PoopStats? ToStats(string steamIdStr, string name, int placed, int pooped)
    {
        if (!ulong.TryParse(steamIdStr, out var raw))
            return null;

        return new PoopStats
        {
            PlayerName    = name,
            SteamId       = (SteamID) raw,
            PoopsPlaced   = placed,
            TimesPoopedOn = pooped,
        };
    }
}
