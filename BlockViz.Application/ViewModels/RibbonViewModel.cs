// ✅ 수정된 RibbonViewModel.cs
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Input;
using BlockViz.Applications.Services;
using BlockViz.Applications.Views;
using Microsoft.Win32;
using System.Waf.Applications;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class RibbonViewModel : ViewModel<IRibbonView>
    {
        private readonly IExcelImportService excelLoader;
        private readonly IScheduleService scheduleService;
        private readonly SimulationService simulationService;

        public ICommand NewCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand Speed2Command { get; }
        public ICommand Speed5Command { get; }
        public ICommand Speed10Command { get; }

        [ImportingConstructor]
        public RibbonViewModel(
            IRibbonView view,
            IExcelImportService excelLoader,
            IScheduleService scheduleService,
            SimulationService simulationService
        ) : base(view)
        {
            this.excelLoader = excelLoader;
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;

            NewCommand = new DelegateCommand(ExecuteNew);
            PauseCommand = new DelegateCommand(simulationService.Pause);
            ResumeCommand = new DelegateCommand(simulationService.Resume);
            Speed2Command = new DelegateCommand(() => simulationService.SetSpeed(2));
            Speed5Command = new DelegateCommand(() => simulationService.SetSpeed(5));
            Speed10Command = new DelegateCommand(() => simulationService.SetSpeed(10));
        }

        private void ExecuteNew()
        {
            var dlg = new OpenFileDialog { Filter = "CSV|*.csv|Excel|*.xlsx;*.xls" };
            if (dlg.ShowDialog() != true) return;

            var blocks = excelLoader.Load(dlg.FileName)
                .Where(b => b.DeployWorkplace >= 1 && b.DeployWorkplace <= 6) // 유효한 작업장만
                .Where(b => b.Start < b.End) // 날짜 유효성 검증
                .ToList();

            if (!blocks.Any()) return;

            scheduleService.SetAllBlocks(blocks);

            var start = blocks.Min(b => b.Start);
            simulationService.Start(start);
        }
    }
}
