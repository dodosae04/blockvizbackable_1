// ✅ 수정본: ToolTipService 네임스페이스 추가 + 블록 Tooltip/색상 일관화
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Controls;                   // ★ ToolTipService
using System.Windows.Media.Media3D;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using System.Waf.Applications;
using System.Windows;
using System.Windows.Media;
using HelixToolkit.Wpf;
using BlockViz.Applications.Models;
using BlockViz.Domain.Models;
using BlockViz.Applications.Extensions;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class FactoryViewModel : ViewModel<IFactoryView>, INotifyPropertyChanged
    {
        private readonly IScheduleService scheduleService;
        private readonly IBlockArrangementService arranger;
        private readonly SimulationService simulationService;
        private readonly ISelectionService selectionService;
        private readonly IBlockColorService colorService;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Visual3D> Visuals { get; }
        public DateTime CurrentDate => simulationService.CurrentDate;

        [ImportingConstructor]
        public FactoryViewModel(
            IFactoryView view,
            IScheduleService scheduleService,
            IBlockArrangementService arranger,
            SimulationService simulationService,
            ISelectionService selectionService,
            IBlockColorService colorService
        ) : base(view)
        {
            this.scheduleService = scheduleService;
            this.arranger = arranger;
            this.simulationService = simulationService;
            this.selectionService = selectionService;
            this.colorService = colorService;

            Visuals = new ObservableCollection<Visual3D>();
            view.Visuals = Visuals;
            view.CurrentDate = CurrentDate;

            simulationService.PropertyChanged += OnSimulationPropertyChanged;
            view.BlockClicked += b => selectionService.SelectedBlock = b;
            view.TimelineValueChanged += OnTimelineValueChanged;

            if (simulationService.RangeStart != DateTime.MinValue && simulationService.RangeEnd != DateTime.MinValue)
            {
                ViewCore.ConfigureTimeline(simulationService.RangeStart, simulationService.RangeEnd);
            }

            UpdateVisuals();
        }

        private void OnSimulationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(simulationService.CurrentDate))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDate)));
                ViewCore.CurrentDate = CurrentDate;
                UpdateVisuals();
            }
            else if (e.PropertyName == nameof(simulationService.RangeStart) || e.PropertyName == nameof(simulationService.RangeEnd))
            {
                if (simulationService.RangeStart != DateTime.MinValue && simulationService.RangeEnd != DateTime.MinValue)
                {
                    ViewCore.ConfigureTimeline(simulationService.RangeStart, simulationService.RangeEnd);
                }
            }
        }

        private void OnTimelineValueChanged(object? sender, double days)
        {
            if (simulationService.RangeStart == DateTime.MinValue) return;
            simulationService.MoveTo(simulationService.RangeStart.AddDays(days));
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
                    var displayName = b.GetDisplayName();
                    foreach (var child in mv3d.Children)
                    {
                        if (child is BoxVisual3D box)
                        {
                            var brush = colorService.GetBrush(b.Name);
                            var material = MaterialHelper.CreateMaterial(brush);
                            box.Material = material;
                            box.BackMaterial = material;

                            // ★ MouseOver 툴팁
                            ToolTipService.SetToolTip(box, displayName);
                            ToolTipService.SetInitialShowDelay(box, 0);
                            ToolTipService.SetBetweenShowDelay(box, 0);
                            ToolTipService.SetShowDuration(box, int.MaxValue);
                        }
                    }
                }
                Visuals.Add(model);
            }
        }
    }
}
