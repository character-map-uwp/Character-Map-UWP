using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.System;
using System.Text;
using Windows.UI.Xaml;

namespace CharacterMap.Services
{
    /// <summary>
    /// A simple drop-in service to monitor the life-cycle of items
    /// </summary>
    public static class LeakTrackingService
    {
        static List<(string, WeakReference)> _pool { get; } = new List<(string, WeakReference)>();

        /// <summary>
        /// Timespan between checking which items are alive
        /// This *must* be set before calling <see cref="TryActivate"/>.
        /// 
        /// </summary>
        public static TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Logging function. If not set, writes to Debug console
        /// </summary>
        public static Action<String> Log { get; set; }

        private static StringBuilder _sb { get; } = new StringBuilder();

        private static bool _isActive = false;
        private static long _objectid = 0;
        private static ulong _prevMem = 0;

        static LeakTrackingService()
        {
            if (Log == null)
                Log = s => Debug.WriteLine(s);

            TryActivate();
        }

        static void TryActivate()
        {
            if (_isActive)
                return;

            if (Debugger.IsAttached)
            {
                DispatcherTimer t = new() { Interval = Interval };

                t.Tick += (s, e) => CheckForLeaks();
                t.Start();
                _isActive = true;
            }
        }

        /// <summary>
        /// This method is automatically called every 10 seconds. 
        /// Only call manually if you have an actual purpose for doing so.
        /// </summary>
        public static void CheckForLeaks()
        {
            _sb.AppendLine("\n*********************************");
            _sb.AppendLine("*** START LEAK TRACKING ********\n\n");

            GC.Collect();
            ProcessMemoryReport report = MemoryManager.GetProcessMemoryReport();

            foreach ((string key, WeakReference reference) entry in _pool.ToList())
            {
                var weakRef = entry.reference;
                bool isAlive = weakRef.IsAlive || weakRef.Target != null;
                if (!isAlive)
                    _pool.Remove(entry);

                _sb.AppendLine(String.Format("{0} : {1}", entry.key, isAlive ? "Alive" : "Collected"));
            }

            _sb.AppendLine($"\nMemory Delta: {((long)report.PrivateWorkingSetUsage - (long)_prevMem) / (1024 * 1024)} MB");
            _sb.AppendLine($"Current Private Working Set usage: {(report.PrivateWorkingSetUsage / 1024) / 1024} MB out of {(MemoryManager.AppMemoryUsageLimit / 1024) / 1024} MB");
            _sb.AppendLine("*** END LEAK TRACKING ********");
            _sb.AppendLine("*********************************");
            _sb.AppendLine();

            Log(_sb.ToString());

            _sb.Clear();
            _prevMem = report.PrivateWorkingSetUsage;
        }

        /// <summary>
        /// Registers an object for LeakTracking
        /// </summary>
        /// <param name="obj"></param>
        public static void Register(object obj)
        {
            if ((!Debugger.IsAttached) || obj == null)
                return;

            string key = obj.GetType().Name;
            _objectid++;
            if (_objectid > Int32.MaxValue)
                _objectid = 0;
            long id = _objectid;

            string actualKey = string.Format("{0}-{1}", id, key);
            _pool.Add((actualKey, new WeakReference(obj)));

            Log(String.Format("LeakTrackingService: Added {0} as {1}", key, actualKey));

            TryActivate();
        }
    }
}
