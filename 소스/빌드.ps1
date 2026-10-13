$ErrorActionPreference = 'Stop'
$taskBuildOutput = $args | Select-Object -First 1
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '..\중간파일\dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '..\중간파일\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $PSScriptRoot '..\중간파일\nuget-cache'
$env:NUGET_SCRATCH = Join-Path $PSScriptRoot '..\중간파일\nuget-scratch'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$sentinelDir = Join-Path $env:DOTNET_CLI_HOME '.dotnet'
New-Item -ItemType Directory -Force -Path $sentinelDir | Out-Null
$sdkVersion = (dotnet --version).Trim()
New-Item -ItemType File -Force -Path (Join-Path $sentinelDir "$sdkVersion.dotnetFirstUseSentinel") | Out-Null
& (Join-Path $PSScriptRoot '아이콘만들기.ps1')
# Compile only against installed .NET reference packs. No downloads or global caches.
$dotnetRoot = Split-Path (Get-Command dotnet).Source
$sdk = Get-ChildItem (Join-Path $dotnetRoot 'sdk') -Directory | Where-Object Name -like '6.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $sdk) { throw '.NET 6 SDK가 필요합니다.' }
$core = Get-ChildItem (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -like '6.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$desktop = Get-ChildItem (Join-Path $dotnetRoot 'packs\Microsoft.WindowsDesktop.App.Ref') -Directory | Where-Object Name -like '6.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$appHostPack = Get-ChildItem (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Host.win-x64') -Directory | Where-Object Name -like '6.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$outDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\실행파일'))
if ($taskBuildOutput) { $outDir = [IO.Path]::GetFullPath($taskBuildOutput) }
$middleDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\중간파일'))
New-Item -ItemType Directory -Force -Path $outDir,$middleDir | Out-Null
$globalUsings = Join-Path $middleDir 'GlobalUsings.cs'
@'
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
'@ | Set-Content -LiteralPath $globalUsings -Encoding utf8
$rsp = @('/nostdlib+','/target:winexe','/langversion:10','/nullable:enable','/optimize+','/debug:portable',('/out:"' + (Join-Path $outDir 'DeckLauncher.dll') + '"'),('/win32icon:"' + (Join-Path $PSScriptRoot 'launcher.ico') + '"'))
$references = @{}
$rsp += '/win32manifest:"' + (Join-Path $PSScriptRoot 'app.manifest') + '"'
foreach ($pack in @($core,$desktop)) { foreach ($dll in Get-ChildItem (Join-Path $pack.FullName 'ref\net6.0') -Filter '*.dll') { $references[$dll.Name] = $dll.FullName } }
foreach ($reference in $references.Values) { $rsp += '/reference:"' + $reference + '"' }
foreach ($file in Get-ChildItem $PSScriptRoot -Filter '*.cs') { $rsp += '"' + $file.FullName + '"' }
$rsp += '"' + $globalUsings + '"'
$response = Join-Path $middleDir 'compiler.rsp'
$rsp | Set-Content -LiteralPath $response -Encoding utf8
& dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') /noconfig "@$response"
if ($LASTEXITCODE -ne 0) { throw '빌드 실패' }
Add-Type -Path (Join-Path $sdk.FullName 'Microsoft.NET.HostModel.dll')
[Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost((Join-Path $appHostPack.FullName 'runtimes\win-x64\native\apphost.exe'),(Join-Path $outDir 'DeckLauncher.exe'),'DeckLauncher.dll',$true,(Join-Path $outDir 'DeckLauncher.dll'),$false)
@'
{"runtimeOptions":{"tfm":"net6.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"6.0.36"},{"name":"Microsoft.WindowsDesktop.App","version":"6.0.36"}]}}
'@ | Set-Content -LiteralPath (Join-Path $outDir 'DeckLauncher.runtimeconfig.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'launcher.ico') -Destination (Join-Path $outDir 'launcher.ico') -Force
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Fonts')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Fonts') -Destination $outDir -Recurse -Force }
