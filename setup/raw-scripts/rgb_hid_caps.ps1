# Query every HID device with VID_048D (Lenovo keyboard controller) and print
# its FeatureReportByteLength via Win32 HidD_GetPreparsedData + HidP_GetCaps.
# LLT's Spectrum keyboard controller requires exactly 0x03C0 = 960 bytes.
# If none of Jack's 048D collections report 960, Spectrum route won't work,
# and we need a different strategy (4-zone code patch or OpenRGB).

$ErrorActionPreference = 'Continue'

$code = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class HidProbe {
    [DllImport("hid.dll", SetLastError=true)] public static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll", SetLastError=true)] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll", SetLastError=true)] public static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll", SetLastError=true)] public static extern int  HidP_GetCaps(IntPtr p, ref HIDP_CAPS c);
    [DllImport("hid.dll", SetLastError=true)] public static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Auto)]
    public static extern SafeFileHandle CreateFile(string fn, uint da, uint sm, IntPtr sa, uint cd, uint fa, IntPtr tf);

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort Version; }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS {
        public ushort Usage; public ushort UsagePage;
        public ushort InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    public static void Probe(string devicePath) {
        var h = CreateFile(devicePath, 0xC0000000u, 0x3u, IntPtr.Zero, 3u, 0x80u, IntPtr.Zero);
        if (h.IsInvalid) { Console.WriteLine("  CreateFile FAILED: " + Marshal.GetLastWin32Error()); return; }
        var attr = new HIDD_ATTRIBUTES(); attr.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
        if (!HidD_GetAttributes(h, ref attr)) { Console.WriteLine("  HidD_GetAttributes failed"); h.Dispose(); return; }
        IntPtr ppd = IntPtr.Zero;
        if (!HidD_GetPreparsedData(h, out ppd)) { Console.WriteLine("  HidD_GetPreparsedData failed"); h.Dispose(); return; }
        var caps = new HIDP_CAPS(); caps.Reserved = new ushort[17];
        int r = HidP_GetCaps(ppd, ref caps);
        Console.WriteLine(String.Format("  VID={0:X4} PID={1:X4}  Feature={2}  Input={3}  Output={4}  UsagePage={5:X4} Usage={6:X4}",
            attr.VendorID, attr.ProductID, caps.FeatureReportByteLength, caps.InputReportByteLength, caps.OutputReportByteLength, caps.UsagePage, caps.Usage));
        HidD_FreePreparsedData(ppd);
        h.Dispose();
    }
}
'@
Add-Type -TypeDefinition $code -Language CSharp -ReferencedAssemblies 'System','Microsoft.Win32.SafeHandles'

# Enumerate all VID_048D HID device paths via SetupAPI (use pnputil instead for speed)
Write-Host "=== VID_048D PID_C615 HID feature report lengths ==="
# Shortcut: use Windows' WMI to get device paths
$ids = Get-PnpDevice | Where-Object {
    $_.Class -in @('HIDClass','Keyboard') -and $_.InstanceId -match 'VID_048D'
} | Select-Object -ExpandProperty InstanceId

foreach ($id in $ids) {
    # Convert PnP instance ID into device path: prepend \\?\, replace \ with #, append GUID
    $guid = '{4d1e55b2-f16f-11cf-88cb-001111000030}'  # GUID_DEVINTERFACE_HID
    $path = '\\?\' + $id.Replace('\','#') + '#' + $guid
    Write-Host "PATH: $path"
    try { [HidProbe]::Probe($path) } catch { Write-Host "  exception: $_" }
}
