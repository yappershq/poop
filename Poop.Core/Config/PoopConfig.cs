using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Prefix.Poop.Config;

/// <summary>
/// Strongly-typed plugin configuration, loaded from <c>sharp/configs/poop.json</c>.
/// Replaces the old appsettings.json. A default file is written if none exists.
/// </summary>
internal sealed class PoopConfig
{
    [JsonPropertyName("assets")]           public AssetsConfig Assets { get; set; } = new();
    [JsonPropertyName("size")]             public SizeConfig Size { get; set; } = new();
    [JsonPropertyName("color")]            public ColorConfig Color { get; set; } = new();
    [JsonPropertyName("sound")]            public SoundConfig Sound { get; set; } = new();
    [JsonPropertyName("victimDetection")]  public VictimDetectionConfig VictimDetection { get; set; } = new();
    [JsonPropertyName("gameplay")]         public GameplayConfig Gameplay { get; set; } = new();
    [JsonPropertyName("commands")]         public CommandsConfig Commands { get; set; } = new();
    [JsonPropertyName("ui")]               public UiConfig Ui { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
        WriteIndented               = true,
    };

    /// <summary>
    /// Load from <c>&lt;sharpPath&gt;/configs/poop.json</c>, writing the bundled default if absent.
    /// </summary>
    public static PoopConfig Load(string sharpPath, ILogger logger)
    {
        var path = Path.Combine(sharpPath, "configs", "poop.json");

        try
        {
            if (!File.Exists(path))
            {
                logger.LogWarning("[Poop] poop.json not found at {Path}, writing default", path);
                var def = new PoopConfig();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(def, JsonOpts));
                return def;
            }

            var cfg = JsonSerializer.Deserialize<PoopConfig>(File.ReadAllText(path), JsonOpts);
            if (cfg is null)
            {
                logger.LogError("[Poop] poop.json deserialized to null, using defaults");
                return new PoopConfig();
            }

            logger.LogInformation("[Poop] Loaded config from {Path}", path);
            return cfg;
        }
        catch (Exception e)
        {
            logger.LogError(e, "[Poop] Failed to load poop.json, using defaults");
            return new PoopConfig();
        }
    }
}

internal sealed class AssetsConfig
{
    [JsonPropertyName("poopModel")]       public string PoopModel { get; set; } = "models/yappershq/fun/poop.vmdl";
    [JsonPropertyName("soundEventsFile")] public string SoundEventsFile { get; set; } = "soundevents/soundevents_general.vsndevts";
}

internal sealed class SizeConfig
{
    [JsonPropertyName("minPoopSize")]                  public float MinPoopSize { get; set; } = 0.3f;
    [JsonPropertyName("maxPoopSize")]                  public float MaxPoopSize { get; set; } = 2.6f;
    [JsonPropertyName("defaultPoopSize")]              public float DefaultPoopSize { get; set; } = 1.0f;
    [JsonPropertyName("massiveAnnouncementThreshold")] public float MassiveAnnouncementThreshold { get; set; } = 2.0f;

    [JsonPropertyName("generationTiers")]
    public List<SizeGenerationTier> GenerationTiers { get; set; } =
    [
        new() { Chance = 40, Name = "Normal",        MinMultiplier = 0.9f, MaxMultiplier = 1.1f },
        new() { Chance = 25, Name = "Above Average", MinMultiplier = 1.1f, MaxMultiplier = 1.4f },
        new() { Chance = 15, Name = "Small",         MinMultiplier = 0.7f, MaxMultiplier = 0.9f },
        new() { Chance = 10, Name = "Large",         MinMultiplier = 1.4f, MaxMultiplier = 1.7f },
        new() { Chance = 5,  Name = "Tiny",          MinMultiplier = 0.5f, MaxMultiplier = 0.7f },
        new() { Chance = 3,  Name = "Huge",          MinMultiplier = 1.7f, MaxMultiplier = 2.0f },
        new()
        {
            Chance = 2, Name = "Rare", MinMultiplier = 2.0f, MaxMultiplier = 2.6f,
            SubTiers =
            [
                new() { Weight = 80, Name = "Massive",         MinRangePercent = 0.0f,   MaxRangePercent = 0.833f },
                new() { Weight = 19, Name = "Legendary",       MinRangePercent = 0.833f, MaxRangePercent = 1.0f },
                new() { Weight = 1,  Name = "Ultra Legendary", MinRangePercent = 0.98f,  MaxRangePercent = 0.999f },
            ],
        },
    ];

    [JsonPropertyName("sizeCategories")]
    public List<SizeCategory> SizeCategories { get; set; } =
    [
        new() { Threshold = 2.5f, LocaleKey = "size.legendary" },
        new() { Threshold = 2.0f, LocaleKey = "size.desc_massive" },
        new() { Threshold = 1.7f, LocaleKey = "size.desc_huge" },
        new() { Threshold = 1.4f, LocaleKey = "size.desc_large" },
        new() { Threshold = 1.1f, LocaleKey = "size.desc_above_average" },
        new() { Threshold = 0.9f, LocaleKey = "size.desc_normal" },
        new() { Threshold = 0.7f, LocaleKey = "size.desc_small" },
        new() { Threshold = 0.5f, LocaleKey = "size.desc_tiny" },
        new() { Threshold = 0.0f, LocaleKey = "size.desc_microscopic" },
    ];
}

internal sealed class SizeGenerationTier
{
    [JsonPropertyName("chance")]        public int Chance { get; set; }
    [JsonPropertyName("name")]          public string Name { get; set; } = string.Empty;
    [JsonPropertyName("minMultiplier")] public float MinMultiplier { get; set; }
    [JsonPropertyName("maxMultiplier")] public float MaxMultiplier { get; set; }
    [JsonPropertyName("subTiers")]      public List<SizeSubTier>? SubTiers { get; set; }
}

internal sealed class SizeSubTier
{
    [JsonPropertyName("weight")]          public int Weight { get; set; }
    [JsonPropertyName("name")]            public string Name { get; set; } = string.Empty;
    [JsonPropertyName("minRangePercent")] public float MinRangePercent { get; set; }
    [JsonPropertyName("maxRangePercent")] public float MaxRangePercent { get; set; }
}

internal sealed class SizeCategory
{
    [JsonPropertyName("threshold")] public float Threshold { get; set; }
    [JsonPropertyName("localeKey")] public string LocaleKey { get; set; } = string.Empty;
}

internal sealed class ColorConfig
{
    [JsonPropertyName("enableRainbowPoops")]    public bool EnableRainbowPoops { get; set; } = true;
    [JsonPropertyName("rainbowAnimationSpeed")] public float RainbowAnimationSpeed { get; set; } = 2.0f;
    [JsonPropertyName("defaultPoopColor")]      public string DefaultPoopColor { get; set; } = "139,69,19";
    [JsonPropertyName("enableColorPreferences")] public bool EnableColorPreferences { get; set; } = true;

    [JsonPropertyName("availableColors")]
    public List<ColorDefinition> AvailableColors { get; set; } =
    [
        new() { LocaleKey = "color.brown_default", Red = 139, Green = 69,  Blue = 19 },
        new() { LocaleKey = "color.white",         Red = 255, Green = 255, Blue = 255 },
        new() { LocaleKey = "color.black",         Red = 0,   Green = 0,   Blue = 0 },
        new() { LocaleKey = "color.red",           Red = 255, Green = 0,   Blue = 0 },
        new() { LocaleKey = "color.green",         Red = 0,   Green = 255, Blue = 0 },
        new() { LocaleKey = "color.blue",          Red = 0,   Green = 0,   Blue = 255 },
        new() { LocaleKey = "color.yellow",        Red = 255, Green = 255, Blue = 0 },
        new() { LocaleKey = "color.purple",        Red = 128, Green = 0,   Blue = 128 },
        new() { LocaleKey = "color.orange",        Red = 255, Green = 165, Blue = 0 },
        new() { LocaleKey = "color.pink",          Red = 255, Green = 105, Blue = 180 },
        new() { LocaleKey = "color.cyan",          Red = 0,   Green = 255, Blue = 255 },
        new() { LocaleKey = "color.gold",          Red = 255, Green = 215, Blue = 0 },
        new() { LocaleKey = "color.lime",          Red = 0,   Green = 255, Blue = 0 },
        new() { LocaleKey = "color.magenta",       Red = 255, Green = 0,   Blue = 255 },
        new() { LocaleKey = "color.silver",        Red = 192, Green = 192, Blue = 192 },
        new() { LocaleKey = "color.rainbow",       Red = 255, Green = 0,   Blue = 0, IsRainbow = true },
        new() { LocaleKey = "color.random",        Red = 0,   Green = 0,   Blue = 0, IsRandom = true },
    ];
}

internal sealed class ColorDefinition
{
    [JsonPropertyName("localeKey")] public string LocaleKey { get; set; } = string.Empty;
    [JsonPropertyName("red")]       public int Red { get; set; }
    [JsonPropertyName("green")]     public int Green { get; set; }
    [JsonPropertyName("blue")]      public int Blue { get; set; }
    [JsonPropertyName("isRainbow")] public bool IsRainbow { get; set; }
    [JsonPropertyName("isRandom")]  public bool IsRandom { get; set; }
}

internal sealed class SoundConfig
{
    [JsonPropertyName("enableSounds")]      public bool EnableSounds { get; set; } = true;
    [JsonPropertyName("soundVolume")]       public float SoundVolume { get; set; } = 0.5f;
    [JsonPropertyName("enableTauntSounds")] public bool EnableTauntSounds { get; set; } = true;

    [JsonPropertyName("poopSounds")]
    public List<SoundEntry> PoopSounds { get; set; } =
    [
        new() { SoundEvent = "poop.poop_sound_01" },
        new() { SoundEvent = "poop.poop_sound_02" },
        new() { SoundEvent = "poop.poop_sound_03", Volume = 0.7f },
    ];

    [JsonPropertyName("tauntSounds")]
    public List<SoundEntry> TauntSounds { get; set; } =
    [
        new() { SoundEvent = "poop.poop_taunt_01" },
        new() { SoundEvent = "poop.poop_taunt_02" },
    ];
}

internal sealed class SoundEntry
{
    [JsonPropertyName("soundEvent")] public string SoundEvent { get; set; } = string.Empty;
    [JsonPropertyName("volume")]     public float? Volume { get; set; }
}

internal sealed class VictimDetectionConfig
{
    [JsonPropertyName("maxDeadPlayerDistance")]     public float MaxDeadPlayerDistance { get; set; } = 500.0f;
    [JsonPropertyName("useRagdollVictimDetection")] public bool UseRagdollVictimDetection { get; set; }
    [JsonPropertyName("ragdollDetectionDistance")]  public float RagdollDetectionDistance { get; set; } = 100.0f;
}

internal sealed class GameplayConfig
{
    [JsonPropertyName("showMessageOnPoop")]   public bool ShowMessageOnPoop { get; set; } = true;
    [JsonPropertyName("maxPoopsPerRound")]    public int MaxPoopsPerRound { get; set; }
    [JsonPropertyName("removePoopsOnRoundEnd")] public bool RemovePoopsOnRoundEnd { get; set; }
    [JsonPropertyName("poopLifetimeSeconds")] public float PoopLifetimeSeconds { get; set; }
}

internal sealed class CommandsConfig
{
    [JsonPropertyName("topRecordsLimit")]        public int TopRecordsLimit { get; set; } = 10;
    [JsonPropertyName("commandCooldownSeconds")] public int CommandCooldownSeconds { get; set; } = 3;
    [JsonPropertyName("vipOnly")]                public bool VipOnly { get; set; } = true;

    [JsonPropertyName("poop")]       public CommandConfig Poop { get; set; } = new() { Aliases = ["poop", "shit"], CooldownSeconds = 3 };
    [JsonPropertyName("color")]      public CommandConfig Color { get; set; } = new() { Aliases = ["poopcolor", "poop_color", "colorpoop"], CooldownSeconds = 2 };
    [JsonPropertyName("topPoopers")] public CommandConfig TopPoopers { get; set; } = new() { Aliases = ["toppoopers", "pooperstop"], CooldownSeconds = 5 };
    [JsonPropertyName("topVictims")] public CommandConfig TopVictims { get; set; } = new() { Aliases = ["toppoop", "pooptop"], CooldownSeconds = 5 };
}

internal sealed class CommandConfig
{
    [JsonPropertyName("enabled")]         public bool Enabled { get; set; } = true;
    [JsonPropertyName("aliases")]         public List<string> Aliases { get; set; } = [];
    [JsonPropertyName("cooldownSeconds")] public int CooldownSeconds { get; set; } = 3;
}

internal sealed class UiConfig
{
    [JsonPropertyName("chatPrefix")] public string ChatPrefix { get; set; } = "  {PURPLE}Poop {HEAD}⪢{MUTED}";
    [JsonPropertyName("debugMode")]  public bool DebugMode { get; set; }
}
