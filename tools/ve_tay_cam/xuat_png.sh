#!/bin/sh
# Xuất các bản vẽ SVG trong docs/anh-tay-cam ra PNG bằng Edge chạy ẩn (Windows, Git Bash).
# Chạy:  sh tools/ve_tay_cam/xuat_png.sh
set -e
cd "$(dirname "$0")/../../docs/anh-tay-cam"
EDGE="/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"
for f in TC-0*.svg; do
  n=${f%.svg}
  w=$(grep -o 'width="[0-9]*"' "$f" | head -1 | grep -o '[0-9]*')
  h=$(grep -o 'height="[0-9]*"' "$f" | head -1 | grep -o '[0-9]*')
  "$EDGE" --headless=new --disable-gpu --hide-scrollbars --virtual-time-budget=6000 \
    --force-device-scale-factor=1.5 --window-size="$w,$h" \
    --screenshot="$(cygpath -w "$PWD")\\$n.png" "file:///$(cygpath -m "$PWD")/$f" 2>/dev/null
  echo "$n.png ${w}x${h}"
done
