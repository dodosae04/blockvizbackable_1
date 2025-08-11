// BlockViz.Applications\Views\IGanttView.cs
using System.Waf.Applications;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Applications.Views
{
    public interface IGanttView : IView
    {
        PlotModel GanttModel { get; set; }
    }
}