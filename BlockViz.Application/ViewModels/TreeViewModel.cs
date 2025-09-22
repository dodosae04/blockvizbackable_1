using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using BlockViz.Applications.Extensions;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using BlockViz.Domain.Models;
using System.Waf.Applications;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class TreeViewModel : ViewModel<ITreeView>
    {
        private readonly ISelectionService selectionService;
        private readonly SimulationService simulationService;

        public ObservableCollection<PropertyNode> SelectedBlockProperties { get; } = new();

        public Block? SelectedBlock => selectionService.SelectedBlock;

        [ImportingConstructor]
        public TreeViewModel(ITreeView view, ISelectionService selectionService, SimulationService simulationService) : base(view)
        {
            this.selectionService = selectionService;
            this.simulationService = simulationService;

            this.selectionService.SelectedBlockChanged += OnSelectedBlockChanged;
            this.simulationService.PropertyChanged += OnSimulationPropertyChanged;

            UpdatePropertyTree();
        }

        private void OnSelectedBlockChanged(object? sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(SelectedBlock));
            UpdatePropertyTree();
        }

        private void OnSimulationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SimulationService.CurrentDate))
            {
                UpdatePropertyTree();
            }
        }

        private void UpdatePropertyTree()
        {
            SelectedBlockProperties.Clear();

            var block = selectionService.SelectedBlock;
            if (block == null)
            {
                return;
            }

            var categories = new PropertyNode?[]
            {
                BuildBasicInfo(block),
                BuildDimensions(block),
                BuildPlacement(block),
                BuildSchedule(block),
                BuildStatus(block)
            };

            foreach (var category in categories)
            {
                if (category != null)
                {
                    SelectedBlockProperties.Add(category);
                }
            }
        }

        private PropertyNode? BuildBasicInfo(Block block)
        {
            var items = new List<PropertyNode?>
            {
                Leaf("Name", block.GetDisplayName()),
                Leaf("Workspace", block.DeployWorkplace.ToString(CultureInfo.CurrentCulture)),
                Leaf("Id", block.BlockID.ToString(CultureInfo.CurrentCulture))
            };

            if (block.NumberOfBlocks > 0)
            {
                items.Add(Leaf("Block Count", block.NumberOfBlocks.ToString(CultureInfo.CurrentCulture)));
            }

            return CategoryIfAny("기본 정보", items);
        }

        private PropertyNode? BuildDimensions(Block block)
        {
            var items = new List<PropertyNode?>
            {
                Leaf("Length", FormatDouble(block.Length, "m")),
                Leaf("Width", FormatDouble(block.Breadth, "m")),
                Leaf("Height", FormatDouble(block.Height, "m"))
            };

            return CategoryIfAny("치수", items);
        }

        private PropertyNode? BuildPlacement(Block block)
        {
            var items = new List<PropertyNode?>
            {
                Leaf("CenterX", FormatDouble(block.X, "m")),
                Leaf("CenterY", FormatDouble(block.Y, "m")),
                Leaf("Direction", FormatDirection(block.Direction))
            };

            return CategoryIfAny("위치/배치", items);
        }

        private PropertyNode? BuildSchedule(Block block)
        {
            var items = new List<PropertyNode?>
            {
                Leaf("StartDate", FormatDate(block.Start)),
                Leaf("EndDate", FormatDate(block.End)),
                LeafIfValue("DueDate", FormatDate(block.Due)),
                LeafIfValue("Duration", GetDurationText(block)),
                LeafIfValue("ProcessingTime", GetProcessingTimeText(block))
            };

            return CategoryIfAny("일정", items);
        }

        private PropertyNode? BuildStatus(Block block)
        {
            if (!HasValidCurrentDate)
            {
                return null;
            }

            var items = new List<PropertyNode?>
            {
                Leaf("기준 날짜", FormatDate(simulationService.CurrentDate)),
                Leaf("IsActive", GetIsActiveText(block)),
                LeafIfValue("Status", GetStatusText(block)),
                LeafIfValue("Progress", GetProgressText(block))
            };

            return CategoryIfAny("상태/진행률", items);
        }

        private bool HasValidCurrentDate
            => simulationService.CurrentDate != DateTime.MinValue && simulationService.CurrentDate != DateTime.MaxValue;

        private static PropertyNode Leaf(string name, string value)
            => new PropertyNode(name, value);

        private static PropertyNode? LeafIfValue(string name, string? value)
            => value is null ? null : new PropertyNode(name, value);

        private static PropertyNode? CategoryIfAny(string name, IEnumerable<PropertyNode?> items)
        {
            var materialized = items.Where(node => node != null)!.Cast<PropertyNode>().ToList();
            return materialized.Count == 0 ? null : new PropertyNode(name, children: materialized);
        }

        private static string FormatDouble(double value, string unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "-";
            }

            var formatted = value.ToString("0.###", CultureInfo.CurrentCulture);
            return string.IsNullOrEmpty(unit) ? formatted : $"{formatted} {unit}";
        }

        private static string FormatDirection(int angle)
        {
            return angle.ToString(CultureInfo.CurrentCulture) + "°";
        }

        private static string FormatDate(DateTime date)
        {
            if (date == DateTime.MinValue || date == DateTime.MaxValue || date == default)
            {
                return "-";
            }

            return date.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
        }

        private static string? FormatDate(DateTime? date)
            => date.HasValue ? FormatDate(date.Value) : null;

        private static string? GetDurationText(Block block)
        {
            var effectiveEnd = block.GetEffectiveEnd();
            if (effectiveEnd.HasValue && effectiveEnd.Value >= block.Start)
            {
                var duration = effectiveEnd.Value - block.Start;
                var days = Math.Max(0.0, duration.TotalDays);
                return $"{days:0.#}일";
            }

            return null;
        }

        private static string? GetProcessingTimeText(Block block)
            => block.ProcessingTime > 0 ? $"{block.ProcessingTime}일" : null;

        private string GetIsActiveText(Block block)
            => block.IsActiveOn(simulationService.CurrentDate) ? "예" : "아니오";

        private string? GetStatusText(Block block)
        {
            var current = simulationService.CurrentDate;

            if (current < block.Start)
            {
                return "미착수";
            }

            var effectiveEnd = block.GetEffectiveEnd();
            if (effectiveEnd.HasValue && current > effectiveEnd.Value)
            {
                return "완료";
            }

            return "진행 중";
        }

        private string? GetProgressText(Block block)
        {
            var effectiveEnd = block.GetEffectiveEnd();
            if (!effectiveEnd.HasValue)
            {
                return null;
            }

            var total = (effectiveEnd.Value - block.Start).TotalDays;
            if (total <= 0)
            {
                return simulationService.CurrentDate >= block.Start ? "100%" : "0%";
            }

            var elapsed = (simulationService.CurrentDate - block.Start).TotalDays;
            var ratio = Math.Clamp(elapsed / total, 0.0, 1.0);
            return $"{ratio * 100:0.#}%";
        }

        private sealed class PropertyNode
        {
            public PropertyNode(string name, string? value = null, IEnumerable<PropertyNode>? children = null)
            {
                Name = name;
                Value = value;
                if (children != null)
                {
                    Children = new ObservableCollection<PropertyNode>(children);
                }
            }

            public string Name { get; }

            public string? Value { get; }

            public ObservableCollection<PropertyNode>? Children { get; }

            public string Display => Value == null ? Name : $"{Name}: {Value}";
        }
    }
}
