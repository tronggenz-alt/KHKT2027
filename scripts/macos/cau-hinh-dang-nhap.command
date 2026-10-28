#!/bin/bash
# Bật THÊM nút "Tiếp tục với Google" (tuỳ chọn – đăng nhập email + mật khẩu đã chạy sẵn, không cần cấu hình):
# BẠN dán Client ID / Client secret (lấy ở Google Cloud Console), script ghi vào .env (giữ nguyên các dòng khác, quyền 600),
# khởi động lại app và kiểm tra. Khoá mã hoá do app tự quản lý (data/app_secret.key) – script không tạo/đổi APP_SECRET_KEY.
# Không in key/secret ra màn hình. AI không sửa .env (AGENTS.md) – người dùng tự chạy script này.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
PY=".venv/bin/python"
dung() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "${1:-0}"; }
[ -x "$PY" ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; dung 1; }
[ -f .env ] || { cp .env.example .env; echo "Đã tạo .env từ .env.example."; }

echo "===== Bật thêm đăng nhập bằng Google (tuỳ chọn) ====="
WEB=""
[ -f data/dia-chi-web.txt ] && WEB=$(head -1 data/dia-chi-web.txt)
if [ -n "$WEB" ]; then
  echo "Địa chỉ web hiện tại (từ tu-chay.command): $WEB"
  echo "Trong Google Cloud Console, OAuth client (Web application) phải có:"
  echo "  Authorized JavaScript origins: $WEB"
  echo "  Authorized redirect URIs:      $WEB/auth/callback"
  echo "  (thêm cả http://127.0.0.1:8000 và http://127.0.0.1:8000/auth/callback để dùng trên máy này)"
fi
echo

read -r -p "1) Dán Client ID (đuôi .apps.googleusercontent.com): " CLIENT_ID
CLIENT_ID="$(printf '%s' "$CLIENT_ID" | tr -d '[:space:]"')"
if [[ ! "$CLIENT_ID" =~ ^[0-9]+-[A-Za-z0-9_]+\.apps\.googleusercontent\.com$ ]]; then
  echo "LỖI: Client ID không đúng dạng (vd 1234567890-abc123.apps.googleusercontent.com)."; dung 1
fi
read -r -s -p "2) Dán Client secret (bắt đầu GOCSPX-, sẽ không hiện khi dán): " CLIENT_SECRET; echo
CLIENT_SECRET="$(printf '%s' "$CLIENT_SECRET" | tr -d '[:space:]"')"
if [ ${#CLIENT_SECRET} -lt 10 ]; then echo "LỖI: Client secret trống hoặc quá ngắn."; dung 1; fi
read -r -p "3) Email của bạn (chủ app – dùng key của chủ app không giới hạn; Enter để bỏ qua): " ADMIN
ADMIN="$(printf '%s' "$ADMIN" | tr -d '[:space:]"')"
REDIRECT=""
if [ -n "$WEB" ]; then
  read -r -p "4) Ghi OAUTH_REDIRECT_URI=$WEB/auth/callback vào .env? [c/k]: " tl
  [[ "$tl" =~ ^[cCyY] ]] && REDIRECT="$WEB/auth/callback"
fi

GOOGLE_CLIENT_ID="$CLIENT_ID" GOOGLE_CLIENT_SECRET="$CLIENT_SECRET" ADMIN_EMAILS="$ADMIN" OAUTH_REDIRECT_URI="$REDIRECT" \
"$PY" - <<'EOF' || dung 1
"""Cập nhật .env: thay dòng KEY=... nếu đã có, không thì thêm cuối file. KHÔNG đụng APP_SECRET_KEY
(app tự dùng data/app_secret.key; thêm/đổi APP_SECRET_KEY là mọi key Gemini đã lưu phải nhập lại)."""
import os, re, stat
from pathlib import Path

p = Path(".env")
dong = p.read_text(encoding="utf-8").splitlines()

def gia_tri(ten):
    for d in dong:
        m = re.match(rf"\s*{ten}\s*=\s*(.*)$", d)
        if m:
            return m.group(1).strip().strip('"').strip("'")
    return ""

def dat(ten, v):
    for i, d in enumerate(dong):
        if re.match(rf"\s*{ten}\s*=", d):
            dong[i] = f"{ten}={v}"
            return
    dong.append(f"{ten}={v}")

moi = {"GOOGLE_CLIENT_ID": os.environ["GOOGLE_CLIENT_ID"], "GOOGLE_CLIENT_SECRET": os.environ["GOOGLE_CLIENT_SECRET"]}
if os.environ.get("ADMIN_EMAILS"):
    moi["ADMIN_EMAILS"] = os.environ["ADMIN_EMAILS"]
if os.environ.get("OAUTH_REDIRECT_URI"):
    moi["OAUTH_REDIRECT_URI"] = os.environ["OAUTH_REDIRECT_URI"]
if not any(re.match(r"\s*GOOGLE_CLIENT_ID\s*=", d) for d in dong):
    dong.append("")
    dong.append("# ----- Đăng nhập Google + key Gemini riêng (ghi bởi cau-hinh-dang-nhap.command)")
for k, v in moi.items():
    dat(k, v)
p.write_text("\n".join(dong) + "\n", encoding="utf-8")
p.chmod(stat.S_IRUSR | stat.S_IWUSR)  # chỉ chủ máy đọc/ghi được
print("Đã ghi vào .env:", ", ".join(moi), "(không hiện giá trị)")
EOF

echo
if launchctl print "gui/$(id -u)/vn.giaichuoihuuco.app" 2>/dev/null | grep -q "state = running"; then
  echo "==> Khởi động lại app để nạp cấu hình mới..."
  "$SCRIPT_DIR/tu-chay.command" khoi-dong-lai </dev/null || dung 1
else
  echo "App không chạy nền: hãy (khởi động lại) start.command để nạp cấu hình mới."
fi

"$PY" -c "
from backend.config import settings
from backend.bao_mat import ma_hoa_san_sang
ok = settings.dang_nhap_google and ma_hoa_san_sang()
print('✅ Nút \"Tiếp tục với Google\": ĐÃ BẬT' if ok else '⚠  Cấu hình chưa đủ – kiểm tra lại .env')
print('   Chủ app:', ', '.join(sorted(settings.admin_emails)) or '(chưa đặt)')
"
echo "Mở app → trang Đăng nhập có thêm nút 'Tiếp tục với Google'. Lỗi redirect_uri_mismatch → kiểm tra lại redirect URI trong Google Cloud Console."
dung 0
