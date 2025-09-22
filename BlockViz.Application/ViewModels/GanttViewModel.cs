using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Waf.Applications;
using BlockViz.Applications.Extensions;
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

        private int? selectedWorkplaceId;

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
            IBlockColorService colorService) : base(view)
        {
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;
            this.colorService = colorService;

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

            UpdateGantt();
            view.GanttModel = GanttModel;
            view.SetActiveWorkplace(selectedWorkplaceId);
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

        private void UpdateGantt()
        {
            GanttModel.Series.Clear();

            var blocks = scheduleService.GetAllBlocks()?.ToList() ?? new List<Block>();
            var now = simulationService.CurrentDate;

            // 유효 시작이 있는 블록만 대상으로 전체 기간 파악( MinValue/MaxValue, default 제외 )
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
                // 데이터 없으면 오늘 ~ 내일
                globalStart = now;
                globalEnd = now.AddDays(1);
            }

            // 보정 & 경계 정리
            if (globalStart > globalEnd) (globalStart, globalEnd) = (globalEnd, globalStart);
            if (globalStart == globalEnd) globalEnd = SafeAddDays(globalStart, 1);

            globalStart = FloorDate(globalStart);
            globalEnd = CeilDateEndOfDay(globalEnd);

            if (now < globalStart) now = globalStart;
            if (now > globalEnd) now = globalEnd;

            // 축 패딩(안전 가드 사용) — 데이터가 1~2일이어도 최소 3일 패딩을 시도하되, 경계 밖으로 나가지 않게 clamp
            var totalDays = Math.Max(1.0, (globalEnd - globalStart).TotalDays);
            var padDays = Math.Max(3.0, totalDays * 0.01);

            var axisMin = SafeAddDays(globalStart, -padDays);
            var axisMax = SafeAddDays(globalEnd, +padDays);

            dateAxis.Minimum = DateTimeAxis.ToDouble(axisMin);
            dateAxis.Maximum = DateTimeAxis.ToDouble(axisMax);

            // ── 작업장별 세그먼트 구성
            var byWp = blocks.GroupBy(b => b.DeployWorkplace)
                             .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

            var visibleWp = GetVisibleWorkplaces().ToList();
            catAxis.Labels.Clear();
            foreach (var id in visibleWp) catAxis.Labels.Add($"작업장 {id}");

            // 현재일 수직선
            var nowLine = new LineSeries
            {
                Color = OxyColors.Gray,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            };
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), -0.5));
            nowLine.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), visibleWp.Count - 0.5));
            GanttModel.Series.Add(nowLine);

            // 막대 시리즈(배경 + 실제 블록)
            var series = new IntervalBarSeries
            {
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                BarWidth = BarWidth,
                LabelFormatString = null,
                TrackerFormatString = "{0}\n{1}\n시작: {2:yyyy-MM-dd}\n끝: {3:yyyy-MM-dd}"
            };

            for (int cat = 0; cat < visibleWp.Count; cat++)
            {
                int wpId = visibleWp[cat];

                // 배경(가동 영역: globalStart~now)
                series.Items.Add(new IntervalBarItem
                {
                    CategoryIndex = cat,
                    Start = DateTimeAxis.ToDouble(globalStart),
                    End = DateTimeAxis.ToDouble(now),
                    Color = OxyColors.LightGray
                });

                if (!byWp.TryGetValue(wpId, out var list) || list.Count == 0) continue;

                foreach (var b in list)
                {
                    // 미래 시작은 표시하지 않음
                    if (b.Start >= now) continue;

                    var effEnd = b.GetEffectiveEnd() ?? now;
                    var end = effEnd > now ? now : effEnd;
                    if (end <= b.Start) continue;

                    series.Items.Add(new IntervalBarItem
                    {
                        CategoryIndex = cat,
                        Start = DateTimeAxis.ToDouble(b.Start),
                        End = DateTimeAxis.ToDouble(end),
                        Color = colorService.GetOxyColor(b.Name), // 색상 동기화
                        Title = b.Name                              // ← 블록명 저장(Tag 대신)
                    });
                }
            }

            GanttModel.Series.Add(series);
            GanttModel.InvalidatePlot(true);
        }

        private IEnumerable<int> GetVisibleWorkplaces()
        {
            if (selectedWorkplaceId.HasValue && WorkplaceIds.Contains(selectedWorkplaceId.Value))
            {
                yield return selectedWorkplaceId.Value;
                yield break;
            }
            foreach (var id in WorkplaceIds) yield return id;
        }
    }
}
