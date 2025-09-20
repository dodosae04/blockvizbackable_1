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
        private const int DefaultFilterKey = 0;

        private readonly Dictionary<int, ToggleButton> filterButtons;
        private bool suppressFilterNotification;
        private PlotModel model;

        public GanttView()
        {
            InitializeComponent();

            filterButtons = new Dictionary<int, ToggleButton>
            {
                { DefaultFilterKey, filterDefaultButton },
                { 1, filterWp1Button },
                { 2, filterWp2Button },
                { 3, filterWp3Button },
                { 4, filterWp4Button },
                { 5, filterWp5Button },
                { 6, filterWp6Button }
            };

            SetActiveButton(DefaultFilterKey);
        }

        public event EventHandler<WorkplaceFilterRequestedEventArgs> WorkplaceFilterRequested;

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

        public void SetActiveWorkplace(int? workplaceId)
        {
            int key = workplaceId.HasValue ? workplaceId.Value : DefaultFilterKey;
            if (!filterButtons.ContainsKey(key))
            {
                key = DefaultFilterKey;
            }

            SetActiveButton(key);
        }

        private void SetActiveButton(int key)
        {
            suppressFilterNotification = true;
            try
            {
                foreach (var pair in filterButtons)
                {
                    pair.Value.IsChecked = pair.Key == key;
                }
            }
            finally
            {
                suppressFilterNotification = false;
            }
        }

        private void OnFilterButtonChecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (suppressFilterNotification)
            {
                return;
            }

            if (sender is ToggleButton toggle)
            {
                int key = ExtractFilterKey(toggle.Tag);
                if (!filterButtons.ContainsKey(key))
                {
                    key = DefaultFilterKey;
                }

                suppressFilterNotification = true;
                try
                {
                    foreach (var pair in filterButtons)
                    {
                        if (!ReferenceEquals(pair.Value, toggle))
                        {
                            pair.Value.IsChecked = false;
                        }
                    }
                }
                finally
                {
                    suppressFilterNotification = false;
                }

                var workplaceId = key == DefaultFilterKey ? (int?)null : key;
                WorkplaceFilterRequested?.Invoke(this, new WorkplaceFilterRequestedEventArgs(workplaceId));
            }
        }

        private void OnFilterButtonUnchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (suppressFilterNotification)
            {
                return;
            }

            if (sender is ToggleButton toggle)
            {
                suppressFilterNotification = true;
                try
                {
                    toggle.IsChecked = true;
                }
                finally
                {
                    suppressFilterNotification = false;
                }
            }
        }

        private static int ExtractFilterKey(object tag)
        {
            if (tag is int intValue)
            {
                return intValue;
            }

            if (tag is string stringValue && int.TryParse(stringValue, out int parsed))
            {
                return parsed;
            }

            return DefaultFilterKey;
        }
    }
}
