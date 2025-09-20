using System;

namespace BlockViz.Applications.Views
{
    public sealed class WorkplaceFilterRequestedEventArgs : EventArgs
    {
        public WorkplaceFilterRequestedEventArgs(int? workplaceId)
        {
            WorkplaceId = workplaceId;
        }

        /// <summary>
        /// null이면 기본 보기(모든 작업장), 값이 있으면 해당 작업장만 표시합니다.
        /// </summary>
        public int? WorkplaceId { get; }
    }
}
