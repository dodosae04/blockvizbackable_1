// ✅ PiViewModel.cs — 리본 토글로 파이차트 라벨/퍼센트 표시 On/Off 지원 (퍼센트 “42%” 표기)
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

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class PiViewModel : ViewModel<IPiView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<PlotModel> PieModels { get; }

        // 집계 기준: GlobalStart(엑셀 전체 최소 시작일) → Now
        private enum BaselineMode { GlobalStart, WorkplaceFirstStart }
        private const BaselineMode Baseline = BaselineMode.GlobalStart;

        private readonly Dictionary<string, OxyColor> colorMap = new();
        private readonly OxyColor[] palette = new[]
        {
            OxyColors.Red, OxyColors.Orange, OxyColors.Yellow,
            OxyColors.LimeGreen, OxyColors.SkyBlue, OxyColors.MediumPurple,
            OxyColors.Teal, OxyColors.Brown, OxyColors.Pink
        };

        // 리본 토글 상태 주입
        private IPieOptions pieOptions;

        [Import(AllowDefault = true)]
        public IPieOptions PieOptions
        {
            get => pieOptions;
            set
            {
                if (pieOptions == value) return;

                if (pieOptions != null)
                    pieOptions.PropertyChanged -= OnPieOptionsChanged;

                pieOptions = value;

                if (pieOptions != null)
                    pieOptions.PropertyChanged += OnPieOptionsChanged;

                UpdatePie();
            }
        }

        [ImportingConstructor]
        public PiViewModel(IPiView view,
                           IScheduleService scheduleService,
                           SimulationService simulationService) : base(view)
        {
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;

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

        private OxyColor GetColor(string key)
        {
            if (!colorMap.ContainsKey(key))
                colorMap[key] = palette[colorMap.Count % palette.Length];
            return colorMap[key];
        }

        private void UpdatePie()
        {
            PieModels.Clear();

            var all = scheduleService.GetAllBlocks().ToList();
            if (!all.Any())
            {
                for (int wp = 1; wp <= 6; wp++)
                    PieModels.Add(BuildIdleOnlyModel(wp, 1.0)); // 100% 미사용
                return;
            }

            DateTime globalStart = all.Min(b => b.Start);
            DateTime globalEnd = all.Max(b => b.End);
            DateTime now = simulationService.CurrentDate;

            if (now < globalStart) now = globalStart;
            if (now > globalEnd) now = globalEnd;

            for (int wp = 1; wp <= 6; wp++)
            {
                var ws = all.Where(b => b.DeployWorkplace == wp).OrderBy(b => b.Start).ToList();

                // 집계 창: (옵션) GlobalStart 또는 작업장 최초 시작 ~ Now
                DateTime windowStart = Baseline == BaselineMode.GlobalStart
                    ? globalStart
                    : (ws.Any() ? ws.Min(b => b.Start) : globalStart);
                DateTime windowEnd = now;

                var model = NewPlotModelWithoutLegend($"작업장 {wp}");
                var series = NewPieSeriesWithToggle(); // ⬅ 토글 반영 버전

                if (windowEnd <= windowStart)
                {
                    // 시간 진행 전: 100% 미사용
                    series.Slices.Add(new PieSlice("", 1.0) { Fill = OxyColors.LightGray });
                    model.Series.Add(series);
                    PieModels.Add(model);
                    continue;
                }

                double totalDays = (windowEnd - windowStart).TotalDays;

                if (!ws.Any())
                {
                    // 블록 자체가 없으면 전부 미사용
                    series.Slices.Add(new PieSlice("", totalDays) { Fill = OxyColors.LightGray });
                    model.Series.Add(series);
                    PieModels.Add(model);
                    continue;
                }

                // 1) 윈도우로 클리핑
                var clipped = ws.Select(b => new
                {
                    Name = b.Name,
                    S = b.Start < windowStart ? windowStart : b.Start,
                    E = b.End > windowEnd ? windowEnd : b.End
                })
                .Where(x => x.E > x.S)
                .ToList();

                if (!clipped.Any())
                {
                    series.Slices.Add(new PieSlice("", totalDays) { Fill = OxyColors.LightGray });
                    model.Series.Add(series);
                    PieModels.Add(model);
                    continue;
                }

                // 2) 경계점 수집
                var ticks = new SortedSet<DateTime> { windowStart, windowEnd };
                foreach (var c in clipped) { ticks.Add(c.S); ticks.Add(c.E); }
                var t = ticks.OrderBy(x => x).ToList();

                // 3) 구간별 활성 블록 집계(겹침은 균등 분배)
                var durByBlock = new Dictionary<string, double>(StringComparer.Ordinal);
                double idleDays = 0;

                for (int i = 0; i < t.Count - 1; i++)
                {
                    var s = t[i];
                    var e = t[i + 1];
                    double seg = (e - s).TotalDays;
                    if (seg <= 0) continue;

                    var active = clipped.Where(c => c.S <= s && c.E > s).ToList();
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

                // 4) 파이 조각 추가 — 라벨 텍스트는 빈 문자열("") 유지
                foreach (var kv in durByBlock.OrderByDescending(x => x.Value))
                {
                    if (kv.Value <= 0) continue;
                    series.Slices.Add(new PieSlice("", kv.Value) { Fill = GetColor(kv.Key) });
                }

                if (idleDays > 0)
                {
                    series.Slices.Add(new PieSlice("", idleDays) { Fill = OxyColors.LightGray });
                }

                // 방어: 아무 조각도 없으면 전부 미사용
                if (series.Slices.Count == 0)
                {
                    series.Slices.Add(new PieSlice("", totalDays) { Fill = OxyColors.LightGray });
                }

                model.Series.Add(series);
                PieModels.Add(model);
            }
        }

        // ── 헬퍼 ───────────────────────────────────────────────────────────
        private static PlotModel NewPlotModelWithoutLegend(string title)
        {
            return new PlotModel
            {
                Title = title,
                IsLegendVisible = false
            };
        }

        // ★ 라벨/퍼센트 토글 반영 (퍼센트는 정수 “42%” 형식으로 표기)
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
                // {2} = Percentage (0~100 값) → 추가 곱셈 없이 정수%만 표시
                s.InsideLabelFormat = "{2:0}%";
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
