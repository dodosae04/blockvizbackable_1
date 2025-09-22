// ✅ 수정된 ScheduleViewModel.cs - BlockName 기준 색상 일관성 적용
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Media.Media3D;
using BlockViz.Applications.Extensions;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using System.Waf.Applications;
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
        private readonly IBlockColorService colorService;

        private static readonly Color OutlineColorDefault = Colors.Black;
        private static readonly Color OutlineColorSelected = Colors.Gold;
        private const double OutlineThicknessDefault = 1.2;
        private const double OutlineThicknessSelected = 2.8;
        private const double HighlightLightenFactor = 0.35;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Visual3D> Visuals { get; }
        public DateTime CurrentDate => simulationService.CurrentDate;

        [ImportingConstructor]
        public ScheduleViewModel(
            IScheduleView view,
            IScheduleService scheduleService,
            ScheduleArrangementService arranger,
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
            selectionService.SelectedBlockChanged += OnSelectedBlockChanged;
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

            var selected = selectionService.SelectedBlock;

            Visuals.Clear();
            foreach (var model in models)
            {
                if (model is ModelVisual3D mv3d)
                {
                    var block = BlockProperties.GetData(mv3d);
                    if (block != null)
                    {
                        ApplyBlockAppearance(mv3d, block, ReferenceEquals(block, selected));
                    }
                }

                Visuals.Add(model);
            }
        }

        private void OnSelectedBlockChanged(object? sender, EventArgs e)
            => UpdateSelectionAppearance();

        private void UpdateSelectionAppearance()
        {
            var selected = selectionService.SelectedBlock;

            foreach (var visual in Visuals)
            {
                if (visual is not ModelVisual3D mv3d)
                {
                    continue;
                }

                var block = BlockProperties.GetData(mv3d);
                if (block == null)
                {
                    continue;
                }

                ApplyBlockAppearance(mv3d, block, ReferenceEquals(block, selected));
            }
        }

        private void ApplyBlockAppearance(ModelVisual3D visual, Block block, bool isSelected)
        {
            var brush = GetBrushForBlock(block, isSelected);
            var material = MaterialHelper.CreateMaterial(brush);

            foreach (var child in visual.Children)
            {
                switch (child)
                {
                    case BoxVisual3D box:
                        box.Material = material;
                        box.BackMaterial = material;
                        break;
                    case Visual3D nested:
                        UpdateOutlineVisual(nested, isSelected);
                        break;
                }
            }
        }

        private SolidColorBrush GetBrushForBlock(Block block, bool isSelected)
        {
            var displayName = block.GetDisplayName();

            if (!isSelected)
            {
                return colorService.GetBrush(displayName);
            }

            var baseColor = colorService.GetColor(displayName);
            var highlight = Lighten(baseColor, HighlightLightenFactor);
            return CreateFrozenBrush(highlight);
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color Lighten(Color color, double factor)
        {
            factor = Math.Clamp(factor, 0.0, 1.0);

            byte Lerp(byte component)
            {
                var value = component + (255 - component) * factor;
                if (value < 0) value = 0;
                if (value > 255) value = 255;
                return (byte)Math.Round(value);
            }

            return Color.FromRgb(Lerp(color.R), Lerp(color.G), Lerp(color.B));
        }

        private static void UpdateOutlineVisual(Visual3D visual, bool isSelected)
        {
            switch (visual)
            {
                case LinesVisual3D lines:
                    lines.Color = isSelected ? OutlineColorSelected : OutlineColorDefault;
                    lines.Thickness = isSelected ? OutlineThicknessSelected : OutlineThicknessDefault;
                    break;
                case ModelVisual3D mv3d:
                    foreach (var child in mv3d.Children)
                    {
                        UpdateOutlineVisual(child, isSelected);
                    }
                    break;
            }
        }
    }
}
