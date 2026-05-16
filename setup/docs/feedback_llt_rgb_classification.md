---
name: LLT RGB classification methodology
description: When LLT shows "LegionSeries: Unknown" or RGB card missing on a new Legion/LOQ laptop, patch MachineTypeMap first, match keyboard PID prefix against Devices.cs HID constants (0xC100/C600/C900).
type: feedback
originSessionId: 051727d2-c849-4e23-b785-2bb076192341
---
When LLT can't detect RGB on a Legion/LOQ laptop, the fix path is:

**Diagnosis order:**
1. Check LLT log (`%LOCALAPPDATA%\LenovoLegionToolkit\log\*.txt`) for `* LegionSeries:` line. `Unknown` or `Legion_Legacy` on a machine with confirmed EC-level RGB (Fn+Space cycles colors) = classification miss.
2. Find keyboard HID PID: `Get-PnpDevice | ? InstanceId -match 'VID_048D'` → note the PID. Match against LLT's `Devices.GetKeyboardConfig` HID PID prefixes: `0xC100` = Legion_5/Pro_5/7/Pro_7 Gen 10+, `0xC600` = LOQ Gen 10+, `0xC900` = default (Legion_9 and fallback).
3. Check LLT's `Compatibility.cs` `MachineTypeMap` for the machine type (e.g. `83Q7`). If missing, that's the fix target.

**Fix path that worked on Legion 5 15AHP11 (83Q7, Hawk Point refresh 2025 — Ryzen 7 250 + Radeon 780M + XDNA 1, NOT Strix Point):**
- Keyboard PID `0xC615` matches LOQ prefix `0xC600`, not Legion_5's `0xC100`.
- Added `{ "83Q7", LegionSeries.LOQ }` to MachineTypeMap in Compatibility.cs.
- Built LenovoLegionToolkit.Lib.dll locally, swapped over installed DLL, backup of original to `.bak`.
- LLT then enumerates via `Devices.FindHidDevices` with PID prefix C600 + feature report length 0x03C0, finds keyboard, Spectrum per-key RGB activates.

**Why:** LLT's RGB 4-zone controller (`RGBKeyboardBacklightController`) hardcodes PID `0xC900` — it's blind to any other PID. Only the Spectrum per-key controller uses `GetKeyboardConfig` which varies by LegionSeries. So classification drives which HID hunt succeeds.

**How to apply:** On any new Legion/LOQ model where LLT shows no RGB card but Fn hotkey changes keyboard color, run the diagnosis above. If LLT's GitHub has no PR for the machine type yet, local-build + DLL-swap is the bridge until a release lands. Local build: `winget install Microsoft.DotNet.SDK.9`, clone `LenovoLegionToolkit-Team/LenovoLegionToolkit`, patch, `dotnet build LenovoLegionToolkit.WPF/*.csproj -c Release`. Output at `BuildLLT/bin/Release/net9.0-windows10.0.26100.0/win-x64/`.

**Re-applied 2026-04-17** after fresh LTSC install + LLT fork migration wiped the patch. The pre-built patched DLL at `C:\Brofalo\_build\llt\BuildLLT\bin\Release\net9.0-windows10.0.26100.0\win-x64\LenovoLegionToolkit.Lib.dll` was binary-compatible with installed v2.33.0.0 Stable (deployed via `scripts/rgb_deploy_patched_llt.ps1`, verified in log: `LegionSeries='LOQ'`, `MachineType='83Q7'`, RGB Keyboard + Spectrum Keyboard initialized, keyboard HID `VID_048D PID_C615` enumerated, profile 5 / brightness 9 retrieved). Also: `BartoszCichecki/LenovoLegionToolkit` was archived 2025-07-24; migrated to `LenovoLegionToolkit-Team/LenovoLegionToolkit` fork. winget ID points at the archived author's 2.26.1; fetch releases from the Team fork directly.
