$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\icon.ico" "/out:$PSScriptRoot\FloatingClipboard.exe" "$PSScriptRoot\FloatingClipboard.cs"
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
