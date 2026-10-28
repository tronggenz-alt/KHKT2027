#!/bin/bash
# MỘT NÚT bật/tắt web CHEMISTRY: nháy đúp →
#   đang chạy → TẮT (app + Cloudflare Tunnel, bỏ tự chạy khi mở máy)
#   đang tắt  → BẬT (app + Cloudflare Tunnel, tự chạy lại mỗi khi mở máy)
# Có thể kéo file này vào Dock (phía bên phải vạch ngăn) để bấm một lần.
# "$SCRIPT_DIR/bat-tat.command" --thu : chỉ in việc sẽ làm, không đổi gì.
DICH="$(readlink "$0")"
SCRIPT_DIR="$(cd "$(dirname "${DICH:-$0}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
NHAN_APP="vn.giaichuoihuuco.app"

thong_bao() { osascript -e "display notification \"$1\" with title \"CHEMISTRY\"" >/dev/null 2>&1; }

if launchctl print "gui/$(id -u)/$NHAN_APP" 2>/dev/null | grep -q "state = running"; then
  if [ "$1" = "--thu" ]; then echo "Đang chạy → sẽ TẮT."; exit 0; fi
  echo "===== Đang TẮT web CHEMISTRY ====="
  "$SCRIPT_DIR/tu-chay.command" tat </dev/null
  thong_bao "Đã TẮT web (app + Cloudflare Tunnel)."
else
  if [ "$1" = "--thu" ]; then echo "Đang tắt → sẽ BẬT."; exit 0; fi
  echo "===== Đang BẬT web CHEMISTRY ====="
  if DONG_Y=1 "$SCRIPT_DIR/tu-chay.command" bat </dev/null; then
    WEB="$(head -1 data/dia-chi-web.txt 2>/dev/null)"
    thong_bao "Đã BẬT: ${WEB:-http://127.0.0.1:8000}"
    [ -n "$WEB" ] && open "$WEB"
  else
    thong_bao "Bật KHÔNG thành công – xem cửa sổ Terminal."
  fi
fi
echo
read -r -t 8 -p "Cửa sổ tự đóng sau 8 giây (Enter để đóng ngay)..." _ 2>/dev/null
exit 0
