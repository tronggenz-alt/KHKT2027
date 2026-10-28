#!/bin/bash
# Tải cơ sở dữ liệu vị trí IP (tra OFFLINE) cho nhật ký đăng nhập: quốc gia, tỉnh/thành gần đúng, nhà mạng (VNPT/Viettel/FPT…).
# - Có MaxMind Account ID + License Key (miễn phí, đăng ký tại maxmind.com/en/geolite2/signup) → tải GeoLite2-City + GeoLite2-ASN.
# - Không có (Enter) → tải DB-IP City Lite + ASN Lite (miễn phí, không cần tài khoản, giấy phép CC BY 4.0) – cùng định dạng .mmdb.
# File đặt trong data/geoip/; app tự nhận file mới trong vòng 1 phút, không cần khởi động lại. Nên chạy lại mỗi tháng để cập nhật.
# Không ghi gì vào .env, không in License Key ra màn hình.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
PY=".venv/bin/python"
dung() { [ -t 0 ] && { echo; read -r -p "Nhấn Enter để đóng cửa sổ..." _; }; exit "${1:-0}"; }
[ -x "$PY" ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; dung 1; }
"$PY" -c "import maxminddb" 2>/dev/null || "$PY" -m pip install -q "maxminddb>=2.6" || { echo "LỖI: không cài được thư viện maxminddb."; dung 1; }

DICH="data/geoip"
mkdir -p "$DICH"
TAM="$(mktemp -d)"
trap 'rm -rf "$TAM"' EXIT

kiem() { # mở thử file .mmdb và tra một IP → in loại cơ sở dữ liệu
  "$PY" -I -c 'import sys, maxminddb; r = maxminddb.open_database(sys.argv[1]); r.get("8.8.8.8"); print("   ✓", r.metadata().database_type, "·", r.metadata().node_count, "nút")' "$1"
}

tai_maxmind() { # $1 = GeoLite2-City | GeoLite2-ASN
  echo "==> Tải $1 (MaxMind)..."
  curl -fsSL --max-time 900 -u "$MM_ID:$MM_KEY" "https://download.maxmind.com/geoip/databases/$1/download?suffix=tar.gz" -o "$TAM/$1.tar.gz" || return 1
  mkdir -p "$TAM/$1" && tar -xzf "$TAM/$1.tar.gz" -C "$TAM/$1" || return 1
  local f
  f="$(find "$TAM/$1" -name "$1.mmdb" | head -1)"
  [ -n "$f" ] && kiem "$f" && mv -f "$f" "$DICH/$1.mmdb"
}

tai_dbip() { # $1 = city | asn
  local thang
  for thang in "$(date +%Y-%m)" "$(date -v-1m +%Y-%m)"; do
    echo "==> Tải DB-IP $1 lite $thang..."
    if curl -fsSL --max-time 900 "https://download.db-ip.com/free/dbip-$1-lite-$thang.mmdb.gz" -o "$TAM/$1.mmdb.gz"; then
      gunzip -f "$TAM/$1.mmdb.gz" && kiem "$TAM/$1.mmdb" && mv -f "$TAM/$1.mmdb" "$DICH/dbip-$1-lite.mmdb" && return 0
    fi
  done
  return 1
}

echo "===== Dữ liệu vị trí IP (offline) cho nhật ký đăng nhập ====="
MM_ID="" MM_KEY=""
if [ -t 0 ]; then
  echo "Có tài khoản MaxMind (GeoLite2) thì nhập Account ID + License Key; không có thì nhấn Enter để dùng DB-IP Lite."
  read -r -p "MaxMind Account ID (Enter để bỏ qua): " MM_ID
  MM_ID="$(printf '%s' "$MM_ID" | tr -d '[:space:]"')"
  if [ -n "$MM_ID" ]; then
    read -r -s -p "MaxMind License Key (không hiện khi dán): " MM_KEY; echo
    MM_KEY="$(printf '%s' "$MM_KEY" | tr -d '[:space:]"')"
  fi
fi

if [ -n "$MM_ID" ] && [ -n "$MM_KEY" ]; then
  if tai_maxmind GeoLite2-City && tai_maxmind GeoLite2-ASN; then
    rm -f "$DICH/dbip-city-lite.mmdb" "$DICH/dbip-asn-lite.mmdb"
  else
    echo "LỖI: không tải được GeoLite2 (sai Account ID / License Key, hoặc mất mạng)."; dung 1
  fi
else
  tai_dbip city && tai_dbip asn || { echo "LỖI: không tải được DB-IP Lite (mất mạng?)."; dung 1; }
fi

echo
ls -lh "$DICH"/*.mmdb | awk '{print "   " $5 "  " $9}'
echo "Xong: app tự dùng dữ liệu mới trong vòng 1 phút (không cần khởi động lại). Chạy lại mỗi tháng để cập nhật."
dung 0
