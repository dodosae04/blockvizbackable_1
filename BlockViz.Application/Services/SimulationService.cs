using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Threading;

namespace BlockViz.Applications.Services
{
    /// <summary>
    /// 시뮬레이션 타임라인 서비스 (슬라이더/재생 제어와 동기화)
    /// </summary>
    [Export]
    public class SimulationService : INotifyPropertyChanged
    {
        private readonly DispatcherTimer timer;
        private double speedMagnitude = 1.0; // 1초당 진행 일수(절대값)
        private int direction = +1;          // +1: 정방향, -1: 되감기
        private DateTime current = DateTime.MinValue;
        private DateTime rangeStart = DateTime.MinValue;
        private DateTime rangeEnd = DateTime.MinValue;

        public event PropertyChangedEventHandler? PropertyChanged;

        public SimulationService()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, __) => Tick();
        }

        public DateTime CurrentDate
        {
            get => current;
            private set
            {
                var clamped = Clamp(value);
                if (current == clamped) return;
                current = clamped;
                RaisePropertyChanged(nameof(CurrentDate));
            }
        }

        public DateTime RangeStart
        {
            get => rangeStart;
            private set
            {
                if (rangeStart == value) return;
                rangeStart = value;
                RaisePropertyChanged(nameof(RangeStart));
            }
        }

        public DateTime RangeEnd
        {
            get => rangeEnd;
            private set
            {
                if (rangeEnd == value) return;
                rangeEnd = value;
                RaisePropertyChanged(nameof(RangeEnd));
            }
        }

        public bool IsRunning => timer.IsEnabled;

        /// <summary>
        /// 데이터 로드 후 타임라인 범위를 설정하고 즉시 재생을 시작합니다.
        /// </summary>
        public void Start(DateTime startDate, DateTime endDate)
        {
            timer.Stop();
            SetRange(startDate, endDate);
            direction = +1;
            speedMagnitude = 1.0;
            CurrentDate = RangeStart;
            if (HasValidRange)
            {
                timer.Start();
                RaisePropertyChanged(nameof(IsRunning));
            }
        }

        public void Pause()
        {
            if (!timer.IsEnabled) return;
            timer.Stop();
            RaisePropertyChanged(nameof(IsRunning));
        }

        /// <summary>정방향 재생</summary>
        public void Resume()
        {
            direction = +1;
            if (!timer.IsEnabled && HasValidRange)
            {
                timer.Start();
                RaisePropertyChanged(nameof(IsRunning));
            }
        }

        /// <summary>되감기: 같은 속도로 과거로</summary>
        public void Rewind()
        {
            direction = -1;
            if (!timer.IsEnabled && HasValidRange)
            {
                timer.Start();
                RaisePropertyChanged(nameof(IsRunning));
            }
        }

        /// <summary>속도 배율(2/5/10 등)</summary>
        public void SetSpeed(double mul)
        {
            if (mul <= 0) mul = 1;
            speedMagnitude = mul;
            if (!timer.IsEnabled && HasValidRange)
            {
                timer.Start();
                RaisePropertyChanged(nameof(IsRunning));
            }
        }

        /// <summary>슬라이더에서 직접 현재 시점을 변경.</summary>
        public void MoveTo(DateTime date)
        {
            CurrentDate = date;
        }

        public TimeSpan Range => HasValidRange ? RangeEnd - RangeStart : TimeSpan.Zero;

        private bool HasValidRange => RangeStart != DateTime.MinValue && RangeEnd != DateTime.MinValue;

        private void Tick()
        {
            if (!HasValidRange)
            {
                Pause();
                return;
            }

            var days = direction * speedMagnitude;
            var next = CurrentDate.AddDays(days);
            var clamped = Clamp(next);

            bool hitBoundary = (direction > 0 && clamped >= RangeEnd && next > RangeEnd)
                || (direction < 0 && clamped <= RangeStart && next < RangeStart);

            CurrentDate = clamped;

            if (hitBoundary)
            {
                Pause();
            }
        }

        private void SetRange(DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
            {
                endDate = startDate;
            }

            RangeStart = startDate;
            RangeEnd = endDate;
        }

        private DateTime Clamp(DateTime date)
        {
            if (HasValidRange)
            {
                if (date < RangeStart) return RangeStart;
                if (date > RangeEnd) return RangeEnd;
            }
            return date;
        }

        private void RaisePropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
