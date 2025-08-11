using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Waf.Applications;
using BlockViz.Applications.ViewModels;
using BlockViz.Applications.Views;
using System.Waf.Applications.Services;

namespace BlockViz.Applications.Controller
{
    [Export(typeof(IModuleController)), Export]
    public class ModuleController : IModuleController
    {
        private readonly RibbonViewModel _ribbonViewModel;
        private readonly TreeViewModel _treeViewModel;
        private readonly FactoryViewModel _factoryViewModel;
        private readonly ScheduleViewModel _scheduleViewModel;
        private readonly GanttViewModel _ganttViewModel;
        private readonly PiViewModel _piViewModel;
        private readonly ShellViewModel _shellViewModel;

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
            _shellViewModel = shellViewModel ?? throw new ArgumentNullException(nameof(shellViewModel));
            _ribbonViewModel = ribbonViewModel ?? throw new ArgumentNullException(nameof(ribbonViewModel));
            _treeViewModel = treeViewModel ?? throw new ArgumentNullException(nameof(treeViewModel));
            _factoryViewModel = factoryViewModel ?? throw new ArgumentNullException(nameof(factoryViewModel));
            _scheduleViewModel = scheduleViewModel ?? throw new ArgumentNullException(nameof(scheduleViewModel));
            _ganttViewModel = ganttViewModel ?? throw new ArgumentNullException(nameof(ganttViewModel));
            _piViewModel = piViewModel ?? throw new ArgumentNullException(nameof(piViewModel));
        }

        public void Initialize()
        {
            _shellViewModel.ContentRibbonView = _ribbonViewModel.View;
            _shellViewModel.ContentTreeView = _treeViewModel.View;
            _shellViewModel.ContentFactoryView = _factoryViewModel.View;
            _shellViewModel.ContentScheduleView = _scheduleViewModel.View;
            _shellViewModel.ContentGanttView = _ganttViewModel.View;
            _shellViewModel.ContentPiView = _piViewModel.View;
        }

        public void Run()
        {
            _shellViewModel.Show();
        }

        public void Shutdown()
        {
            _shellViewModel.Close();
        }
    }
}
