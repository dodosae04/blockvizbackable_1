// BlockViz.Applications\Views\IGanttView.cs
using System;
using System.Collections.Generic;
using System.Waf.Applications;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Applications.Views
{
    public interface IGanttView : IView
    {
        PlotModel GanttModel { get; set; }

        event EventHandler<WorkplaceToggleChangedEventArgs> WorkplaceToggleChanged;

        event EventHandler ExpandAllRequested;

        event EventHandler CollapseAllRequested;

        void SetWorkplaceToggleStates(IReadOnlyDictionary<int, bool> states);
    }
}
