# Goal

- Replace the original AutoHotkey clipboard pin viewer with a minimal Windows exe that runs on this machine.
- Prefer direct reads from Windows clipboard history; fall back to current clipboard/session cache only when history is unavailable.
- Pinned viewers must be draggable, resizable, and scrollable without showing any chrome: no title bar, no border, no scroll bars.
- Commit the project and publish a runnable exe on GitHub Releases.

# Current State

- Source revision deployed: see `git log -1`. Running copy lives in `C:\Portable Programs\ClipboardPinViewer-v0.1.4-framework-dependent\`.
- The Startup shortcut `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\ClipboardPinViewer.lnk` now points at the v0.1.4 folder. The v0.1.3 folder is still on disk as a rollback target.
- F1 is a global hotkey registered by a hidden `MessageWindow`; only one instance may run or `RegisterHotKey` fails and shows a warning box.
- Text viewers: auto-sized up to 1/3 of the primary working area in each dimension, scroll bars hidden, wheel scrolling driven manually, unmodified left-drag moves the window, Shift/Ctrl+left selects text. No `MaximumSize`, so the user can still drag them to any size.
- Image viewers: auto-sized to the working area minus 180px, drag anywhere, aspect ratio locked during resize.
- Both viewer kinds resize from an 8px band along every edge.

# Active Work

- [completed] Implement C# WinForms clipboard pin viewer; build, commit, and publish Releases v0.1.0 through v0.1.3.
- [completed] Make F1 newest-unshown based on visible-window signatures; expand the resize grip; embed the Fluent clipboard icon.
- [completed] Fix the three interaction defects that survived all of the above: text viewers could not be moved, neither viewer kind could actually be resized, and long text was clipped with no way to scroll.
- [completed] Cap auto-generated text windows at 1/3 of the primary working area, keep scroll bars hidden, and deploy v0.1.4.
- [open] `KeepSizingAspectRatio` derives width from height for corner drags, so dragging a corner horizontally has no effect. Now that resizing actually works this is noticeable; consider honouring whichever axis moved further.
- [open] Wheel scrolling needs the viewer focused, because `WM_MOUSEWHEEL` only goes to the focused window. Hover-to-scroll would need a global mouse hook.

# Build / Run / Test

- Build: `dotnet build` in the repo root. Publish: `dotnet publish -c Release -o <dir>` — framework-dependent, 7 files, the apphost must stay beside its dll/json/WinRT dependencies.
- Deploy: stop the running process first (it holds the exe), copy the publish output to a new `C:\Portable Programs\ClipboardPinViewer-v<version>-framework-dependent\`, repoint the Startup shortcut, then relaunch.
- After touching `ViewerForm.cs` run the two probe scripts in `%TEMP%\.agents\clipboard-pin-viewer\`: `verify.ps1` asserts the 1/3 size cap, wheel scrolling with `WS_VSCROLL` absent, and `WM_NCHITTEST` returning 10/15/17 on the edges and 1 in the centre; `verify-image.ps1` asserts the same hit-testing for image viewers plus `WM_SIZING` ratio locking. Both stop the resident instance, drive F1 via `keybd_event`, and inspect the real window — manual drag/select still needs a human.

# Durable Lessons

- A child control with `Dock = DockStyle.Fill` consumes the form's `WM_NCHITTEST`, so a borderless form's resize grip is only as wide as its `Padding` — measured as exactly 1px here, which is why `ResizeGripSize = 30` did nothing for two releases. Fix it in the child: report `HTTRANSPARENT` for the edge band and let the hit test fall through to the form. Do not reach for a process-wide `IMessageFilter`; it would also intercept the tray menu.
- The same ownership rule applies to mouse input: a `Dock = Fill` child swallows `WM_LBUTTONDOWN`, so a form-level `OnMouseDown` drag handler never fires. Image viewers only ever worked because `PictureBox.MouseDown` was wired up explicitly.
- A multiline Edit with `ScrollBars.None` ignores `WM_MOUSEWHEEL` entirely, but `EM_LINESCROLL` still scrolls it and clamps itself at both ends. That pair is how you keep scroll bars hidden without losing the wheel.
- `WM_MOUSEWHEEL` is delivered to the focused window, not the window under the cursor, so both the form and the text control have to handle it. It also means an unfocused pinned viewer will not scroll on hover.
- Verify interaction fixes against the real running window, not a reimplementation in a probe. Both earlier "fixed" resize claims came from reading the code; a `WindowFromPoint` sweep across the client area would have caught the 1px band immediately.
