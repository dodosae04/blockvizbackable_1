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
            if (!all.Any())
            {
                for (int wp = 1; wp <= 6; wp++)
                    PieModels.Add(BuildIdleOnlyModel(wp, 1.0));
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
                var series = NewPieSeriesWithToggle();

                if (windowEnd <= windowStart)
                {
                    series.Slices.Add(new PieSlice("", 1.0) { Fill = OxyColors.LightGray });
                    model.Series.Add(series);
                    PieModels.Add(model);
                    continue;
                }

                double totalDays = (windowEnd - windowStart).TotalDays;

                if (!ws.Any())
                {
                    series.Slices.Add(new PieSlice("", totalDays) { Fill = OxyColors.LightGray });
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
                    series.Slices.Add(new PieSlice("", totalDays) { Fill = OxyColors.LightGray });
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

                // 파이 조각: Label에 블록명 저장(툴팁용), 화면 라벨은 토글에 따라 퍼센트만/숨김
                foreach (var kv in durByBlock.OrderByDescending(x => x.Value))
                {
                    if (kv.Value <= 0) continue;
                    series.Slices.Add(new PieSlice(kv.Key, kv.Value)
                    {
                        Fill = colorService.GetOxyColor(kv.Key)
                    });
                }

                if (idleDays > 0)
                    series.Slices.Add(new PieSlice("", idleDays) { Fill = OxyColors.LightGray });

                if (series.Slices.Count == 0)
                    series.Slices.Add(new PieSlice("", totalDays) { Fill = OxyColors.LightGray });

                model.Series.Add(series);
                PieModels.Add(model);
            }
        }

        // ── 헬퍼 ───────────────────────────────────────────────────────────
        private static PlotModel NewPlotModelWithoutLegend(string title)
            => new PlotModel { Title = title, IsLegendVisible = false };

        // 라벨/퍼센트 토글(퍼센트는 정수 “42%”)
        private PieSeries NewPieSeriesWithToggle()
        {
            var s = new PieSeries
            {
                StrokeThickness = 0.5,
                AngleSpan = 360,
                StartAngle = 0,
                TickHorizontalLength = 0,
                TickRadialLength = 0,
                TickLabelDistance = 0
            };

            if (pieOptions != null && pieOptions.ShowLabels)
            {
                s.InsideLabelFormat = "{2:0}%"; // {0}=Label, {1}=Value, {2}=Percentage
                s.OutsideLabelFormat = null;
            }
            else
            {
                s.InsideLabelFormat = null;
                s.OutsideLabelFormat = null;
            }
            return s;
        }

        private static PlotModel BuildIdleOnlyModel(int workplaceId, double value)
        {
            var model = NewPlotModelWithoutLegend($"작업장 {workplaceId}");
            var s = new PieSeries
            {
                StrokeThickness = 0.5,
                AngleSpan = 360,
                StartAngle = 0,
                InsideLabelFormat = null,
                OutsideLabelFormat = null,
                TickHorizontalLength = 0,
                TickRadialLength = 0,
                TickLabelDistance = 0
            };
            s.Slices.Add(new PieSlice("", value) { Fill = OxyColors.LightGray });
            model.Series.Add(s);
            return model;
        }
    }
}
