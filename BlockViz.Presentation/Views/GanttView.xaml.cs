using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BlockViz.Applications.Extensions;
using BlockViz.Applications.Models;
using BlockViz.Applications.Views;
using BlockViz.Domain.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IGanttView))]
    [PartCreationPolicy(System.ComponentModel.Composition.CreationPolicy.NonShared)]
    public partial class GanttView : UserControl, IGanttView
    {
        private const int DefaultFilterKey = 0;

        private readonly Dictionary<int, ToggleButton> filterButtons;
        private readonly ToolTip hoverToolTip;
        private string? currentTooltipContent;
        private bool suppressFilterNotification;
        private bool suppressExpandNotification;
        private PlotModel model;

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
            SetExpandAllState(false);

            hoverToolTip = new ToolTip { Placement = PlacementMode.Mouse, StaysOpen = false };

            if (plot != null)
            {
                hoverToolTip.PlacementTarget = plot;
                ToolTipService.SetInitialShowDelay(plot, 0);
                ToolTipService.SetBetweenShowDelay(plot, 0);
                ToolTipService.SetShowDuration(plot, int.MaxValue);
                plot.MouseMove += OnPlotMouseMove;
                plot.MouseLeave += OnPlotMouseLeave;
                plot.MouseDown += OnPlotMouseDown;
            }
        }

        public event EventHandler<WorkplaceFilterRequestedEventArgs> WorkplaceFilterRequested;

        public event EventHandler<bool> ExpandAllChanged;

        public event Action<Block> BlockClicked;

        public PlotModel GanttModel
        {
            get => model;
            set
            {
                model = value;
                if (plot != null) plot.Model = model;
            }
        }

        public void SetActiveWorkplace(int? workplaceId)
        {
            int key = workplaceId.HasValue ? workplaceId.Value : DefaultFilterKey;
            if (!filterButtons.ContainsKey(key)) key = DefaultFilterKey;
            SetActiveButton(key);
        }

        public void SetExpandAllState(bool isExpanded)
        {
            suppressExpandNotification = true;
            try
            {
                if (expandAllButton != null)
                {
                    expandAllButton.IsChecked = isExpanded;
                }
            }
            finally
            {
                suppressExpandNotification = false;
            }
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
                        if (!ReferenceEquals(pair.Value, toggle)) pair.Value.IsChecked = false;
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

        private void OnExpandAllChecked(object sender, RoutedEventArgs e)
        {
            if (suppressExpandNotification) return;
            ExpandAllChanged?.Invoke(this, true);
        }

        private void OnExpandAllUnchecked(object sender, RoutedEventArgs e)
        {
            if (suppressExpandNotification) return;
            ExpandAllChanged?.Invoke(this, false);
        }

        private void OnPlotMouseMove(object sender, MouseEventArgs e)
        {
            var item = FindBlockItem(e.GetPosition(plot));
            var displayName = item?.Block.GetDisplayName();
            UpdateTooltip(displayName);
        }

        private void OnPlotMouseLeave(object sender, MouseEventArgs e) => UpdateTooltip(null);

        private void OnPlotMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            var item = FindBlockItem(e.GetPosition(plot));
            if (item?.Block != null)
            {
                BlockClicked?.Invoke(item.Block);
                e.Handled = true;
            }
        }

        private void UpdateTooltip(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                currentTooltipContent = null;
                hoverToolTip.IsOpen = false;
                return;
            }

            if (!string.Equals(currentTooltipContent, content, StringComparison.Ordinal))
            {
                hoverToolTip.Content = content;
                currentTooltipContent = content;
            }
            if (!hoverToolTip.IsOpen) hoverToolTip.IsOpen = true;
        }

        private BlockIntervalBarItem? FindBlockItem(Point position)
        {
            if (plot?.Model is not PlotModel model) return null;

            var barSeries = model.Series.OfType<IntervalBarSeries>().FirstOrDefault();
            if (barSeries == null) return null;

            var dateAxis = model.Axes.OfType<DateTimeAxis>().FirstOrDefault();
            var categoryAxis = model.Axes.OfType<CategoryAxis>().FirstOrDefault();
            if (dateAxis == null || categoryAxis == null) return null;

            var plotArea = model.PlotArea;
            if (position.X < plotArea.Left || position.X > plotArea.Right ||
                position.Y < plotArea.Top || position.Y > plotArea.Bottom) return null;

            var sp = new ScreenPoint(position.X, position.Y);

            foreach (var item in barSeries.Items.OfType<BlockIntervalBarItem>())
            {
                double x0 = dateAxis.Transform(item.Start);
                double x1 = dateAxis.Transform(item.End);
                double half = barSeries.BarWidth / 2.0;
                double y0 = categoryAxis.Transform(item.CategoryIndex - half);
                double y1 = categoryAxis.Transform(item.CategoryIndex + half);

                double minX = Math.Min(x0, x1), maxX = Math.Max(x0, x1);
                double minY = Math.Min(y0, y1), maxY = Math.Max(y0, y1);

                if (sp.X >= minX && sp.X <= maxX && sp.Y >= minY && sp.Y <= maxY)
                    return item;
            }
            return null;
        }

        private static int ExtractFilterKey(object tag)
        {
            if (tag is int i) return i;
            if (tag is string s && int.TryParse(s, out int p)) return p;
            return DefaultFilterKey;
        }
    }
}
