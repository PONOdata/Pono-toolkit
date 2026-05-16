---
name: LTSC Enrichment Auto-Script
description: Reusable Win11 Ent LTSC post-install script we're building. Universal layer (drivers, MS Store, Office, essentials) + per-machine hardware layer. Scope still being defined.
type: project
originSessionId: 6a8f0599-a316-4f0d-9080-5b5899f0dcbe
---
Reusable auto-script for turning a fresh Win11 Enterprise LTSC 2024 install into a working daily driver. Split into two layers so the universal part is portable to any LTSC machine.

**Why:** LTSC ships lean on purpose (no Store, no Edge updates, no Office, stale drivers). Jack reinstalls/rebuilds often enough that a one-shot script beats redoing the manual Appx sideload / driver chase / Office install every time. THX and RGB are machine-specific, not portable, so they live in a separate layer.

**How to apply:** When Jack brings this up, pick up the discussion on scope (esp. what counts as "essentials"). Do NOT assume the full essentials list yet. Don't write the script until scope is locked.

## Layer 1 — Universal LTSC enrichment (portable to any Win11 Ent LTSC install)

- MS Store + winget (kkkgo-style Appx sideload) — proven working on Spambook-Max
- Widgets Platform Runtime + Web Experience Pack
- Latest drivers: chipset, GPU, NIC, audio, OEM-specific pull
- Microsoft Office
- ESSENTIALS — scope TBD with Jack. Candidates to confirm: VC++ redists, .NET runtimes, PowerToys, Edge updates, Windows Terminal, 7-Zip, Notepad++, git, OpenSSH. Do not guess.

## Layer 2 — Per-machine hardware (Spambook-Max / Lenovo Legion 5 15AHP11 / 83Q7)

- Razer THX Spatial Audio key `H2B3F-429H8-GA962-P7CGP` (activate via Synapse 4)
- **Lenovo Legion Toolkit v2.33.0.0 from Team fork** (original `BartoszCichecki` author archived 2025-07-24; use `LenovoLegionToolkit-Team/LenovoLegionToolkit` instead — active, 940 stars, pushed daily). winget ID still lists the archived version, so fetch EXE direct from GitHub release.
- **LLT MachineTypeMap patch** for RGB: 83Q7 keyboard uses ITE PID `0xC615` (LOQ family prefix `0xC600`), not Legion_5's `0xC100`. LLT misclassifies 83Q7 as `Unknown` without the patch and RGB card never appears. Fix: add `{ "83Q7", LegionSeries.LOQ }` to `MachineTypeMap` in `LenovoLegionToolkit.Lib/Utils/Compatibility.cs`, rebuild `LenovoLegionToolkit.Lib.dll`, swap via `scripts/rgb_deploy_patched_llt.ps1`. Full methodology in `feedback_llt_rgb_classification.md`.
- **Lenovo Vantage MUST be uninstalled** — conflicts with LLT (takes hardware ownership, disables RGB card even when patch is in place) and leaves the EC thermal mode in an undefined state (contributed to overheat crash 2026-04-17 02:04 UTC).
- **Hybrid Mode ON** in LLT (lets RTX 5060 dGPU sleep when idle; shared cooler otherwise sustains ~140W APU+dGPU and trips EC thermal cutoff on sustained loads).
- HWiNFO64 via `winget install REALiX.HWiNFO` for live thermal telemetry (stock Windows has no visible CPU package temp on this machine — `MSAcpi_ThermalZoneTemperature` needs admin and thermal zone has no trip points exposed to Windows).
- BT adapter tune: `C:\installers\bt_tune.ps1` (UAC wedged on last attempt, still pending).
- Windows 11 Dynamic Lighting: present on this LTSC build (confirmed — LTSC does NOT strip it). Not useful here because LLT patch owns RGB, but noted for other machines.

## State on Spambook-Max (updated 2026-04-17)

All Layer 2 items done. Session 2026-04-17 reapplied everything after a fresh LTSC + LLT chain:
1. Uninstalled Vantage (service + 5 Vantage processes + registry + folders — verified clean via `scripts/post_vantage_check.ps1`).
2. Installed HWiNFO64 for thermal telemetry.
3. Replaced archived LLT 2.26.1 with Team-fork v2.33.0.0 Stable via `scripts/llt_upgrade_admin.ps1` (registers `LenovoLegionToolkit.LampArray.msix` for Win11 Dynamic Lighting interop; required admin for AppxPackage register).
4. Deployed pre-built patched `LenovoLegionToolkit.Lib.dll` from `C:\Brofalo\_build\llt\BuildLLT\bin\Release\net9.0-windows10.0.26100.0\win-x64\` over the installed 2.33.0.0 via `scripts/rgb_deploy_patched_llt.ps1`. LLT log confirms: `LegionSeries='LOQ'`, `MachineType='83Q7'`, `RGB Keyboard initialized successfully`, `Spectrum Keyboard initialized successfully`, HID device `VID_048D PID_C615` found.
5. Set LLT Hybrid Mode ON.

Layer 1 (universal) is still being codified. Layer 2 now reproducible via the scripts above.

## Lessons banked 2026-04-17

- **BartoszCichecki/LenovoLegionToolkit is archived.** Always check the Team fork (`LenovoLegionToolkit-Team/LenovoLegionToolkit`) for new releases. winget lags, pulls the archived author's 2.26.1.
- **Vantage + LLT cannot coexist.** LLT explicitly disables RGB when Vantage is running and the double-management leaves EC thermal mode undefined (crash vector).
- **Overheat crash signature:** Kernel-Power 41 with no bugcheck 1001, no WHEA, no minidump = hard EC thermal cutoff, not a software fault. Distinguishes from BSOD.
- **RTX 5060 + Ryzen 7 250 combined TGP ~140W** exceeds sustained capacity of Legion 5 15AHP11 cooler without LLT thermal management. Hybrid Mode ON is mandatory for daily use; "dGPU always on" is a crash vector.
- **Memory files with hardware claims get stale.** Prior handoff called this "Strix Point / XDNA 2" when evidence (Radeon 780M iGPU) says Hawk Point / XDNA 1. See `reference_spambook_hardware.md` for authoritative spec.
- **Check memory for prior fixes BEFORE debugging from scratch.** `feedback_llt_rgb_classification.md` already had the RGB fix banked from a prior session; I ran a full re-debug instead of searching memory first. Grade F on that. Remediation: future hardware-quirk debugging opens with `Grep feedback_ AND project_` for the model number.

## Open questions for Jack

- Full essentials list for Layer 1
- Target format: single PowerShell script, winget manifest, or boxstarter-style bootstrap?
- Office flavor: Microsoft 365, Office LTSC 2024 ProPlus (matches Windows LTSC cadence), or Office 2024 retail?
- Driver source: OEM vendor sites scripted, Windows Update, or Snappy Driver Installer Origin?
- BitLocker: in the script or dedicated hardening session (per `HANDOFF_20260416_INCOMING_DATA.md` open items)?
