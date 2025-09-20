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

namespace BlockViz.Applications.ViewModels
{
    /// <summary>
    /// 작업장별 1행(총 6행) · 오버레이 간트 (미래 구간 미표시, 색상 동기화 지원)
    /// - 회색 기본 막대를 먼저 깔고, 같은 작업장의 블록을 '시작일 오름차순'으로 now까지 덧그립니다.
    /// - ColorResolver 훅으로 3D/PI와 동일 색상을 사용합니다.
    /// - 축 범위: 데이터 전체기간 + 살짝의 여백, 막대는 now까지만 그립니다(미래 X).
    /// </summary>
    [Export]
    public sealed class GanttViewModel : ViewModel<IGanttView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;

        public PlotModel GanttModel { get; }

        private readonly DateTimeAxis dateAxis;
        private readonly CategoryAxis catAxis;

        // ====== 색상 동기화 훅 ======
        // 앱 초기화 시 반드시 연결해 주세요 (예시):
        // GanttViewModel.ColorResolver = b => {
        //     var c = CommonPalette.GetColor(b); // Media.Color (3D/PI에서 쓰는 함수)
        //     return OxyColor.FromArgb(c.A, c.R, c.G, c.B);
        // };
        public static Func<Block, OxyColor> ColorResolver { get; set; }

        // fallback (ColorResolver 미연결 시 임시 팔레트)
        private readonly Dictionary<string, OxyColor> fallbackMap = new();
        private readonly OxyColor[] fallbackPalette =
        {
            OxyColors.Red, OxyColors.Orange, OxyColors.Yellow,
            OxyColors.LimeGreen, OxyColors.SkyBlue, OxyColors.MediumPurple,
            OxyColors.SandyBrown, OxyColors.Teal, OxyColors.DeepPink
        };

        // 작업장 라벨(6행 고정)
        private static readonly int[] WorkplaceIds = { 1, 2, 3, 4, 5, 6 };

        // 보기 옵션
        private const double BarWidth = 0.78;  // 카테고리 높이 대비 두께
        private const double GapWidth = 0.18;  // 카테고리 간 여백(=1-BarWidth)

        [ImportingConstructor]
        public GanttViewModel(IGanttView view,
                              IScheduleService scheduleService,
                              SimulationService simulationService) : base(view)
        {
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;

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

            var all = scheduleService.GetAllBlocks()?.ToList() ?? new List<Block>();
            if (all.Count == 0)
            {
                GanttModel.InvalidatePlot(true);
                return;
            }

            // 전체 기간 (엑셀 첫 시작 ~ 마지막 종료)
            var globalStart = FloorDate(all.Min(b => b.Start));
            var globalEnd = CeilDate(all.Max(b => b.End));

            // 현재일(now) — 미래 구간은 그리지 않음
            var now = simulationService.CurrentDate;
            if (now < globalStart) now = globalStart;
            if (now > globalEnd) now = globalEnd;

            // 축 범위: 전체 기간 + 소량 여백
            var totalDays = Math.Max(1.0, (globalEnd - globalStart).TotalDays);
            var padDays = Math.Max(3.0, totalDays * 0.01);
            var axisMin = globalStart.AddDays(-padDays);
            var axisMax = globalEnd.AddDays(+padDays);

            dateAxis.Minimum = DateTimeAxis.ToDouble(axisMin);
            dateAxis.Maximum = DateTimeAxis.ToDouble(axisMax);

            // 현재일 수직선
            var nowLine = new LineSeries
            {
                Color = OxyColors.Gray,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            };
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), -0.5));
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), WorkplaceIds.Length - 0.5));
            GanttModel.Series.Add(nowLine);

            // 막대 시리즈(오버레이)
            var series = new IntervalBarSeries
            {
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                LabelFormatString = null,
                BarWidth = BarWidth
            };

            // 작업장별: 회색 바탕( globalStart ~ now ) → 각 블록을 시작일 오름차순으로 now까지 덧그림
            var byWp = all.GroupBy(b => b.DeployWorkplace)
                          .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

            foreach (var wpId in WorkplaceIds)
            {
                int catIndex = wpId - 1;

                // (1) 바탕: now까지만(미래 X)
                series.Items.Add(new IntervalBarItem
                {
                    CategoryIndex = catIndex,
                    Start = DateTimeAxis.ToDouble(globalStart),
                    End = DateTimeAxis.ToDouble(now),
                    Color = OxyColors.LightGray
                });

                // (2) 블록 오버레이 — now까지만
                if (!byWp.TryGetValue(wpId, out var list)) continue;

                foreach (var b in list) // Start 오름차순 → 나중에 시작한 게 위에 올라감
                {
                    if (b.End <= b.Start) continue;
                    if (b.Start >= now) break;   // 아직 시작 안했으면 그리지 않음

                    var endClamped = b.End > now ? now : b.End;
                    if (endClamped <= b.Start) continue;

                    series.Items.Add(new IntervalBarItem
                    {
                        CategoryIndex = catIndex,
                        Start = DateTimeAxis.ToDouble(b.Start),
                        End = DateTimeAxis.ToDouble(endClamped),
                        Color = ResolveColor(b)
                    });
                }
            }

            GanttModel.Series.Add(series);
            GanttModel.InvalidatePlot(true);
        }

        // ===== 유틸 =====
        private static DateTime FloorDate(DateTime dt) => dt.Date;
        private static DateTime CeilDate(DateTime dt) =>
            dt.TimeOfDay == TimeSpan.Zero ? dt.Date : dt.Date.AddDays(1);

        private OxyColor ResolveColor(Block b)
        {
            // 1) 외부 동기화 훅이 연결돼 있으면 그것 사용(3D/PI와 완전 동일)
            if (ColorResolver != null) return ColorResolver(b);

            // 2) 미연결 시 임시 팔레트 (이 경우 3D/PI와 색이 다를 수 있음)
            var key = string.IsNullOrWhiteSpace(b.Name) ? $"#{b.BlockID}" : b.Name.Trim();
            if (!fallbackMap.TryGetValue(key, out var c))
            {
                c = fallbackPalette[fallbackMap.Count % fallbackPalette.Length];
                fallbackMap[key] = c;
            }
            return c;
        }
    }
}
