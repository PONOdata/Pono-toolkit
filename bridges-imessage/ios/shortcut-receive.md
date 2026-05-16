# iOS Shortcut: forward incoming iMessages to Windows

This Shortcut fires when a message arrives on your iPhone and POSTs the sender + body to your Windows server. It's how Phase 1 bridges the inbound direction without a Mac or a jailbreak.

Requires **iOS 17 or newer** (earlier iOS versions don't expose the "When I get a message" automation trigger).

## Prerequisites

- Windows server running (`windows/run.ps1`)
- Cloudflare Tunnel or ngrok exposing `http://127.0.0.1:8765` at a public HTTPS URL
- Your `PONO_IMESSAGE_KEY` value (printed by `setup.ps1`; also stored at `%LOCALAPPDATA%\pono-imessage\shared_secret.txt`)

## Build the Shortcut

Open the **Shortcuts** app on your iPhone. Create a new **Personal Automation** (Automation tab > + > Personal Automation).

### Trigger

- **When I get a message**
- **From:** Any / specific contacts (your call; "Any" gets everything)
- **Contains:** leave blank (or add a filter if you want only specific text)
- Set **Run Immediately** (otherwise iOS asks permission each time, which defeats the point)
- Set **Notify When Run** off (optional, cleaner)

### Actions (in order)

1. **Get Contents of URL**
   - URL: `https://<your-tunnel>.trycloudflare.com/inbound`
   - Method: `POST`
   - Headers:
     - `X-Pono-Key` : paste your shared secret
     - `Content-Type` : `application/json`
   - Request Body: **JSON**, with the following keys (use the magic-variable picker for each value):
     - `sender`         -> **Sender** (from trigger)
     - `body`           -> **Message** (from trigger)
     - `ios_timestamp`  -> **Current Date** (or leave empty; server stamps its own)
     - `is_group`       -> false (Shortcuts doesn't expose group-detection directly; Phase 2 will add smarter threading)

2. **(Optional) Show Notification** "Sent to pono bridge" — useful while testing, remove once you trust it.

### Test

Send yourself an iMessage from another device. Within a couple of seconds the Shortcut should fire and the Windows server log will print:

```
stored message id=1 thread=+15555551234 sender=+15555551234 bytes=42
```

Refresh `http://127.0.0.1:8765/?key=<your-key>` in your browser. The thread and the message should appear.

## Troubleshooting

- **Nothing happens.** iOS 17 quirk: the automation has to be toggled ON in Automation > your automation > Enable. Re-check after each iOS update; updates sometimes flip it off.
- **401 from server.** Your `X-Pono-Key` header value doesn't match the server's. Copy-paste from `%LOCALAPPDATA%\pono-imessage\shared_secret.txt` again. Watch for trailing whitespace.
- **Tunnel URL changes on each cloudflared run.** That's expected for the free ad-hoc tunnel. Either paste the new URL into the Shortcut each time, or set up a named tunnel on your Cloudflare zone for a stable hostname.
- **Automation says "Tap Run" instead of firing silently.** Turn off "Ask Before Running" in the automation detail view.

## Privacy

Everything iMessage sends through this bridge sits in `%LOCALAPPDATA%\pono-imessage\messages.db` in plaintext. If that's not acceptable, wait for Phase 2 which adds at-rest encryption using a key derived from the shared secret + a machine-local salt.

## Phase 2 preview (outbound)

Phase 2 adds a Swift companion app that receives reply text from Windows over Bonjour (local Wi-Fi) and uses `sms:` URL scheme to open Messages pre-filled. You tap send on the phone. iOS sandboxing blocks true auto-send without jailbreak.
