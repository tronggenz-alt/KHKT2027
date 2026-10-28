"""API trang quản trị /admin/api/* – CHỈ admin (kiểm tra ở backend cho MỌI route qua dependency của router).

Chưa đăng nhập → 401 (trang /admin thì chuyển về /dang-nhap – main.py); đăng nhập nhưng không phải admin → 403.
Dùng chung cho mục Cài đặt → Hoạt động và trang /admin. Không trả mật khẩu, mã xác thực hay key ở bất kỳ đâu.
"""
from __future__ import annotations

import asyncio
import json
import re

from fastapi import APIRouter, Body, Depends, HTTPException, Request
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, ConfigDict, Field, ValidationError

from . import chi_phi_ai, claude_admin, claude_rieng, db, dia_chi_ip, email_rac, key_claude, lo_trinh, ma_claude, ma_giao_dien, nhat_ky, phong_thi_nghiem, sinh_hoc_rag, so_do_he_thong, tai_khoan
from .auth import gioi_han, kiem_csrf, nguoi_dung_hien_tai
from .db import NguoiDung


def can_admin(request: Request) -> NguoiDung:
    u = nguoi_dung_hien_tai(request)
    if u is None:
        raise HTTPException(401, {"loi": "Bạn cần đăng nhập.", "ma": "can_dang_nhap"})
    if not tai_khoan.la_chu_app(u):
        raise HTTPException(403, {"loi": "Chỉ admin mới xem được mục này.", "ma": "khong_phai_admin"})
    return u


router = APIRouter(prefix="/admin/api", dependencies=[Depends(can_admin), gioi_han("quan_tri")])


@router.get("/tong-quan")
def tong_quan():
    return {**nhat_ky.tong_quan(), "so_canh_bao": nhat_ky.canh_bao()["tong"], "id_moi_nhat": nhat_ky.id_moi_nhat(),
            "nguon_vi_tri_ip": dia_chi_ip.nguon_du_lieu()}


@router.get("/online")
def online():
    return {"online": nhat_ky.online()}


@router.get("/hoat-dong")
def hoat_dong(nguoi: str = "", loai: str = "", ngay: str = "", truoc_id: int | None = None, gioi_han: int = 50, ip: str = ""):
    return {"su_kien": nhat_ky.danh_sach(nguoi=nguoi[:120], loai=loai[:120], ngay=ngay[:10], truoc_id=truoc_id, gioi_han=gioi_han,
                                         ip=ip[:64]),
            "id_moi_nhat": nhat_ky.id_moi_nhat(), "ten_su_kien": nhat_ky.SU_KIEN, "nguon_vi_tri_ip": dia_chi_ip.nguon_du_lieu()}


@router.get("/ip")
def theo_ip(ip: str = ""):
    """Mọi tài khoản từng đăng nhập từ một IP (bấm vào IP trên trang /admin, Cài đặt → Hoạt động)."""
    ip = ip.strip()[:64]
    if not dia_chi_ip.chuan_ip(ip) and ip != dia_chi_ip.KHONG_XAC_DINH:
        raise HTTPException(400, {"loi": "IP không hợp lệ.", "ma": "ip_sai"})
    return {**nhat_ky.theo_ip(ip), "nguon_vi_tri_ip": dia_chi_ip.nguon_du_lieu()}


@router.get("/luong")
async def luong(request: Request, sau: int | None = None, mot_lan: bool = False):
    """SSE: đẩy ngay mỗi sự kiện mới (kiểm tra CSDL mỗi 1,5 giây). mot_lan=1: trả các sự kiện mới rồi đóng (dự phòng / test)."""
    bat_dau = await asyncio.to_thread(nhat_ky.id_moi_nhat) if sau is None else max(0, sau)

    async def phat():
        cuoi, vong = bat_dau, 0
        yield f"event: chao\ndata: {json.dumps({'id': cuoi})}\n\n"
        while True:
            moi = await asyncio.to_thread(nhat_ky.danh_sach, sau_id=cuoi, gioi_han=200)
            for d in reversed(moi):
                cuoi = max(cuoi, d["id"])
                yield f"data: {json.dumps(d, ensure_ascii=False)}\n\n"
            if mot_lan:
                return
            vong += 1
            if vong % 10 == 0:
                yield ": nhip\n\n"  # giữ kết nối qua proxy
            if vong % 20 == 0:  # bị bỏ quyền admin / đăng xuất giữa chừng → dừng
                u = await asyncio.to_thread(nguoi_dung_hien_tai, request)
                if u is None or not tai_khoan.la_chu_app(u):
                    return
            if await request.is_disconnected():
                return
            await asyncio.sleep(1.5)

    return StreamingResponse(phat(), media_type="text/event-stream", headers={"X-Accel-Buffering": "no"})


@router.get("/nguoi-dung")
def nguoi_dung():
    key_rieng = claude_rieng.danh_sach_admin()
    return {"nguoi_dung": [{**d, "key_claude_rieng": key_rieng.get(d["id"])} for d in nhat_ky.nguoi_dung_bang()],
            "nhan_thanh_vien": nhat_ky.nhan_thanh_vien()}


@router.get("/nguoi-dung/{uid}")
def chi_tiet(uid: int):
    ct = nhat_ky.chi_tiet(uid)
    if ct is None:
        raise HTTPException(404, {"loi": "Không có người dùng này.", "ma": "khong_co"})
    return {**ct, "key_claude_rieng": claude_rieng.trang_thai_admin(uid)}


class KeyRiengVao(BaseModel):
    bat: bool
    tu_choi: bool = False  # bat=false + tu_choi: từ chối yêu cầu đang chờ


@router.post("/nguoi-dung/{uid}/key-claude-rieng", dependencies=[Depends(kiem_csrf)])
def quyen_key_rieng(uid: int, vao: KeyRiengVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    """Bật / tắt quyền dùng API key Claude riêng của một tài khoản (admin luôn được – không cần bật)."""
    u = db.lay_nguoi_dung(uid)
    if u is None:
        raise HTTPException(404, {"loi": "Không có người dùng này.", "ma": "khong_co"})
    if tai_khoan.la_chu_app(u):
        raise HTTPException(400, {"loi": "Admin luôn dùng được key Claude riêng – không cần bật.", "ma": "la_admin"})
    if not vao.bat and vao.tu_choi:
        if claude_rieng.tu_choi(uid):
            nhat_ky.ghi("tu_choi_key_claude_rieng", request, admin, phuong_thuc="admin_web", doi_tuong=uid, chi_tiet=f"#{uid} {u.email}")
    elif claude_rieng.cap_quyen(uid, vao.bat, admin.id):
        nhat_ky.ghi("bat_key_claude_rieng" if vao.bat else "tat_key_claude_rieng", request, admin, phuong_thuc="admin_web",
                    doi_tuong=uid, chi_tiet=f"#{uid} {u.email}")
    return {"ok": True, **claude_rieng.trang_thai_admin(uid)}


class KhoaVao(BaseModel):
    khoa: bool


@router.post("/nguoi-dung/{uid}/khoa", dependencies=[Depends(kiem_csrf)])
def khoa(uid: int, vao: KhoaVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    muc_tieu = db.lay_nguoi_dung(uid)
    if muc_tieu is None:
        raise HTTPException(404, {"loi": "Không có người dùng này.", "ma": "khong_co"})
    try:
        u = nhat_ky.khoa_tai_khoan(muc_tieu, vao.khoa, admin, request, "admin_web")
    except nhat_ky.LoiQuanTri as e:
        raise HTTPException(400, {"loi": str(e), "ma": "khong_khoa_duoc"}) from e
    return {"ok": True, "bi_khoa": u.bi_khoa}


@router.get("/canh-bao")
def canh_bao():
    return nhat_ky.canh_bao()


@router.get("/bieu-do")
def bieu_do(ngay: int = 14):
    return {"ngay": nhat_ky.bieu_do(max(1, min(ngay, 90)))}


class DaXemVao(BaseModel):
    id: int = Field(0, ge=0)


@router.post("/da-xem", dependencies=[Depends(kiem_csrf)])
def da_xem(vao: DaXemVao, admin: NguoiDung = Depends(can_admin)):
    db.dat_da_xem_nhat_ky(admin.id, min(vao.id or nhat_ky.id_moi_nhat(), nhat_ky.id_moi_nhat()))
    return {"ok": True}


class NhanVao(BaseModel):
    nhan: str = Field("", max_length=100)


@router.post("/nhan-thanh-vien", dependencies=[Depends(kiem_csrf)])
def nhan_thanh_vien(vao: NhanVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    nhan = re.sub(r"\s+", " ", vao.nhan).strip()
    if not 1 <= len(nhan) <= 24:
        raise HTTPException(400, {"loi": "Nhãn cần từ 1 đến 24 ký tự.", "ma": "nhan_do_dai"})
    cu = nhat_ky.nhan_thanh_vien()
    db.dat_cai_dat("nhan_thanh_vien", nhan)
    nhat_ky.ghi("doi_nhan", request, admin, phuong_thuc="admin_web", chi_tiet=f"“{cu}” → “{nhan}”")
    return {"ok": True, "nhan": nhan}


# ---------------------------------------------------------------- mã kích hoạt Claude + chi phí AI
class TaoMaVao(BaseModel):
    so_luot: int = Field(20, ge=1, le=100000)
    het_han: str = Field(..., max_length=10)          # YYYY-MM-DD (dùng được hết ngày này)
    so_tai_khoan: int = Field(1, ge=1, le=1000)
    ghi_chu: str = Field("", max_length=200)


def _loi_ma(e: ma_claude.LoiMaClaude) -> HTTPException:
    return HTTPException(e.status, {"loi": str(e), "ma": e.ma})


@router.get("/ma-claude")
def ds_ma_claude():
    return {"ma": ma_claude.danh_sach()}


@router.post("/ma-claude", dependencies=[Depends(kiem_csrf)])
def tao_ma_claude(vao: TaoMaVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    """Tạo mã: trả mã gốc ĐÚNG MỘT LẦN (CSDL chỉ lưu bản băm)."""
    try:
        ma, ma_id = ma_claude.tao_ma(vao.so_luot, vao.het_han, vao.so_tai_khoan, vao.ghi_chu, nguoi_tao=admin.id)
    except ma_claude.LoiMaClaude as e:
        raise _loi_ma(e) from e
    nhat_ky.ghi("tao_ma_claude", request, admin, phuong_thuc="admin_web",
                chi_tiet=f"#{ma_id} …{ma[-4:]}: {vao.so_luot} lượt, hết hạn {vao.het_han}, {vao.so_tai_khoan} tài khoản"
                         + (f" – {vao.ghi_chu.strip()[:60]}" if vao.ghi_chu.strip() else ""))
    return {"ok": True, "ma_goc": ma, **ma_claude.tim(str(ma_id))}


class ThemLuotVao(BaseModel):
    so: int = Field(..., ge=1, le=100000)


@router.post("/ma-claude/{ma_id}/thu-hoi", dependencies=[Depends(kiem_csrf)])
def thu_hoi_ma_claude(ma_id: int, request: Request, admin: NguoiDung = Depends(can_admin)):
    try:
        x = ma_claude.thu_hoi(ma_id)
    except ma_claude.LoiMaClaude as e:
        raise _loi_ma(e) from e
    nhat_ky.ghi("thu_hoi_ma_claude", request, admin, phuong_thuc="admin_web", chi_tiet=f"#{ma_id} {x['goi_y']}")
    return {"ok": True, **x}


@router.post("/ma-claude/{ma_id}/them-luot", dependencies=[Depends(kiem_csrf)])
def them_luot_ma_claude(ma_id: int, vao: ThemLuotVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    try:
        x = ma_claude.them_luot(ma_id, vao.so)
    except ma_claude.LoiMaClaude as e:
        raise _loi_ma(e) from e
    nhat_ky.ghi("them_luot_ma_claude", request, admin, phuong_thuc="admin_web", chi_tiet=f"#{ma_id} {x['goi_y']}: +{vao.so} lượt")
    return {"ok": True, **x}


# ---- Key Claude dùng chung: ai đã mở Claude, lượt hôm nay / tổng, số lần nhập sai; thu hồi / mở lại (KHÔNG BAO GIỜ trả key)
@router.get("/key-claude")
def ds_key_claude():
    return {"cau_hinh": key_claude.tom_tat(), "nguoi_dung": key_claude.danh_sach()}


def _nguoi_key(uid: int) -> NguoiDung:
    u = db.lay_nguoi_dung(uid)
    if u is None:
        raise HTTPException(404, {"loi": "Không có người dùng này.", "ma": "khong_co"})
    if tai_khoan.la_chu_app(u):
        raise HTTPException(400, {"loi": "Admin luôn dùng được Claude – không thu hồi được.", "ma": "la_admin"})
    return u


@router.post("/key-claude/{uid}/thu-hoi", dependencies=[Depends(kiem_csrf)])
def thu_hoi_key_claude(uid: int, request: Request, admin: NguoiDung = Depends(can_admin)):
    u = _nguoi_key(uid)
    if key_claude.thu_hoi(uid, admin.id):
        nhat_ky.ghi("thu_hoi_key_claude", request, admin, phuong_thuc="admin_web", doi_tuong=uid, chi_tiet=f"#{uid} {u.email}")
    return {"ok": True, **key_claude.trang_thai(u)}


@router.post("/key-claude/{uid}/mo-lai", dependencies=[Depends(kiem_csrf)])
def mo_lai_key_claude(uid: int, request: Request, admin: NguoiDung = Depends(can_admin)):
    u = _nguoi_key(uid)
    if key_claude.mo_lai(uid):
        nhat_ky.ghi("mo_lai_key_claude", request, admin, phuong_thuc="admin_web", doi_tuong=uid, chi_tiet=f"#{uid} {u.email}")
    return {"ok": True, **key_claude.trang_thai(u)}


# ---- Claude API máy chủ: chi phí + token (Admin API của Anthropic, cache 10 phút), các lần nạp tiền, số dư ƯỚC TÍNH
@router.get("/claude")
def claude_may_chu(lam_moi: int = 0):
    return claude_admin.bao_cao(lam_moi=bool(lam_moi))


class NapVao(BaseModel):
    usd: float = Field(..., gt=0, le=100000)
    ghi_chu: str = Field("", max_length=200)


@router.post("/claude/nap", dependencies=[Depends(kiem_csrf)])
def nap_claude(vao: NapVao, admin: NguoiDung = Depends(can_admin)):
    claude_admin.them_nap(vao.usd, vao.ghi_chu, admin.id)
    return claude_admin.bao_cao()


@router.post("/claude/nap/{nap_id}/xoa", dependencies=[Depends(kiem_csrf)])
def xoa_nap_claude(nap_id: int):
    if not claude_admin.xoa_nap(nap_id):
        raise HTTPException(404, {"loi": "Không có lần nạp này.", "ma": "khong_co"})
    return claude_admin.bao_cao()


class NguongVao(BaseModel):
    usd: float = Field(..., ge=0, le=100000)


@router.post("/claude/canh-bao", dependencies=[Depends(kiem_csrf)])
def nguong_claude(vao: NguongVao):
    claude_admin.dat_nguong(vao.usd)
    return claude_admin.bao_cao()


# ---- Mã giao diện (nút "+K"): tạo (mã gốc chỉ trả MỘT lần), xem danh sách, thu hồi
@router.get("/ma-giao-dien")
def ds_ma_giao_dien():
    return {"ma": ma_giao_dien.danh_sach(), "themes": {k: v for k, v in ma_giao_dien.THEMES.items() if k != "default"}}


class TaoMaGiaoDienVao(BaseModel):
    theme_id: str = Field(..., max_length=40)
    ma: str = Field("", max_length=40)                 # trống → sinh ngẫu nhiên GD-XXXX-XXXX
    so_luot: int | None = Field(None, ge=1, le=1_000_000)
    het_han: str = Field("", max_length=10)            # YYYY-MM-DD, trống = không hạn
    ghi_chu: str = Field("", max_length=200)


@router.post("/ma-giao-dien", dependencies=[Depends(kiem_csrf)])
def tao_ma_giao_dien(vao: TaoMaGiaoDienVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    try:
        ma, ma_id = ma_giao_dien.tao_ma(vao.theme_id, vao.ma, vao.so_luot, vao.het_han, vao.ghi_chu, admin.id)
    except ValueError as e:
        raise HTTPException(400, {"loi": str(e), "ma": "sai_du_lieu"}) from e
    nhat_ky.ghi("tao_ma_giao_dien", request, admin, phuong_thuc="admin_web",
                chi_tiet=f"#{ma_id} → {ma_giao_dien.THEMES[vao.theme_id]}")  # không ghi mã gốc vào nhật ký
    return {"ok": True, "ma_goc": ma, "id": ma_id, **ds_ma_giao_dien()}


@router.post("/ma-giao-dien/{ma_id}/thu-hoi", dependencies=[Depends(kiem_csrf)])
def thu_hoi_ma_giao_dien(ma_id: int, request: Request, admin: NguoiDung = Depends(can_admin)):
    if not ma_giao_dien.thu_hoi(ma_id):
        raise HTTPException(404, {"loi": "Không có mã này hoặc đã thu hồi.", "ma": "khong_co"})
    nhat_ky.ghi("thu_hoi_ma_giao_dien", request, admin, phuong_thuc="admin_web", chi_tiet=f"#{ma_id}")
    return ds_ma_giao_dien()


# ---- Thứ tự AI cho từng hệ thống (router backend/ai): kéo thả ở /admin → Thứ tự AI, lưu CSDL đè routing.json
def _thu_tu_ai() -> dict:
    from .ai import NHA_CUNG_CAP
    from .ai import router as ai_router
    from .config import settings
    mo_hinh = {"claude": f"{settings.model} · {settings.claude_model_manh}",
               "gemini": f"{settings.gemini_model_lite} · {settings.gemini_model} · {settings.gemini_model_hsg}",
               "openai": settings.openai_model or "(chưa đặt OPENAI_MODEL)"}
    return {"he_thong": ai_router.tat_ca(),
            "nha_cung_cap": {k: {"ten": ai_router.TEN_AI[k], "co_key": p.co_key(), "mo_hinh": mo_hinh[k],
                                 "thieu": "" if p.co_key() else so_do_he_thong.THIEU_KEY[k]} for k, p in NHA_CUNG_CAP.items()},
            "du_phong": ai_router.ds_du_phong(20), "timeout_giay": settings.ai_timeout_giay, "khong_dung": so_do_he_thong.ai_khong_dung()}


@router.get("/thu-tu-ai")
def xem_thu_tu_ai():
    return _thu_tu_ai()


class ThuTuAIVao(BaseModel):
    he_thong: str = Field(..., max_length=40)
    thu_tu: list[str] | None = None  # None → về mặc định (routing.json)


@router.post("/thu-tu-ai", dependencies=[Depends(kiem_csrf)])
def dat_thu_tu_ai(vao: ThuTuAIVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    from .ai import router as ai_router
    try:
        ds = ai_router.ve_mac_dinh(vao.he_thong) if vao.thu_tu is None else ai_router.dat_thu_tu(vao.he_thong, vao.thu_tu)
    except ValueError as e:
        raise HTTPException(400, {"loi": str(e), "ma": "thu_tu_sai"}) from e
    nhat_ky.ghi("doi_thu_tu_ai", request, admin, phuong_thuc="admin_web",
                chi_tiet=f"{ai_router.HE_THONG[vao.he_thong]}: {' → '.join(ai_router.TEN_AI[x] for x in ds)}"
                         + (" (mặc định)" if vao.thu_tu is None else ""))
    return _thu_tu_ai()


# ---- Sơ đồ hệ thống (/admin/mindmap): cấu hình + tên mô hình thật + số lần gọi hôm nay; sức khoẻ tách riêng (gọi mạng, cache 5 phút)
@router.get("/so-do")
def so_do():
    return so_do_he_thong.du_lieu()


@router.get("/so-do/suc-khoe")
def so_do_suc_khoe(lam_moi: int = 0):
    return so_do_he_thong.suc_khoe(lam_moi=bool(lam_moi))


# ---- Email rác: nhật ký bị chặn, tài khoản nghi ngờ (duyệt), danh sách chặn / cho phép, cập nhật danh sách email tạm thời
@router.get("/email-rac")
def email_rac_tong_hop(ly_do: str = "", tim: str = ""):
    return {"thong_ke": email_rac.thong_ke_chan(7), "chan": email_rac.danh_sach_chan(200, ly_do[:40], tim[:100]),
            "nghi_ngo": email_rac.danh_sach_nghi_ngo(), "quy_tac": email_rac.danh_sach_quy_tac(),
            "danh_sach": email_rac.thong_tin_danh_sach(), "ly_do": email_rac.TEN_LY_DO}


class QuyTacVao(BaseModel):
    ten_mien: str = Field(..., max_length=253)
    loai: str = Field("chan", pattern="^(chan|cho)$")
    ghi_chu: str = Field("", max_length=200)


@router.post("/email-rac/quy-tac", dependencies=[Depends(kiem_csrf)])
def them_quy_tac_email(vao: QuyTacVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    """Thêm tên miền vào danh sách CHẶN (blacklist) hoặc CHO PHÉP (whitelist) – có hiệu lực ngay, không cần khởi động lại."""
    try:
        mau = email_rac.them_quy_tac(vao.ten_mien, vao.loai, vao.ghi_chu, admin.id)
    except ValueError as e:
        raise HTTPException(400, {"loi": str(e), "ma": "ten_mien_sai"}) from e
    nhat_ky.ghi("sua_ds_email", request, admin, phuong_thuc="admin_web",
                chi_tiet=f"{'chặn' if vao.loai == 'chan' else 'cho phép'} {mau}")
    return email_rac_tong_hop()


class XoaQuyTacVao(BaseModel):
    ten_mien: str = Field(..., max_length=253)


@router.post("/email-rac/quy-tac/xoa", dependencies=[Depends(kiem_csrf)])
def xoa_quy_tac_email(vao: XoaQuyTacVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    if not email_rac.xoa_quy_tac(vao.ten_mien):
        raise HTTPException(404, {"loi": "Không có tên miền này trong danh sách.", "ma": "khong_co"})
    nhat_ky.ghi("sua_ds_email", request, admin, phuong_thuc="admin_web", chi_tiet=f"bỏ {vao.ten_mien.strip().lower()}")
    return email_rac_tong_hop()


class DuyetVao(BaseModel):
    khoa: bool = False


@router.post("/email-rac/nghi-ngo/{uid}", dependencies=[Depends(kiem_csrf)])
def duyet_nghi_ngo(uid: int, vao: DuyetVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    """Duyệt tài khoản có email nghi ngờ: cho qua (bỏ đánh dấu) hoặc khoá."""
    u = db.lay_nguoi_dung(uid)
    if u is None or not email_rac.duyet_nghi_ngo(uid, admin.id, vao.khoa):
        raise HTTPException(404, {"loi": "Không có tài khoản nghi ngờ này.", "ma": "khong_co"})
    nhat_ky.ghi("duyet_nghi_ngo", request, admin, phuong_thuc="admin_web", doi_tuong=uid,
                chi_tiet=f"#{uid} {u.email}: {'khoá' if vao.khoa else 'cho qua'}")
    if vao.khoa:
        nhat_ky.ghi("khoa", request, admin, phuong_thuc="admin_web", doi_tuong=uid, chi_tiet=f"#{uid} {u.email} (email nghi ngờ)")
    return email_rac_tong_hop()


@router.post("/email-rac/cap-nhat", dependencies=[Depends(kiem_csrf)])
def cap_nhat_ds_email_tam():
    """Tải ngay bản mới nhất của danh sách email tạm thời từ GitHub (bình thường tự tải mỗi 7 ngày)."""
    try:
        email_rac.cap_nhat_danh_sach()
    except email_rac.LoiCapNhat as e:
        raise HTTPException(502, {"loi": str(e), "ma": "cap_nhat_loi"}) from e
    return email_rac_tong_hop()


# ---- Cài đặt chung (lưu CSDL app_settings, áp dụng ngay – không cần khởi động lại)
class CaiDatChungVao(BaseModel):
    hoat_anh_cai_dat: bool


@router.get("/cai-dat-chung")
def cai_dat_chung():
    return {"hoat_anh_cai_dat": db.cai_dat("hoat_anh_cai_dat", "1") == "1"}


@router.post("/cai-dat-chung", dependencies=[Depends(kiem_csrf)])
def luu_cai_dat_chung(vao: CaiDatChungVao):
    db.dat_cai_dat("hoat_anh_cai_dat", "1" if vao.hoat_anh_cai_dat else "0")
    return cai_dat_chung()


# ---- Phòng thí nghiệm Hoá ảo: công tắc "cho mọi người dùng bản 3D Unity" (app_settings "phong_thi_nghiem") + trạng thái bản build
class PhongThiNghiemVao(BaseModel):
    unity_bat: bool


@router.get("/phong-thi-nghiem")
def phong_thi_nghiem_xem():
    return phong_thi_nghiem.trang_thai_admin()


@router.post("/phong-thi-nghiem", dependencies=[Depends(kiem_csrf)])
def phong_thi_nghiem_luu(vao: PhongThiNghiemVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    phong_thi_nghiem.dat_unity(vao.unity_bat)
    nhat_ky.ghi("doi_phong_thi_nghiem", request, admin, phuong_thuc="admin_web",
                chi_tiet=f"{'Bật' if vao.unity_bat else 'Tắt'} bản Unity 3D Phòng thí nghiệm cho mọi người")
    return phong_thi_nghiem.trang_thai_admin()


class UngDungLabVao(BaseModel):
    bat: bool
    url_mac: str = ""
    url_win: str = ""
    phien_ban: str = "1.0.0"


@router.get("/ung-dung-lab")
def ung_dung_lab_xem(admin: NguoiDung = Depends(can_admin)):
    return phong_thi_nghiem.doc_ung_dung()


@router.post("/ung-dung-lab", dependencies=[Depends(kiem_csrf)])
def ung_dung_lab_luu(vao: UngDungLabVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    kq = phong_thi_nghiem.dat_ung_dung(vao.bat, vao.url_mac, vao.url_win, vao.phien_ban)
    nhat_ky.ghi("doi_ung_dung_lab", request, admin, phuong_thuc="admin_web",
                chi_tiet=f"{'Bật' if vao.bat else 'Tắt'} ứng dụng máy tính ({vao.phien_ban})")
    return kq



# ---- Lộ trình học Sinh (danh sách bài: config/lo_trinh_sinh_hoc.json · tiến độ theo từng admin: bảng lo_trinh_tien_do)
class LoTrinhVao(BaseModel):
    xong: bool


@router.get("/lo-trinh")
def lo_trinh_xem(admin: NguoiDung = Depends(can_admin)):
    return lo_trinh.tien_do(admin.id)


@router.post("/lo-trinh/{bai_id}", dependencies=[Depends(kiem_csrf)])
def lo_trinh_danh_dau(bai_id: str, vao: LoTrinhVao, admin: NguoiDung = Depends(can_admin)):
    try:
        return lo_trinh.danh_dau(admin.id, bai_id, vao.xong)
    except lo_trinh.LoiLoTrinh as e:
        raise HTTPException(e.status, {"loi": str(e), "ma": "khong_co_bai"}) from None


@router.get("/chi-phi")
def chi_phi(ngay: int = 1):
    """Mục "Chi phí": lượt + token theo từng mô hình trong ngày / 7 ngày / 30 ngày, ai dùng Claude nhiều nhất, tỉ lệ mức độ đề."""
    return chi_phi_ai.thong_ke(ngay if ngay in (1, 7, 30) else 1)


# ================================================================ NotebookLM (quản lý đa tài khoản & mindmap)
from services import notebooklm as nlm_service


class NlmTaiKhoanVao(BaseModel):
    """Thêm / sửa tài khoản NotebookLM. raw_cookie = NGUYÊN VĂN nội dung admin dán (backend tự chuẩn hoá, tối đa 20.000 ký tự).
    Không đặt ràng buộc độ dài ở đây: lỗi kiểm tra mặc định của FastAPI trả lại cả dữ liệu đã gửi (lộ cookie) – tự kiểm tra trong code."""
    model_config = ConfigDict(extra="ignore")
    ten_goi_nho: str | None = None
    raw_cookie: str | list | dict | None = None
    email: str | None = None
    is_default: bool | None = None
    cookie: str | None = None        # tên cũ – vẫn nhận
    cookie_raw: str | None = None    # tên cũ – vẫn nhận

    @property
    def cookie_dan(self):
        return self.raw_cookie if self.raw_cookie not in (None, "") else (self.cookie_raw or self.cookie)


def _loi_nlm(e: Exception) -> HTTPException:
    """Lỗi NotebookLM → mẫu chung {code, message, missing} (+ loi / ma cho giao diện cũ). Không bao giờ chứa giá trị cookie."""
    code = getattr(e, "code", "loi_ket_noi")
    d = {"code": code, "message": str(e), "loi": str(e), "ma": code}
    if getattr(e, "missing", None):
        d["missing"] = list(e.missing)
    return HTTPException(getattr(e, "status_code", 400), d)


def _doc_tai_khoan_vao(than: dict) -> NlmTaiKhoanVao:
    try:
        return NlmTaiKhoanVao.model_validate(than)
    except ValidationError as e:   # KHÔNG trả lại e (có cả dữ liệu gửi lên)
        truong = sorted({str(x["loc"][0]) for x in e.errors() if x.get("loc")})
        raise HTTPException(422, {"code": "INVALID_INPUT", "message": f"Dữ liệu gửi lên không hợp lệ ({', '.join(truong)}).",
                                  "loi": "Dữ liệu gửi lên không hợp lệ.", "ma": "INVALID_INPUT"}) from None


class NlmHoiVao(BaseModel):
    account_id: int
    notebook_id: str
    cau_hoi: str = Field(..., max_length=1000)


class NlmNhapAiRiengVao(BaseModel):
    account_id: int
    notebook_id: str
    chu_de: str = "NotebookLM"


@router.get("/notebooklm/accounts")
def nlm_danh_sach_tai_khoan():
    return {"tai_khoan": nlm_service.ds_tai_khoan()}


@router.post("/notebooklm/accounts", dependencies=[Depends(kiem_csrf)])
def nlm_them_tai_khoan(request: Request, than: dict = Body(default_factory=dict), admin: NguoiDung = Depends(can_admin)):
    vao = _doc_tai_khoan_vao(than)
    try:
        acc = nlm_service.them_tai_khoan(vao.ten_goi_nho, vao.cookie_dan, bool(vao.is_default), email=vao.email)
    except nlm_service.LoiNotebookLM as e:
        raise _loi_nlm(e) from None
    nhat_ky.ghi("them_nlm_account", request, admin, phuong_thuc="admin_web", doi_tuong=acc["id"],
                chi_tiet=f"Thêm tài khoản NotebookLM: {acc['ten_goi_nho']} ({acc.get('email') or 'chưa rõ email'})")
    return acc


@router.put("/notebooklm/accounts/{acc_id}", dependencies=[Depends(kiem_csrf)])
def nlm_cap_nhat_tai_khoan(acc_id: int, request: Request, than: dict = Body(default_factory=dict), admin: NguoiDung = Depends(can_admin)):
    vao = _doc_tai_khoan_vao(than)
    try:
        acc = nlm_service.cap_nhat_tai_khoan(acc_id, vao.ten_goi_nho, vao.cookie_dan, vao.is_default, email=vao.email)
    except nlm_service.LoiNotebookLM as e:
        raise _loi_nlm(e) from None
    nhat_ky.ghi("cap_nhat_nlm_account", request, admin, phuong_thuc="admin_web", doi_tuong=acc_id,
                chi_tiet=f"Cập nhật tài khoản NotebookLM #{acc_id}" + (" (cookie mới)" if vao.cookie_dan else ""))
    return acc


@router.delete("/notebooklm/accounts/{acc_id}", dependencies=[Depends(kiem_csrf)])
def nlm_xoa_tai_khoan(acc_id: int, request: Request, admin: NguoiDung = Depends(can_admin)):
    ok = nlm_service.xoa_tai_khoan(acc_id)
    if not ok:
        raise HTTPException(404, {"loi": "Không tìm thấy tài khoản NotebookLM.", "ma": "khong_co"})
    nhat_ky.ghi("xoa_nlm_account", request, admin, phuong_thuc="admin_web", doi_tuong=acc_id,
                chi_tiet=f"Xoá tài khoản NotebookLM #{acc_id}")
    return {"thanh_cong": True}


@router.post("/notebooklm/accounts/{acc_id}/mac-dinh", dependencies=[Depends(kiem_csrf)])
def nlm_dat_mac_dinh(acc_id: int):
    ok = nlm_service.dat_mac_dinh(acc_id)
    if not ok:
        raise HTTPException(404, {"loi": "Không tìm thấy tài khoản NotebookLM.", "ma": "khong_co"})
    return {"thanh_cong": True, "accounts": nlm_service.ds_tai_khoan()}


@router.post("/notebooklm/accounts/{acc_id}/check", dependencies=[Depends(kiem_csrf)])
def nlm_kiem_tra_tai_khoan(acc_id: int):
    """Kiểm tra kết nối: {tai_khoan, ket_noi, message} – message là "Đã kết nối" hoặc lý do (cookie hết hạn…)."""
    try:
        acc = nlm_service.kiem_tra_tai_khoan(acc_id)
    except nlm_service.LoiNotebookLM as e:
        raise _loi_nlm(e) from None
    ok = acc.get("status") == "dang_hoat_dong"
    return {"tai_khoan": acc, "ket_noi": ok, "message": "Đã kết nối" if ok else (acc.get("error_msg") or "Chưa kết nối được.")}


@router.post("/notebooklm/accounts/{acc_id}/sync", dependencies=[Depends(kiem_csrf)])
def nlm_dong_bo_tai_khoan(acc_id: int):
    try:
        return nlm_service.dong_bo_tai_khoan(acc_id)
    except nlm_service.LoiNotebookLM as e:
        raise _loi_nlm(e) from None


@router.get("/notebooklm/mindmap")
def nlm_mindmap_node(account_id: int | None = None, tat_ca: bool = False):
    """Lấy cây dữ liệu NotebookLM cho sơ đồ tư duy (hỗ trợ lazy load và đổi tài khoản)."""
    return nlm_service.lay_nhanh_mindmap(account_id, tat_ca=tat_ca)


@router.get("/notebooklm/notebooks/{acc_id}/{nb_id}")
def nlm_chi_tiet_notebook(acc_id: int, nb_id: str):
    nb = nlm_service.lay_chi_tiet_notebook(acc_id, nb_id)
    if not nb:
        raise HTTPException(404, {"loi": "Không tìm thấy notebook.", "ma": "khong_co"})
    return nb


@router.post("/notebooklm/query", dependencies=[Depends(kiem_csrf)])
def nlm_hoi_notebook(vao: NlmHoiVao):
    try:
        return nlm_service.hoi_notebook(vao.account_id, vao.notebook_id, vao.cau_hoi)
    except nlm_service.LoiNotebookLM as e:
        raise _loi_nlm(e) from None


@router.post("/notebooklm/import-to-ai-rieng", dependencies=[Depends(kiem_csrf)])
def nlm_nhap_ai_rieng(vao: NlmNhapAiRiengVao, request: Request, admin: NguoiDung = Depends(can_admin)):
    try:
        res = nlm_service.nhap_notebook_vao_ai_rieng(vao.account_id, vao.notebook_id, vao.chu_de)
        nhat_ky.ghi("import_nlm_ai_rieng", request, admin, phuong_thuc="admin_web", doi_tuong=res["tai_lieu_id"],
                    chi_tiet=f"Nhập NotebookLM '{res['ten']}' vào AI Riêng ({res['so_doan']} đoạn)")
        return res
    except Exception as e:
        raise HTTPException(400, {"loi": f"Nhập vào AI Riêng thất bại: {e}", "ma": "loi_nhap"}) from e


# =====================================================================================================================
# QUẢN TRỊ KHO KIẾN THỨC VÀ PHẢN HỒI PHÒNG LAB SINH HỌC 3D
# =====================================================================================================================

@router.get("/sinh-hoc/knowledge")
def admin_danh_sach_knowledge():
    """Lấy danh sách các file markdown trong knowledge/."""
    return {"files": sinh_hoc_rag.danh_sach_file_knowledge()}


@router.get("/sinh-hoc/knowledge/file")
def admin_doc_knowledge(name: str):
    """Đọc nội dung một file markdown trong knowledge/."""
    try:
        return {"name": name, "content": sinh_hoc_rag.doc_file_knowledge(name)}
    except FileNotFoundError as e:
        raise HTTPException(404, {"loi": str(e), "ma": "khong_tim_thay"}) from e


class GhiKnowledgeVao(BaseModel):
    name: str = Field(..., min_length=1, max_length=100)
    content: str = Field(..., min_length=1)


@router.post("/sinh-hoc/knowledge/file", dependencies=[Depends(kiem_csrf)])
def admin_ghi_knowledge(vao: GhiKnowledgeVao):
    """Tạo mới hoặc cập nhật file markdown trong knowledge/ và reindex."""
    return sinh_hoc_rag.ghi_file_knowledge(vao.name, vao.content)


@router.delete("/sinh-hoc/knowledge/file", dependencies=[Depends(kiem_csrf)])
def admin_xoa_knowledge(name: str):
    """Xóa file markdown trong knowledge/ và reindex."""
    return sinh_hoc_rag.xoa_file_knowledge(name)


@router.post("/sinh-hoc/knowledge/reindex", dependencies=[Depends(kiem_csrf)])
def admin_reindex_knowledge():
    """Chạy lại chỉ mục vector/BM25 cho toàn bộ kho tài liệu knowledge/."""
    return sinh_hoc_rag.cap_nhat_chi_muc()


@router.get("/sinh-hoc/phan-hoi")
def admin_danh_sach_phan_hoi(gioi_han: int = 100):
    """Lấy danh sách phản hồi và báo sai từ học sinh để hoàn thiện kho tài liệu."""
    with db._ket_noi() as conn:
        cur = conn.execute("""
            SELECT id, ts, user_id, cau_hoi, cau_tra_loi, hai_long, ly_do, chi_tiet
            FROM sinh_hoc_phan_hoi
            ORDER BY id DESC
            LIMIT ?
        """, (gioi_han,))
        rows = [dict(r) for r in cur.fetchall()]
    return {"phan_hoi": rows}


