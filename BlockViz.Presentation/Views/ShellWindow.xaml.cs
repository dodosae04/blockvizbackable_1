using System.ComponentModel.Composition;
using Fluent;
using BlockViz.Applications.Views;
using RibbonWindow = Fluent.RibbonWindow;

namespace BlockViz.Presentation.Views
{
    [Export, Export(typeof(IShellView))]
    public partial class ShellWindow : RibbonWindow, IShellView
    {
        [ImportingConstructor]
        public ShellWindow()
        {
            InitializeComponent();
        }
    }
}