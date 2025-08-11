using System.ComponentModel.Composition;
using System.Windows.Controls;
using BlockViz.Applications.Views;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export, Export(typeof(IGanttView))]
    public partial class GanttView : UserControl, IGanttView
    {
        public PlotModel GanttModel { get; set; }

        [ImportingConstructor]
        public GanttView()
        {
            InitializeComponent();
        }
    }
}