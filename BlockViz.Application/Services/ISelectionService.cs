using System;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.Services
{
    public interface ISelectionService
    {
        Block? SelectedBlock { get; set; }
        event EventHandler? SelectedBlockChanged;
    }
}
