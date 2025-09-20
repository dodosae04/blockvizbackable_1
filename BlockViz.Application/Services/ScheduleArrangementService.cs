using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BlockViz.Applications.Models;   // BlockProperties
using BlockViz.Domain.Models;         // Block
using HelixToolkit.Wpf;

namespace BlockViz.Applications.Services
{
    /// <summary>
    /// ScheduleView:
    /// - XY(작업장·좌표)는 FactoryView와 동일.
    /// - 전역 기준점 baseline(t)=RatePerDay×(t-globalStart)로 시간에 따라 계속 상승.
    /// - 각 블록은 baseZ=baseline(Start)에서 시작해 height=baseline(min(Current,End))-baseline(Start) 만큼 쌓임.
    /// - 전역 기준점과 같은 속도로 상승하는 Z축 날짜 그리드/라벨을 표시하여, '언제' 작업했는지 한눈에 보이게 함.
    /// </summary>
    [Export]
    public sealed class ScheduleArrangementService
    {
        // ===== 스케일/보정 =====
        private const double XYScale = 1.0;     // Factory와 동일(1 unit = 1 m)
        private const double ZScale = 1.0;
        private const double FrameLift = 0.02;
        private const double MinXY = 0.001;
        private const double MinZ = 0.001;
        private const double BlockMinHeight = 0.10;    // 최소 표시 높이

        // 기간 → 높이 환산 (전역 기준점 속도 = 블록 상승 속도)
        private const double RatePerDay = 0.5;     // 1일당 0.5m

        // ===== 공장판 기본 크기(Factory와 동일) =====
        private const double BaseFactoryWidth = 421.0;  // X
        private const double BaseFactoryHeight = 422.0;  // Y

        // ===== 작업장 정의(Factory와 동일) =====
        //  id → (x0,x1,y0,y1) [전역 좌표, m]
        private static readonly Dictionary<int, (double x0, double x1, double y0, double y1)> WorkplaceRects =
            new()
            {
                {1, (0,   311, 376, 452)},
                {2, (0,   311, 300, 376)},
                {3, (0,   139,  46,  92)},
                {4, (0,   139,   0,  46)},
                {5, (323, 444,  46,  87)},
                {6, (200, 444,   0,  46)},
            };

        private static double WPMaxX => WorkplaceRects.Values.Max(r => r.x1);
        private static double WPMaxY => WorkplaceRects.Values.Max(r => r.y1);

        // ===== 블록 외곽선/간격(Factory와 동일) =====
        private const bool EnableBlockOutline = true;
        private const double BlockOutlineThickness = 1.2;
        private static readonly Color BlockOutlineColor = Colors.Black;

        // X/Y를 표시용으로 살짝 줄여 간격을 만드는 값(센터 유지)
        private const double BlockShrinkMeters = 1.0;

        // Z축 날짜 그리드 라벨 스타일
        private static readonly Color ZGridColor = Colors.DarkKhaki;
        private const double ZGridThickness = 0.8;
        private const double ZLabelOffsetX = 2.0;   // 라벨을 우측 바깥쪽으로 조금 빼고 싶을 때(+X)
        private const double ZLabelOffsetY = 1.0;   // +Y 방향 오프셋
        private const int ZLabelFontSize = 12;

        // ---------- Public API ----------
        public IEnumerable<ModelVisual3D> Arrange(IEnumerable<Block> blocks, DateTime currentDate)
        {
            var visuals = new List<ModelVisual3D>();
            var seq = (blocks ?? Array.Empty<Block>()).Where(b => b != null).ToList();

            // 전역 시작점(기준점의 원점)
            DateTime globalStart = seq.Count > 0 ? seq.Min(b => b.Start) : currentDate;

            // 공장판 크기(작업장/블록 외접 경계) — Factory와 동일
            double maxX = WPMaxX, maxY = WPMaxY;
            foreach (var b in seq)
            {
                (double cx, double cy) = GetCenterXY(b);
                double spanX = Math.Max(MinXY, (b.Length > 0 ? b.Length : MinXY) * XYScale);
                double spanY = Math.Max(MinXY, (b.Breadth > 0 ? b.Breadth : MinXY) * XYScale);
                maxX = Math.Max(maxX, cx + spanX / 2.0);
                maxY = Math.Max(maxY, cy + spanY / 2.0);
            }

            double factoryW = Math.Max(BaseFactoryWidth, maxX);
            double factoryH = Math.Max(BaseFactoryHeight, maxY);

            // 조명 + 노란판 + 외곽 + 격자 + 작업장 프레임/라벨
            visuals.Add(new DefaultLights());
            visuals.Add(BuildFactoryBoard(factoryW, factoryH));
            foreach (var id in WorkplaceRects.Keys.OrderBy(k => k))
            {
                visuals.Add(BuildWorkplaceFrame(id));
                visuals.Add(BuildWorkplaceLabel(id));
            }

            // ==== Z축 날짜 그리드/라벨 ====
            visuals.Add(BuildZDateGrid(factoryW, factoryH, globalStart, currentDate));

            if (seq.Count == 0) return visuals;

            // 시작하지 않은 블록은 표시하지 않음(요구사항)
            var started = seq.Where(b => b.Start <= currentDate).ToList();
            if (started.Count == 0) return visuals;

            foreach (var b in started)
            {
                (double cx, double cy) = GetCenterXY(b);

                // XY 크기(Factory와 동일 매핑)
                double sizeX = Math.Max(MinXY, (b.Length > 0 ? b.Length : MinXY) * XYScale);   // X = Length(가로)
                double sizeY = Math.Max(MinXY, (b.Breadth > 0 ? b.Breadth : MinXY) * XYScale);   // Y = Breadth(세로)

                // ---- 전역 기준점에 따른 Z 범위 ----
                double baseZ = BaselineAt(b.Start, globalStart);
                double topZ = BaselineAt(Min(currentDate, b.End), globalStart);
                double sizeZ = Math.Max((topZ - baseZ) * ZScale, BlockMinHeight);

                // 표시용 X/Y 축소
                double visX = Math.Max(MinXY, sizeX - BlockShrinkMeters);
                double visY = Math.Max(MinXY, sizeY - BlockShrinkMeters);

                // 센터 Z는 바닥+절반
                double cz = baseZ + sizeZ / 2.0;

                var mv = new ModelVisual3D();
                var box = new BoxVisual3D
                {
                    Center = new Point3D(cx, cy, cz),
                    Length = visX,      // X
                    Width = visY,      // Y
                    Height = sizeZ,     // Z
                    Material = MaterialHelper.CreateMaterial(Brushes.Silver),
                    BackMaterial = MaterialHelper.CreateMaterial(Brushes.Silver)
                };
                mv.Children.Add(box);

                if (EnableBlockOutline)
                {
                    double x0 = cx - visX / 2.0, x1 = cx + visX / 2.0;
                    double y0 = cy - visY / 2.0, y1 = cy + visY / 2.0;
                    double z0 = baseZ, z1 = baseZ + sizeZ;
                    mv.Children.Add(BuildBoxWireEdges(x0, x1, y0, y1, z0, z1, BlockOutlineColor, BlockOutlineThickness));
                }

                BlockProperties.SetData(mv, b);
                visuals.Add(mv);
            }

            return visuals;
        }

        // ==== 기준점/시간 유틸 ====
        private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;

        // 기준점 높이: globalStart부터 time까지의 경과일 × RatePerDay
        private static double BaselineAt(DateTime time, DateTime globalStart)
        {
            var days = (time - globalStart).TotalDays;
            if (days < 0) days = 0;
            return days * RatePerDay;
        }

        // ==== XY 좌표(Factory와 동일: center_x/center_y가 전역 좌표) ====
        private static (double cx, double cy) GetCenterXY(Block b)
            => (b.X * XYScale, b.Y * XYScale);

        // ==== 공장판/작업장(Factory와 동일) ====
        private static ModelVisual3D BuildFactoryBoard(double width, double height)
        {
            var group = new ModelVisual3D();

            // 노란 바닥
            group.Children.Add(new BoxVisual3D
            {
                Center = new Point3D(width * 0.5, height * 0.5, -2.0),
                Width = width,
                Length = height,
                Height = 0.02,
                Material = MaterialHelper.CreateMaterial(Color.FromRgb(255, 222, 89)),
                BackMaterial = MaterialHelper.CreateMaterial(Color.FromRgb(255, 222, 89))
            });

            // 외곽선
            group.Children.Add(BuildRectLines(0, width, 0, height, Colors.DimGray, 1.5));

            // 50m 격자
            const double step = 50.0;
            var grid = new LinesVisual3D { Color = Colors.Gainsboro, Thickness = 1.0 };
            for (double x = 0; x <= width + 1e-6; x += step)
            {
                grid.Points.Add(new Point3D(x, 0, FrameLift));
                grid.Points.Add(new Point3D(x, height, FrameLift));
            }
            for (double y = 0; y <= height + 1e-6; y += step)
            {
                grid.Points.Add(new Point3D(0, y, FrameLift));
                grid.Points.Add(new Point3D(width, y, FrameLift));
            }
            group.Children.Add(grid);

            // 원점 마커
            group.Children.Add(new SphereVisual3D
            {
                Center = new Point3D(0, 0, FrameLift),
                Radius = 1.0,
                Material = MaterialHelper.CreateMaterial(Brushes.IndianRed)
            });

            return group;
        }

        private static ModelVisual3D BuildWorkplaceFrame(int id)
        {
            var (x0, x1, y0, y1) = WorkplaceRects[id];
            return BuildRectLines(x0, x1, y0, y1, Colors.SteelBlue, 2.0);
        }

        private static ModelVisual3D BuildWorkplaceLabel(int id)
        {
            var (x0, _, y0, _) = WorkplaceRects[id];
            return new BillboardTextVisual3D
            {
                Text = $"작업장 {id}",
                Position = new Point3D(x0 + 0.5, y0 + 0.5, FrameLift + 2.0),
                Foreground = Brushes.Black,
                Background = Brushes.Transparent,
                FontSize = 14
            };
        }

        private static ModelVisual3D BuildRectLines(double x0, double x1, double y0, double y1, Color color, double thickness)
        {
            var lines = new LinesVisual3D { Color = color, Thickness = thickness };
            var p1 = new Point3D(x0, y0, FrameLift);
            var p2 = new Point3D(x1, y0, FrameLift);
            var p3 = new Point3D(x1, y1, FrameLift);
            var p4 = new Point3D(x0, y1, FrameLift);

            lines.Points.Add(p1); lines.Points.Add(p2);
            lines.Points.Add(p2); lines.Points.Add(p3);
            lines.Points.Add(p3); lines.Points.Add(p4);
            lines.Points.Add(p4); lines.Points.Add(p1);
            return lines;
        }

        // ==== Z날짜 그리드/라벨 ====
        private static ModelVisual3D BuildZDateGrid(double width, double height, DateTime globalStart, DateTime upTo)
        {
            var group = new ModelVisual3D();

            // 총 경과일
            var totalDays = Math.Max(0.0, (upTo - globalStart).TotalDays);
            // 보기 좋은 간격 선택 (약 6~12개 눈금 유지)
            int stepDays = ChooseGoodStepDays(totalDays);

            // 0, step, 2*step, ... upTo까지
            for (int d = 0; d <= Math.Max(1, (int)Math.Ceiling(totalDays)); d += stepDays)
            {
                var date = globalStart.AddDays(d);
                double z = d * RatePerDay; // 기준점 속도와 동일

                // 공장판 윤곽선 모양의 사각 프레임을 해당 z에 그려 "층"처럼 보이게
                var frame = BuildRectLinesAtZ(0, width, 0, height, z, ZGridColor, ZGridThickness);
                group.Children.Add(frame);

                // 날짜 라벨 (우측 전면 모서리 근처)
                var label = new BillboardTextVisual3D
                {
                    Text = date.ToString("yyyy-MM-dd"),
                    Position = new Point3D(width + ZLabelOffsetX, ZLabelOffsetY, z + 0.01),
                    Foreground = Brushes.DimGray,
                    Background = Brushes.Transparent,
                    FontSize = ZLabelFontSize
                };
                group.Children.Add(label);
            }

            return group;
        }

        private static int ChooseGoodStepDays(double totalDays)
        {
            if (totalDays <= 14) return 1;
            if (totalDays <= 30) return 2;
            if (totalDays <= 60) return 5;
            if (totalDays <= 120) return 7;
            if (totalDays <= 240) return 14;
            if (totalDays <= 540) return 30;
            if (totalDays <= 900) return 60;
            return 90;
        }

        private static ModelVisual3D BuildRectLinesAtZ(double x0, double x1, double y0, double y1, double z, Color color, double thickness)
        {
            var lines = new LinesVisual3D { Color = color, Thickness = thickness };
            var p1 = new Point3D(x0, y0, z);
            var p2 = new Point3D(x1, y0, z);
            var p3 = new Point3D(x1, y1, z);
            var p4 = new Point3D(x0, y1, z);

            lines.Points.Add(p1); lines.Points.Add(p2);
            lines.Points.Add(p2); lines.Points.Add(p3);
            lines.Points.Add(p3); lines.Points.Add(p4);
            lines.Points.Add(p4); lines.Points.Add(p1);
            return lines;
        }

        // ==== 블록 외곽선 ====
        private static ModelVisual3D BuildBoxWireEdges(
            double x0, double x1, double y0, double y1, double z0, double z1,
            Color color, double thickness)
        {
            var lines = new LinesVisual3D { Color = color, Thickness = thickness };

            var p000 = new Point3D(x0, y0, z0);
            var p100 = new Point3D(x1, y0, z0);
            var p110 = new Point3D(x1, y1, z0);
            var p010 = new Point3D(x0, y1, z0);

            var p001 = new Point3D(x0, y0, z1);
            var p101 = new Point3D(x1, y0, z1);
            var p111 = new Point3D(x1, y1, z1);
            var p011 = new Point3D(x0, y1, z1);

            // 바닥
            lines.Points.Add(p000); lines.Points.Add(p100);
            lines.Points.Add(p100); lines.Points.Add(p110);
            lines.Points.Add(p110); lines.Points.Add(p010);
            lines.Points.Add(p010); lines.Points.Add(p000);

            // 천장
            lines.Points.Add(p001); lines.Points.Add(p101);
            lines.Points.Add(p101); lines.Points.Add(p111);
            lines.Points.Add(p111); lines.Points.Add(p011);
            lines.Points.Add(p011); lines.Points.Add(p001);

            // 기둥 4개
            lines.Points.Add(p000); lines.Points.Add(p001);
            lines.Points.Add(p100); lines.Points.Add(p101);
            lines.Points.Add(p110); lines.Points.Add(p111);
            lines.Points.Add(p010); lines.Points.Add(p011);

            var mv = new ModelVisual3D();
            mv.Children.Add(lines);
            return mv;
        }
    }
}
