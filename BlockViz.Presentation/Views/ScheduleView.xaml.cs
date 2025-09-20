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
    [Export(typeof(IScheduleView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class ScheduleView : UserControl, IScheduleView
    {
        private ObservableCollection<Visual3D> visuals = new();
        private DateTime currentDate;

        // FactoryView와 동일한 초기 카메라 값
        private static readonly Point3D InitPos = new(15, 20, 30);
        private static readonly Vector3D InitDir = new(-1, 0, -10);
        private static readonly Vector3D InitUp = new(-1, 1000, 1);
        private const double InitFov = 40.0;

        public ScheduleView()
        {
            InitializeComponent();
            Loaded += (_, __) => { ApplyVisuals(); UpdateDateText(); EnsureInitialCamera(); };
        }

        // IScheduleView 구현 ----------------------------
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
            set { currentDate = value; UpdateDateText(); }
        }

        public event Action<Block>? BlockClicked;
        // ------------------------------------------------

        private void UpdateDateText()
        {
            if (dateText != null && currentDate != default)
                dateText.Text = currentDate.ToString("yyyy-MM-dd");
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
                // 동일 구도를 유지한 채로 화면만 맞춤
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
                        // Attached DP로 블록 찾기
                        var blk = TryGetBlockFromAttachedProperty(d);
                        if (blk != null) { foundBlock = blk; return HitTestResultBehavior.Stop; }

                        // (옵션) Tag에 붙인 경우도 지원
                        var tag = d.GetValue(FrameworkElement.TagProperty);
                        if (tag is Block tb) { foundBlock = tb; return HitTestResultBehavior.Stop; }

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

        private static Block? TryGetBlockFromAttachedProperty(DependencyObject d)
        {
            try
            {
                // BlockViz.Applications.Models.BlockProperties.GetData(DependencyObject)
                var type = Type.GetType("BlockViz.Applications.Models.BlockProperties, BlockViz.Applications");
                var getMethod = type?.GetMethod("GetData", BindingFlags.Public | BindingFlags.Static);
                var obj = getMethod?.Invoke(null, new object[] { d });
                return obj as Block;
            }
            catch { return null; }
        }
    }
}
