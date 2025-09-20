using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OxyPlot.Wpf;
using BlockViz.Applications.Views;
using BlockViz.Presentation.Utils;

namespace BlockViz.Presentation.Views
{
    [Export, Export(typeof(IRibbonView))]
    public partial class RibbonView : UserControl, IRibbonView
    {
        public RibbonView()
        {
            InitializeComponent();
        }

        // ---- 간트 PNG 저장 ----
        private void ExportGantt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "PNG 이미지 (*.png)|*.png",
                    FileName = $"BlockViz_{DateTime.Now:yyyyMMdd_HHmm}_Gantt.png"
                };
                if (dlg.ShowDialog() != true) return;

                var root = Window.GetWindow(this);
                if (root == null) throw new InvalidOperationException("주 창을 찾을 수 없습니다.");

                // 우선 이름이 'plot'인 PlotView를 찾고, 없으면 가장 큰 PlotView로 대체
                var ganttPlot = ExportHelper.FindChildByName<PlotView>(root, "plot");
                if (ganttPlot == null)
                    ganttPlot = FindLargestPlotView(root);

                if (ganttPlot == null)
                    throw new InvalidOperationException("간트 차트(PlotView)를 찾을 수 없습니다.");

                ExportHelper.ExportElementToPng(ganttPlot, dlg.FileName, dpi: 192);
                MessageBox.Show("간트 차트를 저장했습니다.\n" + dlg.FileName, "내보내기",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("간트 내보내기 실패: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- 파이 PNG 저장 (스크롤 포함 전체) ----
        private void ExportPie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "PNG 이미지 (*.png)|*.png",
                    FileName = $"BlockViz_{DateTime.Now:yyyyMMdd_HHmm}_Pie.png"
                };
                if (dlg.ShowDialog() != true) return;

                var root = Window.GetWindow(this);
                if (root == null) throw new InvalidOperationException("주 창을 찾을 수 없습니다.");

                // PiView 안의 첫 번째 ScrollViewer를 찾아 전체 콘텐츠로 저장
                var piView = ExportHelper.FindChild<BlockViz.Presentation.Views.PiView>(root);
                if (piView == null) throw new InvalidOperationException("PiView를 찾을 수 없습니다.");

                var sc = ExportHelper.FindChild<ScrollViewer>(piView);
                if (sc != null)
                {
                    ExportHelper.ExportScrollViewerContentToPng(sc, dlg.FileName, dpi: 192);
                }
                else
                {
                    // 스크롤이 없다면 전체를 캡처
                    ExportHelper.ExportElementToPng(piView, dlg.FileName, dpi: 192);
                }

                MessageBox.Show("파이 차트를 저장했습니다.\n" + dlg.FileName, "내보내기",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파이 내보내기 실패: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 화면에서 가장 넓은 PlotView 찾기 (Fallback)
        private static PlotView? FindLargestPlotView(DependencyObject start)
        {
            PlotView? found = null;
            double maxW = 0;
            void Walk(DependencyObject node)
            {
                int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
                    if (child is PlotView pv && pv.ActualWidth > maxW)
                    {
                        maxW = pv.ActualWidth;
                        found = pv;
                    }
                    Walk(child);
                }
            }
            Walk(start);
            return found;
        }
    }
}
