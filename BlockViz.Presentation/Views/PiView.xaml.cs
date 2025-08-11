using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using BlockViz.Applications.Views;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export, Export(typeof(IPiView))]
    public partial class PiView : UserControl, IPiView
    {
        public ObservableCollection<PlotModel> PieModels { get; set; }

        [ImportingConstructor]
        public PiView()
        {
            InitializeComponent();
        }
    }
}