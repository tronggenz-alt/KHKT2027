param(
    [switch]$Dung,                # dựng lại bản .exe đo từ bản sao riêng
    [string]$Exe = "exe_goc",     # thư mục bản .exe trong $root
    [string[]]$Luot = @("goc_1", "goc_2", "goc_3", "nap_1", "nap_2", "warm_all", "trace", "warm_gsc")
)
# Đo độ mượt lúc mở app và lần đầu vào lab. Dùng bản sao riêng để không tranh bản sao chung của các phiên khác.
$root = "D:\KHKT_kiem_thu_cache"
$proj = "$root\proj"
$nguon = "D:\KHKT 11 du phong\tools\kiem_thu_unity"
$ra = "$root\ket_qua\$Exe"
New-Item -ItemType Directory -Force $ra | Out-Null

if ($Dung) {
    New-Item -ItemType Directory -Force "$proj\Assets\Scripts\AutoTest", "$root\$Exe" | Out-Null
    Copy-Item "$nguon\KhktCacheBench.cs" "$proj\Assets\Scripts\AutoTest\" -Force
    Copy-Item "$nguon\KhktCacheBuild.cs" "$proj\Assets\Scripts\Editor\" -Force
    $env:KHKT_BUILD = "$root\$Exe"
    $t0 = Get-Date
    $u = Start-Process "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe" -ArgumentList "-batchmode","-projectPath","`"$proj`"","-executeMethod","KhktCacheBuild.BuildBench","-logFile","`"$ra\dung.log`"" -PassThru
    $xong = $u.WaitForExit(3000000)
    "Dựng: xong=$xong mã=$($u.ExitCode) sau $([math]::Round(((Get-Date)-$t0).TotalMinutes,1)) phút"
    Select-String -Path "$ra\dung.log" -Encoding UTF8 -Pattern "\[KHKT-BUILD\]|error CS" | Select-Object -First 15 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(240, $_.Line.Length)) }
    if (-not (Test-Path "$root\$Exe\PhongThiNghiemAo.exe")) { "KHÔNG có bản .exe"; exit 1 }
}

foreach ($l in $Luot) {
    $thamso = @("-screen-width","1920","-screen-height","1080","-screen-fullscreen","0","-logFile","`"$ra\$l.log`"","-khkt-cache-bench","`"$ra`"","-khkt-nhan",$l)
    if ($l -like "nap*") { $thamso += "-khkt-nap-truoc" }
    if ($l -eq "warm_all") { $thamso += "-khkt-warm-all" }
    if ($l -eq "trace") { $thamso += @("-khkt-gsc-trace","`"$ra\vera.graphicsstate`"") }
    if ($l -eq "warm_gsc") { $thamso += @("-khkt-gsc-warm","`"$ra\vera.graphicsstate`"") }
    if ($l -eq "goc_3") { $thamso += "-khkt-may-chu-chet" }
    $khac = @(Get-Process Unity -ErrorAction SilentlyContinue).Count
    $t0 = Get-Date
    $p = Start-Process "$root\$Exe\PhongThiNghiemAo.exe" -ArgumentList $thamso -PassThru
    $xong = $p.WaitForExit(900000)
    if (-not $xong) { Stop-Process -Id $p.Id -Force }
    "--- $l : xong=$xong, $([math]::Round(((Get-Date)-$t0).TotalSeconds)) s, Unity khác đang chạy khi bắt đầu: $khac"
    if (Test-Path "$ra\$l.txt") { Get-Content "$ra\$l.txt" -Encoding UTF8 } else { "(không có kết quả)"; Select-String -Path "$ra\$l.log" -Pattern "Exception|\[KHKT-CACHE\]" -Encoding UTF8 | Select-Object -Last 5 | ForEach-Object { $_.Line } }
}
