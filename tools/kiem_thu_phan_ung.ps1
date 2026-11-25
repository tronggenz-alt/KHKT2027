# Biên dịch và chạy kiểm thử phản ứng (tests/PhanUngTests.cs) bằng .NET đi kèm Unity, không cần mở Unity.
#   powershell -ExecutionPolicy Bypass -File tools/kiem_thu_phan_ung.ps1              bộ phản ứng chuẩn (THCS, THPT, nâng cao)
#   powershell -ExecutionPolicy Bypass -File tools/kiem_thu_phan_ung.ps1 -v           in cả các phản ứng đạt
#   powershell -ExecutionPolicy Bypass -File tools/kiem_thu_phan_ung.ps1 "Cu + HNO"   chỉ các phản ứng có tên chứa chuỗi này
#   powershell -ExecutionPolicy Bypass -File tools/kiem_thu_phan_ung.ps1 cap 0 1      mọi cặp hóa chất (phân mảnh 0 / 1)
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest, [string]$UnityEditor = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $UnityEditor "Data\DotNetSdk"
$dotnet = Join-Path $sdk "dotnet.exe"
$csc = Get-ChildItem (Join-Path $sdk "sdk") -Recurse -Filter csc.dll | Where-Object { $_.FullName -like "*bincore*" } | Select-Object -First 1
$refDir = Get-ChildItem (Join-Path $sdk "packs\Microsoft.NETCore.App.Ref") -Directory | Sort-Object Name | Select-Object -Last 1
$refs = Get-ChildItem (Join-Path $refDir.FullName "ref\net8.0") -Filter *.dll | ForEach-Object { "-r:`"$($_.FullName)`"" }
$runtime = (Get-ChildItem (Join-Path $sdk "shared\Microsoft.NETCore.App") -Directory | Sort-Object Name | Select-Object -Last 1).Name

$out = Join-Path $root "tools.bin_kiem_thu"  # thư mục TEMP có thể bị Application Control chặn chạy dll
New-Item -ItemType Directory -Force $out | Out-Null
$chem = Join-Path $root "Phong thi nghiem ao\Assets\Scripts\Simulation\Chem"
$src = @(Get-ChildItem $chem -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }) + "`"$(Join-Path $root 'tests\PhanUngTests.cs')`""
$rsp = Join-Path $out "build.rsp"
(@("-nologo", "-optimize+", "-target:exe", "-langversion:9", "-nullable:disable", "-out:`"$out\PhanUng.dll`"") + $refs + $src) | Set-Content -Encoding UTF8 $rsp
& $dotnet $csc.FullName "@$rsp"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
@"
{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$runtime" } } }
"@ | Set-Content -Encoding UTF8 (Join-Path $out "PhanUng.runtimeconfig.json")
$env:KHKT_ROOT = $root
& $dotnet exec --runtimeconfig (Join-Path $out "PhanUng.runtimeconfig.json") (Join-Path $out "PhanUng.dll") @Rest
exit $LASTEXITCODE
