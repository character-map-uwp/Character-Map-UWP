using Windows.UI.Xaml;

namespace CharacterMap.Helpers;

public class Debouncer
{
    private DispatcherTimer _timer = null;

    public bool IsActive => _timer != null && _timer.IsEnabled;

    /// <summary>
    /// Default debounce internal in milliseconds
    /// </summary>
    public int DefaultDebounceInterval { get; set; }

    public Debouncer() { }
    public Debouncer(int defaultIntervalMs) 
    {
        DefaultDebounceInterval = defaultIntervalMs;
    }

    public void Debounce(Action action) => Debounce(DefaultDebounceInterval, action);

    public void Debounce(int milliseconds, Action action)
    {
        _timer?.Stop();
        _timer = null;

        if (milliseconds > 0)
        {
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
        else
        {
            action();
        }
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
