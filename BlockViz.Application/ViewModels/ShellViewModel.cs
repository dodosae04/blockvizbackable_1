using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Waf.Applications;
using BlockViz.Applications.Views;

namespace BlockViz.Applications.ViewModels
{
    [Export]
    public class ShellViewModel : ViewModel<IShellView>
    {
        [ImportingConstructor]
        public ShellViewModel(IShellView view) : base(view)
        {
        }

        public void Show()
        {
            ViewCore.Show();
        }

        public void Close()
        {
            ViewCore.Close();
        }

        private object _contentRibbonView;
        public object ContentRibbonView
        {
            get { return _contentRibbonView; }
            set { SetProperty(ref _contentRibbonView, value); }
        }

        private object _contentTreeView;
        public object ContentTreeView
        {
            get { return _contentTreeView; }
            set { SetProperty(ref _contentTreeView, value); }
        }

        private object _contentFactoryView;
        public object ContentFactoryView
        {
            get { return _contentFactoryView; }
            set { SetProperty(ref _contentFactoryView, value); }
        }

        private object _contentGanttView;
        public object ContentGanttView
        {
            get { return _contentGanttView; }
            set { SetProperty(ref _contentGanttView, value); }
        }

        private object _contentScheduleView;
        public object ContentScheduleView
        {
            get { return _contentScheduleView; }
            set { SetProperty(ref _contentScheduleView, value); }
        }

        private object _contentPiView;
        public object ContentPiView
        {
            get { return _contentPiView; }
            set { SetProperty(ref _contentPiView, value); }
        }


    }
}
