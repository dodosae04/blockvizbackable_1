// BlockViz.Applications\Views\IGanttView.cs
using System;
using System.Waf.Applications;
using BlockViz.Domain.Models;
using PlotModel = OxyPlot.PlotModel;

namespace BlockViz.Applications.Views
{
    public interface IGanttView : IView
    {
        PlotModel GanttModel { get; set; }

        event EventHandler<WorkplaceFilterRequestedEventArgs> WorkplaceFilterRequested;

        event EventHandler<bool> ExpandAllChanged;

        event Action<Block> BlockClicked;

        /// <summary>
        /// UI 버튼 상태를 동기화하기 위해 현재 선택된 작업장을 설정합니다.
        /// null이면 기본 보기(모든 작업장)를 의미합니다.
        /// </summary>
        void SetActiveWorkplace(int? workplaceId);

        void SetExpandAllState(bool isExpanded);
    }
}
