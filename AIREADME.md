# Project Goal

- Replace the original AutoHotkey clipboard pin viewer with a minimal Windows exe that runs on this machine.
- Prefer direct reads from Windows clipboard history; fall back to current clipboard/session cache only when history is unavailable.
- Fix image viewer resizing so images stay stable while dragging or resizing.
- Commit the project and publish a runnable exe on GitHub Releases.

# Lessons Learned

- Windows clipboard history is available from C# via `Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync()` on this machine.
- Keeping a single `Image` in a WinForms `PictureBox` with `SizeMode.Zoom` avoids the AHK implementation's repeated bitmap reload/resample behavior during resize.
- 2026-06-22 proxy diagnosis: Windows browser proxy and `HTTP_PROXY`/`HTTPS_PROXY` were set to `127.0.0.1:7897`, served by `verge-mihomo`; WinHTTP remained direct.
- 2026-06-22 proxy diagnosis: `www.google.com`, `accounts.google.com`, `aistudio.google.com`, and `generativelanguage.googleapis.com` worked through the proxy, while `gemini.google.com` timed out after the local HTTP `CONNECT` tunnel was established.
- 2026-06-22 proxy diagnosis: Mihomo showed `gemini.google.com` matched `DomainKeyword google -> 良心云 -> 🇯🇵日本高速01|CTCU|0.5x`; the delay API returned `504 Timeout` for that node/group against Gemini, while US, Singapore, and Taiwan nodes had normal Gemini delays.

# Task Board

- [completed] Implement C# WinForms clipboard pin viewer.
- [completed] Build and publish minimal runnable exe.
- [completed] Commit, push, and create GitHub Release v0.1.0.
- [pending] If approved, switch `良心云` away from `🇯🇵日本高速01|CTCU|0.5x` for Gemini or add a Gemini-specific rule to a working node/policy group.
