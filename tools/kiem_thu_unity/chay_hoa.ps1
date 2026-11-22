param([string]$Test = "PhanUngTests", [string]$ArgsLine = "")
# Chạy kiểm thử hóa học trong một project Unity nhỏ riêng (chỉ mã Simulation/Chem + tests), không đụng bản sao proj các phiên khác dùng.
# Windows Application Control chặn dll tự biên dịch chạy bằng dotnet, nên chạy trong Unity batchmode.
$src = "D:\KHKT 11 du phong"
$proj = "D:\KHKT_kiem_thu\hoa_proj"
robocopy "$src\Phong thi nghiem ao\Assets\Scripts\Simulation\Chem" "$proj\Assets\Chem" *.cs /MIR /NJH /NJS /NDL /NP /NFL | Out-Null
$ed = "$proj\Assets\Editor"
New-Item -ItemType Directory -Force $ed | Out-Null
Copy-Item "$src\tools\kiem_thu_unity\KhktChemRun.cs" $ed -Force
Copy-Item "$src\tests\PhanUngTests.cs" $ed -Force
Copy-Item "$src\tests\ChemEngineTests.cs" $ed -Force
$out = "D:\KHKT_kiem_thu\ket_qua\hoa"
New-Item -ItemType Directory -Force $out | Out-Null
$env:KHKT_CHEM_TEST = $Test
$env:KHKT_CHEM_ARGS = $ArgsLine
$env:KHKT_CHEM_OUT = "$out\$Test.txt"
$env:KHKT_ROOT = $src
if (Test-Path "$out\$Test.txt") { Remove-Item "$out\$Test.txt" }
$t0 = Get-Date
$a = Start-Process "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe" -ArgumentList "-batchmode","-nographics","-projectPath","`"$proj`"","-executeMethod","KhktChemRun.Run","-logFile","`"$out\unity_$Test.log`"" -PassThru
$ok = $a.WaitForExit(3600000)
"===== $Test exited=$ok code=$($a.ExitCode) sau $([math]::Round(((Get-Date)-$t0).TotalSeconds)) s"
Select-String -Path "$out\unity_$Test.log" -Encoding UTF8 -Pattern "error CS" | Select-Object -First 30 | ForEach-Object { $_.Line }
if (Test-Path "$out\$Test.txt") { Get-Content "$out\$Test.txt" -Encoding UTF8 }
