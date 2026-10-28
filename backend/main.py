"""FastAPI: phục vụ giao diện + API giải chuỗi, tiến trình SSE, tải file, mở ChemDraw, đăng nhập Google + key riêng (auth.py)."""
from __future__ import annotations

import asyncio
import hashlib
import json
import logging
import re
import threading
import time
from contextlib import asynccontextmanager
from typing import Callable
from urllib.parse import quote

from fastapi import Depends, FastAPI, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import FileResponse, HTMLResponse, JSONResponse, RedirectResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field
from starlette.middleware.sessions import SessionMiddleware

from . import sao_luu
from .config import setup_logging as _bat_nhat_ky

_bat_nhat_ky()
sao_luu.khoi_phuc_neu_can()  # máy chủ không ổ đĩa lưu (Render Free): tải dữ liệu từ bản sao lưu GitHub TRƯỚC khi mở CSDL
from . import co_che_anh, db, giai_nhanh, key_claude, literature, ma_claude, phong_thi_nghiem, pipeline, research, sinh_hoc_ai, tai_khoan, tim_chat  # noqa: E402
from .auth import duong_dan_an_toan, can_dang_nhap, gioi_han, kiem_csrf, loi_tai_khoan, nguoi_dung_hien_tai, router as router_tai_khoan
from .bao_mat import khoa_ky_phien
from .chemdraw import mo_bang_chemdraw, tim_chemdraw
from .config import FRONTEND_DIR, OUTPUT_DIR, settings, setup_logging
from .db import NguoiDung
from .ve_co_che import ve as ve_co_che, xuat_cdxml
from .co_che import tao_co_che
from .llm import KeHoachAI, LoiAI, dung_ke_hoach, dung_khoa, gemini_san_sang
from .mo_hinh_3d import tao_mo_hinh_3d
from .quan_tri_web import router as router_quan_tri
from .ai_rieng.web import router as router_ai_rieng, router_nguoi_dung as router_bao_sai
from .cap_nhat_app import router as router_cap_nhat_app

setup_logging()
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
log = logging.getLogger(__name__)

class DauCache:
    """Header Cache-Control cho mọi phản hồi – cần khi chạy sau Cloudflare (Tunnel/proxy):
    - /api/*, /auth/*: "private, no-store" → Cloudflare không giữ ảnh/file/kết quả riêng của từng người ở edge (.png, .zip... mặc định bị cache).
    - trang HTML và /static/*: "no-cache" → luôn hỏi lại server (ETag), không dùng app.js cũ sau khi cập nhật code.
    """

    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        duong_dan = scope.get("path", "") if scope["type"] == "http" else ""
        if duong_dan.startswith(("/api/", "/auth/", "/admin")):
            gia_tri = b"private, no-store"
        elif duong_dan.startswith("/static/") or duong_dan in ("/", "/cai-dat", "/dang-nhap"):
            gia_tri = b"no-cache"
        else:
            return await self.app(scope, receive, send)

        async def gui(msg):
            if msg["type"] == "http.response.start":
                msg["headers"] = [(k, v) for k, v in msg.get("headers", []) if k.lower() != b"cache-control"]
                msg["headers"].append((b"cache-control", gia_tri))
            await send(msg)

        await self.app(scope, receive, gui)


@asynccontextmanager
async def _vong_doi(_app):
    """Khởi động: không bài nào còn đang giải → bỏ các lượt mã Claude / Key Claude đang "giữ" (app tắt giữa chừng lần trước)."""
    ma_claude.don_giu_khi_khoi_dong()
    key_claude.don_khi_khoi_dong()
    from . import claude_rieng
    if n := claude_rieng.don_het_han():
        log.info("Đã xoá hẳn %d API key Claude riêng (admin tắt quyền quá %d ngày)", n, claude_rieng.GIU_KHI_TAT_NGAY)
    from . import email_rac, ma_giao_dien
    if ma_giao_dien.tao_ma_mau():
        log.info("Đã tạo mã giao diện mẫu %s (theme %s) – admin thu hồi được ở /admin → Mã giao diện", ma_giao_dien.MA_MAU, ma_giao_dien.THEME_MAU)
    email_rac.chay_nen()  # mỗi giờ: xoá tài khoản quá 24 giờ chưa xác minh email; danh sách email tạm cũ hơn 7 ngày → tải lại
    from .ai_rieng import xu_ly as ai_rieng_xu_ly
    ai_rieng_xu_ly.chay_nen()  # AI Riêng: hàng chờ tài liệu (tách đoạn, trích bài có lời giải) + nhúng vector – thread nền
    from .auth import cap_admin_theo_cau_hinh
    cap_admin_theo_cau_hinh()
    sao_luu.bat_dau()          # sao lưu dữ liệu lên GitHub mỗi SAO_LUU_PHUT phút (chỉ khi đã cấu hình)
    yield
    sao_luu.dung()             # web đi ngủ / cập nhật: gửi bản sao lưu cuối


app = FastAPI(title="CHEMISTRY", lifespan=_vong_doi)
app.add_middleware(DauCache)
app.add_middleware(
    SessionMiddleware,
    secret_key=khoa_ky_phien(),
    session_cookie="giai_chuoi_phien",
    max_age=14 * 24 * 3600,
    same_site="lax",
    https_only=settings.cookie_secure,  # cookie có cờ Secure (+ httponly mặc định của SessionMiddleware)
)
app.mount("/static", StaticFiles(directory=FRONTEND_DIR), name="static")
app.include_router(router_tai_khoan)
app.include_router(router_quan_tri)  # /admin/api/* – chỉ admin
app.include_router(router_ai_rieng)  # /admin/api/ai-rieng/* – chỉ admin
app.include_router(router_bao_sai)   # /api/bao-sai – người dùng báo lời giải sai (hàng chờ admin)
app.include_router(router_cap_nhat_app)  # /api/app/phien-ban – app máy tính hỏi bản mới (công khai, chỉ đọc)

ANH_TOI_DA = 10 * 1024 * 1024
_MA_JOB = re.compile(r"\d{8}-\d{6}-[0-9a-f]{6,32}")


@app.exception_handler(HTTPException)
async def _loi_http(_req, exc: HTTPException):
    noi_dung = exc.detail if isinstance(exc.detail, dict) else {"loi": exc.detail}
    return JSONResponse(noi_dung, status_code=exc.status_code, headers=getattr(exc, "headers", None))


def _kiem_chu(job_id: str, u: NguoiDung) -> None:
    """Chỉ chủ bài giải xem được tiến trình/kết quả/file (người khác nhận 404 như thể bài không tồn tại).
    Bài cũ (trước khi có đăng nhập, không có chủ) chỉ chủ app xem được."""
    if not _MA_JOB.fullmatch(job_id):
        raise HTTPException(404, "Không tìm thấy bài giải này.")
    job = pipeline.JOBS.get(job_id)
    chu = job.user_id if job is not None else db.chu_bai_giai(job_id)
    if chu is None and tai_khoan.la_chu_app(u):
        return
    if chu != u.id:
        raise HTTPException(404, "Không tìm thấy bài giải này.")


def _job(job_id: str, u: NguoiDung) -> pipeline.Job:
    _kiem_chu(job_id, u)
    job = pipeline.JOBS.get(job_id)
    if job is None:
        raise HTTPException(404, "Không tìm thấy bài giải này (có thể server đã khởi động lại).")
    return job


def _trang(ten: str) -> HTMLResponse:
    """Trang HTML với /static/*.js|css gắn ?v=<mã băm nội dung>: cập nhật code là URL đổi,
    nên Cloudflare/trình duyệt không thể dùng lại bản JS/CSS cũ đã lưu.
    Khi settings.che_do_app=True: thay thế các URL CDN bằng bản vendor offline."""

    def gan_phien_ban(m: re.Match) -> str:
        tep = FRONTEND_DIR / m.group(1)
        v = hashlib.sha256(tep.read_bytes()).hexdigest()[:10] if tep.is_file() else "0"
        return f'"/static/{m.group(1)}?v={v}"'

    html = (FRONTEND_DIR / ten).read_text(encoding="utf-8")
    res_html = re.sub(r'"/static/((?:[\w-]+/)*[\w.-]+\.(?:js|css))"', gan_phien_ban, html)

    if settings.che_do_app:
        res_html = res_html.replace(
            "https://cdn.tailwindcss.com/3.4.16",
            "/static/vendor/tailwind.3.4.16.js"
        ).replace(
            "https://cdn.jsdelivr.net/npm/gsap@3.12.5/dist/gsap.min.js",
            "/static/vendor/gsap.3.12.5.min.js"
        ).replace(
            "https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js",
            "/static/vendor/three/three.module.js"
        ).replace(
            "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/",
            "/static/vendor/three/addons/"
        ).replace(
            "https://cdn.jsdelivr.net/npm/mathjax@3.2.2/es5/tex-chtml.js",
            "/static/vendor/mathjax.3.2.2.js"
        )

    return HTMLResponse(res_html)


def _trang_can_dang_nhap(request: Request, ten: str):
    """Mở app khi chưa đăng nhập → trang Đăng nhập (sau đó quay lại đúng trang/bài đang mở). Máy chạy app (chế độ cục bộ) vào thẳng."""
    if nguoi_dung_hien_tai(request) is None:
        tiep = request.url.path + (f"?{request.url.query}" if request.url.query else "")
        return RedirectResponse("/dang-nhap?tiep=" + quote(tiep, safe=""), status_code=303)
    return _trang(ten)


@app.get("/favicon.ico", include_in_schema=False)
def bieu_tuong_favicon():
    from fastapi import Response
    tep_ico = FRONTEND_DIR / "favicon.ico"
    if tep_ico.is_file():
        return FileResponse(tep_ico)
    return Response(status_code=204)


@app.get("/")
def trang_chu(request: Request):
    return _trang_can_dang_nhap(request, "index.html")


@app.get("/cai-dat")
def trang_cai_dat(request: Request):
    return _trang_can_dang_nhap(request, "cai-dat.html")


@app.get("/sinh-hoc")
def trang_sinh_hoc(request: Request):
    """Phòng Lab Sinh học 3D (frontend/biology-lab-3d.html – một file HTML độc lập, mở trực tiếp cũng chạy): giải phẫu 3D, mô phỏng
    sinh lý, cấp độ hiển vi, AI Bio-Tutor. Tab "Hoá học" trong trang nhúng lại trang chủ app (cùng nguồn)."""
    return _trang_can_dang_nhap(request, "biology-lab-3d.html")


@app.get("/sinh-hoc-v2")
def trang_sinh_hoc_v2():
    """Bản v2 đã bỏ (26/10/2026, chỉ giữ một phòng Sinh học). Đường dẫn cũ – kể cả menu của app đã cài – chuyển về /sinh-hoc."""
    return RedirectResponse("/sinh-hoc", status_code=307)


@app.get("/hoa-hoc/phong-thi-nghiem")
def trang_phong_thi_nghiem(request: Request):
    """Phòng thí nghiệm Hoá học ảo (frontend/phong-thi-nghiem.html + frontend/hoa-hoc/phong-thi-nghiem/): 5 thí nghiệm, bản Web 3D
    (Three.js) + bản Unity 3D do admin bật/tắt. ?tn=<id> mở thẳng một thí nghiệm (trang JS đọc)."""
    return _trang_can_dang_nhap(request, "phong-thi-nghiem.html")


@app.get("/hoa-hoc/hoa-ly")
def trang_hoa_ly(request: Request):
    """Mục Hoá lý: lưới mô phỏng đọc từ frontend/hoa-hoc/hoa-ly/danh-muc.json."""
    return _trang_can_dang_nhap(request, "hoa-ly.html")


@app.get("/hoa-hoc/hoa-ly/{ma}")
def trang_hoa_ly_mo_phong(request: Request, ma: str):
    """Trang mô phỏng / thư viện Hoá lý theo frontend/hoa-hoc/hoa-ly/danh-muc.json (chỉ mục trang_thai = 'co')."""
    if not re.match(r"^[a-z0-9-]{2,40}$", ma):
        raise HTTPException(status_code=404, detail="Mã không hợp lệ")

    tep_danh_muc = FRONTEND_DIR / "hoa-hoc" / "hoa-ly" / "danh-muc.json"
    if not tep_danh_muc.is_file():
        raise HTTPException(status_code=404, detail="Không tìm thấy danh mục")

    try:
        data = json.loads(tep_danh_muc.read_text(encoding="utf-8"))
    except Exception:
        raise HTTPException(status_code=500, detail="Lỗi đọc danh mục")

    danh_sach = data.get("mo_phong", []) + data.get("thu_vien", [])
    muc = next((m for m in danh_sach if m.get("id") == ma and m.get("trang_thai") == "co"), None)
    if not muc:
        raise HTTPException(status_code=404, detail=f"Mục '{ma}' không tồn tại hoặc chưa ra mắt")

    duong_dan_tep = f"hoa-hoc/hoa-ly/{ma}/trang.html"
    if not (FRONTEND_DIR / duong_dan_tep).is_file():
        raise HTTPException(status_code=404, detail=f"Giao diện '{duong_dan_tep}' không tồn tại")

    return _trang_can_dang_nhap(request, duong_dan_tep)


@app.get("/hoa-hoc/phong-thi-nghiem/thu-vien-dung-cu")
@app.get("/hoa-hoc/phong-thi-nghiem/thu-vien-dung-cu.html")
def trang_thu_vien_dung_cu(request: Request):
    """Thư viện dụng cụ phòng thí nghiệm ISO 3D."""
    return _trang("hoa-hoc/phong-thi-nghiem/thu-vien-dung-cu.html")


@app.get("/api/phong-thi-nghiem/cau-hinh")
def cau_hinh_phong_thi_nghiem(u: NguoiDung = Depends(can_dang_nhap)):
    """Bản Unity có được bật cho mọi người không, bản build có sẵn không, người đang xem có phải admin không."""
    return phong_thi_nghiem.cau_hinh_cho(tai_khoan.la_chu_app(u))


@app.get("/api/phong-thi-nghiem/ung-dung")
def ung_dung_phong_thi_nghiem(u: NguoiDung = Depends(can_dang_nhap)):
    """Thông tin ứng dụng máy tính tải về (chỉ trả link khi admin đã bật)."""
    return phong_thi_nghiem.ung_dung_cho_nguoi_dung()



def _trang_admin(request: Request, ten: str):
    """Trang chỉ admin: chưa đăng nhập → trang Đăng nhập (rồi quay lại đúng trang); không phải admin → 403 (kiểm tra ở backend)."""
    u = nguoi_dung_hien_tai(request)
    if u is None:
        return RedirectResponse("/dang-nhap?tiep=" + quote(request.url.path, safe=""), status_code=303)
    if not tai_khoan.la_chu_app(u):
        return HTMLResponse('<!doctype html><meta charset="utf-8"><title>403 · CHEMISTRY</title>'
                            '<p style="font-family:sans-serif;padding:2rem">403 – Trang này chỉ dành cho admin. <a href="/">Về trang giải</a></p>',
                            status_code=403)
    return _trang(ten)


@app.get("/admin")
def trang_admin(request: Request):
    return _trang_admin(request, "admin.html")


@app.get("/admin/ai-rieng")
def trang_ai_rieng(request: Request):
    """AI Riêng – Gia sư Hoá Hữu Cơ: dạy kiến thức, thư viện lời giải, Sân tập, Bộ đề kiểm tra (chỉ admin)."""
    return _trang_admin(request, "ai-rieng.html")


@app.get("/admin/mindmap")
def trang_so_do(request: Request):
    """Sơ đồ tư duy toàn hệ thống (nền đen): mỗi mục dùng AI nào, vai trò, trạng thái – dữ liệu từ config/so_do_he_thong.json."""
    return _trang_admin(request, "mindmap.html")


@app.get("/dang-nhap")
def trang_dang_nhap(request: Request, tiep: str = "/"):
    if request.session.get("uid") is not None and nguoi_dung_hien_tai(request) is not None:  # đã đăng nhập rồi
        return RedirectResponse(duong_dan_an_toan(tiep), status_code=303)
    return _trang("dang-nhap.html")


@app.get("/api/suc-khoe")
def suc_khoe():
    return {
        "ok": True,
        "co_claude": bool(settings.api_key),
        "co_gemini": gemini_san_sang(),
        "co_openai": bool(settings.openai_api_key and settings.openai_model),  # ChatGPT – AI dự phòng của máy chủ
        "mo_hinh": settings.model,
        "chemdraw": tim_chemdraw() is not None,
        "dang_nhap_google": settings.dang_nhap_google,
        "co_chrome": research.co_google_chrome(),
    }


def _ket_thuc_claude(quyen, thanh_cong: bool, kh: KeHoachAI, bao: Callable[[str], None] | None = None) -> None:
    """Lượt Claude (mã Claude / lượt ngày của Key Claude): chỉ trừ khi yêu cầu xong và có lượt Claude trả lời thành công;
    ngược lại trả lượt đã giữ."""
    if quyen is None:
        return
    if thanh_cong and kh.claude_thanh_cong:
        if quyen.chot() and bao:
            u = db.lay_nguoi_dung(kh.user_id) if kh.user_id else None
            if isinstance(quyen, key_claude.QuyenKey):
                tt = key_claude.trang_thai(u) if u else {}
                bao(f"Đã dùng 1 lượt Claude hôm nay (còn {tt.get('con_hom_nay', '?')}/{key_claude.gioi_han_ngay()} lượt).")
            else:
                con = (ma_claude.trang_thai(u) if u else {}).get("con_luot")
                bao(f"Đã trừ 1 lượt mã Claude" + (f" (còn {con} lượt)." if con is not None else "."))
    else:
        quyen.tra()


def _chay_bai(job: pipeline.Job, lua_chon: tai_khoan.LuaChonKhoa, uid: int, chuoi: str, anh: bytes | None,
              hien_chrome: bool, demo: bool, muc_do: str, quyen, thong_bao_claude: str = "", muc_thong_minh: str = "",
              bat_claude: bool = False, tra_cuu_tai_lieu: str = "", pdf: tuple[bytes, str] | None = None, ai: str = "") -> None:
    """Thread nền: chạy pipeline với đúng key + kế hoạch AI của người dùng, rồi ghi lịch sử (và trả lượt dùng thử nếu lỗi phía app).
    ai: "" | "ai_rieng" | "gemini" | "openai" (xem pipeline.chay)."""
    kh = KeHoachAI(loai="giai", user_id=uid, job_id=job.id, claude=quyen)
    if isinstance(quyen, key_claude.QuyenKey):
        quyen.loai, quyen.job_id = "giai", job.id

    def truoc_khi_xong(j: pipeline.Job, k: KeHoachAI) -> None:
        _ket_thuc_claude(quyen, j.ket_qua is not None and not j.loi, k, lambda m: j.emit("xong", m, "canh_bao"))

    with dung_khoa(lua_chon.khoa):
        if lua_chon.nguon == "ca_nhan":
            job.emit("phan_tich", f"Dùng {lua_chon.khoa.mo_ta}.")
        elif lua_chon.nguon == "dung_thu":
            job.emit("phan_tich", "Dùng thử bằng key của chủ app (đã trừ 1 lượt).")
        if thong_bao_claude:
            job.emit("phan_tich", thong_bao_claude, "canh_bao")
        pipeline.chay(job, chuoi, anh, hien_chrome, demo, muc_do_toi_thieu=muc_do, ke_hoach=kh, truoc_khi_xong=truoc_khi_xong,
                      muc_thong_minh=muc_thong_minh, bat_claude=bat_claude, tra_cuu_tai_lieu=tra_cuu_tai_lieu, pdf=pdf, ai=ai)
    if lua_chon.da_tru_luot and job.ket_qua and (job.ket_qua.get("ai_rieng") or {}).get("tang") == "thu_vien":
        db.hoan_luot_thu(uid)  # lời giải lấy từ thư viện AI Riêng (không gọi AI) → không tính lượt dùng thử
        lua_chon.da_tru_luot = False
        job.emit("xong", "Bài lấy từ thư viện lời giải – không tính lượt dùng thử.")
    try:
        tai_khoan.ket_thuc_bai(lua_chon, uid, job.id, job.ket_qua, job.loi, job.loai_loi, muc_do=job.muc_do,
                               claude=kh.claude_thanh_cong and job.ket_qua is not None)
    except Exception:  # noqa: BLE001 – lỗi ghi lịch sử không được làm hỏng bài giải
        log.exception("Không ghi được lịch sử bài %s", job.id)


def _quyen_claude(u: NguoiDung, muon_dung: bool) -> tuple[object | None, str]:
    """Backend quyết định quyền Claude (frontend chỉ gửi mong muốn) → (quyền | None, thông báo cho người dùng).
    Admin → Key Claude (còn lượt hôm nay) → mã Claude. Không có quyền / hết lượt → TỪ CHỐI Claude, yêu cầu vẫn chạy bằng Gemini."""
    if not muon_dung:
        return None, ""
    return key_claude.chon_quyen(u)


@app.post("/api/giai", dependencies=[Depends(kiem_csrf), gioi_han("giai")])
async def giai(
    chuoi: str = Form(""),
    hien_chrome: bool = Form(False),
    demo: bool = Form(False),
    hsg: bool = Form(False),
    muc_do: str = Form(""),
    dung_claude: bool = Form(False),
    muc_thong_minh: str = Form(""),
    tra_cuu_tai_lieu: str = Form(""),
    ai: str = Form(""),
    anh: UploadFile | None = File(None),
    pdf: UploadFile | None = File(None),
    pdf_doi: str = Form(""),
    u: NguoiDung = Depends(can_dang_nhap),
):
    """muc_do: mức tối thiểu người dùng chọn ("" = tự phân loại | "trung_binh" | "hsg"; hsg=true như "hsg").
    dung_claude: XIN dùng Claude – backend kiểm tra quyền (Key Claude / mã Claude / admin); không có quyền → giải bằng Gemini + thông báo.
    ai: "ai_rieng" (AI Riêng – admin luôn được, người dùng khi admin đã mở) | "gemini" | "openai" (AI đó giải trước) | "" (như cũ)."""
    ai = ai if ai in ("ai_rieng", "gemini", "openai") else ""
    if ai == "ai_rieng" and not ai_rieng_duoc_dung(u):
        ai = ""  # admin chưa mở AI Riêng cho người dùng → giải như thường
    muc_do = "hsg" if hsg else (muc_do if muc_do in ("trung_binh", "hsg") else "")
    muc_thong_minh = muc_thong_minh if muc_thong_minh in giai_nhanh.MUC_TM else ""  # "" = tự động theo độ khó
    # Mức Tối đa = mô hình mạnh nhất ĐƯỢC PHÉP → xin quyền Claude kể cả khi chưa bật công tắc (không có quyền → pipeline hạ về Kỹ)
    quyen, thong_bao_claude = (None, "") if demo else _quyen_claude(u, dung_claude or muc_thong_minh == "toi_da")
    if quyen is None and not dung_claude:
        thong_bao_claude = ""
    du_lieu_anh = None
    if anh is not None and anh.filename:
        if not (anh.content_type or "").startswith("image/"):
            raise HTTPException(400, "File tải lên phải là ảnh (PNG, JPG...).")
        du_lieu_anh = await anh.read()
        if len(du_lieu_anh) > ANH_TOI_DA:
            raise HTTPException(400, "Ảnh quá lớn (tối đa 10 MB).")
    if not chuoi.strip() and not du_lieu_anh:
        raise HTTPException(400, "Hãy nhập chuỗi phản ứng hoặc tải ảnh đề.")
    tep_pdf = None  # "Tải lên PDF của bạn" (bài không có bản mở): chỉ đọc trong bộ nhớ cho lượt giải này – không lưu, không chia sẻ
    if pdf is not None and pdf.filename:
        du_lieu_pdf = await pdf.read(literature.TAI_TOI_DA + 1)
        await pdf.close()
        if len(du_lieu_pdf) > literature.TAI_TOI_DA:
            raise HTTPException(413, "File PDF quá lớn (tối đa 25 MB).")
        if du_lieu_pdf[:5] != b"%PDF-":
            raise HTTPException(415, "File tải lên không phải PDF.")
        tep_pdf = (du_lieu_pdf, (literature.tim_doi(pdf_doi) or [""])[0])
    if len(chuoi) > 4000:
        raise HTTPException(400, "Đề bài quá dài (tối đa 4000 ký tự).")
    try:
        lua_chon = tai_khoan.chon_khoa_giai(u, demo=demo)  # dùng thử: trừ lượt tại đây
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e
    job = pipeline.tao_job(u.id)
    try:
        db.tao_bai_giai(u.id, job.id, chuoi or "(ảnh đề)", muc_do == "hsg", demo, lua_chon.nguon)
    except Exception:
        if lua_chon.da_tru_luot:
            db.hoan_luot_thu(u.id)
        raise
    threading.Thread(
        target=_chay_bai, args=(job, lua_chon, u.id, chuoi, du_lieu_anh, hien_chrome, demo, muc_do, quyen, thong_bao_claude,
                                muc_thong_minh, dung_claude, tra_cuu_tai_lieu if tra_cuu_tai_lieu in ("", "0", "1") else "", tep_pdf, ai),
        daemon=True,
        name=f"job-{job.id}",
    ).start()
    return {"job_id": job.id, "nguon_key": lua_chon.nguon, "dung_claude": quyen is not None, "thong_bao_claude": thong_bao_claude, "ai": ai}


def ai_rieng_duoc_dung(u: NguoiDung | None) -> bool:
    """AI Riêng: admin luôn dùng được (để dạy / thử); người dùng khác chỉ khi admin bật "Mở cho người dùng"."""
    from .ai_rieng import kho as ai_rieng_kho

    cd = ai_rieng_kho.cai_dat()
    return u is not None and bool(cd["bat"]) and (tai_khoan.la_chu_app(u) or bool(cd["mo_cho_nguoi_dung"]))


def bat_dau_giai_san_tap(u: NguoiDung, chuoi: str, ai: str, tuy_chon: dict | None = None, dung_claude: bool = False) -> str:
    """Sân tập AI Riêng (admin): tạo một bài giải như /api/giai (cùng tiến trình SSE / kết quả / file) – trả job_id."""
    quyen, thong_bao = _quyen_claude(u, dung_claude)
    lua_chon = tai_khoan.chon_khoa_giai(u)
    job = pipeline.tao_job(u.id)
    job.tuy_chon = dict(tuy_chon or {})
    db.tao_bai_giai(u.id, job.id, chuoi, False, False, lua_chon.nguon)
    threading.Thread(target=_chay_bai, args=(job, lua_chon, u.id, chuoi, None, False, False, "", quyen, thong_bao, "", dung_claude, "0",
                                             None, ai), daemon=True, name=f"job-{job.id}").start()
    return job.id


@app.post("/api/3d", dependencies=[Depends(kiem_csrf), gioi_han("3d")])
def mo_hinh_3d(request: Request, van_ban: str = Form(""), smiles: str = Form(""), anh: UploadFile | None = File(None)):
    """Mục "Mô hình 3D": tên/công thức/SMILES hoặc ảnh → nhận diện (PubChem, AI) → cấu dạng 3D (SDF/XYZ).
    Không cần đăng nhập cho tên tiếng Anh/SMILES; bước AI dùng key theo tai_khoan.chon_khoa_3d."""
    du_lieu_anh = None
    if anh is not None and anh.filename:
        if not (anh.content_type or "").startswith("image/"):
            raise HTTPException(400, "File tải lên phải là ảnh (PNG, JPG...).")
        du_lieu_anh = anh.file.read()
        if len(du_lieu_anh) > ANH_TOI_DA:
            raise HTTPException(400, "Ảnh quá lớn (tối đa 10 MB).")
    if len(van_ban) > 500 or len(smiles) > 2000:
        raise HTTPException(400, "Nội dung nhập quá dài.")
    try:
        nguoi = nguoi_dung_hien_tai(request)
        khoa = tai_khoan.chon_khoa_3d(nguoi)
        with dung_khoa(khoa), dung_ke_hoach(KeHoachAI(loai="3d", user_id=nguoi.id if nguoi else None, buoc="nhan_dien")):
            return tao_mo_hinh_3d(van_ban, du_lieu_anh, smiles)
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e
    except LoiAI as e:
        raise HTTPException(400, str(e)) from e


class CoCheVao(BaseModel):
    van_ban: str = Field("", max_length=600)
    loai: str = Field("", max_length=20)
    chat: list[str] = Field(default_factory=list, max_length=12)
    san_pham: list[str] = Field(default_factory=list, max_length=12)
    dieu_kien: str = Field("", max_length=200)
    dung_claude: bool = False  # xin dùng Claude cho cơ chế – backend kiểm tra quyền (Key Claude / admin; mã Claude: theo co_che.py)
    tra_cuu_tai_lieu: bool | None = None  # tra cứu tài liệu khoa học (None = tự động: bật khi đề có DOI)


@app.post("/api/co-che", dependencies=[Depends(kiem_csrf), gioi_han("co_che")])
def co_che_phan_ung(vao: CoCheVao, u: NguoiDung = Depends(can_dang_nhap)):
    """Mục 05 "Cơ chế": phản ứng (chữ/SMILES, hoặc SMILES từ lời giải chuỗi) + loại (trống = tự nhận diện)
    → cơ chế (thư viện mẫu; ngoài danh sách thì AI viết, gắn nhãn cần kiểm tra) + SVG + CDXML."""
    if any(len(s) > 300 for s in vao.chat + vao.san_pham):
        raise HTTPException(400, "SMILES quá dài.")
    quyen, thong_bao_claude = _quyen_claude(u, vao.dung_claude)
    kh = KeHoachAI(loai="co_che", user_id=u.id, buoc="co_che", claude=quyen)
    if getattr(quyen, "moi_muc", False):  # Key Claude / admin bật "Dùng Claude" → cơ chế cũng bằng Claude
        kh.bac = "claude"
    if isinstance(quyen, key_claude.QuyenKey):
        quyen.loai = "co_che"
    thanh_cong = False
    try:
        # AI chỉ nhận diện/viết cơ chế: như mục 3D, không trừ lượt dùng thử; mức HSG theo quy tắc chọn mô hình (co_che.py)
        with dung_khoa(tai_khoan.chon_khoa_3d(u)), dung_ke_hoach(kh):
            van_ban, tl = vao.van_ban, None
            if vao.tra_cuu_tai_lieu or (vao.tra_cuu_tai_lieu is None and literature.tim_doi(vao.van_ban)):
                kh.buoc = "tra_cuu"
                tk = literature.rut_tu_khoa(vao.van_ban + (f" | SMILES: {', '.join(vao.chat + vao.san_pham)}" if vao.chat else ""))
                tl = literature.tra_cuu(tk, so_tu=1500)  # cơ chế: tối đa ~1.500 từ tài liệu
                if tl["khoi_prompt"]:
                    van_ban += ("\n\n(TÀI LIỆU THAM KHẢO để viết cơ chế – KHÔNG phải phản ứng cần nhận diện; dựa vào khi có, phần không có "
                                "trong tài liệu là suy luận)\n" + tl["khoi_prompt"])
                kh.buoc = "co_che"
            cc = tao_co_che(van_ban, vao.loai, vao.chat, vao.san_pham, vao.dieu_kien)
        thanh_cong = True
        hinh = ve_co_che(cc)
        return {
            "co_che": cc.to_json(), "ten": cc.ten, "loai": cc.loai, "nguon": cc.nguon, "canh_bao": cc.canh_bao,
            "ghi_chu": [b.ghi_chu for b in cc.buoc], "svg": hinh.svg, "cdxml": xuat_cdxml(cc, hinh),
            "dung_claude": kh.claude_thanh_cong, "thong_bao_claude": [t for t in [thong_bao_claude, *kh.thong_bao] if t],
            "tai_lieu": (tl or {}).get("bai", []),
            "tra_cuu_tl": {"bat": tl is not None, "so_bai": len((tl or {}).get("bai", [])), "token": (tl or {}).get("token_uoc_tinh", 0),
                           "thoi_gian": (tl or {}).get("thoi_gian", 0)},
        }
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e
    except LoiAI as e:
        raise HTTPException(400, str(e)) from e
    finally:
        _ket_thuc_claude(quyen, thanh_cong, kh)


class CoCheGiaiThichVao(BaseModel):
    van_ban: str = Field("", max_length=600)
    ten: str = Field("", max_length=200)
    loai: str = Field("", max_length=100)
    buoc: list[dict] = Field(default_factory=list)


@app.post("/api/co-che/giai-thich", dependencies=[Depends(kiem_csrf), gioi_han("co_che")])
def giai_thich_co_che(vao: CoCheGiaiThichVao, u: NguoiDung = Depends(can_dang_nhap)):
    """Tạo giải thích chi tiết chuyên sâu từng bước cơ chế (chạy ngầm sau khi đã xuất hình)."""
    return co_che.giai_thich_chi_tiet_co_che(vao.van_ban, vao.ten, vao.loai, vao.buoc)


@app.post("/api/co-che/doc-anh", dependencies=[Depends(kiem_csrf), gioi_han("co_che_anh")])
def doc_anh_co_che(anh: list[UploadFile] = File(...), van_ban: str = Form(""), dung_claude: bool = Form(False),
                   u: NguoiDung = Depends(can_dang_nhap)):
    """Mục 05 – nhập đề bằng ẢNH (1–4 ảnh, + chữ kèm): AI có thị giác đọc → JSON đã kiểm SMILES bằng RDKit → giao diện cho người dùng
    XÁC NHẬN / sửa rồi mới gọi /api/co-che để vẽ. Ảnh chỉ đọc vào bộ nhớ, đóng (xoá tệp tạm của máy chủ web) ngay, không lưu lại."""
    if len(anh) > co_che_anh.SO_ANH_TOI_DA:
        raise HTTPException(400, {"loi": f"Tối đa {co_che_anh.SO_ANH_TOI_DA} ảnh mỗi lần.", "ma": "qua_nhieu_anh"})
    if len(van_ban) > 600:
        raise HTTPException(400, "Nội dung nhập quá dài (tối đa 600 ký tự).")
    ds_anh = []
    for f in anh:
        try:
            ds_anh.append((f.file.read(co_che_anh.ANH_TOI_DA + 1), f.filename or "ảnh dán"))
        finally:
            f.file.close()
    quyen, thong_bao_claude = _quyen_claude(u, dung_claude)
    kh = KeHoachAI(loai="co_che_anh", user_id=u.id, buoc="doc_anh", claude=quyen)
    if isinstance(quyen, key_claude.QuyenKey):
        quyen.loai = "co_che_anh"
    tien_trinh: list[str] = []
    thanh_cong = False
    try:
        with dung_khoa(tai_khoan.chon_khoa_3d(u)), dung_ke_hoach(kh):
            kq = co_che_anh.doc_anh(ds_anh, van_ban, tien_trinh.append)
        thanh_cong = True
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e
    except co_che_anh.LoiAnh as e:
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma}) from e
    except LoiAI as e:
        raise HTTPException(400, str(e)) from e
    finally:
        ds_anh.clear()
        _ket_thuc_claude(quyen, thanh_cong, kh)
    return {**kq, "tien_trinh": tien_trinh, "dung_claude": kh.claude_thanh_cong,
            "thong_bao_claude": [t for t in [thong_bao_claude, *kh.thong_bao] if t]}


class ChatCoCheVao(BaseModel):
    chuoi: str = Field("", max_length=300)


@app.post("/api/co-che/chat", dependencies=[Depends(kiem_csrf), gioi_han("3d")])
def chat_co_che(vao: ChatCoCheVao, u: NguoiDung = Depends(can_dang_nhap)):
    """Bước xác nhận ảnh đề – nút "Sửa" một chất: SMILES hoặc tên (Việt/Anh) → SMILES + hình vẽ RDKit (như mục 3D)."""
    try:
        with dung_khoa(tai_khoan.chon_khoa_3d(u)), dung_ke_hoach(KeHoachAI(loai="co_che_anh", user_id=u.id, buoc="sua_chat", bac="lite")):
            return co_che_anh.nhan_chat(vao.chuoi)
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e
    except co_che_anh.LoiAnh as e:
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma}) from e
    except LoiAI as e:
        raise HTTPException(400, str(e)) from e


class LuotChatSinhHoc(BaseModel):
    vai: str = Field("nguoi", max_length=10)
    chu: str = Field("", max_length=1200)


class BoPhanSinhHoc(BaseModel):
    id: str = Field(..., max_length=40)
    vi: str = Field("", max_length=80)


class HoiSinhHocVao(BaseModel):
    cau_hoi: str = Field(..., min_length=1, max_length=500)
    lich_su: list[LuotChatSinhHoc] = Field(default_factory=list, max_length=8)
    bo_phan: list[BoPhanSinhHoc] = Field(default_factory=list, max_length=240)
    duong: list[str] = Field(default_factory=list, max_length=24)
    vi_mo: list[str] = Field(default_factory=list, max_length=12)
    co_quan: list[str] = Field(default_factory=list, max_length=12)


@app.post("/api/sinh-hoc/hoi", dependencies=[Depends(kiem_csrf), gioi_han("sinh_hoc")])
def hoi_sinh_hoc(vao: HoiSinhHocVao, u: NguoiDung = Depends(can_dang_nhap)):
    """AI Bio-Tutor của Phòng Lab Sinh học 3D qua máy chủ: key AI chỉ ở server, trình duyệt nhận {answer_html, action}."""
    khoa = tai_khoan.chon_khoa_sinh_hoc(u)
    if khoa.nguon == "khong_co":
        raise HTTPException(503, {"loi": "Máy chủ chưa có key AI – đang dùng AI nội bộ.", "ma": "khong_co_ai"})
    try:
        with dung_khoa(khoa), dung_ke_hoach(KeHoachAI(loai="sinh_hoc", user_id=u.id, buoc="bio_tutor", bac="flash")):
            return sinh_hoc_ai.hoi(vao.cau_hoi, [m.model_dump() for m in vao.lich_su], [b.model_dump() for b in vao.bo_phan],
                                   vao.duong, vao.vi_mo, vao.co_quan)
    except LoiAI as e:
        raise HTTPException(502, {"loi": str(e), "ma": "loi_ai"}) from e


class GiaiThichAnhVao(BaseModel):
    anh: str = Field(..., min_length=10)
    mesh_names: list[str] = Field(default_factory=list, max_length=300)
    processes: list[str] = Field(default_factory=list, max_length=50)


def _xu_ly_giai_thich_anh(vao: GiaiThichAnhVao, request: Request):
    u = nguoi_dung_hien_tai(request)
    khoa = tai_khoan.chon_khoa_doc_anh(u)
    if khoa.nguon == "khong_co":
        raise HTTPException(503, {"loi": "Máy chủ chưa có key AI hỗ trợ đọc ảnh – hãy cấu hình Gemini API key.", "ma": "khong_co_ai"})
    try:
        with dung_khoa(khoa), dung_ke_hoach(KeHoachAI(loai="sinh_hoc_anh", user_id=u.id if u else None, buoc="giai_thich_anh", bac="flash")):
            return sinh_hoc_ai.giai_thich_anh(vao.anh, vao.mesh_names, vao.processes)
    except LoiAI as e:
        raise HTTPException(502, {"loi": str(e), "ma": "loi_ai"}) from e


@app.post("/api/sinh-hoc/giai-thich-anh", dependencies=[Depends(kiem_csrf), gioi_han("sinh_hoc_anh")])
def giai_thich_anh_endpoint(vao: GiaiThichAnhVao, request: Request):
    """Giải thích thuật ngữ và quá trình sinh học từ ảnh chụp sách giáo khoa / sơ đồ 3D."""
    return _xu_ly_giai_thich_anh(vao, request)


@app.post("/api/explain-image", dependencies=[Depends(kiem_csrf), gioi_han("sinh_hoc_anh")])
def explain_image_endpoint(vao: GiaiThichAnhVao, request: Request):
    """Bí danh của /api/sinh-hoc/giai-thich-anh theo đặc tả yêu cầu."""
    return _xu_ly_giai_thich_anh(vao, request)


class PhanHoiSinhHocVao(BaseModel):
    cau_hoi: str = Field(..., max_length=500)
    cau_tra_loi: str = Field(..., max_length=6000)
    hai_long: int | None = Field(default=None, ge=0, le=1)
    vote: str | None = Field(default=None, max_length=20)
    ly_do: str = Field(default="", max_length=100)
    chi_tiet: str = Field(default="", max_length=1000)


@app.post("/api/sinh-hoc/phan-hoi", dependencies=[Depends(kiem_csrf)])
def phan_hoi_sinh_hoc(vao: PhanHoiSinhHocVao, request: Request):
    """Lưu đánh giá 👍 / 👎 của học sinh đối với câu trả lời AI Bio-Tutor."""
    u = nguoi_dung_hien_tai(request)
    hl = vao.hai_long
    if hl is None:
        hl = 1 if (vao.vote in ("tot", "1", "like")) else 0
    with db._ket_noi() as conn:
        conn.execute("""
            INSERT INTO sinh_hoc_phan_hoi (ts, user_id, cau_hoi, cau_tra_loi, hai_long, ly_do, chi_tiet)
            VALUES (?, ?, ?, ?, ?, ?, ?)
        """, (time.time(), u.id if u else None, vao.cau_hoi, vao.cau_tra_loi, hl, vao.ly_do, vao.chi_tiet))
    return {"ok": True, "status": "ok", "message": "Cảm ơn bạn đã đóng góp phản hồi!"}




@app.get("/api/tien-trinh/{job_id}")
async def tien_trinh(job_id: str, u: NguoiDung = Depends(can_dang_nhap)):
    job = _job(job_id, u)

    async def luong():
        i, cho = 0, 0.0
        while True:
            while i < len(job.su_kien):
                yield f"data: {json.dumps(job.su_kien[i], ensure_ascii=False)}\n\n"
                i += 1
                cho = 0.0
            if job.xong and i >= len(job.su_kien):
                yield "event: ket_thuc\ndata: {}\n\n"
                return
            await asyncio.sleep(0.25)
            cho += 0.25
            if cho >= 15:  # giữ kết nối
                yield ": ping\n\n"
                cho = 0.0

    return StreamingResponse(luong(), media_type="text/event-stream", headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"})


@app.post("/api/phuong-trinh/{job_id}", dependencies=[Depends(kiem_csrf), gioi_han("phuong_trinh")])
def phuong_trinh_chi_tiet(job_id: str, u: NguoiDung = Depends(can_dang_nhap)):
    """Nút "Xem phương trình chi tiết": chỉ lúc này mới gọi AI viết phương trình (Gemini Flash, ít suy nghĩ); lưu lại để lần sau không gọi."""
    _kiem_chu(job_id, u)
    job = pipeline.JOBS.get(job_id)
    kq = job.ket_qua if job is not None and job.ket_qua else (db.ket_qua_da_luu(job_id, u.id) or {}).get("ket_qua")
    if not kq:
        raise HTTPException(404, "Không tìm thấy kết quả của bài này.")
    if kq.get("phuong_trinh"):
        return {"phuong_trinh": kq["phuong_trinh"], "mo_hinh": kq.get("mo_hinh_phuong_trinh", ""), "da_luu": True}
    if not kq.get("gon"):
        raise HTTPException(400, "Bài giải theo định dạng cũ đã có phương trình trong ảnh đáp án.")
    try:
        with dung_khoa(tai_khoan.chon_khoa_3d(u)), dung_ke_hoach(KeHoachAI(loai="phuong_trinh", user_id=u.id, job_id=job_id,
                                                                           buoc="phuong_trinh", bac="flash")):
            ds, mo_hinh = giai_nhanh.phuong_trinh_chi_tiet(kq["gon"], kq.get("de_bai", ""))
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e
    except LoiAI as e:
        raise HTTPException(400, str(e)) from e
    kq["phuong_trinh"], kq["mo_hinh_phuong_trinh"] = ds, mo_hinh
    db.cap_nhat_ket_qua(job_id, kq)
    return {"phuong_trinh": ds, "mo_hinh": mo_hinh, "da_luu": False}


@app.get("/api/ket-qua/{job_id}")
def ket_qua(job_id: str, u: NguoiDung = Depends(can_dang_nhap)):
    _kiem_chu(job_id, u)
    job = pipeline.JOBS.get(job_id)
    if job is None:  # server đã khởi động lại → lấy từ lịch sử đã lưu
        da_luu = db.ket_qua_da_luu(job_id, u.id)
        if da_luu is None:
            raise HTTPException(404, "Không tìm thấy bài giải này (có thể server đã khởi động lại).")
        if da_luu["loi"]:
            raise HTTPException(500, da_luu["loi"])
        if da_luu["ket_qua"] is None:
            raise HTTPException(409, "Bài này chưa giải xong (server đã khởi động lại giữa chừng) – hãy giải lại.")
        return da_luu["ket_qua"]
    if job.loi:
        raise HTTPException(500, {"loi": job.loi, "ma": job.loai_loi or "loi", **(job.ket_qua_loi or {})})
    if not job.xong or job.ket_qua is None:
        raise HTTPException(409, "Bài đang được giải, vui lòng đợi.")
    return job.ket_qua


@app.get("/api/tep/{job_id}/{duong_dan:path}")
def tep(job_id: str, duong_dan: str, u: NguoiDung = Depends(can_dang_nhap)):
    _kiem_chu(job_id, u)
    goc = (OUTPUT_DIR / job_id).resolve()
    f = (goc / duong_dan).resolve()
    if not f.is_relative_to(goc) or not f.is_file():
        raise HTTPException(404, "Không tìm thấy file.")
    return FileResponse(f)


@app.get("/api/tai/{job_id}/{loai}")
def tai(job_id: str, loai: str, u: NguoiDung = Depends(can_dang_nhap)):
    _kiem_chu(job_id, u)
    ten = {"png": "dap_an.png", "zip": "ket_qua.zip"}.get(loai)
    f = (OUTPUT_DIR / job_id / ten) if ten else None
    if f is None or not f.is_file():
        raise HTTPException(404, "Chưa có file để tải.")
    return FileResponse(f, filename=f"giai_chuoi_{job_id}_{ten}")


@app.post("/api/chemdraw/{job_id}", dependencies=[Depends(kiem_csrf), gioi_han("chemdraw")])
def chemdraw(job_id: str, u: NguoiDung = Depends(can_dang_nhap)):
    _kiem_chu(job_id, u)
    if not tai_khoan.la_chu_app(u):  # lệnh này mở ChemDraw trên MÁY CHẠY APP, không phải máy người dùng
        raise HTTPException(403, "Nút này chỉ mở ChemDraw trên máy chạy app. Hãy tải ZIP rồi mở file so_do.cdxml bằng ChemDraw trên máy bạn.")
    d = OUTPUT_DIR / job_id
    if not d.is_dir():
        raise HTTPException(404, "Không tìm thấy bài giải này.")
    f = d / "so_do.cdxml"
    if not f.exists():
        f = d / "tat_ca_chat.sdf"
    return mo_bang_chemdraw(f)


# =====================================================================
# TÌM CẤU TẠO CHẤT (TỪ ĐỀ DÀI)
# =====================================================================

@app.get("/api/tim-cau-tao/mau")
@app.get("/api/tim-chat/mau")
def lay_de_mau_tim_chat():
    """Lấy danh sách 10 đề mẫu tìm chất phong phú chuẩn THPT & HSG."""
    return {"de_mau": tim_chat.DE_MAU_TIM_CHAT}


@app.post("/api/tim-cau-tao/ocr", dependencies=[Depends(kiem_csrf), gioi_han("co_che_anh")])
@app.post("/api/tim-chat/ocr", dependencies=[Depends(kiem_csrf), gioi_han("co_che_anh")])
async def ocr_anh_de_tim_chat(anh: UploadFile = File(...), u: NguoiDung = Depends(can_dang_nhap)):
    """Đọc ảnh chụp đề bài Hoá học bằng AI Vision để đưa văn bản vào ô đề."""
    if not (anh.content_type or "").startswith("image/"):
        raise HTTPException(400, "File tải lên phải là ảnh (PNG, JPG, WEBP...).")
    du_lieu = await anh.read()
    if len(du_lieu) > ANH_TOI_DA:
        raise HTTPException(400, "Ảnh quá lớn (tối đa 10 MB).")
    try:
        lua_chon = tai_khoan.chon_khoa_giai(u)
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e

    try:
        with dung_khoa(lua_chon.khoa), dung_ke_hoach(KeHoachAI(loai="ocr_tim_chat", user_id=u.id, bac="flash")):
            van_ban = tim_chat.doc_anh_de_bai(du_lieu, mime_type=anh.content_type or "image/jpeg")
            return {"de_bai": van_ban}
    except LoiAI as e:
        raise HTTPException(400, str(e)) from e


class YeuCauGiaiTimChat(BaseModel):
    de_bai: str = Field(..., min_length=5, max_length=5000)
    trac_nghiem: str = Field("", max_length=2000)
    cau_hoi: str = Field("", max_length=1000)
    dung_claude: bool = Field(False)
    dung_cache: bool = Field(True)
    streaming: bool = Field(False)


@app.post("/api/tim-cau-tao/giai", dependencies=[Depends(kiem_csrf), gioi_han("giai")])
@app.post("/api/tim-chat/giai", dependencies=[Depends(kiem_csrf), gioi_han("giai")])
async def giai_tim_chat(req: YeuCauGiaiTimChat, u: NguoiDung = Depends(can_dang_nhap)):
    """Quy trình 5 bước tìm cấu tạo chất: trích dữ kiện -> tính code -> suy luận -> kiểm tra chéo -> kết luận."""
    quyen, _ = _quyen_claude(u, req.dung_claude)
    try:
        lua_chon = tai_khoan.chon_khoa_giai(u)
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e

    kh = KeHoachAI(loai="tim_cau_tao", user_id=u.id, claude=quyen)

    if req.streaming:
        hang_doi = asyncio.Queue()
        loop = asyncio.get_running_loop()

        def _emit(giai_doan: str, thong_diep: str, du_lieu: dict):
            loop.call_soon_threadsafe(
                hang_doi.put_nowait,
                {"giai_doan": giai_doan, "thong_diep": thong_diep, "du_lieu": du_lieu},
            )

        def _chay_thread():
            try:
                with dung_khoa(lua_chon.khoa), dung_ke_hoach(kh):
                    kq = tim_chat.chay_pipeline_tim_chat(
                        de_bai=req.de_bai,
                        trac_nghiem=req.trac_nghiem,
                        cau_hoi=req.cau_hoi,
                        dung_cache=req.dung_cache,
                        emit=_emit,
                        uu_tien_claude=quyen is not None,
                    )
                    loop.call_soon_threadsafe(
                        hang_doi.put_nowait,
                        {"giai_doan": "xong", "ket_qua": kq.model_dump()},
                    )
            except Exception as e:
                log.exception("Lỗi trong pipeline tìm cấu tạo chất")
                loop.call_soon_threadsafe(
                    hang_doi.put_nowait,
                    {"giai_doan": "loi", "thong_diep": str(e)},
                )
            finally:
                loop.call_soon_threadsafe(hang_doi.put_nowait, None)

        threading.Thread(target=_chay_thread, daemon=True).start()

        async def _event_stream():
            while True:
                item = await hang_doi.get()
                if item is None:
                    break
                yield f"data: {json.dumps(item, ensure_ascii=False)}\n\n"

        return StreamingResponse(_event_stream(), media_type="text/event-stream")

    # Chạy đồng bộ (non-streaming)
    try:
        with dung_khoa(lua_chon.khoa), dung_ke_hoach(kh):
            kq = tim_chat.chay_pipeline_tim_chat(
                de_bai=req.de_bai,
                trac_nghiem=req.trac_nghiem,
                cau_hoi=req.cau_hoi,
                dung_cache=req.dung_cache,
                uu_tien_claude=quyen is not None,
            )
            return kq.model_dump()
    except LoiAI as e:
        raise HTTPException(400, str(e)) from e
    except Exception as e:
        log.exception("Lỗi khi tìm cấu tạo chất")
        raise HTTPException(500, f"Lỗi xử lý: {e}") from e


class XuatAnhVao(BaseModel):
    ket_qua: dict


@app.post("/api/tim-cau-tao/xuat-anh", dependencies=[Depends(kiem_csrf)])
@app.post("/api/tim-chat/xuat-anh", dependencies=[Depends(kiem_csrf)])
def xuat_anh_tim_chat(vao: XuatAnhVao, _u: NguoiDung = Depends(can_dang_nhap)):
    """Xuất toàn bộ lời giải thành ảnh PNG chất lượng cao chuẩn Giấy & Mực."""
    try:
        kq_obj = tim_chat.KetQuaTimChat(**vao.ket_qua)
        png_bytes = tim_chat.tao_anh_loi_giai(kq_obj)
        from fastapi.responses import Response
        return Response(
            content=png_bytes,
            media_type="image/png",
            headers={"Content-Disposition": 'attachment; filename="loi_giai_tim_cau_tao.png"'},
        )
    except Exception as e:
        log.exception("Lỗi khi xuất ảnh lời giải tìm chất")
        raise HTTPException(400, f"Không thể xuất ảnh: {e}") from e
