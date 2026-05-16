# Architecture

## Topology

```
    +-----------------+                        +---------------------+
    |   iMessage      |                        |  Windows box        |
    |   (Apple cloud) |                        |  (this repo)        |
    +--------+--------+                        +----------+----------+
             |                                            ^
             | encrypted, Apple-native                    | HTTPS POST /inbound
             v                                            | X-Pono-Key header
    +-----------------+        +----------------+         |
    |   iPhone        |        |  Cloudflare    |         |
    |   iOS 17+       +------->+  Tunnel /      +---------+
    |   Shortcuts app |  POST  |  ngrok         |
    +-----------------+        +----------------+
          ^   |
          |   | Phase 2: Bonjour / BLE
          |   v
    +-----------------+
    | Swift companion |  Phase 2 only. Receives outbound
    | app (sideload)  |  reply text from Windows, opens
    +-----------------+  Messages.app pre-filled.
```

## Data flow (Phase 1, inbound only)

1. iPhone receives an iMessage.
2. iOS triggers the "When I get a message" automation (user-built Shortcut).
3. Shortcut serializes sender + body + optional timestamp to JSON.
4. Shortcut POSTs to the Cloudflare Tunnel (public HTTPS).
5. Tunnel forwards to Windows localhost:8765.
6. FastAPI server:
   - Validates `X-Pono-Key` in constant time.
   - Computes a SHA-256 over sender|ts|body for idempotent dedup.
   - Inserts into SQLite (unique index on the SHA).
   - Returns 201 + message id, or 200 + existing id on dedup hit.
7. Browser at `/` polls `/threads` every 5s and renders conversations.

## Why these pieces

| Piece | Choice | Why |
|---|---|---|
| Inbound trigger | iOS Shortcuts automation | Only legal way to read iMessage content from iOS without Apple hardware or jailbreak. |
| Transport | Cloudflare Tunnel | TLS, stable-ish hostname with a named tunnel, free, already installed per `windows-pono`. |
| Server | FastAPI + uvicorn | Async, pydantic validation, OpenAPI docs for debugging. |
| Storage | SQLite (WAL) | Zero-ops, file-backed, survives reboots, queryable without a running service. |
| UI | Embedded HTML + vanilla JS | No build step. Polls for updates. Good enough for Phase 1. |
| Auth | Shared secret header (HMAC compare) | Simple, travels inside the TLS envelope. Phase 2 upgrades to HMAC-signed requests with nonce. |

## What this does not do (yet)

- **Outbound send.** Phase 2.
- **Attachments, reactions, tapbacks.** Phase 3+.
- **True threading with Apple message IDs.** The Shortcut trigger doesn't expose Apple's iMessage thread GUIDs, so we thread by sender phone/email. Group chats end up lumped into one-per-sender threads until Phase 2 adds a group-aware Shortcut + payload schema bump.
- **Read receipts / typing indicators.** Not exposed to Shortcuts.
- **End-to-end encryption of stored messages.** Phase 2 adds at-rest encryption keyed off shared secret + machine salt.

## Security posture (Phase 1)

Threat model assumes:

- TLS is enforced by Cloudflare Tunnel (attackers can't sniff in transit).
- The shared secret is long enough (`secrets.token_urlsafe(32)` = 256 bits) that brute force is infeasible.
- The tunnel URL is not trivially discoverable. (A determined attacker who finds your tunnel URL still needs the secret.)

Known limitations:

- Server logs the first 80 chars of bodies at INFO. Log rotation is on the operator (you).
- SQLite is plaintext on disk. Encrypt the LOCALAPPDATA path or wait for Phase 2.
- No rate limiting. The trycloudflare free tunnel has its own ceiling; don't expose long-term at scale.

## Phase 2 plan

- Swift iOS companion app: receives outbound reply from Windows over local Bonjour (`_ponoimessage._tcp`). Opens Messages with `sms:+<number>&body=<text>`. User taps send.
- Windows server gains a `/outbound` endpoint the UI calls; it pushes to the companion app via the Bonjour channel.
- At-rest encryption of `messages.db` (SQLCipher or libsodium-derived).
- HMAC-signed requests with nonce to replace the raw shared-secret compare.

## Phase 3 plan (jailbreak territory)

- Only path to true auto-send without tapping the phone.
- Requires a jailbroken iPhone with a tweak that intercepts `sms:` URL schemes and auto-taps send.
- Out of scope for non-jailbroken operators; document but do not implement in the main branch.
