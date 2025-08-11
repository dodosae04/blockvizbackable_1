using System;
using System.ComponentModel.Composition;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.Services
{
    [Export(typeof(ISelectionService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class SelectionService : ISelectionService
    {
        private Block? selected;
        public event EventHandler? SelectedBlockChanged;

        public Block? SelectedBlock
        {
            get => selected;
            set
            {
                if (!Equals(selected, value))
                {
                    selected = value;
                    SelectedBlockChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}
