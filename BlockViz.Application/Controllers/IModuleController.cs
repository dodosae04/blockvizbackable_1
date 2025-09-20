using System;

namespace BlockViz.Applications.Controllers
{
    /// <summary>
    /// 앱 부트스트랩을 담당하는 모듈 컨트롤러 계약.
    /// </summary>
    public interface IModuleController
    {
        /// <summary>View/Service 조립 및 바인딩 등 1회 초기화.</summary>
        void Initialize();

        /// <summary>Shell 표시 등 실제 실행.</summary>
        void Run();

        /// <summary>정리/리소스 해제.</summary>
        void Shutdown();
    }
}