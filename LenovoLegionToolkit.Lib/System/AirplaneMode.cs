using System;
using System.Diagnostics;
using System.ServiceProcess;
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
    // path is missing or the write fails. Returns the target state on success
    // or null on failure.
    public static bool? Toggle()
    {
        try
        {
            var before = IsOn() ?? false;
            var target = !before;

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
            _ = Task.Run(BounceRadioService);

            return target;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to toggle airplane mode, opening settings page instead.", ex);
            Open();
            return null;
        }
    }

    private static void BounceRadioService()
    {
        try
        {
            using var sc = new ServiceController(RadioServiceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
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
