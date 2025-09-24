using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BlockViz.Applications.Models;
using BlockViz.Applications.Views;
using OxyPlot;
using OxyPlot.Series;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IPiView))]
    [PartCreationPolicy(System.ComponentModel.Composition.CreationPolicy.NonShared)]
    public partial class PiView : UserControl, IPiView
    {
        private ObservableCollection<PlotModel> models = new ObservableCollection<PlotModel>();
        private PieSlice? activeSlice;
        private PlotModel? activeModel;
        private Brush? defaultOverlayBrush;
        private Thickness defaultOverlayThickness;

        public PiView()
        {
            InitializeComponent();
            Focusable = true;

            models.CollectionChanged += OnPieModelsChanged;
            if (cards != null) cards.ItemsSource = models;

            defaultOverlayBrush = nameOverlayPanel?.BorderBrush;
            defaultOverlayThickness = nameOverlayPanel?.BorderThickness ?? new Thickness(1.2);

            PreviewKeyDown += OnPreviewKeyDown;
        }

        public ObservableCollection<PlotModel> PieModels
        {
            get => models;
            set
            {
                if (ReferenceEquals(models, value)) return;

                if (models != null)
                    models.CollectionChanged -= OnPieModelsChanged;

                models = value ?? new ObservableCollection<PlotModel>();
                models.CollectionChanged += OnPieModelsChanged;

                if (cards != null) cards.ItemsSource = models;
                HideOverlay();
            }
        }

        private void OnPieModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => HideOverlay();

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsOverlayVisible())
            {
                HideOverlay();
                e.Handled = true;
            }
        }

        private void OnPieMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not OxyPlot.Wpf.PlotView plotView || plotView.ActualModel is not PlotModel model)
            {
                HideOverlay();
                return;
            }

            var pos = e.GetPosition(plotView);
            var screenPoint = new ScreenPoint(pos.X, pos.Y);

            foreach (var series in model.Series.OfType<PieSeries>())
            {
                var result = series.GetNearestPoint(screenPoint, true);
                if (result?.Item is PieSlice slice)
                {
                    if (ReferenceEquals(activeSlice, slice) && IsOverlayVisible())
                    {
                        HideOverlay();
                    }
                    else
                    {
                        ShowOverlay(model, slice);
                    }
                    e.Handled = true;
                    return;
                }
            }

            if (IsOverlayVisible())
            {
                HideOverlay();
            }
        }

        private void OnOverlayBackgroundMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsOverlayVisible()) return;

            if (nameOverlayPanel != null && e.OriginalSource is DependencyObject d && IsDescendantOf(d, nameOverlayPanel))
            {
                return;
            }

            HideOverlay();
            e.Handled = true;
        }

        private void ShowOverlay(PlotModel model, PieSlice slice)
        {
            activeModel = model;
            activeSlice = slice;

            string chartTitle = string.IsNullOrWhiteSpace(model?.Title) ? "파이 차트" : model.Title;

            if (slice is BlockPieSlice blockSlice)
            {
                var names = blockSlice.BlockNames ?? Array.Empty<string>();

                if (names.Count > 0)
                {
                    var builder = new StringBuilder();
                    for (int i = 0; i < names.Count; i++)
                    {
                        builder.Append(i + 1).Append(". ").Append(names[i]).AppendLine();
                    }
                    nameOverlayText.Text = builder.ToString().TrimEnd();
                    nameOverlayTitle.Text = $"{chartTitle} — 블록 {names.Count}개";
                    nameOverlayHint.Text = "Ctrl+C로 복사, ESC 또는 배경 클릭으로 닫기";
                }
                else
                {
                    nameOverlayText.Text = "표시할 블록 이름이 없습니다.";
                    nameOverlayTitle.Text = $"{chartTitle} — 블록 없음";
                    nameOverlayHint.Text = "ESC 또는 배경을 클릭하면 닫힙니다.";
                }

                UpdateOverlayBorder(slice.Fill);
            }
            else
            {
                nameOverlayText.Text = "해당 구간에는 연결된 블록이 없습니다.";
                nameOverlayTitle.Text = $"{chartTitle} — 미사용";
                nameOverlayHint.Text = "ESC 또는 배경을 클릭하면 닫힙니다.";
                ResetOverlayBorder();
            }

            if (nameOverlay != null)
                nameOverlay.Visibility = Visibility.Visible;

            nameOverlayText.Focus();
            nameOverlayText.CaretIndex = 0;
            nameOverlayText.ScrollToHome();
        }

        private void HideOverlay()
        {
            if (nameOverlay != null)
                nameOverlay.Visibility = Visibility.Collapsed;

            nameOverlayText.Text = string.Empty;
            ResetOverlayBorder();
            activeSlice = null;
            activeModel = null;
        }

        private void UpdateOverlayBorder(OxyColor color)
        {
            if (nameOverlayPanel == null) return;

            if (!color.IsUndefined)
            {
                var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
                brush.Freeze();
                nameOverlayPanel.BorderBrush = brush;
                nameOverlayPanel.BorderThickness = new Thickness(2);
            }
            else
            {
                ResetOverlayBorder();
            }
        }

        private void ResetOverlayBorder()
        {
            if (nameOverlayPanel == null) return;
            nameOverlayPanel.BorderBrush = defaultOverlayBrush;
            nameOverlayPanel.BorderThickness = defaultOverlayThickness;
        }

        private bool IsOverlayVisible()
            => nameOverlay != null && nameOverlay.Visibility == Visibility.Visible;

        private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
        {
            var current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor)) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
