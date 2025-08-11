// ✅ 수정된 PiViewModel.cs - BlockName 기준 고정 색상 적용
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Collections.ObjectModel;
using System.Linq;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using OxyPlot;
using OxyPlot.Series;
using System.Waf.Applications;
using System.Collections.Generic;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class PiViewModel : ViewModel<IPiView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;
        private readonly Dictionary<string, OxyColor> colorMap = new();
        private readonly OxyColor[] palette = new[]
        {
            OxyColors.Red, OxyColors.Orange, OxyColors.Yellow,
            OxyColors.LimeGreen, OxyColors.SkyBlue, OxyColors.Purple,
            OxyColors.Brown, OxyColors.Teal, OxyColors.Pink
        };

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<PlotModel> PieModels { get; }

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

        private OxyColor GetColor(string name)
        {
            if (!colorMap.ContainsKey(name))
                colorMap[name] = palette[colorMap.Count % palette.Length];
            return colorMap[name];
        }

        private void UpdatePie()
        {
            PieModels.Clear();
            var blocks = scheduleService.GetAllBlocks().ToList();
            if (!blocks.Any()) return;

            var currentDate = simulationService.CurrentDate;

            for (int wp = 1; wp <= 6; wp++)
            {
                var wsBlocks = blocks.Where(b => b.DeployWorkplace == wp).ToList();
                if (!wsBlocks.Any())
                {
                    var emptyModel = new PlotModel { Title = $"작업장 {wp}" };
                    var emptySeries = new PieSeries
                    {
                        StrokeThickness = 0.5,
                        InsideLabelPosition = 0.5,
                        AngleSpan = 360,
                        StartAngle = 0
                    };
                    emptySeries.Slices.Add(new PieSlice("미사용", 1) { Fill = OxyColors.LightGray });
                    emptyModel.Series.Add(emptySeries);
                    PieModels.Add(emptyModel);
                    continue;
                }

                var wsStart = wsBlocks.Min(b => b.Start);
                var wsEnd = wsBlocks.Max(b => b.End);
                var end = currentDate < wsEnd ? currentDate : wsEnd;
                double totalDays = (end - wsStart).TotalDays;

                var model = new PlotModel { Title = $"작업장 {wp}" };
                var series = new PieSeries
                {
                    StrokeThickness = 0.5,
                    InsideLabelPosition = 0.5,
                    AngleSpan = 360,
                    StartAngle = 0
                };

                double usedSum = 0;
                foreach (var b in wsBlocks.OrderBy(b => b.Start))
                {
                    if (b.Start >= end) break;
                    var useStart = b.Start < wsStart ? wsStart : b.Start;
                    var useEnd = b.End < end ? b.End : end;
                    double days = Math.Max(0, (useEnd - useStart).TotalDays);
                    if (days <= 0) continue;
                    usedSum += days;

                    var color = GetColor(b.Name);

                    series.Slices.Add(new PieSlice(b.Name, days) { Fill = color });
                }

                var idle = Math.Max(0, totalDays - usedSum);
                if (idle > 0)
                {
                    series.Slices.Add(new PieSlice("미사용", idle)
                    {
                        Fill = OxyColors.LightGray
                    });
                }

                model.Series.Add(series);
                PieModels.Add(model);
            }
        }
    }
}
