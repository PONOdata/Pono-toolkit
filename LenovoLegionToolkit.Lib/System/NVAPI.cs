using LenovoLegionToolkit.Lib.Utils;
using NvAPIWrapper;
using NvAPIWrapper.Display;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native.Exceptions;
using NvAPIWrapper.Native.GPU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LenovoLegionToolkit.Lib.System;

internal static class NVAPI
{
    public static bool IsInitialized { get; set; }
    public static void Initialize()
    {
        try
        {
            if (IsInitialized)
            {
                return;
            }

            NVIDIA.Initialize();
            IsInitialized = true;
        }
        catch (NVIDIAApiException ex)
        {
            if (!ex.Message.Contains("NVAPI_NVIDIA_DEVICE_NOT_FOUND") && !ex.Message.Contains("NVAPI_API_NOT_INITIALIZED"))
            {
                Log.Instance.Trace($"Exception in Initialize", ex);
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Exception in Initialize", ex);
        }
    }

    public static void Unload() => NVIDIA.Unload();

    public static PhysicalGPU? GetGPU()
    {
        try
        {
            var gpu = PhysicalGPU.GetPhysicalGPUs().FirstOrDefault(gpu => gpu.SystemType == SystemType.Laptop);

            if (gpu != null)
            {
                return gpu;
            }

            return null;
        }
        catch (NVIDIAApiException ex)
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
