using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;
using MsRegistry = Microsoft.Win32.Registry;

namespace LenovoLegionToolkit.Lib.System;

public static class AirplaneMode
{
    // Windows tracks the system-wide airplane mode flag in this registry value.
    // The Radio Management Service reads it on start and applies the policy to
    // every radio. The value is readable without elevation; writing requires
    // admin, which LLT already has.
    private const string RadioManagementSubKey = @"SYSTEM\CurrentControlSet\Control\RadioManagement";
    private const string SystemRadioStateValue = "SystemRadioState";
    private const string RadioServiceName = "RmSvc";
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(5);

    // Serializes Toggle() so a rapid Fn+F8 double-press cannot interleave the
    // read-then-write half of the operation. The lock is released before the
    // RmSvc bounce starts on its background task, so a second press queues
    // briefly and then proceeds with a fresh read of the now-flipped state.
    private static readonly object Sync = new();

    // Serializes bounce work so two threads do not fight the service control
    // manager (which is not reentrant for stop/start cycles).
    private static readonly object BounceSync = new();

    // Bounce request flag. Set by Toggle() after each successful registry write;
    // the drain loop reads and clears it under BounceSync until it observes 0,
    // so a second toggle that arrives mid-bounce still gets its policy applied
    // by a follow-up stop/start cycle rather than being silently dropped.
    private static int _bounceRequested;

    public static void Open()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c \"start ms-settings:network-airplanemode\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    public static bool? IsOn()
    {
        try
        {
            using var key = MsRegistry.LocalMachine.OpenSubKey(RadioManagementSubKey);
            return key?.GetValue(SystemRadioStateValue) is int state && state != 0;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to read airplane mode state.", ex);
            return null;
        }
    }

    // Toggles system airplane mode by writing the RadioManagement registry value
    // and bouncing the Radio Management Service so the policy is reapplied.
    // Falls back to opening the airplane mode settings page when the registry
    // read fails, the registry path is missing, or the write throws. Returns
    // the new target state on success, or null on failure.
    public static bool? Toggle()
    {
        try
        {
            lock (Sync)
            {
                Log.Instance.Trace($"Toggling airplane mode...");

                var before = IsOn();
                if (before is null)
                {
                    Log.Instance.Trace($"Could not read airplane mode state, opening settings page instead.");
                    Open();
                    return null;
                }

                var target = !before.Value;

                using (var key = MsRegistry.LocalMachine.OpenSubKey(RadioManagementSubKey, writable: true))
                {
                    if (key is null)
                    {
                        Log.Instance.Trace($"RadioManagement registry key missing, opening settings page instead.");
                        Open();
                        return null;
                    }
                    key.SetValue(SystemRadioStateValue, target ? 1 : 0, RegistryValueKind.DWord);
                }

                // Bouncing on a background task keeps Fn+F8 snappy. The radios
                // re-evaluate the airplane mode flag once RmSvc comes back; the
                // notification fires on the target state and does not block on it.
                Interlocked.Exchange(ref _bounceRequested, 1);
                _ = Task.Run(BounceRadioServiceDrain);

                Log.Instance.Trace($"Airplane mode toggled to {(target ? "on" : "off")}.");
                return target;
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to toggle airplane mode, opening settings page instead.", ex);
            Open();
            return null;
        }
    }

    // Drains pending bounce requests so a second Fn+F8 that arrived mid-bounce
    // still triggers a follow-up stop/start cycle. TryEnter coalesces multiple
    // concurrent invocations of the drain itself (only one drain runs at a
    // time); the inner while loop ensures every set of _bounceRequested becomes
    // a real bounce before exit.
    private static void BounceRadioServiceDrain()
    {
        if (!Monitor.TryEnter(BounceSync))
        {
            Log.Instance.Trace($"Skipping {RadioServiceName} drain, one already in flight.");
            return;
        }

        try
        {
            while (Interlocked.Exchange(ref _bounceRequested, 0) == 1)
                BounceRadioServiceOnce();
        }
        finally
        {
            Monitor.Exit(BounceSync);
        }
    }

    private static void BounceRadioServiceOnce()
    {
        try
        {
            using var sc = new ServiceController(RadioServiceName);

            // Accessing Status throws InvalidOperationException if the service
            // does not exist on this Windows SKU. In that case the registry
            // write has still landed; the policy will apply when (if) the
            // service is later installed and started.
            ServiceControllerStatus status;
            try
            {
                status = sc.Status;
            }
            catch (InvalidOperationException ex)
            {
                Log.Instance.Trace($"{RadioServiceName} service not present, skipping bounce.", ex);
                return;
            }

            if (status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
            }
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to bounce {RadioServiceName}.", ex);
        }
    }
}
