<div align="center">

# Pono Toolkit

  [![Build Status](https://img.shields.io/github/actions/workflow/status/PONOdata/Pono-toolkit/build.yml?branch=main&logo=github&logoColor=white)](https://github.com/PONOdata/Pono-toolkit/actions)
  [![Latest Release](https://img.shields.io/github/v/release/PONOdata/Pono-toolkit?include_prereleases&color=brightgreen)](https://github.com/PONOdata/Pono-toolkit/releases)
  [![License](https://img.shields.io/github/license/PONOdata/Pono-toolkit?color=blue)](LICENSE)
  [![Based on LLT](https://img.shields.io/badge/based%20on-Lenovo%20Legion%20Toolkit-orange)](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit)

</div>

## What this is

**Pono Toolkit** is a Windows desktop utility for power users who want one place to manage system-level features that vendors usually spread across multiple bloated apps. Lenovo Legion is the primary supported platform today; Pono Toolkit is built as an **AIO** (all-in-one) tool with cross-vendor utilities at its core (airplane mode, Dynamic Lighting, automation, notifications) and Lenovo-specific tools layered on top.

Pono Toolkit is a fork of [Lenovo Legion Toolkit](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) (LLT) by Bartosz Cichecki, Kaguya, and Dr. Skinner. It is independent, GPL-3.0 licensed, and not officially affiliated with Lenovo or the LLT-Team.

## Standing on shoulders

Pono Toolkit would not exist without **Lenovo Legion Toolkit**. Bartosz Cichecki started the project, and Kaguya and Dr. Skinner have been carrying it forward. Years of WMI reverse engineering, hardware compatibility work, ACPI exploration, and feature design done by them is the foundation everything in this fork rests on.

If you find Pono Toolkit useful, please consider donating to the upstream project that made it possible:

[![Donate to upstream LLT](https://img.shields.io/badge/Donate%20to%20upstream%20LLT-PayPal-0070BA?logo=paypal&logoColor=white)](https://www.paypal.com/donate/?hosted_button_id=22AZE2NBP3HTL)

Donations through this link go directly to the upstream LLT maintainers (Bartosz Cichecki et al.), not to Pono Data Solutions. That is intentional. The economic foundation of this fork belongs upstream.

## What is in this fork that is not in upstream

- **Auto-detect Dynamic Lighting devices.** No debug flag. The Dynamic Lighting page surfaces automatically when a `LampArray`-conformant device is present.
- **Three system indicator effects.** `BatteryLowEffect` (red pulse below configurable threshold), `ChargingEffect` (gradient sweep when AC connected), `CapsLockIndicatorEffect` (configurable color when caps lock is on). Each samples its platform signal at most once per wall-clock second so the indicator cadence stays steady regardless of animation speed.
- **Borg auto-adapt mode.** Zero-configuration adaptive effect that "just works" on any LampArray-conformant device. `Status` and `Branding` lamps stay white so system feedback channels remain readable; everything else runs a spatial rainbow whose period scales with `LampCount`. Master toggle replaces the current default effect for the array while per-lamp overrides and routing continue to apply.
- **`LampPurposes`-aware effect routing.** Optional `RespectLampPurposes` toggle. When on, lamps marked `Status` or `Branding` hold a configurable `StatusLampColor` instead of running the active animation, preserving the system's intended channel for caps-lock, charging, and brand-mark feedback.
- **`IsControlled` state surfacing.** The Dynamic Lighting page shows whether the controller is currently `Active` or `Yielded to system or other application`, so users can tell at a glance who owns the array.
- **Fn+F8 system airplane mode toggle.** Pressing `Fn+F8` flips the `SystemRadioState` registry value and bounces the Radio Management Service so Windows reapplies the policy. An LLT-style notification confirms the new state. Falls back to opening the airplane mode settings page if the registry write fails.
- **Smart Key cycle sync with automation.** When an automation pipeline runs because of an event (e.g. a game starts and triggers a pipeline that runs Quick Action 2), the smart key cycle index advances past that quick action, so the next smart key press produces the next action instead of repeating the one the automation just ran.
- **Latent `PowerModeExtreme` notification fix.** Fixes a missing switch arm in `NotificationsManager` that previously threw `ArgumentException` for any Extreme-mode notification.
- **Three-tier signing pipeline.** `build.yml` now routes signing through `LLT_CERT_PFX` (canonical), Azure Trusted Signing (federated identity, for forks with their own setup), or a `dotnet build` compile-check fallback (fork PRs without signing secrets), so the workflow always produces meaningful CI signal.
- **AIO framing.** The cross-vendor utilities (airplane toggle, Dynamic Lighting completion) are designed to work on any Windows 11 device, not just Legion hardware.

## Install

Download the latest signed installer from the [Releases page](https://ponotoolkit.com/releases (or [GitHub Releases](https://github.com/PONOdata/Pono-toolkit/releases/latest))) and run it.

Pono Toolkit installs to `Program Files\PonoToolkit` and stores its settings under `%localappdata%\PonoToolkit`. It can coexist on the same machine as Lenovo Legion Toolkit; the AppId, mutex, and settings folder are independent.

## Compatibility

**Lenovo Legion** — primary supported platform. The full Lenovo-specific feature set from upstream LLT works the same way: power modes, fan curves, GPU controls, RGB keyboard, GodMode, automation triggers, hardware sensors. Tested device matrix lives in [Compatibility.cs](LenovoLegionToolkit.Lib/Utils/Compatibility.cs).

**Other Windows 11 devices** — the cross-vendor features are usable independently:

| Feature | Hardware requirement |
|---|---|
| Fn+F8 airplane toggle | Any keyboard with an Fn+F8 hotkey that LLT can intercept; or rebind via Windows |
| Dynamic Lighting effects | Any device exposing a `Windows.Devices.Lights.LampArray` HID descriptor |
| System indicator effects | Same as Dynamic Lighting |
| Borg auto-adapt | Same as Dynamic Lighting |
| Smart key automation sync | Smart-key-equipped Lenovo (currently); planned to generalize to any global hotkey |

If your non-Lenovo device works with Pono Toolkit, please open a GitHub Issue noting the make/model so the AIO compatibility list can grow.

## Build from source

```
git clone https://github.com/PONOdata/Pono-toolkit.git
cd Pono-toolkit
dotnet build LenovoLegionToolkit.WPF/LenovoLegionToolkit.WPF.csproj -c Release
```

The output binary is at `BuildLLT/bin/Release/net9.0-windows*/win-x64/Pono Toolkit.exe`. To produce an installer, you also need [Inno Setup 6](https://jrsoftware.org/isinfo.php) and run `iscc make_installer.iss`.

## Versioning

Pono Toolkit uses semantic versioning starting at v0.1.0. Releases are tagged on the `main` branch and signed via Azure Trusted Signing under the Pono Data Solutions identity.

## License and attribution

Pono Toolkit is licensed under [GPL-3.0](LICENSE), inherited from Lenovo Legion Toolkit. Original copyright notices in source files are preserved. The upstream project is the foundation for everything in this fork.

- Lenovo Legion Toolkit — © Bartosz Cichecki, and the LLT-Team (Kaguya, Dr. Skinner). https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit
- Pono Toolkit additions — © Pono Data Solutions.

## Status

Pono Toolkit is early-stage. The fork is maintained alongside upstream LLT; non-conflicting upstream patches will be merged in over time. Filing issues here is welcome; the project does not currently provide a Discord server.
