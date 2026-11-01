#requires -Version 7.0
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\publish'))
if (-not $publishRoot.StartsWith($repoRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publish output must stay inside the repository.'
}
if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

dotnet publish (Join-Path $repoRoot 'ClipboardPinViewer.csproj') --configuration Release --output $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$files = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse)
if ($files.Count -ne 1 -or $files[0].Name -ne 'ClipboardPinViewer.exe') {
    throw 'Expected exactly one framework-dependent single-file EXE.'
}

# Run the exact distributable in isolation from bin/obj, without registering F1
# or reading/writing clipboard content. This must resolve bundled WinRT DLLs.
$startInfo = [System.Diagnostics.ProcessStartInfo]::new($files[0].FullName)
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.WorkingDirectory = $publishRoot
$startInfo.ArgumentList.Add('--check-dependencies')
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($startInfo)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(20000)) {
        $process.Kill()
        throw 'Published EXE dependency check timed out.'
    }
    $output = $stdout.GetAwaiter().GetResult()
    $errorOutput = $stderr.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) {
        throw "Published EXE dependency check failed: $errorOutput"
    }
    $report = $output | ConvertFrom-Json
    if ($report.Status -ne 'ok' -or -not $report.WindowsSdk -or -not $report.WinRt) {
        throw 'Published EXE returned an incomplete dependency report.'
    }
    Write-Output $output.Trim()
    Write-Output "Ready: $($files[0].FullName)"
}
finally {
    $process.Dispose()
}
