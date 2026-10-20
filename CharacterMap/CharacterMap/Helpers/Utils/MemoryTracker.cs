using System.Diagnostics;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;

namespace CharacterMap.Diagnostics;

/// <summary>
/// Lightweight memory sampling logger.
/// Results written to LocalFolder\memory_trace.tsv.
/// </summary>
internal static class MemoryTracker
{
    static DispatcherTimer _timer;
    static StorageFile _file;
    static Stopwatch _sw = new();
    static bool _running;
    static int _sampleIndex;
    static string _currentLabel = string.Empty;

    public static async Task StartAsync(string label = "")
    {
        if (_running)
            return;

        _running = true;
        _sampleIndex = 0;
        _currentLabel = label;
        _sw.Restart();

        _file = await ApplicationData.Current.LocalFolder
            .CreateFileAsync("memory_trace.tsv", CreationCollisionOption.ReplaceExisting);

        await AppendAsync("Index\tElapsed_ms\tLabel\tAppMemory_MB\tManagedHeap_MB\tProcessHeap_MB\tSurface_MB\tSurface_Count\tDWrite_DX_MB\tAppMemoryLimit_MB");
        await SampleAsync("START:" + label);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (s, e) => await SampleAsync(_currentLabel);
        _timer.Start();

        Debug.WriteLine($"[MemoryTracker] Started -> {_file.Path}");
    }

    /// <summary>Pin a named event into the log without waiting for the next timer tick.</summary>
    public static async Task SnapshotAsync(string label)
    {
        if (!_running) return;
        await SampleAsync(label);
    }

    public static async Task StopAsync()
    {
        if (!_running) return;

        _timer?.Stop();
        _timer = null;
        _running = false;
        await SampleAsync("STOP");
        _sw.Stop();

        Debug.WriteLine($"[MemoryTracker] Done -> {_file?.Path}");
    }

    static async Task SampleAsync(string label)
    {
        try
        {
            // Total app private bytes
            ulong appBytes   = MemoryManager.AppMemoryUsage;
            ulong limitBytes = MemoryManager.AppMemoryUsageLimit;

            // Managed GC heap
            long gcBytes = GC.GetTotalMemory(forceFullCollection: false);

            // C++ Default Process Heap (our code, stl vectors, objects)
            ulong heapBytes = CharacterMapCX.CompositionDeviceManager.GetProcessHeapUsage();

            // FontGlyphs composition drawing surfaces
            int surfaceCount = CharacterMapCX.CompositionDeviceManager.GetActiveSurfaceCount();
            ulong surfaceBytes = CharacterMapCX.CompositionDeviceManager.GetActiveSurfaceMemoryBytes();

            double appMB     = appBytes     / 1_048_576.0;
            double gcMB      = gcBytes      / 1_048_576.0;
            double heapMB    = heapBytes    / 1_048_576.0;
            double surfaceMB = surfaceBytes / 1_048_576.0;

            // Remainder: DirectWrite internal caches (outlines/color bitmaps) + Direct2D device caches + graphics driver
            double dwriteDxMB = Math.Max(0, appMB - gcMB - heapMB - surfaceMB);
            double limitMB    = limitBytes   / 1_048_576.0;

            string line = $"{_sampleIndex++}\t{_sw.ElapsedMilliseconds}\t{label}\t{appMB:F1}\t{gcMB:F1}\t{heapMB:F1}\t{surfaceMB:F1}\t{surfaceCount}\t{dwriteDxMB:F1}\t{limitMB:F0}";
            await AppendAsync(line);
            Debug.WriteLine($"[MemoryTracker] {line}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MemoryTracker] error: {ex.Message}");
        }
    }

    static async Task AppendAsync(string line)
    {
        if (_file is null) return;
        try { await FileIO.AppendTextAsync(_file, line + "\r\n"); }
        catch { }
    }

    public static string OutputPath => _file?.Path;
}
