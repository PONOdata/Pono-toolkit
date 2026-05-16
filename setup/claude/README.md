# claude/

Auto-installer for Claude on new Pono fleet nodes.

## Status: on hold

`install.ps1` is a skeleton. It currently exits immediately with code 3. Do not run it yet.

## Why on hold

A stock Claude install has no Pono memory, no skills, no PAL wiring, no fleet awareness. Running `npm install -g @anthropic-ai/claude-code` on a new node gives you a blank Claude that can't do anything Pono-specific. The point of `windows-pono` is to stand up a _node_ that's ready to plug into the fleet, not just a box with Claude on it.

What needs to land before this script goes live:
1. The Pono Claude training deliverable, whatever form it takes (probably a bundle of: `~/.claude/memory/`, `~/.claude/skills/`, `~/.claude/plugins/`, PAL tokens, MCP server config).
2. A deterministic way to drop that bundle into a fresh install: verify hashes, decrypt, extract, re-register MCP servers, validate PAM checkin.
3. An answer to credential delivery: the installer must not embed secrets. PyArmor keys, PAL API keys, PAM tokens need a secure out-of-band path per the repo's standing "No plaintext secrets in code" rule.

## What's here

- `install.ps1` - skeleton with the four install phases sketched out (Node.js, Claude Code CLI, Claude Desktop, bundle restore). First line aborts. Remove the banner + exit once the bundle format is decided.
- `bundle/` (not yet present) - will hold the Pono memory/skills bundle when training lands.

## Until then

For the old-laptop-to-Frodo-node provisioning, run the hardware-side scripts (`drivers/scan-drivers.ps1`, `ryzen-ai/*`, `legion-toolkit/*` if Legion) to get the OS ready, then hold on Claude install until the bundle exists.
