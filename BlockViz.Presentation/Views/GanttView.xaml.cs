using System.ComponentModel.Composition;
using System.Windows.Controls;
using BlockViz.Applications.Views;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IGanttView))]
    [PartCreationPolicy(System.ComponentModel.Composition.CreationPolicy.NonShared)]
    public partial class GanttView : UserControl, IGanttView
    {
        private PlotModel model;

        public GanttView()
        {
            InitializeComponent();
        }

        public PlotModel GanttModel
        {
            get => model;
            set { model = value; if (plot != null) plot.Model = model; }
        }
    }
}