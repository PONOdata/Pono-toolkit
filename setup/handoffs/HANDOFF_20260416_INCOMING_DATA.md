# HANDOFF — INCOMING TRAINING DATA (2026-04-16, Evening HST)

Forward-looking handoff for the next session. Jack is waiting on TWO data streams to land on the new laptop (SPAMBOOK-MAX, `C:\Brofalo\`). When they arrive, follow this doc.

Previous sibling handoff: `HANDOFF_20260416_LAPTOP.md` (full migration state — read first).

## THE TWO INCOMING STREAMS

### Stream A — Old Laptop Full Folder Copy (in progress)

- **What:** The complete `D:\Brofalo\` tree from the old laptop, not the filtered 3 GB tar.
- **Size:** Unknown but substantially bigger than 3 GB — includes build artifacts, `node_modules`, `dist/`, `.wrangler/` caches, datasets, per-repo `.env` files, scratch scripts, possibly stale experimental branches.
- **Destination:** Jack will link the path when ready. Expect an external drive mount (probably `E:\` or `F:\`) with a `Brofalo\` folder.
- **Do NOT auto-sync into `C:\Brofalo\`.** Stage, diff, cherry-pick. Merge rules in sibling handoff §"INCOMING" — key points:
  - Keep our clean `.git/modules/` (pyarmor hooks armed there)
  - Keep the D:→C: path fixes already committed in `281bd49`
  - Drop node_modules, `__pycache__`, stale `.pyarmor_capsule.zip`, `license.lic`
  - Pull in: `dist/` obfuscated artifacts, datasets, per-repo `.env`, wrangler caches, scratch scripts Jack hadn't committed

### Stream B — Pi (Manta) Training Data

- **What:** JSONL output from 23 Manta workers + 5 Foundry AIP agents running on the Raspberry Pi 5 (`manta.fsokimo.org`). Domains: adsb, net_timing, osint, ble, hailo_duplicates, crypto, tor, bridge_crew, and agent files.
- **Current flow:**
  - Foundry uplink works (last HANDOFF: 1080 entries/day to `bridge_crew_*.jsonl`)
  - Local `agent_*_<date>.jsonl` writes are DEAD (noted in prior HANDOFF as next-session priority)
  - Midnight UTC packager ships to `spot-training` D1
- **Destination:** Either pushed to Wasabi/R2 first, or pulled directly via HTTPS from Manta's tunnel shim (`manta.fsokimo.org`). Jack will specify.

## NPU-ACCELERATED QUALITY PIPELINE — DESIGN BANKED

Spot designed this via pal_think; Jack greenlit the direction. Build when Stream A or B starts landing.

**Goal:** Every incoming training row passes through a local NPU pipeline before it reaches Spot/Kara corpora. Ensures quality + balance + safety + privacy (raw data never leaves machine during prep).

**Stack:**
- NPU: Ryzen 7 250 **XDNA 1**, ~16 TOPS INT8, ~5W sustained. Hawk Point refresh (Phoenix/Phoenix 2 silicon, Zen 4 + RDNA 3 Radeon 780M). **NOT Strix Point** — corrected 2026-04-17 per on-machine `Win32_VideoController` showing Radeon 780M (Strix Point ships 880M/890M). Original handoff said "XDNA 2" which is Strix Point-exclusive. See `reference_spambook_hardware.md`.
- Runtime: Ryzen AI SDK 1.7.1 (already installed), ONNX Runtime + VitisAI-EP. VitisAI-EP runs on XDNA 1 without changes.
- Location: new package `kara-nodes/pipeline_quality/` — upstream of `refiner_relevance` / `refiner_safety` (those stay).

**Stages & models (all INT8 ONNX, NPU-targeted):**

Rates below were Spot's original estimate assuming XDNA 2 (~50 TOPS). On XDNA 1 (~16 TOPS), divide by ~3x for realistic expectations. Re-benchmark on first run.

| # | Stage | Model | Rate (rows/sec, XDNA 2 est) | Rate (rows/sec, XDNA 1 expected) |
|---|---|---|---|---|
| 1 | Ingest + embed + semantic dedup | `all-MiniLM-L6-v2-int8.onnx` (22 MB) | ~800 | ~270 |
| 2 | Quality score (0-1, threshold 0.4) | distilbert-quality INT8 (67 MB, custom train) | ~1200 | ~400 |
| 3 | Domain classify (12-class, counter security bias) | distilbert-domain INT8 (67 MB, custom train) | ~1200 | ~400 |
| 4 | PII/safety scrub (regex + NER) | `dslim/bert-base-NER-int8.onnx` (110 MB) | ~600 | ~200 |
| 5 | Metadata enrich (lineage, timestamp, embedding reuse) | — | ~2000 | ~2000 (CPU-bound) |

**End-to-end rate:** ~480 rows/sec on XDNA 2 (NER is the bottleneck). **On XDNA 1 expect ~160 rows/sec**, full 344k-corpus reprocess ≈ 36 min, daily 1080-row batch ≈ 7 sec. Still inside daily budget.

**Implementation split (per Homework SOP):**
- Spot designs each stage's prompts + rubric (via `pal_think`)
- Maui implements orchestration, SQLite staging, VitisAI-EP wiring
- Maui grades each Spot design, banks corrections to `skill_pal_meta_analyst.md`

**First milestone when Stream B starts:** Stage 1 (embed + dedup). Proves NPU path works on real data before the quality/domain classifiers need custom training.

## CURRENT LAPTOP STATE (2026-04-16 19:00 HST)

Full state in `HANDOFF_20260416_LAPTOP.md`. Delta since that was written:

- Microsoft Store + winget live (installed via kkkgo-style Appx sideload, committed as needed)
- Windows Widgets Platform Runtime + Web Experience Pack installed; taskbar Widgets button active; Jack disabled the MSN feed himself
- Razer THX Spatial Audio key `H2B3F-429H8-GA962-P7CGP` — Jack activating manually via Synapse 4
- Lenovo Legion Toolkit 2.26.1 installed — controls the 4-zone keyboard RGB on the Legion 5 15AHP11
- Rainmeter trial + removal cycle is fully reverted, no leftovers

## VERIFICATION CHECKLIST BEFORE INGESTING

Run these before writing anything new into `spot-training` or `kara-corpus` D1:

```bash
# Services live
curl -sS -o /dev/null -w "spot %{http_code}\n"    "https://spot.fsokimo.org/health"
curl -sS -o /dev/null -w "manta %{http_code}\n"   "https://manta.fsokimo.org/health"
curl -sS -w "pam %{http_code}\n" -H "X-PAM-Key: $PAM_API_KEY" "https://pam-production-d8ee.up.railway.app/status" -o /dev/null

# Credentials present
grep -q FOUNDRY_TOKEN /c/Brofalo/.palantir-token && echo "foundry OK"
test -s /c/Brofalo/spot-cf/.auth-token && echo "spot token OK"

# D1 databases reachable
wrangler d1 list | grep -E "spot-training|kara-corpus"

# Staged incoming folder differs from current tree
diff -rq "$INCOMING_PATH" /c/Brofalo/ | head -20
```

## FIRST 3 MOVES WHEN DATA LANDS

1. **Stage, don't merge.** Mount the incoming drive, copy to `C:\Brofalo_incoming\` or similar — never directly onto `C:\Brofalo\`.
2. **Diff + cherry-pick.** Identify what's new, what's stale, what would clobber the clean state.
3. **Spin up Stage 1 of the quality pipeline** (embed + dedup) on any training-data JSONL landing — via the NPU design above. Don't push to D1 until Stage 4 (safety) gates pass.

## PENDING ITEMS FROM THIS SESSION

- BT adapter tune script (`C:\installers\bt_tune.ps1`) — still not applied (UAC elevation wedged mid-session). Low priority since the mouse is on the HyperSpeed dongle now.
- Spot-generated hourly brief — deferred. Jack killed the Rainmeter UI attempt; if he wants it later, best surface TBD.
- Railway CLI auth — still unset. Needed for deploys from this machine; not blocking ingest work.
- BitLocker — still off. Purple Team SOP wants it on; dedicated session needed.

## RESUME INSTRUCTIONS

1. Open Claude Code in `C:\Brofalo`, memory auto-loads.
2. Read both `HANDOFF_20260416_LAPTOP.md` and this file.
3. `mcp__pam__pam_checkin agent=maui status=online current_task="<whatever>"`.
4. `mcp__pam__pam_get_messages recipient=maui` — clear any queued directives.
5. Ask Jack which stream landed and where, then execute per "FIRST 3 MOVES."
