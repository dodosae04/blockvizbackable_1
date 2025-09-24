using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BlockViz.Applications.Views;
using PlotModel = OxyPlot.PlotModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IGanttView))]
    [PartCreationPolicy(System.ComponentModel.Composition.CreationPolicy.NonShared)]
    public partial class GanttView : UserControl, IGanttView
    {
        private const int DefaultFilterKey = 0;

        private readonly Dictionary<int, ToggleButton> filterButtons;
        private bool suppressFilterNotification;
        private PlotModel model;
        private readonly ToolTip blockToolTip;
        private string currentTooltipText;

        public GanttView()
        {
            InitializeComponent();

            filterButtons = new Dictionary<int, ToggleButton>
            {
                { DefaultFilterKey, filterDefaultButton },
                { 1, filterWp1Button },
                { 2, filterWp2Button },
                { 3, filterWp3Button },
                { 4, filterWp4Button },
                { 5, filterWp5Button },
                { 6, filterWp6Button }
            };

            SetActiveButton(DefaultFilterKey);

            blockToolTip = new ToolTip
            {
                Placement = PlacementMode.Mouse,
                StaysOpen = false,
                IsHitTestVisible = false
            };
            if (plot != null)
            {
                blockToolTip.PlacementTarget = plot;
                ToolTipService.SetInitialShowDelay(plot, 0);
                ToolTipService.SetBetweenShowDelay(plot, 0);
                ToolTipService.SetShowDuration(plot, int.MaxValue);
                plot.MouseMove += Plot_MouseMove;
                plot.MouseLeave += Plot_MouseLeave;
                plot.MouseDown += Plot_MouseDown;
            }
        }

        public event EventHandler<WorkplaceFilterRequestedEventArgs> WorkplaceFilterRequested;

        public PlotModel GanttModel
        {
            get => model;
            set
            {
                model = value;
                if (plot != null)
                {
                    plot.Model = model;
                }
            }
        }

        private void Plot_MouseMove(object sender, MouseEventArgs e)
        {
            if (!TryShowTooltip(e.GetPosition(plot)))
            {
                HideTooltip();
            }
        }

        private void Plot_MouseLeave(object sender, MouseEventArgs e) => HideTooltip();

        private void Plot_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (!TryShowTooltip(e.GetPosition(plot)))
            {
                HideTooltip();
            }
        }

        private bool TryShowTooltip(Point position)
        {
            if (plot?.Model is not PlotModel plotModel)
            {
                return false;
            }

            var screenPoint = new ScreenPoint(position.X, position.Y);

            foreach (var series in plotModel.Series.OfType<IntervalBarSeries>())
            {
                var result = series.GetNearestPoint(screenPoint, true);
                if (result?.Item is IntervalBarItem item)
                {
                    var tooltipText = BuildTooltipText(item);
                    if (string.IsNullOrEmpty(tooltipText))
                    {
                        var fallback = FindBlockItem(series, item.CategoryIndex, result.DataPoint.X);
                        tooltipText = BuildTooltipText(fallback);
                    }
                    if (!string.IsNullOrEmpty(tooltipText))
                    {
                        ShowTooltip(tooltipText);
                        return true;
                    }
                }
            }

            return false;
        }

        public void SetActiveWorkplace(int? workplaceId)
        {
            int key = workplaceId.HasValue ? workplaceId.Value : DefaultFilterKey;
            if (!filterButtons.ContainsKey(key)) key = DefaultFilterKey;
            SetActiveButton(key);
        }

        private void SetActiveButton(int key)
        {
            suppressFilterNotification = true;
            try
            {
                foreach (var pair in filterButtons)
                    pair.Value.IsChecked = pair.Key == key;
            }
            finally { suppressFilterNotification = false; }
        }

        private void OnFilterButtonChecked(object sender, RoutedEventArgs e)
        {
            if (suppressFilterNotification) return;

            if (sender is ToggleButton toggle)
            {
                int key = ExtractFilterKey(toggle.Tag);
                if (!filterButtons.ContainsKey(key)) key = DefaultFilterKey;

                suppressFilterNotification = true;
                try
                {
                    foreach (var pair in filterButtons)
                        if (!ReferenceEquals(pair.Value, toggle))
                            pair.Value.IsChecked = false;
                }
                finally { suppressFilterNotification = false; }

                var workplaceId = key == DefaultFilterKey ? (int?)null : key;
                WorkplaceFilterRequested?.Invoke(this, new WorkplaceFilterRequestedEventArgs(workplaceId));
            }
        }

        private void OnFilterButtonUnchecked(object sender, RoutedEventArgs e)
        {
            if (suppressFilterNotification) return;
            if (sender is ToggleButton toggle)
            {
                suppressFilterNotification = true;
                try { toggle.IsChecked = true; }
                finally { suppressFilterNotification = false; }
            }
        }

        private static int ExtractFilterKey(object tag)
        {
            if (tag is int i) return i;
            if (tag is string s && int.TryParse(s, out int parsed)) return parsed;
            return DefaultFilterKey;
        }

        private static IntervalBarItem? FindBlockItem(IntervalBarSeries series, int categoryIndex, double positionX)
        {
            if (series == null) return null;

            for (int i = series.Items.Count - 1; i >= 0; i--)
            {
                var c = series.Items[i];
                if (c != null &&
                    c.CategoryIndex == categoryIndex &&
                    c.Start <= positionX && positionX <= c.End &&
                    !string.IsNullOrWhiteSpace(c.Title))
                {
                    return c;
                }
            }
            return null;
        }

        // IntervalBarItem.Tag 는 사용하지 않고 Title(표시명)과 축 값을 이용해 문자열을 구성한다.
        private static string BuildTooltipText(IntervalBarItem? item)
        {
            if (item == null) return string.Empty;

            var title = item.Title?.Trim();
            if (string.IsNullOrEmpty(title)) return string.Empty;

            var startText = FormatDate(item.Start);
            var endText = FormatDate(item.End);

            if (string.IsNullOrEmpty(startText) && string.IsNullOrEmpty(endText))
            {
                return title;
            }

            if (string.IsNullOrEmpty(startText))
            {
                return string.IsNullOrEmpty(endText)
                    ? title
                    : $"{title}{Environment.NewLine}종료: {endText}";
            }

            if (string.IsNullOrEmpty(endText))
            {
                return $"{title}{Environment.NewLine}시작: {startText}";
            }

            return $"{title}{Environment.NewLine}시작: {startText}{Environment.NewLine}종료: {endText}";
        }

        private static string FormatDate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return string.Empty;

            try
            {
                var date = DateTimeAxis.ToDateTime(value);
                if (date == DateTime.MinValue || date == DateTime.MaxValue)
                {
                    return string.Empty;
                }

                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void ShowTooltip(string text)
        {
            if (!string.Equals(currentTooltipText, text, StringComparison.Ordinal))
            {
                blockToolTip.Content = text;
                currentTooltipText = text;
            }
            if (!blockToolTip.IsOpen) blockToolTip.IsOpen = true;
        }

        private void HideTooltip()
        {
            if (blockToolTip.IsOpen) blockToolTip.IsOpen = false;
            currentTooltipText = string.Empty;
        }
    }
}
