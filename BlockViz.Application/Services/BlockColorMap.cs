using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace BlockViz.Applications.Helpers
{
    public static class BlockColorMap
    {
        // Color 매핑용
        private static readonly Dictionary<string, Color> colorMap = new();
        private static readonly Color[] palette = new[]
        {
            Colors.Red, Colors.Orange, Colors.Yellow,
            Colors.Green, Colors.Blue, Colors.Purple,
            Colors.Brown, Colors.Teal, Colors.Cyan,
            Colors.Magenta
        };
        private static readonly Color defaultColor = Colors.LightGray;

        // Brush 매핑용 (기존 기능)
        private static readonly Dictionary<string, SolidColorBrush> brushMap = new();
        private static readonly SolidColorBrush defaultBrush = new SolidColorBrush(defaultColor);

        /// <summary>
        /// 프로젝트(블록) 이름에 따라 고유한 Color 값을 반환합니다.
        /// </summary>
        public static Color GetColor(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
                return defaultColor;

            if (!colorMap.TryGetValue(blockName, out var color))
            {
                try
                {
                    // 이름 해시 기반으로 팔레트에서 색상 선택
                    int idx = Math.Abs(blockName.GetHashCode()) % palette.Length;
                    color = palette[idx];
                }
                catch
                {
                    color = defaultColor;
                }
                colorMap[blockName] = color;
            }

            return color;
        }

        /// <summary>
        /// 프로젝트(블록) 이름에 따라 고유한 SolidColorBrush 를 반환합니다.
        /// </summary>
        public static SolidColorBrush GetBrush(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
                return defaultBrush;

            if (!brushMap.TryGetValue(blockName, out var brush))
            {
                // 내부적으로 GetColor 를 호출해 Color 를 얻고, Brush 로 생성
                var color = GetColor(blockName);
                brush = new SolidColorBrush(color);
                brushMap[blockName] = brush;
            }

            return brush;
        }
    }
}
