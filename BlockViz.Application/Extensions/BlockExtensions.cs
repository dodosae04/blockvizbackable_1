using System;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.Extensions
{
    public static class BlockExtensions
    {
        private const string UnnamedBlockLabel = "(무명 블록)";

        public static DateTime? GetEffectiveEnd(this Block block)
        {
            if (block == null)
            {
                return null;
            }

            if (block.End > block.Start)
            {
                return block.End;
            }

            if (block.Due.HasValue && block.Due.Value > block.Start)
            {
                return block.Due.Value;
            }

            if (block.ProcessingTime > 0)
            {
                return block.Start.AddDays(block.ProcessingTime);
            }

            return null;
        }

        public static bool IsActiveOn(this Block block, DateTime date)
        {
            if (block == null)
            {
                return false;
            }

            if (date < block.Start)
            {
                return false;
            }

            var end = block.GetEffectiveEnd();
            return end == null || date <= end.Value;
        }

        public static string GetDisplayName(this Block block)
        {
            if (block == null)
            {
                return UnnamedBlockLabel;
            }

            return GetDisplayName(block.Name);
        }

        public static string GetDisplayName(string? blockName)
        {
            var trimmed = blockName?.Trim();
            return string.IsNullOrEmpty(trimmed) ? UnnamedBlockLabel : trimmed;
        }
    }
}
