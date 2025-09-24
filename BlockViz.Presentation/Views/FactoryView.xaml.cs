using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BlockViz.Applications.Views;
using BlockViz.Domain.Models;
using HelixToolkit.Wpf;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IFactoryView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class FactoryView : UserControl, IFactoryView
    {
        private ObservableCollection<Visual3D> visuals = new();
        private DateTime currentDate;
        private DateTime timelineStart;
        private DateTime timelineEnd;
        private bool timelineConfigured;
        private bool suppressTimelineEvent;

        // 옛날 카메라와 동일한 초기값(필요 시 ZoomExtents 후에도 자세 유지)
        private static readonly Point3D InitPos = new(15, 20, 30);
        private static readonly Vector3D InitDir = new(-1, 0, -10);
        private static readonly Vector3D InitUp = new(-1, 1000, 1);
        private const double InitFov = 40.0;

        public FactoryView()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                ApplyVisuals();
                UpdateDateText();
                EnsureInitialCamera();
            };
        }

        // ===== IFactoryView 구현 =====
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
        // ============================

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
                // 초기 카메라 자세 유지한 채로 화면만 맞춤
                EnsureInitialCamera();
                viewport?.ZoomExtents();
            }
            catch { /* 디자이너/런타임 보호 */ }
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
            var pt = e.GetPosition(viewport);

            Block? foundBlock = null;

            HitTestResultCallback cb = (hit) =>
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

                        var tag = d.GetValue(System.Windows.FrameworkElement.TagProperty);
                        if (tag is Block tagBlock)
                        {
                            foundBlock = tagBlock;
                            return HitTestResultBehavior.Stop;
                        }

                        d = VisualTreeHelper.GetParent(d);
                    }
                }
                return HitTestResultBehavior.Continue;
            };

            VisualTreeHelper.HitTest(viewport, null, cb, new PointHitTestParameters(pt));

            if (foundBlock != null)
            {
                BlockClicked?.Invoke(foundBlock);
                e.Handled = true;
            }
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!timelineConfigured || suppressTimelineEvent) return;
            TimelineValueChanged?.Invoke(this, e.NewValue);
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
    }
}
