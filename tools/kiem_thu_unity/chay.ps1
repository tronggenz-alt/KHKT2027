param([string]$Mode = "", [string]$Tag = "")
# Chạy bài kiểm thử tự động trong bản sao project ở ổ D (dựng lại cảnh từ trình dựng rồi chạy chế độ $Mode; "" = bài đầy đủ)
$root = "D:\KHKT_kiem_thu"
$proj = "$root\proj"
if (-not $Tag) { $Tag = if ($Mode) { $Mode } else { "day_du" } }
robocopy "D:\KHKT 11 du phong\Phong thi nghiem ao\Assets\Scripts" "$proj\Assets\Scripts" /E /NJH /NJS /NDL /NP /NFL /XD AutoTest /XF KhktPlayVerify.cs KhktVerify.cs | Out-Null
robocopy "D:\KHKT 11 du phong\Phong thi nghiem ao\Assets\Lab\Data" "$proj\Assets\Lab\Data" /E /NJH /NJS /NDL /NP /NFL | Out-Null
robocopy "D:\KHKT 11 du phong\Phong thi nghiem ao\Assets\Lab\Logo" "$proj\Assets\Lab\Logo" /E /NJH /NJS /NDL /NP /NFL | Out-Null
foreach ($k in "NO_OCCLUSION", "NO_VWINDOW", "HLOD", "IMPOSTOR", "BLOOM_HQ", "AA") { Remove-Item "Env:KHKT_$k" -ErrorAction SilentlyContinue }
$out = "$root\ket_qua\$Tag"
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null
$env:KHKT_SHOTS = $out
if ($Mode) { $env:KHKT_TEST = $Mode } else { Remove-Item Env:KHKT_TEST -ErrorAction SilentlyContinue }
$t0 = Get-Date
$a = Start-Process "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe" -ArgumentList "-batchmode","-projectPath","`"$proj`"","-executeMethod","KhktPlayVerify.Run","-logFile","`"$out\unity.log`"" -PassThru
$ok = $a.WaitForExit(1800000)
"===== ${Tag}: exited=$ok code=$($a.ExitCode) sau $([math]::Round(((Get-Date)-$t0).TotalSeconds)) s"
Select-String -Path "$out\unity.log" -Encoding UTF8 -Pattern "\[KHKT-PLAY\]|error CS|Exception:" | Where-Object { $_.Line -notmatch 'ROBOT NÓI|StackTrace' } | ForEach-Object { $_.Line.Substring(0, [Math]::Min(400, $_.Line.Length)) }