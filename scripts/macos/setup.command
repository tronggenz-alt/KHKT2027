#!/bin/bash
# Cài đặt "CHEMISTRY": tạo .venv, cài thư viện, kiểm tra Chrome cho Playwright, tạo .env.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
mkdir -p logs outputs
LOG="logs/setup.log"
PY="/opt/homebrew/bin/python3.11"

say() { echo "$1" | tee -a "$LOG"; }
pause_exit() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "$1"; }

echo "===== $(date '+%Y-%m-%d %H:%M:%S') =====" >> "$LOG"
say "==> Cài đặt CHEMISTRY"

if [ ! -x "$PY" ]; then
  say "LỖI: không tìm thấy $PY. Hãy cài bằng lệnh: brew install python@3.11"
  pause_exit 1
fi

if [ ! -d ".venv" ]; then
  say "==> Tạo môi trường ảo .venv bằng Python 3.11"
  "$PY" -m venv .venv >> "$LOG" 2>&1 || { say "LỖI: không tạo được .venv (xem $LOG)"; pause_exit 1; }
fi
# shellcheck disable=SC1091
source .venv/bin/activate

say "==> Cài thư viện (có thể mất vài phút)..."
python -m pip install --upgrade pip >> "$LOG" 2>&1
if ! python -m pip install -r requirements.txt 2>&1 | tee -a "$LOG" | grep -E "^(Successfully|ERROR)"; then :; fi
python -c "import fastapi, anthropic, rdkit, playwright, PIL" 2>> "$LOG" || { say "LỖI: cài thư viện chưa thành công (xem $LOG)"; pause_exit 1; }

# Playwright điều khiển Google Chrome có sẵn (channel="chrome") nên không cần tải Chromium riêng.
if [ -d "/Applications/Google Chrome.app" ]; then
  say "==> Đã thấy Google Chrome, kiểm tra Playwright..."
  if python - >> "$LOG" 2>&1 <<'EOF'
from playwright.sync_api import sync_playwright
with sync_playwright() as p:
    b = p.chromium.launch(channel="chrome", headless=True)
    b.close()
EOF
  then say "    Playwright + Chrome: OK"
  else say "    Cảnh báo: Playwright chưa mở được Chrome (xem $LOG). App vẫn chạy, chỉ bỏ qua tra cứu web."
  fi
else
  say "Cảnh báo: không thấy Google Chrome trong /Applications. Tra cứu web sẽ bị bỏ qua."
fi

if [ ! -f ".env" ]; then
  cp .env.example .env
  say "==> Đã tạo file .env. Hãy dán API key vào dòng ANTHROPIC_API_KEY= rồi lưu lại."
  open -a TextEdit .env
elif ! python -c "from backend.config import settings; import sys; sys.exit(0 if settings.api_key else 1)" 2>/dev/null; then
  say "==> File .env chưa có ANTHROPIC_API_KEY. Hãy dán key rồi lưu lại."
  open -a TextEdit .env
fi

chmod +x start.command setup.command 2>/dev/null
say "==> Xong! Nháy đúp start.command để chạy app."
pause_exit 0
