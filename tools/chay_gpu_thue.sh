#!/usr/bin/env bash
# Chạy TRÊN MAC: nối với VERA-Hoa-32B trên máy GPU thuê qua đường hầm SSH, rồi bật máy chủ AI VERA Lab.
#
#   tools/chay_gpu_thue.sh <IP_MAY_THUE> <CONG_SSH> [ten_mo_hinh]      ví dụ: tools/chay_gpu_thue.sh 194.68.245.10 22042
#
# Thứ tự trả lời: 32B trên máy thuê → mạng lưới khóa API → Claude → thẻ soạn sẵn (AI_THU_TU).
# Đường hầm dùng cổng 11435 trên Mac vì 11434 dành cho Ollama của Mac (bge-m3 tìm tài liệu RAG).
# iPad trong cùng mạng Wi-Fi gọi tới http://<IP của Mac>:8000.
set -euo pipefail

IP=${1:?"Cách dùng: tools/chay_gpu_thue.sh <IP_MAY_THUE> <CONG_SSH> [ten_mo_hinh]"}
CONG=${2:?"Thiếu cổng SSH của máy thuê"}
MO_HINH=${3:-vera-hoa-32b}
NGUOI=${NGUOI:-root}
CONG_HAM=${CONG_HAM:-11435}
GOC="$(cd "$(dirname "$0")/.." && pwd)"
PY=${PY:-$GOC/.venv/bin/python}
[ -x "$PY" ] || PY=python3

if curl -sf "http://127.0.0.1:$CONG_HAM/api/tags" >/dev/null; then
  echo "Đường hầm cổng $CONG_HAM đang mở."
else
  echo "Mở đường hầm SSH tới $NGUOI@$IP:$CONG …"
  ssh -f -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -o ServerAliveCountMax=3 \
      -L "$CONG_HAM:127.0.0.1:11434" -p "$CONG" "$NGUOI@$IP"
  for _ in $(seq 20); do curl -sf "http://127.0.0.1:$CONG_HAM/api/tags" >/dev/null && break; sleep 1; done
fi

if ! curl -sf "http://127.0.0.1:$CONG_HAM/api/tags" | grep -q "\"$MO_HINH"; then
  echo "Máy thuê chưa có mô hình $MO_HINH. Trên máy thuê chạy: bash cai_may_gpu.sh" >&2
  exit 1
fi

echo "Nạp sẵn $MO_HINH (lần đầu có thể mất 10-30 giây)…"
curl -sf -m 180 "http://127.0.0.1:$CONG_HAM/api/generate" \
  -d "{\"model\":\"$MO_HINH\",\"prompt\":\"Chào\",\"stream\":false,\"keep_alive\":-1,\"options\":{\"num_predict\":4}}" >/dev/null \
  && echo "32B đã sẵn sàng." || echo "Cảnh báo: chưa nạp được mô hình, máy chủ vẫn chạy với API dự phòng."

if curl -sf http://127.0.0.1:11434/api/tags | grep -q '"bge-m3'; then
  echo "RAG: bge-m3 trên Mac sẵn sàng."
else
  echo "Cảnh báo: Ollama trên Mac chưa có bge-m3, RAG chỉ tìm bằng từ khóa (brew services start ollama && ollama pull bge-m3)."
fi

cd "$GOC"
export AI_THU_TU=${AI_THU_TU:-openai_compat,mang_luoi_khoa,claude}
export LLM_BASE_URL="http://127.0.0.1:$CONG_HAM/v1" LLM_MODEL="$MO_HINH" LLM_TIMEOUT=${LLM_TIMEOUT:-90}
echo "Máy chủ VERA Lab: http://$(ipconfig getifaddr en0 2>/dev/null || echo localhost):8000"
exec "$PY" -m uvicorn server.ai.main:app --host 0.0.0.0 --port 8000
