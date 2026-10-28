#!/usr/bin/env bash
set -e

# ==============================================================================
# desktop/build-mac.sh
# Đóng gói toàn bộ ứng dụng CHEMISTRY (macOS arm64 / Apple Silicon)
# Thực hiện ngoài iCloud tại ~/AppBuild/CHEMISTRY/ để tránh nghẽn I/O và đồng bộ.
# ==============================================================================

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="$HOME/AppBuild/CHEMISTRY"
OUTPUT_DIR="$BUILD_DIR/dist"

echo "======================================================================"
echo "🧪 BẮT ĐẦU ĐÓNG GÓI ỨNG DỤNG CHEMISTRY (macOS)"
echo "Thư mục dự án: $DIR"
echo "Thư mục build : $BUILD_DIR"
echo "======================================================================"

# 1. Kiểm tra không gian ổ đĩa
echo "-> [1/8] Kiểm tra dung lượng ổ đĩa..."
df -h ~

# 2. Đảm bảo vendor offline và icon app đầy đủ
echo "-> [2/8] Kiểm tra thư viện vendor và bộ icon..."
"$DIR/.venv/bin/python3" "$DIR/tools/tai_vendor.py"
"$DIR/.venv/bin/python3" "$DIR/tools/tao_icon_app.py"

# 3. Đóng gói máy chủ Python (PyInstaller onedir)
echo "-> [3/8] Đóng gói máy chủ Python backend bằng PyInstaller..."
mkdir -p "$BUILD_DIR/may_chu"
"$DIR/.venv/bin/pyinstaller" \
  --noconfirm \
  --clean \
  --distpath "$BUILD_DIR/may_chu/dist" \
  --workpath "$BUILD_DIR/may_chu/build" \
  "$DIR/desktop/may_chu/chemistry.spec"

# Kiểm tra file thực thi máy chủ
SERVER_EXE="$BUILD_DIR/may_chu/dist/may_chu_chemistry/may_chu_chemistry"
if [ ! -f "$SERVER_EXE" ]; then
  echo "❌ Lỗi: Không tạo được file thực thi máy chủ: $SERVER_EXE"
  exit 1
fi
echo "✔ Máy chủ Python đã đóng gói thành công: $SERVER_EXE"

# 4. Chuẩn bị thư mục desktop ngoài iCloud
echo "-> [4/8] Chuẩn bị mã nguồn desktop tại $BUILD_DIR/desktop..."
mkdir -p "$BUILD_DIR/desktop"
rsync -a --exclude 'node_modules' --exclude 'dist' "$DIR/desktop/" "$BUILD_DIR/desktop/"

# Đưa bản build máy chủ Python vào may_chu/dist
mkdir -p "$BUILD_DIR/desktop/may_chu/dist"
rm -rf "$BUILD_DIR/desktop/may_chu/dist/may_chu_chemistry"
cp -R "$BUILD_DIR/may_chu/dist/may_chu_chemistry" "$BUILD_DIR/desktop/may_chu/dist/"

# Đưa app Unity PhongLabHoa3D.app vào may_chu/dist
UNITY_SRC=""
if [ -d "$DIR/outputs/app-lab/PhongLabHoa3D.app" ]; then
  UNITY_SRC="$DIR/outputs/app-lab/PhongLabHoa3D.app"
elif [ -d "$HOME/UnityProjects/PhongLabHoa3D/Builds/macOS/PhongLabHoa3D.app" ]; then
  UNITY_SRC="$HOME/UnityProjects/PhongLabHoa3D/Builds/macOS/PhongLabHoa3D.app"
fi

if [ -n "$UNITY_SRC" ]; then
  echo "   Sao chép app Unity từ $UNITY_SRC..."
  rm -rf "$BUILD_DIR/desktop/may_chu/dist/PhongLabHoa3D.app"
  cp -R "$UNITY_SRC" "$BUILD_DIR/desktop/may_chu/dist/PhongLabHoa3D.app"
  chmod -R +x "$BUILD_DIR/desktop/may_chu/dist/PhongLabHoa3D.app/Contents/MacOS"
  xattr -cr "$BUILD_DIR/desktop/may_chu/dist/PhongLabHoa3D.app" || true
else
  echo "⚠ Cảnh báo: Không tìm thấy file build PhongLabHoa3D.app!"
fi

# Đưa bản WebGL lab vào may_chu/dist/lab-webgl
WEBGL_SRC=""
if [ -d "$DIR/outputs/app-lab/webgl" ]; then
  WEBGL_SRC="$DIR/outputs/app-lab/webgl"
elif [ -d "$HOME/UnityProjects/PhongLabHoa3D/Builds/WebGL/lab" ]; then
  WEBGL_SRC="$HOME/UnityProjects/PhongLabHoa3D/Builds/WebGL/lab"
fi

if [ -n "$WEBGL_SRC" ]; then
  echo "   Sao chép WebGL lab từ $WEBGL_SRC..."
  rm -rf "$BUILD_DIR/desktop/may_chu/dist/lab-webgl"
  mkdir -p "$BUILD_DIR/desktop/may_chu/dist/lab-webgl"
  cp -R "$WEBGL_SRC/" "$BUILD_DIR/desktop/may_chu/dist/lab-webgl/"
else
  echo "⚠ Cảnh báo: Không tìm thấy thư mục build WebGL lab! App sẽ dùng chế độ Three.js."
fi

# 5. Cài đặt các gói Electron
echo "-> [5/8] Kiểm tra môi trường Electron & dependencies..."
cd "$BUILD_DIR/desktop"
if [ ! -d "node_modules/electron" ]; then
  npm install
fi

# Đảm bảo binary Electron được giải nén đúng
if ! npx electron -v >/dev/null 2>&1; then
  echo "   Đang thiết lập binary Electron từ cache..."
  CACHE_ZIP=$(find "$HOME/Library/Caches/electron" -name "electron-*-darwin-*.zip" | head -n 1)
  if [ -n "$CACHE_ZIP" ]; then
    rm -rf node_modules/electron/dist
    mkdir -p node_modules/electron/dist
    unzip -q "$CACHE_ZIP" -d node_modules/electron/dist
    echo -n "Electron.app/Contents/MacOS/Electron" > node_modules/electron/path.txt
    echo "33.4.11" > node_modules/electron/dist/version
  fi
fi

# 6. Đóng gói Electron App bằng electron-builder
echo "-> [6/8] Chạy electron-builder (arm64)..."
rm -rf dist
npx electron-builder --mac --arm64

# 7. Ký ad-hoc codesign cho app bundle
echo "-> [7/8] Ký mã nguồn ad-hoc codesign..."
APP_BUNDLE=$(find "$BUILD_DIR/desktop/dist" -name "CHEMISTRY.app" -type d | head -n 1)
if [ -d "$APP_BUNDLE" ]; then
  xattr -cr "$APP_BUNDLE" || true
  codesign --force --deep -s - "$APP_BUNDLE"
  echo "✔ Đã ký ad-hoc thành công cho: $APP_BUNDLE"
else
  echo "❌ Không tìm thấy CHEMISTRY.app trong dist!"
  exit 1
fi

# 8. Chuyển sản phẩm xuất ra $OUTPUT_DIR
echo "-> [8/8] Hoàn thiện bản phân phối tại $OUTPUT_DIR..."
mkdir -p "$OUTPUT_DIR"
rm -rf "$OUTPUT_DIR/CHEMISTRY.app"
cp -R "$APP_BUNDLE" "$OUTPUT_DIR/CHEMISTRY.app"

# Tạo bản nén zip phân phối
echo "   Tạo file nén CHEMISTRY-mac.zip..."
(cd "$OUTPUT_DIR" && rm -f "CHEMISTRY-mac.zip" && zip -r -y -q "CHEMISTRY-mac.zip" "CHEMISTRY.app")

# Di chuyển hoặc đồng bộ DMG nếu electron-builder đã tạo
DMG_SRC=$(find "$BUILD_DIR/desktop/dist" -name "*.dmg" | head -n 1)
if [ -n "$DMG_SRC" ] && [ -f "$DMG_SRC" ]; then
  cp "$DMG_SRC" "$OUTPUT_DIR/CHEMISTRY-mac.dmg"
else
  # Tạo DMG bằng hdiutil native
  echo "   Tạo file đĩa CHEMISTRY-mac.dmg bằng hdiutil..."
  hdiutil create -volname "CHEMISTRY" -srcfolder "$OUTPUT_DIR/CHEMISTRY.app" -ov -format UDZO "$OUTPUT_DIR/CHEMISTRY-mac.dmg"
fi

echo "======================================================================"
echo "🎉 ĐÓNG GÓI HOÀN TẤT!"
echo "Các file cài đặt tại: $OUTPUT_DIR"
ls -lh "$OUTPUT_DIR"
echo "SHA-256 bộ cài: $(shasum -a 256 "$OUTPUT_DIR/CHEMISTRY-mac.dmg" | cut -d' ' -f1)"
echo "Phát hành bản cập nhật cho app đang chạy: xem tools/phat_hanh_app.py (tải .dmg lên nơi công khai rồi tạo config/phien_ban_app.json)."
echo "======================================================================"
