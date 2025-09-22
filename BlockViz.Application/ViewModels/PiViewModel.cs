using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Waf.Applications;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using OxyPlot;
using OxyPlot.Series;
using BlockViz.Applications.Extensions;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class PiViewModel : ViewModel<IPiView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;
        private readonly IBlockColorService colorService;

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<PlotModel> PieModels { get; }

        private enum BaselineMode { GlobalStart, WorkplaceFirstStart }
        private const BaselineMode Baseline = BaselineMode.GlobalStart;

        // 리본 토글(라벨/퍼센트 표시)
        private IPieOptions pieOptions;

        [Import(AllowDefault = true)]
        public IPieOptions PieOptions
        {
            get => pieOptions;
            set
            {
                if (pieOptions == value) return;
                if (pieOptions != null) pieOptions.PropertyChanged -= OnPieOptionsChanged;
                pieOptions = value;
                if (pieOptions != null) pieOptions.PropertyChanged += OnPieOptionsChanged;
                UpdatePie();
            }
        }

        [ImportingConstructor]
        public PiViewModel(IPiView view,
                           IScheduleService scheduleService,
                           SimulationService simulationService,
                           IBlockColorService colorService) : base(view)
        {
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;
            this.colorService = colorService;

            PieModels = new ObservableCollection<PlotModel>();

            simulationService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(simulationService.CurrentDate))
                    UpdatePie();
            };

            UpdatePie();
            view.PieModels = PieModels;
        }

        private void OnPieOptionsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IPieOptions.ShowLabels))
                UpdatePie();
        }

        private void UpdatePie()
        {
            PieModels.Clear();

            var all = scheduleService.GetAllBlocks().ToList();
            var showLabels = pieOptions?.ShowLabels ?? false;
            if (!all.Any())
            {
                for (int wp = 1; wp <= 6; wp++)
                    PieModels.Add(BuildIdleOnlyModel(wp, 1.0, showLabels));
                return;
            }

            DateTime globalStart = all.Min(b => b.Start);
            DateTime now = simulationService.CurrentDate;
            DateTime globalEnd = all.Select(b => b.GetEffectiveEnd() ?? now).Max();

            if (now < globalStart) now = globalStart;
            if (now > globalEnd) now = globalEnd;

            for (int wp = 1; wp <= 6; wp++)
            {
                var ws = all.Where(b => b.DeployWorkplace == wp).OrderBy(b => b.Start).ToList();

                DateTime windowStart = Baseline == BaselineMode.GlobalStart
                    ? globalStart
                    : (ws.Any() ? ws.Min(b => b.Start) : globalStart);
                DateTime windowEnd = now;

                var model = NewPlotModelWithoutLegend($"작업장 {wp}");
                var series = NewPieSeriesWithToggle(showLabels);

                if (windowEnd <= windowStart)
                {
                    var emptySlice = new PieSlice("", 1.0) { Fill = OxyColors.LightGray };
                    ConfigureSliceLabel(emptySlice, string.Empty, 1.0, showLabels);
                    series.Slices.Add(emptySlice);
                    model.Series.Add(series);
                    PieModels.Add(model);
                    continue;
                }

                double totalDays = (windowEnd - windowStart).TotalDays;

                if (!ws.Any())
                {
                    var idleSlice = new PieSlice("", totalDays) { Fill = OxyColors.LightGray };
                    ConfigureSliceLabel(idleSlice, string.Empty, Math.Max(totalDays, 1.0), showLabels);
                    series.Slices.Add(idleSlice);
                    model.Series.Add(series);
                    PieModels.Add(model);
                    continue;
                }

                // 윈도우 클리핑
                var clipped = new List<(string Name, DateTime Start, DateTime End)>();
                foreach (var b in ws)
                {
                    var effectiveEnd = b.GetEffectiveEnd() ?? windowEnd;
                    if (effectiveEnd < windowStart) continue;
                    var s = b.Start < windowStart ? windowStart : b.Start;
                    var e = effectiveEnd > windowEnd ? windowEnd : effectiveEnd;
                    if (e <= s) continue;
                    var displayName = b.GetDisplayName();
                    clipped.Add((displayName, s, e));
                }

                if (!clipped.Any())
                {
                    var idleSlice = new PieSlice("", totalDays) { Fill = OxyColors.LightGray };
                    ConfigureSliceLabel(idleSlice, string.Empty, Math.Max(totalDays, 1.0), showLabels);
                    series.Slices.Add(idleSlice);
                    model.Series.Add(series);
                    PieModels.Add(model);
                    continue;
                }

                // 경계점
                var ticks = new SortedSet<DateTime> { windowStart, windowEnd };
                foreach (var c in clipped)
                {
                    ticks.Add(c.Start);
                    ticks.Add(c.End);
                }
                var t = ticks.OrderBy(x => x).ToList();

                // 구간별 집계
                var durByBlock = new Dictionary<string, double>(StringComparer.Ordinal);
                double idleDays = 0;

                for (int i = 0; i < t.Count - 1; i++)
                {
                    var s = t[i];
                    var e = t[i + 1];
                    double seg = (e - s).TotalDays;
                    if (seg <= 0) continue;

                    var active = clipped.Where(c => c.Start <= s && c.End > s).ToList();
                    if (active.Count == 0)
                    {
                        idleDays += seg;
                    }
                    else
                    {
                        double share = seg / active.Count;
                        foreach (var a in active)
                        {
                            if (!durByBlock.ContainsKey(a.Name)) durByBlock[a.Name] = 0;
                            durByBlock[a.Name] += share;
                        }
                    }
                }

                // 파이 조각: 라벨 토글에 따라 블록명/퍼센트를 구성
                double totalForLabels = durByBlock.Sum(x => Math.Max(0.0, x.Value));
                if (idleDays > 0)
                    totalForLabels += idleDays;
                if (totalForLabels <= 0)
                    totalForLabels = Math.Max(totalDays, 1.0);

                foreach (var kv in durByBlock.OrderByDescending(x => x.Value))
                {
                    if (kv.Value <= 0) continue;
                    var slice = new PieSlice(kv.Key, kv.Value)
                    {
                        Fill = colorService.GetOxyColor(kv.Key)
                    };
                    ConfigureSliceLabel(slice, kv.Key, totalForLabels, showLabels);
                    series.Slices.Add(slice);
                }

                if (idleDays > 0)
                {
                    var idleSlice = new PieSlice("", idleDays) { Fill = OxyColors.LightGray };
                    ConfigureSliceLabel(idleSlice, string.Empty, totalForLabels, showLabels);
                    series.Slices.Add(idleSlice);
                }

                if (series.Slices.Count == 0)
                {
                    var fallback = new PieSlice("", totalDays) { Fill = OxyColors.LightGray };
                    ConfigureSliceLabel(fallback, string.Empty, Math.Max(totalDays, 1.0), showLabels);
                    series.Slices.Add(fallback);
                }

                model.Series.Add(series);
                PieModels.Add(model);
            }
        }

        // ── 헬퍼 ───────────────────────────────────────────────────────────
        private static PlotModel NewPlotModelWithoutLegend(string title)
            => new PlotModel { Title = title, IsLegendVisible = false };

        // 라벨/퍼센트 토글(퍼센트는 정수 “42%”)
        private static PieSeries NewPieSeriesWithToggle(bool showLabels)
        {
            return new PieSeries
            {
                StrokeThickness = 0.5,
                AngleSpan = 360,
                StartAngle = 0,
                TickHorizontalLength = 0,
                TickRadialLength = 0,
                TickLabelDistance = 0,
                InsideLabelFormat = showLabels ? "{0}" : "{2:0}%",
                OutsideLabelFormat = null
            };
        }

        private static void ConfigureSliceLabel(PieSlice slice, string? displayName, double totalValue, bool showLabels)
        {
            if (!showLabels)
            {
                // Since 'PieSlice.Label' is read-only, we cannot directly assign to it.
                // Instead, we can use the 'ToCode()' method to generate a string representation
                // or handle the label display logic externally.
                return;
            }

            var name = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            var percent = totalValue > 0 ? slice.Value / totalValue * 100.0 : 0.0;
            var percentText = $"{percent:0}%";

            // Handle label display logic externally or use another property/method to reflect the label.
            // Example: Log or store the label information for external use.
            var label = string.IsNullOrEmpty(name)
                ? percentText
                : $"{name}\n{percentText}";
        }

        private static PlotModel BuildIdleOnlyModel(int workplaceId, double value, bool showLabels)
        {
            var model = NewPlotModelWithoutLegend($"작업장 {workplaceId}");
            var s = NewPieSeriesWithToggle(showLabels);
            var slice = new PieSlice("", value) { Fill = OxyColors.LightGray };
            ConfigureSliceLabel(slice, string.Empty, Math.Max(value, 1.0), showLabels);
            s.Slices.Add(slice);
            model.Series.Add(s);
            return model;
        }
    }
}
