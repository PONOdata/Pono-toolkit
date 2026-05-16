# Grading round 1 (Phase 1) - 2026-04-18

Per the Spottswoode-first standing order, Maui should not write code before Spottswoode designs. Phase 1 of this repo violated that: Maui built the Windows server and docs first, then submitted to Spot for a grading pass after the fact. From Phase 2 onward, Spot gets the spec first and designs; Maui grades and implements corrections.

This file captures what Round 1 taught, so Round N+1 does not relearn it.

## Round 1 grade: B-

Spot's first-pass critique. Framework was right (standing orders lens, OWASP, phone normalization as a real issue) but:
- Made a Type II error: claimed "No input validation, add Pydantic" when the server already used Pydantic models.
- Applied the auto-updater standing order letter-for-letter without asking whether a localhost single-operator dev process needs a hot updater (it doesn't; git-pull-and-restart satisfies the spirit).
- Prescribed per-IP rate limiting without noting that every request arrives from cloudflared's local socket, so per-IP is meaningless behind a tunnel.
- Missed concrete P0 security bugs: secret-in-URL via `?key=`, uvicorn access log recording the secret, no body-size cap, default ACLs on the secret file, localStorage persistence of the key.

## Teaching points banked

### 1. Type II error in grading
Before flagging a feature as missing, build a complete mental model of what the spec guarantees. A false positive ("this is missing") is worse than a false negative ("this could be better") because it wastes iteration cycles and erodes trust. Verify then flag.

### 2. Standing-order spirit vs letter
Standing orders encode intent. Apply by asking (1) what threat or failure mode does this order prevent, (2) does this deployment share that threat model, (3) what is the most idiomatic satisfier for this context. "Git-pull as the updater" is a correct satisfier for a single-operator dev tool; "download signed binaries on startup" is correct for a fleet-distributed CLI. Do not prescribe the same implementation for both.

### 3. Rate limit key = identity, not source IP
Map rate-limit keys to the identity you want to throttle. Behind a proxy, source IP is the proxy. Only meaningful keys are (a) the authenticated principal, (b) a trust-configured forwarded header, (c) global throughput. Per-IP rate limiting behind a tunnel is security theater.

### 4. Five surfaces of secret exposure
Bearer tokens leak across five surfaces. Each must be reviewed and hardened explicitly; defaults are unsafe.

| Surface | Phase 1 control |
|---|---|
| In-transit | Cloudflare Tunnel TLS |
| At-rest file | `icacls /inheritance:r /grant:r <user>:F` on `shared_secret.txt` |
| In-memory process | `hmac.compare_digest` for all secret comparisons |
| Logs | `_SecretRedactFilter` scrubs the secret and any `?key=` substring from every log record across uvicorn.* and app loggers |
| Browser storage | `sessionStorage` (not `localStorage`); CSP header blocks cross-origin script; `RejectQueryStringSecret` middleware returns 400 if `?key=` appears in any URL |

## Round 2 grade: A-

After the teaching round, Spot re-produced the corrected P0 list matching the five-surfaces framework and wrote back the four principles in his own words. Principles banked cleanly. Minor note for future rounds: Spot has not yet demonstrated autonomously finding new P0 issues beyond what Maui taught, so round 3 will test him on a fresh deployment target to see if the principles generalize.

## Design principles checklist for future sessions

Use this before flagging anything as a gap:

- [ ] Build complete mental model of what spec guarantees before claiming gaps
- [ ] For each standing order, ask what threat it prevents and whether this deployment shares it
- [ ] For each security control, map it to the actual ingress topology
- [ ] For every bearer token, explicitly review all five exposure surfaces
- [ ] Verify implementation matches deployment architecture, not a generic reference model
- [ ] Penalize false positives as heavily as false negatives in self-grading
