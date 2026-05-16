# companion-app/ - Phase 2 scaffold

Swift iOS app that bridges the outbound direction: Windows pushes reply text over Bonjour on the local network, the app opens Messages pre-filled with the `sms:` URL scheme, and you tap send on the phone. One tap is the best iOS will give us without jailbreak.

## Status

**Scaffold only.** No Xcode project yet. Build plan below so the next session can start cold.

## Build plan

### Phase 2.0 (MVP)

Target: **iOS 17+**, SwiftUI app, Bonjour service + URL scheme opener.

1. Create Xcode project on MacinCloud (LA645 per `reference_macincloud.md` in the Spot memory). Template: App > SwiftUI > iOS.
2. App structure:
   - `PonoBridgeApp.swift` - `@main`, App body
   - `BonjourListener.swift` - advertises `_ponoimessage._tcp` via `NWListener`, accepts one Windows client at a time
   - `MessageForwarder.swift` - receives `{ "to": "+15555551234", "body": "hello" }` payloads, constructs `sms:+15555551234&body=<percent-encoded>`, calls `UIApplication.shared.open(url)` which presents Messages.app with the text pre-filled
   - `ContentView.swift` - minimal UI showing "Connected to <windows hostname>" + recent outbound drafts for audit
3. Info.plist:
   - `NSLocalNetworkUsageDescription` - required prompt for Bonjour
   - `NSBonjourServices` array with `_ponoimessage._tcp`
4. Auth: same shared secret as Phase 1 (`PONO_IMESSAGE_KEY`). Phone stores it in Keychain after a one-time QR pairing from the Windows UI.
5. Sideload: Apple Developer account (free tier works for personal sideload on your own device, 7-day provisioning profile renewals). For longer provisioning, use Pono's paid Developer team if one exists; otherwise live with the weekly re-sideload cycle.

### Phase 2.1 (polish)

- Shortcut integration: user long-presses a draft in the companion app to run a Shortcut that pastes the draft into Messages compose (skips the URL-scheme round trip).
- Background refresh: prewarm the Messages URL handler so the tap-to-send feels instant.
- Group chat support: detect the incoming message's thread type and route to the correct Messages thread via `imessage://` URL scheme (if still supported on target iOS).

## Directory layout (when built)

```
companion-app/
├── README.md                   (this file)
├── PonoBridge.xcodeproj/
├── PonoBridge/
│   ├── PonoBridgeApp.swift
│   ├── BonjourListener.swift
│   ├── MessageForwarder.swift
│   ├── ContentView.swift
│   ├── Assets.xcassets/
│   └── Info.plist
└── PonoBridgeTests/
```

## Why Bonjour and not HTTP

- Zero-config on the local network. The Windows side just advertises + connects by service name, no IP configuration.
- Works over Wi-Fi or personal hotspot. If the phone is within Bluetooth range but not Wi-Fi, fall back to a BLE GATT characteristic (Phase 2.2).
- Apple's `Network.framework` supports this natively with a clean API on both ends.

## Why `sms:` and not `imessage:`

Both URL schemes exist. `imessage:` historically worked but is inconsistently documented and has been deprecated in places. `sms:` is stable, forces Messages.app to open the compose sheet, and on an iPhone with iMessage enabled for the recipient, it sends as iMessage (not SMS). For SMS-only recipients, it sends as SMS. That's the user's call when they tap send.

## iOS sandbox hard wall

Even with the companion app, the **user must tap send**. `UIApplication.shared.open(sms:...)` opens the compose sheet; it cannot dispatch it. Private APIs exist (private framework calls into MFMessageComposeViewController internals) but ship with App Store rejection and, when sideloaded, work until Apple flags the hardware UUID. Out of scope for the main branch; documented in `../docs/limitations.md`.
