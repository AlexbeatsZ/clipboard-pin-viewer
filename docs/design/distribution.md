# Distribution

The Windows x64 release is a **framework-dependent single-file EXE**. The .NET 10 Desktop Runtime is provided by the machine; every application dependency, including `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll`, is bundled into the EXE. Do not distribute the small apphost from `bin/Release/...` or manually select files from a multi-file build directory.

The project file owns the publish settings: Windows x64 RID, `SelfContained=false`, `PublishSingleFile=true`, embedded debugging symbols, and native-library bundling. This keeps direct `dotnet publish` and the repository publish script on the same packaging path. Do not enable bundle compression with framework-dependent publishing: SDK 10.0.400 rejects that combination with NETSDK1176.

## Build and verify

Run with PowerShell 7 and the .NET 10 SDK:

```powershell
pwsh -NoProfile -File scripts/publish.ps1
```

The script recreates `artifacts/publish` inside this repository, publishes Release, and rejects output containing anything other than `ClipboardPinViewer.exe`. It then runs **that published EXE** with `--check-dependencies`, redirects its standard output/error, and requires a successful JSON report and exit code.

The check explicitly loads both WinRT assemblies and calls `Windows.ApplicationModel.DataTransfer.Clipboard.IsHistoryEnabled()`. It reports only versions and the history-enabled boolean, without reading clipboard/history contents, modifying the clipboard, opening viewers, or claiming F1. History being disabled is a valid result, not a dependency failure.

The dependency check's WinRT-referencing method must stay behind a non-inlined call inside Main's try/catch. A missing referenced assembly can throw while JIT-compiling a method, before a try/catch inside that same method starts. This was why the original history fallback did not cover an incomplete installation.

## Install and validate

The stable installed path is `C:\Portable Programs\ClipboardPinViewer\ClipboardPinViewer.exe`. Keep the existing Startup shortcut's target, working directory, and icon there. Do not create another versioned installation for each release.

Before replacement, check whether the resident process has visible pinned windows. Preserve user-opened viewers; for a tray-only process, post WM_QUIT to its WinForms UI thread and wait for normal exit before replacing the mapped EXE. Start the new copy and verify the installed dependency check, process path, and Startup shortcut readback.

For the final application smoke check, deliver WM_HOTKEY (ID `0x435056`) to the resident message window and verify that it opens a real viewer. Close only the viewer created by the probe and confirm the process returns to tray-only state. Avoid changing the clipboard or logging its contents. This exercises the actual hotkey handler and clipboard code; manual keyboard, drag, selection, and wheel acceptance are separate checks.

Remove obsolete versioned installation folders only after the replacement has passed validation and no processes/shortcuts reference them. Do not retain old local release artifacts.
