using System;
using System.Collections.ObjectModel;
using System.Windows.Media.Media3D;
using BlockViz.Domain.Models;
using System.Waf.Applications;

namespace BlockViz.Applications.Views
{
    public interface IFactoryView : IView
    {
        ObservableCollection<Visual3D> Visuals { get; set; }

        DateTime CurrentDate { get; set; }

        event Action<Block> BlockClicked;
    }
}