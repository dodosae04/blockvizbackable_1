using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BlockViz.Applications.Views;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IPiView))]
    [PartCreationPolicy(System.ComponentModel.Composition.CreationPolicy.NonShared)]
    public partial class PiView : UserControl, IPiView
    {
        private const double AngleTolerance = 1e-3;

        private readonly HashSet<PlotView> attachedPlots = new();
        private readonly ToolTip hoverToolTip;

        private ObservableCollection<PlotModel> models = new();
        private string? currentTooltipContent;
        private PlotView? tooltipOwner;

        public PiView()
        {
            InitializeComponent();
            if (cards != null) cards.ItemsSource = models;

            hoverToolTip = new ToolTip
            {
                Placement = PlacementMode.Mouse,
                StaysOpen = false
            };
        }

        public ObservableCollection<PlotModel> PieModels
        {
            get => models;
            set
            {
                models = value ?? new ObservableCollection<PlotModel>();
                if (cards != null) cards.ItemsSource = models;
            }
        }

        private void OnPiePlotLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is PlotView plotView && attachedPlots.Add(plotView))
            {
                ToolTipService.SetInitialShowDelay(plotView, 0);
                ToolTipService.SetBetweenShowDelay(plotView, 0);
                ToolTipService.SetShowDuration(plotView, int.MaxValue);
                plotView.MouseMove += OnPiePlotMouseMove;
                plotView.MouseLeave += OnPiePlotMouseLeave;
            }
        }

        private void OnPiePlotUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is PlotView plotView && attachedPlots.Remove(plotView))
            {
                plotView.MouseMove -= OnPiePlotMouseMove;
                plotView.MouseLeave -= OnPiePlotMouseLeave;
                if (ReferenceEquals(tooltipOwner, plotView))
                {
                    tooltipOwner = null;
                    UpdateTooltip(null);
                }
            }
        }

        private void OnPiePlotMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not PlotView plotView)
            {
                return;
            }

            var blockName = FindSliceName(plotView, e.GetPosition(plotView));
            if (string.IsNullOrWhiteSpace(blockName))
            {
                if (ReferenceEquals(tooltipOwner, plotView))
                {
                    tooltipOwner = null;
                }
                UpdateTooltip(null);
                return;
            }

            if (!ReferenceEquals(tooltipOwner, plotView))
            {
                tooltipOwner = plotView;
                hoverToolTip.PlacementTarget = plotView;
            }

            UpdateTooltip(blockName);
        }

        private void OnPiePlotMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is PlotView plotView && ReferenceEquals(tooltipOwner, plotView))
            {
                tooltipOwner = null;
            }

            UpdateTooltip(null);
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

            if (!hoverToolTip.IsOpen)
            {
                hoverToolTip.IsOpen = true;
            }
        }

        private string? FindSliceName(PlotView plotView, Point position)
        {
            if (plotView.Model is not PlotModel model)
            {
                return null;
            }

            var pieSeries = model.Series.OfType<PieSeries>().FirstOrDefault();
            if (pieSeries == null)
            {
                return null;
            }

            var plotArea = model.PlotArea;
            if (plotArea.Width <= 0 || plotArea.Height <= 0)
            {
                return null;
            }

            if (position.X < plotArea.Left || position.X > plotArea.Right ||
                position.Y < plotArea.Top || position.Y > plotArea.Bottom)
            {
                return null;
            }

            double centerX = plotArea.Left + plotArea.Width / 2.0;
            double centerY = plotArea.Top + plotArea.Height / 2.0;
            double dx = position.X - centerX;
            double dy = position.Y - centerY;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            double outerRadius = Math.Min(plotArea.Width, plotArea.Height) / 2.0;

            if (radius <= 0 || radius > outerRadius + 0.5)
            {
                return null;
            }

            double innerRadius = 0;
            if (pieSeries.InnerDiameter > 0 && pieSeries.Diameter > 0)
            {
                innerRadius = outerRadius * (pieSeries.InnerDiameter / pieSeries.Diameter);
            }

            if (innerRadius > 0 && radius < innerRadius - 0.5)
            {
                return null;
            }

            double totalValue = pieSeries.Slices.Sum(s => Math.Max(0.0, s.Value));
            if (totalValue <= 0)
            {
                return null;
            }

            double angleFromStart = NormalizeAngle(Math.Atan2(-dy, dx) * 180.0 / Math.PI - pieSeries.StartAngle);
            double totalSpan = Math.Abs(pieSeries.AngleSpan);
            if (totalSpan <= 0)
            {
                return null;
            }

            double progress = pieSeries.AngleSpan >= 0
                ? angleFromStart
                : (360.0 - angleFromStart) % 360.0;

            if (progress > totalSpan + AngleTolerance)
            {
                return null;
            }

            double accumulated = 0.0;
            foreach (var slice in pieSeries.Slices)
            {
                double sliceValue = Math.Max(0.0, slice.Value);
                double sweep = totalSpan * (sliceValue / totalValue);
                double end = accumulated + sweep;

                if (progress + AngleTolerance >= accumulated && progress <= end + AngleTolerance)
                {
                    if (slice.Tag is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }

                    return null;
                }

                accumulated = end;
            }

            return null;
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 360.0;
            if (angle < 0) angle += 360.0;
            return angle;
        }
    }
}

