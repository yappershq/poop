using System;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database;

/// <summary>
/// One poop placement, persisted via the generic <see cref="IDatabaseProvider"/>.
/// Backend-agnostic (LiteDB / MySQL / Postgres) — the provider owns the schema.
/// </summary>
[DbTable("poop_logs")]
internal sealed class PoopLogEntity
{
    [DbColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [DbColumn(IsNullable = false, Length = 255)]
    public string PlayerName { get; set; } = string.Empty;

    [DbColumn(IsNullable = false, Length = 64)]
    public string PlayerSteamId { get; set; } = string.Empty;

    [DbColumn(IsNullable = true, Length = 255)]
    public string? TargetName { get; set; }

    [DbColumn(IsNullable = true, Length = 64)]
    public string? TargetSteamId { get; set; }

    [DbColumn(IsNullable = false, Length = 255)]
    public string MapName { get; set; } = string.Empty;

    [DbColumn(IsNullable = false)]
    public float PoopSize { get; set; }

    [DbColumn(IsNullable = false)] public int PoopColorR { get; set; }
    [DbColumn(IsNullable = false)] public int PoopColorG { get; set; }
    [DbColumn(IsNullable = false)] public int PoopColorB { get; set; }

    [DbColumn(IsNullable = false)] public bool IsRainbow { get; set; }

    [DbColumn(IsNullable = false)] public float PlayerX { get; set; }
    [DbColumn(IsNullable = false)] public float PlayerY { get; set; }
    [DbColumn(IsNullable = false)] public float PlayerZ { get; set; }

    [DbColumn(IsNullable = false)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
