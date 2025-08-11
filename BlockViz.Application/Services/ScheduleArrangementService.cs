using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BlockViz.Domain.Models;
using HelixToolkit.Wpf;
using BlockViz.Applications.Models;

namespace BlockViz.Applications.Services
{
    [Export]
    public class ScheduleArrangementService
    {
        // ── 설정 값 ────────────────────────────────────────────
        private const double Scale = 0.15;  // BlockArrangementService와 동일
        private const double GapRatio = 0.7;   // Breadth 대비 간격 비율
        private const double RatePerDay = 0.7;   // 하루당 높이 증가량

        // 작업장 중심 좌표
        private static readonly Dictionary<int, Point3D> WorkplaceCenters = new()
        {
            {1, new Point3D(-30, 0, -40)},
            {2, new Point3D(-30, 0, -17)},
            {3, new Point3D(-54, 0,  17)},
            {4, new Point3D(-54, 0,  40)},
            {5, new Point3D( 15, 0,  17)},
            {6, new Point3D(  5, 0,  40)},
        };

        // 작업장 크기 (X 폭, Z 깊이)
        private static readonly Dictionary<int, (double X, double Z)> WorkplaceSizes = new()
        {
            {1, (12*7, 4*5)},
            {2, (12*7, 4*5)},
            {3, ( 6*6, 4*5)},
            {4, ( 6*6, 4*5)},
            {5, ( 6*7, 4*5)},
            {6, (12*6, 4*5)},
        };

        // 색상 팔레트
        private static readonly Brush[] Palette = {
            Brushes.Red, Brushes.Orange, Brushes.Yellow,
            Brushes.LimeGreen, Brushes.DeepSkyBlue, Brushes.MediumPurple
        };

        public IEnumerable<ModelVisual3D> Arrange(IEnumerable<Block> blocks, DateTime currentDate)
        {
            var visuals = new List<ModelVisual3D>();

            // 1) 조명·바닥·공장
            visuals.Add(new DefaultLights());
            visuals.Add(BuildFactoryFloor());
            var factoryModel = LoadFactoryModel();
            if (factoryModel != null) visuals.Add(factoryModel);

            // 2) 작업장 프레임 + 번호 라벨
            for (int id = 1; id <= 6; id++)
            {
                visuals.Add(BuildFrameAtCenter(id));
                visuals.Add(BuildLabelAtTopLeft(id));
            }

            // 3) globalStart / globalEnd 계산
            var allStarts = blocks.Select(b => b.Start).ToList();
            var allEnds = blocks.Select(b => b.End).ToList();
            if (!allStarts.Any() || !allEnds.Any())
                return visuals;
            DateTime globalStart = allStarts.Min();
            DateTime globalEnd = allEnds.Max();

            // 4) 작업장별 모든 블록 배치
            foreach (var wpGroup in blocks
                .OrderBy(b => b.Start)
                .GroupBy(b => b.DeployWorkplace)
                .OrderBy(g => g.Key))
            {
                int wpId = wpGroup.Key;
                if (!WorkplaceCenters.ContainsKey(wpId)) continue;

                var center = WorkplaceCenters[wpId];
                var (workX, _) = WorkplaceSizes[wpId];
                double fullWidth = workX * Scale;

                // 프로젝트별 그룹화
                var projectGroups = wpGroup
                    .GroupBy(b => b.Name)
                    .Select((g, idx) => (Blocks: g.OrderBy(b => b.Start).ToList(), Index: idx))
                    .ToList();

                int trackCount = projectGroups.Count;
                // 동적 트랙 중심 X 좌표
                var offsets = Enumerable.Range(0, trackCount)
                    .Select(i => center.X - fullWidth / 2 + fullWidth * (i + 0.5) / trackCount)
                    .ToList();

                foreach (var (projBlocks, trackIdx) in projectGroups)
                {
                    // 폭·간격 계산
                    var widths = projBlocks.Select(b => b.Length * Scale).ToList();
                    var breadths = projBlocks.Select(b => b.Breadth * Scale).ToList();
                    var gaps = breadths.Select(b => b * GapRatio).ToList();

                    double span = widths.Sum() + gaps.Sum();
                    double startX = offsets[trackIdx] - span / 2;
                    double accX = startX;

                    for (int i = 0; i < projBlocks.Count; i++)
                    {
                        var blk = projBlocks[i];
                        accX += gaps[i];

                        // 높이: 시작일부터 End/now 중 빠른 쪽
                        var effEnd = currentDate < blk.End ? currentDate : blk.End;
                        double elapsed = (effEnd - blk.Start).TotalDays;
                        double h = Math.Max(elapsed * RatePerDay * Scale, 0.1);

                        // baseline: 시작일까지 누적 높이
                        double baseline = Math.Max(
                            (blk.Start - globalStart).TotalDays * RatePerDay * Scale, 0.0);

                        double w = widths[i];
                        double d = blk.Breadth * Scale;

                        double xC = accX + w / 2;
                        double yC = baseline + h / 2;
                        double zC = center.Z;

                        var bc = new Point3D(xC, yC, zC);
                        var m = Regex.Match(blk.Name, @"\d+");
                        int idx = m.Success ? int.Parse(m.Value) - 1 : 0;
                        var color = Palette[idx % Palette.Length];

                        var box = new BoxVisual3D
                        {
                            Center = bc,
                            Width = w,
                            Length = d,
                            Height = h,
                            Material = MaterialHelper.CreateMaterial(color),
                            BackMaterial = MaterialHelper.CreateMaterial(color),
                            Transform = new RotateTransform3D(
                                new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90), bc)
                        };

                        var mv = new ModelVisual3D();
                        mv.Children.Add(box);
                        BlockProperties.SetData(mv, blk);
                        visuals.Add(mv);

                        accX += w;
                    }
                }
            }

            // 5) 날짜 스케일 추가 (왼쪽/오른쪽)
            visuals.Add(BuildDateScaleRuler(true, globalStart, globalEnd));
            visuals.Add(BuildDateScaleRuler(false, globalStart, globalEnd));

            return visuals;
        }

        // ── 헬퍼: 날짜 축 (Y축) ────────────────────────────────────
        private ModelVisual3D BuildDateScaleRuler(bool left, DateTime start, DateTime end)
        {
            var group = new ModelVisual3D();
            double totalDays = (end - start).TotalDays;
            double height = totalDays * RatePerDay * Scale;
            double intervalDays = Math.Max(1, totalDays / 10.0);

            double x = left ? -90 : 50;
            double z = 0;

            var line = new LinesVisual3D { Color = Colors.Black, Thickness = 1 };
            line.Points.Add(new Point3D(x, 0, z));
            line.Points.Add(new Point3D(x, height, z));

            for (double d = 0; d <= totalDays; d += intervalDays)
            {
                double y = d * RatePerDay * Scale;
                var dt = start.AddDays(d);
                line.Points.Add(new Point3D(x - 1, y, z));
                line.Points.Add(new Point3D(x + 1, y, z));

                group.Children.Add(new BillboardTextVisual3D
                {
                    Text = dt.ToString("yyyy-MM-dd"),
                    Position = new Point3D(x - 5, y, z),
                    FontSize = 10,
                    Foreground = Brushes.Black,
                    Background = Brushes.Transparent
                });
            }

            group.Children.Add(line);
            return group;
        }

        // ── 헬퍼: 공장 바닥 ─────────────────────────────────────────
        private ModelVisual3D BuildFactoryFloor()
        {
            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;
            foreach (var kv in WorkplaceCenters)
            {
                var c = kv.Value;
                var s = WorkplaceSizes[kv.Key];
                minX = Math.Min(minX, c.X - s.X / 2);
                maxX = Math.Max(maxX, c.X + s.X / 2);
                minZ = Math.Min(minZ, c.Z - s.Z / 2);
                maxZ = Math.Max(maxZ, c.Z + s.Z / 2);
            }
            double width = (maxX - minX) - 20;
            double length = (maxZ - minZ) + 60;
            double cx = (minX + maxX) / 2;
            double cz = (minZ + maxZ) / 2;
            var center3D = new Point3D(cx, -2, cz);

            var floor = new BoxVisual3D
            {
                Center = center3D,
                Width = width + 35,
                Length = length,
                Height = 0.1,
                Material = MaterialHelper.CreateMaterial(Brushes.Gold)
            };
            floor.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90), center3D);
            return floor;
        }

        // ── 헬퍼: 작업장 프레임 ─────────────────────────────────────
        private ModelVisual3D BuildFrameAtCenter(int id)
        {
            var c = WorkplaceCenters[id];
            var s = WorkplaceSizes[id];
            double hx = s.X / 2, hz = s.Z / 2;

            var lines = new LinesVisual3D { Color = Colors.DimGray, Thickness = 2 };
            var p1 = new Point3D(c.X - hx, 0, c.Z - hz);
            var p2 = new Point3D(c.X + hx, 0, c.Z - hz);
            var p3 = new Point3D(c.X + hx, 0, c.Z + hz);
            var p4 = new Point3D(c.X - hx, 0, c.Z + hz);
            lines.Points.Add(p1); lines.Points.Add(p2);
            lines.Points.Add(p2); lines.Points.Add(p3);
            lines.Points.Add(p3); lines.Points.Add(p4);
            lines.Points.Add(p4); lines.Points.Add(p1);
            return lines;
        }

        // ── 헬퍼: 작업장 번호 라벨 ─────────────────────────────────
        private ModelVisual3D BuildLabelAtTopLeft(int id)
        {
            var c = WorkplaceCenters[id];
            var s = WorkplaceSizes[id];
            double x = c.X - s.X / 2 + 0.5;
            double y = 0.2;
            double z = c.Z - s.Z / 2 + 0.5;
            return new BillboardTextVisual3D
            {
                Text = $"작업장 {id}",
                Position = new Point3D(x, y, z),
                Foreground = Brushes.Black,
                Background = Brushes.Transparent,
                FontSize = 14
            };
        }

        // ── 헬퍼: 공장 모델 로드 ───────────────────────────────────
        private ModelVisual3D? LoadFactoryModel()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Models", "factory.obj");
            if (!File.Exists(path)) return null;
            var importer = new ModelImporter();
            var model = importer.Load(path);
            return new ModelVisual3D { Content = model };
        }
    }
}
