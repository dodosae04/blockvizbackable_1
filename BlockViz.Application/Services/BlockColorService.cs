using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using OxyPlot;

namespace BlockViz.Applications.Services
{
    [Export(typeof(IBlockColorService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class BlockColorService : IBlockColorService
    {
        private readonly Dictionary<string, Color> colors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SolidColorBrush> brushes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, OxyColor> oxyColors = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Color DefaultColor = Color.FromRgb(204, 204, 204);
        private static readonly SolidColorBrush DefaultBrush = CreateFrozen(DefaultColor);
        private static readonly OxyColor DefaultOxyColor = OxyColor.FromArgb(DefaultColor.A, DefaultColor.R, DefaultColor.G, DefaultColor.B);

        public Color GetColor(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return DefaultColor;
            }

            if (!colors.TryGetValue(blockName, out var color))
            {
                color = CreateColor(blockName);
                colors[blockName] = color;
            }

            return color;
        }

        public SolidColorBrush GetBrush(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return DefaultBrush;
            }

            if (!brushes.TryGetValue(blockName, out var brush))
            {
                var color = GetColor(blockName);
                brush = CreateFrozen(color);
                brushes[blockName] = brush;
            }

            return brush;
        }

        public OxyColor GetOxyColor(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return DefaultOxyColor;
            }

            if (!oxyColors.TryGetValue(blockName, out var value))
            {
                var color = GetColor(blockName);
                value = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
                oxyColors[blockName] = value;
            }

            return value;
        }

        public void Reset()
        {
            colors.Clear();
            brushes.Clear();
            oxyColors.Clear();
        }

        private static SolidColorBrush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color CreateColor(string key)
        {
            var hash = ComputeStableHash(key);

            var hue = (hash % 360 + 360) % 360;
            var saturation = 0.55 + ((hash >> 8) & 0x3F) / 255.0;   // 0.55 ~ 0.79
            if (saturation > 0.85) saturation = 0.85;
            var lightness = 0.45 + ((hash >> 16) & 0x3F) / 255.0;   // 0.45 ~ 0.69
            if (lightness > 0.75) lightness = 0.75;

            return FromHsl(hue, saturation, lightness);
        }

        private static int ComputeStableHash(string key)
        {
            unchecked
            {
                int hash = 17;
                foreach (var ch in key)
                {
                    hash = hash * 31 + ch;
                }
                return hash;
            }
        }

        private static Color FromHsl(double hue, double saturation, double lightness)
        {
            double c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double x = c * (1 - Math.Abs((hue / 60.0 % 2) - 1));
            double m = lightness - c / 2.0;

            double r1, g1, b1;
            if (hue < 60)
            {
                r1 = c; g1 = x; b1 = 0;
            }
            else if (hue < 120)
            {
                r1 = x; g1 = c; b1 = 0;
            }
            else if (hue < 180)
            {
                r1 = 0; g1 = c; b1 = x;
            }
            else if (hue < 240)
            {
                r1 = 0; g1 = x; b1 = c;
            }
            else if (hue < 300)
            {
                r1 = x; g1 = 0; b1 = c;
            }
            else
            {
                r1 = c; g1 = 0; b1 = x;
            }

            byte r = (byte)Math.Round((r1 + m) * 255);
            byte g = (byte)Math.Round((g1 + m) * 255);
            byte b = (byte)Math.Round((b1 + m) * 255);

            return Color.FromRgb(r, g, b);
        }
    }
}
