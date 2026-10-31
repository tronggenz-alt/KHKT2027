#!/usr/bin/env bash
# Biên dịch và chạy kiểm thử bộ máy hóa học / sinh học (tests/*.cs) bằng .NET đi kèm Unity trên macOS.
# Không cần mở Unity. Dùng:  bash tools/chay_kiem_thu_hoa.sh [tests/ChemEngineTests.cs]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DOTNET="/Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"
if [ ! -f "$DOTNET" ]; then
    # Thử tìm dotnet hệ thống nếu Unity không ở đường dẫn mặc định
    if which dotnet >/dev/null 2>&1; then
        DOTNET="$(which dotnet)"
    else
        echo "Lỗi: Không tìm thấy dotnet tại $DOTNET và trong PATH." >&2
        exit 1
    fi
fi

CSC=$(find /Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk -name "csc.dll" 2>/dev/null | grep bincore | head -n 1)
REF_DIR=$(find /Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/packs/Microsoft.NETCore.App.Ref -type d -name "net8.0" 2>/dev/null | sort | tail -n 1)

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

RUNTIME=$(basename "$(ls -d /Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/shared/Microsoft.NETCore.App/* 2>/dev/null | tail -n 1)")
cat << EOF > "$OUT_DIR/ChemTests.runtimeconfig.json"
{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$RUNTIME" } } }
EOF

export KHKT_ROOT="$ROOT"
"$DOTNET" exec --runtimeconfig "$OUT_DIR/ChemTests.runtimeconfig.json" "$OUT_DIR/ChemTests.dll"
