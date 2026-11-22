#!/usr/bin/env bash
# Biên dịch và chạy kiểm thử bộ máy hóa học / sinh học (tests/*.cs) bằng .NET đi kèm Unity trên macOS.
# Không cần mở Unity. Dùng:  bash tools/chay_kiem_thu_hoa.sh [tests/ChemEngineTests.cs]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Unity nằm ở đâu: biến UNITY_APP nếu có, không thì thử lần lượt các chỗ đã dùng
# (bản trong ~/Documents đã dời đi từ khi chuyển dự án khỏi iCloud nên kịch bản cũ không chạy được nữa).
UNITY_SDK=""
for app in "${UNITY_APP:-}" "$HOME/AppBuild/UnityEditor/Unity.app" "$HOME/Documents/6000.6.7f1/Unity.app" /Applications/Unity/Hub/Editor/*/Unity.app; do
    if [ -n "$app" ] && [ -f "$app/Contents/Resources/Scripting/DotNetSdk/dotnet" ]; then
        UNITY_SDK="$app/Contents/Resources/Scripting/DotNetSdk"
        break
    fi
done
if [ -z "$UNITY_SDK" ]; then
    echo "Lỗi: không tìm thấy Unity (đặt UNITY_APP=/đường/dẫn/Unity.app rồi chạy lại)." >&2
    exit 1
fi
DOTNET="$UNITY_SDK/dotnet"

CSC=$(find "$UNITY_SDK" -name "csc.dll" 2>/dev/null | grep bincore | head -n 1)
REF_DIR=$(find "$UNITY_SDK/packs/Microsoft.NETCore.App.Ref" -type d -name "net8.0" 2>/dev/null | sort | tail -n 1)

OUT_DIR="/tmp/khkt_chem_tests"
mkdir -p "$OUT_DIR"

TEST_FILE="${1:-tests/ChemEngineTests.cs}"

cat << EOF > "$OUT_DIR/build.rsp"
-nologo
-target:exe
-langversion:latest
-nullable:disable
-out:$OUT_DIR/ChemTests.dll
EOF

# Thêm tham chiếu thư viện net8.0
find "$REF_DIR" -name "*.dll" | while read -r f; do
    echo "-r:\"$f\"" >> "$OUT_DIR/build.rsp"
done

# Thêm mã nguồn mô phỏng hóa học
find "$ROOT/Phong thi nghiem ao/Assets/Scripts/Simulation/Chem" -name "*.cs" | while read -r f; do
    echo "\"$f\"" >> "$OUT_DIR/build.rsp"
done

# Thêm mã nguồn mô phỏng sinh học nếu có
if [ -d "$ROOT/Phong thi nghiem ao/Assets/Scripts/Simulation/Bio" ]; then
    find "$ROOT/Phong thi nghiem ao/Assets/Scripts/Simulation/Bio" -name "*.cs" | while read -r f; do
        echo "\"$f\"" >> "$OUT_DIR/build.rsp"
    done
fi

echo "\"$ROOT/$TEST_FILE\"" >> "$OUT_DIR/build.rsp"

"$DOTNET" "$CSC" @"$OUT_DIR/build.rsp"

RUNTIME=$(basename "$(ls -d "$UNITY_SDK"/shared/Microsoft.NETCore.App/* 2>/dev/null | tail -n 1)")
cat << EOF > "$OUT_DIR/ChemTests.runtimeconfig.json"
{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$RUNTIME" } } }
EOF

export KHKT_ROOT="$ROOT"
"$DOTNET" exec --runtimeconfig "$OUT_DIR/ChemTests.runtimeconfig.json" "$OUT_DIR/ChemTests.dll"
