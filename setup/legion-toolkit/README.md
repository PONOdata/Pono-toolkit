# legion-toolkit

Scripts for getting Lenovo Legion Toolkit (LLT) onto a fresh LTSC box and patched for newer Legion/LOQ machine types that the released build hasn't classified.

## Why this exists

LLT is the only realistic replacement for Lenovo Vantage on LTSC (Vantage drops a ton of consumer telemetry/bloat, and it actively conflicts with LLT over RGB + thermal mode). Two problems on new hardware:

1. **The original author archived the repo 2025-07-24.** `BartoszCichecki/LenovoLegionToolkit` is frozen at 2.26.1. winget still tracks that archived repo, so `winget install --id BartoszCichecki.LenovoLegionToolkit` gives you the stale version. The community fork at `LenovoLegionToolkit-Team/LenovoLegionToolkit` ships the ongoing work.

2. **LLT's `MachineTypeMap` in `Compatibility.cs` is a hard-coded lookup.** When you buy a Legion or LOQ released after the last LLT release, your machine type is `Unknown`, and the Spectrum/4-zone RGB card never appears in the sidebar even when the hardware supports it.

## `llt-upgrade-to-fork.ps1`

Uninstalls the archived LLT, downloads the Team-fork release directly from GitHub, and installs silently. Default target is `v2.33.0.0` (Stable). Override with `-ReleaseTag v2.33.9.0` for the Dev channel.

```powershell
# default Stable
.\llt-upgrade-to-fork.ps1

# specific release
.\llt-upgrade-to-fork.ps1 -ReleaseTag v2.33.9.0
```

## `rgb-patch-deploy.ps1` + building the patched DLL

If LLT's sidebar has no Keyboard / RGB section on your machine but `Fn+Space` cycles colors on the hardware, your machine type isn't in `MachineTypeMap`. Patch and rebuild:

### Diagnosis order

1. Check the LLT log at `%LOCALAPPDATA%\LenovoLegionToolkit\log\log_*.txt` for a line beginning with `* LegionSeries: 'Unknown'` or `'Legion_Legacy'`. That's the classification miss.
2. Get the keyboard HID PID. In PowerShell:
   ```powershell
   Get-PnpDevice | Where-Object { $_.InstanceId -match 'VID_048D' } | Select-Object FriendlyName, InstanceId
   ```
   Note the four-character PID in the `PID_XXXX` segment (e.g. `PID_C615`).
3. Match the PID prefix against LLT's `Devices.GetKeyboardConfig` constants:
   - `0xC100` — Legion_5 / Legion_Pro_5 / Legion_7 / Legion_Pro_7 Gen 10+
   - `0xC600` — LOQ Gen 10+
   - `0xC900` — Legion_9 and default/fallback
4. Look up your machine type (e.g. `83Q7`) and decide which `LegionSeries` to map it to based on the PID family above.

### Build steps

```powershell
# prerequisites
winget install --id Microsoft.DotNet.SDK.9 --silent --accept-source-agreements --accept-package-agreements
git clone https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit.git
cd LenovoLegionToolkit
```

Edit `LenovoLegionToolkit.Lib/Utils/Compatibility.cs`, find `MachineTypeMap`, and add your mapping. Example:

```csharp
private static readonly Dictionary<string, LegionSeries> MachineTypeMap = new()
{
    // ... existing entries ...
    { "83Q7", LegionSeries.LOQ },   // Legion 5 15AHP11 (Hawk Point refresh, keyboard PID 0xC615)
};
```

Build Release:

```powershell
dotnet build LenovoLegionToolkit.Lib/LenovoLegionToolkit.Lib.csproj -c Release
```

Output lands at `LenovoLegionToolkit.Lib/bin/Release/net9.0-windows10.0.26100.0/win-x64/LenovoLegionToolkit.Lib.dll`. Copy that into this directory as `_build/LenovoLegionToolkit.Lib.dll` (or pass `-BuiltDll <path>` to the deploy script).

### Deploy

```powershell
# from an elevated shell
.\rgb-patch-deploy.ps1
```

The script:
- Kills any running LLT
- Backs up `LenovoLegionToolkit.Lib.dll` to `.bak` (first call only)
- Drops the patched DLL in place
- Launches LLT
- Waits 12 seconds for enumeration
- Greps the LLT log for `LegionSeries|MachineType|RGB Keyboard|Spectrum|Found device` and prints the matches

Expected success signature in the log:
```
* LegionSeries: 'LOQ'
* MachineType: '83Q7'
RGB Keyboard initialized successfully.
Found device. [vendorId=48D, productId=C615, descriptorLength=960]
Spectrum Keyboard initialized successfully.
```

### Rollback

If LLT crashes or classifies incorrectly:

```powershell
$inst = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit"
Copy-Item "$inst\LenovoLegionToolkit.Lib.dll.bak" "$inst\LenovoLegionToolkit.Lib.dll" -Force
```

## Upstream

If your patch works, open a PR to `LenovoLegionToolkit-Team/LenovoLegionToolkit` with the `MachineTypeMap` entry so the next release covers your machine for everyone. DLL-swap is a bridge, not a permanent solution.
