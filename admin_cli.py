#!/usr/bin/env python3
"""Công cụ dòng lệnh cho admin CHEMISTRY: chạy trên máy chứa app, đọc thẳng CSDL (data/app.db), không cần đăng nhập web.

    python3 admin_cli.py --help        (tự chuyển sang Python của .venv nếu cần)
Không bao giờ in mật khẩu, mã xác thực hay key.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

GOC = Path(__file__).resolve().parent
_VENV = GOC / ".venv"
if (_VENV / "bin" / "python").exists() and Path(sys.prefix).resolve() != _VENV.resolve() and not os.environ.get("CHEMISTRY_CLI_VENV"):
    os.environ["CHEMISTRY_CLI_VENV"] = "1"  # chạy lại bằng .venv/bin/python (có rich + thư viện của app)
    os.execv(str(_VENV / "bin" / "python"), [str(_VENV / "bin" / "python"), str(Path(__file__).resolve()), *sys.argv[1:]])
sys.path.insert(0, str(GOC))

import argparse  # noqa: E402
import time  # noqa: E402
from datetime import date, timedelta  # noqa: E402

try:
    from rich import box  # noqa: E402
    from rich.console import Console  # noqa: E402
    from rich.table import Table  # noqa: E402
    from rich.text import Text  # noqa: E402
except ImportError:  # pragma: no cover
    print("Thiếu thư viện rich – chạy: .venv/bin/pip install rich", file=sys.stderr)
    sys.exit(1)

from backend import chi_phi_ai, claude_admin, claude_rieng, db, dia_chi_ip, email_rac, key_claude, ma_claude, ma_giao_dien, nhat_ky, tai_khoan  # noqa: E402

con_sole = Console(highlight=False)

MAU = {"dang_nhap": "green", "dang_ky": "yellow", "dang_nhap_sai": "red", "xac_thuc_sai": "red", "dang_xuat": "cyan",
       "xac_thuc_email": "bright_green", "dat_lai_mat_khau": "magenta", "doi_ten": "blue", "khoa": "bold red", "mo_khoa": "bold green",
       "doi_nhan": "blue"}
CO = {"tai_khoan_moi": ("mới", "yellow"), "sai_nhieu": ("sai nhiều", "red"), "nhieu_tai_khoan_ip": ("nhiều TK/IP", "magenta"),
      "vi_tri_la": ("vị trí lạ", "bold red")}
LOAI = ", ".join(nhat_ky.NHOM)
XAC_THUC = {"-": "—", "roi": "rồi", "mien": "miễn (admin / kì cựu)", "chua": "CHƯA"}


def _gio(ts: float | None) -> str:
    if not ts:
        return "-"
    t = time.localtime(ts)
    return time.strftime("%H:%M:%S" if date.fromtimestamp(ts) == date.today() else "%d/%m %H:%M", t)


def _ngay(s: str) -> str:
    """'2026-10-11 09:49:25' → '11/10 09:49' (thêm năm nếu khác năm nay)."""
    if not s or len(s) < 16 or s[4] != "-":
        return s or "-"
    nam = "" if s[:4] == time.strftime("%Y") else f"/{s[2:4]}"
    return f"{s[8:10]}/{s[5:7]}{nam} {s[11:16]}"


def _chu_su_kien(d: dict) -> Text:
    return Text(d["ten_su_kien"], style=MAU.get(d["su_kien"], ""))


def _co(d: dict) -> Text:
    t = Text()
    for c in d.get("co", []):
        nhan, mau = CO[c]
        t.append(f"[{nhan}] ", style=mau)
    if d.get("vi_tri_la"):
        x = d["vi_tri_la"]
        t.append(f"{'quốc gia' if x['loai'] == 'quoc_gia' else 'tỉnh/thành'} khác thường lệ: {x['noi']} (thường: {x['thuong_le']}) ",
                 style="bold red")
    return t


def _ip(info: dict | None, ip: str) -> str:
    """'113.161.1.2 · Hà Nội · VNPT' (vị trí + nhà mạng tra offline); không có IP → '-'."""
    return (info or {}).get("mo_ta") or ip or "-"


_KHONG_XUONG_DONG = {"ID", "Giờ", "Lúc", "Ngày tạo", "Đăng nhập cuối", "Hoạt động cuối", "Bài", "Số TK", "Số lần", "Lần cuối"}


def _bang(tieu_de: str, *cot: str) -> Table:
    b = Table(title=tieu_de, box=box.SIMPLE_HEAVY, title_style="bold", header_style="bold", show_lines=False, expand=False,
              pad_edge=False)
    for c in cot:
        b.add_column(c, overflow="fold", no_wrap=c in _KHONG_XUONG_DONG)
    return b


def _bang_su_kien(ds: list[dict], tieu_de: str) -> Table:
    b = _bang(tieu_de, "Giờ", "Sự kiện", "Tên", "Email", "Cách", "IP", "Thiết bị", "Ghi chú")
    for d in ds:
        b.add_row(_gio(d["ts"]), _chu_su_kien(d), d["ten"] or "-", d["email"] or "-", d["ten_phuong_thuc"] or "-",
                  _ip(d.get("ip_info"), d["ip"]), d["thiet_bi"], Text.assemble(_co(d), d["chi_tiet"]))
    return b


def _mot_dong(d: dict) -> Text:
    """Một dòng cho lệnh live: giờ, sự kiện, tên, email, IP, thiết bị."""
    mau = MAU.get(d["su_kien"], "")
    t = Text(f"{time.strftime('%H:%M:%S', time.localtime(d['ts']))}  ", style="dim")
    t.append(f"{d['ten_su_kien']:<17}", style=mau)
    t.append(f"{(d['ten'] or '-')[:24]:<25}", style=mau if d["su_kien"] != "dang_xuat" else "")
    t.append(f"{(d['email'] or '-')[:34]:<35}")
    t.append(f"{_ip(d.get('ip_info'), d['ip']):<34} ", style="dim")
    t.append(d["thiet_bi"], style="dim")
    if d["chi_tiet"]:
        t.append(f"  ({d['chi_tiet']})", style="dim italic")
    if d.get("co"):
        t.append("  ")
        t.append_text(_co(d))
    return t


# ---------------------------------------------------------------- lệnh
def lenh_live(a) -> int:
    cuoi = nhat_ky.id_moi_nhat()
    if a.truoc:
        for d in reversed(nhat_ky.danh_sach(gioi_han=a.truoc)):
            con_sole.print(_mot_dong(d), style="dim")
    con_sole.print(Text.assemble(("● Đang theo dõi trực tiếp ", "bold green"), (str(db.DUONG_DAN), "dim"),
                                 ("   xanh = đăng nhập · vàng = tài khoản mới · đỏ = đăng nhập sai   Ctrl+C để thoát", "")))
    vong = 0
    try:
        while a.so_vong is None or vong < a.so_vong:
            for d in reversed(nhat_ky.danh_sach(sau_id=cuoi, gioi_han=200)):
                cuoi = max(cuoi, d["id"])
                if not a.loai or d["su_kien"] in nhat_ky.cac_su_kien(a.loai):
                    con_sole.print(_mot_dong(d))
            vong += 1
            if a.so_vong is None or vong < a.so_vong:
                time.sleep(a.chu_ky)
    except KeyboardInterrupt:
        con_sole.print("\nĐã thoát.", style="dim")
    return 0


def lenh_online(_a) -> int:
    ds = nhat_ky.online()
    b = _bang(f"Đang online (hoạt động trong {nhat_ky.ONLINE_GIAY // 60} phút gần nhất): {len(ds)} người",
              "ID", "Tên", "Email", "Vai trò", "Hoạt động cuối", "IP", "Thiết bị")
    for d in ds:
        b.add_row(str(d["id"]), d["ten"] or "-", d["email"] or "-", d["vai_tro"], _gio(d["hoat_dong_cuoi"]), _ip(d.get("ip_info"), d["ip"]),
                  d["thiet_bi"])
    con_sole.print(b if ds else Text("Không có ai online trong 5 phút gần nhất.", style="dim"))
    return 0


def lenh_log(a) -> int:
    nguoi = ""
    if a.user:
        u = nhat_ky.tim_nguoi_dung(a.user)
        nguoi = f"#{u.id}" if u else a.user  # không có tài khoản (vd đăng nhập sai bằng email lạ) → lọc theo chữ
    ngay = "hom_nay" if a.today else (a.date or "")
    if a.type and not nhat_ky.cac_su_kien(a.type):
        con_sole.print(f"Loại không hợp lệ: {a.type}. Dùng một trong: {LOAI}", style="red")
        return 2
    ds = nhat_ky.danh_sach(nguoi=nguoi, loai=a.type or "", ngay=ngay, gioi_han=a.limit)
    loc = ", ".join(x for x in (a.user and f"người dùng {a.user}", a.type and f"loại {a.type}", ngay and f"ngày {ngay}") if x)
    con_sole.print(_bang_su_kien(ds, f"Nhật ký{f' ({loc})' if loc else ''} – {len(ds)} dòng, mới nhất ở trên") if ds
                   else Text("Không có dòng nhật ký nào khớp bộ lọc.", style="dim"))
    return 0


def lenh_users(_a) -> int:
    ds = nhat_ky.nguoi_dung_bang()
    b = _bang(f"Người dùng: {len(ds)}", "ID", "Tên", "Email", "Vai trò", "Đăng nhập", "Xác thực", "Ngày tạo",
              "Đăng nhập cuối", "IP gần nhất", "Bài", "Trạng thái")
    for d in ds:
        xt = {"-": Text("—", style="dim"), "roi": Text("✓", style="green"), "mien": Text("miễn", style="dim"),
              "chua": Text("✗ chưa", style="yellow")}[d["xac_thuc"]]
        tt = Text("KHOÁ", style="bold red") if d["bi_khoa"] else (Text("● online", style="green") if d["online"] else Text("—", style="dim"))
        b.add_row(str(d["id"]), d["ten"] or "-", d["email"] or "-", Text(d["vai_tro"], style="bold" if d["la_admin"] else ""),
                  {"email": "mật khẩu", "google": "Google", "cuc_bo": "máy chủ"}.get(d["dang_nhap_bang"], d["dang_nhap_bang"]),
                  xt, _ngay(d["tao_luc"]), _ngay(d["dang_nhap_cuoi"]), _ip(d.get("ip_cuoi_info"), d.get("ip_cuoi", "")),
                  str(d["so_bai_giai"]), tt)
    con_sole.print(b)
    return 0


def _tim(chuoi: str):
    u = nhat_ky.tim_nguoi_dung(chuoi)
    if u is None:
        con_sole.print(f"Không có người dùng nào với email/ID “{chuoi}”.", style="red")
    return u


def lenh_user(a) -> int:
    u = _tim(a.email)
    if u is None:
        return 1
    ct = nhat_ky.chi_tiet(u.id)
    con_sole.print(Text.assemble((f"#{ct['id']} {ct['ten']}", "bold"), f"  <{ct['email'] or '-'}>  ", (ct["vai_tro"], "bold cyan"),
                                 ("  KHOÁ" if ct["bi_khoa"] else "", "bold red")))
    con_sole.print(f"Đăng nhập bằng: {ct['dang_nhap_bang']} · xác thực email: {XAC_THUC[ct['xac_thuc']]} · "
                   f"tạo lúc {ct['tao_luc']} (IP {_ip(ct.get('ip_dang_ky_info'), ct['ip_dang_ky'])}) · hoạt động cuối {ct['hoat_dong_cuoi'] or '-'}"
                   f" (IP {_ip(ct.get('ip_cuoi_info'), ct.get('ip_cuoi', ''))})")
    bg = ct["bai_giai"]
    con_sole.print(f"Bài đã giải: [bold]{bg['xong']}[/] (lỗi {bg['loi']}, tổng {bg['tong']}; bài gần nhất {bg['cuoi'] or '-'}) · "
                   f"lượt dùng thử đã dùng {ct['luot_da_dung']} · key Gemini riêng: {'có' if ct['co_key'] else 'không'}")
    dn = [d for d in ct["lich_su"] if d["su_kien"] in ("dang_nhap", "dang_nhap_sai", "dang_xuat", "dang_ky", "xac_thuc_email",
                                                      "xac_thuc_sai", "dat_lai_mat_khau", "khoa", "mo_khoa")]
    con_sole.print(_bang_su_kien(dn[:50], "Lịch sử đăng nhập (50 dòng mới nhất)") if dn else Text("Chưa có lịch sử đăng nhập.", style="dim"))
    if ct["doi_ten"]:
        b = _bang("Lịch sử đổi tên", "Lúc", "Tên cũ", "Tên mới", "IP")
        for x in ct["doi_ten"]:
            b.add_row(_ngay(nhat_ky.gio(x["ts"])), x["ten_cu"], x["ten_moi"], _ip(x.get("ip_info"), x["ip"]))
        con_sole.print(b)
    else:
        con_sole.print("Chưa đổi tên lần nào.", style="dim")
    return 0


def _khoa(a, khoa: bool) -> int:
    u = _tim(a.email)
    if u is None:
        return 1
    if u.bi_khoa == khoa:
        con_sole.print(f"Tài khoản #{u.id} {u.email} đã {'bị khoá' if khoa else 'mở'} sẵn rồi.", style="yellow")
        return 0
    viec = "KHOÁ" if khoa else "MỞ KHOÁ"
    if not a.yes:
        try:
            tl = input(f"{viec} tài khoản #{u.id} {u.ten} <{u.email}>? (y/n): ").strip().lower()
        except EOFError:
            tl = ""
        if tl not in ("y", "yes", "c", "có", "co"):
            con_sole.print("Đã huỷ, không thay đổi gì.", style="dim")
            return 1
    try:
        nhat_ky.khoa_tai_khoan(u, khoa, None, None, "cli", ip="máy chủ")
    except nhat_ky.LoiQuanTri as e:
        con_sole.print(f"LỖI: {e}", style="red")
        return 1
    con_sole.print(f"Đã {viec.lower()} tài khoản #{u.id} {u.email}."
                   + (" Người này bị đăng xuất ngay và không đăng nhập lại được." if khoa else " Người này đăng nhập lại được."),
                   style="bold red" if khoa else "bold green")
    return 0


def lenh_suspicious(_a) -> int:
    cb = nhat_ky.canh_bao()
    if not cb["tong"]:
        con_sole.print("✓ Không có dấu hiệu đáng ngờ.", style="green")
        return 0
    if cb["nhieu_tai_khoan_cung_ip"]:
        b = _bang(f"Nhiều tài khoản cùng một IP (≥ {nhat_ky.TK_CUNG_IP} tài khoản trong 7 ngày)", "IP", "Số TK", "Lần cuối", "Tài khoản")
        for x in cb["nhieu_tai_khoan_cung_ip"]:
            b.add_row(_ip(x.get("ip_info"), x["ip"]), str(x["so_tai_khoan"]), _ngay(x["lan_cuoi"]),
                      ", ".join(f"#{t['id']} {t['email']}" for t in x["tai_khoan"]))
        con_sole.print(b, style="magenta")
    if cb["dang_nhap_sai_nhieu"] or cb["dang_nhap_sai_theo_ip"]:
        b = _bang("Đăng nhập sai nhiều lần (24 giờ)", "Email / IP", "Số lần", "Lần cuối", "Từ IP")
        for x in cb["dang_nhap_sai_nhieu"]:
            b.add_row(x["email"], str(x["so_lan"]), _ngay(x["lan_cuoi"]), ", ".join(x["ips"]))
        for x in cb["dang_nhap_sai_theo_ip"]:
            b.add_row(f"IP {x['ip']} ({x['so_email']} email)", str(x["so_lan"]), _ngay(x["lan_cuoi"]), _ip(x.get("ip_info"), x["ip"]))
        con_sole.print(b, style="red")
    if cb["tao_hang_loat"]:
        b = _bang("Tài khoản mới tạo hàng loạt", "Khi / IP", "Số đăng ký", "Email")
        for x in cb["tao_hang_loat"]:
            khi = f"IP {x['ip']} trong {x['trong']}" if "ip" in x else f"{x['tu'][5:16]} → {x['den'][11:16]} ({', '.join(x['ips'])})"
            b.add_row(khi, str(x["so_dang_ky"]), ", ".join(x["emails"]))
        con_sole.print(b, style="yellow")
    if cb.get("vi_tri_la"):
        b = _bang("Đăng nhập từ quốc gia / tỉnh-thành khác thường lệ (7 ngày; vị trí IP chỉ gần đúng)", "Lúc", "Người dùng", "Từ",
                  "Thường lệ", "IP", "Thiết bị")
        for x in cb["vi_tri_la"]:
            b.add_row(_ngay(x["lan"]), f"#{x['user_id']} {x['email'] or x['ten']}", Text(x["noi"], style="bold"), x["thuong_le"],
                      _ip(x.get("ip_info"), x["ip"]), x["thiet_bi"])
        con_sole.print(b, style="red")
    return 0


def lenh_ip(a) -> int:
    ip = a.ip.strip()
    if not dia_chi_ip.chuan_ip(ip):
        con_sole.print(f"“{ip}” không phải địa chỉ IP hợp lệ.", style="red")
        return 2
    d = nhat_ky.theo_ip(ip)
    con_sole.print(Text.assemble(("IP ", "bold"), (_ip(d["ip_info"], d["ip"]), "bold cyan")))
    if d["tai_khoan"]:
        b = _bang(f"Tài khoản từng dùng IP này: {len(d['tai_khoan'])}", "ID", "Tên", "Email", "Vai trò", "Đăng nhập", "Lần đầu", "Lần cuối",
                  "Ghi chú")
        for x in d["tai_khoan"]:
            b.add_row(str(x["id"]), x["ten"] or "-", x["email"] or "-", x["vai_tro"], str(x["so_lan_dang_nhap"]), _ngay(x["lan_dau"]),
                      _ngay(x["lan_cuoi"]), Text.assemble(("KHOÁ " if x["bi_khoa"] else "", "bold red"), x["ghi_chu"]))
        con_sole.print(b)
    else:
        con_sole.print("Chưa có tài khoản nào đăng nhập từ IP này.", style="dim")
    if d["dang_nhap_sai"]:
        b = _bang("Đăng nhập sai từ IP này", "Email", "Số lần", "Lần cuối")
        for x in d["dang_nhap_sai"]:
            b.add_row(x["email"] or "-", str(x["so_lan"]), _ngay(x["lan_cuoi"]))
        con_sole.print(b, style="red")
    if d["gan_day"]:
        con_sole.print(_bang_su_kien(d["gan_day"][:20], "Hoạt động gần đây của IP (20 dòng)"))
    if nguon := dia_chi_ip.nguon_du_lieu():
        con_sole.print(nguon, style="dim")
    return 0


# ---------------------------------------------------------------- mã kích hoạt Claude
def _ghi_cli(su_kien: str, chi_tiet: str) -> None:
    nhat_ky.ghi(su_kien, None, None, email="(máy chủ)", ten="admin (dòng lệnh)", phuong_thuc="cli", ip="máy chủ", ua="",
                chi_tiet=chi_tiet)


def _bang_ma(ds: list[dict], tieu_de: str) -> Table:
    b = _bang(tieu_de, "ID", "Mã", "Lượt (dùng/tổng)", "Còn", "Hết hạn", "Tài khoản", "Trạng thái", "Ghi chú")
    mau = {"con_hieu_luc": "green", "het_han": "yellow", "het_luot": "yellow", "thu_hoi": "red"}
    for x in ds:
        tk = ", ".join(f"#{n['id']} {n['email'] or n['ten']} ({n['so_bai']} bài)" for n in x["nguoi_dung"]) or "-"
        b.add_row(str(x["id"]), x["goi_y"], f"{x['da_dung']}/{x['tong_luot']}" + (f" (+{x['dang_giu']} đang giữ)" if x["dang_giu"] else ""),
                  str(x["con_lai"]), x["het_han"], f"{len(x['nguoi_dung'])}/{x['so_tai_khoan']}: {tk}",
                  Text(x["ten_trang_thai"], style=mau.get(x["trang_thai"], "")), x["ghi_chu"] or "-")
    return b


def lenh_code(a) -> int:
    try:
        if a.viec == "create":
            het_han = a.het_han or (date.today() + timedelta(days=30)).isoformat()
            ma, ma_id = ma_claude.tao_ma(a.luot, het_han, a.tai_khoan, a.ghi_chu or "")
            _ghi_cli("tao_ma_claude", f"#{ma_id} …{ma[-4:]}: {a.luot} lượt, hết hạn {het_han}, {a.tai_khoan} tài khoản"
                     + (f" – {a.ghi_chu[:60]}" if a.ghi_chu else ""))
            con_sole.print(Text.assemble(("Mã Claude mới: ", "bold"), (ma, "bold reverse green")))
            con_sole.print(f"#{ma_id} · {a.luot} lượt · hết hạn {het_han} (dùng được hết ngày này) · {a.tai_khoan} tài khoản"
                           + (f" · {a.ghi_chu}" if a.ghi_chu else ""))
            con_sole.print("⚠ Mã chỉ hiện ĐÚNG MỘT LẦN – hãy chép gửi người dùng ngay (CSDL chỉ lưu bản băm).", style="yellow")
            return 0
        if a.viec == "list":
            ds = ma_claude.danh_sach()
            con_sole.print(_bang_ma(ds, f"Mã Claude: {len(ds)}") if ds else Text("Chưa có mã Claude nào.", style="dim"))
            return 0
        x = ma_claude.tim(a.ma)
        if x is None:
            con_sole.print(f"Không có mã Claude “{a.ma}” (dùng ID trong `code list`, hoặc mã gốc).", style="red")
            return 1
        if a.viec == "revoke":
            if x["trang_thai"] == "thu_hoi":
                con_sole.print(f"Mã #{x['id']} đã bị thu hồi từ {x['thu_hoi_luc']}.", style="dim")
                return 0
            if not a.yes:
                tl = input(f"THU HỒI mã #{x['id']} {x['goi_y']} ({x['ghi_chu'] or 'không ghi chú'}; {len(x['nguoi_dung'])} tài khoản đang dùng)? (y/n): ")
                if tl.strip().lower() not in ("y", "yes", "c", "co", "có"):
                    con_sole.print("Đã huỷ.", style="dim")
                    return 1
            x = ma_claude.thu_hoi(x["id"])
            _ghi_cli("thu_hoi_ma_claude", f"#{x['id']} {x['goi_y']}")
            con_sole.print(f"Đã thu hồi mã #{x['id']} {x['goi_y']} – không ai dùng được Claude bằng mã này nữa.", style="green")
            return 0
        if a.viec == "add":
            x = ma_claude.them_luot(x["id"], a.so)
            _ghi_cli("them_luot_ma_claude", f"#{x['id']} {x['goi_y']}: +{a.so} lượt")
            con_sole.print(f"Đã cộng {a.so} lượt cho mã #{x['id']} {x['goi_y']}: còn {x['con_lai']}/{x['tong_luot']} lượt.", style="green")
            return 0
    except ma_claude.LoiMaClaude as e:
        con_sole.print(f"LỖI: {e}", style="red")
        return 1
    return 2


# ---------------------------------------------------------------- Key Claude dùng chung (KHÔNG BAO GIỜ in key)
def lenh_key(a) -> int:
    if a.viec == "list":
        ch = key_claude.tom_tat()
        con_sole.print(f"Key Claude: {'BẬT' if ch['bat'] else 'TẮT (chưa đặt CLAUDE_ACCESS_KEY trong .env)'} · tối đa "
                       f"{ch['gioi_han_ngay']} bài Claude/người/ngày", style="bold" if ch["bat"] else "yellow")
        ds = key_claude.danh_sach()
        if not ds:
            con_sole.print("Chưa ai mở Claude bằng key / nhập sai key.", style="dim")
            return 0
        b = _bang(f"Key Claude: {sum(x['trang_thai'] == 'da_mo' for x in ds)} người đang mở Claude", "ID", "Tên", "Email", "Trạng thái",
                  "Kích hoạt lúc", "Hôm nay", "Tổng bài", "Nhập sai (24h / 30 ngày)", "Khoá nhập")
        mau = {"da_mo": "green", "key_cu": "yellow", "bi_thu_hoi": "red", "tat": "yellow", "chua_mo": "dim"}
        for x in ds:
            b.add_row(str(x["id"]), x["ten"] or "-", x["email"], Text(x["ten_trang_thai"], style=mau.get(x["trang_thai"], "")),
                      x["kich_hoat_luc"] or "-", f"{x['hom_nay']}/{x['gioi_han']}", str(x["tong"]),
                      f"{x['sai_24h']} / {x['sai_30_ngay']}" if x["sai_30_ngay"] else "-",
                      f"còn {x['khoa_nhap_giay'] // 60 + 1} phút" if x["khoa_nhap_giay"] else "-")
        con_sole.print(b)
        return 0
    u = _tim(a.nguoi)
    if u is None:
        return 1
    if tai_khoan.la_chu_app(u):
        con_sole.print("Admin luôn dùng được Claude – không thu hồi được.", style="yellow")
        return 1
    if a.viec == "revoke":
        if not a.yes:
            tl = input(f"THU HỒI quyền Claude (Key Claude) của #{u.id} {u.email}? Người này không tự kích hoạt lại được. (y/n): ")
            if tl.strip().lower() not in ("y", "yes", "c", "co", "có"):
                con_sole.print("Đã huỷ.", style="dim")
                return 1
        if key_claude.thu_hoi(u.id):
            nhat_ky.ghi("thu_hoi_key_claude", None, None, email="(máy chủ)", ten="admin (dòng lệnh)", phuong_thuc="cli", ip="máy chủ",
                        ua="", doi_tuong=u.id, chi_tiet=f"#{u.id} {u.email}")
            con_sole.print(f"Đã thu hồi quyền Claude của #{u.id} {u.email}.", style="green")
        else:
            con_sole.print(f"#{u.id} {u.email} đã bị thu hồi từ trước.", style="dim")
        return 0
    if a.viec == "allow":
        if key_claude.mo_lai(u.id):
            nhat_ky.ghi("mo_lai_key_claude", None, None, email="(máy chủ)", ten="admin (dòng lệnh)", phuong_thuc="cli", ip="máy chủ",
                        ua="", doi_tuong=u.id, chi_tiet=f"#{u.id} {u.email}")
            con_sole.print(f"Đã mở lại: #{u.id} {u.email} nhập lại Key Claude được.", style="green")
        else:
            con_sole.print(f"#{u.id} {u.email} không bị thu hồi.", style="dim")
        return 0
    return 2


# ---------------------------------------------------------------- quyền dùng API key Claude RIÊNG của người dùng
def lenh_ownkey(a) -> int:
    if a.viec == "list":
        tt = claude_rieng.danh_sach_admin()
        ds = [(u, tt.get(u.id)) for u in db.danh_sach_nguoi_dung()]
        ds = [(u, t) for u, t in ds if t and (t["bat"] or t["yeu_cau_luc"] or t["cuoi"])]
        if not ds:
            con_sole.print("Chưa ai được bật / yêu cầu dùng API key Claude riêng (admin luôn được).", style="dim")
            return 0
        b = _bang("API key Claude riêng", "ID", "Tên", "Email", "Quyền", "Key", "Dùng key của tôi", "Bật lúc / bởi", "Yêu cầu / ghi chú")
        mau = {"loi": "red", "da_nhap": "green", "chua_nhap": "dim"}
        for u, t in sorted(ds, key=lambda x: (not x[1]["yeu_cau_luc"], not x[1]["bat"], x[0].id)):
            quyen = "admin (luôn)" if t["la_admin"] else ("BẬT" if t["bat"] else "tắt")
            ghi = (f"⏳ ĐANG YÊU CẦU từ {t['yeu_cau_luc']}" if t["yeu_cau_luc"] else
                   f"tắt lúc {t['tat_luc']} – xoá key sau {t['xoa_sau_ngay']} ngày" if t["xoa_sau_ngay"] is not None else (t["loi"][:50] or "-"))
            b.add_row(str(u.id), u.ten or "-", u.email or "-", Text(quyen, style="bold green" if t["bat"] else "dim"),
                      Text(t["ten_trang_thai"], style=mau.get(t["trang_thai"], "")), "có" if t["dung"] else "không",
                      f"{t['bat_luc']} {t['bat_boi']}".strip() or "-", Text(ghi, style="bold yellow" if t["yeu_cau_luc"] else ""))
        con_sole.print(b)
        return 0
    u = _tim(a.email)
    if u is None:
        return 1
    if tai_khoan.la_chu_app(u):
        con_sole.print("Admin luôn dùng được API key Claude riêng – không cần bật/tắt.", style="yellow")
        return 1
    bat = a.viec == "on"
    da_yeu_cau = bool((claude_rieng.trang_thai_admin(u.id) or {}).get("yeu_cau_luc"))
    doi = claude_rieng.cap_quyen(u.id, bat)
    if not bat and not doi and da_yeu_cau:  # chưa bật mà đang yêu cầu → "off" = từ chối yêu cầu
        claude_rieng.tu_choi(u.id)
        _ghi_cli("tu_choi_key_claude_rieng", f"#{u.id} {u.email}")
        con_sole.print(f"Đã TỪ CHỐI yêu cầu dùng key Claude riêng của #{u.id} {u.email}.", style="green")
        return 0
    if doi:
        nhat_ky.ghi("bat_key_claude_rieng" if bat else "tat_key_claude_rieng", None, None, email="(máy chủ)", ten="admin (dòng lệnh)",
                    phuong_thuc="cli", ip="máy chủ", ua="", doi_tuong=u.id, chi_tiet=f"#{u.id} {u.email}")
        con_sole.print(f"Đã {'BẬT' if bat else 'TẮT'} API key Claude riêng cho #{u.id} {u.email}."
                       + ("" if bat else f" Key đã lưu bị vô hiệu ngay, xoá hẳn sau {claude_rieng.GIU_KHI_TAT_NGAY} ngày nếu không bật lại."),
                       style="green")
    else:
        con_sole.print(f"#{u.id} {u.email} đã {'được bật' if bat else 'đang tắt'} từ trước.", style="dim")
    return 0


# ---------------------------------------------------------------- email rác (email_rac.py)
def lenh_email(a) -> int:
    v = a.viec
    if v == "check":  # thử một email đúng như lúc đăng ký (có tra MX thật) – không tạo tài khoản, không ghi nhật ký
        kq = email_rac.kiem_tra(a.email)
        diem, ly_do = email_rac.diem_ngau_nhien(a.email)
        con_sole.print(f"{a.email}: " + (f"[bold red]BỊ CHẶN[/] – {kq.ma}: {kq.loi}" if kq.chan else "[bold green]cho qua[/]"))
        con_sole.print(f"  email chuẩn hoá: {db.email_chuan(a.email)} · điểm ngẫu nhiên {diem} (≥ 3 = nghi ngờ) {('(' + ', '.join(ly_do) + ')') if ly_do else ''}"
                       + (" → [yellow]NGHI NGỜ[/]" if kq.nghi_ngo else ""))
        return 1 if kq.chan else 0
    if v == "blocked":
        ds = email_rac.danh_sach_chan(a.n, a.ly_do or "", a.tim or "")
        tk = email_rac.thong_ke_chan(7)
        con_sole.print(f"7 ngày qua: {tk['tong']} lần bị chặn · " + " · ".join(f"{x['ten']} {x['so']}" for x in tk["theo_ly_do"]), style="dim")
        b = _bang("Các lần bị chặn (mới nhất trước)", "Lúc", "Email", "IP", "Lý do", "Chi tiết")
        for x in ds:
            b.add_row(x["luc"], x["email"] or "-", x["ip"] or "-", x["ten_ly_do"], (x["chi_tiet"] or "")[:60])
        con_sole.print(b if ds else Text("Chưa có lần nào bị chặn.", style="dim"))
        return 0
    if v == "suspect":
        ds = email_rac.danh_sach_nghi_ngo(ca_da_duyet=a.all)
        b = _bang("Tài khoản email nghi ngờ", "ID", "Email", "Tên", "Tạo lúc", "Lý do", "Duyệt")
        for x in ds:
            b.add_row(str(x["id"]), x["email"], x["ten"] or "-", x["tao_luc"], x["ly_do"],
                      {"": Text("chờ duyệt", style="bold yellow"), "ok": "cho qua", "khoa": Text("khoá", style="red")}[x["duyet"]])
        con_sole.print(b if ds else Text("Không có tài khoản nào đang chờ duyệt.", style="dim"))
        return 0
    if v == "approve":
        u = _tim(a.nguoi)
        if u is None:
            return 1
        if not email_rac.duyet_nghi_ngo(u.id, None, a.lock):
            con_sole.print(f"#{u.id} {u.email} không bị đánh dấu nghi ngờ.", style="yellow")
            return 1
        _ghi_cli("duyet_nghi_ngo", f"#{u.id} {u.email}: {'khoá' if a.lock else 'cho qua'}")
        con_sole.print(f"Đã {'KHOÁ' if a.lock else 'cho qua'} #{u.id} {u.email}.", style="green")
        return 0
    if v in ("block", "allow"):
        try:
            mau = email_rac.them_quy_tac(a.ten_mien, "chan" if v == "block" else "cho", a.ghi_chu or "")
        except ValueError as e:
            con_sole.print(str(e), style="red")
            return 1
        _ghi_cli("sua_ds_email", f"{'chặn' if v == 'block' else 'cho phép'} {mau}")
        con_sole.print(f"Đã thêm {mau} vào danh sách {'CHẶN' if v == 'block' else 'CHO PHÉP'} (có hiệu lực ngay).", style="green")
        return 0
    if v == "remove":
        if not email_rac.xoa_quy_tac(a.ten_mien):
            con_sole.print(f"{a.ten_mien} không có trong danh sách.", style="yellow")
            return 1
        _ghi_cli("sua_ds_email", f"bỏ {a.ten_mien.strip().lower()}")
        con_sole.print(f"Đã bỏ {a.ten_mien} khỏi danh sách.", style="green")
        return 0
    if v == "rules":
        ds = email_rac.danh_sach_quy_tac()
        b = _bang("Danh sách chặn / cho phép", "Tên miền", "Loại", "Ghi chú", "Thêm bởi", "Lúc")
        for x in ds:
            b.add_row(x["ten_mien"], Text("chặn", style="red") if x["loai"] == "chan" else Text("cho phép", style="green"),
                      x["ghi_chu"] or "-", x["admin"], x["luc"])
        con_sole.print(b if ds else Text("Chưa thêm tên miền nào.", style="dim"))
        tt = email_rac.thong_tin_danh_sach()
        con_sole.print(f"Danh sách email tạm thời: {tt['so_ten_mien']} tên miền · tải lần cuối {tt['cap_nhat_luc'] or '(bản đi kèm)'}", style="dim")
        return 0
    if v == "update":
        try:
            n = email_rac.cap_nhat_danh_sach()
        except email_rac.LoiCapNhat as e:
            con_sole.print(str(e), style="red")
            return 1
        con_sole.print(f"Đã tải danh sách mới: {n} tên miền (app đang chạy tự đọc lại).", style="green")
        return 0
    if v == "purge":
        ds = email_rac.xoa_chua_xac_minh()
        for r in ds:
            nhat_ky.ghi("xoa_chua_xac_minh", None, None, email=r["email"], ten=r["name"], phuong_thuc="cli", ip=r["register_ip"] or "",
                        ua="", chi_tiet=f"#{r['id']} quá 24 giờ chưa xác minh email")
        con_sole.print(f"Đã xoá {len(ds)} tài khoản quá 24 giờ chưa xác minh email.", style="green" if ds else "dim")
        return 0
    return 1


# ---------------------------------------------------------------- mã giao diện (ma_giao_dien.py)
def lenh_theme(a) -> int:
    if a.viec == "create":
        try:
            ma, ma_id = ma_giao_dien.tao_ma(a.theme, a.ma or "", a.luot, a.het_han or "", a.ghi_chu or "")
        except ValueError as e:
            con_sole.print(str(e), style="red")
            return 1
        _ghi_cli("tao_ma_giao_dien", f"#{ma_id} → {ma_giao_dien.THEMES[a.theme]}")
        con_sole.print(Text.assemble("Mã giao diện: ", (ma, "bold green"), f"  → theme {ma_giao_dien.THEMES[a.theme]}"
                                     + (f", {a.luot} lượt" if a.luot else ", không giới hạn lượt") + (f", hạn {a.het_han}" if a.het_han else "")))
        return 0
    if a.viec == "revoke":
        r = ma_giao_dien.tim(a.ma)
        if r is None or not ma_giao_dien.thu_hoi(r["id"]):
            con_sole.print("Không có mã này hoặc đã thu hồi.", style="yellow")
            return 1
        _ghi_cli("thu_hoi_ma_giao_dien", f"#{r['id']}")
        con_sole.print(f"Đã thu hồi mã giao diện #{r['id']}.", style="green")
        return 0
    b = _bang("Mã giao diện", "ID", "Mã", "Theme", "Đã dùng", "Hạn", "Trạng thái", "Ghi chú")
    for x in ma_giao_dien.danh_sach():
        b.add_row(str(x["id"]), x["ma"] or f"{x['goi_y']} (không xem lại được)", x["ten_theme"], f"{x['da_dung']}" + (f"/{x['toi_da']}" if x["toi_da"] else ""), x["het_han"] or "-",
                  Text(x["ten_trang_thai"], style="green" if x["trang_thai"] == "con_hieu_luc" else "red" if x["trang_thai"] == "thu_hoi" else "yellow"),
                  x["ghi_chu"] or "-")
    con_sole.print(b)
    return 0


# ---------------------------------------------------------------- Claude API máy chủ (Admin API Anthropic)
def lenh_claude(a) -> int:
    if a.nap is not None:
        try:
            claude_admin.them_nap(a.nap, a.ghi_chu or "")
        except ValueError as e:
            con_sole.print(f"LỖI: {e}", style="red")
            return 1
        con_sole.print(f"Đã ghi lần nạp ${a.nap:.2f}.", style="green")
    d = claude_admin.bao_cao(lam_moi=a.lam_moi)
    u = lambda v, so=2: "—" if v is None else f"${v:.{so}f}"  # noqa: E731
    if not d["co_admin_key"]:
        con_sole.print("Chưa có ANTHROPIC_ADMIN_KEY – chỉ hiện nhật ký nội bộ. Cách tạo:", style="yellow")
        for i, x in enumerate(d["huong_dan"], 1):
            con_sole.print(f"  {i}. {x}")
    elif d.get("loi"):
        con_sole.print(f"LỖI Admin API: {d['loi']}", style="red")
    else:
        mau = "bold red" if d["canh_bao"] else "bold green"
        con_sole.print(Text.assemble(("Số dư ƯỚC TÍNH: ", "bold"), (u(d["so_du_uoc_tinh"]), mau),
                                     f"  (đã nạp {u(d['tong_nap'])} − đã dùng {u(d['da_dung_tu_lan_nap_dau'])}; cảnh báo dưới {u(d['nguong'])})"))
        if d["canh_bao"]:
            con_sole.print("⚠ Số dư dưới ngưỡng – nạp thêm tín dụng tại platform.claude.com", style="bold red")
        con_sole.print(f"Chi phí (UTC): hôm nay {u(d['hom_nay'], 3)} · 7 ngày {u(d['ngay_7'], 3)} · 30 ngày {u(d['ngay_30'], 3)}"
                       f"   (cập nhật {d['cap_nhat_luc']}, cache 10 phút – thêm --lam-moi để lấy lại)")
        b = _bang("Token theo mô hình (Anthropic, 30 ngày)", "Mô hình", "Token vào", "Đọc cache", "Ghi cache", "Token ra")
        for m in d["mo_hinh"]:
            b.add_row(m["model"], _so(m["tokens_in"]), _so(m["cache_doc"]), _so(m["cache_ghi"]), _so(m["tokens_out"]))
        con_sole.print(b)
    nb = d["noi_bo"]
    b = _bang("Nhật ký nội bộ (Claude bằng key máy chủ, 30 ngày)", "Mô hình", "Lượt gọi", "Lỗi", "Token vào", "Token ra", "Ước tính")
    for r in nb["theo_mo_hinh"]:
        b.add_row(r["model"] or "-", str(r["so_lan"]), str(r["loi"] or 0), _so(r["tokens_in"]), _so(r["tokens_out"]), u(r["usd"], 4))
    con_sole.print(b if nb["theo_mo_hinh"] else Text("Nhật ký nội bộ: chưa gọi Claude bằng key máy chủ trong 30 ngày.", style="dim"))
    if d["nap"]:
        b = _bang(f"Lịch sử nạp (tổng {u(d['tong_nap'])})", "Lúc", "USD", "Ghi chú", "Admin")
        for n in d["nap"]:
            b.add_row(n["luc"], u(n["usd"]), n["ghi_chu"] or "-", n["admin"])
        con_sole.print(b)
    return 0


# ---------------------------------------------------------------- chi phí AI
def _so(n) -> str:
    return f"{int(n or 0):,}".replace(",", ".")


def lenh_usage(a) -> int:
    so_ngay = 30 if a.month else 7 if a.week else 1
    tk = chi_phi_ai.thong_ke(so_ngay)
    ten = {1: "hôm nay", 7: "7 ngày", 30: "30 ngày"}[so_ngay]
    if tk["theo_mo_hinh"]:
        b = _bang(f"Chi phí AI {ten} (từ {tk['tu'][:16]})", "Mô hình", "Nhà cung cấp", "Lượt gọi", "Lỗi", "Token vào", "Token ra",
                  "(suy nghĩ)", "Nâng cấp", "Key người dùng", "Ước tính USD")
        for r in tk["theo_mo_hinh"]:
            b.add_row(r["model"] or "-", r["provider"], str(r["so_lan"]), str(r["loi"]) if r["loi"] else "-", _so(r["tokens_in"]),
                      _so(r["tokens_out"]), _so(r["tokens_think"]), str(r["nang_cap"]) if r["nang_cap"] else "-",
                      str(r["key_rieng"]) if r["key_rieng"] else "-", f"${r['usd']:.4f}")
        t = tk["tong"]
        b.add_row(Text("TỔNG", style="bold"), "", str(t["so_lan"]), "", _so(t["tokens_in"]), _so(t["tokens_out"]), "", "", "",
                  Text(f"${t['usd']:.4f}", style="bold"))
        con_sole.print(b)
    else:
        con_sole.print(f"Chưa có lượt gọi AI nào {ten}.", style="dim")
    if tk["claude_nhieu_nhat"]:
        b = _bang("Dùng Claude nhiều nhất", "ID", "Tên", "Email", "Lượt gọi", "Bài", "Token vào", "Token ra")
        for r in tk["claude_nhieu_nhat"]:
            b.add_row(str(r["user_id"] or "-"), r["ten"] or "-", r["email"] or "-", str(r["so_lan"]), str(r["so_bai"]),
                      _so(r["tokens_in"]), _so(r["tokens_out"]))
        con_sole.print(b)
    b = _bang(f"Mức độ đề {ten}", "Mức", "Số bài", "Tỉ lệ")
    for r in tk["ti_le_muc_do"]:
        b.add_row(r["ten"], str(r["so_bai"]), f"{r['phan_tram']}%")
    if tk["bai_chua_phan_loai"]:
        b.add_row("chưa phân loại (bài cũ / tự chọn mức)", str(tk["bai_chua_phan_loai"]), "")
    con_sole.print(b)
    if tk["theo_muc_thong_minh"]:
        b = _bang(f"Thời gian & token mỗi bài theo mức độ thông minh {ten}", "Mức", "Số bài", "Thời gian TB", "Token TB/bài", "Lần gọi AI TB", "Từ cache")
        for r in tk["theo_muc_thong_minh"]:
            b.add_row(r["ten"], str(r["so_bai"]), f"{r['tb_giay']} s", _so(r["tb_token"]), str(r["tb_lan_goi"]), str(r["tu_cache"] or "-"))
        con_sole.print(b)
    t = tk["tra_cuu_tai_lieu"]
    if t["so_bai_giai"]:
        con_sole.print(f"Tra cứu tài liệu khoa học: {t['so_bai_giai']} bài giải có tra cứu · TB {t['tb_so_bai_bao']} bài báo/lần · "
                       f"~{_so(t['tong_token'])} token phần tài liệu")
    con_sole.print(tk["ghi_chu"], style="dim")
    return 0


# ---------------------------------------------------------------- tham số (hướng dẫn tiếng Việt)
class _DinhDang(argparse.RawDescriptionHelpFormatter):
    def add_usage(self, usage, actions, groups, prefix=None):
        return super().add_usage(usage, actions, groups, prefix="Cách dùng: " if prefix is None else prefix)


def _parser(**kw) -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(formatter_class=_DinhDang, add_help=False, **kw)
    p._positionals.title, p._optionals.title = "Tham số", "Tuỳ chọn"
    p.add_argument("-h", "--help", action="help", help="hiện hướng dẫn này rồi thoát")
    return p


def tao_parser() -> argparse.ArgumentParser:
    ap = _parser(prog="admin_cli.py", description="Công cụ cho admin CHEMISTRY – đọc thẳng cơ sở dữ liệu trên máy chạy app (không cần đăng nhập web).",
                 epilog="Ví dụ:\n"
                        "  python3 admin_cli.py live                       theo dõi trực tiếp (Ctrl+C để thoát)\n"
                        "  python3 admin_cli.py online                     ai đang online\n"
                        "  python3 admin_cli.py log --type fail --today    đăng nhập sai hôm nay\n"
                        "  python3 admin_cli.py log --user ban@gmail.com   nhật ký của một người\n"
                        "  python3 admin_cli.py users                      bảng toàn bộ người dùng\n"
                        "  python3 admin_cli.py user ban@gmail.com         chi tiết một người\n"
                        "  python3 admin_cli.py lock ban@gmail.com         khoá (hỏi y/n trước)\n"
                        "  python3 admin_cli.py suspicious                 cảnh báo đáng ngờ\n"
                        "  python3 admin_cli.py ip 113.161.1.2             mọi tài khoản từng dùng một IP\n"
                        "  python3 admin_cli.py code create --luot 20 --ghi-chu \"cho Lan\"   tạo mã Claude\n"
                        "  python3 admin_cli.py code list                  các mã Claude\n"
                        "  python3 admin_cli.py usage --week               chi phí AI 7 ngày\n"
                        "Xem hướng dẫn từng lệnh: python3 admin_cli.py <lệnh> --help")
    ap.add_argument("--db", help="đường dẫn CSDL khác (mặc định data/app.db)")
    sub = ap.add_subparsers(dest="lenh", title="Lệnh", metavar="LỆNH", parser_class=_parser)

    p = sub.add_parser("live", help="theo dõi trực tiếp: in ngay mỗi lượt đăng ký / đăng nhập / đăng xuất / đăng nhập sai",
                       description="In ngay một dòng (giờ, sự kiện, tên, email, IP, thiết bị) mỗi khi có người đăng ký, đăng nhập, "
                                   "đăng xuất hay đăng nhập sai. Màu: xanh = đăng nhập thành công, vàng = tài khoản mới, đỏ = đăng nhập sai. "
                                   "Nhấn Ctrl+C để thoát.")
    p.add_argument("--truoc", type=int, default=0, metavar="N", help="in trước N sự kiện gần nhất cho dễ theo dõi (mặc định 0)")
    p.add_argument("--loai", default="", metavar="LOẠI", help=f"chỉ in một loại: {LOAI}")
    p.add_argument("--chu-ky", type=float, default=1.0, metavar="GIÂY", help="bao lâu kiểm tra một lần (mặc định 1 giây)")
    p.add_argument("--so-vong", type=int, default=None, help=argparse.SUPPRESS)  # cho test

    sub.add_parser("online", help="danh sách người đang online (hoạt động trong 5 phút gần nhất)",
                   description="Người có hoạt động trên app trong 5 phút gần nhất, kèm IP và thiết bị.")

    p = sub.add_parser("log", help="xem nhật ký hoạt động có lọc", description="Nhật ký hoạt động, mới nhất ở trên.")
    p.add_argument("--user", metavar="EMAIL", help="chỉ một người (email hoặc #ID)")
    p.add_argument("--type", metavar="LOẠI", help=f"loại sự kiện: login (đăng nhập), register (đăng ký), fail (đăng nhập sai) – hoặc: {LOAI}")
    p.add_argument("--today", action="store_true", help="chỉ hôm nay")
    p.add_argument("--date", metavar="YYYY-MM-DD", help="chỉ một ngày")
    p.add_argument("--limit", type=int, default=50, help="số dòng tối đa (mặc định 50)")

    sub.add_parser("users", help="bảng toàn bộ người dùng",
                   description="Tên, email, vai trò, đã xác thực email chưa, ngày tạo, lần đăng nhập cuối, số bài đã giải, trạng thái khoá.")
    p = sub.add_parser("user", help="chi tiết một người: lịch sử đăng nhập, đổi tên, số bài đã giải",
                       description="Chi tiết một người dùng: lịch sử đăng nhập, lịch sử đổi tên, số bài đã giải.")
    p.add_argument("email", metavar="EMAIL", help="email (hoặc #ID) của người dùng")
    for ten, viec in (("lock", "khoá"), ("unlock", "mở khoá")):
        p = sub.add_parser(ten, help=f"{viec} tài khoản (hỏi xác nhận y/n trước)",
                           description=f"{viec.capitalize()} tài khoản. Hỏi xác nhận y/n trước khi làm; thao tác được ghi vào nhật ký."
                                       + (" Người bị khoá bị đăng xuất ngay và không đăng nhập được." if ten == "lock" else ""))
        p.add_argument("email", metavar="EMAIL", help="email (hoặc #ID) của người dùng")
        p.add_argument("-y", "--yes", action="store_true", help="không hỏi lại (dùng trong script)")
    sub.add_parser("suspicious", help="cảnh báo: nhiều tài khoản cùng IP, đăng nhập sai nhiều lần, tạo hàng loạt, vị trí lạ",
                   description="Dấu hiệu đáng ngờ: nhiều tài khoản cùng một IP (7 ngày), đăng nhập sai nhiều lần (24 giờ), "
                               "tài khoản mới tạo hàng loạt, đăng nhập từ quốc gia / tỉnh-thành khác thường lệ (7 ngày).")
    p = sub.add_parser("ip", help="mọi tài khoản từng đăng nhập từ một IP (kèm vị trí, nhà mạng)",
                       description="Vị trí + nhà mạng (tra offline) của một IP, mọi tài khoản từng đăng ký / đăng nhập / dùng app từ IP đó, "
                                   "các email đăng nhập sai từ IP đó và hoạt động gần đây.")
    p.add_argument("ip", metavar="IP", help="địa chỉ IPv4 hoặc IPv6")

    p = sub.add_parser("code", help="mã kích hoạt Claude: create / list / revoke / add",
                       description="Mã kích hoạt Claude (mặc định không ai dùng được Claude, trừ admin). Mã dạng CLD-XXXX-XXXX chỉ hiện "
                                   "đúng một lần lúc tạo; người dùng nhập trong Cài đặt → Mã Claude.")
    csub = p.add_subparsers(dest="viec", title="Việc", metavar="VIỆC", parser_class=_parser, required=True)
    c = csub.add_parser("create", help="tạo mã mới (in mã gốc MỘT lần)", description="Tạo mã kích hoạt Claude mới.")
    c.add_argument("--luot", type=int, default=20, metavar="N", help="số bài được giải bằng Claude (mặc định 20)")
    c.add_argument("--het-han", metavar="YYYY-MM-DD", help="ngày hết hạn – dùng được hết ngày này (mặc định 30 ngày nữa)")
    c.add_argument("--tai-khoan", type=int, default=1, metavar="N", help="số tài khoản được dùng chung (mặc định 1)")
    c.add_argument("--ghi-chu", metavar="CHỮ", help="ghi chú, vd cấp cho ai")
    csub.add_parser("list", help="danh sách mã: còn bao nhiêu lượt, ai đang dùng, hết hạn chưa", description="Danh sách mã Claude.")
    c = csub.add_parser("revoke", help="thu hồi mã (hỏi y/n)", description="Thu hồi mã: không ai dùng được Claude bằng mã này nữa.")
    c.add_argument("ma", metavar="ID|MÃ", help="ID (trong `code list`) hoặc mã gốc")
    c.add_argument("-y", "--yes", action="store_true", help="không hỏi lại")
    c = csub.add_parser("add", help="cộng thêm lượt cho mã", description="Cộng thêm lượt dùng Claude cho một mã.")
    c.add_argument("ma", metavar="ID|MÃ", help="ID (trong `code list`) hoặc mã gốc")
    c.add_argument("so", type=int, metavar="SỐ", help="số lượt cộng thêm")

    p = sub.add_parser("claude-key", aliases=["key"], help="Key Claude dùng chung: list / revoke / allow",
                       description="Key Claude dùng chung (CLAUDE_ACCESS_KEY trong .env – lệnh này KHÔNG BAO GIỜ in key): ai đã mở Claude, "
                                   "kích hoạt lúc nào, số bài Claude hôm nay / tổng, số lần nhập sai; thu hồi / mở lại quyền từng người.")
    ksub = p.add_subparsers(dest="viec", title="Việc", metavar="VIỆC", parser_class=_parser, required=True)
    ksub.add_parser("list", help="ai đã mở Claude bằng key, lượt hôm nay / tổng, số lần nhập sai", description="Danh sách Key Claude.")
    c = ksub.add_parser("revoke", help="thu hồi quyền Claude của một người (hỏi y/n)",
                        description="Thu hồi quyền Claude (Key Claude) của một người: mất quyền ngay, không tự kích hoạt lại được.")
    c.add_argument("nguoi", metavar="EMAIL|ID", help="email hoặc ID người dùng")
    c.add_argument("-y", "--yes", action="store_true", help="không hỏi lại")
    c = ksub.add_parser("allow", help="mở lại: người đã bị thu hồi được nhập lại key", description="Bỏ thu hồi quyền Claude của một người.")
    c.add_argument("nguoi", metavar="EMAIL|ID", help="email hoặc ID người dùng")

    p = sub.add_parser("ownkey", help="quyền dùng API key Claude riêng của người dùng: on / off / list",
                       description="Bật / tắt quyền dùng API key Claude RIÊNG cho từng tài khoản (admin luôn được). Tắt: key đã lưu vô hiệu "
                                   "ngay, giữ mã hoá 30 ngày rồi xoá. `off` với người đang yêu cầu (chưa bật) = từ chối yêu cầu.")
    osub = p.add_subparsers(dest="viec", title="Việc", metavar="VIỆC", parser_class=_parser, required=True)
    for ten, viec in (("on", "BẬT quyền (duyệt yêu cầu)"), ("off", "TẮT quyền / từ chối yêu cầu")):
        c = osub.add_parser(ten, help=viec, description=viec + " dùng API key Claude riêng.")
        c.add_argument("email", metavar="EMAIL|ID", help="email hoặc ID người dùng")
    osub.add_parser("list", help="ai được bật, ai đang yêu cầu, trạng thái key", description="Danh sách quyền dùng API key Claude riêng.")

    p = sub.add_parser("theme", help="mã giao diện (nút +K): create / list / revoke",
                       description="Mã giao diện do admin cấp: người dùng bấm +K trên trang chủ, nhập mã → đổi theme. CSDL chỉ lưu HMAC.")
    tsub = p.add_subparsers(dest="viec", title="Việc", metavar="VIỆC", parser_class=_parser, required=True)
    c = tsub.add_parser("create", help="tạo mã mới", description="Tạo mã giao diện (xem lại bằng `theme list`).")
    c.add_argument("--theme", required=True, choices=[k for k in ma_giao_dien.THEMES if k != "default"], help="theme của mã")
    c.add_argument("--ma", metavar="MÃ", help="mã tự chọn (vd HOAXUAN2027); bỏ trống → sinh ngẫu nhiên GD-XXXX-XXXX")
    c.add_argument("--luot", type=int, metavar="SỐ", help="số lượt tối đa (mỗi tài khoản 1 lượt); bỏ trống = không giới hạn")
    c.add_argument("--het-han", metavar="YYYY-MM-DD", help="hạn dùng (dùng được hết ngày đó)")
    c.add_argument("--ghi-chu", metavar="CHỮ")
    tsub.add_parser("list", help="danh sách mã (hiện mã đầy đủ để đưa cho người khác)", description="Danh sách mã giao diện.")
    c = tsub.add_parser("revoke", help="thu hồi mã", description="Thu hồi một mã giao diện.")
    c.add_argument("ma", metavar="ID|MÃ")

    p = sub.add_parser("email", help="email rác: thử email, các lần bị chặn, nghi ngờ, danh sách chặn / cho phép, cập nhật danh sách",
                       description="Chặn email ảo / tạm thời khi đăng ký (email_rac.py). Tên miền chặn / cho phép có hiệu lực ngay, "
                                   "dùng được dấu * (vd tempmail.*, *.xyz).")
    esub = p.add_subparsers(dest="viec", title="Việc", metavar="VIỆC", parser_class=_parser, required=True)
    c = esub.add_parser("check", help="thử một email như lúc đăng ký (tra MX thật, không tạo tài khoản)", description="Thử kiểm tra một email.")
    c.add_argument("email", metavar="EMAIL")
    c = esub.add_parser("blocked", help="các lần bị chặn (email, IP, lý do, lúc)", description="Nhật ký các lần bị chặn (giữ 90 ngày).")
    c.add_argument("-n", type=int, default=50, help="số dòng (mặc định 50)")
    c.add_argument("--ly-do", metavar="LÝ_DO", help="lọc lý do: " + ", ".join(email_rac.TEN_LY_DO))
    c.add_argument("--tim", metavar="CHỮ", help="tìm theo email / IP")
    c = esub.add_parser("suspect", help="tài khoản email nghi ngờ (chờ duyệt)", description="Tài khoản có tên email trông ngẫu nhiên.")
    c.add_argument("--all", action="store_true", help="hiện cả những người đã duyệt")
    c = esub.add_parser("approve", help="duyệt tài khoản nghi ngờ: cho qua (hoặc --lock để khoá)", description="Duyệt tài khoản nghi ngờ.")
    c.add_argument("nguoi", metavar="EMAIL|ID")
    c.add_argument("--lock", action="store_true", help="khoá tài khoản thay vì cho qua")
    for ten, mo_ta in (("block", "thêm tên miền vào danh sách CHẶN (blacklist)"), ("allow", "thêm tên miền vào danh sách CHO PHÉP (whitelist)")):
        c = esub.add_parser(ten, help=mo_ta, description=mo_ta + " – vd example.com, tempmail.*, *.xyz")
        c.add_argument("ten_mien", metavar="TÊN_MIỀN")
        c.add_argument("--ghi-chu", metavar="CHỮ")
    c = esub.add_parser("remove", help="bỏ một tên miền khỏi danh sách chặn / cho phép", description="Bỏ tên miền khỏi danh sách.")
    c.add_argument("ten_mien", metavar="TÊN_MIỀN")
    esub.add_parser("rules", help="xem danh sách chặn / cho phép", description="Danh sách chặn / cho phép của admin.")
    esub.add_parser("update", help="tải ngay bản mới nhất danh sách email tạm thời (GitHub)", description="Cập nhật danh sách email tạm thời.")
    esub.add_parser("purge", help="xoá ngay tài khoản quá 24 giờ chưa xác minh (app cũng tự làm mỗi giờ)",
                    description="Xoá tài khoản đăng ký mới quá 24 giờ chưa xác minh email.")

    p = sub.add_parser("claude", help="Claude API máy chủ: số dư ước tính, chi phí, token (Admin API Anthropic), ghi lần nạp",
                       description="Chi phí + token Claude của máy chủ từ Admin API của Anthropic (cần ANTHROPIC_ADMIN_KEY), số dư ƯỚC TÍNH = "
                                   "tổng đã nạp − chi phí, so với nhật ký nội bộ của app. Không bao giờ in key.")
    p.add_argument("--lam-moi", action="store_true", help="bỏ qua cache 10 phút, lấy lại từ Anthropic")
    p.add_argument("--nap", type=float, metavar="USD", help="ghi thêm một lần nạp tiền (USD) trước khi in báo cáo")
    p.add_argument("--ghi-chu", metavar="CHỮ", help="ghi chú cho lần nạp")

    p = sub.add_parser("usage", help="chi phí AI: lượt + token theo mô hình, ai dùng Claude nhiều nhất, tỉ lệ đề dễ/TB/HSG",
                       description="Số lượt gọi AI và token theo từng mô hình, ước tính tiền (bảng giá trả phí), ai dùng Claude nhiều nhất, "
                                   "tỉ lệ đề Dễ / Trung bình / HSG. Mặc định: hôm nay.")
    g = p.add_mutually_exclusive_group()
    g.add_argument("--today", action="store_true", help="hôm nay (mặc định)")
    g.add_argument("--week", action="store_true", help="7 ngày gần nhất")
    g.add_argument("--month", action="store_true", help="30 ngày gần nhất")
    return ap


def main(args: list[str] | None = None) -> int:
    ap = tao_parser()
    a = ap.parse_args(args)
    if a.db:
        db.DUONG_DAN = Path(a.db).expanduser().resolve()
    if not db.DUONG_DAN.exists():
        con_sole.print(f"Chưa có cơ sở dữ liệu {db.DUONG_DAN} – app chưa chạy lần nào?", style="red")
        return 1
    if a.lenh is None:
        ap.print_help()
        return 0
    nhat_ky.don_dep_dinh_ky()
    lenh = {"live": lenh_live, "online": lenh_online, "log": lenh_log, "users": lenh_users, "user": lenh_user,
            "lock": lambda x: _khoa(x, True), "unlock": lambda x: _khoa(x, False), "suspicious": lenh_suspicious, "ip": lenh_ip,
            "code": lenh_code, "usage": lenh_usage, "claude-key": lenh_key, "key": lenh_key, "claude": lenh_claude, "ownkey": lenh_ownkey,
            "email": lenh_email, "theme": lenh_theme}[a.lenh]
    return lenh(a)


if __name__ == "__main__":
    sys.exit(main())
