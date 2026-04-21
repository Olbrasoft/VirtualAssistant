# Paste into Claude Code (inside tmux) — the recurring "No image found in clipboard" bug

**Status:** Actively managed. A structural fix landed in PR #953 (2026-04-16)
and a root-cause gdbus-parse fix landed in PR #1048 (2026-04-21, closes #1047)
— but the class of bugs this document describes keeps recurring, so any future
regression should start here instead of from scratch.

> **⚠️ Service-restart fallacy.** Right after a `systemctl --user restart
> virtual-assistant.service` (or an auto-deploy) the system *always* looks
> healthy for a few minutes — the test environment is reset, caches are
> cold, the first few interactions go through the happy path. That tells
> you nothing about whether the underlying regression is gone. Trust the
> log signals in the debug checklist, not the post-restart vibe check.

---

## TL;DR

Pasting text into a TUI agent (Claude Code is the worst offender, but OpenCode
and Gemini CLI have the same shape) running **inside tmux** intermittently
breaks:

- The Remote Control's **"Vložit ze schránky"** button does nothing in Claude
  Code, or pastes *something other* than what the user copied.
- After a **dictation**, the wrong text lands in Claude Code — very often the
  text from the **previous** dictation (the "off-by-one" symptom).
- A red-on-black message appears in the bottom-right corner of the Claude Code
  TUI: **`No image found in clipboard. Use ctrl+v to paste images.`**
- A **service restart** makes the problem vanish for a while. It then
  re-appears, usually after the user interacts with an image in the clipboard
  (copying a screenshot, dragging an image, etc.).
- We've "fixed" this at least half a dozen times. Each fix improves the happy
  path but leaves at least one unhandled edge in the timing / selection / CopyQ
  interaction that surfaces later.

If you're reading this because it broke again — **do not just restart the
service**. Follow the debug checklist near the bottom first.

---

## The moving parts that make this hard

```
 Remote Control (phone)           Virtual Assistant service
 ──────────────                   ────────────────────────
 Vložit ze schránky  ─────SignalR──►  DictationHub
                                         │
                                         ▼
                                  XDoToolKeyboardService
                                  .PasteFromClipboardAsync
                                         │
                      ┌──────────────────┼──────────────────┐
                      ▼                                     ▼
               IClipboardManager                       dotool (uinput)
               wl-copy / wl-paste                    emits key event:
                      │                              Shift+Insert  or
                      │                              Ctrl+Shift+V  or
                      │                              Ctrl+V
                      ▼
              GNOME Shell clipboard  ◄────────────────────┐
              (Wayland ↔ XWayland)                        │
                      │                                   │
                      ▼                                   │
                   CopyQ  ──auto-sync CLIPBOARD⇄PRIMARY──►┘
                      │
                      ▼
                 terminator  (XWayland client)
                      │
                      ▼
                  tmux server  (systemd daemon,
                                NOT a child of terminator)
                      │
                      ▼
                  tmux pane
                      │
                      ▼
                 claude (TUI)   ← interprets Ctrl+V / Ctrl+Shift+V
                                  itself as "paste image"
```

Every arrow is either async, a different process, or both. That's the setup
that keeps biting us.

---

## Known-complicating factors

1. **Claude Code intercepts `Ctrl+V` and `Ctrl+Shift+V`** as "paste image"
   shortcuts. If the clipboard holds text (no `image/*` MIME), Claude Code
   prints `No image found in clipboard. Use ctrl+v to paste images.` This is
   the ground-truth symptom — if you see this message, it means our keystroke
   was delivered to Claude Code instead of the terminal.

2. **tmux detaches the CLI app from the terminal process tree.** The tmux
   server runs under `systemd --user`, so `claude` inside a pane is NOT a
   descendant of the focused terminal. Any detection that relies on `pgrep -P`
   descending from the focused window's PID will miss it. Title-based
   detection (the terminal title contains "Claude Code") is the only reliable
   fallback.

3. **Shift+Insert reads X11 `PRIMARY` selection, not `CLIPBOARD`.** This is the
   traditional X11 middle-click paste buffer. It's a DIFFERENT selection from
   `Ctrl+C`/`Ctrl+V`'s `CLIPBOARD`. Writing to one does not automatically put
   text in the other.

4. **CopyQ runs as a clipboard manager and auto-syncs `CLIPBOARD` ↔ `PRIMARY`
   with asynchronous, non-deterministic delay.** This was the true cause of
   the "off-by-one" symptom. If we set `CLIPBOARD = X` and immediately send
   `Shift+Insert`, `PRIMARY` still holds whatever was there last cycle —
   CopyQ hasn't finished the sync yet. The paste reads stale `PRIMARY`. Then
   CopyQ eventually syncs `PRIMARY = X`, which is what the *next* cycle's
   paste reads. Hence the user sees each dictation paste as "the one from
   last time".

5. **Wayland + XWayland split surfaces.** The user runs GNOME/Wayland, but
   `terminator` is an XWayland client. GNOME Shell bridges `CLIPBOARD` across
   Wayland and XWayland; `PRIMARY` is *also* bridged in modern GNOME, but
   with extra latency.

6. **The image path.** When the user copies a screenshot (image MIME) into
   the clipboard, several things happen:
   - CopyQ stores the image entry and (depending on settings) tries to
     synthesize a preview.
   - Any subsequent text write races the image entry.
   - If our code happens to read the clipboard while it holds an image MIME,
     `wl-paste --no-newline` without `-t` might return empty/garbage,
     overwriting our "original" backup and making restore nondeterministic.
   This is the most likely origin of the "it starts misbehaving after
   I do something with an image" pattern the user keeps reporting.

7. **dotool emits uinput events**, which go through the compositor. There is
   NO synchronous acknowledgement that the target app has *read* the selection.
   We insert fixed delays (100–300 ms); anything that moves slower than that
   (tmux under load, remote tmux-attach, long Claude Code render frames)
   re-opens the race.

---

## Symptom → likely cause map

| Symptom | Most likely cause |
| --- | --- |
| "No image found in clipboard" shown by Claude Code | Our keystroke was `Ctrl+V` or `Ctrl+Shift+V`. Either `GetPasteShortcutAsync` didn't detect the CLI app, or the user is NOT in Claude Code but our detector thinks they are. |
| Dictation pastes previous dictation's text ("off-by-one") | CopyQ CLIPBOARD↔PRIMARY sync is racing our write. We set `CLIPBOARD`, `Shift+Insert` read stale `PRIMARY`. Fixed by writing directly to `PRIMARY`. |
| Paste does nothing at all (no error, no text) | (a) `PRIMARY` is genuinely empty and Shift+Insert had nothing to read; or (b) the restore step ran before the terminal read the selection. |
| Wrong content pasted (something from much earlier) | Our `originalClipboard`/`originalPrimary` backup captured a stale or garbage value (image MIME, truncated UTF-8), then we "restored" that stale value before the terminal consumed our real text. |
| Problem appears only after image interaction | We read the clipboard while it held an image MIME; `wl-paste --no-newline` returned something we then treated as `originalClipboard`, and the restore poisoned subsequent cycles. |
| Restart "fixes" it, then it recurs | Not a real fix — just resets whatever stale state accumulated (CopyQ queue, long-running `wl-paste --watch` handle, tmux buffer, our in-process delays). The underlying race is still there. |

---

## History of attempted fixes (abridged)

The root bug has been reshuffled many times. Each bullet is a fix that
helped the common case but did not remove the class of bugs.

1. Send `Ctrl+Shift+V` in terminals, `Ctrl+V` elsewhere. Broke the moment
   `Ctrl+Shift+V` started being handled by Claude Code as "paste image".
2. Detect CLI app via process tree (`pgrep -P` descend from focused terminal).
   Worked for direct `claude` children; broke under tmux where the claude
   process's parent is the tmux server, not the terminal.
3. Add title-based detection fallback ("Claude Code" substring in window
   title). Makes detection survive tmux but means *any* window titled
   "Claude Code" triggers CLI handling — brittle.
4. Switch to `Shift+Insert` for CLI apps. Bypasses Claude Code's
   `Ctrl+*+V` handler. But `Shift+Insert` reads `PRIMARY`, not `CLIPBOARD`,
   so we had to stage text in `PRIMARY` too — and CopyQ's auto-sync defeated
   our initial attempt to do this via `CLIPBOARD`.
5. Stage text directly in `PRIMARY` via `wl-copy --primary`, restore
   `PRIMARY` after 300 ms. PR #953 (2026-04-16). Covers the off-by-one
   and the `No image found` cases *as long as CLI-app detection works*.
6. **Fix `GdbusJsonHelper.UnescapeQuotes` to match the real gdbus wire
   format.** PR #1048 / issue #1047 (2026-04-21 ~16:20 CEST). See the
   dedicated section below.

What is **not** covered:

- Behaviour when the original clipboard holds an image MIME at the moment
  we snapshot it for "restore".
- Long tail on the 300 ms delay — on a slow tmux or a heavily loaded host
  the terminal may not have consumed `PRIMARY` yet.
- What happens if CopyQ is configured differently on the user's host (e.g.
  "Store selection changes" ON vs OFF — currently unknown / not audited).
- What happens if the focused window changes between the moment we detect
  the CLI app and the moment dotool fires.
- A future gdbus / glib version that emits a *different* escape format
  (e.g. single-quote wrap with no escape, or extra layers). The 2026-04-21
  fix added a unit test pinning the current byte sequence; if that test
  ever regresses on a host upgrade, this document's "Debug checklist
  step 1b" is how to re-capture the new format.

---

## 2026-04-21: gdbus JSON parse regression (PR #1048, issue #1047)

**What was broken.** `GdbusJsonHelper.UnescapeQuotes` (at
`src/VirtualAssistant.Core/WindowManagement/GdbusJsonHelper.cs`) was
searching for *double-escaped* quotes (`\\"` on the wire — three bytes:
backslash, backslash, quote) and rewriting them to JSON-escaped quotes
(`\"`). The comment in the file claimed gdbus emits that format. It
doesn't — at least not on this host.

**Actual gdbus wire format (verified by `od -c` on live output):**

```
( " [ { \ " i n _ c u r r e n t _ w o r k s p a c e \ " : f a l s e
```

Positions 4–5 are `\` and `"` — **two** distinct bytes, i.e. the string
is **single-escape**, wrapped in outer double-quotes. The old `Replace`
never matched, `UnescapeQuotes` returned the escaped string unchanged,
and `System.Text.Json.JsonSerializer` threw on the very first property
name:

```
System.Text.Json.JsonException: '\' is an invalid start of a property name.
  Expected a '"'. Path: $ | LineNumber: 0 | BytePositionInLine: 2.
  at GdbusWindowDetector.cs:line 65
```

**Propagation.** The exception was swallowed, every gdbus-based detector
returned null, and the downstream consequences fanned out:

| Consumer | Degradation |
| --- | --- |
| `GdbusWindowDetector.GetFocusedWindowInfoAsync` | returns `null` |
| `TerminalCliAppDetector.DetectCliAppAsync` | never sees Claude Code |
| `WaylandTerminalDetector.IsTerminalActiveAsync` | returns `false` |
| `XDoToolKeyboardService.GetPasteShortcutAsync` | falls through to `ctrl+v` |
| Claude Code TUI | hijacks `ctrl+v` → red `No image found in clipboard` toast |
| `DesktopMonitorBroadcastWorker` | never broadcasts `CliAppChanged` |
| Remote Control web UI | shows the monolithic **Diktovat** button instead of the split **Pokračuj / Diktovat** |
| Desktop Monitor dashboard | correction prompt stays `DefaultCorrection` instead of `ClaudeCodeCorrection` |

**Fix.** Rewrite `UnescapeQuotes` as a single-pass `StringBuilder` scan
that recognises only `\\` → `\` and `\"` → `"`, leaving every other
`\x` sequence (including genuine JSON escapes like `\n`, `\t`, `\uXXXX`
and lone trailing backslashes) intact. Signature widened to
`string? UnescapeQuotes(string?)` with
`[return: NotNullIfNotNull(nameof(json))]` so callers that pass a
non-null string still see a non-null return.

New regression test (`GdbusJsonHelperTests.UnescapeQuotes_RealGdbusWireFormat_ProducesValidJson`)
feeds the helper the exact byte pattern captured via `od -c` from live
`gdbus call ... Windows.List`, and asserts that System.Text.Json
deserializes it and that the focused window resolves to
`wm_class="terminator"`, `title="/bin/bash"`, `pid=322375`.

**Live-verify signal after deploy.**

```
journalctl --user -u virtual-assistant.service --since "5 min ago" \
  | grep -E "paste shortcut|Focused window|Failed to parse"
```

A **healthy** log shows both halves of the pair:

```
Focused window: terminator "Claude Code" (PID: 6508)
Using paste shortcut: shift+insert (CLI app: Claude Code — Ctrl+Shift+V would be hijacked)
```

A **regressed** log shows:

```
Failed to parse D-Bus JSON response
System.Text.Json.JsonException: '\' is an invalid start of a property name.
…
Using paste shortcut: ctrl+v (terminal: False)
```

**If this document is open because the bug is back:** do **not** assume
the 2026-04-21 fix is still doing its job. First step is always to run
the `od -c` probe in the Debug checklist "step 1b" below and compare
the byte pattern of `\` vs `"` against what `GdbusJsonHelper.UnescapeQuotes`
expects *today*. A host upgrade (glib / gdbus / libxml) could shift the
format again, and the unit test pinning the 2026-04-21 bytes will still
pass locally while prod behaves differently.

---

## Current implementation (PR #953)

Entry point: `XDoToolKeyboardService` in
`src/VirtualAssistant.Core/Keyboard/`.

**`GetPasteShortcutAsync()`** returns:
- `shift+insert` — if the CLI detector reports an active agent TUI. This is
  both the process-tree detection AND the terminal title fallback.
- `ctrl+shift+v` — if the focused window is any other terminal.
- `ctrl+v` — for everything else (GUI apps).

**`TypeIntoActiveWindowAsync(text)`** (dictation insert path):
1. Resolve paste shortcut and `usePrimary = shortcut == "shift+insert"`.
2. Snapshot the selection we are about to overwrite (`PRIMARY` if
   `usePrimary`, else `CLIPBOARD`).
3. Write `text + " "` to that selection.
4. Sleep 50 ms.
5. Fire `dotool key <shortcut>`.
6. Sleep 300 ms — terminal / tmux / TUI must have read the selection by now.
7. Restore the original selection.

**`FastPasteAsync(text)`** (quick dictation): same as above but skips the
snapshot/restore steps for latency. The user accepts that quick dictation
leaves the text in the selection.

**`PasteFromClipboardAsync()`** ("Vložit ze schránky" button):
1. Resolve paste shortcut.
2. If `shift+insert`, mirror `CLIPBOARD` → `PRIMARY` for the paste and
   restore `PRIMARY` after 300 ms. **`CLIPBOARD` is never written or
   restored on this path — the user's clipboard is left exactly as it was.**
3. Otherwise just send the shortcut.

---

## Debug checklist when it breaks again

**Before restarting the service**, collect:

1. What does the focused window title say? Run
   ```
   gdbus call --session --dest org.gnome.Shell \
     --object-path /org/gnome/Shell/Extensions/Windows \
     --method org.gnome.Shell.Extensions.Windows.List
   ```
   Look for `"focus": true` and read the `title` field. Does it contain
   "Claude Code"?

   **1b. What is the exact gdbus escape format?** (added 2026-04-21 after
   PR #1048.) Run
   ```
   gdbus call --session --dest org.gnome.Shell \
     --object-path /org/gnome/Shell/Extensions/Windows \
     --method org.gnome.Shell.Extensions.Windows.List | od -c | head
   ```
   Look at bytes 4–5 of the first record (`\` + `"` = two bytes =
   single-escape, which is what `GdbusJsonHelper.UnescapeQuotes` handles
   today). If you see `\ \ "` (three bytes) or some other shape, the
   gdbus / glib stack has changed escape rules on this host and the
   helper's regression test
   (`GdbusJsonHelperTests.UnescapeQuotes_RealGdbusWireFormat_ProducesValidJson`)
   will need an update to match — don't paper over it by re-restarting
   the service.

   Also tail the service log for the deterministic signal:
   ```
   journalctl --user -u virtual-assistant.service --since "5 min ago" \
     | grep -E "Failed to parse D-Bus JSON response"
   ```
   Presence of that line means gdbus parsing regressed again. Cross-check
   against step 1b.

2. What does VA detect? Hit
   `http://localhost:5055/Admin/DesktopMonitor` and check `APP NAME`,
   `CORRECTION PROMPT`. If they don't say `Claude Code` /
   `ClaudeCodeCorrection`, the CLI detector is the bug.

3. What does CopyQ have? Look in CopyQ's history for entries created AT the
   moment of the failed paste. If the top entry is an `image/*` MIME
   and the paste failed immediately after, you're in the image-poisoning
   path (see "Known-complicating factors" #6).

4. What did VA log? Tail the service and look for the paste shortcut line:
   ```
   journalctl --user -u virtual-assistant.service -f \
     | grep -E "paste|PRIMARY|CLIPBOARD|CliApp"
   ```
   Key log lines:
   - `Using paste shortcut: shift+insert (CLI app: Claude Code …)`
     — correct detection.
   - `Using paste shortcut: ctrl+shift+v (terminal: True)` while the user
     is in Claude Code — detector missed it.
   - `Set PRIMARY content (N chars)` followed by `Simulating paste with
     shortcut: shift+insert` — our write landed.
   - Any `wl-paste failed` / `wl-copy failed` — a clipboard tool crashed
     or raced; inspect surrounding lines.

5. What happens if the user pastes from keyboard manually (middle-click,
   or `Shift+Insert` from the physical keyboard)? If manual paste works and
   ours doesn't, it's timing/async; if manual paste *also* produces "No
   image found", Claude Code itself has changed its key handling and our
   Shift+Insert assumption no longer holds.

6. What's in `PRIMARY` right before VA sends the paste? Add a temporary
   diagnostic: in `XDoToolKeyboardService.PasteFromClipboardAsync`, log
   `wl-paste --primary --no-newline` output before and after the write,
   plus `wl-paste --list-types` to catch image MIME.

7. Has Claude Code been updated? The `No image found in clipboard` message
   is emitted by Claude Code itself. If the shortcut it reacts to has been
   extended (e.g. it starts handling `Shift+Insert`), our whole strategy
   needs to move to another escape — most likely tmux `load-buffer`+`paste-buffer`
   injected into the target pane.

---

## Hypotheses still not ruled out

1. **Image MIME poisoning of `originalClipboard`.** `IClipboardManager.GetClipboardAsync`
   calls `wl-paste --no-newline` without a MIME filter. If the clipboard
   holds an image, the returned bytes are whatever `wl-paste` prints for
   images (often empty with a nonzero exit). We then treat that as the
   "original" and restore it. This could leave the clipboard in a bad
   state that survives across dictations until a restart clears it.
   **Mitigation idea:** call `wl-paste --list-types` first; if the top
   type is `image/*`, skip the save/restore and paste without touching
   CLIPBOARD at all. (Not implemented.)

2. **CopyQ synthesizing paste while we're writing.** CopyQ has a "paste
   current clipboard into window on shortcut" feature. If any of its
   global shortcuts overlaps with `Shift+Insert` for this user, our event
   goes to CopyQ's handler, not the terminal. (Not verified — check
   `~/.config/copyq/copyq.conf`.)

3. **`dotool` firing before the target window has re-focused.** We resolve
   the CLI app and the selection on one thread, then send the keystroke.
   If the focused window changed in between, we paste into the wrong app.
   A focus check immediately before `dotool key` would catch it.

4. **`PRIMARY` already owned by another client that refuses to release it.**
   Some X clients hold `PRIMARY` and respond to SetSelectionOwner slowly.
   If terminator reads `PRIMARY` via XWayland while GNOME Shell is still
   transferring ownership, terminator sees the old content.

---

## What to try next if PR #953 isn't enough

In rough order of complexity:

1. **Extend the post-paste delay to e.g. 500 ms** when the focused terminal
   is running under tmux. Cheap to try; only affects latency.

2. **Skip clipboard save/restore when an image MIME is present.** Guard
   `GetClipboardAsync` / the worker's call site with a
   `--list-types`-based check.

3. **Target tmux directly.** When we detect "tmux is between us and
   Claude Code", bypass the keyboard path entirely:
   `tmux set-buffer -b __va_paste "<text>" && tmux paste-buffer -b __va_paste -t <pane>`.
   Needs a way to identify which pane holds Claude Code (we already have
   `tmux list-panes -a -F` working for diagnosis — wire it into detection).

4. **Stop using the user's selections at all.** Pipe text directly to the
   pane's pty via `tmux send-keys -l -t <pane> "<text>"`. This bypasses
   every clipboard, every selection, every keybinding. Main risk: the
   text is interpreted one char at a time, so Czech diacritics need the
   `-l` (literal) flag, which tmux 2.6+ supports.

5. **Write our own, minimal clipboard daemon** that pretends to be CopyQ
   but gives us synchronous `CLIPBOARD` writes with no cross-selection
   sync. High effort, last resort.

---

## Files involved

- `src/VirtualAssistant.Core/Keyboard/XDoToolKeyboardService.cs` — all
  keyboard simulation, paste shortcut selection, clipboard save/restore.
- `src/VirtualAssistant.Core/Clipboard/WlClipboardManager.cs` — `wl-copy` /
  `wl-paste` wrapper, including `--primary` variants.
- `src/VirtualAssistant.Core/WindowManagement/TerminalCliAppDetector.cs` —
  process-tree + title-based detection of Claude Code / OpenCode / Gemini.
- `src/VirtualAssistant.Service/Workers/DesktopMonitorBroadcastWorker.cs` —
  broadcasts `CliAppChanged` to the Remote Control based on the same
  detection flow.
- `src/VirtualAssistant.Service/Hubs/DictationHub.cs` — SignalR entry
  points: `PasteFromClipboard`, `PressEnter`, `SendContinue`,
  `GetActiveCliApp`.

## Related PRs

- PR #953 — Pokračuj button + CliAppChanged broadcast + Shift+Insert paste
  + PRIMARY-selection staging. (2026-04-16)
- PR #1048 (closes #1047) — `GdbusJsonHelper.UnescapeQuotes` single-vs-double
  escape fix. Single-pass scanner, regression test pinning the real gdbus
  byte pattern. *The current state.* (2026-04-21)

---

## Quick self-test (manual)

With the service running and the user's Claude Code session focused in tmux
under terminator:

1. Copy any plain text on the desktop (`Ctrl+C`).
2. Open the Remote Control on the phone. Confirm the **Pokračuj** button
   is visible (confirms `CliAppChanged` is "Claude Code").
3. Tap **Vložit ze schránky**.
   - ✅ The copied text appears at the Claude Code prompt.
   - ❌ If "No image found in clipboard" shows → the shortcut path
     reverted to `Ctrl+Shift+V`. Jump to debug checklist.
   - ❌ If something else pastes → PRIMARY raced. Jump to debug checklist.
4. Re-dictate a fresh Czech sentence (e.g. "Příklad s háčky").
   - ✅ Exactly that sentence lands in Claude Code.
   - ❌ If *previous* sentence lands → off-by-one re-appeared. CopyQ sync
     is fighting us; confirm write went to PRIMARY, not CLIPBOARD.
5. After steps 3–4, check that the CLIPBOARD still holds the text from
   step 1 (open CopyQ; the top entry should still be that text).
6. Repeat steps 3–4 five times in a row to check for drift. The system
   must not need a restart after those five iterations.
