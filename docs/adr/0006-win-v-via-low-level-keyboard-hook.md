# 6. Claim Win+V with a low-level keyboard hook

- Status: accepted
- Date: 2026-06-15
- Deciders: project maintainers
- Tags: architecture, infra

## Context

ClipVault summons its history with **Win+V** to match the muscle memory of the
built-in clipboard history. On current Windows 11 builds Win+V is reserved by the
shell, and `RegisterHotKey` cannot claim it — the registration silently fails to fire.
The project also forbids any persistent OS footprint (see
[SECURITY.md](../../SECURITY.md)).

## Decision

Claim Win+V with a `WH_KEYBOARD_LL` low-level keyboard hook (`LowLevelKeyboardHook`,
driven by `TrayHotkeyController`). The hook is installed at runtime and released on
exit; nothing is registered persistently with the OS.

## Consequences

- The desired Win+V chord works despite the shell reservation.
- No persistent footprint: the hook exists only for the process lifetime, consistent
  with the no-footprint stance.
- A low-level hook is global and runs on every keystroke, so the callback must stay
  fast and non-blocking.
- While ClipVault runs it swallows Win+V, suppressing the OS clipboard-history popup —
  an accepted trade-off for owning the chord.

## Alternatives considered

- **`RegisterHotKey`.** Rejected: cannot claim the shell-reserved Win+V on current
  Windows 11 builds.
- **A different, non-reserved chord.** Rejected: breaks the Win+V expectation the
  whole interaction is built around.
- **Virtual-desktop / shell shims.** Rejected: unsupported and fragile.

## References

- [SECURITY.md](../../SECURITY.md) — no persistent footprint.
- `src/ClipVault.App/Platform/LowLevelKeyboardHook.cs`,
  `Platform/TrayHotkeyController.cs`.
