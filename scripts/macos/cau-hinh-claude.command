#!/bin/bash
# Đặt key Claude cho MÁY CHỦ – một script cho cả hai loại key, TỰ NHẬN RA loại theo phần đầu:
#   sk-ant-api03-…   → ANTHROPIC_API_KEY   (giải bài bằng Claude; kiểm tra bằng 1 lần gọi max_tokens 1)
#   sk-ant-admin01-… → ANTHROPIC_ADMIN_KEY (chỉ đọc chi phí + token ở /admin; kiểm tra bằng Admin API)
# Dán lần lượt bao nhiêu key cũng được (Enter trống = xong). Ghi .env (giữ nguyên các dòng khác, quyền 600) rồi khởi động lại app.
# Không in key ra màn hình (chỉ 4 ký tự cuối). AI không sửa .env (AGENTS.md).
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
PY=".venv/bin/python"
dung() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "${1:-0}"; }
[ -x "$PY" ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; dung 1; }
[ -f .env ] || { cp .env.example .env; echo "Đã tạo .env từ .env.example."; }

echo "===== Key Claude của máy chủ ====="
echo "  • API key   (sk-ant-api03-…):   platform.claude.com → API keys        → để GIẢI BÀI bằng Claude"
echo "  • Admin key (sk-ant-admin01-…): platform.claude.com → Settings → Admin keys (tài khoản Tổ chức) → để XEM CHI PHÍ ở /admin"
echo "  Dán key nào cũng được – script tự nhận ra loại. Key cũ hết tín dụng thì chỉ cần nạp tiền (Billing), không cần đổi key."
DA_GHI=0
while true; do
  echo
  read -r -s -p "Dán key (sẽ không hiện khi dán; Enter trống = xong): " KEY; echo
  KEY="$(printf '%s' "$KEY" | tr -d '[:space:]"'"'")"
  [ -n "$KEY" ] || break
  if KEY_MOI="$KEY" "$PY" - <<'EOF'
"""Nhận loại key → kiểm tra → ghi đúng biến trong .env. Thoát 0 = đã ghi, 1 = không ghi. Không in key."""
import os, re, stat, sys
from pathlib import Path

import requests

key = os.environ["KEY_MOI"]
if key.startswith("sk-ant-admin"):
    ten, loai = "ANTHROPIC_ADMIN_KEY", "Admin key (xem chi phí ở /admin)"
    try:
        r = requests.get("https://api.anthropic.com/v1/organizations/cost_report", params={"starting_at": "2026-01-01T00:00:00Z", "limit": 1},
                         headers={"x-api-key": key, "anthropic-version": "2023-06-01"}, timeout=25)
    except requests.RequestException:
        print("❌ Không kết nối được Anthropic để kiểm tra – thử lại sau. Không thay đổi gì.")
        sys.exit(1)
    if r.status_code != 200:
        print(f"❌ Admin key không dùng được (Admin API trả mã {r.status_code}: sai key / không có quyền / không phải tài khoản Tổ chức)."
              " Không thay đổi gì.")
        sys.exit(1)
else:
    from backend.claude_rieng import LoiKeyRieng, chuan_hoa, kiem_tra
    from backend.config import settings

    ten, loai = "ANTHROPIC_API_KEY", "API key (giải bài bằng Claude)"
    try:
        key = chuan_hoa(key)
        kq = kiem_tra(key)
    except LoiKeyRieng as e:
        print(f"❌ {e} Không thay đổi gì.")
        sys.exit(1)
    if not kq.get("hop_le"):
        print(f"❌ {kq.get('thong_diep') or 'Key không hợp lệ.'} Không thay đổi gì.")
        sys.exit(1)
    if kq.get("canh_bao"):
        print("⚠ " + kq["canh_bao"].replace("Key hợp lệ nhưng ", "").replace("của bạn ", ""))
print(f"✅ Nhận ra {loai} …{key[-4:]} – hợp lệ.")

p = Path(".env")
dong = p.read_text(encoding="utf-8").splitlines()
if ten == "ANTHROPIC_API_KEY":
    dong = [d for d in dong if not re.match(r"\s*sk-ant-(?!admin)", d)]  # bỏ dòng key "trần" cũ để không lẫn key
for i, d in enumerate(dong):
    if re.match(rf"\s*{ten}\s*=", d):
        dong[i] = f"{ten}={key}"
        break
else:
    dong += ["", f"# ----- {ten} (ghi bởi cau-hinh-claude.command)", f"{ten}={key}"]
p.write_text("\n".join(dong) + "\n", encoding="utf-8")
p.chmod(stat.S_IRUSR | stat.S_IWUSR)
print(f"Đã ghi {ten} vào .env (không hiện giá trị).")
EOF
  then DA_GHI=1; fi
  echo "(Dán thêm key loại kia nếu có, hoặc Enter để kết thúc.)"
done

[ "$DA_GHI" = 1 ] || { echo "Không có key nào được ghi."; dung 0; }
echo
if launchctl print "gui/$(id -u)/vn.giaichuoihuuco.app" 2>/dev/null | grep -q "state = running"; then
  echo "==> Khởi động lại app để nạp key mới..."
  "$SCRIPT_DIR/tu-chay.command" khoi-dong-lai </dev/null || dung 1
else
  echo "App không chạy nền: hãy bật lại (bat-tat.command / start.command) để nạp key mới."
fi
echo "Xong. API key → giải bài bằng Claude; Admin key → /admin → \"Claude API (máy chủ)\" hoặc: python3 admin_cli.py claude"
dung 0
