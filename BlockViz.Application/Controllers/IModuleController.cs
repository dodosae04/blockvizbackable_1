using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockViz.Applications.Controllers
{
    public interface IModuleController
    {
        void Initialize();
        void Shutdown();
    }
}

