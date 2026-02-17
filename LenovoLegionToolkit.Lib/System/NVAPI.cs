using LenovoLegionToolkit.Lib.Utils;
using NvAPIWrapper;
using NvAPIWrapper.Display;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native.Exceptions;
using NvAPIWrapper.Native.GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace LenovoLegionToolkit.Lib.System;

internal static class NVAPI
{
    public static bool IsInitialized { get; set; }
    private static bool? _hasNvidiaCache = null;

    public static void Initialize()
    {
        if (IsInitialized)
        {
            return;
        }

        bool hasCard = HasActiveNvidiaGpu();

        switch (_hasNvidiaCache)
        {
            case false:
                return;
            case null:
            {
                if (!hasCard)
                {
                    _hasNvidiaCache = false;
                    return;
                }
                _hasNvidiaCache = true;
                break;
            }
        }

        try
        {
            NVIDIA.Initialize();
            IsInitialized = true;
        }
        catch (NVIDIAApiException ex)
        {
            Log.Instance.Trace($"Exception in Initialize", ex);
        }
    }

    public static void Unload() => NVIDIA.Unload();

    public static bool HasActiveNvidiaGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            using var collection = searcher.Get();

            foreach (var item in collection)
            {
                var pnpId = item["PNPDeviceID"]?.ToString().ToUpper();
                if (string.IsNullOrEmpty(pnpId) || !pnpId.Contains("VEN_10DE"))
                {
                    continue;
                }

                var errorCodeObj = item["ConfigManagerErrorCode"];
                if (errorCodeObj != null)
                {
                    uint errorCode = Convert.ToUInt32(errorCodeObj);
                    if (errorCode != 0)
                    {
                        Log.Instance.Trace($"NVIDIA GPU found but not active. ErrorCode: {errorCode}");
                        continue;
                    }
                }

                var status = item["Status"]?.ToString();
                if (status != "OK")
                {
                    Log.Instance.Trace($"NVIDIA GPU found but Status is: {status}");
                    continue;
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Error checking for active NVIDIA GPU via WMI", ex);
        }

        return false;
    }

    public static PhysicalGPU? GetGPU()
    {
        try
        {
            bool hasCard = HasActiveNvidiaGpu();

            switch (_hasNvidiaCache)
            {
                case false:
                    return null;
                case null:
                {
                    if (!hasCard)
                    {
                        _hasNvidiaCache = false;
                        return null;
                    }
                    _hasNvidiaCache = true;
                    break;
                }
            }

            var gpu = PhysicalGPU.GetPhysicalGPUs().FirstOrDefault(gpu => gpu.SystemType == SystemType.Laptop);

            if (gpu != null)
            {
                return gpu;
            }

            return null;
        }
        catch (NVIDIAApiException)
        {
            IsInitialized = false;

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }


    public static bool IsDisplayConnected(PhysicalGPU gpu)
    {
        try
        {
            return Display.GetDisplays().Any(d => d.PhysicalGPUs.Contains(gpu, PhysicalGPUEqualityComparer.Instance));
        }
        catch (NVIDIAApiException)
        {
            return false;
        }
    }

    public static string? GetGPUId(PhysicalGPU gpu)
    {
        try
        {
            return gpu.BusInformation.PCIIdentifiers.ToString();
        }
        catch (NVIDIAApiException)
        {
            return null;
        }
    }

    private class PhysicalGPUEqualityComparer : IEqualityComparer<PhysicalGPU>
    {
        public static readonly PhysicalGPUEqualityComparer Instance = new();

        private PhysicalGPUEqualityComparer() { }

        public bool Equals(PhysicalGPU? x, PhysicalGPU? y) => x?.GPUId == y?.GPUId;

        public int GetHashCode(PhysicalGPU obj) => obj.GPUId.GetHashCode();
    }
}
