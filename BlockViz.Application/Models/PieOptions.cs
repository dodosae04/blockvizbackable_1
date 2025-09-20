using System.ComponentModel;
using System.ComponentModel.Composition;
using BlockViz.Applications.Services;

namespace BlockViz.Applications.Models
{
    [Export(typeof(IPieOptions))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class PieOptions : IPieOptions
    {
        private bool showLabels; // false: 숨김(기본), true: 라벨+퍼센트 표시

        public bool ShowLabels
        {
            get => showLabels;
            set
            {
                if (showLabels != value)
                {
                    showLabels = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLabels)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}