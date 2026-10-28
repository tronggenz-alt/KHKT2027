#!/bin/bash
# Đặt key ChatGPT (OpenAI) cho MÁY CHỦ – AI thứ 3 của app (dự phòng sau Claude → Gemini; đổi thứ tự ở /admin → Thứ tự AI).
#   1. Dán OPENAI_API_KEY (gõ ẩn, không hiện trên màn hình) – platform.openai.com → API keys
#   2. Chọn mô hình (OPENAI_MODEL) – Enter = gợi ý gpt-6.1-sol (cân bằng, đọc được ảnh)
#   3. Script kiểm tra bằng một request rất nhẹ (chỉ hỏi thông tin mô hình – không tốn token), ghi .env (giữ nguyên các dòng khác,
#      quyền 600) rồi khởi động lại app. Không in key ra màn hình (chỉ 4 ký tự cuối). AI không sửa .env (AGENTS.md).
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
PY=".venv/bin/python"
dung() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "${1:-0}"; }
[ -x "$PY" ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; dung 1; }
"$PY" -c "import openai" 2>/dev/null || { echo "==> Cài SDK openai..."; .venv/bin/pip install -q "openai>=1.80" || dung 1; }
[ -f .env ] || { cp .env.example .env; echo "Đã tạo .env từ .env.example."; }

echo "===== ChatGPT (OpenAI) cho máy chủ ====="
echo "  Key: platform.openai.com → API keys → Create new secret key (dạng sk-proj-…). Cần nạp tiền (Billing) mới gọi được."
echo "  Mô hình gợi ý (10/2026, đều đọc được ảnh): gpt-6.1-sol (cân bằng) · gpt-6-astra (mạnh nhất, đắt) · gpt-6-luna (rẻ, nhanh)"
echo
read -r -s -p "Dán OPENAI_API_KEY (sẽ không hiện khi dán; Enter trống = giữ key cũ): " KEY; echo
KEY="$(printf '%s' "$KEY" | tr -d '[:space:]"'"'")"
read -r -p "OPENAI_MODEL (Enter = gpt-6.1-sol): " MODEL
MODEL="$(printf '%s' "${MODEL:-gpt-6.1-sol}" | tr -d '[:space:]"'"'")"

if KEY_MOI="$KEY" MODEL_MOI="$MODEL" "$PY" - <<'EOF'
"""Kiểm tra key + mô hình (models.retrieve – không sinh token) → ghi OPENAI_API_KEY / OPENAI_MODEL vào .env. Không in key."""
import os, re, stat, sys
from pathlib import Path

import openai
from dotenv import dotenv_values

key, model = os.environ["KEY_MOI"], os.environ["MODEL_MOI"]
p = Path(".env")
cu = dotenv_values(p)
key = key or (cu.get("OPENAI_API_KEY") or "")
if not key:
    print("❌ Chưa có key. Không thay đổi gì.")
    sys.exit(1)
try:
    openai.OpenAI(api_key=key, timeout=20, max_retries=0).models.retrieve(model)
except openai.AuthenticationError:
    print("❌ Key không hợp lệ hoặc đã bị thu hồi. Không thay đổi gì.")
    sys.exit(1)
except openai.NotFoundError:
    print(f"❌ Key này không dùng được mô hình '{model}' (sai tên hoặc tài khoản chưa có quyền). Không thay đổi gì.")
    sys.exit(1)
except (openai.APIConnectionError, openai.APITimeoutError):
    print("❌ Không kết nối được OpenAI để kiểm tra – thử lại sau. Không thay đổi gì.")
    sys.exit(1)
except openai.OpenAIError as e:
    print(f"❌ OpenAI từ chối ({e.__class__.__name__}). Không thay đổi gì.")
    sys.exit(1)
print(f"✅ Key …{key[-4:]} hợp lệ, mô hình {model} dùng được.")

dong = p.read_text(encoding="utf-8").splitlines()
for ten, gia_tri in (("OPENAI_API_KEY", key), ("OPENAI_MODEL", model)):
    for i, d in enumerate(dong):
        if re.match(rf"\s*{ten}\s*=", d):
            dong[i] = f"{ten}={gia_tri}"
            break
    else:
        dong += [f"{ten}={gia_tri}"]
p.write_text("\n".join(dong) + "\n", encoding="utf-8")
p.chmod(stat.S_IRUSR | stat.S_IWUSR)
print("Đã ghi OPENAI_API_KEY + OPENAI_MODEL vào .env (không hiện giá trị key).")
EOF
then :; else dung 1; fi

echo
if launchctl print "gui/$(id -u)/vn.giaichuoihuuco.app" 2>/dev/null | grep -q "state = running"; then
  echo "==> Khởi động lại app để nạp key mới..."
  "$SCRIPT_DIR/tu-chay.command" khoi-dong-lai </dev/null || dung 1
else
  echo "App không chạy nền: hãy bật lại (bat-tat.command / start.command) để nạp key mới."
fi
echo "Xong. Xem thứ tự AI ở /admin → Thứ tự AI, trạng thái ChatGPT ở /admin/mindmap."
dung 0
