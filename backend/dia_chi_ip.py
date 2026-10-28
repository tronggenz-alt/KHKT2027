"""IP thật của người dùng + vị trí / nhà mạng (tra OFFLINE bằng file .mmdb, không gọi dịch vụ ngoài).

App chạy sau Cloudflare Tunnel (cloudflared kết nối tới app từ 127.0.0.1) hoặc sau proxy khác (Render…), nên IP kết nối trực tiếp
là IP của proxy. get_client_ip() chỉ đọc header IP khi IP kết nối trực tiếp là PROXY TIN CẬY (TRUSTED_PROXIES trong .env, mặc định
127.0.0.1, ::1 và dải IP Cloudflare), theo thứ tự CF-Connecting-IP → X-Forwarded-For → X-Real-IP; truy cập thẳng thì bỏ qua các header
đó (người dùng tự gửi header giả cũng vô ích).
- 127.0.0.1 KHÔNG kèm X-Forwarded-For = mở thẳng trên máy chạy app (cloudflared/proxy thật luôn gửi X-Forwarded-For) → không tin header,
  giống cách auth.che_do_cuc_bo nhận ra "máy này".
- uvicorn (mặc định --proxy-headers, tin 127.0.0.1) có thể đã thay client.host bằng IP trong X-Forwarded-For trước khi tới app; khi đó
  IP kết nối trực tiếp đã là IP người dùng (không phải proxy tin cậy) nên được trả luôn – kết quả vẫn đúng.

Vị trí + nhà mạng: GeoLite2-City/GeoLite2-ASN (MaxMind) hoặc DB-IP City/ASN Lite (cùng định dạng .mmdb), đặt trong data/geoip/
(tai-du-lieu-ip.command tải về) hoặc đường dẫn GEOIP_CITY_DB / GEOIP_ASN_DB. Thiếu file / thiếu thư viện → bỏ qua, không báo lỗi.
"""
from __future__ import annotations

import ipaddress
import logging
import re
import threading
import time
import unicodedata
from functools import lru_cache
from pathlib import Path

from .config import BASE_DIR, DATA_DIR, settings

log = logging.getLogger(__name__)

KHONG_XAC_DINH = "không xác định"
NOI_BO = "mạng nội bộ"
GHI_TRUOC_KHI_SUA = "ghi trước khi sửa"
THU_MUC_GEOIP = DATA_DIR / "geoip"

# https://www.cloudflare.com/ips-v4 + ips-v6 (kiểm tra 11/10/2026)
CLOUDFLARE = (
    "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18", "108.162.192.0/18",
    "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
    "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
    "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32",
)

DiaChi = ipaddress.IPv4Address | ipaddress.IPv6Address


# ---------------------------------------------------------------- IP thật
def _ip(s) -> DiaChi | None:
    """Chuỗi → địa chỉ IP (bỏ cổng, ngoặc vuông, zone id; IPv4 viết dạng IPv6 → IPv4). Không hợp lệ → None."""
    if s is None:
        return None
    s = str(s).strip().strip('"').strip()
    if s.startswith("["):  # [2001:db8::1]:443
        s = s[1:].split("]", 1)[0]
    elif s.count(":") == 1:  # 1.2.3.4:5678
        s = s.split(":", 1)[0]
    s = s.split("%", 1)[0]  # fe80::1%en0
    if not s or len(s) > 45:
        return None
    try:
        ip = ipaddress.ip_address(s)
    except ValueError:
        return None
    if ip.version == 6 and ip.ipv4_mapped:
        ip = ip.ipv4_mapped
    return ip


def chuan_ip(s) -> str | None:
    """IP hợp lệ dạng chuẩn (IPv6 viết gọn) hoặc None."""
    ip = _ip(s)
    return str(ip) if ip is not None else None


@lru_cache(maxsize=8)
def _mang_tin_cay(cau_hinh: str) -> tuple:
    mang = []
    for phan in re.split(r"[,;\s]+", cau_hinh or ""):
        if not phan:
            continue
        if phan.lower() == "cloudflare":
            mang += [ipaddress.ip_network(m) for m in CLOUDFLARE]
            continue
        try:
            mang.append(ipaddress.ip_network(phan, strict=False))
        except ValueError:
            log.warning("TRUSTED_PROXIES: bỏ qua %r (không phải IP hay dải IP)", phan)
    return tuple(mang)


def la_proxy_tin_cay(ip) -> bool:
    ip = ip if isinstance(ip, (ipaddress.IPv4Address, ipaddress.IPv6Address)) else _ip(ip)
    return ip is not None and any(ip in m for m in _mang_tin_cay(settings.trusted_proxies))


def _gia_tri(headers, ten: str) -> list[str]:
    """Mọi dòng của một header (Starlette Headers có getlist; dict thường trong test thì lấy 1 giá trị)."""
    if hasattr(headers, "getlist"):
        return [v for v in headers.getlist(ten) if v.strip()]
    v = headers.get(ten)
    return [v] if v and v.strip() else []


def _tu_x_forwarded_for(dong: list[str]) -> str:
    """X-Forwarded-For "client, proxy1, proxy2": mỗi proxy nối thêm vào CUỐI, nên đi từ phải sang trái, bỏ qua các proxy tin cậy,
    IP đầu tiên không phải proxy chính là người dùng (bình thường đó cũng là IP đầu danh sách; người dùng tự chèn IP giả vào đầu
    danh sách thì không lừa được). Gặp mục không phải IP → không xác định."""
    cac = [m.strip() for d in dong for m in d.split(",") if m.strip()]
    for muc in reversed(cac):
        ip = _ip(muc)
        if ip is None:
            return KHONG_XAC_DINH
        if not la_proxy_tin_cay(ip):
            return str(ip)
    return chuan_ip(cac[0]) or KHONG_XAC_DINH  # toàn proxy tin cậy (vd chính máy chủ gọi qua tunnel)


def get_client_ip(request) -> str:
    """IP thật của người dùng (xem đầu file). Không có request → ""; IP không hợp lệ → "không xác định"."""
    if request is None:
        return ""
    client = getattr(request, "client", None)
    truc_tiep = _ip(client.host if client else None)
    if truc_tiep is None:
        return KHONG_XAC_DINH
    h = request.headers
    xff = _gia_tri(h, "x-forwarded-for")
    if not la_proxy_tin_cay(truc_tiep) or (truc_tiep.is_loopback and not xff):
        return str(truc_tiep)  # truy cập thẳng: bỏ qua mọi header IP
    if cf := _gia_tri(h, "cf-connecting-ip"):
        return chuan_ip(cf[0]) or KHONG_XAC_DINH
    if xff:
        return _tu_x_forwarded_for(xff)
    if thuc := _gia_tri(h, "x-real-ip"):
        return chuan_ip(thuc[0]) or KHONG_XAC_DINH
    return str(truc_tiep)


def la_noi_bo(ip) -> bool:
    """192.168.x.x, 10.x.x.x, 172.16–31.x.x, 127.x.x.x, ::1, fc00::/7, fe80::/10…"""
    ip = ip if isinstance(ip, (ipaddress.IPv4Address, ipaddress.IPv6Address)) else _ip(ip)
    return ip is not None and (ip.is_private or ip.is_loopback or ip.is_link_local)


# ---------------------------------------------------------------- vị trí + nhà mạng (offline)
_TEN_FILE = {"city": ("GeoLite2-City.mmdb", "dbip-city-lite.mmdb"), "asn": ("GeoLite2-ASN.mmdb", "dbip-asn-lite.mmdb")}
_khoa_doc = threading.Lock()
_bo_doc: dict[str, tuple] = {}  # loai → (đường dẫn, mtime, reader | None)
_lan_kiem = 0.0


def duong_dan_csdl(loai: str) -> Path | None:
    """File .mmdb đang dùng: GEOIP_CITY_DB / GEOIP_ASN_DB nếu có, không thì tìm trong data/geoip/ rồi data/ (GeoLite2 trước, DB-IP sau)."""
    cau_hinh = settings.geoip_city_db if loai == "city" else settings.geoip_asn_db
    if cau_hinh:
        p = Path(cau_hinh).expanduser()
        p = p if p.is_absolute() else BASE_DIR / p
        return p if p.is_file() else None
    for ten in _TEN_FILE[loai]:
        for thu_muc in (THU_MUC_GEOIP, DATA_DIR):
            if (thu_muc / ten).is_file():
                return thu_muc / ten
    return None


def _doc(loai: str):
    """Reader maxminddb (mở một lần; file đổi – vd tải bản mới – thì mở lại). Thiếu file/thư viện → None."""
    global _lan_kiem
    with _khoa_doc:
        if time.time() - _lan_kiem > 60 or loai not in _bo_doc:
            _lan_kiem = time.time()
            for ten in _TEN_FILE:
                p = duong_dan_csdl(ten)
                cu = _bo_doc.get(ten)
                mtime = p.stat().st_mtime if p else None
                if cu and cu[0] == p and cu[1] == mtime:
                    continue
                reader = None
                if p is not None:
                    try:
                        import maxminddb
                        reader = maxminddb.open_database(str(p))
                    except Exception as e:  # noqa: BLE001 – thiếu thư viện / file hỏng: bỏ qua phần vị trí
                        log.warning("Không mở được %s (%s) – bỏ qua vị trí IP", p.name, e)
                _bo_doc[ten] = (p, mtime, reader)
                tra_ip.cache_clear()
        return _bo_doc[loai][2]


def _bo_dau(s: str) -> str:
    s = unicodedata.normalize("NFD", s.replace("đ", "d").replace("Đ", "D"))
    return "".join(c for c in s if not unicodedata.combining(c)).lower()


def _khoa_ten(s: str) -> str:
    """'Thành phố Hồ Chí Minh' / 'Ho Chi Minh City' / 'TP. HCM' → 'hochiminh' để so khớp tên tỉnh tiếng Anh với tiếng Việt."""
    tu = [t for t in re.split(r"[^a-z0-9]+", _bo_dau(s)) if t]
    tu = [t for t in tu if t not in {"province", "city", "municipality", "tinh", "tp"}]
    chuoi = " ".join(tu).replace("thanh pho ", "")
    return chuoi.replace(" ", "")


_TINH = ("An Giang", "Bà Rịa - Vũng Tàu", "Bạc Liêu", "Bắc Giang", "Bắc Kạn", "Bắc Ninh", "Bến Tre", "Bình Dương", "Bình Định",
         "Bình Phước", "Bình Thuận", "Cà Mau", "Cao Bằng", "Cần Thơ", "Đà Nẵng", "Đắk Lắk", "Đắk Nông", "Điện Biên", "Đồng Nai",
         "Đồng Tháp", "Gia Lai", "Hà Giang", "Hà Nam", "Hà Nội", "Hà Tĩnh", "Hải Dương", "Hải Phòng", "Hậu Giang", "Hòa Bình",
         "Hưng Yên", "Khánh Hòa", "Kiên Giang", "Kon Tum", "Lai Châu", "Lâm Đồng", "Lạng Sơn", "Lào Cai", "Long An", "Nam Định",
         "Nghệ An", "Ninh Bình", "Ninh Thuận", "Phú Thọ", "Phú Yên", "Quảng Bình", "Quảng Nam", "Quảng Ngãi", "Quảng Ninh",
         "Quảng Trị", "Sóc Trăng", "Sơn La", "Tây Ninh", "Thái Bình", "Thái Nguyên", "Thanh Hóa", "Thừa Thiên Huế", "Huế",
         "Tiền Giang", "TP.HCM", "Trà Vinh", "Tuyên Quang", "Vĩnh Long", "Vĩnh Phúc", "Yên Bái")
_TEN_TINH = {_khoa_ten(t): t for t in _TINH} | {"hochiminh": "TP.HCM", "saigon": "TP.HCM", "haiphong": "Hải Phòng",
                                                 "hanoi": "Hà Nội", "danang": "Đà Nẵng", "cantho": "Cần Thơ"}
_QUOC_GIA = {"VN": "Việt Nam", "US": "Hoa Kỳ", "SG": "Singapore", "JP": "Nhật Bản", "KR": "Hàn Quốc", "CN": "Trung Quốc",
             "TW": "Đài Loan", "HK": "Hồng Kông", "TH": "Thái Lan", "MY": "Malaysia", "ID": "Indonesia", "PH": "Philippines",
             "KH": "Campuchia", "LA": "Lào", "AU": "Úc", "GB": "Anh", "FR": "Pháp", "DE": "Đức", "CA": "Canada", "RU": "Nga",
             "IN": "Ấn Độ", "NL": "Hà Lan", "IT": "Ý", "ES": "Tây Ban Nha", "CH": "Thuỵ Sĩ", "SE": "Thuỵ Điển", "FI": "Phần Lan",
             "PL": "Ba Lan", "BR": "Brazil", "NZ": "New Zealand", "IE": "Ireland", "AE": "UAE"}
_NHA_MANG = ((r"viettel", "Viettel"), (r"\bvnpt\b|vietnam posts and telecom|vinaphone", "VNPT"), (r"\bfpt\b", "FPT"),
             (r"mobifone", "MobiFone"), (r"\bcmc\b", "CMC"), (r"vietnamobile|hanoi telecom", "Vietnamobile"), (r"\bsctv\b", "SCTV"),
             (r"netnam", "NetNam"), (r"saigon tourist cable", "SCTV"), (r"saigon postel", "SPT"), (r"cloudflare", "Cloudflare"), (r"google", "Google"),
             (r"amazon", "Amazon"), (r"microsoft", "Microsoft"))
_DUOI_CONG_TY = re.compile(r"[,.\s]*(joint stock company|corporation|company|corp|limited|ltd|inc|llc|jsc|co)\.?$", re.I)


def ten_quoc_gia(qg: str) -> str:
    return _QUOC_GIA.get((qg or "").upper(), qg or "")


def _ten(muc: dict | None) -> str:
    names = (muc or {}).get("names") or {}
    return names.get("vi") or names.get("en") or ""


def ten_nha_mang(to_chuc: str) -> str:
    """'VNPT Corp' → 'VNPT', 'Viettel Group' → 'Viettel', 'FPT Telecom Company' → 'FPT'; còn lại: tên tổ chức bỏ đuôi công ty."""
    if not to_chuc:
        return ""
    thap = to_chuc.lower()
    for mau, ten in _NHA_MANG:
        if re.search(mau, thap):
            return ten
    for _ in range(2):
        to_chuc = _DUOI_CONG_TY.sub("", to_chuc).strip()
    return to_chuc[:40]


@lru_cache(maxsize=4096)
def tra_ip(ip: str) -> dict:
    """{"ip", "hop_le", "noi_bo", "qg" (mã ISO), "quoc_gia", "vi_tri" (tỉnh/thành gần đúng), "nha_mang"} – tra offline, có bộ nhớ đệm."""
    kq = {"ip": ip, "hop_le": False, "noi_bo": False, "qg": "", "quoc_gia": "", "vi_tri": "", "nha_mang": ""}
    dc = _ip(ip)
    if dc is None:
        return kq
    kq.update(ip=str(dc), hop_le=True)
    if la_noi_bo(dc):
        kq["noi_bo"] = True
        return kq
    try:
        if (doc := _doc("city")) is not None and (r := doc.get(str(dc))):
            qg = ((r.get("country") or {}).get("iso_code") or "").upper()
            quoc_gia = _QUOC_GIA.get(qg) or _ten(r.get("country"))
            tinh = _ten((r.get("subdivisions") or [{}])[0])
            tp = _ten(r.get("city"))
            if qg == "VN":
                vi_tri = _TEN_TINH.get(_khoa_ten(tinh)) or _TEN_TINH.get(_khoa_ten(tp)) or tinh or tp
            else:
                noi = tp or tinh
                vi_tri = f"{noi}, {quoc_gia}" if noi and quoc_gia and _khoa_ten(noi) != _khoa_ten(quoc_gia) else (noi or quoc_gia)
            kq.update(qg=qg, quoc_gia=quoc_gia, vi_tri=vi_tri or quoc_gia)
        if (doc := _doc("asn")) is not None and (r := doc.get(str(dc))):
            kq["nha_mang"] = ten_nha_mang(r.get("autonomous_system_organization") or "")
    except Exception:  # noqa: BLE001 – file hỏng giữa chừng: bỏ qua phần vị trí
        log.exception("Không tra được vị trí IP")
    return kq


def mo_ta(ip: str, *, cu: bool = False, vi_tri: str = "", nha_mang: str = "", noi_bo: bool | None = None) -> str:
    """'113.161.1.2 · Hà Nội · VNPT' / '192.168.1.5 · mạng nội bộ' / '127.0.0.1 · ghi trước khi sửa'.
    vi_tri/nha_mang: giá trị đã lưu lúc ghi nhật ký; trống thì tra lại."""
    if not ip:
        return ""
    if ip == KHONG_XAC_DINH or _ip(ip) is None:
        return KHONG_XAC_DINH if ip == KHONG_XAC_DINH else ip
    if cu and la_noi_bo(ip):
        return f"{ip} · {GHI_TRUOC_KHI_SUA}"
    if not (vi_tri or nha_mang):
        t = tra_ip(ip)
        vi_tri, nha_mang, noi_bo = t["vi_tri"], t["nha_mang"], t["noi_bo"]
    if noi_bo if noi_bo is not None else la_noi_bo(ip):
        return f"{ip} · {NOI_BO}"
    return " · ".join(x for x in (ip, vi_tri, nha_mang) if x)


def thong_tin(ip: str, *, cu: bool = False, qg: str = "", vi_tri: str = "", nha_mang: str = "") -> dict:
    """Cho API: {"mo_ta", "vi_tri", "nha_mang", "qg", "noi_bo", "cu"} (cu = 127.0.0.1 ghi trước khi sửa cách lấy IP)."""
    if ip and not (vi_tri or nha_mang or qg) and _ip(ip) is not None:
        t = tra_ip(ip)
        qg, vi_tri, nha_mang = t["qg"], t["vi_tri"], t["nha_mang"]
    noi_bo = la_noi_bo(ip) if ip else False
    return {"mo_ta": mo_ta(ip, cu=cu, vi_tri=vi_tri, nha_mang=nha_mang, noi_bo=noi_bo), "vi_tri": vi_tri, "nha_mang": nha_mang,
            "qg": qg, "noi_bo": noi_bo, "cu": bool(cu and noi_bo)}


def nguon_du_lieu() -> str:
    """Ghi công nguồn dữ liệu vị trí (bắt buộc theo giấy phép): '' nếu chưa có file."""
    ten = set()
    for loai in _TEN_FILE:
        doc = _doc(loai)
        if doc is not None:
            loai_csdl = (getattr(doc.metadata(), "database_type", "") or "").lower()
            ten.add("DB-IP" if "dbip" in loai_csdl or "db-ip" in loai_csdl else "GeoLite2 (MaxMind)" if "geolite" in loai_csdl
                    else loai_csdl)
    if not ten:
        return ""
    phan = []
    if "DB-IP" in ten:
        phan.append("IP Geolocation by DB-IP (db-ip.com, CC BY 4.0)")
    if "GeoLite2 (MaxMind)" in ten:
        phan.append("GeoLite2 data created by MaxMind (maxmind.com)")
    phan += sorted(t for t in ten if t not in {"DB-IP", "GeoLite2 (MaxMind)"})
    return "Vị trí IP gần đúng · " + " · ".join(phan)
