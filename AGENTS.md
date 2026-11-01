# Goal

- Replace the original AutoHotkey clipboard pin viewer with a minimal Windows exe that runs on this machine.
- Prefer direct reads from Windows clipboard history; fall back to current clipboard/session cache only when history is unavailable.
- Pinned viewers must be draggable, resizable, and scrollable without showing any chrome: no title bar, no border, no scroll bars.
- Commit the project and publish a runnable exe on GitHub Releases.

# Current State

- Version: 0.1.5. The installed application is a framework-dependent single-file EXE at `C:\Portable Programs\ClipboardPinViewer\ClipboardPinViewer.exe`.
- The Startup shortcut `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\ClipboardPinViewer.lnk` points at that stable installation directory.
- All application/WinRT dependencies are bundled into the published EXE. The machine still needs the .NET 10 Desktop Runtime; no global runtime changes were needed for the 0.1.5 repair.
- Verified on 2026-11-01: installed dependency check loads Windows SDK 10.0.19041.38 and WinRT.Runtime 2.2.0.0; the real resident application's F1 message handler opens a text viewer and returns to tray-only state after closing it.
- F1 is a global hotkey registered by a hidden `MessageWindow`; only one instance may run or `RegisterHotKey` fails and shows a warning box.
- Text viewers: auto-sized up to 1/3 of the primary working area in each dimension, scroll bars hidden, wheel scrolling driven manually, unmodified left-drag moves the window, Shift/Ctrl+left selects text. No `MaximumSize`, so the user can still drag them to any size.
- Image viewers: auto-sized to the working area minus 180px, drag anywhere, aspect ratio locked during resize.
- Both viewer kinds resize from an 8px band along every edge.

# Active Work

- [open] `KeepSizingAspectRatio` derives width from height for corner drags, so dragging a corner horizontally has no effect. Now that resizing actually works this is noticeable; consider honouring whichever axis moved further.
- [open] Wheel scrolling needs the viewer focused, because `WM_MOUSEWHEEL` only goes to the focused window. Hover-to-scroll would need a global mouse hook.

# Build / Run / Test

- Build: `dotnet build` with the .NET 10 SDK. The project targets Windows x64.
- Publish and verify: `pwsh -NoProfile -File scripts/publish.ps1` (PowerShell 7+). Output: `artifacts/publish/ClipboardPinViewer.exe`. The script clears only that repository output directory, requires exactly one EXE, and executes its `--check-dependencies` mode to load both bundled WinRT assemblies and call the real Windows clipboard-history availability API without reading/writing clipboard contents or registering F1.
- Distribution and deployment rules: read `docs/design/distribution.md` before changing packaging. Normal application runs still require the .NET 10 Desktop Runtime.
- Deploy to `C:\Portable Programs\ClipboardPinViewer\`: check for open pinned viewers first, request normal resident application exit, replace the EXE, and relaunch. Keep the Startup shortcut's target, working directory, and icon on the stable installed EXE.
- After touching `ViewerForm.cs`, probe the real process for the 1/3 text size cap, wheel scrolling with `WS_VSCROLL` absent, `WM_NCHITTEST` returning 10/15/17 on edges and 1 in the centre, image resizing, and `WM_SIZING` ratio locking. Historical temporary probe scripts are not kept in the repository; recreate them as needed. Manual drag/select still needs a human.

# Durable Lessons

- The 0.1.4 installed folder contained only the apphost EXE and main DLL, omitting `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll`. The process could start but clipboard display failed when the WinRT-referencing async method was JIT-compiled; an inner try/catch cannot reliably catch its own method's load/JIT failure. A build or successful tray startup does not validate packaging: execute the actual published artifact's dependency check.
- .NET SDK 10.0.400 rejects `EnableCompressionInSingleFile=true` with `SelfContained=false` (NETSDK1176). Keep framework-dependent single-file publishing uncompressed.
- A child control with `Dock = DockStyle.Fill` consumes the form's `WM_NCHITTEST`, so a borderless form's resize grip is only as wide as its `Padding` — measured as exactly 1px here, which is why `ResizeGripSize = 30` did nothing for two releases. Fix it in the child: report `HTTRANSPARENT` for the edge band and let the hit test fall through to the form. Do not reach for a process-wide `IMessageFilter`; it would also intercept the tray menu.
- The same ownership rule applies to mouse input: a `Dock = Fill` child swallows `WM_LBUTTONDOWN`, so a form-level `OnMouseDown` drag handler never fires. Image viewers only ever worked because `PictureBox.MouseDown` was wired up explicitly.
- A multiline Edit with `ScrollBars.None` ignores `WM_MOUSEWHEEL` entirely, but `EM_LINESCROLL` still scrolls it and clamps itself at both ends. That pair is how you keep scroll bars hidden without losing the wheel.
- `WM_MOUSEWHEEL` is delivered to the focused window, not the window under the cursor, so both the form and the text control have to handle it. It also means an unfocused pinned viewer will not scroll on hover.
- Verify interaction fixes against the real running window, not a reimplementation in a probe. Both earlier "fixed" resize claims came from reading the code; a `WindowFromPoint` sweep across the client area would have caught the 1px band immediately.

# Docs Index

- `docs/design/distribution.md`: single-file publishing, runtime requirements, artifact verification, and deployment.
