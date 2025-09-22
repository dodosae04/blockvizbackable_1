using System;
using BlockViz.Applications.Extensions;
using BlockViz.Domain.Models;
using OxyPlot.Series;

namespace BlockViz.Applications.Models
{
    /// <summary>
    /// 간트 막대 항목과 실제 Block 모델을 연결하기 위한 경량 래퍼입니다.
    /// 기존 SelectionService 전달 경로를 유지하면서 Block 참조를 보존하기 위해 추가했습니다.
    /// </summary>
    public sealed class BlockIntervalBarItem : IntervalBarItem
    {
        public BlockIntervalBarItem(Block block)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
            Title = block.GetDisplayName();
        }

        public Block Block { get; }
    }
}
