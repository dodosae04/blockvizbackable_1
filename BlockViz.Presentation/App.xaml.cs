using System;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Waf.Applications.Services;
using System.Windows;
using BlockViz.Applications.Controllers;   // ★ 우리 계약
using BlockViz.Applications.Services;
using BlockViz.Applications.ViewModels;

namespace BlockViz.Presentation
{
    public partial class App : Application
    {
        private CompositionContainer container;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // === MEF 카탈로그: 필요한 어셈블리만 명시 ===
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));                 // Presentation
            catalog.Catalogs.Add(new AssemblyCatalog(typeof(ShellViewModel).Assembly));                 // Applications (VM)
            catalog.Catalogs.Add(new AssemblyCatalog(typeof(IMessageService).Assembly));                // Applications (Services)

            container = new CompositionContainer(catalog, CompositionOptions.DisableSilentRejection);

            try
            {
                // ★ 우리 인터페이스로 컨트롤러 수집
                var controllers = container.GetExportedValues<IModuleController>().ToArray();

                if (controllers.Length == 0)
                {
                    // 안전장치: 컨트롤러가 없을 때는 Shell만 직접 띄움
                    var shellVm = container.GetExportedValue<ShellViewModel>();
                    var shellWindow = (Window)shellVm.View;
                    shellWindow.DataContext = shellVm;
                    shellWindow.Show();
                    return;
                }

                // 정상 경로: Initialize → Run
                foreach (var c in controllers) c.Initialize();
                foreach (var c in controllers) c.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
    }
}
