"""Nhật ký hoạt động tài khoản (bảng activity_log) + các truy vấn cho admin: Cài đặt → Hoạt động, trang /admin, admin_cli.py.

Ghi: đăng ký, đăng nhập (thành công/thất bại), đăng xuất, xác thực email, đặt lại mật khẩu, đổi tên, thao tác của admin (khoá/mở khoá…)
kèm thời gian, tên, email, phương thức, IP thật (dia_chi_ip.get_client_ip) + vị trí/nhà mạng tra offline, trình duyệt/thiết bị
(từ User-Agent). KHÔNG bao giờ ghi mật khẩu hay mã xác thực.
"Đang online" = có yêu cầu trong 5 phút gần nhất (users.last_seen, ghi tối đa 1 lần/phút/người). Nhật ký cũ hơn 90 ngày tự xoá.
"""
from __future__ import annotations

import logging
import threading
from collections import Counter
import time
from datetime import date, datetime, timedelta

from . import db, tai_khoan
from . import dia_chi_ip as dip
from .db import NguoiDung
from .dia_chi_ip import KHONG_XAC_DINH, get_client_ip

log = logging.getLogger(__name__)

SU_KIEN = {
    "dang_ky": "Đăng ký",
    "dang_nhap": "Đăng nhập",
    "dang_nhap_sai": "Đăng nhập sai",
    "dang_xuat": "Đăng xuất",
    "xac_thuc_email": "Xác thực email",
    "xac_thuc_sai": "Nhập sai mã",
    "dat_lai_mat_khau": "Đặt lại mật khẩu",
    "doi_ten": "Đổi tên",
    "khoa": "Khoá tài khoản",
    "mo_khoa": "Mở khoá tài khoản",
    "doi_nhan": "Đổi nhãn thành viên",
    "gan_ma_claude": "Nhập mã Claude",
    "nhap_sai_ma_claude": "Nhập sai mã Claude",
    "tao_ma_claude": "Tạo mã Claude",
    "thu_hoi_ma_claude": "Thu hồi mã Claude",
    "them_luot_ma_claude": "Cộng lượt mã Claude",
    "kich_hoat_key_claude": "Kích hoạt Key Claude",
    "nhap_sai_key_claude": "Nhập sai Key Claude",
    "khoa_key_claude": "Bị khoá nhập Key Claude",
    "go_key_claude": "Gỡ Key Claude",
    "thu_hoi_key_claude": "Thu hồi quyền Claude",
    "mo_lai_key_claude": "Mở lại quyền Claude",
    "bat_key_claude_rieng": "Bật key Claude riêng",
    "tat_key_claude_rieng": "Tắt key Claude riêng",
    "yeu_cau_key_claude_rieng": "Yêu cầu bật key Claude riêng",
    "tu_choi_key_claude_rieng": "Từ chối key Claude riêng",
    "email_nghi_ngo": "Email nghi ngờ",
    "duyet_nghi_ngo": "Duyệt email nghi ngờ",
    "xoa_chua_xac_minh": "Xoá tài khoản chưa xác minh",
    "sua_ds_email": "Sửa danh sách tên miền email",
    "doi_giao_dien": "Đổi giao diện bằng mã",
    "nhap_sai_ma_giao_dien": "Nhập sai mã giao diện",
    "tao_ma_giao_dien": "Tạo mã giao diện",
    "thu_hoi_ma_giao_dien": "Thu hồi mã giao diện",
    "doi_thu_tu_ai": "Đổi thứ tự AI",
    "doi_phong_thi_nghiem": "Bật/tắt bản Unity Phòng thí nghiệm",
    "doi_ung_dung_lab": "Cài đặt ứng dụng máy tính",
    "ai_rieng": "Quản trị AI Riêng",
    "bao_sai_ai": "Báo lời giải sai",
}
# tên nhóm dùng ở bộ lọc (admin_cli --type, ô lọc trên web) → các sự kiện
NHOM = {
    "login": ("dang_nhap",), "register": ("dang_ky",), "fail": ("dang_nhap_sai", "xac_thuc_sai"), "logout": ("dang_xuat",),
    "verify": ("xac_thuc_email", "xac_thuc_sai"), "rename": ("doi_ten",), "admin": ("khoa", "mo_khoa", "doi_nhan", "tao_ma_claude", "thu_hoi_ma_claude", "them_luot_ma_claude",
                                                       "thu_hoi_key_claude", "mo_lai_key_claude", "bat_key_claude_rieng", "tat_key_claude_rieng",
                                                       "tu_choi_key_claude_rieng", "duyet_nghi_ngo", "sua_ds_email",
                                                       "tao_ma_giao_dien", "thu_hoi_ma_giao_dien", "doi_thu_tu_ai", "doi_phong_thi_nghiem", "doi_ung_dung_lab", "ai_rieng"),
    "claude": ("gan_ma_claude", "nhap_sai_ma_claude", "tao_ma_claude", "thu_hoi_ma_claude", "them_luot_ma_claude",
               "kich_hoat_key_claude", "nhap_sai_key_claude", "khoa_key_claude", "go_key_claude", "thu_hoi_key_claude", "mo_lai_key_claude",
               "bat_key_claude_rieng", "tat_key_claude_rieng", "yeu_cau_key_claude_rieng", "tu_choi_key_claude_rieng"),
    "password": ("dat_lai_mat_khau",),
    "spam": ("email_nghi_ngo", "duyet_nghi_ngo", "xoa_chua_xac_minh", "sua_ds_email"),
    "theme": ("doi_giao_dien", "nhap_sai_ma_giao_dien", "tao_ma_giao_dien", "thu_hoi_ma_giao_dien"),
}
PHUONG_THUC = {"email": "email + mật khẩu", "google": "Google", "ma_email": "mã qua email", "web": "web", "admin_web": "trang admin",
               "cli": "dòng lệnh (máy chủ)", "tu_dong": "tự động"}

ONLINE_GIAY = 300          # "đang online": có hoạt động trong 5 phút
GIU_NGAY = 90              # tự xoá nhật ký cũ hơn 90 ngày
SAI_NHIEU = 3              # ≥ 3 lần đăng nhập sai (cùng email) trong 24 giờ → đánh dấu
SAI_NHIEU_IP = 5           # ≥ 5 lần sai từ cùng IP trong 24 giờ
TK_CUNG_IP = 3             # ≥ 3 tài khoản khác nhau dùng cùng IP trong 7 ngày
DANG_KY_LOAT = 5           # ≥ 5 đăng ký trong 10 phút (mọi IP) = tạo hàng loạt
TAI_KHOAN_MOI_GIAY = 86400
NEN_QUOC_GIA = 2           # cần ≥ 2 lần đăng nhập trước đó (biết vị trí) mới so "quốc gia khác thường lệ"
NEN_THANH_PHO = 3          # cần ≥ 3 lần trước đó trong cùng quốc gia mới so "tỉnh/thành khác thường lệ"

_khoa_online = threading.Lock()
_lan_ghi_online: dict[tuple[str, int], float] = {}
_don_lan_cuoi = 0.0


# ---------------------------------------------------------------- IP (dia_chi_ip.get_client_ip) + thiết bị
def ua_cua(request) -> str:
    return (request.headers.get("user-agent", "") if request is not None else "")[:300]


def thiet_bi(ua: str) -> str:
    """User-Agent → "Trình duyệt · Hệ điều hành (loại máy)" ngắn gọn, đủ để admin nhận ra."""
    if not ua:
        return "không rõ"
    thap = ua.lower()
    if "zalo" in thap:
        trinh = "Zalo"
    elif "fban" in thap or "fbav" in thap or "fb_iab" in thap:
        trinh = "Facebook"
    elif "coc_coc" in thap or "coccoc" in thap:
        trinh = "Cốc Cốc"
    elif "edg/" in thap or "edga/" in thap or "edgios" in thap:
        trinh = "Edge"
    elif "opr/" in thap or "opera" in thap:
        trinh = "Opera"
    elif "samsungbrowser" in thap:
        trinh = "Samsung Internet"
    elif "firefox/" in thap or "fxios" in thap:
        trinh = "Firefox"
    elif "chrome/" in thap or "crios" in thap:
        trinh = "Chrome"
    elif "safari/" in thap:
        trinh = "Safari"
    elif thap.startswith("curl"):
        trinh = "curl"
    elif "python" in thap or "httpx" in thap or "testclient" in thap:
        trinh = "Python"
    else:
        trinh = "Trình duyệt khác"
    if "iphone" in thap:
        may = "iPhone"
    elif "ipad" in thap:
        may = "iPad"
    elif "android" in thap:
        may = "Android (điện thoại)" if "mobile" in thap else "Android (máy tính bảng)"
    elif "windows" in thap:
        may = "Windows"
    elif "macintosh" in thap or "mac os x" in thap:
        may = "macOS"
    elif "cros" in thap:
        may = "ChromeOS"
    elif "linux" in thap:
        may = "Linux"
    else:
        may = ""
    return f"{trinh} · {may}" if may else trinh


# ---------------------------------------------------------------- ghi
def ghi(su_kien: str, request=None, u: NguoiDung | None = None, *, email: str = "", ten: str = "", phuong_thuc: str = "",
        chi_tiet: str = "", doi_tuong: int | None = None, ip: str | None = None, ua: str | None = None) -> int:
    """Ghi một sự kiện; lỗi CSDL chỉ ghi log, không làm hỏng việc đăng nhập."""
    ua = ua_cua(request) if ua is None else ua
    try:
        ip = get_client_ip(request) if ip is None else ip
        geo = dip.tra_ip(ip) if dip.chuan_ip(ip) else {}
        with db.ket_noi() as con:
            ma = con.execute(
                "INSERT INTO activity_log (ts, event, user_id, target_id, email, name, method, ip, user_agent, device, detail, "
                "ip_v, geo_qg, geo_noi, geo_mang) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?)",
                (time.time(), su_kien, u.id if u else None, doi_tuong, (u.email if u and not email else email)[:254],
                 (u.ten if u and not ten else ten)[:80], phuong_thuc, ip[:64], ua[:300], thiet_bi(ua), chi_tiet[:300],
                 geo.get("qg", ""), geo.get("vi_tri", "")[:80], geo.get("nha_mang", "")[:60])).lastrowid
    except Exception:  # noqa: BLE001
        log.exception("Không ghi được nhật ký hoạt động (%s)", su_kien)
        return 0
    don_dep_dinh_ky()
    return ma


def cap_nhat_online(u: NguoiDung, request, ep: bool = False) -> None:
    """Ghi lần hoạt động cuối của người dùng (tối đa 1 lần/phút/người để không ghi CSDL mỗi yêu cầu; ep=True: ghi ngay, vd lúc đăng nhập)."""
    bay_gio = time.time()
    khoa = (str(db.DUONG_DAN), u.id)
    with _khoa_online:
        if not ep and bay_gio - _lan_ghi_online.get(khoa, 0) < 60:
            return
        _lan_ghi_online[khoa] = bay_gio
    try:
        db.ghi_hoat_dong_cuoi(u.id, get_client_ip(request), ua_cua(request))
    except Exception:  # noqa: BLE001
        log.exception("Không ghi được lần hoạt động cuối")


def quen_online(uid: int) -> None:
    """Đăng xuất → không tính là online nữa (xoá dấu thời gian cuối)."""
    with _khoa_online:
        _lan_ghi_online.pop((str(db.DUONG_DAN), uid), None)
    with db.ket_noi() as con:
        con.execute("UPDATE users SET last_seen = NULL WHERE id = ?", (uid,))


def don_dep(giu_ngay: int = GIU_NGAY) -> int:
    with db.ket_noi() as con:
        n = con.execute("DELETE FROM activity_log WHERE ts < ?", (time.time() - giu_ngay * 86400,)).rowcount
    if n:
        log.info("Đã xoá %d dòng nhật ký cũ hơn %d ngày", n, giu_ngay)
    return n


def don_dep_dinh_ky() -> None:
    """Tự dọn tối đa 1 lần/giờ (gọi sau mỗi lần ghi)."""
    global _don_lan_cuoi
    if time.time() - _don_lan_cuoi < 3600:
        return
    _don_lan_cuoi = time.time()
    try:
        don_dep()
        from .xac_thuc_email import don_dep as don_ma
        don_ma()
    except Exception:  # noqa: BLE001
        log.exception("Không dọn được nhật ký cũ")


# ---------------------------------------------------------------- đọc
def _ngay_dau(ngay: date) -> float:
    return time.mktime(datetime(ngay.year, ngay.month, ngay.day).timetuple())


def dau_hom_nay() -> float:
    return _ngay_dau(date.today())


def gio(ts: float | None) -> str:
    return time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(ts)) if ts else ""


def ip_info(r) -> dict:
    """Thông tin IP của một dòng nhật ký: vị trí/nhà mạng đã lưu lúc ghi (dòng cũ thì tra lại); 127.0.0.1 ghi trước khi sửa → cu=True."""
    return dip.thong_tin(r["ip"], cu=not r["ip_v"], qg=r["geo_qg"], vi_tri=r["geo_noi"], nha_mang=r["geo_mang"])


def _dong(r) -> dict:
    tt = ip_info(r)
    return {"id": r["id"], "ts": r["ts"], "thoi_gian": gio(r["ts"]), "su_kien": r["event"], "ten_su_kien": SU_KIEN.get(r["event"], r["event"]),
            "user_id": r["user_id"], "doi_tuong": r["target_id"], "email": r["email"], "ten": r["name"],
            "phuong_thuc": r["method"], "ten_phuong_thuc": PHUONG_THUC.get(r["method"], r["method"]),
            "ip": r["ip"], "ip_info": tt, "ip_mo_ta": tt["mo_ta"], "thiet_bi": r["device"], "user_agent": r["user_agent"],
            "chi_tiet": r["detail"]}


def cac_su_kien(loai: str) -> tuple[str, ...]:
    """'login' / 'dang_nhap' / 'dang_nhap,dang_ky' → các mã sự kiện."""
    kq: list[str] = []
    for phan in (loai or "").split(","):
        phan = phan.strip()
        if phan in NHOM:
            kq += NHOM[phan]
        elif phan in SU_KIEN:
            kq.append(phan)
    return tuple(dict.fromkeys(kq))


def danh_sach(*, nguoi: str = "", loai: str = "", ngay: str = "", truoc_id: int | None = None, sau_id: int | None = None,
              gioi_han: int = 50, ip: str = "") -> list[dict]:
    """Nhật ký mới nhất trước. nguoi: email/tên (chứa) hoặc "#id"; loai: mã sự kiện hoặc nhóm (login|register|fail…);
    ngay: "YYYY-MM-DD" hoặc "hom_nay"; ip: đúng một IP."""
    dk, ts = ["1 = 1"], []
    if ip:
        dk.append("ip = ?")
        ts.append(dip.chuan_ip(ip) or ip.strip())
    nguoi = (nguoi or "").strip()
    if nguoi.startswith("#") and nguoi[1:].isdigit():
        dk.append("(user_id = ? OR target_id = ?)")
        ts += [int(nguoi[1:])] * 2
    elif nguoi:
        dk.append("(lower(email) LIKE ? OR lower(name) LIKE ?)")
        ts += [f"%{nguoi.lower()}%"] * 2
    if loai:
        ev = cac_su_kien(loai)
        if not ev:
            return []
        dk.append(f"event IN ({','.join('?' * len(ev))})")
        ts += list(ev)
    if ngay:
        try:
            d = date.today() if ngay == "hom_nay" else date.fromisoformat(ngay)
        except ValueError:
            return []
        dk.append("ts >= ? AND ts < ?")
        ts += [_ngay_dau(d), _ngay_dau(d + timedelta(days=1))]
    if truoc_id:
        dk.append("id < ?")
        ts.append(int(truoc_id))
    if sau_id is not None:
        dk.append("id > ?")
        ts.append(int(sau_id))
    ts.append(max(1, min(int(gioi_han), 500)))
    with db.ket_noi() as con:
        rows = con.execute(f"SELECT * FROM activity_log WHERE {' AND '.join(dk)} ORDER BY id DESC LIMIT ?", ts).fetchall()
    return danh_dau([_dong(r) for r in rows])


def id_moi_nhat() -> int:
    with db.ket_noi() as con:
        return con.execute("SELECT COALESCE(MAX(id), 0) FROM activity_log").fetchone()[0]


def danh_dau(ds: list[dict]) -> list[dict]:
    """Thêm d["co"]: tai_khoan_moi (tạo < 24 giờ) · sai_nhieu (đăng nhập sai nhiều lần) · nhieu_tai_khoan_ip (≥ 3 tài khoản cùng IP)
    · vi_tri_la (đăng nhập từ quốc gia / tỉnh-thành khác thường lệ, kèm d["vi_tri_la"])."""
    if not ds:
        return ds
    bay_gio = time.time()
    uids = {d["user_id"] for d in ds if d["user_id"]} | {d["doi_tuong"] for d in ds if d["doi_tuong"]}
    with db.ket_noi() as con:
        moi = set()
        if uids:
            moc = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(bay_gio - TAI_KHOAN_MOI_GIAY))
            moi = {r[0] for r in con.execute(
                f"SELECT id FROM users WHERE id IN ({','.join('?' * len(uids))}) AND created_at >= ?", [*uids, moc])}
        sai_email = {r[0] for r in con.execute(
            "SELECT email FROM activity_log WHERE event = 'dang_nhap_sai' AND ts >= ? AND email <> '' GROUP BY email HAVING COUNT(*) >= ?",
            (bay_gio - 86400, SAI_NHIEU))}
        sai_ip = {r[0] for r in con.execute(
            "SELECT ip FROM activity_log WHERE event = 'dang_nhap_sai' AND ts >= ? AND ip NOT IN ('', ?) GROUP BY ip HAVING COUNT(*) >= ?",
            (bay_gio - 86400, KHONG_XAC_DINH, SAI_NHIEU_IP))}
        ip_nhieu = {r[0] for r in con.execute(
            "SELECT ip FROM activity_log WHERE event IN ('dang_ky', 'dang_nhap') AND ts >= ? AND ip NOT IN ('', ?) AND user_id IS NOT NULL "
            "GROUP BY ip HAVING COUNT(DISTINCT user_id) >= ?", (bay_gio - 7 * 86400, KHONG_XAC_DINH, TK_CUNG_IP))}
    la = vi_tri_la({d["user_id"] for d in ds if d["su_kien"] == "dang_nhap" and d["user_id"]})
    for d in ds:
        co = []
        if (d["user_id"] in moi and d["su_kien"] in ("dang_ky", "xac_thuc_email", "dang_nhap")) or d["su_kien"] == "dang_ky":
            co.append("tai_khoan_moi")
        if d["su_kien"] in ("dang_nhap_sai", "dang_nhap") and (d["email"] in sai_email or d["ip"] in sai_ip):
            co.append("sai_nhieu")
        if d["ip"] in ip_nhieu:
            co.append("nhieu_tai_khoan_ip")
        if d["id"] in la:
            co.append("vi_tri_la")
            d["vi_tri_la"] = la[d["id"]]
        d["co"] = co
    return ds


def _vi_tri_dang_nhap(r) -> tuple[str, str]:
    """(mã quốc gia, tỉnh/thành) của một lượt đăng nhập: giá trị lưu lúc ghi, thiếu thì tra lại; IP nội bộ / không rõ → ("", "")."""
    if r["geo_qg"]:
        return r["geo_qg"], r["geo_noi"]
    if not dip.chuan_ip(r["ip"]):
        return "", ""
    t = dip.tra_ip(r["ip"])
    return t["qg"], t["vi_tri"]


def vi_tri_la(uids: set | None = None, tu: float = 0) -> dict[int, dict]:
    """Lượt đăng nhập (từ thời điểm `tu`) ở quốc gia / tỉnh-thành mà CHÍNH người đó chưa từng đăng nhập trước đây:
    quốc gia mới khi đã có ≥ NEN_QUOC_GIA lần trước biết vị trí; tỉnh/thành mới khi đã có ≥ NEN_THANH_PHO lần trong quốc gia đó.
    Trả {id nhật ký: {"loai": "quoc_gia"|"thanh_pho", "noi", "thuong_le"}}. Vị trí IP chỉ gần đúng (4G hay báo sai tỉnh)."""
    dk, ts = "event = 'dang_nhap' AND user_id IS NOT NULL", []
    if uids is not None:
        if not uids:
            return {}
        dk += f" AND user_id IN ({','.join('?' * len(uids))})"
        ts += list(uids)
    with db.ket_noi() as con:
        rows = con.execute(f"SELECT id, user_id, ts, ip, geo_qg, geo_noi FROM activity_log WHERE {dk} ORDER BY user_id, ts, id",
                           ts).fetchall()
    kq: dict[int, dict] = {}
    theo_qg: dict[int, Counter] = {}
    theo_noi: dict[tuple, Counter] = {}
    for r in rows:
        qg, noi = _vi_tri_dang_nhap(r)
        if not qg:
            continue
        dem_qg = theo_qg.setdefault(r["user_id"], Counter())
        dem_noi = theo_noi.setdefault((r["user_id"], qg), Counter())
        if r["ts"] >= tu:
            if sum(dem_qg.values()) >= NEN_QUOC_GIA and qg not in dem_qg:
                kq[r["id"]] = {"loai": "quoc_gia", "noi": noi or dip.ten_quoc_gia(qg),
                               "thuong_le": ", ".join(dip.ten_quoc_gia(q) for q, _ in dem_qg.most_common(3))}
            elif noi and sum(dem_noi.values()) >= NEN_THANH_PHO and noi not in dem_noi:
                kq[r["id"]] = {"loai": "thanh_pho", "noi": noi, "thuong_le": ", ".join(n for n, _ in dem_noi.most_common(3))}
        dem_qg[qg] += 1
        if noi:
            dem_noi[noi] += 1
    return kq


def online(giay: int = ONLINE_GIAY) -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT * FROM users WHERE last_seen >= ? ORDER BY last_seen DESC", (time.time() - giay,)).fetchall()
    return [{"id": r["id"], "ten": r["name"], "email": r["email"], "hoat_dong_cuoi": r["last_seen"], "thoi_gian": gio(r["last_seen"]),
             "ip": r["last_ip"], "ip_info": dip.thong_tin(r["last_ip"]), "thiet_bi": thiet_bi(r["last_ua"]),
             "vai_tro": _vai_tro(db._nguoi_dung(r))} for r in rows]


def nhan_thanh_vien() -> str:
    return db.cai_dat("nhan_thanh_vien", "thành viên") or "thành viên"


NHAN_KI_CUU = "kì cựu"


def _vai_tro(u: NguoiDung, nhan: str | None = None) -> str:
    return "admin" if tai_khoan.la_chu_app(u) else NHAN_KI_CUU if u.la_ki_cuu else (nhan or nhan_thanh_vien())


def nguoi_dung_bang() -> list[dict]:
    """Bảng mọi người dùng cho admin (không có mật khẩu/key)."""
    nhan = nhan_thanh_vien()
    bay_gio = time.time()
    with db.ket_noi() as con:
        rows = con.execute(
            "SELECT u.*, (SELECT COUNT(*) FROM solutions s WHERE s.user_id = u.id AND s.status = 'xong') AS so_bai, "
            "(SELECT MAX(ts) FROM activity_log a WHERE a.user_id = u.id AND a.event = 'dang_nhap') AS dang_nhap_ts "
            "FROM users u ORDER BY u.id").fetchall()
    kq = []
    for r in rows:
        u = db._nguoi_dung(r)
        kq.append({
            "id": u.id, "ten": u.ten, "email": u.email, "vai_tro": _vai_tro(u, nhan), "la_admin": tai_khoan.la_chu_app(u),
            "la_ki_cuu": u.la_ki_cuu and not tai_khoan.la_chu_app(u),
            "dang_nhap_bang": u.dang_nhap_bang, "da_xac_thuc": not u.can_xac_thuc and u.dang_nhap_bang != "cuc_bo",
            "xac_thuc": ("-" if u.cuc_bo else "roi" if u.da_xac_thuc_email else "mien" if not u.can_xac_thuc else "chua"),
            "tao_luc": u.tao_luc, "dang_nhap_cuoi": gio(r["dang_nhap_ts"]) or u.dang_nhap_cuoi,
            "hoat_dong_cuoi": gio(u.hoat_dong_cuoi), "online": bool(u.hoat_dong_cuoi and u.hoat_dong_cuoi >= bay_gio - ONLINE_GIAY),
            "so_bai_giai": r["so_bai"], "bi_khoa": u.bi_khoa, "ip_dang_ky": u.ip_dang_ky, "co_key": u.co_key,
            "ip_dang_ky_info": dip.thong_tin(u.ip_dang_ky), "ip_cuoi": r["last_ip"], "ip_cuoi_info": dip.thong_tin(r["last_ip"]),
            "luot_da_dung": u.luot_da_dung,
            "nghi_ngo": r["suspect_reason"] if r["suspect_review"] == "" else "",  # email nghi ngờ đang chờ admin duyệt
        })
    return kq


def tim_nguoi_dung(chuoi: str) -> NguoiDung | None:
    """'#12' / '12' / email (so sánh sau chuẩn hoá)."""
    chuoi = (chuoi or "").strip()
    so = chuoi.lstrip("#")
    if so.isdigit():
        return db.lay_nguoi_dung(int(so))
    return db.tim_theo_email(chuoi)


def chi_tiet(uid: int, gioi_han: int = 200) -> dict | None:
    u = db.lay_nguoi_dung(uid)
    if u is None:
        return None
    hang = next((d for d in nguoi_dung_bang() if d["id"] == uid), {})
    with db.ket_noi() as con:
        bai = con.execute("SELECT COUNT(*) AS tong, SUM(status = 'xong') AS xong, SUM(status = 'loi') AS loi, MAX(created_at) AS cuoi "
                          "FROM solutions WHERE user_id = ?", (uid,)).fetchone()
    return {
        **hang,
        "lich_su": danh_sach(nguoi=f"#{uid}", gioi_han=gioi_han),
        "doi_ten": [{**x, "ip_info": dip.thong_tin(x.get("ip") or "")} for x in db.lich_su_doi_ten(uid)],
        "bai_giai": {"tong": bai["tong"] or 0, "xong": bai["xong"] or 0, "loi": bai["loi"] or 0, "cuoi": bai["cuoi"] or ""},
    }


def canh_bao() -> dict:
    """Dấu hiệu đáng ngờ: nhiều tài khoản cùng IP, đăng nhập sai nhiều lần, tài khoản mới tạo hàng loạt,
    đăng nhập từ quốc gia / tỉnh-thành khác thường lệ (7 ngày)."""
    bay_gio = time.time()
    with db.ket_noi() as con:
        cung_ip = []
        for r in con.execute(
                "SELECT ip, COUNT(DISTINCT user_id) AS n, MAX(ts) AS cuoi FROM activity_log WHERE event IN ('dang_ky', 'dang_nhap') "
                "AND ts >= ? AND ip NOT IN ('', ?) AND user_id IS NOT NULL GROUP BY ip HAVING n >= ? ORDER BY n DESC, cuoi DESC",
                (bay_gio - 7 * 86400, KHONG_XAC_DINH, TK_CUNG_IP)).fetchall():
            tk = con.execute(
                "SELECT DISTINCT u.id, u.name, u.email FROM activity_log a JOIN users u ON u.id = a.user_id "
                "WHERE a.ip = ? AND a.ts >= ? AND a.event IN ('dang_ky', 'dang_nhap') ORDER BY u.id", (r["ip"], bay_gio - 7 * 86400)).fetchall()
            cung_ip.append({"ip": r["ip"], "ip_info": dip.thong_tin(r["ip"]), "so_tai_khoan": r["n"], "lan_cuoi": gio(r["cuoi"]),
                            "tai_khoan": [{"id": t["id"], "ten": t["name"], "email": t["email"]} for t in tk]})
        sai = [{"email": r["email"], "so_lan": r["n"], "lan_cuoi": gio(r["cuoi"]), "ips": sorted(set((r["ips"] or "").split(",")) - {""})}
               for r in con.execute(
                   "SELECT email, COUNT(*) AS n, MAX(ts) AS cuoi, GROUP_CONCAT(DISTINCT ip) AS ips FROM activity_log "
                   "WHERE event = 'dang_nhap_sai' AND ts >= ? AND email <> '' GROUP BY email HAVING n >= ? ORDER BY n DESC",
                   (bay_gio - 86400, SAI_NHIEU))]
        sai_ip = [{"ip": r["ip"], "ip_info": dip.thong_tin(r["ip"]), "so_lan": r["n"], "so_email": r["m"], "lan_cuoi": gio(r["cuoi"])}
                  for r in con.execute(
                      "SELECT ip, COUNT(*) AS n, COUNT(DISTINCT email) AS m, MAX(ts) AS cuoi FROM activity_log "
                      "WHERE event = 'dang_nhap_sai' AND ts >= ? AND ip NOT IN ('', ?) GROUP BY ip HAVING n >= ? ORDER BY n DESC",
                      (bay_gio - 86400, KHONG_XAC_DINH, SAI_NHIEU_IP))]
        dk = con.execute("SELECT ts, ip, email FROM activity_log WHERE event = 'dang_ky' AND ts >= ? ORDER BY ts",
                         (bay_gio - 7 * 86400,)).fetchall()
    loat, i = [], 0  # cửa sổ 10 phút có ≥ DANG_KY_LOAT đăng ký (gộp các cửa sổ chồng nhau)
    for j in range(len(dk)):
        while dk[j]["ts"] - dk[i]["ts"] > 600:
            i += 1
        if j - i + 1 >= DANG_KY_LOAT:
            if loat and dk[i]["ts"] <= loat[-1]["_den"]:
                loat[-1].update(_den=dk[j]["ts"], den=gio(dk[j]["ts"]))
                loat[-1]["_cac"] |= {k for k in range(i, j + 1)}
            else:
                loat.append({"tu": gio(dk[i]["ts"]), "_den": dk[j]["ts"], "den": gio(dk[j]["ts"]), "_cac": set(range(i, j + 1))})
    tao_loat = []
    for d in loat:
        cac = [dk[k] for k in sorted(d["_cac"])]
        tao_loat.append({"tu": d["tu"], "den": d["den"], "so_dang_ky": len(cac), "ips": sorted({c["ip"] for c in cac}),
                         "emails": [c["email"] for c in cac]})
    theo_ip: dict[str, list] = {}
    for r in dk:
        if r["ts"] >= bay_gio - 86400 and r["ip"]:
            theo_ip.setdefault(r["ip"], []).append(r["email"])
    tao_loat += [{"ip": ip, "so_dang_ky": len(em), "emails": em, "trong": "24 giờ"} for ip, em in theo_ip.items() if len(em) >= 3]
    la = vi_tri_la(tu=bay_gio - 7 * 86400)
    vi_tri = []
    if la:
        with db.ket_noi() as con:
            rows = con.execute(f"SELECT * FROM activity_log WHERE id IN ({','.join('?' * len(la))}) ORDER BY id DESC", list(la)).fetchall()
        vi_tri = [{**la[r["id"]], "id": r["id"], "user_id": r["user_id"], "ten": r["name"], "email": r["email"], "lan": gio(r["ts"]),
                   "ip": r["ip"], "ip_info": ip_info(r), "thiet_bi": r["device"]} for r in rows]
    return {"nhieu_tai_khoan_cung_ip": cung_ip, "dang_nhap_sai_nhieu": sai, "dang_nhap_sai_theo_ip": sai_ip,
            "tao_hang_loat": tao_loat, "vi_tri_la": vi_tri,
            "tong": len(cung_ip) + len(sai) + len(sai_ip) + len(tao_loat) + len(vi_tri)}


def theo_ip(ip: str, gioi_han: int = 50) -> dict:
    """Mọi tài khoản từng đăng ký / đăng nhập / dùng app từ một IP + các email đăng nhập sai từ IP đó + nhật ký gần đây của IP."""
    ip = dip.chuan_ip(ip) or (ip or "").strip()
    with db.ket_noi() as con:
        dong = con.execute(
            "SELECT user_id, COUNT(*) AS n, MIN(ts) AS dau, MAX(ts) AS cuoi, GROUP_CONCAT(DISTINCT event) AS ev FROM activity_log "
            "WHERE ip = ? AND user_id IS NOT NULL AND event IN ('dang_ky', 'dang_nhap', 'xac_thuc_email', 'dat_lai_mat_khau') "
            "GROUP BY user_id ORDER BY cuoi DESC", (ip,)).fetchall()
        khac = con.execute("SELECT id, register_ip, last_ip, last_seen FROM users WHERE register_ip = ? OR last_ip = ?", (ip, ip)).fetchall()
        sai = con.execute("SELECT email, COUNT(*) AS n, MAX(ts) AS cuoi FROM activity_log WHERE ip = ? AND event = 'dang_nhap_sai' "
                          "GROUP BY email ORDER BY cuoi DESC", (ip,)).fetchall()
    nhan = nhan_thanh_vien()
    tk: dict[int, dict] = {}
    for r in dong:
        tk[r["user_id"]] = {"so_lan_dang_nhap": 0, "lan_dau": gio(r["dau"]), "lan_cuoi": gio(r["cuoi"]), "_cuoi": r["cuoi"],
                            "su_kien": sorted(set((r["ev"] or "").split(",")) - {""}), "ghi_chu": ""}
    with db.ket_noi() as con:
        for uid, n in con.execute("SELECT user_id, COUNT(*) FROM activity_log WHERE ip = ? AND event = 'dang_nhap' AND user_id IS NOT NULL "
                                  "GROUP BY user_id", (ip,)):
            if uid in tk:
                tk[uid]["so_lan_dang_nhap"] = n
    for r in khac:  # dùng app từ IP này (vd đổi mạng giữa chừng) dù không có lượt đăng nhập nào ghi IP này
        if r["id"] not in tk:
            tk[r["id"]] = {"so_lan_dang_nhap": 0, "lan_dau": "", "lan_cuoi": gio(r["last_seen"]) if r["last_ip"] == ip else "",
                           "_cuoi": r["last_seen"] or 0, "su_kien": [],
                           "ghi_chu": "IP lúc đăng ký" if r["register_ip"] == ip else "dùng app từ IP này (lần cuối)"}
    kq = []
    for uid, x in sorted(tk.items(), key=lambda kv: kv[1]["_cuoi"] or 0, reverse=True):
        u = db.lay_nguoi_dung(uid)
        if u is None:
            continue
        x.pop("_cuoi")
        kq.append({"id": u.id, "ten": u.ten, "email": u.email, "vai_tro": _vai_tro(u, nhan), "la_admin": tai_khoan.la_chu_app(u),
                   "la_ki_cuu": u.la_ki_cuu and not tai_khoan.la_chu_app(u), "bi_khoa": u.bi_khoa, **x})
    return {"ip": ip, "ip_info": dip.thong_tin(ip), "tai_khoan": kq,
            "dang_nhap_sai": [{"email": r["email"], "so_lan": r["n"], "lan_cuoi": gio(r["cuoi"])} for r in sai],
            "gan_day": danh_sach(ip=ip, gioi_han=gioi_han)}


def tong_quan() -> dict:
    dau = dau_hom_nay()
    with db.ket_noi() as con:
        dem = dict(con.execute("SELECT event, COUNT(*) FROM activity_log WHERE ts >= ? GROUP BY event", (dau,)).fetchall())
        bai = con.execute("SELECT COUNT(*) FROM solutions WHERE created_at >= ? AND status = 'xong'",
                          (time.strftime("%Y-%m-%d 00:00:00"),)).fetchone()[0]
        tong = con.execute("SELECT COUNT(*) FROM users WHERE google_sub <> ?", (db.SUB_CUC_BO,)).fetchone()[0]
    return {"online": len(online()), "dang_ky_hom_nay": dem.get("dang_ky", 0), "dang_nhap_hom_nay": dem.get("dang_nhap", 0),
            "dang_nhap_sai_hom_nay": dem.get("dang_nhap_sai", 0), "bai_giai_hom_nay": bai, "tong_nguoi_dung": tong}


def bieu_do(so_ngay: int = 14) -> list[dict]:
    hom_nay = date.today()
    ngay = [hom_nay - timedelta(days=k) for k in range(so_ngay - 1, -1, -1)]
    kq = {d.isoformat(): {"ngay": d.isoformat(), "dang_nhap": 0, "dang_ky": 0} for d in ngay}
    with db.ket_noi() as con:
        for r in con.execute("SELECT ts, event FROM activity_log WHERE event IN ('dang_nhap', 'dang_ky') AND ts >= ?",
                             (_ngay_dau(ngay[0]),)):
            k = date.fromtimestamp(r["ts"]).isoformat()
            if k in kq:
                kq[k][r["event"]] += 1
    return list(kq.values())


def so_moi_cho_admin(u: NguoiDung) -> int:
    """Số lượt đăng ký/đăng nhập/yêu cầu bật key Claude riêng của NGƯỜI KHÁC kể từ lần admin xem nhật ký gần nhất (chấm thông báo)."""
    with db.ket_noi() as con:
        return con.execute("SELECT COUNT(*) FROM activity_log WHERE id > ? AND event IN ('dang_ky', 'dang_nhap', 'yeu_cau_key_claude_rieng') "
                           "AND (user_id IS NULL OR user_id <> ?)", (u.da_xem_nhat_ky, u.id)).fetchone()[0]


# ---------------------------------------------------------------- thao tác của admin (web + admin_cli.py)
class LoiQuanTri(Exception):
    pass


def khoa_tai_khoan(muc_tieu: NguoiDung, khoa: bool, nguoi_lam: NguoiDung | None = None, request=None,
                   phuong_thuc: str = "admin_web", ip: str | None = None) -> NguoiDung:
    """Khoá / mở khoá một tài khoản và ghi nhật ký (ai làm, lúc nào, từ đâu). Không khoá được admin hay chính mình."""
    if muc_tieu.cuc_bo:
        raise LoiQuanTri("Không khoá được tài khoản chủ app của chế độ cục bộ.")
    if nguoi_lam is not None and muc_tieu.id == nguoi_lam.id:
        raise LoiQuanTri("Không tự khoá tài khoản của chính mình.")
    if khoa and tai_khoan.la_chu_app(muc_tieu):
        raise LoiQuanTri("Không khoá được tài khoản admin – bỏ quyền admin trước (quan-tri.command → Bỏ quyền admin).")
    db.dat_khoa(muc_tieu.id, khoa)
    if khoa:
        with _khoa_online:
            _lan_ghi_online.pop((str(db.DUONG_DAN), muc_tieu.id), None)
        with db.ket_noi() as con:
            con.execute("UPDATE users SET last_seen = NULL WHERE id = ?", (muc_tieu.id,))
    ghi("khoa" if khoa else "mo_khoa", request, nguoi_lam, doi_tuong=muc_tieu.id, phuong_thuc=phuong_thuc,
        email="" if nguoi_lam else "(máy chủ)", ten="" if nguoi_lam else "admin (dòng lệnh)", ip=ip,
        ua=None if request is not None else "",
        chi_tiet=f"{'khoá' if khoa else 'mở khoá'} #{muc_tieu.id} {muc_tieu.email or muc_tieu.ten}")
    return db.lay_nguoi_dung(muc_tieu.id)
