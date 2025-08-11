using System.Windows.Media.Media3D;

namespace BlockViz.Domain.Models
{
    public class Workspace
    {
        public int Id { get; set; }
        public Point3D Origin { get; set; }
        public double CellLength { get; set; } = 10;
    }
}