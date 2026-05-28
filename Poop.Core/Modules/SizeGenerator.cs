using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;

namespace Prefix.Poop.Modules;

/// <summary>
/// Handles poop size generation with a configurable two-level weighted-random tier system,
/// size category lookup, and a dry-run distribution report.
/// </summary>
internal sealed class SizeGenerator
{
    private readonly PoopConfig _config;
    private readonly ILogger<SizeGenerator> _logger;
    private readonly Random _random = new();

    public SizeGenerator(PoopConfig config, ILogger<SizeGenerator> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Generates a random poop size based on rarity configuration.
    /// Uses the two-level weighted-random tier algorithm: first rolls 1-100 against
    /// cumulative tier chances, then (if the tier has sub-tiers) rolls 1-totalWeight
    /// against cumulative sub-tier weights, computing the final size within the
    /// sub-tier's percentage slice of the parent tier's multiplier range.
    /// Final value is clamped to [MinPoopSize, MaxPoopSize] and rounded to 3 decimals.
    /// </summary>
    public float GetRandomSize()
    {
        float defaultSize = _config.Size.DefaultPoopSize;
        float minSize     = _config.Size.MinPoopSize;
        float maxSize     = _config.Size.MaxPoopSize;

        var tiers = _config.Size.GenerationTiers;
        if (tiers == null || tiers.Count == 0)
        {
            _logger.LogWarning("No generation tiers configured, using default size");
            return defaultSize;
        }

        // Roll 1-100 against cumulative tier chances
        int roll = _random.Next(1, 101);
        int cumulative = 0;

        foreach (var tier in tiers)
        {
            cumulative += tier.Chance;
            if (roll <= cumulative)
            {
                float sizeValue;

                if (tier.SubTiers != null && tier.SubTiers.Count > 0)
                {
                    sizeValue = GetSubTierSize(tier, defaultSize, minSize, maxSize);
                }
                else
                {
                    float min = defaultSize * tier.MinMultiplier;
                    float max = defaultSize * tier.MaxMultiplier;

                    min = Math.Max(min, minSize);
                    max = Math.Min(max, maxSize);

                    sizeValue = min + (float)(_random.NextDouble() * (max - min));
                }

                sizeValue = Math.Clamp(sizeValue, minSize, maxSize);
                sizeValue = MathF.Round(sizeValue * 1000) / 1000;

                _logger.LogDebug("Generated {TierName} poop with size {Size:F3}", tier.Name, sizeValue);

                return sizeValue;
            }
        }

        // Fallback — tier chances don't sum to 100
        _logger.LogWarning("Tier chances don't sum to 100%, using default size");
        return defaultSize;
    }

    /// <summary>
    /// Generates size using the configurable sub-tier system.
    /// Sub-tiers use weighted probability within the parent tier's multiplier range.
    /// </summary>
    private float GetSubTierSize(SizeGenerationTier parentTier, float defaultSize, float minSize, float maxSize)
    {
        if (parentTier.SubTiers == null || parentTier.SubTiers.Count == 0)
        {
            _logger.LogWarning("Tier '{Name}' has no SubTiers configured, falling back to standard generation", parentTier.Name);
            float min = defaultSize * parentTier.MinMultiplier;
            float max = defaultSize * parentTier.MaxMultiplier;
            min = Math.Max(min, minSize);
            max = Math.Min(max, maxSize);
            return min + (float)(_random.NextDouble() * (max - min));
        }

        // Sum total weight
        int totalWeight = 0;
        foreach (var subTier in parentTier.SubTiers)
            totalWeight += subTier.Weight;

        if (totalWeight == 0)
        {
            _logger.LogWarning("Tier '{Name}' has SubTiers with zero total weight", parentTier.Name);
            float min = defaultSize * parentTier.MinMultiplier;
            float max = defaultSize * parentTier.MaxMultiplier;
            min = Math.Max(min, minSize);
            max = Math.Min(max, maxSize);
            return min + (float)(_random.NextDouble() * (max - min));
        }

        // Roll 1-totalWeight against cumulative sub-tier weights
        int subTierRoll = _random.Next(1, totalWeight + 1);
        int cumulativeWeight = 0;

        foreach (var subTier in parentTier.SubTiers)
        {
            cumulativeWeight += subTier.Weight;
            if (subTierRoll <= cumulativeWeight)
            {
                float parentMin   = defaultSize * parentTier.MinMultiplier;
                float parentMax   = defaultSize * parentTier.MaxMultiplier;
                float parentRange = parentMax - parentMin;

                float subTierMin = parentMin + (parentRange * subTier.MinRangePercent);
                float subTierMax = parentMin + (parentRange * subTier.MaxRangePercent);

                subTierMin = Math.Max(subTierMin, minSize);
                subTierMax = Math.Min(subTierMax, maxSize);

                float sizeValue = subTierMin + (float)(_random.NextDouble() * (subTierMax - subTierMin));

                _logger.LogDebug("Generated {ParentName} > {SubTierName} poop with size {Size:F3}",
                    parentTier.Name, subTier.Name, sizeValue);

                return sizeValue;
            }
        }

        // Fallback — should never reach here
        _logger.LogWarning("Sub-tier selection failed for tier '{Name}'", parentTier.Name);
        float fallbackMin = defaultSize * parentTier.MinMultiplier;
        float fallbackMax = defaultSize * parentTier.MaxMultiplier;
        fallbackMin = Math.Max(fallbackMin, minSize);
        fallbackMax = Math.Min(fallbackMax, maxSize);
        return fallbackMin + (float)(_random.NextDouble() * (fallbackMax - fallbackMin));
    }

    /// <summary>
    /// Returns the locale key for the size category that applies to <paramref name="size"/>.
    /// Walks <c>config.Size.SizeCategories</c> in order (expected: largest threshold first),
    /// returning the first entry whose Threshold is &lt;= size.
    /// Falls back to "size.desc_microscopic" if none match.
    /// </summary>
    public string GetSizeCategoryKey(float size)
    {
        foreach (var category in _config.Size.SizeCategories)
        {
            if (size >= category.Threshold)
                return category.LocaleKey;
        }

        return "size.desc_microscopic";
    }

    /// <summary>
    /// Runs <paramref name="count"/> simulated size rolls and returns a multi-line distribution
    /// report string suitable for the poop_dryrun console command.
    /// </summary>
    public string RunDistributionReport(int count)
    {
        // Collect raw locale keys and group by category LocaleKey
        var hits = new Dictionary<string, int>(StringComparer.Ordinal);
        var sizes = new List<float>(count);

        for (int i = 0; i < count; i++)
        {
            float size = GetRandomSize();
            sizes.Add(size);
            var key = GetSizeCategoryKey(size);
            hits.TryGetValue(key, out int existing);
            hits[key] = existing + 1;
        }

        sizes.Sort();
        float totalSize = 0f;
        foreach (var s in sizes) totalSize += s;
        float avg = totalSize / count;
        float min = sizes[0];
        float max = sizes[sizes.Count - 1];

        var sb = new StringBuilder();
        sb.AppendLine($"=== Poop Size Distribution ({count:N0} rolls) ===");
        sb.AppendLine($"  Min: {min:F3}  Max: {max:F3}  Avg: {avg:F3}");
        sb.AppendLine();

        // Print categories in config order (largest threshold first)
        foreach (var category in _config.Size.SizeCategories)
        {
            if (!hits.TryGetValue(category.LocaleKey, out int n))
                n = 0;
            float pct = count > 0 ? (n * 100f / count) : 0f;
            sb.AppendLine($"  {category.LocaleKey,-30}  {n,6:N0}  ({pct,6:F2}%)");
        }

        return sb.ToString();
    }
}
