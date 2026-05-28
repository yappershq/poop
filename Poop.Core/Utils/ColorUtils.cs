using System;
using System.Collections.Generic;
using System.Linq;
using Prefix.Poop.Config;

namespace Prefix.Poop.Utils;

/// <summary>
/// Utility methods for poop color operations
/// </summary>
internal static class ColorUtils
{
    private static readonly Random Random = new();

    /// <summary>
    /// Gets a random color from the available colors list (excluding Rainbow and Random entries)
    /// </summary>
    public static ColorDefinition GetRandomColor(IReadOnlyList<ColorDefinition> colors)
    {
        var normalColors = colors
            .Where(c => !c.IsRainbow && !c.IsRandom)
            .ToArray();

        if (normalColors.Length == 0)
        {
            // Default brown fallback
            return new ColorDefinition { Red = 139, Green = 69, Blue = 19 };
        }

        var randomIndex = Random.Next(normalColors.Length);
        return normalColors[randomIndex];
    }

    /// <summary>
    /// Converts HSV color values to RGB bytes.
    /// Hue is in degrees (0-360), saturation and value are in range 0.0-1.0.
    /// </summary>
    public static (byte r, byte g, byte b) HsvToRgb(float hue, float saturation, float value)
    {
        if (saturation <= 0.0f)
        {
            byte grey = (byte)(value * 255);
            return (grey, grey, grey);
        }

        float h = hue % 360.0f;
        if (h < 0.0f) h += 360.0f;

        float sector = h / 60.0f;
        int i = (int)MathF.Floor(sector);
        float f = sector - i;

        float p = value * (1.0f - saturation);
        float q = value * (1.0f - saturation * f);
        float t = value * (1.0f - saturation * (1.0f - f));

        float r, g, b;
        switch (i)
        {
            case 0:  r = value; g = t;     b = p;     break;
            case 1:  r = q;     g = value; b = p;     break;
            case 2:  r = p;     g = value; b = t;     break;
            case 3:  r = p;     g = q;     b = value; break;
            case 4:  r = t;     g = p;     b = value; break;
            default: r = value; g = p;     b = q;     break;
        }

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    /// <summary>
    /// Parses a color from "R,G,B" string format. Returns null if parsing fails.
    /// </summary>
    public static (byte r, byte g, byte b)? ParseRgbString(string rgb)
    {
        if (string.IsNullOrWhiteSpace(rgb))
            return null;

        var parts = rgb.Split(',');
        if (parts.Length != 3)
            return null;

        if (byte.TryParse(parts[0].Trim(), out byte r) &&
            byte.TryParse(parts[1].Trim(), out byte g) &&
            byte.TryParse(parts[2].Trim(), out byte b))
        {
            return (r, g, b);
        }

        return null;
    }
}
