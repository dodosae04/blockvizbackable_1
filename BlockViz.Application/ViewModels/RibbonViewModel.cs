// ✅ 기존 기능 유지 + "되감기" 커맨드만 추가
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Input;
using BlockViz.Applications.Extensions;
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
        private readonly IBlockColorService colorService;

        public ICommand NewCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand RewindCommand { get; } // ⬅️ 추가
        public ICommand Speed2Command { get; }
        public ICommand Speed5Command { get; }
        public ICommand Speed10Command { get; }

        // (기존) 파이 라벨/퍼센트 표시 토글
        [Import(AllowDefault = true)]
        public IPieOptions PieOptions { get; set; }

        public bool ShowPieLabels
        {
            get => PieOptions != null && PieOptions.ShowLabels;
            set
            {
                if (PieOptions == null) return;
                if (PieOptions.ShowLabels == value) return;
                PieOptions.ShowLabels = value;
                RaisePropertyChanged(nameof(ShowPieLabels));
            }
        }

        [ImportingConstructor]
        public RibbonViewModel(
            IRibbonView view,
            IExcelImportService excelLoader,
            IScheduleService scheduleService,
            SimulationService simulationService,
            IBlockColorService colorService
        ) : base(view)
        {
            this.excelLoader = excelLoader;
            this.scheduleService = scheduleService;
            this.simulationService = simulationService;
            this.colorService = colorService;

            NewCommand = new DelegateCommand(ExecuteNew);
            PauseCommand = new DelegateCommand(simulationService.Pause);
            ResumeCommand = new DelegateCommand(simulationService.Resume);
            RewindCommand = new DelegateCommand(simulationService.Rewind); // ⬅️ 추가
            Speed2Command = new DelegateCommand(() => simulationService.SetSpeed(2));
            Speed5Command = new DelegateCommand(() => simulationService.SetSpeed(5));
            Speed10Command = new DelegateCommand(() => simulationService.SetSpeed(10));
        }

        private void ExecuteNew()
        {
            var dlg = new OpenFileDialog { Filter = "CSV|*.csv|Excel|*.xlsx;*.xls" };
            if (dlg.ShowDialog() != true) return;

            var blocks = excelLoader.Load(dlg.FileName)
                .Where(b => b.DeployWorkplace >= 1 && b.DeployWorkplace <= 6)
                .ToList();
            if (!blocks.Any()) return;

            colorService.Reset();
            scheduleService.SetAllBlocks(blocks);
            var rangeStart = blocks.Min(b => b.Start);
            var rangeEnd = blocks.Select(b => b.GetEffectiveEnd() ?? b.Start).Max();
            if (rangeEnd <= rangeStart)
                rangeEnd = rangeStart.AddDays(1);
            simulationService.Start(rangeStart, rangeEnd);
        }
    }

    // (프로젝트에 이미 DelegateCommand 있으면 아래는 무시해도 됩니다)
    public sealed class DelegateCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;
        private readonly Action<object> executeWithParam;
        private readonly Func<object, bool> canExecuteWithParam;

        public DelegateCommand(Action execute, Func<bool> canExecute = null)
        { this.execute = execute; this.canExecute = canExecute; }

        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = null)
        { executeWithParam = execute; canExecuteWithParam = canExecute; }

        public bool CanExecute(object parameter)
            => execute != null ? (canExecute?.Invoke() ?? true)
                               : (canExecuteWithParam?.Invoke(parameter) ?? true);

        public void Execute(object parameter)
        { if (execute != null) execute(); else executeWithParam?.Invoke(parameter); }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
