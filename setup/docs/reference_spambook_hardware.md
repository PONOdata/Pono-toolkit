---
name: Spambook-Max Hardware Spec (Lenovo Legion 5 15AHP11)
description: Authoritative hardware inventory for Jack's 2026-04 laptop. Use this for any NPU/GPU/thermal/RGB decision. Correcting prior XDNA 2 / Strix Point assumption.
type: reference
originSessionId: 6a8f0599-a316-4f0d-9080-5b5899f0dcbe
---
Jack's new laptop, hostname **SPAMBOOK-MAX**. Lenovo Legion 5 15AHP11 (machine type `83Q7`, full MTM `83Q7CTO1WW`, serial `MP2V4G4T`). Bought 2026-04-13, warranty through 2027-06-11.

**How to apply:** Reference this file any time you're about to make a claim about the NPU generation, GPU, thermal envelope, or RGB controller on Jack's laptop. Do NOT trust prior handoffs that reference "Strix Point" or "XDNA 2" for this machine — those were wrong and have been corrected.

## Silicon — the important correction

| Component | What it actually is | What earlier handoffs wrongly said |
|---|---|---|
| CPU | **AMD Ryzen 7 250** (Hawk Point refresh, Phoenix 2 silicon, Zen 4, 8C/16T, ~45W PPT) | — |
| iGPU | **Radeon 780M** (RDNA 3, 12 CUs) | — |
| NPU | **XDNA 1**, ~16 TOPS INT8, ~5W sustained | "XDNA 2, ~16 TOPS" — XDNA 2 is Strix Point (~50 TOPS), wrong silicon family |
| dGPU | **NVIDIA RTX 5060 Laptop GPU** (Blackwell, 8 GB GDDR7, ~95W TGP, driver 595.79) | — |
| RAM | — (confirm via `Get-CimInstance Win32_PhysicalMemory` next time) | — |

**How to tell the difference:** Strix Point ships with Radeon **880M/890M** iGPU. Hawk Point refresh ships with **780M**. `Get-CimInstance Win32_VideoController` on this machine shows 780M → definitely Hawk Point. Also: AMD's "Ryzen AI" branding only applies to Strix Point and newer; this chip is "Ryzen 7 250" with no "AI" suffix.

## Thermals (learned the hard way 2026-04-17)

- Cooling is shared between CPU + dGPU. Combined sustained load ~140W (45W APU + 95W dGPU TGP) can overrun the stock fan curve on LTSC with no Lenovo thermal service.
- Hard cutoff observed at 02:04 UTC 2026-04-17 (Kernel-Power 41, no bugcheck, no WHEA — classic EC thermal safety trip).
- **Baseline settings:** Hybrid Mode ON (lets RTX 5060 sleep when idle). LLT Power Mode Balanced for daily, Quiet when chasing headroom. HWiNFO64 installed for live monitoring.
- ACPI thermal zone `\_TZ.TZ01` reports `_PSV = _AC0 = _AC1 = 0K` → Windows sees no trip points; fan is 100% EC-governed with no Windows-side governor. This is expected Legion behavior and not a fault.

## Keyboard (the RGB saga)

- 4-zone RGB exists (Fn+Space cycles colors → hardware confirmed).
- Keyboard controller VID `0x048D` (ITE Tech), PID `0xC615`. The `0xC6xx` prefix matches LLT's LOQ keyboard family, not Legion_5.
- LLT classifies `83Q7` as `Unknown` by default → no RGB card appears. Fix is in `feedback_llt_rgb_classification.md`: patch `MachineTypeMap` in `Compatibility.cs` to add `{ "83Q7", LegionSeries.LOQ }`, rebuild, swap DLL. Script: `scripts/rgb_deploy_patched_llt.ps1`.
- Windows 11 Dynamic Lighting is available on this LTSC build (confirmed) but doesn't drive the keyboard without the Spectrum driver or LLT patch.

## Driver state (as of 2026-04-17)

- AMD Chipset Software 8.02.18.557 — installed ✓
- AMD PMF 26.10.4.0 — installed ✓
- AMD PPM Provisioning File Driver 8.0.0.59 — installed ✓
- AMD PSP Driver 5.40.0.0 — installed ✓
- NVIDIA Graphics Driver 595.79 — installed ✓
- Lenovo System Update 5.08.03.59 — installed, useful for pulling machine-specific drivers by MTM
- Lenovo Vantage — **uninstalled** 2026-04-17 (conflicted with LLT)
- Razer Synapse 4 + Chroma — installed (Basilisk V3 X HyperSpeed mouse)

## Lenovo driver pull URL (machine-specific)

`https://pcsupport.lenovo.com/us/en/products/LAPTOPS-AND-NETBOOKS/LEGION-SERIES/LEGION-5-15AHP11/83Q7/83Q7CTO1WW/MP2V4G4T`

This URL pulls drivers targeted to exactly this serial. Useful when winget / generic driver sources don't have the right SKU match.

## NPU pipeline impact

For `kara-nodes/pipeline_quality/` throughput planning: expect ~1/3 the row/sec numbers from Spot's original design (which assumed XDNA 2). See `HANDOFF_20260416_INCOMING_DATA.md` for the corrected rate table. Full 344k-corpus reprocess ≈ 36 min on XDNA 1 vs 12 min on XDNA 2. Daily 1080-row Pi drop ≈ 7 sec. Still well inside budget.

## Ryzen AI SDK install state (2026-04-17)

- **Installed:** Ryzen AI SDK 1.7.1 at `C:\Program Files\RyzenAI\1.7.1\`
- **Conda base:** Miniforge3 26.1.1-3 at `C:\Users\Jack\miniforge3\` (winget `CondaForge.Miniforge3`). Must be on **Machine PATH** (not just User) because the AMD installer elevates via UAC and the elevated process uses Machine PATH only.
- **SDK conda env:** `ryzen-ai-1.7.1` (Python 3.12) at `C:\Users\Jack\miniforge3\envs\ryzen-ai-1.7.1`
- **ORT:** `1.23.3.dev20260320` with `VitisAIExecutionProvider`, `DmlExecutionProvider`, `CPUExecutionProvider` available
- **NPU driver:** 32.0.203.314 (exceeds required 32.0.203.280)
- **Machine env var set:** `RYZEN_AI_INSTALLATION_PATH=C:\Program Files\RyzenAI\1.7.1` (persisted 2026-04-17)
- **Quicktest verified** on NPU: session init succeeds, AMD's quicktest prints `Setting environment for PHX/HPT` (confirming Phoenix/Hawk Point = XDNA 1 auto-detection) and finishes.

**Install gotchas banked for next LTSC box:**
1. AMD installer requires `conda` on **Machine PATH** (not User). If only User PATH has conda, installer shows "Conda not found" because UAC-elevated process reads Machine PATH.
2. The `ryzen-ai-lt-*.exe` ("lightweight") variant does NOT bundle Miniforge; pre-install Miniforge via `winget install --id CondaForge.Miniforge3 --silent`.
3. Download is gated at `account.amd.com/en/forms/downloads/xef.html?filename=<name>` — can't curl past; requires AMD account login + EULA click.
4. Silent install flags (`/VERYSILENT /LOG=...`) return exit 0 even when install silently aborts due to missing prerequisites. Always run interactively first time on a new box so the conda-detection failure is visible, then go silent on subsequent boxes.
5. Installer drops its own env-var log at `C:\Temp\ryzen_ai_<timestamp>.txt` when it fails — check there first for the exact failed command.

**For Hawk Point specifically:** when writing Python that calls VitisAI-EP, set provider option `target='X1'` (not the default Strix target). AMD docs, and the quicktest itself, both key off this.

**Scripts banked:**
- `scripts/ryzen_ai_check.ps1` — pre-install inventory
- `scripts/ryzen_ai_install_interactive.ps1` — launch AMD installer with conda on PATH
- `scripts/add_conda_to_machine_path.ps1` — add Miniforge to Machine PATH (elevated)
- `scripts/ryzen_ai_postinstall.ps1` — post-install verify
- `scripts/ryzen_ai_quicktest.ps1` — AMD's bundled smoke test runner
- `scripts/persist_ryzen_ai_env.ps1` — persist `RYZEN_AI_INSTALLATION_PATH` to Machine
