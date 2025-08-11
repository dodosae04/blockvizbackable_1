using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Waf.Applications;
using BlockViz.Applications.Views;

namespace BlockViz.Applications.Views
{
    public interface IShellView : IView
    {
        void Show();
        void Close();
    }
}
