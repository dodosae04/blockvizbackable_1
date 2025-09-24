using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BlockViz.Applications.Views;
using PlotModel = OxyPlot.PlotModel;
using BlockViz.Applications.Extensions;
using OxyPlot;
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
            if (plot?.Model is not PlotModel plotModel)
            {
                HideTooltip();
            }
            else
            {
                var position = e.GetPosition(plot);
                var screenPoint = new ScreenPoint(position.X, position.Y);

                foreach (var series in plotModel.Series.OfType<IntervalBarSeries>())
                {
                    var result = series.GetNearestPoint(screenPoint, true);
                    if (result?.Item is IntervalBarItem item)
                    {
                        var tooltipText = ExtractTooltipText(item);
                        if (string.IsNullOrEmpty(tooltipText))
                        {
                            tooltipText = FindBlockTooltip(series, item.CategoryIndex, result.DataPoint.X);
                        }
                        if (!string.IsNullOrEmpty(tooltipText))
                        {
                            ShowTooltip(tooltipText);
                            return;
                        }
                    }
                }
                HideTooltip();
            }
        }

        private void Plot_MouseLeave(object sender, MouseEventArgs e) => HideTooltip();

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

        // ★ IntervalBarItem.Tag 를 전혀 사용하지 않음 ? Title(표시명)만 사용
        private static string ExtractTooltipText(IntervalBarItem item)
            => string.IsNullOrWhiteSpace(item?.Title) ? string.Empty : item.Title;

        private static string FindBlockTooltip(IntervalBarSeries series, int categoryIndex, double positionX)
        {
            if (series == null) return string.Empty;

            for (int i = series.Items.Count - 1; i >= 0; i--)
            {
                var c = series.Items[i];
                if (c != null &&
                    c.CategoryIndex == categoryIndex &&
                    c.Start <= positionX && positionX <= c.End)
                {
                    return string.IsNullOrWhiteSpace(c.Title) ? string.Empty : c.Title;
                }
            }
            return string.Empty;
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
