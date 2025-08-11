
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Waf.Applications;
using BlockViz.Applications;


namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class GanttViewModel : ViewModel<IGanttView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;
        private readonly DateTimeAxis dateAxis;
        private readonly Dictionary<string, OxyColor> colorMap = new();
        private readonly OxyColor[] palette = new[]
        {
            OxyColors.Red, OxyColors.Orange, OxyColors.Yellow,
            OxyColors.LimeGreen, OxyColors.SkyBlue, OxyColors.Purple,
            OxyColors.Brown, OxyColors.Teal, OxyColors.Pink
        };

        public event PropertyChangedEventHandler PropertyChanged;
        public PlotModel GanttModel { get; }

        [ImportingConstructor]
        public GanttViewModel(IGanttView view,
                              IScheduleService scheduleService,
                              SimulationService simulationService) : base(view)
        {
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;

            GanttModel = new PlotModel { Title = "공장 가동 스케줄" };
            dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "기간",
                LabelFormatter = d => DateTimeAxis.ToDateTime(d).ToString("yyyy-MM-dd")
            };
            GanttModel.Axes.Add(dateAxis);

            var catAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "작업장",
                GapWidth = 0.2
            };
            for (int i = 1; i <= 6; i++) catAxis.Labels.Add($"작업장 {i}");
            GanttModel.Axes.Add(catAxis);

            simulationService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(simulationService.CurrentDate))
                    UpdateGantt();
            };
            UpdateGantt();

            view.GanttModel = GanttModel;
        }

        private OxyColor GetColor(string name)
        {
            if (!colorMap.ContainsKey(name))
                colorMap[name] = palette[colorMap.Count % palette.Length];
            return colorMap[name];
        }

        private void UpdateGantt()
        {
            GanttModel.Series.Clear();

            var series = new IntervalBarSeries
            {
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                LabelFormatString = null
            };

            var blocks = scheduleService.GetAllBlocks().ToList();
            if (!blocks.Any())
            {
                GanttModel.InvalidatePlot(true);
                return;
            }

            var overallStart = blocks.Min(b => b.Start);
            var overallEnd = blocks.Max(b => b.End);

            var simDate = simulationService.CurrentDate;
            var currentDate = simDate > overallEnd ? overallEnd : simDate;

            dateAxis.Minimum = DateTimeAxis.ToDouble(overallStart);
            dateAxis.Maximum = DateTimeAxis.ToDouble(overallEnd);

            foreach (var grp in blocks.GroupBy(b => b.DeployWorkplace))
            {
                int wp = grp.Key;
                int catIndex = wp - 1;
                DateTime cursor = overallStart;

                foreach (var b in grp.OrderBy(b => b.Start))
                {
                    var gapEnd = b.Start < currentDate ? b.Start : currentDate;
                    if (gapEnd > cursor)
                    {
                        series.Items.Add(new IntervalBarItem
                        {
                            Start = DateTimeAxis.ToDouble(cursor),
                            End = DateTimeAxis.ToDouble(gapEnd),
                            CategoryIndex = catIndex,
                            Color = OxyColors.LightGray
                        });
                    }

                    if (b.Start < currentDate)
                    {
                        var useStart = b.Start;
                        var useEnd = b.End < currentDate ? b.End : currentDate;
                        if (useEnd > useStart)
                        {
                            var color = GetColor(b.Name);
                            series.Items.Add(new IntervalBarItem
                            {
                                Start = DateTimeAxis.ToDouble(useStart),
                                End = DateTimeAxis.ToDouble(useEnd),
                                CategoryIndex = catIndex,
                                Color = color
                            });
                        }
                        cursor = useEnd;
                    }

                    if (b.Start > currentDate) break;
                }
                if (cursor < currentDate)
                {
                    series.Items.Add(new IntervalBarItem
                    {
                        Start = DateTimeAxis.ToDouble(cursor),
                        End = DateTimeAxis.ToDouble(currentDate),
                        CategoryIndex = catIndex,
                        Color = OxyColors.LightGray
                    });
                }
            }

            GanttModel.Series.Add(series);
            GanttModel.InvalidatePlot(true);
        }
    }
}
