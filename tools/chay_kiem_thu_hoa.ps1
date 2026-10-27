# Biên dịch và chạy kiểm thử bộ máy hóa học (tests/ChemEngineTests.cs) bằng .NET đi kèm Unity.
# Không cần mở Unity. Dùng:  powershell -ExecutionPolicy Bypass -File tools/chay_kiem_thu_hoa.ps1
param([string]$UnityEditor = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $UnityEditor "Data\DotNetSdk"
$dotnet = Join-Path $sdk "dotnet.exe"
$csc = Get-ChildItem (Join-Path $sdk "sdk") -Recurse -Filter csc.dll | Where-Object { $_.FullName -like "*bincore*" } | Select-Object -First 1
$refDir = Get-ChildItem (Join-Path $sdk "packs\Microsoft.NETCore.App.Ref") -Directory | Sort-Object Name | Select-Object -Last 1
$refs = Get-ChildItem (Join-Path $refDir.FullName "ref\net8.0") -Filter *.dll | ForEach-Object { "-r:`"$($_.FullName)`"" }
$runtime = (Get-ChildItem (Join-Path $sdk "shared\Microsoft.NETCore.App") -Directory | Sort-Object Name | Select-Object -Last 1).Name

$out = Join-Path $env:TEMP "khkt_chem_tests"
New-Item -ItemType Directory -Force $out | Out-Null
$chem = Join-Path $root "Phong thi nghiem ao\Assets\Scripts\Simulation\Chem"
$src = @(Get-ChildItem $chem -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }) + "`"$(Join-Path $root 'tests\ChemEngineTests.cs')`""
$rsp = Join-Path $out "build.rsp"
(@("-nologo", "-target:exe", "-langversion:latest", "-nullable:disable", "-out:`"$out\ChemTests.dll`"") + $refs + $src) | Set-Content -Encoding UTF8 $rsp
& $dotnet $csc.FullName "@$rsp"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
@"
{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$runtime" } } }
"@ | Set-Content -Encoding UTF8 (Join-Path $out "ChemTests.runtimeconfig.json")
$env:KHKT_ROOT = $root  # bài bay hơi đọc data/chem/nhiet_do_soi.json
& $dotnet exec --runtimeconfig (Join-Path $out "ChemTests.runtimeconfig.json") (Join-Path $out "ChemTests.dll")
exit $LASTEXITCODE
