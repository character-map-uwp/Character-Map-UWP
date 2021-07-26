using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CharacterMap.Helpers
{
    public class Debouncer
    {
        private DispatcherTimer _timer = null;

        public bool IsActive => _timer != null && _timer.IsEnabled;

        public void Debounce(int milliseconds, Action action)
        {
            _timer?.Stop();
            _timer = null;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(milliseconds)
            };

            _timer.Tick += (s, e) =>
            {
                if (_timer == null)
                    return;

                _timer?.Stop();
                _timer = null;
                action();
            };


            _timer.Start();
        }


        public void Cancel()
        {
            if (_timer is not null && _timer.IsEnabled)
            {
                _timer?.Stop();
                _timer = null;
            }
        }
    }
}
