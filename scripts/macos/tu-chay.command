#!/bin/bash
# Chạy "CHEMISTRY" mỗi khi máy Mac này bật + Cloudflare Tunnel (cách 1):
#   - app tự chạy khi đăng nhập macOS và tự chạy lại nếu bị lỗi (launchd, ~/Library/LaunchAgents)
#   - KHÔNG chặn máy ngủ: máy ngủ/tắt thì web tạm không vào được, máy thức/bật lại thì app + tunnel tự chạy tiếp
#   - Cloudflare Tunnel: tên miền cố định (named tunnel) hoặc địa chỉ trycloudflare.com tạm thời
# Dùng: nháy đúp để mở menu, hoặc: "$SCRIPT_DIR/tu-chay.command" bat|ten-mien [địa chỉ]|trang-thai|khoi-dong-lai|tat
# KHONG_TUNNEL=1 "$SCRIPT_DIR/tu-chay.command" bat  → chỉ tự chạy app trên máy (không mở ra Internet)
# DONG_Y=1 "$SCRIPT_DIR/tu-chay.command" bat         → tự trả lời "có" mọi câu hỏi (vd dùng địa chỉ tạm trycloudflare.com)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
THU_MUC="$ROOT_DIR"
UID_NGUOI_DUNG="$(id -u)"
NHAN_APP="vn.giaichuoihuuco.app"
NHAN_TUNNEL="vn.giaichuoihuuco.tunnel"
LA="$HOME/Library/LaunchAgents"
PLIST_APP="$LA/$NHAN_APP.plist"
PLIST_TUNNEL="$LA/$NHAN_TUNNEL.plist"
CAU_HINH="$THU_MUC/data/tunnel.env"      # CHE_DO=co-dinh|nhanh, TEN_TUNNEL, TEN_MIEN (không commit)
LOG_APP="$THU_MUC/logs/tu-chay-app.log"
# cloudflared không có quyền vào ~/Documents (quyền riêng tư macOS) → thư mục làm việc + nhật ký của tunnel để ở ~/Library/Logs
THU_MUC_TUNNEL="$HOME/Library/Logs/GiaiChuoiHuuCo"
LOG_TUNNEL="$THU_MUC_TUNNEL/tunnel.log"
PORT=8000
URL_MAY="http://127.0.0.1:$PORT"
CLOUDFLARED="$(command -v cloudflared || echo /opt/homebrew/bin/cloudflared)"
mkdir -p "$LA" "$THU_MUC/logs" "$THU_MUC/data" "$THU_MUC_TUNNEL"

co_tty() { [ -t 0 ]; }
dung_lai() { if co_tty; then echo; read -r -p "Nhấn Enter để tiếp tục..." _; fi; }
hoi() { # hoi "câu hỏi" → 0 nếu trả lời có (DONG_Y=1: tự trả lời có, dùng khi chạy không có người ngồi trước máy)
  [ -n "$DONG_Y" ] && { echo "$1 → có (DONG_Y=1)"; return 0; }
  co_tty || return 1
  local tl; read -r -p "$1 [c/k]: " tl; [[ "$tl" =~ ^[cCyY] ]]
}
xml() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'; }
doc_cau_hinh() { CHE_DO=""; TEN_TUNNEL=""; TEN_MIEN=""; [ -f "$CAU_HINH" ] && . "$CAU_HINH"; }

# ---------------------------------------------------------------- launchd
da_nap() { launchctl print "gui/$UID_NGUOI_DUNG/$1" >/dev/null 2>&1; }
go_dich_vu() { # gỡ dịch vụ và CHỜ launchd gỡ xong (nạp lại ngay sẽ lỗi "Bootstrap failed: 5")
  launchctl bootout "gui/$UID_NGUOI_DUNG/$1" >/dev/null 2>&1
  for _ in $(seq 1 40); do da_nap "$1" || return 0; sleep 0.25; done
}
nap() { # nap <nhãn> <plist>
  go_dich_vu "$1"
  local lan
  for lan in 1 2 3; do
    launchctl bootstrap "gui/$UID_NGUOI_DUNG" "$2" 2>/dev/null && break
    [ "$lan" = 3 ] && { echo "LỖI: launchd không nạp được $2"; return 1; }
    sleep 1
  done
  launchctl enable "gui/$UID_NGUOI_DUNG/$1"
  launchctl kickstart "gui/$UID_NGUOI_DUNG/$1" >/dev/null 2>&1
  return 0
}
go() { go_dich_vu "$1"; rm -f "$2"; }
dang_chay() { launchctl print "gui/$UID_NGUOI_DUNG/$1" 2>/dev/null | grep -q "state = running"; }

viet_plist() { # viet_plist <nhãn> <log> <thư mục làm việc> <đối số...>
  local nhan="$1" log="$2" thu_muc="$3"; shift 3
  local args="" a
  for a in "$@"; do args+="    <string>$(xml "$a")</string>"$'\n'; done
  cat <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>$nhan</string>
  <key>ProgramArguments</key>
  <array>
$args  </array>
  <key>WorkingDirectory</key><string>$(xml "$thu_muc")</string>
  <key>EnvironmentVariables</key>
  <dict>
    <key>PATH</key><string>/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin</string>
    <key>PYTHONUNBUFFERED</key><string>1</string>
    <key>KHONG_MO_TRINH_DUYET</key><string>1</string>
  </dict>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>ThrottleInterval</key><integer>10</integer>
  <key>ProcessType</key><string>Interactive</string>
  <key>StandardOutPath</key><string>$(xml "$log")</string>
  <key>StandardErrorPath</key><string>$(xml "$log")</string>
</dict>
</plist>
EOF
}

# ---------------------------------------------------------------- kiểm tra trước khi bật
kiem_tra_cau_hinh_dang_nhap() { # chỉ thông báo: đăng nhập email + mật khẩu luôn có, Google là tuỳ chọn
  local google
  google=$("$THU_MUC/.venv/bin/python" -c "from backend.config import settings; print(int(settings.dang_nhap_google))" 2>/dev/null)
  if [ "$google" = "1" ]; then echo "Đăng nhập: email + mật khẩu và Google."
  else echo "Đăng nhập: email + mật khẩu (muốn thêm nút Google: chạy cau-hinh-dang-nhap.command)."; fi
  return 0
}

giai_phong_cong() { # cổng 8000 đang bị app chạy bằng start.command (hoặc cách khác) chiếm → tắt nó
  dang_chay "$NHAN_APP" && return 0
  local pid lenh
  pid=$(lsof -nP -t -iTCP:$PORT -sTCP:LISTEN 2>/dev/null | head -1)
  [ -z "$pid" ] && return 0
  lenh=$(ps -o command= -p "$pid" 2>/dev/null)
  if [[ "$lenh" == *"uvicorn backend.main:app"* ]]; then
    echo "Đang tắt app chạy bằng start.command (PID $pid) để chế độ tự chạy dùng cổng $PORT..."
    kill "$pid"; sleep 2
    return 0
  fi
  echo "LỖI: cổng $PORT đang bị chương trình khác dùng: $lenh"
  echo "Hãy tắt chương trình đó rồi chạy lại."
  return 1
}

cho_app() {
  for _ in $(seq 1 60); do
    curl -fs "$URL_MAY/api/suc-khoe" >/dev/null 2>&1 && return 0
    sleep 0.5
  done
  return 1
}

dia_chi_web() {
  doc_cau_hinh
  if [ "$CHE_DO" = "co-dinh" ] && [ -n "$TEN_MIEN" ]; then
    echo "https://$TEN_MIEN"
  else
    grep -Eo 'https://[a-z0-9-]+\.trycloudflare\.com' "$LOG_TUNNEL" 2>/dev/null | tail -1
  fi
}

# ---------------------------------------------------------------- các lệnh
bat() {
  echo "===== Bật tự chạy khi máy bật ====="
  if [ ! -x "$THU_MUC/.venv/bin/python" ]; then echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; return 1; fi
  giai_phong_cong || return 1

  viet_plist "$NHAN_APP" "$LOG_APP" "$THU_MUC" \
    "$THU_MUC/.venv/bin/python" -m uvicorn backend.main:app --host 127.0.0.1 --port "$PORT" \
    --proxy-headers --forwarded-allow-ips 127.0.0.1 > "$PLIST_APP"
  plutil -lint "$PLIST_APP" >/dev/null || { echo "LỖI: không tạo được $PLIST_APP"; return 1; }
  nap "$NHAN_APP" "$PLIST_APP" || return 1
  echo "==> App: đang khởi động tại $URL_MAY ..."
  if cho_app; then
    echo "    App đã chạy (tự bật mỗi khi đăng nhập macOS, tự chạy lại khi lỗi; máy vẫn ngủ bình thường)."
  else
    echo "LỖI: app không phản hồi sau 30 giây. 20 dòng cuối $LOG_APP:"
    tail -20 "$LOG_APP"
    return 1
  fi

  if [ -n "$KHONG_TUNNEL" ]; then
    go "$NHAN_TUNNEL" "$PLIST_TUNNEL"
    echo "==> Không bật Cloudflare Tunnel (KHONG_TUNNEL=1): app chỉ mở được trên máy này."
    return 0
  fi
  if [ ! -x "$CLOUDFLARED" ]; then
    echo "LỖI: chưa có cloudflared. Cài bằng: brew install cloudflared"; return 1
  fi
  kiem_tra_cau_hinh_dang_nhap

  doc_cau_hinh
  if [ "$CHE_DO" != "co-dinh" ]; then
    echo
    echo "Chưa thiết lập tên miền cố định (menu 2). Nếu dùng địa chỉ tạm *.trycloudflare.com thì địa chỉ ĐỔI mỗi khi"
    echo "tunnel/máy khởi động lại → phải khai báo lại redirect URI trong Google Cloud Console mỗi lần."
    if { co_tty || [ -n "$DONG_Y" ]; } && ! hoi "Dùng địa chỉ tạm trycloudflare.com?"; then
      echo "→ Chọn menu 2 để thiết lập tên miền cố định trước."; return 1
    fi
    CHE_DO="nhanh"
  fi

  : > "$LOG_TUNNEL"
  if [ "$CHE_DO" = "co-dinh" ]; then
    viet_plist "$NHAN_TUNNEL" "$LOG_TUNNEL" "$THU_MUC_TUNNEL" "$CLOUDFLARED" tunnel --no-autoupdate run --url "$URL_MAY" "$TEN_TUNNEL" > "$PLIST_TUNNEL"
  else
    viet_plist "$NHAN_TUNNEL" "$LOG_TUNNEL" "$THU_MUC_TUNNEL" "$CLOUDFLARED" tunnel --no-autoupdate --url "$URL_MAY" > "$PLIST_TUNNEL"
  fi
  plutil -lint "$PLIST_TUNNEL" >/dev/null || { echo "LỖI: không tạo được $PLIST_TUNNEL"; return 1; }
  nap "$NHAN_TUNNEL" "$PLIST_TUNNEL" || return 1
  echo "==> Cloudflare Tunnel: đang kết nối..."
  local web=""
  for _ in $(seq 1 40); do
    web=$(dia_chi_web)
    if [ -n "$web" ] && grep -qiE "Registered tunnel connection|Connection .* registered" "$LOG_TUNNEL" 2>/dev/null; then break; fi
    sleep 1
  done
  if [ -z "$web" ]; then
    echo "LỖI: tunnel chưa lên. 20 dòng cuối $LOG_TUNNEL:"; tail -20 "$LOG_TUNNEL"; return 1
  fi
  echo "$web" > "$THU_MUC/data/dia-chi-web.txt"
  echo
  echo "✅ Web đang chạy: $web"
  echo "   Google Cloud Console → OAuth client: thêm origin  $web"
  echo "                                       và redirect  $web/auth/callback"
  [ "$CHE_DO" = "nhanh" ] && echo "   (địa chỉ tạm – đổi khi tunnel/máy khởi động lại)"
  return 0
}

ten_mien() {
  echo "===== Thiết lập tên miền cố định (named tunnel) – làm một lần ====="
  echo "Cần: một tên miền đã thêm vào tài khoản Cloudflare của bạn (mua ở Cloudflare Registrar hoặc nơi khác rồi đổi nameserver)."
  if [ ! -x "$CLOUDFLARED" ]; then echo "LỖI: chưa có cloudflared. Cài bằng: brew install cloudflared"; return 1; fi
  local host="$1" ten="giai-chuoi"
  if [ -z "$host" ]; then
    if ! co_tty; then echo "Dùng: "$SCRIPT_DIR/tu-chay.command" ten-mien <địa chỉ>, hoặc nháy đúp tu-chay.command."; return 1; fi
    read -r -p "Địa chỉ web muốn dùng (vd giaichuoi.ten-mien-cua-ban.com): " host
  fi
  host="${host#https://}"; host="${host#http://}"; host="${host%%/*}"
  if ! [[ "$host" =~ ^[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ ]]; then echo "Địa chỉ không hợp lệ."; return 1; fi

  if [ ! -f "$HOME/.cloudflared/cert.pem" ]; then
    echo "==> Đăng nhập Cloudflare: trình duyệt sẽ mở, chọn tên miền rồi bấm Authorize..."
    "$CLOUDFLARED" tunnel login || { echo "LỖI: đăng nhập Cloudflare không thành công."; return 1; }
  fi
  if ! "$CLOUDFLARED" tunnel list 2>/dev/null | awk '{print $2}' | grep -qx "$ten"; then
    echo "==> Tạo tunnel '$ten'..."
    "$CLOUDFLARED" tunnel create "$ten" || return 1
  fi
  echo "==> Trỏ $host → tunnel '$ten' (tạo bản ghi DNS trên Cloudflare)..."
  "$CLOUDFLARED" tunnel route dns "$ten" "$host" || return 1
  printf 'CHE_DO=co-dinh\nTEN_TUNNEL=%s\nTEN_MIEN=%s\n' "$ten" "$host" > "$CAU_HINH"
  echo
  echo "✅ Xong. Địa chỉ cố định: https://$host"
  echo "   1) Google Cloud Console → OAuth client: thêm origin https://$host và redirect https://$host/auth/callback"
  echo "   2) (nên) thêm vào .env: OAUTH_REDIRECT_URI=https://$host/auth/callback"
  if dang_chay "$NHAN_TUNNEL" || dang_chay "$NHAN_APP"; then
    hoi "Bật lại tự chạy với địa chỉ mới ngay?" && bat
  else
    echo "   3) Chọn menu 1 để bật tự chạy."
  fi
}

trang_thai() {
  echo "===== Trạng thái ====="
  if dang_chay "$NHAN_APP"; then
    if curl -fs "$URL_MAY/api/suc-khoe" >/dev/null 2>&1; then echo "App tự chạy:     ĐANG CHẠY ($URL_MAY)"
    else echo "App tự chạy:     đang khởi động hoặc lỗi – xem $LOG_APP"; fi
  elif [ -f "$PLIST_APP" ]; then echo "App tự chạy:     đã cài nhưng KHÔNG chạy – xem $LOG_APP"
  else echo "App tự chạy:     TẮT"; fi
  doc_cau_hinh
  if dang_chay "$NHAN_TUNNEL"; then
    local web; web=$(dia_chi_web)
    echo "Cloudflare:      ĐANG CHẠY (${CHE_DO:-nhanh}) → ${web:-chưa có địa chỉ}"
    if [ -n "$web" ]; then
      if curl -fs --max-time 15 "$web/api/suc-khoe" >/dev/null 2>&1; then echo "Truy cập từ Internet: OK"
      else echo "Truy cập từ Internet: chưa được (tunnel đang kết nối, hoặc DNS chưa cập nhật – thử lại sau 1–2 phút)"; fi
    fi
  elif [ -f "$PLIST_TUNNEL" ]; then echo "Cloudflare:      đã cài nhưng KHÔNG chạy – xem $LOG_TUNNEL"
  else echo "Cloudflare:      TẮT"; fi
  [ "$CHE_DO" = "co-dinh" ] && echo "Tên miền cố định: https://$TEN_MIEN (tunnel '$TEN_TUNNEL')"
  echo "Lưu ý:           web chỉ vào được khi máy đang bật và không ngủ (máy ngủ/tắt → tạm ngừng, bật lại → tự chạy)."
}

khoi_dong_lai() {
  if ! dang_chay "$NHAN_APP"; then echo "Chế độ tự chạy đang tắt – chọn menu 1 để bật."; return 1; fi
  echo "==> Khởi động lại app (nạp code backend/.env mới; bài đang giải dở sẽ bị dừng)..."
  launchctl kickstart -k "gui/$UID_NGUOI_DUNG/$NHAN_APP"
  if cho_app; then echo "    Xong – app đã chạy lại. (Tunnel không cần khởi động lại, địa chỉ web giữ nguyên.)"
  else echo "LỖI: app không chạy lại được. 20 dòng cuối $LOG_APP:"; tail -20 "$LOG_APP"; return 1; fi
}

tat() {
  go "$NHAN_TUNNEL" "$PLIST_TUNNEL"
  go "$NHAN_APP" "$PLIST_APP"
  echo "Đã tắt chế độ tự chạy (app + Cloudflare Tunnel) và bỏ tự chạy khi đăng nhập macOS."
  echo "Cấu hình tên miền vẫn giữ trong data/tunnel.env – bật lại bằng menu 1."
}

case "$1" in
  bat) bat; exit $? ;;
  ten-mien) ten_mien "$2"; exit $? ;;
  trang-thai) trang_thai; exit 0 ;;
  khoi-dong-lai) khoi_dong_lai; exit $? ;;
  tat) tat; exit 0 ;;
  "") ;;
  *) echo "Dùng: $0 [bat|ten-mien|trang-thai|khoi-dong-lai|tat]"; exit 1 ;;
esac

while true; do
  echo
  echo "===== CHEMISTRY – tự chạy khi máy bật ====="
  trang_thai
  echo
  echo "  1) Bật tự chạy (app + Cloudflare Tunnel, mỗi khi máy bật)"
  echo "  2) Thiết lập tên miền cố định (làm một lần)"
  echo "  3) Xem trạng thái"
  echo "  4) Khởi động lại app (sau khi sửa code backend hoặc .env)"
  echo "  5) Tắt tự chạy"
  echo "  0) Thoát"
  read -r -p "Chọn: " chon || exit 0
  case "$chon" in
    1) bat; dung_lai ;;
    2) ten_mien; dung_lai ;;
    3) trang_thai; dung_lai ;;
    4) khoi_dong_lai; dung_lai ;;
    5) tat; dung_lai ;;
    0|q) exit 0 ;;
  esac
done
