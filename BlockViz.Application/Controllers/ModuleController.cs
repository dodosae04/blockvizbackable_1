using System;
using System.ComponentModel.Composition;
using System.Waf.Applications;
using System.Waf.Applications.Services;
using BlockViz.Applications.Controllers;   // ★ 계약 네임스페이스 명시
using BlockViz.Applications.ViewModels;
using BlockViz.Applications.Views;

namespace BlockViz.Applications.Controllers  // ★ 네임스페이스 통일 (Controllers)
{
    /// <summary>
    /// MEF로 Export되는 실제 모듈 컨트롤러.
    /// ShellViewModel에 각 ViewModel(View)을 연결하고 Shell을 띄웁니다.
    /// </summary>
    [Export(typeof(IModuleController))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class ModuleController : IModuleController
    {
        private readonly ShellViewModel shell;
        private readonly RibbonViewModel ribbon;
        private readonly TreeViewModel tree;
        private readonly FactoryViewModel factory;
        private readonly ScheduleViewModel schedule;
        private readonly GanttViewModel gantt;
        private readonly PiViewModel pi;

        [ImportingConstructor]
        public ModuleController(
            ShellViewModel shellViewModel,
            RibbonViewModel ribbonViewModel,
            TreeViewModel treeViewModel,
            FactoryViewModel factoryViewModel,
            ScheduleViewModel scheduleViewModel,
            GanttViewModel ganttViewModel,
            PiViewModel piViewModel)
        {
            shell = shellViewModel ?? throw new ArgumentNullException(nameof(shellViewModel));
            ribbon = ribbonViewModel ?? throw new ArgumentNullException(nameof(ribbonViewModel));
            tree = treeViewModel ?? throw new ArgumentNullException(nameof(treeViewModel));
            factory = factoryViewModel ?? throw new ArgumentNullException(nameof(factoryViewModel));
            schedule = scheduleViewModel ?? throw new ArgumentNullException(nameof(scheduleViewModel));
            gantt = ganttViewModel ?? throw new ArgumentNullException(nameof(ganttViewModel));
            pi = piViewModel ?? throw new ArgumentNullException(nameof(piViewModel));
        }

        public void Initialize()
        {
            // Shell의 ContentPresenter 바인딩 대상에 ViewModel.View 주입
            shell.ContentRibbonView = ribbon.View;
            shell.ContentTreeView = tree.View;
            shell.ContentFactoryView = factory.View;
            shell.ContentScheduleView = schedule.View;
            shell.ContentGanttView = gantt.View;
            shell.ContentPiView = pi.View;
        }

        public void Run()
        {
            // Shell 창 표시
            shell.Show();
        }

        public void Shutdown()
        {
            shell.Close();
        }
    }
}
