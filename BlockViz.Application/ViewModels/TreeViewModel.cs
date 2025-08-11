using BlockViz.Applications.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using BlockViz.Applications.Services;
using BlockViz.Domain.Models;
using System.Waf.Applications;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class TreeViewModel : ViewModel<ITreeView>
    {
        private readonly ISelectionService selectionService;

        public Block? SelectedBlock => selectionService.SelectedBlock;

        [ImportingConstructor]
        public TreeViewModel(ITreeView view, ISelectionService selectionService) : base(view)
        {
            this.selectionService = selectionService;
            selectionService.SelectedBlockChanged += (_, _) => RaisePropertyChanged(nameof(SelectedBlock));
        }
    }
}
