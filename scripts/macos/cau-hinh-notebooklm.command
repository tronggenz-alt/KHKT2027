#!/bin/bash
# Tạo khoá mã hoá cookie NotebookLM (NOTEBOOKLM_COOKIE_KEY – AES-256-GCM) cho máy Mac này.
#   - Sinh ngẫu nhiên 32 byte, ghi vào .env (giữ nguyên các dòng khác, quyền 600) rồi khởi động lại app. KHÔNG in khoá ra màn hình.
#   - Đã có khoá → giữ nguyên (đổi khoá = cookie đã lưu không giải mã được, phải dán lại); chỉ đổi khi bạn gõ DOI.
#   - Trên Render: render.yaml tự sinh biến này (generateValue); không thấy thì Environment → Add → NOTEBOOKLM_COOKIE_KEY → Generate.
# AI không sửa .env (AGENTS.md) – bạn tự chạy script này.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
PY=".venv/bin/python"
dung() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "${1:-0}"; }
[ -x "$PY" ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; dung 1; }
[ -f .env ] || { cp .env.example .env; echo "Đã tạo .env từ .env.example."; }

echo "===== Khoá mã hoá cookie NotebookLM ====="
DOI=""
if "$PY" -c "from dotenv import dotenv_values; import sys; sys.exit(0 if len((dotenv_values('.env').get('NOTEBOOKLM_COOKIE_KEY') or '').strip()) >= 32 else 1)"; then
  echo "Đã có NOTEBOOKLM_COOKIE_KEY trong .env."
  echo "Đổi khoá thì mọi cookie NotebookLM đã lưu sẽ KHÔNG đọc được nữa (phải dán lại từng tài khoản)."
  read -r -p "Gõ DOI để tạo khoá mới, Enter = giữ khoá cũ: " DOI
  [ "$DOI" = "DOI" ] || { echo "Giữ nguyên khoá cũ. Không thay đổi gì."; dung 0; }
fi

if "$PY" - <<'EOF'
"""Sinh khoá 32 byte (base64 url-safe) → ghi NOTEBOOKLM_COOKIE_KEY vào .env. Không in khoá."""
import base64, os, re, stat
from pathlib import Path

khoa = base64.urlsafe_b64encode(os.urandom(32)).decode("ascii")
p = Path(".env")
dong = p.read_text(encoding="utf-8").splitlines()
for i, d in enumerate(dong):
    if re.match(r"\s*NOTEBOOKLM_COOKIE_KEY\s*=", d):
        dong[i] = f"NOTEBOOKLM_COOKIE_KEY={khoa}"
        break
else:
    dong += [f"NOTEBOOKLM_COOKIE_KEY={khoa}"]
p.write_text("\n".join(dong) + "\n", encoding="utf-8")
p.chmod(stat.S_IRUSR | stat.S_IWUSR)
print("✅ Đã ghi NOTEBOOKLM_COOKIE_KEY vào .env (32 byte ngẫu nhiên – không hiện giá trị).")
EOF
then :; else dung 1; fi

echo
if launchctl print "gui/$(id -u)/vn.giaichuoihuuco.app" 2>/dev/null | grep -q "state = running"; then
  echo "==> Khởi động lại app để nạp khoá mới..."
  "$SCRIPT_DIR/tu-chay.command" khoi-dong-lai </dev/null || dung 1
else
  echo "App không chạy nền: hãy bật lại (bat-tat.command / start.command) để nạp khoá mới."
fi
echo "Xong. Vào /admin → NotebookLM → dán cookie → Lưu & Kết nối."
dung 0
