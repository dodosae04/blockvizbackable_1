using System;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.Extensions
{
    public static class BlockExtensions
    {
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
    }
}
