using System.Windows.Media;
using OxyPlot;

namespace BlockViz.Applications.Services
{
    public interface IBlockColorService
    {
        Color GetColor(string blockName);

        SolidColorBrush GetBrush(string blockName);

        OxyColor GetOxyColor(string blockName);

        void Reset();
    }
}
