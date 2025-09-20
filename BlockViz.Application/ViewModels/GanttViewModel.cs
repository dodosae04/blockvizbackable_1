using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using BlockViz.Applications.Extensions;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Waf.Applications;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.ViewModels
{
    /// <summary>
    /// 작업장별 다중 레이어 간트 (미래 구간 미표시, 색상 동기화 지원)
    /// - 회색 기본 막대를 먼저 깔고, 같은 작업장의 블록을 '시작일 오름차순'으로 now까지 덧그립니다.
    /// - 전역 색상 서비스와 동기화하여 3D/PI와 동일 색상을 사용합니다.
    /// - 동시 작업 시 작업장 행을 lane 단위로 확장하고, 과밀 구간은 요약 배지로 표시합니다.
    /// - 축 범위: 데이터 전체기간 + 살짝의 여백, 막대는 now까지만 그립니다(미래 X).
    /// </summary>
    [Export]
    public sealed class GanttViewModel : ViewModel<IGanttView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;
        private readonly IBlockColorService colorService;

        public PlotModel GanttModel { get; }

        private readonly DateTimeAxis dateAxis;
        private readonly CategoryAxis catAxis;

        // 작업장 라벨(6행 고정)
        private static readonly int[] WorkplaceIds = { 1, 2, 3, 4, 5, 6 };

        // 보기 옵션
        private const double BarWidth = 0.78;  // 카테고리 높이 대비 두께
        private const double GapWidth = 0.18;  // 카테고리 간 여백(=1-BarWidth)
        private const int MaxVisibleLanes = 4; // 동시 작업 요약 임계치

        [ImportingConstructor]
        public GanttViewModel(IGanttView view,
                              IScheduleService scheduleService,
                              SimulationService simulationService,
                              IBlockColorService colorService) : base(view)
        {
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;
            this.colorService = colorService;

            GanttModel = new PlotModel { Title = "공장 가동 스케줄" };

            // X축(날짜)
            dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "기간",
                StringFormat = "yyyy-MM-dd",
                IsZoomEnabled = false,
                IsPanEnabled = false,
                MinimumPadding = 0.0,
                MaximumPadding = 0.0
            };
            GanttModel.Axes.Add(dateAxis);

            // Y축(작업장) — 6개 라벨 고정
            catAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "작업장",
                GapWidth = GapWidth,
                IsZoomEnabled = false,
                IsPanEnabled = false
            };
            foreach (var id in WorkplaceIds) catAxis.Labels.Add($"작업장 {id}");
            GanttModel.Axes.Add(catAxis);

            // 시뮬레이션 날짜 변경시 동기화
            simulationService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(simulationService.CurrentDate))
                    UpdateGantt();
            };

            UpdateGantt();
            view.GanttModel = GanttModel;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void UpdateGantt()
        {
            GanttModel.Series.Clear();
            GanttModel.Annotations.Clear();
            catAxis.Labels.Clear();

            var all = scheduleService.GetAllBlocks()?.ToList() ?? new List<Block>();
            if (all.Count == 0)
            {
                foreach (var id in WorkplaceIds)
                    catAxis.Labels.Add($"작업장 {id}");
                GanttModel.InvalidatePlot(true);
                return;
            }

            // 전체 기간 (엑셀 첫 시작 ~ 마지막 종료)
            var globalStart = FloorDate(all.Min(b => b.Start));
            var now = simulationService.CurrentDate;
            var endCandidates = all.Select(b => b.GetEffectiveEnd())
                                   .Where(d => d.HasValue)
                                   .Select(d => d!.Value)
                                   .ToList();
            var globalEnd = endCandidates.Count > 0
                ? CeilDate(endCandidates.Max())
                : CeilDate(now > globalStart ? now : globalStart);

            // 현재일(now) — 미래 구간은 그리지 않음
            if (now < globalStart) now = globalStart;
            if (now > globalEnd) now = globalEnd;

            // 축 범위: 전체 기간 + 소량 여백
            var totalDays = Math.Max(1.0, (globalEnd - globalStart).TotalDays);
            var padDays = Math.Max(3.0, totalDays * 0.01);
            var axisMin = globalStart.AddDays(-padDays);
            var axisMax = globalEnd.AddDays(+padDays);

            var axisMinValue = DateTimeAxis.ToDouble(axisMin);
            var axisMaxValue = DateTimeAxis.ToDouble(axisMax);
            var globalStartValue = DateTimeAxis.ToDouble(globalStart);
            var nowValue = DateTimeAxis.ToDouble(now);

            dateAxis.Minimum = axisMinValue;
            dateAxis.Maximum = axisMaxValue;

            // 현재일 수직선
            var nowLine = new LineSeries
            {
                Color = OxyColors.Gray,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            };

            // 막대 시리즈(오버레이)
            var series = new IntervalBarSeries
            {
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                LabelFormatString = null,
                BarWidth = BarWidth
            };

            var byWp = all.GroupBy(b => b.DeployWorkplace)
                          .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

            var categoryLabels = new List<string>();
            var laneSummaries = new List<LaneSummary>();

            foreach (var wpId in WorkplaceIds)
            {
                var firstCategoryIndex = categoryLabels.Count;
                byWp.TryGetValue(wpId, out var blocksForWorkplace);

                var lanes = BuildLanes(blocksForWorkplace, now);
                var totalLaneCount = Math.Max(1, lanes.Count);
                var visibleLaneCount = lanes.Count == 0 ? 1 : Math.Min(MaxVisibleLanes, lanes.Count);
                var backgroundColor = GetBackgroundColor(totalLaneCount);

                for (int laneIndex = 0; laneIndex < visibleLaneCount; laneIndex++)
                {
                    int categoryIndex = categoryLabels.Count;
                    categoryLabels.Add(laneIndex == 0 ? $"작업장 {wpId}" : string.Empty);

                    series.Items.Add(new IntervalBarItem
                    {
                        CategoryIndex = categoryIndex,
                        Start = globalStartValue,
                        End = nowValue,
                        Color = backgroundColor
                    });

                    if (laneIndex < lanes.Count)
                    {
                        foreach (var segment in lanes[laneIndex].Segments)
                        {
                            series.Items.Add(new IntervalBarItem
                            {
                                CategoryIndex = categoryIndex,
                                Start = DateTimeAxis.ToDouble(segment.Start),
                                End = DateTimeAxis.ToDouble(segment.End),
                                Color = colorService.GetOxyColor(segment.Block.Name),
                                Tag = segment.Block
                            });
                        }
                    }
                }

                if (lanes.Count > MaxVisibleLanes)
                {
                    var hiddenSegments = lanes.Skip(MaxVisibleLanes)
                                              .SelectMany(l => l.Segments)
                                              .OrderBy(s => s.Start)
                                              .ToList();
                    if (hiddenSegments.Count > 0)
                    {
                        laneSummaries.Add(new LaneSummary
                        {
                            WorkplaceId = wpId,
                            FirstCategoryIndex = firstCategoryIndex,
                            VisibleLaneCount = Math.Max(1, visibleLaneCount),
                            HiddenSegments = hiddenSegments
                        });
                    }
                }
            }

            if (categoryLabels.Count == 0)
            {
                foreach (var id in WorkplaceIds)
                    categoryLabels.Add($"작업장 {id}");
            }

            foreach (var label in categoryLabels)
                catAxis.Labels.Add(label);

            var categoryCount = Math.Max(1, categoryLabels.Count);
            nowLine.Points.Add(new DataPoint(nowValue, -0.5));
            nowLine.Points.Add(new DataPoint(nowValue, categoryCount - 0.5));
            GanttModel.Series.Add(nowLine);
            GanttModel.Series.Add(series);

            foreach (var summary in laneSummaries)
            {
                var annotation = CreateSummaryAnnotation(summary, axisMinValue, axisMaxValue);
                if (annotation != null)
                    GanttModel.Annotations.Add(annotation);
            }

            GanttModel.InvalidatePlot(true);
        }

        private static List<LaneInfo> BuildLanes(IReadOnlyList<Block> blocks, DateTime now)
        {
            var lanes = new List<LaneInfo>();
            if (blocks == null || blocks.Count == 0)
                return lanes;

            var segments = new List<BlockSlice>(blocks.Count);
            foreach (var block in blocks)
            {
                if (block.Start >= now)
                    break;

                var effectiveEnd = block.GetEffectiveEnd() ?? now;
                if (effectiveEnd <= block.Start)
                    continue;

                var clampedEnd = effectiveEnd > now ? now : effectiveEnd;
                if (clampedEnd <= block.Start)
                    continue;

                segments.Add(new BlockSlice(block, block.Start, clampedEnd));
            }

            if (segments.Count == 0)
                return lanes;

            segments.Sort((a, b) => a.Start.CompareTo(b.Start));

            foreach (var segment in segments)
            {
                LaneInfo lane = null;
                foreach (var candidate in lanes)
                {
                    if (candidate.LastEnd <= segment.Start)
                    {
                        lane = candidate;
                        break;
                    }
                }

                if (lane == null)
                {
                    lane = new LaneInfo();
                    lanes.Add(lane);
                }

                lane.Segments.Add(segment);
                lane.LastEnd = segment.End;
            }

            return lanes;
        }

        private static OxyColor GetBackgroundColor(int laneCount)
        {
            laneCount = Math.Max(1, laneCount);
            const int baseShade = 235;
            const int minShade = 185;
            int shadeValue = baseShade - Math.Min(6, laneCount - 1) * 10;
            if (shadeValue < minShade) shadeValue = minShade;
            var shade = (byte)shadeValue;
            return OxyColor.FromRgb(shade, shade, shade);
        }

        private TextAnnotation CreateSummaryAnnotation(LaneSummary summary, double axisMinValue, double axisMaxValue)
        {
            if (summary.HiddenSegments.Count == 0)
                return null;

            var width = axisMaxValue - axisMinValue;
            if (double.IsNaN(width) || double.IsInfinity(width))
                width = 0;

            var x = axisMinValue + (width <= 0 ? 0 : width * 0.01);
            var y = summary.FirstCategoryIndex + Math.Max(1, summary.VisibleLaneCount) - 0.6;

            return new TextAnnotation
            {
                Text = $"+{summary.HiddenSegments.Count}",
                TextColor = OxyColors.White,
                Stroke = OxyColors.Transparent,
                Background = OxyColor.FromAColor(200, OxyColors.DimGray),
                Position = new DataPoint(x, y),
                TextHorizontalAlignment = HorizontalAlignment.Left,
                TextVerticalAlignment = VerticalAlignment.Top,
                FontSize = 11,
                Padding = new OxyThickness(6, 2, 6, 2),
                ToolTip = BuildSummaryTooltip(summary.WorkplaceId, summary.HiddenSegments)
            };
        }

        private static string BuildSummaryTooltip(int workplaceId, IReadOnlyList<BlockSlice> hiddenSegments)
        {
            if (hiddenSegments.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"작업장 {workplaceId} 숨김 블록 {hiddenSegments.Count}개");
            foreach (var segment in hiddenSegments)
            {
                sb.AppendLine($"• {segment.Block.Name} : {segment.Start:yyyy-MM-dd} ~ {segment.End:yyyy-MM-dd}");
            }

            return sb.ToString().TrimEnd();
        }

        private sealed class LaneInfo
        {
            public List<BlockSlice> Segments { get; } = new();
            public DateTime LastEnd { get; set; } = DateTime.MinValue;
        }

        private sealed record BlockSlice(Block Block, DateTime Start, DateTime End);

        private sealed class LaneSummary
        {
            public int WorkplaceId { get; init; }
            public int FirstCategoryIndex { get; init; }
            public int VisibleLaneCount { get; init; }
            public IReadOnlyList<BlockSlice> HiddenSegments { get; init; } = Array.Empty<BlockSlice>();
        }

        // ===== 유틸 =====
        private static DateTime FloorDate(DateTime dt) => dt.Date;

        private static DateTime CeilDate(DateTime dt)
            => dt.TimeOfDay == TimeSpan.Zero ? dt.Date : dt.Date.AddDays(1);
    }
}
