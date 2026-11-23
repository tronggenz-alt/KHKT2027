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
# KHOA_WEB_*: hiện khoá mã 4 số cho IP mới; miễn IP đăng nhập gốc của tài khoản #10 trong CSDL sao lưu.
ENV STORAGE_DIR=/var/data CHE_DO_CUC_BO=0 PORT=10000 \
    KHOA_WEB_BAT=1 KHOA_WEB_MIEN_USER_ID=10 \
    KHOA_WEB_MA_BAM=kwv1-chem:8bcbf5e5ad2043d184f3a1be23759597060690f7cff2e02017e431b6099bdd13
EXPOSE 10000
CMD ["sh", "-c", "exec uvicorn backend.main:app --host 0.0.0.0 --port ${PORT} --proxy-headers --forwarded-allow-ips='10.0.0.0/8,172.16.0.0/12,100.64.0.0/10,127.0.0.1,::1,173.245.48.0/20,103.21.244.0/22,103.22.200.0/22,103.31.4.0/22,141.101.64.0/18,108.162.192.0/18,190.93.240.0/20,188.114.96.0/20,197.234.240.0/22,198.41.128.0/17,162.158.0.0/15,104.16.0.0/13,104.24.0.0/14,172.64.0.0/13,131.0.72.0/22,2400:cb00::/32,2606:4700::/32,2803:f800::/32,2405:b500::/32,2405:8100::/32,2a06:98c0::/29,2c0f:f248::/32'"]
