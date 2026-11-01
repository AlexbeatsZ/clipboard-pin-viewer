# Clipboard Pin Viewer

A small Windows tray app for pinning clipboard text and images in borderless, always-on-top viewers.

- Press **F1** or double-click the tray icon to display the newest clipboard item that is not already visible.
- Uses Windows clipboard history when available, with current-clipboard/session fallback.
- Drag to move; resize from the edges. Hold Shift/Ctrl while dragging text to select it.

## Download

Get `ClipboardPinViewer.exe` from [GitHub Releases](https://github.com/AlexbeatsZ/clipboard-pin-viewer/releases). Windows x64 and the **.NET 10 Desktop Runtime** are required. Starting with 0.1.5, the released EXE bundles all application/WinRT dependencies; no adjacent DLLs or JSON files are needed.

## Build

Use the .NET 10 SDK and PowerShell 7:

```powershell
pwsh -NoProfile -File scripts/publish.ps1
```

The verified distributable is `artifacts/publish/ClipboardPinViewer.exe`. The script also executes the artifact's `--check-dependencies` mode, which checks bundled assemblies and Windows API availability without reading clipboard contents or registering F1.

See [distribution design](docs/design/distribution.md) for packaging, verification, and deployment details.
