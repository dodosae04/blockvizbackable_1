using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Waf.Applications;
using BlockViz.Applications.Extensions;
using BlockViz.Applications.Models;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using BlockViz.Domain.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace BlockViz.Applications.ViewModels
{
    /// <summary>
    /// 작업장별 간트 뷰모델(안전한 축 패딩, 색상 동기화, 블록명 Title 저장)
    /// </summary>
    [Export]
    public sealed class GanttViewModel : ViewModel<IGanttView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;
        private readonly IBlockColorService colorService;
        private readonly ISelectionService selectionService;

        private int? selectedWorkplaceId;
        private bool expandAll;

        public PlotModel GanttModel { get; }

        private readonly DateTimeAxis dateAxis;
        private readonly CategoryAxis catAxis;

        private static readonly int[] WorkplaceIds = { 1, 2, 3, 4, 5, 6 };
        private const double BarWidth = 0.78;
        private const double GapWidth = 0.18;

        // ───────────────────── DateTime 안전 유틸 ─────────────────────
        private static DateTime SafeAddDays(DateTime dt, double days)
        {
            if (days == 0) return dt;

            // AddDays는 double을 받지만 Min/Max 경계를 넘으면 예외 → 경계에서 clamp
            if (days > 0)
            {
                var remain = (DateTime.MaxValue - dt).TotalDays;
                if (remain <= 0) return DateTime.MaxValue;
                if (days > remain) days = Math.Floor(remain);
                return dt.AddDays(days);
            }
            else
            {
                var remain = (dt - DateTime.MinValue).TotalDays;
                if (remain <= 0) return DateTime.MinValue;
                if (-days > remain) days = -Math.Floor(remain);
                return dt.AddDays(days);
            }
        }

        private static DateTime FloorDate(DateTime dt)
            => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Kind);

        private static DateTime CeilDateEndOfDay(DateTime dt)
            => new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, dt.Kind);
        // ─────────────────────────────────────────────────────────────

        [ImportingConstructor]
        public GanttViewModel(
            IGanttView view,
            IScheduleService scheduleService,
            SimulationService simulationService,
            IBlockColorService colorService,
            ISelectionService selectionService) : base(view)
        {
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;
            this.colorService = colorService;
            this.selectionService = selectionService;

            GanttModel = new PlotModel { Title = "작업장 별 블록 생산 일정" };

            // X축(일 단위)
            dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "기간",
                StringFormat = "yyyy-MM-dd",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Angle = -45,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MinimumPadding = 0,
                MaximumPadding = 0
            };
            GanttModel.Axes.Add(dateAxis);

            // Y축(작업장)
            catAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "작업장",
                GapWidth = GapWidth,
                IsPanEnabled = false,
                IsZoomEnabled = false
            };
            foreach (var id in WorkplaceIds) catAxis.Labels.Add($"작업장 {id}");
            GanttModel.Axes.Add(catAxis);

            // 시뮬레이션 날짜 변경 시 동기화
            simulationService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(simulationService.CurrentDate))
                    UpdateGantt();
            };

            view.WorkplaceFilterRequested += OnWorkplaceFilterRequested;
            view.ExpandAllChanged += OnExpandAllChanged;
            view.BlockClicked += b => this.selectionService.SelectedBlock = b;

            UpdateGantt();
            view.GanttModel = GanttModel;
            view.SetActiveWorkplace(selectedWorkplaceId);
            view.SetExpandAllState(expandAll);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnWorkplaceFilterRequested(object sender, WorkplaceFilterRequestedEventArgs e)
        {
            if (e?.WorkplaceId.HasValue == true && WorkplaceIds.Contains(e.WorkplaceId.Value))
                selectedWorkplaceId = e.WorkplaceId.Value;
            else
                selectedWorkplaceId = null;

            ViewCore.SetActiveWorkplace(selectedWorkplaceId);
            UpdateGantt();
        }

        private void OnExpandAllChanged(object sender, bool isExpanded)
        {
            expandAll = isExpanded;
            UpdateGantt();
        }

        private void UpdateGantt()
        {
            GanttModel.Series.Clear();

            var blocks = scheduleService.GetAllBlocks()?.ToList() ?? new List<Block>();
            var now = simulationService.CurrentDate;

            var valid = blocks.Where(b =>
                b != null &&
                b.Start != default &&
                b.Start != DateTime.MinValue &&
                b.Start != DateTime.MaxValue).ToList();

            DateTime globalStart, globalEnd;
            if (valid.Count > 0)
            {
                globalStart = valid.Min(b => b.Start);
                var endCandidates = valid
                    .Select(b => b.GetEffectiveEnd())
                    .Where(d => d.HasValue && d.Value != DateTime.MinValue && d.Value != DateTime.MaxValue)
                    .Select(d => d!.Value)
                    .ToList();
                globalEnd = endCandidates.Count > 0 ? endCandidates.Max() : now;
            }
            else
            {
                globalStart = now;
                globalEnd = now.AddDays(1);
            }

            if (globalStart > globalEnd) (globalStart, globalEnd) = (globalEnd, globalStart);
            if (globalStart == globalEnd) globalEnd = SafeAddDays(globalStart, 1);

            globalStart = FloorDate(globalStart);
            globalEnd = CeilDateEndOfDay(globalEnd);

            if (now < globalStart) now = globalStart;
            if (now > globalEnd) now = globalEnd;

            var totalDays = Math.Max(1.0, (globalEnd - globalStart).TotalDays);
            var padDays = Math.Max(3.0, totalDays * 0.01);

            var axisMin = SafeAddDays(globalStart, -padDays);
            var axisMax = SafeAddDays(globalEnd, +padDays);

            dateAxis.Minimum = DateTimeAxis.ToDouble(axisMin);
            dateAxis.Maximum = DateTimeAxis.ToDouble(axisMax);

            var byWp = blocks
                .Where(b => b != null)
                .GroupBy(b => b.DeployWorkplace)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

            var visibleWp = GetVisibleWorkplaces().ToList();
            var categoryLabels = new List<string>();

            var series = new IntervalBarSeries
            {
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                BarWidth = BarWidth,
                LabelFormatString = null,
                TrackerFormatString = "{0}\n{1}\n시작: {2:yyyy-MM-dd}\n끝: {3:yyyy-MM-dd}"
            };

            int categoryIndex = 0;
            foreach (var wpId in visibleWp)
            {
                var label = $"작업장 {wpId}";
                var blocksInWorkplace = byWp.TryGetValue(wpId, out var list) ? list : new List<Block>();
                bool isExpanded = expandAll || (selectedWorkplaceId.HasValue && selectedWorkplaceId.Value == wpId);

                if (!isExpanded)
                {
                    categoryLabels.Add(label);
                    AddBackground(series, categoryIndex, globalStart, now);
                    var intervals = BuildIntervals(blocksInWorkplace, now);
                    AddBlockItems(series, categoryIndex, intervals);
                    categoryIndex++;
                    continue;
                }

                var layers = BuildLayeredIntervals(blocksInWorkplace, now);
                if (layers.Count == 0)
                {
                    categoryLabels.Add(label);
                    AddBackground(series, categoryIndex, globalStart, now);
                    categoryIndex++;
                    continue;
                }

                for (int layer = 0; layer < layers.Count; layer++)
                {
                    categoryLabels.Add(layer == 0 ? label : string.Empty);
                    AddBackground(series, categoryIndex, globalStart, now);
                    AddBlockItems(series, categoryIndex, layers[layer]);
                    categoryIndex++;
                }
            }

            catAxis.Labels.Clear();
            foreach (var label in categoryLabels)
            {
                catAxis.Labels.Add(label);
            }

            var nowLine = new LineSeries
            {
                Color = OxyColors.Gray,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            };
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), -0.5));
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), categoryLabels.Count - 0.5));

            GanttModel.Series.Add(nowLine);
            GanttModel.Series.Add(series);
            GanttModel.InvalidatePlot(true);
        }

        private void AddBackground(IntervalBarSeries series, int categoryIndex, DateTime start, DateTime end)
        {
            series.Items.Add(new IntervalBarItem
            {
                CategoryIndex = categoryIndex,
                Start = DateTimeAxis.ToDouble(start),
                End = DateTimeAxis.ToDouble(end),
                Color = OxyColors.LightGray
            });
        }

        private void AddBlockItems(IntervalBarSeries series, int categoryIndex, IEnumerable<(Block Block, DateTime Start, DateTime End)> intervals)
        {
            foreach (var interval in intervals)
            {
                var block = interval.Block;
                if (block == null) continue;

                var displayName = block.GetDisplayName();
                var item = new BlockIntervalBarItem(block)
                {
                    CategoryIndex = categoryIndex,
                    Start = DateTimeAxis.ToDouble(interval.Start),
                    End = DateTimeAxis.ToDouble(interval.End),
                    Color = colorService.GetOxyColor(displayName)
                };
                series.Items.Add(item);
            }
        }

        private static List<(Block Block, DateTime Start, DateTime End)> BuildIntervals(IEnumerable<Block> blocks, DateTime now)
        {
            var result = new List<(Block Block, DateTime Start, DateTime End)>();

            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (block.Start >= now) continue;

                var effectiveEnd = block.GetEffectiveEnd() ?? now;
                var end = effectiveEnd > now ? now : effectiveEnd;
                if (end <= block.Start) continue;

                result.Add((block, block.Start, end));
            }

            return result;
        }

        private static List<List<(Block Block, DateTime Start, DateTime End)>> BuildLayeredIntervals(IEnumerable<Block> blocks, DateTime now)
        {
            var intervals = BuildIntervals(blocks, now)
                .OrderBy(i => i.Start)
                .ThenBy(i => i.End)
                .ToList();

            var layers = new List<List<(Block Block, DateTime Start, DateTime End)>>();
            var layerEnds = new List<DateTime>();

            foreach (var interval in intervals)
            {
                int targetLayer = -1;
                for (int i = 0; i < layerEnds.Count; i++)
                {
                    if (interval.Start >= layerEnds[i])
                    {
                        targetLayer = i;
                        break;
                    }
                }

                if (targetLayer == -1)
                {
                    targetLayer = layers.Count;
                    layers.Add(new List<(Block Block, DateTime Start, DateTime End)>());
                    layerEnds.Add(interval.End);
                }
                else
                {
                    layerEnds[targetLayer] = interval.End;
                }

                layers[targetLayer].Add(interval);
            }

            return layers;
        }

        private IEnumerable<int> GetVisibleWorkplaces()
        {
            if (!expandAll && selectedWorkplaceId.HasValue && WorkplaceIds.Contains(selectedWorkplaceId.Value))
            {
                yield return selectedWorkplaceId.Value;
                yield break;
            }

            foreach (var id in WorkplaceIds)
            {
                yield return id;
            }
        }
    }
}
