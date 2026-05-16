# windows-pono

Everything we did to turn a fresh Windows 11 Enterprise LTSC 2024 install on a **Lenovo Legion 5 15AHP11** (machine type `83Q7`, Ryzen 7 250 / Hawk Point / **XDNA 1**, Radeon 780M iGPU, RTX 5060 dGPU) into a working Pono dev box.

One long 2026-04-17 session, all artifacts preserved: the curated scripts we'd run again on the next box, the raw disposable diagnostics we wrote mid-debug, the memory docs that got banked, the handoff that got corrected, and the pre-built patched LLT DLL that makes RGB work.

## Silicon reality check — read this before anything else

AMD marketing names are a trap on this chip. The real identifiers:

| Thing | Actual value | What it might be confused with |
|---|---|---|
| CPU | AMD Ryzen 7 250 (Hawk Point refresh, Phoenix 2 silicon, Zen 4, 8C/16T) | "Ryzen AI" (Strix Point) — **not this** |
| CPUID | Family 25 (0x19) Model 117 (0x75) Stepping 2 | Strix Point is Family 26 Model 36 |
| iGPU | Radeon 780M (RDNA 3) | Strix Point ships 880M / 890M |
| NPU | **XDNA 1**, ~16 TOPS INT8, ~5W sustained | XDNA 2 ships on Strix Point (~50 TOPS) |
| dGPU | NVIDIA RTX 5060 Laptop GPU (Blackwell, 8 GB GDDR7, driver 595.79+) | — |

When writing Python against the Ryzen AI SDK for this box, set the VitisAI provider option `target='X1'` (Strix Point uses the default). AMD's own `quicktest.py` prints `Setting environment for PHX/HPT` on this silicon — Phoenix / Hawk Point, confirming the X1 target.

Run `diagnostics/cpu-identify.ps1` on any new box to settle this definitively before trusting the marketing name.

## Repo layout

```
windows-pono/
├── README.md                    (this file)
├── .gitignore
│
│  -- Curated, reusable on the next LTSC box -------------------------
├── ryzen-ai/                    Ryzen AI SDK 1.7.1 install chain
│   ├── add-conda-to-machine-path.ps1   (the critical Machine PATH fix)
│   ├── install.ps1                     (interactive launcher w/ sig check)
│   ├── persist-env.ps1                 (RYZEN_AI_INSTALLATION_PATH machine-wide)
│   ├── postinstall-verify.ps1
│   └── quicktest.ps1
│
├── legion-toolkit/              LLT migration + MachineTypeMap RGB patch
│   ├── README.md                       (full patch methodology + build steps)
│   ├── llt-upgrade-to-fork.ps1         (uninstall archived 2.26.1, install Team-fork)
│   ├── rgb-patch-deploy.ps1            (swap patched Lib.dll, backup+rollback)
│   └── patched-dll/
│       └── LenovoLegionToolkit.Lib.dll (pre-built patched DLL with 83Q7 -> LOQ mapping)
│
├── diagnostics/                 Read-only triage scripts
│   ├── cpu-identify.ps1                (CPUID decode: Hawk vs Strix vs Krackan vs Strix Halo)
│   ├── thermal-diag.ps1                (Kernel-Power 41 / WHEA / BugCheck scan)
│   └── post-vantage-check.ps1          (verify Lenovo Vantage uninstall is clean)
│
├── drivers/                     Hardware-agnostic driver scanner + Lenovo link
│   ├── scan-drivers.ps1                (full PnP inventory, problem codes, missing-driver HWIDs, vendor versions)
│   └── lenovo-drivers-open.ps1         (opens Lenovo's machine-specific driver page for this exact MTM + serial)
│
├── claude/                      Claude auto-install (WIP, on hold)
│   ├── README.md                       (status + what needs to land before this goes live)
│   └── install.ps1                     (skeleton: Node.js + CLI + Desktop + Pono bundle. Aborts until training deliverable exists.)
│
│  -- Raw session archive -------------------------------------------
├── raw-scripts/                 Every PS1 written in the session, verbatim.
│                                Many are disposable one-shots we wrote mid-debug.
│                                Kept here so the investigation trail is reproducible.
│   ├── rgb_diag.ps1, rgb_diag2.ps1, rgb_deep_diag.ps1, rgb_drive.ps1, ...
│   ├── thermal_diag.ps1, post_vantage_check.ps1, cpu_identify.ps1, ...
│   ├── llt_upgrade.ps1, llt_upgrade_admin.ps1, llt_restart.ps1, ...
│   ├── rgb_deploy_patched_llt.ps1, rgb_deploy_clean_llt.ps1, ...
│   ├── ryzen_ai_check.ps1, ryzen_ai_install*.ps1, ryzen_ai_quicktest.ps1, ...
│   └── ... (29 scripts total)
│
├── docs/                        Memory files banked during the session
│   ├── reference_spambook_hardware.md     (authoritative hardware spec)
│   ├── project_ltsc_autoscript.md         (Layer 1 + Layer 2 setup project)
│   └── feedback_llt_rgb_classification.md (the RGB fix methodology, reusable)
│
└── handoffs/                    Session handoff docs
    └── HANDOFF_20260416_INCOMING_DATA.md  (corrected: XDNA 2 → XDNA 1)
```

## What happened, chronologically

1. **RGB didn't work.** Banked fix in `feedback_llt_rgb_classification.md` from a prior session said patch LLT `MachineTypeMap` to add `83Q7 → LegionSeries.LOQ`. I didn't check memory first, spent an hour re-deriving it. **Grade F on that; the fix was already written.** After applying the banked patch: RGB works via Spectrum per-key.
2. **Laptop crashed.** Hard EC thermal cutoff at 02:04 UTC (Kernel-Power 41, no BugCheck, no WHEA, no minidump — that signature = EC trip, not software). Root cause: `dGPU always on` + Lenovo Vantage + LLT double-managing thermal mode left the EC unattended. Fix: uninstall Vantage, Hybrid Mode ON in LLT so dGPU sleeps when idle, install HWiNFO64 for live telemetry.
3. **Corrected the handoff's XDNA-2 error.** On-machine `Win32_VideoController` shows Radeon 780M → Hawk Point, not Strix Point. Corrected handoff throughput table and banked authoritative hardware spec.
4. **Installed Ryzen AI SDK 1.7.1.** AMD's `-lt` installer silently aborts with exit 0 when conda isn't on **Machine** PATH (elevation strips User PATH). Fixed by pre-installing Miniforge, adding its Scripts+condabin to Machine PATH (elevated), then running the installer interactively. `quicktest.py` passes, prints `Setting environment for PHX/HPT`.
5. **Published this repo.** Both the curated reusable scripts (top-level `ryzen-ai/`, `legion-toolkit/`, `diagnostics/`) and the raw session trail (`raw-scripts/`, `docs/`, `handoffs/`) so the next time any of this goes sideways the full investigation is one clone away.

## Order of operations on a fresh LTSC box

```
# 0. Basic LTSC enrichment (not in this repo yet — see docs/project_ltsc_autoscript.md)
#    MS Store + winget via kkkgo-style Appx sideload, Widgets Platform, drivers.

# 1. Miniforge (user scope is fine; we'll fix PATH next)
winget install --id CondaForge.Miniforge3 --silent --accept-source-agreements --accept-package-agreements

# 2. Add conda to MACHINE PATH so AMD's UAC-elevated installer can see it
.\ryzen-ai\add-conda-to-machine-path.ps1

# 3. Download ryzen-ai-lt-*.exe from account.amd.com (click-through form; can't curl)
#    URL: https://account.amd.com/en/forms/downloads/xef.html?filename=ryzen-ai-lt-1.7.1.exe

# 4. Launch installer with conda visible
.\ryzen-ai\install.ps1

# 5. Persist install path to Machine scope
.\ryzen-ai\persist-env.ps1

# 6. Verify
.\ryzen-ai\postinstall-verify.ps1
.\ryzen-ai\quicktest.ps1

# 7. Monitoring for the thermal-sensitive chassis
winget install --id REALiX.HWiNFO --silent --accept-source-agreements --accept-package-agreements

# 8. Legion 5 15AHP11 only: uninstall Lenovo Vantage (Settings → Apps)
#    then swap LLT for the Team fork and drop the RGB patch
.\legion-toolkit\llt-upgrade-to-fork.ps1
.\legion-toolkit\rgb-patch-deploy.ps1   # uses legion-toolkit/patched-dll/LenovoLegionToolkit.Lib.dll
# then in LLT UI: Dashboard → Hybrid Mode → On. Power Mode → Balanced or Quiet.
```

## Gotchas worth remembering (full list in docs/reference_spambook_hardware.md)

- **AMD installer requires conda on Machine PATH, not User.** UAC-elevated processes don't see User PATH.
- **`ryzen-ai-lt-*.exe` does not bundle Miniforge.** Pre-install it.
- **Download is export-gated.** `account.amd.com/en/forms/downloads/xef.html` requires an AMD account login.
- **Silent install masks prerequisite failures.** Exit 0 with zero install when conda was missing. Run interactively the first time on a new box.
- **Error log lives at `C:\Temp\ryzen_ai_<timestamp>.txt`** when the installer aborts.
- **BartoszCichecki/LenovoLegionToolkit is archived** (since 2025-07-24). Pull LLT from `LenovoLegionToolkit-Team/LenovoLegionToolkit` instead; winget still points at the archived version.
- **Lenovo Vantage and LLT cannot coexist.** Vantage claims the hardware interface and LLT's RGB card goes dark. Uninstall Vantage.
- **`83Q7` isn't in LLT's default MachineTypeMap.** Patch `Compatibility.cs` to add `{ "83Q7", LegionSeries.LOQ }` (keyboard PID 0xC615 → LOQ prefix 0xC600). See `legion-toolkit/README.md`.
- **`dGPU always on` + RTX 5060 + Ryzen 7 250 combined load can trip the EC thermal cutoff** on this chassis. Hybrid Mode ON is mandatory for daily use.

## Intended audience

Pono Data Solutions internal. Private repo. Scripts are PowerShell 5.1 compatible. Admin self-elevates via UAC where required.
