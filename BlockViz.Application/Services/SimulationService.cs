using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Threading;

namespace BlockViz.Applications.Services
{
    /// <summary>
    /// 시뮬레이션 타임라인 서비스 (기존 유지 + 되감기 추가)
    /// </summary>
    [Export]
    public class SimulationService : INotifyPropertyChanged
    {
        private readonly DispatcherTimer timer;
        private double speedMagnitude = 1.0; // 1초당 진행 일수(절대값)
        private int direction = +1;          // +1: 정방향, -1: 되감기
        private DateTime current;

        public event PropertyChangedEventHandler PropertyChanged;

        public SimulationService()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; // 기존 1초 1틱
            timer.Tick += (_, __) => Tick();
        }

        public DateTime CurrentDate
        {
            get => current;
            private set
            {
                if (current == value) return;
                current = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDate)));
            }
        }

        /// <summary>데이터 로드 후 최초 시작일 지정(기존 기능)</summary>
        public void Start(DateTime startDate)
        {
            CurrentDate = startDate;
            direction = +1;
            timer.Start();
        }

        public void Pause() => timer.Stop();

        /// <summary>정방향 재생(기존 Resume 의미 유지)</summary>
        public void Resume()
        {
            direction = +1;
            timer.Start();
        }

        /// <summary>되감기: 같은 속도로 과거로</summary>
        public void Rewind()
        {
            direction = -1;
            timer.Start();
        }

        /// <summary>속도 배율(2/5/10 등, 기존과 동일)</summary>
        public void SetSpeed(double mul)
        {
            if (mul <= 0) mul = 1;
            speedMagnitude = mul;
            if (!timer.IsEnabled) timer.Start();
        }

        private void Tick()
        {
            var days = direction * speedMagnitude;    // ✅ 속도는 같고, 방향만 반대
            CurrentDate = CurrentDate.AddDays(days);
        }
    }
}
