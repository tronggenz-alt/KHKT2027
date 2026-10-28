#!/bin/bash
# Đặt / đổi KEY CLAUDE dùng chung (CLAUDE_ACCESS_KEY) + số bài Claude tối đa mỗi người mỗi ngày (CLAUDE_DAILY_LIMIT).
# BẠN gõ key (không hiện trên màn hình); script ghi vào .env (giữ nguyên các dòng khác, quyền 600) rồi khởi động lại app.
# Đổi key → mọi tài khoản đã kích hoạt bằng key cũ tự mất quyền, phải nhập key mới. AI không sửa .env (AGENTS.md).
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
PY=".venv/bin/python"
dung() { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; exit "${1:-0}"; }
[ -x "$PY" ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; dung 1; }
[ -f .env ] || { cp .env.example .env; echo "Đã tạo .env từ .env.example."; }

echo "===== Key Claude dùng chung ====="
echo "Người dùng (không phải admin) nhập đúng key này trong Cài đặt → Key Claude thì được giải bài bằng Claude của máy chủ"
echo "(ANTHROPIC_API_KEY trong .env). Không nhập key thì chỉ dùng Gemini. Admin luôn dùng được Claude."
echo "Key nên dài, khó đoán (≥ 8 ký tự, có chữ và số); không chứa khoảng trắng, dấu nháy hay dấu #."
echo
read -r -s -p "1) Gõ key mới (sẽ không hiện khi gõ): " KEY1; echo
read -r -s -p "   Gõ lại key để xác nhận: " KEY2; echo
[ -n "$KEY1" ] || { echo "LỖI: key trống – không thay đổi gì."; dung 1; }
[ "$KEY1" = "$KEY2" ] || { echo "LỖI: hai lần gõ không khớp – không thay đổi gì."; dung 1; }
if [[ "$KEY1" =~ [[:space:]\"\'#] ]]; then echo "LỖI: key không được chứa khoảng trắng, dấu nháy hay dấu #."; dung 1; fi
[ ${#KEY1} -ge 8 ] || echo "⚠ Key ngắn hơn 8 ký tự – dễ bị dò. Vẫn tiếp tục (có khoá 1 giờ sau 5 lần nhập sai)."
read -r -p "2) Số bài Claude tối đa mỗi người mỗi ngày (Enter = 10): " GIOI_HAN
GIOI_HAN="${GIOI_HAN:-10}"
[[ "$GIOI_HAN" =~ ^[0-9]+$ ]] || { echo "LỖI: phải là số nguyên ≥ 0."; dung 1; }

CLAUDE_ACCESS_KEY="$KEY1" CLAUDE_DAILY_LIMIT="$GIOI_HAN" "$PY" - <<'EOF' || dung 1
"""Cập nhật .env: thay dòng KEY=... nếu đã có, không thì thêm cuối file. Không in giá trị key."""
import os, re, stat
from pathlib import Path

p = Path(".env")
dong = p.read_text(encoding="utf-8").splitlines()
co_tu_truoc = any(re.match(r"\s*CLAUDE_ACCESS_KEY\s*=\s*\S", d) for d in dong)

def dat(ten, v):
    for i, d in enumerate(dong):
        if re.match(rf"\s*{ten}\s*=", d):
            dong[i] = f"{ten}={v}"
            return
    dong.append(f"{ten}={v}")

if not any(re.match(r"\s*CLAUDE_ACCESS_KEY\s*=", d) for d in dong):
    dong += ["", "# ----- Key Claude dùng chung (ghi bởi cau-hinh-key-claude.command)"]
dat("CLAUDE_ACCESS_KEY", os.environ["CLAUDE_ACCESS_KEY"])
dat("CLAUDE_DAILY_LIMIT", os.environ["CLAUDE_DAILY_LIMIT"])
p.write_text("\n".join(dong) + "\n", encoding="utf-8")
p.chmod(stat.S_IRUSR | stat.S_IWUSR)  # chỉ chủ máy đọc/ghi được
print("Đã ghi vào .env: CLAUDE_ACCESS_KEY (không hiện giá trị), CLAUDE_DAILY_LIMIT =", os.environ["CLAUDE_DAILY_LIMIT"])
if co_tu_truoc:
    print("Key đã ĐỔI: sau khi khởi động lại, mọi tài khoản kích hoạt bằng key cũ phải nhập key mới.")
EOF

echo
if launchctl print "gui/$(id -u)/vn.giaichuoihuuco.app" 2>/dev/null | grep -q "state = running"; then
  echo "==> Khởi động lại app để nạp key mới..."
  "$SCRIPT_DIR/tu-chay.command" khoi-dong-lai </dev/null || dung 1
else
  echo "App không chạy nền: hãy bật lại (bat-tat.command / start.command) để nạp key mới."
fi
echo "Xong: đưa key cho người được phép dùng Claude; họ nhập trong Cài đặt → Key Claude. Xem ai đã mở Claude: /admin → Key Claude"
echo "hoặc: python3 admin_cli.py claude-key list"
dung 0
