using BlockViz.Domain.Models;
using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BlockViz.Applications.Models
{
    public class BlockVisual
    {
        public Block Data { get; }
        public BoxVisual3D Visual { get; } 

        public BlockVisual(Block data, Brush color,
            double scale, double cell)
        {
            Data = data;

            double w = data.Height * scale;
            double d = data.Length * scale;
            double h = data.Breadth * scale;

            double cx = ((data.DeployWorkplace - 1) % 3) * cell + w * 0.5;
            double cz = ((data.DeployWorkplace - 1) / 3) * cell + d * 0.5;

            Visual = new BoxVisual3D
            {
                Width = w,
                Length = d,
                Height = h,
                Center = new Point3D(cx, h * 0.5, cz),
                Fill = color
            };
        }

        public void Update(DateTime now)
        {
            Visual.Visible = (now >= Data.Start && now <= Data.End);
        }
    }
}