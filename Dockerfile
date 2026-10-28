# Ảnh Docker để chạy app trên máy chủ (Render – xem render.yaml và HUONG_DAN_DUA_LEN_WEB.md).
# Không chứa key nào: mọi key đặt ở mục Environment của Render. Dữ liệu (CSDL, ảnh kết quả, nhật ký) nằm trên ổ đĩa STORAGE_DIR.
FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 PIP_NO_CACHE_DIR=1 PIP_DISABLE_PIP_VERSION_CHECK=1

# libxrender1 / libxext6 / libexpat1 / libfreetype6 / libfontconfig1: RDKit vẽ công thức (rdMolDraw2D)
# fonts-liberation2 (≈ Arial) + fonts-dejavu-core: chữ tiếng Việt trong ảnh xuất PNG
RUN apt-get update \
 && apt-get install -y --no-install-recommends libxrender1 libxext6 libexpat1 libfreetype6 libfontconfig1 \
    fonts-liberation2 fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt
COPY . .

# Không cài Google Chrome: chỉ chế độ Demo dùng Chrome tra Wikipedia – thiếu thì app tự bỏ qua bước đó.
# CHE_DO_CUC_BO=0: tắt hẳn chế độ "máy chạy app vào thẳng quyền chủ app".
ENV STORAGE_DIR=/var/data CHE_DO_CUC_BO=0 PORT=10000
EXPOSE 10000
CMD ["sh", "-c", "exec uvicorn backend.main:app --host 0.0.0.0 --port ${PORT} --proxy-headers --forwarded-allow-ips='*'"]
