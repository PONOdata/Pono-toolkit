# Limitations and honest caveats

## What iOS will not let us do (without jailbreak)

- **Auto-send iMessage from a third-party app.** The `sms:` URL scheme opens Messages pre-filled but never sends automatically. Apple has blocked this path since iOS 7. MDM profiles and enterprise provisioning profiles do not change it.
- **Read iMessage content from a background daemon.** The Messages.app sandbox does not share its database with anything else on the device. The ONLY current-OS-supported way to observe incoming messages is the Shortcuts "When I get a message" automation.
- **Intercept outgoing iMessages.** Same sandbox. The outgoing-message path is fully contained inside Messages.app.
- **True thread GUIDs / message IDs.** Shortcuts does not expose Apple's internal iMessage thread identifiers. We derive threading from sender identity, which breaks when you iMessage with the same person across multiple handles (phone + email + Apple ID).

## What iOS Shortcuts does not expose in the trigger

Varies by iOS version and is partially undocumented. As of iOS 17:

- Sender handle (phone or email)
- Message text
- Trigger-local timestamp (the time the Shortcut fires, not the message's iMessage timestamp)

Not exposed:

- Attachment payloads (images, videos, audio)
- Reactions (tapbacks)
- Reply-to parent message id
- Group chat membership / group title
- Whether the message was SMS or iMessage (the trigger fires for both interchangeably)
- Read status

## What Apple does that breaks reverse-engineered clients

Since Beeper Mini launched in late 2023:

- Apple rotated APNs registration requirements multiple times, each breaking direct-protocol clients for days to weeks.
- Apple added proof-of-hardware checks tied to validation data derived from real Mac/iPhone devices ("nac" blobs).
- Apple began server-side account flagging: accounts that hit registration endpoints from non-Apple hardware sometimes get their iMessage temporarily disabled.

This is why the companion-app approach (reading through Shortcuts, replying through `sms:`) is more durable than direct protocol: Apple never changes the iOS-app-contract as aggressively as it changes the APNs-protocol contract.

## What this project does not try to be

- A replacement for the Messages app.
- A commercial-grade bridge. Beeper exists for that.
- A drop-in for enterprise iMessage compliance. Use Apple Business Messages if you need that.

## If any of this changes

- **Shortcuts "When I get a message" trigger gets more fields:** Phase 1 gains them automatically; update the shortcut-receive.md doc.
- **Apple adds a first-party Messages-on-Windows experience:** use theirs, archive this repo.
- **Someone publishes a maintained community iMessage protocol library that works on non-Apple hardware:** consider a Phase 4 that swaps out the companion app for the direct-protocol implementation. Keep the Phase 1+2 code as a fallback since Apple will eventually break the direct approach again.
