using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BlockViz.Applications.Helpers;
using BlockViz.Applications.Models;
using BlockViz.Domain.Models;
using HelixToolkit.Wpf;

namespace BlockViz.Applications.Services
{
    [Export(typeof(IBlockArrangementService))]
    public class BlockArrangementService : IBlockArrangementService
    {
        private const double Scale = 0.15;
        private const double GapRatio = 0.7;  // Breadth 대비 간격 비율

        // 작업장 중심 (원본 좌표계)
        private static readonly Dictionary<int, Point3D> WorkplaceCenters = new()
        {
            {1, new Point3D(-30, 0, -40)},
            {2, new Point3D(-30, 0, -17)},
            {3, new Point3D(-54, 0,  17)},
            {4, new Point3D(-54, 0,  40)},
            {5, new Point3D( 15, 0,  17)},
            {6, new Point3D(  5, 0,  40)},
        };

        // 작업장 크기 (원본 좌표계 X폭, Z깊이)
        private static readonly Dictionary<int, (double X, double Z)> WorkplaceSizes = new()
        {
            {1, (12 * 7, 4 * 5)},
            {2, (12 * 7, 4 * 5)},
            {3, ( 6 * 6, 4 * 5)},
            {4, ( 6 * 6, 4 * 5)},
            {5, ( 6 * 7, 4 * 5)},
            {6, (12 * 6, 4 * 5)},
        };

        public IEnumerable<ModelVisual3D> Arrange(IEnumerable<Block> blocks, DateTime date)
        {
            var visuals = new List<ModelVisual3D>();

            // ── 1) 기본 라이트·바닥·공장 모델
            visuals.Add(new DefaultLights());
            visuals.Add(BuildFactoryFloor());
            var factory = LoadFactoryModel();
            if (factory != null) visuals.Add(factory);

            // ── 2) 작업장 프레임 + 라벨
            for (int id = 1; id <= 6; id++)
            {
                visuals.Add(BuildFrameAtCenter(id));
                visuals.Add(BuildLabelAtTopLeft(id));
            }

            // ── 3) 현재 날짜에 유효한 블록만 필터링
            var live = blocks
                .Where(b => b.Start <= date && date <= b.End)
                .ToList();

            // ── 4) 작업장별 배치
            foreach (var wpGroup in live.GroupBy(b => b.DeployWorkplace).OrderBy(g => g.Key))
            {
                int wpId = wpGroup.Key;
                if (!WorkplaceCenters.ContainsKey(wpId)) continue;

                var center = WorkplaceCenters[wpId];
                var size = WorkplaceSizes[wpId];
                double fullWidth = size.X * Scale;
                // 트랙 간격을 깊이 전체만큼 크게 설정합니다.
                double trackSpacing = size.Z * Scale;

                // 4-1) 프로젝트별 그룹화
                var projectGroups = wpGroup
                    .GroupBy(b => b.Name)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Blocks = g.OrderBy(b => b.Start).ToList(),
                        Start = g.Min(b => b.Start),
                        End = g.Max(b => b.End)
                    })
                    .OrderBy(p => p.Start)
                    .ToList();

                // 4-2) 3개 트랙에 프로젝트 할당 (시간 겹침 피하기)
                var tracks = new List<string>[3] { new(), new(), new() };
                var projectTrack = new Dictionary<string, int>();
                foreach (var proj in projectGroups)
                {
                    int assign = 2;
                    for (int t = 0; t < 3; t++)
                    {
                        if (tracks[t].All(name =>
                        {
                            var other = projectGroups.First(p => p.Name == name);
                            return other.End <= proj.Start || other.Start >= proj.End;
                        }))
                        {
                            assign = t;
                            break;
                        }
                    }
                    tracks[assign].Add(proj.Name);
                    projectTrack[proj.Name] = assign;
                }

                // 4-3) 박스 생성 및 배치
                double halfOffset = fullWidth / 2.0;
                var offsets = new[] { -halfOffset, 0.0, halfOffset };

                foreach (var proj in projectGroups)
                {
                    int tIdx = projectTrack[proj.Name];
                    double z = center.Z + (tIdx - 1) * trackSpacing;

                    var widths = proj.Blocks.Select(b => b.Length * Scale).ToList();
                    var gaps = proj.Blocks.Select(b => b.Breadth * Scale * GapRatio).ToList();
                    double span = widths.Sum() + gaps.Sum();

                    double startX = center.X + offsets[tIdx] - span / 2.0;
                    double accX = startX;

                    for (int i = 0; i < proj.Blocks.Count; i++)
                    {
                        var blk = proj.Blocks[i];
                        accX += gaps[i];

                        double w = blk.Length * Scale;
                        double d = blk.Breadth * Scale;
                        double h = blk.Height * Scale;

                        double xC = accX + w / 2.0;
                        double yC = h / 2.0;

                        var box = new BoxVisual3D
                        {
                            Center = new Point3D(xC, yC, z),
                            Width = w,
                            Length = d,
                            Height = h,
                            Material = MaterialHelper.CreateMaterial(BlockColorMap.GetColor(blk.Name)),
                            BackMaterial = MaterialHelper.CreateMaterial(BlockColorMap.GetColor(blk.Name))
                        };
                        var mv = new ModelVisual3D();
                        mv.Children.Add(box);
                        BlockProperties.SetData(mv, blk);
                        visuals.Add(mv);

                        accX += w;
                    }
                }
            }

            return visuals;
        }

        #region 헬퍼 메서드 (원본 그대로)

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
                new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90),
                center3D);
            return floor;
        }

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

        private ModelVisual3D BuildLabelAtTopLeft(int id)
        {
            var c = WorkplaceCenters[id];
            var s = WorkplaceSizes[id];
            double hx = s.X / 2, hz = s.Z / 2;

            return new BillboardTextVisual3D
            {
                Text = $"작업장 {id}",
                Position = new Point3D(c.X - hx + 0.5, 0.2, c.Z - hz + 0.5),
                Foreground = Brushes.Black,
                Background = Brushes.Transparent,
                FontSize = 14
            };
        }

        private ModelVisual3D? LoadFactoryModel()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "A");
            if (!File.Exists(path)) return null;
            var importer = new ModelImporter();
            var model = importer.Load(path);
            return new ModelVisual3D { Content = model };
        }

        #endregion
    }
}
