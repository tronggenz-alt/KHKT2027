#!/bin/bash
# Bật gửi MÃ XÁC THỰC EMAIL (đăng ký / quên mật khẩu) + (tuỳ chọn) Cloudflare Turnstile ở form đăng ký.
# BẠN dán địa chỉ Gmail + App Password (và khoá Turnstile nếu có); script ghi vào .env (giữ nguyên các dòng khác, quyền 600),
# gửi thử một email cho chính bạn, rồi khởi động lại app. Không in mật khẩu/khoá ra màn hình. AI không sửa .env (AGENTS.md).
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
PY=".venv/bin/python"
dung() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "${1:-0}"; }
[ -x "$PY" ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; dung 1; }
[ -f .env ] || { cp .env.example .env; echo "Đã tạo .env từ .env.example."; }

echo "===== Gửi mã xác thực email bằng Gmail ====="
echo "Cần một App Password của Gmail (KHÔNG phải mật khẩu Gmail):"
echo "  1. Bật Xác minh 2 bước: https://myaccount.google.com/signinoptions/two-step-verification"
echo "  2. Tạo App Password:    https://myaccount.google.com/apppasswords  (đặt tên: CHEMISTRY) → chép 16 ký tự"
echo
read -r -p "1) Địa chỉ Gmail dùng để gửi mã (vd chemistry.app@gmail.com): " GMAIL
GMAIL="$(printf '%s' "$GMAIL" | tr -d '[:space:]"')"
if [[ ! "$GMAIL" =~ ^[^@[:space:]]+@[^@[:space:]]+\.[A-Za-z]{2,}$ ]]; then echo "LỖI: email không hợp lệ."; dung 1; fi
read -r -s -p "2) Dán App Password 16 ký tự (sẽ không hiện khi dán): " APP_PASS; echo
APP_PASS="$(printf '%s' "$APP_PASS" | tr -d '[:space:]"')"
if [ ${#APP_PASS} -lt 16 ]; then echo "LỖI: App Password phải đủ 16 ký tự."; dung 1; fi

echo
echo "----- (Tuỳ chọn) Cloudflare Turnstile – ô “Tôi không phải robot” ở form Tạo tài khoản -----"
echo "  dash.cloudflare.com → Turnstile → Add widget → Hostname: tên miền của web (vd trycloudflare.com nếu dùng"
echo "  Quick Tunnel, thêm localhost) → Widget mode: Managed → chép Site Key + Secret Key. Enter để bỏ qua."
read -r -p "3) Site Key (bắt đầu 0x…, Enter để bỏ qua): " TS_SITE
TS_SITE="$(printf '%s' "$TS_SITE" | tr -d '[:space:]"')"
TS_SECRET=""
if [ -n "$TS_SITE" ]; then
  read -r -s -p "4) Secret Key (sẽ không hiện khi dán): " TS_SECRET; echo
  TS_SECRET="$(printf '%s' "$TS_SECRET" | tr -d '[:space:]"')"
  [ -n "$TS_SECRET" ] || { echo "LỖI: có Site Key thì phải có Secret Key."; dung 1; }
fi

SMTP_USER="$GMAIL" SMTP_PASS="$APP_PASS" TS_SITE="$TS_SITE" TS_SECRET="$TS_SECRET" "$PY" - <<'EOF' || dung 1
"""Cập nhật .env: thay dòng KEY=... nếu đã có, không thì thêm cuối file. Không in giá trị."""
import os, re, stat
from pathlib import Path

p = Path(".env")
dong = p.read_text(encoding="utf-8").splitlines()

def dat(ten, v):
    for i, d in enumerate(dong):
        if re.match(rf"\s*{ten}\s*=", d):
            dong[i] = f"{ten}={v}"
            return
    dong.append(f"{ten}={v}")

moi = {"SMTP_HOST": "smtp.gmail.com", "SMTP_PORT": "587", "SMTP_USER": os.environ["SMTP_USER"], "SMTP_PASS": os.environ["SMTP_PASS"],
       "EMAIL_FROM": os.environ["SMTP_USER"]}
if os.environ.get("TS_SITE"):
    moi |= {"TURNSTILE_SITE_KEY": os.environ["TS_SITE"], "TURNSTILE_SECRET_KEY": os.environ["TS_SECRET"]}
if not any(re.match(r"\s*SMTP_USER\s*=", d) for d in dong):
    dong += ["", "# ----- Gửi mã xác thực email + Turnstile (ghi bởi cau-hinh-email.command)"]
for k, v in moi.items():
    dat(k, v)
p.write_text("\n".join(dong) + "\n", encoding="utf-8")
p.chmod(stat.S_IRUSR | stat.S_IWUSR)  # chỉ chủ máy đọc/ghi được
print("Đã ghi vào .env:", ", ".join(moi), "(không hiện giá trị)")
EOF

echo
echo "==> Gửi thử một email tới $GMAIL ..."
"$PY" - <<'EOF' || dung 1
import sys
from backend import config
config.settings = config.load_settings()
from backend import xac_thuc_email
xac_thuc_email.settings = config.settings
try:
    xac_thuc_email.gui_thu(config.settings.smtp_user, "CHEMISTRY – email thử",
                           "Cấu hình gửi mã xác thực của CHEMISTRY đã chạy. Bạn có thể xoá email này.",
                           "<p>Cấu hình gửi mã xác thực của <b>CHEMISTRY</b> đã chạy. Bạn có thể xoá email này.</p>")
except xac_thuc_email.LoiXacThuc as e:
    print("❌ Không gửi được:", e)
    print("   Kiểm tra: đúng địa chỉ Gmail, App Password 16 ký tự (tạo lại nếu cần), đã bật Xác minh 2 bước.")
    sys.exit(1)
print("✅ Đã gửi – mở hộp thư", config.settings.smtp_user, "để xem (có thể nằm trong Spam).")
EOF

echo
if launchctl print "gui/$(id -u)/vn.giaichuoihuuco.app" 2>/dev/null | grep -q "state = running"; then
  echo "==> Khởi động lại app để nạp cấu hình mới..."
  "$SCRIPT_DIR/tu-chay.command" khoi-dong-lai </dev/null || dung 1
else
  echo "App không chạy nền: hãy bật lại (bat-tat.command / start.command) để nạp cấu hình mới."
fi
echo "Xong: tạo tài khoản mới sẽ nhận mã 6 số qua email; tài khoản email cũ (không phải admin) xác thực ở lần đăng nhập tới."
dung 0
