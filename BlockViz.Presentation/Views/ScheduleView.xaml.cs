using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BlockViz.Applications.Extensions;
using BlockViz.Applications.Views;
using BlockViz.Domain.Models;
using HelixToolkit.Wpf;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IScheduleView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class ScheduleView : UserControl, IScheduleView
    {
        private ObservableCollection<Visual3D> visuals = new();
        private DateTime currentDate;
        private DateTime timelineStart;
        private DateTime timelineEnd;
        private bool timelineConfigured;
        private bool suppressTimelineEvent;
        private readonly ToolTip hoverToolTip;
        private string? currentTooltipContent;

        private static readonly Point3D InitPos = new(15, 20, 30);
        private static readonly Vector3D InitDir = new(-1, 0, -10);
        private static readonly Vector3D InitUp = new(-1, 1000, 1);
        private const double InitFov = 40.0;

        public ScheduleView()
        {
            InitializeComponent();
            hoverToolTip = new ToolTip
            {
                Placement = PlacementMode.Mouse,
                StaysOpen = false
            };

            if (viewport != null)
            {
                hoverToolTip.PlacementTarget = viewport;
                ToolTipService.SetInitialShowDelay(viewport, 0);
                ToolTipService.SetBetweenShowDelay(viewport, 0);
                ToolTipService.SetShowDuration(viewport, int.MaxValue);
                viewport.MouseMove += OnViewportMouseMove;
                viewport.MouseLeave += OnViewportMouseLeave;
            }
            Loaded += (_, __) => { ApplyVisuals(); UpdateDateText(); EnsureInitialCamera(); };
        }

        public ObservableCollection<Visual3D> Visuals
        {
            get => visuals;
            set
            {
                if (ReferenceEquals(visuals, value)) return;
                if (visuals != null) visuals.CollectionChanged -= Visuals_CollectionChanged;
                visuals = value ?? new ObservableCollection<Visual3D>();
                visuals.CollectionChanged += Visuals_CollectionChanged;
                ApplyVisuals();
            }
        }

        public DateTime CurrentDate
        {
            get => currentDate;
            set
            {
                currentDate = value;
                UpdateDateText();
                UpdateTimelineSlider();
            }
        }

        public event Action<Block>? BlockClicked;
        public event EventHandler<double>? TimelineValueChanged;

        public void ConfigureTimeline(DateTime start, DateTime end)
        {
            timelineStart = start;
            timelineEnd = end < start ? start : end;
            timelineConfigured = true;

            if (startDateText != null)
                startDateText.Text = timelineConfigured ? timelineStart.ToString("yyyy-MM-dd") : "-";
            if (endDateText != null)
                endDateText.Text = timelineConfigured ? timelineEnd.ToString("yyyy-MM-dd") : "-";

            if (timelineSlider != null)
            {
                var totalDays = Math.Max(0.0, (timelineEnd - timelineStart).TotalDays);
                timelineSlider.Minimum = 0;
                timelineSlider.Maximum = totalDays;
                timelineSlider.IsEnabled = timelineConfigured && totalDays > 0;
            }

            UpdateTimelineSlider();
        }

        private void UpdateDateText()
        {
            if (dateText != null && currentDate != default)
                dateText.Text = currentDate.ToString("yyyy-MM-dd");
        }

        private void UpdateTimelineSlider()
        {
            if (!timelineConfigured || timelineSlider == null) return;

            var clamped = currentDate;
            if (clamped < timelineStart) clamped = timelineStart;
            if (clamped > timelineEnd) clamped = timelineEnd;

            var value = (clamped - timelineStart).TotalDays;
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
            if (value < timelineSlider.Minimum) value = timelineSlider.Minimum;
            if (value > timelineSlider.Maximum) value = timelineSlider.Maximum;

            suppressTimelineEvent = true;
            timelineSlider.Value = value;
            suppressTimelineEvent = false;
        }

        private void Visuals_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => ApplyVisuals();

        private void ApplyVisuals()
        {
            if (SceneRoot == null) return;
            SceneRoot.Children.Clear();
            foreach (var v in visuals)
                if (v != null) SceneRoot.Children.Add(v);

            try
            {
                EnsureInitialCamera();
                viewport?.ZoomExtents();
            }
            catch
            {
                // 디자인/런타임 예외 무시
            }
        }

        private void EnsureInitialCamera()
        {
            if (viewport?.Camera is PerspectiveCamera cam)
            {
                cam.Position = InitPos;
                cam.LookDirection = InitDir;
                cam.UpDirection = InitUp;
                cam.FieldOfView = InitFov;
            }
        }

        private void OnViewportMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (viewport == null) return;
            var block = HitTestBlock(e.GetPosition(viewport));

            if (block != null)
            {
                BlockClicked?.Invoke(block);
                e.Handled = true;
            }
        }

        private void OnViewportMouseMove(object sender, MouseEventArgs e)
        {
            if (viewport == null) return;
            var block = HitTestBlock(e.GetPosition(viewport));
            UpdateTooltip(block);
        }

        private void OnViewportMouseLeave(object sender, MouseEventArgs e)
        {
            UpdateTooltip(null);
        }

        private void UpdateTooltip(Block? block)
        {
            if (block == null)
            {
                currentTooltipContent = null;
                hoverToolTip.IsOpen = false;
                return;
            }

            var content = block.GetDisplayName();

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

        private Block? HitTestBlock(Point pt)
        {
            if (viewport == null) return null;

            Block? foundBlock = null;

            HitTestResultCallback cb = hit =>
            {
                if (hit is RayHitTestResult r)
                {
                    DependencyObject? d = r.VisualHit as DependencyObject;
                    while (d != null)
                    {
                        var blk = TryGetBlockFromAttachedProperty(d);
                        if (blk != null)
                        {
                            foundBlock = blk;
                            return HitTestResultBehavior.Stop;
                        }

                        var tag = d.GetValue(FrameworkElement.TagProperty);
                        if (tag is Block tb)
                        {
                            foundBlock = tb;
                            return HitTestResultBehavior.Stop;
                        }

                        d = VisualTreeHelper.GetParent(d);
                    }
                }

                return HitTestResultBehavior.Continue;
            };

            VisualTreeHelper.HitTest(viewport, null, cb, new PointHitTestParameters(pt));
            return foundBlock;
        }

        private static Block? TryGetBlockFromAttachedProperty(DependencyObject d)
        {
            try
            {
                var type = Type.GetType("BlockViz.Applications.Models.BlockProperties, BlockViz.Applications");
                var getMethod = type?.GetMethod("GetData", BindingFlags.Public | BindingFlags.Static);
                var obj = getMethod?.Invoke(null, new object[] { d });
                return obj as Block;
            }
            catch { return null; }
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!timelineConfigured || suppressTimelineEvent) return;
            TimelineValueChanged?.Invoke(this, e.NewValue);
        }
    }
}
