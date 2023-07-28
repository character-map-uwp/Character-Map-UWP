using System;

namespace CharacterMap.Helpers
{
    public class Throttler
    {
        private DateTimeOffset _lastFired = DateTimeOffset.MinValue;

        public void Throttle(int milliseconds, Action action)
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastFired).TotalMilliseconds > milliseconds)
            {
                action?.Invoke();
                _lastFired = now;
            }
        }
    }
}
