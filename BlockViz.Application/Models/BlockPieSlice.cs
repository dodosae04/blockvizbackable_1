using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OxyPlot.Series;

namespace BlockViz.Applications.Models
{
    /// <summary>
    /// PieSlice 확장형 — 파이 차트 조각에 연결된 블록 이름 목록을 포함.
    /// Label은 항상 빈 문자열로 유지하여 기존 UI(라벨 숨김)를 보존.
    /// </summary>
    public sealed class BlockPieSlice : PieSlice
    {
        private static readonly IReadOnlyList<string> EmptyNames = Array.Empty<string>();

        public BlockPieSlice(string colorKey, IEnumerable<string>? displayNames, double value)
            : base(string.Empty, value)
        {
            ColorKey = colorKey ?? string.Empty;

            if (displayNames == null)
            {
                BlockNames = EmptyNames;
            }
            else
            {
                var list = displayNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                BlockNames = list.Count == 0
                    ? EmptyNames
                    : new ReadOnlyCollection<string>(list);
            }
        }

        /// <summary>
        /// 색상 계산 시 사용한 키 (기존 block.Name 또는 대체 값).
        /// </summary>
        public string ColorKey { get; }

        /// <summary>
        /// 슬라이스에 속한 블록 이름 목록 (오름차순, 중복 제거).
        /// </summary>
        public IReadOnlyList<string> BlockNames { get; }

        /// <summary>
        /// 목록이 비어 있지 않다면 첫 번째 항목을 대표 이름으로 사용.
        /// </summary>
        public string RepresentativeName => BlockNames.Count > 0 ? BlockNames[0] : string.Empty;
    }
}
