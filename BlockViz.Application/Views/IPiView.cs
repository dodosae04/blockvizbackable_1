// BlockViz.Applications\Views\IPiView.cs
using System.Collections.ObjectModel;
using System.Waf.Applications;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Applications.Views
{
    public interface IPiView : IView
    {
        ObservableCollection<PlotModel> PieModels { get; set; }
    }
}