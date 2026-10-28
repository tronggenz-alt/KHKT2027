#!/usr/bin/env bash
set -euo pipefail

# tools/unity_dong_bo.sh
# Đồng bộ mã nguồn và tài nguyên giữa repo và thư mục làm việc ngoài iCloud

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_REPO="$REPO_DIR/unity/PhongLabHoa3D"
UNITY_EXTERNAL="$HOME/UnityProjects/PhongLabHoa3D"

EXCLUDES=(
    "--exclude=Library"
    "--exclude=Temp"
    "--exclude=Logs"
    "--exclude=Builds"
    "--exclude=UserSettings"
    "--exclude=Assets/TaiNguyenNgoai"
    "--exclude=.DS_Store"
    "--exclude=*.csproj"
    "--exclude=*.sln"
)

usage() {
    echo "Cách dùng: $0 [keo|day]"
    echo "  keo : Đồng bộ từ repo ($UNITY_REPO) sang thư mục làm việc ngoài iCloud ($UNITY_EXTERNAL)"
    echo "  day : Đồng bộ từ thư mục ngoài iCloud ($UNITY_EXTERNAL) về repo ($UNITY_REPO)"
    exit 1
}

if [ $# -lt 1 ]; then
    usage
fi

CMD="$1"

case "$CMD" in
    keo)
        echo "==> Đang kéo từ repo -> $UNITY_EXTERNAL ..."
        mkdir -p "$UNITY_EXTERNAL"
        if [ -d "$UNITY_REPO" ]; then
            rsync -av --delete "${EXCLUDES[@]}" "$UNITY_REPO/" "$UNITY_EXTERNAL/"
        else
            echo "Thư mục repo $UNITY_REPO chưa tồn tại."
        fi
        echo "==> Kéo thành công."
        ;;
    day)
        echo "==> Đang đẩy từ $UNITY_EXTERNAL -> repo ..."
        mkdir -p "$UNITY_REPO"
        if [ -d "$UNITY_EXTERNAL" ]; then
            rsync -av --delete "${EXCLUDES[@]}" "$UNITY_EXTERNAL/" "$UNITY_REPO/"
        else
            echo "Thư mục nguồn $UNITY_EXTERNAL chưa tồn tại."
        fi
        echo "==> Đẩy thành công."
        ;;
    *)
        usage
        ;;
esac
