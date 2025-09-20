using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BlockViz.Applications.Views;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Presentation.Views
{
    [Export(typeof(IGanttView))]
    [PartCreationPolicy(System.ComponentModel.Composition.CreationPolicy.NonShared)]
    public partial class GanttView : UserControl, IGanttView
    {
        private readonly Dictionary<int, ToggleButton> toggleButtons;
        private bool suppressToggleNotification;
        private PlotModel model;

        public GanttView()
        {
            InitializeComponent();

            toggleButtons = new Dictionary<int, ToggleButton>
            {
                { 1, toggleWp1 },
                { 2, toggleWp2 },
                { 3, toggleWp3 },
                { 4, toggleWp4 },
                { 5, toggleWp5 },
                { 6, toggleWp6 }
            };
        }

        public event EventHandler<WorkplaceToggleChangedEventArgs> WorkplaceToggleChanged;

        public event EventHandler ExpandAllRequested;

        public event EventHandler CollapseAllRequested;

        public PlotModel GanttModel
        {
            get => model;
            set
            {
                model = value;
                if (plot != null)
                {
                    plot.Model = model;
                }
            }
        }

        public void SetWorkplaceToggleStates(IReadOnlyDictionary<int, bool> states)
        {
            suppressToggleNotification = true;
            try
            {
                foreach (var pair in toggleButtons)
                {
                    bool isExpanded = states != null && states.TryGetValue(pair.Key, out var value) && value;
                    pair.Value.IsChecked = isExpanded;
                }
            }
            finally
            {
                suppressToggleNotification = false;
            }
        }

        private void OnWorkplaceToggleChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (suppressToggleNotification)
            {
                return;
            }

            if (sender is ToggleButton toggle)
            {
                int id = ExtractWorkplaceId(toggle.Tag);
                if (id > 0)
                {
                    bool isExpanded = toggle.IsChecked == true;
                    WorkplaceToggleChanged?.Invoke(this, new WorkplaceToggleChangedEventArgs(id, isExpanded));
                }
            }
        }

        private void OnExpandAllClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            ExpandAllRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnCollapseAllClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            CollapseAllRequested?.Invoke(this, EventArgs.Empty);
        }

        private static int ExtractWorkplaceId(object tag)
        {
            if (tag is int intValue)
            {
                return intValue;
            }

            if (tag is string stringValue && int.TryParse(stringValue, out int parsed))
            {
                return parsed;
            }

            return -1;
        }
    }
}

