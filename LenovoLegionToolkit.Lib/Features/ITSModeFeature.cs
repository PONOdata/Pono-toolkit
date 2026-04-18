using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace LenovoLegionToolkit.Lib.Features;

public partial class ITSModeFeature : IFeature<ITSMode>
{
    #region Magic Constants
    private const string REG_KEY_LITSSVC_BASE = @"SYSTEM\CurrentControlSet\Services\LITSSVC\LNBITS\IC";
    private const string REG_KEY_LITSSVC_MMC = @"SYSTEM\CurrentControlSet\Services\LITSSVC\LNBITS\IC\MMC";
    private const string REG_KEY_DISPATCHER = @"SYSTEM\CurrentControlSet\Services\LenovoProcessManagement\Performance\PowerSlider";

    private const string VAL_VERSION = "Version";
    private const string VAL_CAPABILITY = "Capability";
    private const string VAL_AUTO_SETTING = "AutomaticModeSetting";
    private const string VAL_CURRENT_SETTING = "CurrentSetting";
    private const string VAL_ITS_FN_CAP = "ITS_FN_Capability";
    private const string VAL_ITS_CUR_SET = "ITS_CurrentSetting";
    private const string VAL_ITS_CUR_SET_V = "ITS_CurrentSettingV";

    private const string DISPATCHER_SERVICE_NAME = "LenovoProcessManagement";
    private const string ITS_SERVICE_NAME = "LITSSVC";

    private const uint DISPATCHER_VERSION_3 = 8192U;
    #endregion

    public ITSMode LastItsMode { get; set; } = ITSMode.None;

    public async Task<bool> IsSupportedAsync()
    {
        if (AppFlags.Instance.Debug)
        {
            return true;
        }

        var machineInfo = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return machineInfo.Properties.SupportsITSMode;
    }

    public async Task<ITSMode[]> GetAllStatesAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        if (mi.LegionSeries == LegionSeries.ThinkBook)
        {
            return Enum.GetValues(typeof(ITSMode))
                       .Cast<ITSMode>()
                       .Where(mode => mode != ITSMode.None)
                       .ToArray();
        }
        else
        {
            return Enum.GetValues(typeof(ITSMode))
                       .Cast<ITSMode>()
                       .Where(mode => mode != ITSMode.MmcGeek && mode != ITSMode.None)
                       .ToArray();
        }
    }

    public async Task<ITSMode> GetStateAsync()
    {
        try
        {
            return await GetITSModeEx().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to get ITS mode", ex);
            return ITSMode.None;
        }
    }

    public async Task SetStateAsync(ITSMode state)
    {
        Log.Instance.Trace($"Setting ITS mode to: {state}");

        try
        {
            SetITSModeEx(state);
            LastItsMode = state;

            Log.Instance.Trace($"ITS mode set successfully to: {state}");

            PublishNotification(state);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to set ITS mode to {state}", ex);
            throw;
        }
    }

    public async Task ToggleItsMode()
    {
        try
        {
            var currentState = await GetStateAsync().ConfigureAwait(false);
            var allStates = await GetAllStatesAsync().ConfigureAwait(false);
            var availableStates = allStates.Where(state => state != ITSMode.None).ToArray();

            if (availableStates.Length == 0) return;

            ITSMode nextState;

            if (currentState == ITSMode.None)
            {
                nextState = LastItsMode != ITSMode.None && availableStates.Contains(LastItsMode)
                    ? LastItsMode
                    : availableStates[0];
            }
            else
            {
                var currentIndex = Array.IndexOf(availableStates, currentState);
                nextState = availableStates[(currentIndex + 1) % availableStates.Length];
            }

            Log.Instance.Trace($"Toggling ITS mode: {currentState} -> {nextState}");
            await SetStateAsync(nextState).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to toggle ITS mode", ex);
        }
    }

    private static void PublishNotification(ITSMode value)
    {
        switch (value)
        {
            case ITSMode.ItsAuto:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModeAuto, value.GetDisplayName()));
                break;
            case ITSMode.MmcCool:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModeCool, value.GetDisplayName()));
                break;
            case ITSMode.MmcPerformance:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModePerformance, value.GetDisplayName()));
                break;
            case ITSMode.MmcGeek:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModeGeek, value.GetDisplayName()));
                break;
        }
    }

    public static void ControlService(string serviceName, ITSModeServiceControlMessage message)
    {
        try
        {
            using ServiceController serviceController = new ServiceController(serviceName);
            Log.Instance.Trace($"Service {serviceName} status: {serviceController.Status}");

            serviceController.ExecuteCommand((int)message);
            Log.Instance.Trace($"Service {serviceName} successfully executed command: {message}");
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to control service", ex);
            throw;
        }
    }

    public static int GetDispatcherVersionEx()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_KEY_DISPATCHER, false);
            if (key != null)
            {
                object value = key.GetValue(VAL_VERSION, 0);
                if (value is int intValue)
                {
                    return intValue;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"GetDispatcherVersionEx failed: {ex.Message}");
        }
        return 0;
    }

    public static int GetITSVersionEx()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_KEY_LITSSVC_BASE, false);
            if (key != null)
            {
                object value = key.GetValue(VAL_VERSION, 0);
                if (value is int intValue)
                {
                    return intValue;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"GetITSVersionEx failed: {ex.Message}");
        }
        return 0;
    }

    public static bool HasDispatcherDeviceNodeEx()
    {
        try
        {
            string targetDeviceId = @"ACPI\\IDEA200C";
            string query = $"SELECT PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%{targetDeviceId}%'";

            using ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            using ManagementObjectCollection collection = searcher.Get();
            return collection.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"HasDispatcherDeviceNodeEx failed: {ex.Message}");
        }
        return false;
    }

    public static bool IsEnergyDriverPresentEx()
    {
        try
        {
            using var handle = PInvoke.CreateFile(
                @"\\.\EnergyDrv",
                (uint)FILE_ACCESS_RIGHTS.FILE_READ_DATA | (uint)FILE_ACCESS_RIGHTS.FILE_WRITE_DATA,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                null);

            return !handle.IsInvalid;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Energy driver check failed", ex);
            return false;
        }
    }

    public static async Task<ITSMode> GetITSModeEx()
    {
        try
        {
            if (!IsEnergyDriverPresentEx())
            {
                Log.Instance.Trace($"EnergyDrv not found, returning None.");
                return ITSMode.None;
            }

            var dispatcherVersion = GetDispatcherVersionEx();
            var machineInfo = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var isThinkBook = machineInfo.LegionSeries == LegionSeries.ThinkBook;

            if (dispatcherVersion >= DISPATCHER_VERSION_3)
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_KEY_DISPATCHER, false);
                if (key != null)
                {
                    int capability = (int)key.GetValue(VAL_ITS_FN_CAP, 0);

                    if (!isThinkBook)
                    {
                        capability &= ~0x10;
                    }

                    bool useVersioned = (capability & 0x10) != 0;
                    string settingKey = useVersioned ? VAL_ITS_CUR_SET_V : VAL_ITS_CUR_SET;

                    int currentSetting = (int)key.GetValue(settingKey, -1);
                    Log.Instance.Trace($"ITS mode check: {settingKey}={currentSetting}");

                    return currentSetting switch
                    {
                        0 => ITSMode.ItsAuto,
                        1 => ITSMode.MmcCool,
                        3 => ITSMode.MmcPerformance,
                        4 => ITSMode.MmcGeek,
                        _ => ITSMode.None
                    };
                }
            }
            else
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_KEY_LITSSVC_MMC, false);
                if (key != null)
                {
                    int autoSetting = (int)key.GetValue(VAL_AUTO_SETTING, -1);
                    int currentSetting = (int)key.GetValue(VAL_CURRENT_SETTING, -1);

                    Log.Instance.Trace($"ITS mode check: Legacy Auto={autoSetting}, Current={currentSetting}");

                    if (autoSetting == 2 && currentSetting == 0)
                    {
                        return ITSMode.ItsAuto;
                    }
                    else if (autoSetting == 1)
                    {
                        if (currentSetting == 1) return ITSMode.MmcCool;
                        if (currentSetting == 3) return ITSMode.MmcPerformance;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"GetITSModeEx failed: {ex.Message}");
        }

        return ITSMode.None;
    }

    public void SetITSModeEx(ITSMode mode)
    {
        try
        {
            var dispatcherVersion = GetDispatcherVersionEx();
            var machineInfo = Compatibility.GetMachineInformationAsync().Result;
            var isThinkBook = machineInfo.LegionSeries == LegionSeries.ThinkBook;

            string targetServiceName;
            ITSModeServiceControlMessage targetMessage;

            if (dispatcherVersion >= DISPATCHER_VERSION_3)
            {
                targetServiceName = DISPATCHER_SERVICE_NAME;
                targetMessage = mode switch
                {
                    ITSMode.ItsAuto => ITSModeServiceControlMessage.IntelligentCoolingIntelligent,
                    ITSMode.MmcCool => ITSModeServiceControlMessage.IntelligentCoolingBsm,
                    ITSMode.MmcPerformance => ITSModeServiceControlMessage.IntelligentCoolingEpm,
                    ITSMode.MmcGeek => ITSModeServiceControlMessage.IntelligentCoolingGeek,
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };
            }
            else
            {
                targetServiceName = ITS_SERVICE_NAME;
                targetMessage = mode switch
                {
                    ITSMode.ItsAuto => ITSModeServiceControlMessage.IntelligentCoolingIntelligent,
                    ITSMode.MmcCool => ITSModeServiceControlMessage.IntelligentCoolingCool,
                    ITSMode.MmcPerformance => ITSModeServiceControlMessage.IntelligentCoolingHighPerformance,
                    ITSMode.MmcGeek => ITSModeServiceControlMessage.IntelligentCoolingGeek,
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };
            }

            Log.Instance.Trace($"Setting ITS mode via service: {targetServiceName} ({targetMessage})");
            ControlService(targetServiceName, targetMessage);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"SetITSModeEx failed: {ex.Message}");
            throw;
        }
    }
}
