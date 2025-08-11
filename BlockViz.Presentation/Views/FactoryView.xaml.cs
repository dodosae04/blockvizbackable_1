using System;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows;
using BlockViz.Applications.Views;
using System.Windows.Media.Media3D;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BlockViz.Domain.Models;
using HelixToolkit.Wpf;
using System.Windows.Media;

namespace BlockViz.Presentation.Views
{
    [Export, Export(typeof(IFactoryView))]
    public partial class FactoryView : UserControl, IFactoryView
    {
        public ObservableCollection<Visual3D> Visuals { get; set; }
        public DateTime CurrentDate { get; set; }
        public event Action<Block>? BlockClicked;

        [ImportingConstructor]
        public FactoryView()
        {
            InitializeComponent();
            viewport.MouseLeftButtonDown += OnViewportMouseDown;
        }

        private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
        {
            var hits = Viewport3DHelper.FindHits(viewport.Viewport, e.GetPosition(viewport));
            if (hits.Count == 0) return;

            DependencyObject v = hits[0].Visual as DependencyObject;
            while (v != null)
            {
                if (v is ModelVisual3D mv)
                {
                    var block = BlockViz.Applications.Models.BlockProperties.GetData(mv);
                    if (block != null)
                    {
                        BlockClicked?.Invoke(block);
                        break;
                    }
                }
                v = VisualTreeHelper.GetParent(v);
            }
        }
    }
}