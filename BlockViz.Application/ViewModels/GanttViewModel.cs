using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Waf.Applications;
using BlockViz.Domain.Models;
using BlockViz.Applications.Extensions;

namespace BlockViz.Applications.ViewModels
{
    /// <summary>
    /// 작업장별 1행 기본 · 토글 확장 지원 간트(미래 구간 미표시, 색상 동기화)
    /// - 회색 기본 막대를 먼저 깔고, 같은 작업장의 블록을 '시작일 오름차순'으로 now까지 덧그립니다.
    /// - 작업장 토글이 확장된 경우 겹치는 블록을 레이어로 나누어 모두 표시합니다.
    /// - 축 범위: 데이터 전체기간 + 소량 여백(경계 안전), 막대는 now까지만 그립니다(미래 X).
    /// </summary>
    [Export]
    public sealed class GanttViewModel : ViewModel<IGanttView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;
        private readonly IBlockColorService colorService;
        private int? selectedWorkplaceId;

        public PlotModel GanttModel { get; }

        private readonly DateTimeAxis dateAxis;
        private readonly CategoryAxis catAxis;

        // 작업장 라벨(필요 수만큼)
        private static readonly int[] WorkplaceIds = { 1, 2, 3, 4, 5, 6 };

        // 보기 옵션
        private const double BarWidth = 0.78;  // 카테고리 높이 대비 두께
        private const double GapWidth = 0.18;  // 카테고리 간 여백(=1-BarWidth)

        // ───────────────────────── 헬퍼(새 파일 아님, 이 클래스 안에만 추가) ─────────────────────────
        private static DateTime SafeAddDays(DateTime dt, double days)
        {
            if (days == 0 || dt == DateTime.MinValue || dt == DateTime.MaxValue) return dt;

            if (days > 0)
            {
                var remain = (DateTime.MaxValue - dt).TotalDays;
                if (remain <= 0) return DateTime.MaxValue;
                if (days > remain) days = Math.Floor(remain);
            }
            else // days < 0
            {
                var remain = (dt - DateTime.MinValue).TotalDays;
                if (remain <= 0) return DateTime.MinValue;
                if (-days > remain) days = -Math.Floor(remain);
            }
            return dt.AddDays(days);
        }

        private static void EnsureValidRange(ref DateTime start, ref DateTime end)
        {
            // default/MinValue 보정 + 역전/동일 케이스 보정
            if (start == default || start == DateTime.MinValue) start = DateTime.Today;
            if (end == default || end == DateTime.MinValue) end = start.AddDays(1);
            if (start > end) { var t = start; start = end; end = t; }
            if (start == end) end = SafeAddDays(start, 1);
        }

        private static DateTime FloorDateSafe(DateTime dt)
            => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Kind);

        private static DateTime CeilDateSafe(DateTime dt)
        {
            var d0 = FloorDateSafe(dt);
            if ((DateTime.MaxValue - d0).TotalDays < 1) return DateTime.MaxValue;
            return d0.AddDays(1).AddTicks(-1);
        }
        // ─────────────────────────────────────────────────────────────────────────────────────────────

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

            // Y축(작업장)
            catAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "작업장",
                GapWidth = GapWidth,
                IsZoomEnabled = false,
                IsPanEnabled = false
            };
            foreach (var id in WorkplaceIds)
                catAxis.Labels.Add($"작업장 {id}");
            GanttModel.Axes.Add(catAxis);

            // 시뮬레이션 날짜 변경시 동기화
            simulationService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(simulationService.CurrentDate))
                    UpdateGantt();
            };

            view.WorkplaceFilterRequested += OnWorkplaceFilterRequested;

            UpdateGantt();
            view.GanttModel = GanttModel;
            view.SetActiveWorkplace(selectedWorkplaceId);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnWorkplaceFilterRequested(object sender, WorkplaceFilterRequestedEventArgs e)
        {
            if (e?.WorkplaceId.HasValue == true && !WorkplaceIds.Contains(e.WorkplaceId.Value))
            {
                selectedWorkplaceId = null;
            }
            else
            {
                selectedWorkplaceId = e?.WorkplaceId;
            }

            ViewCore.SetActiveWorkplace(selectedWorkplaceId);
            UpdateGantt();
        }

        private void UpdateGantt()
        {
            GanttModel.Series.Clear();

            var all = scheduleService.GetAllBlocks()?.ToList() ?? new List<Block>();
            DateTime now = simulationService.CurrentDate;

            // ── 전체 기간 계산(유효하지 않은 날짜 제거 + 안전 보정)
            DateTime globalStart, globalEnd;

            // default/MinValue/MaxValue 제외
            var validBlocks = all.Where(b =>
                    b != null &&
                    b.Start != default && b.Start != DateTime.MinValue && b.Start != DateTime.MaxValue)
                .ToList();

            if (validBlocks.Count > 0)
            {
                globalStart = validBlocks.Min(b => b.Start);
                // End는 GetEffectiveEnd() 중 유효한 값만
                var endCandidates = validBlocks
                    .Select(b => b.GetEffectiveEnd())
                    .Where(d => d.HasValue && d.Value != DateTime.MinValue && d.Value != DateTime.MaxValue)
                    .Select(d => d!.Value)
                    .ToList();

                globalEnd = endCandidates.Count > 0
                    ? endCandidates.Max()
                    : (now > globalStart ? now : globalStart);
            }
            else
            {
                globalStart = now;
                globalEnd = now.AddDays(1);
            }

            // 안전 보정 + 일 경계 정리
            EnsureValidRange(ref globalStart, ref globalEnd);
            globalStart = FloorDateSafe(globalStart);
            globalEnd = CeilDateSafe(globalEnd);

            if (now < globalStart) now = globalStart;
            if (now > globalEnd) now = globalEnd;

            // ── 축 범위: 전체 기간 + 소량 여백(경계 안전)
            var totalDays = Math.Max(1.0, (globalEnd - globalStart).TotalDays);
            var padDays = Math.Max(3.0, totalDays * 0.01);

            var axisMin = SafeAddDays(globalStart, -padDays); // ★경계 안전
            var axisMax = SafeAddDays(globalEnd, +padDays); // ★경계 안전

            dateAxis.Minimum = DateTimeAxis.ToDouble(axisMin);
            dateAxis.Maximum = DateTimeAxis.ToDouble(axisMax);

            // ── 작업장별 데이터 정리
            var byWp = all.GroupBy(b => b.DeployWorkplace)
                          .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

            var axisLabels = new List<string>();
            var rowSegments = new List<List<BlockSegment>>();

            var visibleWorkplaces = GetVisibleWorkplaces();

            foreach (var wpId in visibleWorkplaces)
            {
                var list = byWp.TryGetValue(wpId, out var l) ? l : null;
                var segments = BuildSegments(list, now);

                if (!ShouldExpandWorkplace(wpId))
                {
                    axisLabels.Add($"작업장 {wpId}");
                    rowSegments.Add(segments);
                }
                else
                {
                    var layers = BuildLayers(segments);

                    if (layers.Count == 0)
                    {
                        axisLabels.Add($"작업장 {wpId}");
                        rowSegments.Add(new List<BlockSegment>());
                    }
                    else
                    {
                        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                        {
                            var label = layerIndex == 0
                                ? $"작업장 {wpId}"
                                : $"작업장 {wpId} ({layerIndex + 1})";

                            axisLabels.Add(label);
                            rowSegments.Add(layers[layerIndex]);
                        }
                    }
                }
            }

            EnsureDefaultRows(axisLabels, rowSegments);

            catAxis.Labels.Clear();
            foreach (var label in axisLabels) catAxis.Labels.Add(label);

            // ── 현재일 수직선
            var nowLine = new LineSeries
            {
                Color = OxyColors.Gray,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            };
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), -0.5));
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), axisLabels.Count - 0.5));
            GanttModel.Series.Add(nowLine);

            // ── 막대 시리즈(배경 + 블록)
            var series = new IntervalBarSeries
            {
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                LabelFormatString = null,
                BarWidth = BarWidth
            };

            for (int categoryIndex = 0; categoryIndex < rowSegments.Count; categoryIndex++)
            {
                // 배경(작업장 가동 시간대: globalStart~now)
                series.Items.Add(new IntervalBarItem
                {
                    CategoryIndex = categoryIndex,
                    Start = DateTimeAxis.ToDouble(globalStart),
                    End = DateTimeAxis.ToDouble(now),
                    Color = OxyColors.LightGray
                });

                foreach (var segment in rowSegments[categoryIndex])
                {
                    series.Items.Add(new IntervalBarItem
                    {
                        CategoryIndex = categoryIndex,
                        Start = DateTimeAxis.ToDouble(segment.Start),
                        End = DateTimeAxis.ToDouble(segment.End),
                        Color = colorService.GetOxyColor(segment.Block.Name) // 색상 동기화
                    });
                }
            }

            GanttModel.Series.Add(series);
            GanttModel.InvalidatePlot(true);
        }

        private IEnumerable<int> GetVisibleWorkplaces()
        {
            if (selectedWorkplaceId.HasValue)
            {
                var id = selectedWorkplaceId.Value;
                if (WorkplaceIds.Contains(id))
                {
                    yield return id;
                    yield break;
                }
            }

            foreach (var id in WorkplaceIds)
            {
                yield return id;
            }
        }

        private bool ShouldExpandWorkplace(int workplaceId)
            => selectedWorkplaceId.HasValue && selectedWorkplaceId.Value == workplaceId;

        private void EnsureDefaultRows(ICollection<string> axisLabels, ICollection<List<BlockSegment>> rowSegments)
        {
            if (axisLabels.Count > 0)
            {
                return;
            }

            foreach (var id in GetVisibleWorkplaces())
            {
                axisLabels.Add($"작업장 {id}");
                rowSegments.Add(new List<BlockSegment>());
            }
        }

        private static List<BlockSegment> BuildSegments(IReadOnlyList<Block>? blocks, DateTime now)
        {
            var segments = new List<BlockSegment>();
            if (blocks == null) return segments;

            foreach (var block in blocks)
            {
                // 유효하지 않은 시작값 건너뜀
                if (block.Start == default || block.Start == DateTime.MinValue || block.Start >= now)
                    continue;

                var effectiveEnd = block.GetEffectiveEnd() ?? now;
                if (effectiveEnd <= block.Start) continue;

                var endClamped = effectiveEnd > now ? now : effectiveEnd;
                if (endClamped <= block.Start) continue;

                segments.Add(new BlockSegment(block, block.Start, endClamped));
            }

            return segments;
        }

        private static List<List<BlockSegment>> BuildLayers(List<BlockSegment> segments)
        {
            var layers = new List<List<BlockSegment>>();
            if (segments == null || segments.Count == 0) return layers;

            foreach (var segment in segments.OrderBy(s => s.Start).ThenBy(s => s.End))
            {
                bool placed = false;

                foreach (var layer in layers)
                {
                    if (layer.Count == 0 || layer[^1].End <= segment.Start)
                    {
                        layer.Add(segment);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                    layers.Add(new List<BlockSegment> { segment });
            }

            return layers;
        }

        // ===== 유틸 =====
        private readonly struct BlockSegment
        {
            public BlockSegment(Block block, DateTime start, DateTime end)
            {
                Block = block;
                Start = start;
                End = end;
            }
            public Block Block { get; }
            public DateTime Start { get; }
            public DateTime End { get; }
        }
    }
}
