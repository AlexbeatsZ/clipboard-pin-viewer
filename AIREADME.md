# Project Goal

- Replace the original AutoHotkey clipboard pin viewer with a minimal Windows exe that runs on this machine.
- Prefer direct reads from Windows clipboard history; fall back to current clipboard/session cache only when history is unavailable.
- Fix image viewer resizing so images stay stable while dragging or resizing.
- Commit the project and publish a runnable exe on GitHub Releases.

# Lessons Learned

- Windows clipboard history is available from C# via `Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync()` on this machine.
- Keeping a single `Image` in a WinForms `PictureBox` with `SizeMode.Zoom` avoids the AHK implementation's repeated bitmap reload/resample behavior during resize.
- Loading only the selected clipboard history item avoids decoding every image in the history on each F1 press.

# Task Board

- [completed] Implement C# WinForms clipboard pin viewer.
- [completed] Build and publish minimal runnable exe.
- [completed] Commit, push, and create GitHub Release v0.1.0.
- [completed] Improve resize hit-testing, image aspect-ratio locking, F1 responsiveness, and hidden text scrollbars.
- [completed] Commit, push, and create GitHub Release v0.1.1 with the interaction fixes.
