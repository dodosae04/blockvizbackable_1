using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Threading;

namespace BlockViz.Applications.Services
{
    [Export]
    public class SimulationService : INotifyPropertyChanged
    {
        private readonly DispatcherTimer timer;
        private double speed = 1;   
        private DateTime current;

        public event PropertyChangedEventHandler PropertyChanged;

        public SimulationService()
        {
            timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(500),
                DispatcherPriority.Background,
                (_, _) => Tick(),
                Dispatcher.CurrentDispatcher
            );
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

        public void Start(DateTime startDate, double initSpeed = 1)
        {
            CurrentDate = startDate;
            speed = initSpeed;
            timer.Start();
        }

        public void Pause() => timer.Stop();

        public void Resume() => timer.Start();

        public void SetSpeed(double mul)
        {
            speed = mul;
            if (!timer.IsEnabled) timer.Start();
        }

        private void Tick() => CurrentDate = CurrentDate.AddDays(speed);
    }
}