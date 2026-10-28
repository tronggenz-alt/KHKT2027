#!/bin/bash
# Chạy "CHEMISTRY": kiểm tra API key, chạy server (cổng 8000, bận thì 8001), đợi sẵn sàng rồi mở bằng Google Chrome.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
mkdir -p logs outputs
LOG="logs/server-$(date +%Y%m%d).log"

pause_exit() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "$1"; }
cong_trong() { ! lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1; }

echo "===== CHEMISTRY ====="
if [ ! -x ".venv/bin/python" ]; then
  echo "LỖI: chưa cài đặt. Hãy nháy đúp setup.command trước."
  pause_exit 1
fi
# shellcheck disable=SC1091
source .venv/bin/activate

# App đã tự chạy nền (tu-chay.command) → chỉ mở trình duyệt, không chạy thêm bản thứ hai
if launchctl print "gui/$(id -u)/vn.giaichuoihuuco.app" 2>/dev/null | grep -q "state = running" \
   && curl -fs "http://127.0.0.1:8000/api/suc-khoe" >/dev/null 2>&1; then
  echo "App đang tự chạy nền (tu-chay.command) tại http://127.0.0.1:8000 → mở trình duyệt."
  [ -z "$KHONG_MO_TRINH_DUYET" ] && { open -a "Google Chrome" "http://127.0.0.1:8000" 2>/dev/null || open "http://127.0.0.1:8000"; }
  echo "Muốn tắt/khởi động lại app: nháy đúp tu-chay.command."
  exit 0
fi

# Kiểm tra key (đọc .env qua backend.config – không in key ra màn hình)
KEY_STATE=$(python -c "
from backend.config import settings
if settings.api_key: print('claude')
elif settings.gemini_api_key and settings.gemini_fallback: print('gemini')
elif settings.dang_nhap_google: print('byok')
else: print('none')" 2>>"$LOG")
if [ "$KEY_STATE" = "byok" ]; then
  echo "Cảnh báo: .env không có key AI của chủ app → không có lượt dùng thử; người dùng phải đăng nhập và nhập key Gemini riêng."
elif [ "$KEY_STATE" = "none" ] || [ -z "$KEY_STATE" ]; then
  echo "LỖI: chưa có ANTHROPIC_API_KEY trong file .env."
  echo "Hãy mở file .env (nháy đúp setup.command sẽ mở giúp bằng TextEdit),"
  echo "dán API key vào dòng ANTHROPIC_API_KEY= rồi lưu lại và chạy lại start.command."
  pause_exit 1
elif [ "$KEY_STATE" = "gemini" ]; then
  echo "Cảnh báo: chưa có ANTHROPIC_API_KEY – app sẽ dùng Gemini (GEMINI_API_KEY) thay cho Claude."
fi

python -c "
from backend.config import settings
from backend.bao_mat import ma_hoa_san_sang
if settings.dang_nhap_google and not ma_hoa_san_sang():
    print('Cảnh báo: đã bật đăng nhập Google nhưng chưa có APP_SECRET_KEY (≥ 32 ký tự) trong .env → người dùng chưa lưu được key Gemini riêng, khởi động lại là phải đăng nhập lại.')
" 2>>"$LOG"

PORT=8000
if ! cong_trong 8000; then
  if cong_trong 8001; then
    PORT=8001
    echo "Cổng 8000 đang bận → dùng cổng 8001."
  else
    echo "LỖI: cổng 8000 và 8001 đều đang bận. Hãy tắt chương trình đang dùng các cổng này rồi thử lại."
    pause_exit 1
  fi
fi

URL="http://127.0.0.1:$PORT"
echo "==> Khởi động server tại $URL (nhật ký: $LOG)"
echo "===== $(date '+%Y-%m-%d %H:%M:%S') khởi động cổng $PORT =====" >> "$LOG"
python -m uvicorn backend.main:app --host 127.0.0.1 --port "$PORT" >> "$LOG" 2>&1 &
PID=$!
trap 'echo; echo "Đang dừng server..."; kill $PID 2>/dev/null; wait $PID 2>/dev/null; exit 0' INT TERM HUP

SAN_SANG=""
for _ in $(seq 1 60); do
  if curl -fs "$URL/api/suc-khoe" >/dev/null 2>&1; then SAN_SANG=1; break; fi
  if ! kill -0 "$PID" 2>/dev/null; then
    echo "LỖI: server không khởi động được. 20 dòng cuối nhật ký:"
    tail -20 "$LOG"
    pause_exit 1
  fi
  sleep 0.5
done
if [ -z "$SAN_SANG" ]; then
  echo "LỖI: server không phản hồi sau 30 giây. Xem $LOG"
  kill "$PID" 2>/dev/null
  pause_exit 1
fi

if [ -z "$KHONG_MO_TRINH_DUYET" ]; then
  open -a "Google Chrome" "$URL" 2>/dev/null || open "$URL"
fi
echo ""
echo "App đang chạy: $URL"
echo "Đóng cửa sổ này hoặc nhấn Ctrl+C để dừng app."
wait "$PID"
