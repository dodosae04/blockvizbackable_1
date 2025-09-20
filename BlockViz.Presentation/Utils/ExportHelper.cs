using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlockViz.Presentation.Utils
{
    public static class ExportHelper
    {
        /// <summary>
        /// 임의의 FrameworkElement를 PNG로 저장 (화면에 보이는 크기 그대로)
        /// </summary>
        public static void ExportElementToPng(FrameworkElement element, string filePath, double dpi = 192)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            element.UpdateLayout();
            double w = element.ActualWidth;
            double h = element.ActualHeight;

            if (w <= 0 || h <= 0)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                element.Arrange(new Rect(element.DesiredSize));
                element.UpdateLayout();
                w = element.ActualWidth;
                h = element.ActualHeight;
            }
            if (w <= 0 || h <= 0) throw new InvalidOperationException("내보낼 대상의 크기가 0입니다.");

            var rtb = new RenderTargetBitmap(
                (int)Math.Round(w * dpi / 96.0),
                (int)Math.Round(h * dpi / 96.0),
                dpi, dpi, PixelFormats.Pbgra32);

            rtb.Render(element);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }

        /// <summary>
        /// ScrollViewer의 전체 콘텐츠(보이지 않는 영역 포함)를 PNG로 저장
        /// </summary>
        public static void ExportScrollViewerContentToPng(System.Windows.Controls.ScrollViewer sc, string filePath, double dpi = 192)
        {
            if (sc == null) throw new ArgumentNullException(nameof(sc));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            if (sc.Content is not FrameworkElement content)
            {
                // 콘텐츠 타입을 모르면 ScrollViewer 자체를 캡처
                ExportElementToPng(sc, filePath, dpi);
                return;
            }

            sc.UpdateLayout();
            content.UpdateLayout();

            // 전체 스크롤 영역 크기(Extent) — 잘리는 문제 해결 포인트
            double w = Math.Max(sc.ExtentWidth, content.ActualWidth);
            double h = Math.Max(sc.ExtentHeight, content.ActualHeight);
            if (w <= 0 || h <= 0) throw new InvalidOperationException("스크롤 콘텐츠 크기를 알 수 없습니다.");

            // 원래 상태 백업
            bool wWasNaN = double.IsNaN(content.Width);
            bool hWasNaN = double.IsNaN(content.Height);
            double oldW = content.Width;
            double oldH = content.Height;
            bool oldClip = content.ClipToBounds;

            try
            {
                // 콘텐츠를 전체 크기로 임시 확장하여 배치
                content.Width = w;
                content.Height = h;
                content.ClipToBounds = false;

                content.Measure(new Size(w, h));
                content.Arrange(new Rect(0, 0, w, h));
                content.UpdateLayout();

                var rtb = new RenderTargetBitmap(
                    (int)Math.Round(w * dpi / 96.0),
                    (int)Math.Round(h * dpi / 96.0),
                    dpi, dpi, PixelFormats.Pbgra32);

                rtb.Render(content);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                encoder.Save(fs);
            }
            finally
            {
                // 원상복구
                if (wWasNaN) content.ClearValue(FrameworkElement.WidthProperty); else content.Width = oldW;
                if (hWasNaN) content.ClearValue(FrameworkElement.HeightProperty); else content.Height = oldH;
                content.ClipToBounds = oldClip;
                sc.UpdateLayout();
            }
        }

        /// <summary>시각 트리에서 첫 번째 T 자식 찾기 (DFS)</summary>
        public static T? FindChild<T>(DependencyObject start) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(start);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                if (child is T t) return t;
                var deep = FindChild<T>(child);
                if (deep != null) return deep;
            }
            return null;
        }

        /// <summary>이름으로 FrameworkElement 찾기</summary>
        public static T? FindChildByName<T>(DependencyObject start, string name) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(start);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                if (child is T fe && fe.Name == name) return fe;
                var deep = FindChildByName<T>(child, name);
                if (deep != null) return deep;
            }
            return null;
        }
    }
}
