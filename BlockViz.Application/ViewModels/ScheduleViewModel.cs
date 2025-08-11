// ✅ 수정된 ScheduleViewModel.cs - BlockName 기준 색상 일관성 적용
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Media.Media3D;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using System.Waf.Applications;
using System.Collections.Generic;
using System.Windows.Media;
using HelixToolkit.Wpf;
using BlockViz.Applications.Models;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class ScheduleViewModel : ViewModel<IScheduleView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly ScheduleArrangementService arranger;
        private readonly SimulationService simulationService;
        private readonly ISelectionService selectionService;

        private readonly Dictionary<string, Brush> colorMap = new();
        private readonly Brush[] palette = new[]
        {
            Brushes.Red, Brushes.Orange, Brushes.Yellow,
            Brushes.LimeGreen, Brushes.DeepSkyBlue, Brushes.MediumPurple,
            Brushes.Brown, Brushes.Teal, Brushes.Pink
        };

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Visual3D> Visuals { get; }
        public DateTime CurrentDate => simulationService.CurrentDate;

        [ImportingConstructor]
        public ScheduleViewModel(
            IScheduleView view,
            IScheduleService scheduleService,
            ScheduleArrangementService arranger,
            SimulationService simulationService,
            ISelectionService selectionService
        ) : base(view)
        {
            this.scheduleService = scheduleService;
            this.arranger = arranger;
            this.simulationService = simulationService;
            this.selectionService = selectionService;

            Visuals = new ObservableCollection<Visual3D>();
            view.Visuals = Visuals;
            view.CurrentDate = CurrentDate;

            simulationService.PropertyChanged += OnSimulationTick;
            view.BlockClicked += b => selectionService.SelectedBlock = b;

            UpdateVisuals();
        }

        private void OnSimulationTick(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(simulationService.CurrentDate))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDate)));
                ViewCore.CurrentDate = CurrentDate;
                UpdateVisuals();
            }
        }

        private Brush GetColor(string name)
        {
            if (!colorMap.ContainsKey(name))
                colorMap[name] = palette[colorMap.Count % palette.Length];
            return colorMap[name];
        }

        private void UpdateVisuals()
        {
            var blocks = scheduleService.GetAllBlocks();
            var models = arranger.Arrange(blocks, simulationService.CurrentDate);

            Visuals.Clear();
            foreach (var model in models)
            {
                if (model is ModelVisual3D mv3d &&
                    BlockProperties.GetData(mv3d) is Block b)
                {
                    foreach (var child in mv3d.Children)
                    {
                        if (child is BoxVisual3D box)
                        {
                            var color = GetColor(b.Name);
                            box.Material = MaterialHelper.CreateMaterial(color);
                            box.BackMaterial = MaterialHelper.CreateMaterial(color);
                        }
                    }
                }
                Visuals.Add(model);
            }
        }
    }
}
