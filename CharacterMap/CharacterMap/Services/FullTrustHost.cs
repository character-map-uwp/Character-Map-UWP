using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;

namespace CharacterMap.Services;

/// <summary>
/// Plumbing for spawning and hosting a lightweight FullTrust background instance
/// of CharacterMap itself (launched with <see cref="LaunchArgument"/>).
/// </summary>
public static class FullTrustHost
{
    public const string LaunchArgument = "--fulltrust";
    public const string ParameterGroupId = "FullTrustHost";

    private const string MutexName = @"Local\CharacterMap_FullTrustHost_Mutex";
    private const string StopEventName = @"Local\CharacterMap_FullTrustHost_Stop";

    public static bool IsSupported =>
        ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0);

    /// <summary>
    /// Spawns the FullTrust background instance of CharacterMap from the UWP app process.
    /// </summary>
    public static async Task<bool> LaunchAsync()
    {
        if (!IsSupported)
            return false;

        await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync(ParameterGroupId);
        return true;
    }

    /// <summary>
    /// Signals a running FullTrust background instance to shut down cleanly.
    /// </summary>
    public static void RequestShutdown()
    {
        if (EventWaitHandle.TryOpenExisting(StopEventName, out EventWaitHandle stopEvent))
            using (stopEvent)
                stopEvent.Set();
    }

    /// <summary>
    /// Entry point for the FullTrust background process (runs without XAML/UI).
    /// </summary>
    public static void Run(string[] args)
    {
        using Mutex instanceMutex = new(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
            return;

        using EventWaitHandle stopEvent = new(false, EventResetMode.ManualReset, StopEventName);

        // TODO: Start FullTrust IPC / system font management host here.
        stopEvent.WaitOne();
    }
}
