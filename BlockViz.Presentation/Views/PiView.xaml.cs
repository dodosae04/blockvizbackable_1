using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using BlockViz.Applications.Views;
using OxyPlot.Wpf;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IPiView))]
    [PartCreationPolicy(System.ComponentModel.Composition.CreationPolicy.NonShared)]
    public partial class PiView : UserControl, IPiView
    {
        private ObservableCollection<PlotModel> models = new();

        public PiView()
        {
            InitializeComponent();
            if (cards != null) cards.ItemsSource = models;
        }

        public ObservableCollection<PlotModel> PieModels
        {
            get => models;
            set
            {
                models = value ?? new ObservableCollection<PlotModel>();
                if (cards != null) cards.ItemsSource = models;
            }
        }

        private void OnPiePlotLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is PlotView plotView)
            {
                ToolTipService.SetIsEnabled(plotView, false);
            }
        }

        private void OnPiePlotUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is PlotView plotView)
            {
                ToolTipService.SetIsEnabled(plotView, false);
            }
        }
    }
}
