#!/bin/bash
# Đưa bản code ĐÃ COMMIT lên GitHub → Render tự dựng lại web (~5–10 phút). Nháy đúp hoặc: ./dua-len-web.command
# Lần đầu: ./dua-len-web.command https://github.com/<tài-khoản>/<tên-repo>.git   (repo PRIVATE, trống)
#
# Chỉ đẩy ẢNH CHỤP code hiện tại (nhánh riêng "ban-web"), KHÔNG kèm lịch sử cũ trên máy (lịch sử cũ từng có .env và .venv).
# Trước khi đẩy: dừng nếu bản commit có .env / CSDL / khoá, hoặc có giá trị bí mật thật của .env nằm trong code.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
REMOTE=web

if [ "${1:-}" != "" ]; then
  if git remote get-url "$REMOTE" >/dev/null 2>&1; then git remote set-url "$REMOTE" "$1"; else git remote add "$REMOTE" "$1"; fi
  echo "==> Đã nối GitHub: $1"
fi
if ! git remote get-url "$REMOTE" >/dev/null 2>&1; then
  echo "Chưa nối GitHub. Lần đầu chạy:  ./dua-len-web.command https://github.com/<tài-khoản>/<tên-repo>.git"
  exit 1
fi

if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
  echo "⚠️  Có thay đổi CHƯA commit – chỉ bản đã commit được đưa lên web:"
  git status --short --untracked-files=no | head -15
  echo
fi

# 1. Không có file bí mật trong bản sắp đẩy
if git ls-tree -r --name-only HEAD | grep -qE '(^|/)\.env$|^data/|^outputs/|^\.venv/|\.db$|app_secret\.key$|\.pem$'; then
  echo "DỪNG: bản commit có file bí mật / dữ liệu (.env, data/, .db, khoá…). Bỏ khỏi git rồi chạy lại."
  exit 1
fi
# 2. Không có giá trị bí mật THẬT của .env nằm trong code (so khớp, không in giá trị)
if [ -f .env ] && [ -x .venv/bin/python ]; then
  .venv/bin/python - <<'PY' || exit 1
import subprocess, sys
from dotenv import dotenv_values
bi_mat = [v for k, v in dotenv_values(".env").items() if v and len(v) >= 12 and k.endswith(("KEY", "PASS", "SECRET", "TOKEN"))]
tep = subprocess.run(["git", "ls-tree", "-r", "--name-only", "HEAD"], capture_output=True, text=True).stdout.split("\n")
for t in filter(None, tep):
    nd = subprocess.run(["git", "show", f"HEAD:{t}"], capture_output=True).stdout.decode("utf-8", "ignore")
    if any(v in nd for v in bi_mat):
        sys.exit(f"DỪNG: file {t} chứa giá trị bí mật thật của .env – xoá khỏi code rồi commit lại.")
PY
fi

# 3. Ảnh chụp cây code của HEAD → một commit mới trên nhánh ban-web (cha = bản web trước đó, không dính lịch sử cũ)
cay=$(git rev-parse "HEAD^{tree}")
cha=()
if git rev-parse -q --verify refs/heads/ban-web >/dev/null; then
  truoc=$(git rev-parse refs/heads/ban-web)
  cha=(-p "$truoc")
fi
moi=$(git commit-tree "$cay" ${cha[@]+"${cha[@]}"} -m "Bản web $(date '+%Y-%m-%d %H:%M') (từ $(git rev-parse --short HEAD))")
git update-ref refs/heads/ban-web "$moi"

echo "==> Đang đẩy lên GitHub ($REMOTE → main)..."
git push "$REMOTE" ban-web:main
echo
echo "✅ Xong. Render sẽ tự dựng lại trong ~5–10 phút (xem tiến trình ở tab Events / Logs của dịch vụ trên dashboard.render.com)."
