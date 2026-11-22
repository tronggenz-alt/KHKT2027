#!/usr/bin/env bash
# Chạy TRÊN MÁY GPU THUÊ (Linux, GPU NVIDIA từ 24 GB): cài Ollama, tải Qwen2.5-32B, tạo vera-hoa-32b.
#
#   bash cai_may_gpu.sh            lần đầu: cài + tải khoảng 20 GB (5-15 phút)
#   bash cai_may_gpu.sh            các lần sau (mô hình đã nằm trong ổ lưu trữ): chỉ bật Ollama, khoảng 1 phút
#
# Mô hình lưu ở OLLAMA_MODELS (mặc định /workspace/ollama): gắn ổ lưu trữ (volume) vào /workspace để lần sau khỏi tải lại.
# Không mở cổng 11434 ra Internet: Ollama không có mật khẩu. Mac nối vào bằng đường hầm SSH (tools/chay_gpu_thue.sh).
set -euo pipefail

export OLLAMA_MODELS=${OLLAMA_MODELS:-/workspace/ollama}
export OLLAMA_KEEP_ALIVE=-1          # giữ mô hình trong GPU, không nạp lại mỗi câu hỏi
export OLLAMA_FLASH_ATTENTION=1
export OLLAMA_NUM_PARALLEL=${OLLAMA_NUM_PARALLEL:-2}   # 24 GB: 2 câu cùng lúc; 32 GB (RTX 5090): 4
export OLLAMA_CONTEXT_LENGTH=8192
export OLLAMA_HOST=127.0.0.1:11434
GOC=${GOC:-qwen2.5:32b-instruct-q4_K_M}
TEN=${TEN:-vera-hoa-32b}

mkdir -p "$OLLAMA_MODELS"
command -v nvidia-smi >/dev/null && nvidia-smi --query-gpu=name,memory.total,driver_version --format=csv,noheader
command -v ollama >/dev/null || curl -fsSL https://ollama.com/install.sh | sh

if ! curl -sf http://127.0.0.1:11434/api/tags >/dev/null; then
  nohup ollama serve > "$OLLAMA_MODELS/ollama.log" 2>&1 &
  for _ in $(seq 30); do curl -sf http://127.0.0.1:11434/api/tags >/dev/null && break; sleep 1; done
fi

ollama list | grep -q "^$GOC" || ollama pull "$GOC"

cat > /tmp/Modelfile <<EOF
FROM $GOC
PARAMETER num_ctx 8192
PARAMETER temperature 0.3
PARAMETER top_p 0.9
PARAMETER repeat_penalty 1.05
SYSTEM """Bạn là VERA-Hoa, trợ giảng Hóa học và Sinh học cho học sinh THPT Việt Nam trong phòng thí nghiệm ảo VERA Lab.
Trả lời bằng tiếng Việt, ngắn gọn, đúng chương trình GDPT 2018, dùng tên chất theo IUPAC kèm tên cũ khi cần.
Dựa vào TÀI LIỆU THAM KHẢO khi được cung cấp; nếu tài liệu không đủ thì nói rõ, không bịa số liệu.
Luôn nhắc an toàn khi nói về hóa chất độc, ăn mòn, dễ cháy. Không hướng dẫn điều chế chất nổ, chất độc hay vũ khí."""
EOF
ollama create "$TEN" -f /tmp/Modelfile

echo "Nạp sẵn $TEN vào GPU…"
time ollama run "$TEN" "Cân bằng phương trình Fe + Cl2 → FeCl3, trả lời một dòng." --verbose 2>&1 | tail -6
echo "Xong. Trên Mac chạy: tools/chay_gpu_thue.sh <IP> <CỔNG_SSH>"
