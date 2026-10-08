$ErrorActionPreference = 'Stop'
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$outputPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'PariPari Anti-HDR 1.7.exe'
$oldTemp = $env:TEMP
$oldTmp = $env:TMP
try {
    # Keep compiler scratch files inside the project as well as the executable.
    $buildTemp = Join-Path $PSScriptRoot '.build-temp'
    New-Item -ItemType Directory -Force $buildTemp | Out-Null
    $env:TEMP = $buildTemp
    $env:TMP = $buildTemp
    & (Join-Path $frameworkPath 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ "/out:$outputPath" "/resource:$(Join-Path $PSScriptRoot 'Assets\Banner.png'),PariPari.Banner" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$frameworkPath\WPF\PresentationCore.dll" "/reference:$frameworkPath\WPF\WindowsBase.dll" (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $PSScriptRoot 'Widgets.cs') (Join-Path $PSScriptRoot 'AutoTone.cs') (Join-Path $PSScriptRoot 'Localization.cs') (Join-Path $PSScriptRoot 'Theme.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
} finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
}
