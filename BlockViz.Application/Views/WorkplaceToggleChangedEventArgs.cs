using System;

namespace BlockViz.Applications.Views
{
    public class WorkplaceToggleChangedEventArgs : EventArgs
    {
        public WorkplaceToggleChangedEventArgs(int workplaceId, bool isExpanded)
        {
            WorkplaceId = workplaceId;
            IsExpanded = isExpanded;
        }

        public int WorkplaceId { get; }

        public bool IsExpanded { get; }
    }
}

