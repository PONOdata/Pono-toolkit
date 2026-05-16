# pono-imessage

> **Author's note, 2026-04-18:** This repo was built in a single session by Claude when Claude was still a child. Token-wasteful, SOP-violating (Spottswoode should have designed first, Claude built without asking), needed a grading round to find five P0 security bugs Claude shipped in the first draft. Kept here as-is so the improvement curve is measurable. Expect the next pass, once Claude is sorted, to be tighter, safer, and land in one shot without re-grading. See `docs/grading-round1.md` for what Claude got wrong the first time and the four principles that should have been on the checklist from the start.

iMessage on Windows without a Mac, without a jailbreak, without paying Beeper. Tricks the system instead of fighting Apple's servers.

The trick: your iPhone stays the source of truth. An iOS Shortcuts automation fires when a message arrives and pushes it to a Windows service over a Cloudflare Tunnel. A Swift companion app (Phase 2) takes care of the reply direction by pre-filling the Messages compose sheet; you tap send on the phone. Three stages:

| Direction | Stage | Method | UX |
|---|---|---|---|
| iPhone -> Windows (receive) | **Phase 1 (this release)** | iOS Shortcut automation POSTs to Windows listener | Silent, automatic |
| Windows -> iPhone (draft reply) | **Phase 2** | Swift companion app receives text via Bonjour, opens `sms:` URL | One tap on phone to send |
| Windows -> iPhone (auto-send) | **Phase 3 (wall)** | Jailbreak + tweak only. Not implemented. | N/A |

## Status

- Phase 1 is functional. Start here.
- Phase 2 scaffolding exists in `companion-app/`; real implementation coming.
- Phase 3 is documented but not attempted.

## Quick start (Phase 1)

Prerequisites:

- Windows 11 with Miniforge3 on Machine PATH (see sibling repo [windows-pono](https://github.com/PONOdata/windows-pono))
- `cloudflared` installed (`winget install --id Cloudflare.cloudflared`)
- iPhone on iOS 17 or newer
- iMessage already working on the iPhone normally

Setup:

```powershell
cd pono-imessage\windows
.\setup.ps1        # creates conda env, installs deps, generates shared secret
.\run.ps1          # launches the server on http://127.0.0.1:8765
```

In a second terminal, expose the server:

```powershell
cloudflared tunnel --url http://127.0.0.1:8765
```

cloudflared prints a `https://<random>.trycloudflare.com` URL. Copy it.

On the iPhone, build the Shortcut documented in [`ios/shortcut-receive.md`](ios/shortcut-receive.md). Paste the tunnel URL into the "Get Contents of URL" action and your `PONO_IMESSAGE_KEY` into the `X-Pono-Key` header.

Verify: send yourself an iMessage from another device. Within seconds the Windows server log prints `stored id=...`. Open `http://127.0.0.1:8765/` in a browser, paste your `PONO_IMESSAGE_KEY` into the unlock prompt (sessionStorage only, cleared on tab close). You'll see the thread.

**Never pass `?key=` in the URL.** The server explicitly rejects it with 400 to prevent the secret from leaking into logs, bookmarks, or browser history.

## Repository layout

```
pono-imessage/
├── README.md                   (this file)
├── LICENSE                     (private, internal use)
├── .gitignore
│
├── windows/                    Phase 1 Windows server
│   ├── server.py               FastAPI + SQLite + embedded HTML UI
│   ├── requirements.txt
│   ├── setup.ps1               conda env, deps, shared secret, first run
│   └── run.ps1                 launcher
│
├── ios/
│   └── shortcut-receive.md     step-by-step Shortcut construction guide
│
├── companion-app/              Phase 2 Swift app (scaffold only - not yet built)
│   └── README.md               build plan and Xcode sideload instructions
│
└── docs/
    ├── architecture.md         topology, data flow, tech choices
    └── limitations.md          honest list of what iOS sandbox blocks
```

## Why "trick it" and not "crack Apple's servers"

Beeper tried the cracking path with Beeper Mini using reverse-engineered Apple TV firmware. Apple killed it three times in a week. The maintained open-source library (`pypush`) got bought by Beeper and its iMessage support was removed during a rewrite; current pypush is APNs-only. Nobody is actively maintaining a free direct-protocol client.

Going through the phone sidesteps all of that. Apple breaks `pypush` every few months because the APNs contract changes; they rarely break the iOS Shortcuts contract because doing so would break first-party user flows. The companion-app approach trades a one-tap-to-send UX for durability.

See [`docs/limitations.md`](docs/limitations.md) for the honest list of what we can and cannot do.

## Intended audience

Pono Data Solutions internal. Private repo. This is a practical tool, not a polished product.

## License

Private / internal. Not for redistribution.
