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
    /// 좌표계: XY 바닥, +Z 위.
    /// - 엑셀 X,Y는 "블록 중심(center)의 전역(Factory) 좌표"로 해석
    /// - Length → +X(가로), Breadth → +Y(세로), Height → +Z
    /// - 공장판/작업장 크기와 위치는 기존 설정을 그대로 사용
    /// </summary>
    [Export(typeof(IBlockArrangementService))]
    [Export]
    public class BlockArrangementService : IBlockArrangementService
    {
        // ===== 기존 설정 =====
        private const bool ExcelCoordinatesAreGlobal = true;

        private const double XYScale = 1.0;    // 1 unit = 1 m
        private const double ZScale = 1.0;
        private const double FrameLift = 0.02;
        private const double MinXY = 0.001;
        private const double MinZ = 0.001;
        private const double EPS = 1e-6;

        private const double BaseFactoryWidth = 421.0;  // X
        private const double BaseFactoryHeight = 422.0;  // Y

        // ===== 작업장 정의 (첨부본 그대로) =====
        //   id → (x0,x1,y0,y1)  [전역 좌표, 단위 m]
        private static readonly Dictionary<int, (double x0, double x1, double y0, double y1)> WorkplaceRects =
            new()
            {
                {1, (0,   311, 376, 452)},
                {2, (0,   311, 300, 376)},
                {3, (0,   139,  46,  92)},
                {4, (0,   139,   0,  46)}, // (0,0) 시작
                {5, (323, 444,  46,  87)},
                {6, (200, 444,   0,  46)},
            };

        private static double WPMaxX => WorkplaceRects.Values.Max(r => r.x1);
        private static double WPMaxY => WorkplaceRects.Values.Max(r => r.y1);

        // ===== [추가] 블록 외곽선/축소 옵션 =====
        private const bool EnableBlockOutline = true;    // 검정 외곽선 ON/OFF
        private const double BlockOutlineThickness = 1.2;     // 외곽선 두께
        private static readonly Color BlockOutlineColor = Colors.Black;

        // 가로(X=Length), 세로(Y=Breadth) 각각 축소할 값[m]. 0이면 축소 없음.
        private const double BlockShrinkMeters = 1.0;

        public IEnumerable<ModelVisual3D> Arrange(IEnumerable<Block> blocks, DateTime date)
        {
            var visuals = new List<ModelVisual3D>();

            // 오늘 보이는 블록
            var live = (blocks ?? Array.Empty<Block>())
                      .Where(b => b.Start <= date && date <= b.End)
                      .ToList();

            // 공장판 크기 산출 (작업장/블록 외접 경계 기준) — 기존 로직 유지
            double maxX = WPMaxX, maxY = WPMaxY;
            foreach (var b in live)
            {
                GetGlobalCenterXY(b, out double cx, out double cy);

                double w = Math.Max(MinXY, (b.Length > 0 ? b.Length : MinXY) * XYScale); // X span
                double l = Math.Max(MinXY, (b.Breadth > 0 ? b.Breadth : MinXY) * XYScale); // Y span

                maxX = Math.Max(maxX, cx + w / 2.0);
                maxY = Math.Max(maxY, cy + l / 2.0);
            }

            double factoryW = Math.Max(BaseFactoryWidth, maxX);
            double factoryH = Math.Max(BaseFactoryHeight, maxY);

            // 조명 + 공장판(노란 바닥) + 격자 + 외곽/원점 — 기존 모양 그대로
            visuals.Add(new DefaultLights());
            visuals.Add(BuildFactoryBoard(factoryW, factoryH));

            // 작업장 프레임/라벨 — 기존 좌표/크기 그대로
            foreach (var id in WorkplaceRects.Keys.OrderBy(k => k))
            {
                visuals.Add(BuildWorkplaceFrame(id));
                visuals.Add(BuildWorkplaceLabel(id));
            }

            if (live.Count == 0) return visuals;

            // 블록 배치 (센터 좌표/색/높이 산출 방식은 기존 그대로, 시각화만 개선)
            foreach (var b in live)
            {
                GetGlobalCenterXY(b, out double cx, out double cy);

                // 원본 크기
                double sizeX = Math.Max(MinXY, (b.Length > 0 ? b.Length : MinXY) * XYScale);   // X = Length(가로)
                double sizeY = Math.Max(MinXY, (b.Breadth > 0 ? b.Breadth : MinXY) * XYScale);   // Y = Breadth(세로)
                double sizeZ = Math.Max(MinZ, (b.Height > 0 ? b.Height : MinZ) * ZScale);    // Z = Height

                // 표시용 축소(센터 고정 → 간격 생성)
                double visX = Math.Max(MinXY, sizeX - BlockShrinkMeters);
                double visY = Math.Max(MinXY, sizeY - BlockShrinkMeters);

                double cz = sizeZ / 2.0;

                var mv = new ModelVisual3D();

                // HelixToolkit: Length=X, Width=Y, Height=Z  (정확히 매핑)
                var box = new BoxVisual3D
                {
                    Center = new Point3D(cx, cy, cz),
                    Length = visX,   // X(가로)
                    Width = visY,   // Y(세로)
                    Height = sizeZ,  // Z
                    Material = MaterialHelper.CreateMaterial(Brushes.Silver),
                    BackMaterial = MaterialHelper.CreateMaterial(Brushes.Silver)
                };
                mv.Children.Add(box);

                // 검정 외곽선 (표시용 크기와 1:1 일치)
                if (EnableBlockOutline)
                {
                    double x0 = cx - visX / 2.0, x1 = cx + visX / 2.0;
                    double y0 = cy - visY / 2.0, y1 = cy + visY / 2.0;
                    double z0 = 0.0, z1 = sizeZ;

                    mv.Children.Add(BuildBoxWireEdges(
                        x0, x1, y0, y1, z0, z1, BlockOutlineColor, BlockOutlineThickness));
                }

                // 기존 속성 바인딩
                BlockProperties.SetData(mv, b);

                visuals.Add(mv);
            }

            return visuals;
        }

        // ───────── Helper: 전역 "중심(center)" 좌표 얻기 ─────────
        private static void GetGlobalCenterXY(Block b, out double cx, out double cy)
        {
            if (ExcelCoordinatesAreGlobal)
            {
                cx = b.X * XYScale;
                cy = b.Y * XYScale;
            }
            else
            {
                double wpX0 = 0.0, wpY0 = 0.0;
                if (WorkplaceRects.TryGetValue(b.DeployWorkplace, out var rect))
                {
                    wpX0 = rect.x0;
                    wpY0 = rect.y0;
                }
                cx = (wpX0 + b.X) * XYScale;
                cy = (wpY0 + b.Y) * XYScale;
            }

            if (Math.Abs(cx) < EPS) cx = 0.0;
            if (Math.Abs(cy) < EPS) cy = 0.0;
        }

        // ───────── 보드/프레임/라벨 (첨부본 그대로) ─────────
        private static ModelVisual3D BuildFactoryBoard(double width, double height)
        {
            var group = new ModelVisual3D();

            // 노란 작업판(좌하단 0,0 / z=-2)
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

        // ───────── [추가] 블록 외곽선 생성 ─────────
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
