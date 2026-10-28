# ==============================================================================
# desktop/build-win.ps1
# Kịch bản đóng gói ứng dụng máy tính CHEMISTRY cho Windows (x64)
# Thực hiện trên máy Windows (hoặc PowerShell Core trên Windows)
# ==============================================================================

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "🧪 BẮT ĐẦU ĐÓNG GÓI ỨNG DỤNG CHEMISTRY (Windows x64)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = (Get-Item "$ScriptDir\..").FullName
$BuildDir = "$env:LOCALAPPDATA\AppBuild\CHEMISTRY"
$OutputDir = "$BuildDir\dist"

Write-Host "Thư mục dự án: $ProjectDir"
Write-Host "Thư mục build : $BuildDir"

# 1. Kiểm tra Python và Node.js
Write-Host "-> [1/6] Kiểm tra môi trường hệ thống..." -ForegroundColor Yellow
python --version
node --version
npm --version

# 2. Tạo assets và vendor
Write-Host "-> [2/6] Tạo bộ icon và kiểm tra vendor..." -ForegroundColor Yellow
python "$ProjectDir\tools\tai_vendor.py"
python "$ProjectDir\tools\tao_icon_app.py"

# 3. Đóng gói máy chủ Python bằng PyInstaller
Write-Host "-> [3/6] Đóng gói máy chủ Python backend bằng PyInstaller..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "$BuildDir\may_chu" | Out-Null
pyinstaller --noconfirm --clean `
  --distpath "$BuildDir\may_chu\dist" `
  --workpath "$BuildDir\may_chu\build" `
  "$ScriptDir\may_chu\chemistry.spec"

$ServerExe = "$BuildDir\may_chu\dist\may_chu_chemistry\may_chu_chemistry.exe"
if (-not (Test-Path $ServerExe)) {
    Write-Error "Không tìm thấy file thực thi máy chủ: $ServerExe"
}

# 4. Chuẩn bị thư mục desktop
Write-Host "-> [4/6] Sao chép tệp desktop..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "$BuildDir\desktop" | Out-Null
Copy-Item -Recurse -Force "$ScriptDir\*" "$BuildDir\desktop\" -Exclude "node_modules", "dist"

New-Item -ItemType Directory -Force -Path "$BuildDir\desktop\may_chu\dist" | Out-Null
Copy-Item -Recurse -Force "$BuildDir\may_chu\dist\may_chu_chemistry" "$BuildDir\desktop\may_chu\dist\"

# Sao chép WebGL lab nếu có
$WebGlSrc = "$ProjectDir\outputs\app-lab\webgl"
if (Test-Path $WebGlSrc) {
    Write-Host "   Sao chép WebGL lab từ $WebGlSrc..."
    Copy-Item -Recurse -Force $WebGlSrc "$BuildDir\desktop\may_chu\dist\lab-webgl"
} else {
    Write-Host "⚠ Cảnh báo: Không tìm thấy $WebGlSrc! App sẽ dùng chế độ Three.js." -ForegroundColor DarkYellow
}

# 5. Cài đặt npm dependencies và đóng gói electron-builder
Write-Host "-> [5/6] Đóng gói Electron (NSIS Installer & Portable)..." -ForegroundColor Yellow
Set-Location "$BuildDir\desktop"
npm install
npx electron-builder --win --x64

# 6. Thu gom sản phẩm xuất xưởng
Write-Host "-> [6/6] Thu gom bản cài đặt tại: $OutputDir" -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Copy-Item -Force "$BuildDir\desktop\dist\*.exe" $OutputDir

Write-Host "======================================================================" -ForegroundColor Green
Write-Host "🎉 HOÀN TẤT ĐÓNG GÓI CHO WINDOWS!" -ForegroundColor Green
Write-Host "File cài đặt nằm tại: $OutputDir" -ForegroundColor Green
Get-ChildItem $OutputDir
Write-Host "======================================================================" -ForegroundColor Green
