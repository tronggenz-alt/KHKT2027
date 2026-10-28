"""Chặn EMAIL ẢO / EMAIL RÁC khi đăng ký – mọi kiểm tra chạy ở SERVER (giao diện chỉ hiện thông báo).

Thứ tự kiểm tra một email (kiem_tra):
  1. Định dạng (ten@ten-mien.tld, không có ".." hay dấu chấm ở đầu/cuối phần tên).
  2. Danh sách CHO PHÉP của admin (whitelist) → bỏ qua mọi kiểm tra tên miền bên dưới.
  3. Danh sách CHẶN của admin (blacklist) – hỗ trợ dấu * (vd "tempmail.*", "*.xyz"), khớp cả tên miền con.
  4. Danh sách email tạm thời mở "disposable-email-domains" (GitHub, CC0): bản đi kèm mã nguồn (backend/email_tam_thoi.txt)
     + bản tải mới hằng tuần (data/email_tam_thoi.txt) – khớp cả tên miền con.
  5. Mẫu tên email tạm phổ biến ở MỌI đuôi (tempmail.*, 10minutemail.*, yopmail.*…).
  6. Bản ghi MX (dnspython): tên miền không tồn tại / không có máy chủ nhận thư → từ chối. Lỗi DNS (mạng chập chờn) → CHO QUA
     để không chặn nhầm cả loạt người thật; kết quả được lưu tạm (có MX: 1 ngày, không có: 1 giờ).
Tên email trông ngẫu nhiên (xk29fjq8zz@…) KHÔNG bị chặn – chỉ đánh dấu "nghi ngờ" để admin duyệt (diem_ngau_nhien).

Ghi lại mọi lần bị chặn (email, IP, lý do, thời gian) vào bảng email_blocks (giữ 90 ngày) → /admin "Email rác" + `admin_cli.py email`.
Việc nền (chay_nen): mỗi giờ xoá tài khoản đăng ký mới mà quá 24 giờ chưa xác minh email; danh sách cũ hơn 7 ngày thì tải lại.
"""
from __future__ import annotations

import fnmatch
import logging
import re
import threading
import time
from dataclasses import dataclass
from pathlib import Path

import requests

from . import db
from .config import DATA_DIR

log = logging.getLogger(__name__)

URL_DANH_SACH = ("https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/main/"
                 "disposable_email_blocklist.conf")
TEP_GOC = Path(__file__).with_name("email_tam_thoi.txt")  # đi kèm mã nguồn (có commit)
TEP_CAP_NHAT = DATA_DIR / "email_tam_thoi.txt"            # bản tải về hằng tuần (thư mục data/, không commit)
CAP_NHAT_SAU_NGAY = 7
TOI_THIEU_TEN_MIEN = 1000      # bản tải về ít hơn số này → coi là hỏng, giữ bản cũ
XOA_CHUA_XAC_MINH_GIAY = 24 * 3600
GIU_NHAT_KY_CHAN_NGAY = 90
THU_DANG_KY_MOI_GIO = 3        # tối đa 3 lần thử đăng ký / IP / giờ (tính cả lần bị chặn vì email rác, bot, captcha)
DNS_GIAY = 4.0

# tên email tạm phổ biến – chặn ở MỌI đuôi tên miền (vd tempmail.net, temp-mail.io, yopmail.fr)
TU_KHOA_TAM = ("tempmail", "temp-mail", "tmpmail", "tmp-mail", "tempinbox", "10minutemail", "10-minute-mail", "minutemail",
               "guerrillamail", "mailinator", "yopmail", "trashmail", "throwawaymail", "fakemail", "fakeinbox", "burnermail",
               "disposablemail", "discardmail", "dispostable", "getnada", "maildrop", "sharklasers", "mailnesia", "mintemail",
               "spamgourmet", "emailondeck", "mohmal", "mailcatch", "spambox", "jetable")

# lý do bị chặn → câu thông báo cho người dùng (tiếng Việt, thân thiện)
THONG_BAO = {
    "dinh_dang": "Email không hợp lệ – hãy kiểm tra lại (ví dụ: ten.cua.ban@gmail.com).",
    "email_tam": "Vui lòng dùng email thật, không hỗ trợ email tạm thời / dùng một lần (Gmail, Outlook, email trường… đều được).",
    "ten_mien_bi_chan": "Tên miền email này không được chấp nhận – hãy dùng email khác (Gmail, Outlook, email trường…).",
    "khong_ton_tai": "Tên miền email “{mien}” không tồn tại – hãy kiểm tra lại chính tả (ví dụ gmail.com, không phải gmial.com).",
    "khong_mx": "Tên miền “{mien}” không nhận được thư (không có máy chủ email) – vui lòng dùng email thật.",
    "honeypot": "Không tạo được tài khoản – hãy tải lại trang rồi thử lại.",
    "captcha": "Chưa qua bước xác minh “Tôi không phải robot”.",
    "qua_nhieu_lan_thu": "Bạn đã thử đăng ký quá nhiều lần – hãy thử lại sau khoảng 1 giờ.",
    "qua_gioi_han_ngay": "Mạng (IP) này đã tạo đủ số tài khoản cho phép trong ngày.",
    "ten_mien_google": "Tên miền email của tài khoản Google này không được chấp nhận.",
}
TEN_LY_DO = {"dinh_dang": "Sai định dạng", "email_tam": "Email tạm thời", "ten_mien_bi_chan": "Tên miền trong danh sách chặn",
             "khong_ton_tai": "Tên miền không tồn tại", "khong_mx": "Không có MX", "honeypot": "Bot (ô bẫy)", "captcha": "Captcha sai",
             "qua_nhieu_lan_thu": "Quá 3 lần/giờ", "qua_gioi_han_ngay": "Quá 3 tài khoản/ngày", "ten_mien_google": "Google: tên miền bị chặn"}
# các lần bị chặn được tính vào giới hạn "3 lần thử đăng ký / IP / giờ" (không tính chính lần bị chặn vì quá giới hạn)
DEM_LAN_THU = ("dinh_dang", "email_tam", "ten_mien_bi_chan", "khong_ton_tai", "khong_mx", "honeypot", "captcha")

_MAU_EMAIL = re.compile(r"[a-z0-9!#$%&'*+/=?^_`{|}~.-]{1,64}@(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.){1,8}[a-z]{2,24}")


@dataclass
class KetQua:
    ma: str = ""        # lý do bị chặn ("" = cho qua)
    loi: str = ""       # thông báo cho người dùng
    nghi_ngo: str = ""  # lý do nghi ngờ (KHÔNG chặn – đánh dấu để admin duyệt)

    @property
    def chan(self) -> bool:
        return bool(self.ma)


def ten_mien(email: str) -> str:
    return (email or "").rsplit("@", 1)[-1].strip().lower().rstrip(".")


def _cac_cap(mien: str) -> list[str]:
    """a.b.example.com → [a.b.example.com, b.example.com, example.com] (để khớp cả tên miền con)."""
    phan = mien.split(".")
    return [".".join(phan[i:]) for i in range(len(phan) - 1)]


def dinh_dang_hop_le(email: str) -> bool:
    email = (email or "").strip().lower()
    if not _MAU_EMAIL.fullmatch(email):
        return False
    ten = email.split("@")[0]
    return ".." not in email and not ten.startswith(".") and not ten.endswith(".")


# ---------------------------------------------------------------- danh sách email tạm thời (GitHub) + cập nhật định kỳ
_ds_tam: frozenset[str] = frozenset()
_ds_moc: tuple = ()
_khoa_ds = threading.Lock()


def _doc_tep(tep: Path) -> set[str]:
    try:
        dong = tep.read_text(encoding="utf-8").splitlines()
    except OSError:
        return set()
    return {d.strip().lower() for d in dong if d.strip() and not d.lstrip().startswith("#")}


def danh_sach_tam() -> frozenset[str]:
    """Bản đi kèm + bản tải mới (đọc lại khi file đổi)."""
    global _ds_tam, _ds_moc
    moc = tuple(t.stat().st_mtime if t.exists() else 0 for t in (TEP_GOC, TEP_CAP_NHAT))
    with _khoa_ds:
        if moc != _ds_moc:
            ds = _doc_tep(TEP_GOC) | _doc_tep(TEP_CAP_NHAT)
            if not ds:
                log.warning("Không đọc được danh sách email tạm thời (%s) – chỉ còn chặn theo mẫu + danh sách của admin", TEP_GOC)
            _ds_tam, _ds_moc = frozenset(ds), moc
        return _ds_tam


def thong_tin_danh_sach() -> dict:
    tep = TEP_CAP_NHAT if TEP_CAP_NHAT.exists() else None
    return {"so_ten_mien": len(danh_sach_tam()), "nguon": URL_DANH_SACH.split("/main/")[0].replace("raw.githubusercontent.com", "github.com"),
            "cap_nhat_luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(tep.stat().st_mtime)) if tep else "",
            "cap_nhat_sau_ngay": CAP_NHAT_SAU_NGAY}


class LoiCapNhat(Exception):
    pass


def cap_nhat_danh_sach() -> int:
    """Tải bản mới nhất từ GitHub → data/email_tam_thoi.txt (ghi qua file tạm rồi đổi tên). Trả số tên miền."""
    try:
        r = requests.get(URL_DANH_SACH, timeout=30, headers={"User-Agent": "CHEMISTRY-app (disposable email check)"})
    except requests.RequestException as e:
        raise LoiCapNhat(f"Không tải được danh sách ({type(e).__name__}).") from e
    if r.status_code != 200:
        raise LoiCapNhat(f"GitHub trả lỗi {r.status_code}.")
    ds = sorted({d.strip().lower() for d in r.text.splitlines() if d.strip() and not d.startswith("#") and "." in d})
    if len(ds) < TOI_THIEU_TEN_MIEN:
        raise LoiCapNhat(f"Bản tải về chỉ có {len(ds)} tên miền – có vẻ hỏng, giữ bản cũ.")
    TEP_CAP_NHAT.parent.mkdir(parents=True, exist_ok=True)
    tam = TEP_CAP_NHAT.with_suffix(".tmp")
    tam.write_text(f"# {URL_DANH_SACH}\n# tải lúc {time.strftime('%Y-%m-%d %H:%M:%S')}\n" + "\n".join(ds) + "\n", encoding="utf-8")
    tam.replace(TEP_CAP_NHAT)
    log.info("Đã cập nhật danh sách email tạm thời: %d tên miền", len(ds))
    return len(ds)


def can_cap_nhat() -> bool:
    return not TEP_CAP_NHAT.exists() or time.time() - TEP_CAP_NHAT.stat().st_mtime > CAP_NHAT_SAU_NGAY * 86400


# ---------------------------------------------------------------- danh sách CHẶN / CHO PHÉP của admin (bảng email_domain_rules)
def _chuan_quy_tac(mau: str) -> str:
    mau = (mau or "").strip().lower().lstrip("@").rstrip(".")
    if not re.fullmatch(r"[a-z0-9*][a-z0-9.*-]{0,252}", mau) or ("." not in mau and "*" not in mau) or ".." in mau:
        raise ValueError("Tên miền không hợp lệ – ví dụ: example.com, tempmail.* hoặc *.xyz")
    return mau


def them_quy_tac(mau: str, loai: str, ghi_chu: str = "", admin_id: int | None = None) -> str:
    """loai: "chan" (blacklist) | "cho" (whitelist). Đã có → đổi loại. Trả mẫu đã chuẩn hoá."""
    if loai not in ("chan", "cho"):
        raise ValueError(loai)
    mau = _chuan_quy_tac(mau)
    with db.ket_noi() as con:
        con.execute("INSERT INTO email_domain_rules (domain, kind, note, created, admin_id) VALUES (?, ?, ?, ?, ?) "
                    "ON CONFLICT(domain) DO UPDATE SET kind = excluded.kind, note = excluded.note, created = excluded.created, "
                    "admin_id = excluded.admin_id", (mau, loai, (ghi_chu or "").strip()[:200], time.time(), admin_id))
    return mau


def xoa_quy_tac(mau: str) -> bool:
    with db.ket_noi() as con:
        return con.execute("DELETE FROM email_domain_rules WHERE domain = ?", ((mau or "").strip().lower(),)).rowcount == 1


def danh_sach_quy_tac() -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT r.*, u.email AS admin_email FROM email_domain_rules r LEFT JOIN users u ON u.id = r.admin_id "
                           "ORDER BY r.kind, r.domain").fetchall()
    return [{"ten_mien": r["domain"], "loai": r["kind"], "ghi_chu": r["note"], "admin": r["admin_email"] or "(dòng lệnh)",
             "luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(r["created"]))} for r in rows]


def _khop_quy_tac(mien: str, loai: str) -> str:
    """Mẫu (của admin) khớp tên miền hoặc một tên miền cha của nó; "" nếu không có."""
    with db.ket_noi() as con:
        mau = [r[0] for r in con.execute("SELECT domain FROM email_domain_rules WHERE kind = ?", (loai,))]
    for m in mau:
        if any(fnmatch.fnmatchcase(c, m) for c in _cac_cap(mien)):
            return m
    return ""


# ---------------------------------------------------------------- email tạm thời
def la_email_tam(email: str) -> bool:
    """Tên miền (hoặc tên miền cha) có trong danh sách email tạm thời / khớp mẫu tên email tạm (không tính whitelist/blacklist)."""
    mien = ten_mien(email)
    if any(c in danh_sach_tam() for c in _cac_cap(mien)):
        return True
    nhan = mien.split(".")[:-1]  # bỏ đuôi (.com, .net…) – "tempmail.*" khớp mọi đuôi
    return any(n.startswith(k) for n in nhan for k in TU_KHOA_TAM)


# ---------------------------------------------------------------- bản ghi MX
_cache_mx: dict[str, tuple[float, str]] = {}
_khoa_mx = threading.Lock()


def _tra_mx_dns(mien: str) -> str:
    """Hỏi DNS → "co" | "khong_mx" | "khong_ton_tai" | "loi" (mạng / máy chủ DNS lỗi – không kết luận được)."""
    import dns.exception
    import dns.resolver
    try:
        tl = dns.resolver.resolve(mien, "MX", lifetime=DNS_GIAY)
    except dns.resolver.NXDOMAIN:
        return "khong_ton_tai"
    except dns.resolver.NoAnswer:
        return "khong_mx"
    except dns.exception.DNSException as e:  # Timeout, NoNameservers, …
        log.warning("Không tra được MX của %s: %s", mien, type(e).__name__)
        return "loi"
    may_chu = [str(r.exchange).rstrip(".") for r in tl]
    return "co" if any(may_chu) else "khong_mx"  # "Null MX" (RFC 7505: "0 .") = tên miền tuyên bố không nhận thư


def kiem_mx(mien: str) -> str:
    bay_gio = time.time()
    with _khoa_mx:
        luc, kq = _cache_mx.get(mien, (0.0, ""))
        if kq and bay_gio - luc < (86400 if kq == "co" else 3600):
            return kq
    kq = _tra_mx_dns(mien)
    if kq != "loi":
        with _khoa_mx:
            if len(_cache_mx) > 5000:
                _cache_mx.clear()
            _cache_mx[mien] = (bay_gio, kq)
    return kq


# ---------------------------------------------------------------- tên email trông ngẫu nhiên → "nghi ngờ"
_NGUYEN_AM = set("aeiouy")


def diem_ngau_nhien(email: str) -> tuple[int, list[str]]:
    """Chấm điểm phần tên (trước @, bỏ "+nhãn" và dấu chấm). ≥ 3 điểm = nghi ngờ. Số điện thoại / tên thường (nguyenvana, toan.180982) = 0–1."""
    ten = (email or "").split("@")[0].lower().split("+", 1)[0]
    ten = re.sub(r"[._-]", "", ten)
    if len(ten) < 6:
        return 0, []
    chu = [c for c in ten if c.isalpha()]
    so = [c for c in ten if c.isdigit()]
    if not chu:
        return 0, []  # toàn số (thường là số điện thoại) – không coi là ngẫu nhiên
    diem, ly_do = 0, []
    ti_le_nguyen_am = sum(c in _NGUYEN_AM for c in chu) / len(chu)
    if len(chu) >= 4 and ti_le_nguyen_am < 0.2:
        diem += 2
        ly_do.append("gần như không có nguyên âm")
    chuoi_phu_am = max((len(m) for m in re.findall(r"[b-df-hj-np-tv-xz]+", ten)), default=0)
    if chuoi_phu_am >= 5:
        diem += 2
        ly_do.append(f"{chuoi_phu_am} phụ âm liền nhau")
    elif chuoi_phu_am == 4:
        diem += 1
    doi = sum(1 for a, b in zip(ten, ten[1:]) if a.isdigit() != b.isdigit())  # số lần đổi chữ ↔ số
    if doi >= 4:
        diem += 2
        ly_do.append("chữ và số xen kẽ lộn xộn")
    elif doi == 3:
        diem += 1
    if len(ten) >= 8 and len(so) / len(ten) >= 0.5:
        diem += 1
        ly_do.append("quá nhiều chữ số")
    return diem, ly_do


def ly_do_nghi_ngo(email: str) -> str:
    diem, ly_do = diem_ngau_nhien(email)
    return f"Tên email trông ngẫu nhiên ({', '.join(ly_do)})" if diem >= 3 else ""


# ---------------------------------------------------------------- kiểm tra tổng hợp
def kiem_tra(email: str, kiem_mx_dns: bool = True) -> KetQua:
    """Kiểm tra email khi ĐĂNG KÝ. Không chặn tài khoản cũ (đăng nhập không gọi hàm này)."""
    email = (email or "").strip().lower()
    if not dinh_dang_hop_le(email):
        return KetQua("dinh_dang", THONG_BAO["dinh_dang"])
    mien = ten_mien(email)
    nghi_ngo = ly_do_nghi_ngo(email)
    if _khop_quy_tac(mien, "cho"):
        return KetQua(nghi_ngo=nghi_ngo)  # admin cho phép tên miền này → bỏ qua kiểm tra tên miền
    if _khop_quy_tac(mien, "chan"):
        return KetQua("ten_mien_bi_chan", THONG_BAO["ten_mien_bi_chan"])
    if la_email_tam(email):
        return KetQua("email_tam", THONG_BAO["email_tam"])
    if kiem_mx_dns:
        mx = kiem_mx(mien)
        if mx in ("khong_ton_tai", "khong_mx"):
            return KetQua(mx, THONG_BAO[mx].format(mien=mien))
    return KetQua(nghi_ngo=nghi_ngo)


# ---------------------------------------------------------------- nhật ký các lần bị chặn (bảng email_blocks)
def ghi_chan(email: str, ip: str, ly_do: str, chi_tiet: str = "", hanh_dong: str = "dang_ky") -> None:
    """Không bao giờ làm hỏng việc đăng ký vì lỗi ghi log."""
    try:
        with db.ket_noi() as con:
            con.execute("INSERT INTO email_blocks (ts, email, ip, reason, detail, action) VALUES (?, ?, ?, ?, ?, ?)",
                        (time.time(), (email or "").strip().lower()[:254], (ip or "")[:64], ly_do, (chi_tiet or "")[:300], hanh_dong))
    except Exception:  # noqa: BLE001
        log.exception("Không ghi được nhật ký chặn email")
    log.info("Chặn %s: %s (IP %s)", hanh_dong, ly_do, ip)


def so_lan_thu_gio(ip: str) -> int:
    """Số lần thử đăng ký từ IP trong 1 giờ: tài khoản đã tạo + lần bị chặn vì email rác / bot / captcha."""
    tu = time.time() - 3600
    with db.ket_noi() as con:
        tao = con.execute("SELECT COUNT(*) FROM activity_log WHERE event = 'dang_ky' AND method = 'email' AND ip = ? AND ts >= ?",
                          (ip, tu)).fetchone()[0]
        chan = con.execute(f"SELECT COUNT(*) FROM email_blocks WHERE ip = ? AND ts >= ? AND action = 'dang_ky' "
                           f"AND reason IN ({','.join('?' * len(DEM_LAN_THU))})", (ip, tu, *DEM_LAN_THU)).fetchone()[0]
    return tao + chan


def danh_sach_chan(gioi_han: int = 200, ly_do: str = "", tim: str = "") -> list[dict]:
    sql, ts = "SELECT * FROM email_blocks WHERE 1 = 1", []
    if ly_do:
        sql, ts = sql + " AND reason = ?", ts + [ly_do]
    if tim:
        sql, ts = sql + " AND (email LIKE ? OR ip LIKE ?)", ts + [f"%{tim.lower()}%", f"%{tim}%"]
    with db.ket_noi() as con:
        rows = con.execute(sql + " ORDER BY id DESC LIMIT ?", (*ts, max(1, min(gioi_han, 1000)))).fetchall()
    return [{"id": r["id"], "luc": time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(r["ts"])), "email": r["email"], "ip": r["ip"],
             "ly_do": r["reason"], "ten_ly_do": TEN_LY_DO.get(r["reason"], r["reason"]), "chi_tiet": r["detail"],
             "hanh_dong": r["action"]} for r in rows]


def thong_ke_chan(ngay: int = 7) -> dict:
    with db.ket_noi() as con:
        rows = con.execute("SELECT reason, COUNT(*) FROM email_blocks WHERE ts >= ? GROUP BY reason ORDER BY 2 DESC",
                           (time.time() - ngay * 86400,)).fetchall()
    return {"ngay": ngay, "tong": sum(r[1] for r in rows), "theo_ly_do": [{"ly_do": r[0], "ten": TEN_LY_DO.get(r[0], r[0]), "so": r[1]}
                                                                            for r in rows]}


# ---------------------------------------------------------------- hạn xác minh + tài khoản nghi ngờ (admin duyệt)
def dat_han_xac_minh(uid: int) -> None:
    """Tài khoản vừa đăng ký: quá 24 giờ chưa xác minh email thì bị xoá (tài khoản cũ không có hạn → không bao giờ bị xoá)."""
    with db.ket_noi() as con:
        con.execute("UPDATE users SET verify_deadline = ? WHERE id = ?", (time.time() + XOA_CHUA_XAC_MINH_GIAY, uid))


def danh_dau_nghi_ngo(uid: int, ly_do: str) -> None:
    with db.ket_noi() as con:
        con.execute("UPDATE users SET suspect_reason = ?, suspect_at = ?, suspect_review = '', suspect_by = NULL WHERE id = ?",
                    (ly_do[:300], time.time(), uid))


def duyet_nghi_ngo(uid: int, admin_id: int | None, khoa: bool = False) -> bool:
    """Admin duyệt: "ok" (bỏ đánh dấu, tài khoản dùng bình thường) hoặc khoá tài khoản."""
    with db.ket_noi() as con:
        n = con.execute("UPDATE users SET suspect_review = ?, suspect_by = ? WHERE id = ? AND suspect_reason <> ''",
                        ("khoa" if khoa else "ok", admin_id, uid)).rowcount
    if n and khoa:
        db.dat_khoa(uid, True)
    return bool(n)


def danh_sach_nghi_ngo(ca_da_duyet: bool = False) -> list[dict]:
    sql = ("SELECT u.id, u.email, u.name, u.created_at, u.email_verified, u.locked, u.register_ip, u.suspect_reason, u.suspect_at, "
           "u.suspect_review, a.email AS admin_email FROM users u LEFT JOIN users a ON a.id = u.suspect_by WHERE u.suspect_reason <> ''")
    if not ca_da_duyet:
        sql += " AND u.suspect_review = ''"
    with db.ket_noi() as con:
        rows = con.execute(sql + " ORDER BY u.suspect_at DESC LIMIT 300").fetchall()
    return [{"id": r["id"], "email": r["email"], "ten": r["name"], "tao_luc": r["created_at"], "da_xac_minh": bool(r["email_verified"]),
             "bi_khoa": bool(r["locked"]), "ip_dang_ky": r["register_ip"], "ly_do": r["suspect_reason"],
             "duyet": r["suspect_review"], "duyet_boi": r["admin_email"] or ""} for r in rows]


# ---------------------------------------------------------------- dọn dẹp + việc nền
def xoa_chua_xac_minh() -> list[dict]:
    """Xoá tài khoản ĐĂNG KÝ MỚI (có hạn xác minh) quá 24 giờ chưa xác minh email. Tài khoản cũ (không có hạn) không bao giờ bị xoá."""
    with db.ket_noi() as con:
        rows = [dict(r) for r in con.execute(
            "SELECT id, email, name, register_ip FROM users WHERE verify_deadline IS NOT NULL AND verify_deadline < ? "
            "AND email_verified = 0 AND is_admin = 0 AND is_veteran = 0 AND google_sub LIKE ?", (time.time(), db.SUB_EMAIL + "%"))]
        for r in rows:
            con.execute("DELETE FROM users WHERE id = ?", (r["id"],))
            con.execute("DELETE FROM email_codes WHERE user_id = ?", (r["id"],))
    return rows


def don_nhat_ky_chan() -> None:
    with db.ket_noi() as con:
        con.execute("DELETE FROM email_blocks WHERE ts < ?", (time.time() - GIU_NHAT_KY_CHAN_NGAY * 86400,))


def mot_vong() -> None:
    """Một lượt việc nền (gọi mỗi giờ): xoá tài khoản chưa xác minh, dọn nhật ký chặn cũ, cập nhật danh sách nếu cũ hơn 7 ngày."""
    from . import nhat_ky
    for r in xoa_chua_xac_minh():
        nhat_ky.ghi("xoa_chua_xac_minh", email=r["email"], ten=r["name"], phuong_thuc="tu_dong", ip=r["register_ip"] or "", ua="",
                    chi_tiet=f"#{r['id']} quá 24 giờ chưa xác minh email")
        log.info("Đã xoá tài khoản #%s: quá 24 giờ chưa xác minh email", r["id"])
    don_nhat_ky_chan()
    if can_cap_nhat():
        try:
            cap_nhat_danh_sach()
        except LoiCapNhat as e:
            log.warning("Cập nhật danh sách email tạm thời: %s", e)


_luong_nen: threading.Thread | None = None


def chay_nen(cach_giay: int = 3600, tre_giay: int = 30) -> None:
    """Gọi một lần lúc app khởi động (main._vong_doi): luồng nền chạy mot_vong() mỗi giờ (lần đầu sau 30 giây)."""
    global _luong_nen
    if _luong_nen is not None and _luong_nen.is_alive():
        return

    def _chay():
        time.sleep(tre_giay)
        while True:
            try:
                mot_vong()
            except Exception:  # noqa: BLE001
                log.exception("Việc nền chặn email rác lỗi")
            time.sleep(cach_giay)

    _luong_nen = threading.Thread(target=_chay, name="email-rac", daemon=True)
    _luong_nen.start()
