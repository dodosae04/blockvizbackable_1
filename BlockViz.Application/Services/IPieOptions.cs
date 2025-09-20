using System.ComponentModel;

namespace BlockViz.Applications.Services
{
    /// <summary>
    /// 파이차트 라벨/퍼센트 표시 여부를 공유하는 옵션 서비스
    /// </summary>
    public interface IPieOptions : INotifyPropertyChanged
    {
        bool ShowLabels { get; set; }
    }
}