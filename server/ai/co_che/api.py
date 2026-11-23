"""Điểm cuối máy chủ cơ chế (APIRouter /co-che), dùng trong may_chu.py (cổng 8010).

    GET  /co-che/suc-khoe        mô hình tại máy, RAG, số bài
    GET  /co-che/bai             danh sách bài (mã, tên, loại, lớp, đề)
    GET  /co-che/bai/{ma}        gói .verapack của bài (mã hóa, app giải trong bộ nhớ)
    GET  /co-che/chuoi-mau       3 chuỗi mẫu chạy được khi không có mạng
    POST /co-che/giai-thich      {"cau_hoi", "ma_bai"?} → lời giải thích có trích nguồn
    POST /co-che/giai-chuoi      {"chuoi": "C2H4 -> C2H5OH -> CH3CHO -> CH3COOH"} → từng bước + mã cơ chế nếu khớp mẫu
    POST /co-che/mo-hinh-3d      {"van_ban": "ethanol" | "CH3COOH" | "CCO"} → nguyên tử, liên kết 3D (mục Mô hình 3D)
    GET  /co-che/mo-hinh-3d/mau  tên chất mẫu
    POST /co-che/giai-de         ĐỀ MỚI → cơ chế (giai_de.py): Claude viết, RDKit kiểm (nhờ AI sửa ≤ 3 lần), dựng 3D + giấy.
                                 JSON {"van_ban", "anh_base64"?, "loai"?} hoặc multipart (van_ban, loai, anh = tệp ảnh).
                                 Ảnh ≤ 8 MB (JPG/PNG/WEBP/HEIC), chờ tối đa 300 s (VERA_GIAI_DE_CHO). Trả JSON THƯỜNG cùng khung gói
                                 .verapack {"phien_ban","ma","bai","kich_ban_3d","so_do_giay"} + "co_che","doc_anh","thong_tin".
                                 Lỗi: {"detail": {"thong_diep","ma","chi_tiet"}} mã 400/413/415/422/503 (chưa có ANTHROPIC_API_KEY)/504.
    POST /co-che/giai-thich-chi-tiet  {"ten","de","loai","buoc":[{"ten","ghi_chu"}]} → tổng quan + từng bước + lưu ý (hoi_dap.py)
    POST /co-che/hoi-dap         {"cau_hoi","ma_bai"?,"ten","de","buoc","lich_su":[{"vai","noi_dung"}]} → trả lời theo bài đang xem
    POST /co-che/bao-sai         {"ma_bai"?,"ten","mo_ta","che_do"?,"buoc"?,"nguon"?} → ghi data/bao_sai/<nguon>.jsonl
"""
from __future__ import annotations

import asyncio
import base64
import binascii
import json
import os
import re
from dataclasses import asdict

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import Response
from pydantic import BaseModel, Field

from . import dong_goi, giai_chuoi, giai_de, giai_thich, hoi_dap, mo_hinh_3d, tri_thuc
from .duong_dan import thu_muc_build
from ..cau_hinh_may import thong_so

router = APIRouter(prefix="/co-che", tags=["co-che"])


class YeuCauGiaiThich(BaseModel):
    cau_hoi: str = Field(min_length=1, max_length=800)
    ma_bai: str = Field(default="", max_length=40)


class YeuCauChuoi(BaseModel):
    chuoi: str = Field(default="", max_length=400)
    anh_base64: str = Field(default="", max_length=giai_de.ANH_TOI_DA * 4 // 3 + 1024)   # ảnh đề chuỗi: AI chép chuỗi rồi giải như chữ
    muc_do: str = Field(default="tu_dong", max_length=20)          # tu_dong | trung_binh | hsg (tuỳ chọn của app, ghi vào kết quả)
    muc_thong_minh: str = Field(default="can_bang", max_length=20)
    tra_cuu: bool = False


class YeuCauGiaiThichChiTiet(BaseModel):
    ten: str = Field(default="", max_length=300)
    de: str = Field(default="", max_length=4000)
    loai: str = Field(default="", max_length=60)
    buoc: list[dict] = Field(default_factory=list, max_length=30)


class YeuCauHoiDap(BaseModel):
    cau_hoi: str = Field(min_length=1, max_length=800)
    ma_bai: str = Field(default="", max_length=40)
    ten: str = Field(default="", max_length=300)
    de: str = Field(default="", max_length=4000)
    buoc: list[dict] = Field(default_factory=list, max_length=30)
    lich_su: list[dict] = Field(default_factory=list, max_length=40)


class YeuCauBaoSai(BaseModel):
    ma_bai: str = Field(default="", max_length=60)
    ten: str = Field(default="", max_length=300)
    mo_ta: str = Field(min_length=1, max_length=2000)
    che_do: str = Field(default="", max_length=20)
    buoc: int | None = None
    nguon: str = Field(default="co_che", pattern=r"^(co_che|chuoi|mo_hinh_3d)$")


class YeuCau3D(BaseModel):
    van_ban: str = Field(default="", max_length=200)
    anh_base64: str = Field(default="", max_length=giai_de.ANH_TOI_DA * 4 // 3 + 1024)


def _muc_luc() -> list[dict]:
    try:
        return json.loads((thu_muc_build() / "muc_luc.json").read_text(encoding="utf-8"))["bai"]
    except (OSError, ValueError, KeyError):
        return []


@router.get("/suc-khoe")
def suc_khoe() -> dict:
    ts = thong_so()
    kh = tri_thuc.kho()
    return {"ok": True, "mo_hinh": ts.mo_hinh, "muc": ts.muc, "ram_gb": ts.ram_gb, "so_bai": len(_muc_luc()),
            "rag": {"so_doan": len(kh.doan), "vector": kh.co_vector}}


@router.get("/may")
def thong_so_may() -> dict:
    return asdict(thong_so())


@router.get("/bai")
def danh_sach_bai() -> list[dict]:
    return [{k: b.get(k) for k in ("ma", "ten", "loai", "lop", "de", "so_buoc", "nhanh_sai")} for b in _muc_luc()]


@router.get("/bai/{ma}")
def goi_bai(ma: str) -> Response:
    if not re.fullmatch(r"[a-z0-9_]{1,40}", ma) or ma not in {b["ma"] for b in _muc_luc()}:
        raise HTTPException(404, "Không có bài này")
    try:
        goi, dau = dong_goi.dong_goi_bai(ma, dong_goi.doc_khoa())
    except (dong_goi.LoiGoi, OSError) as e:
        raise HTTPException(503, f"Máy chủ chưa đóng gói được bài: {e}") from e
    dong_goi.ghi_nhat_ky(ma, dau, "may_chu")
    return Response(goi, media_type="application/octet-stream", headers={"Content-Disposition": f'attachment; filename="{ma}.verapack"'})


@router.get("/chuoi-mau")
def chuoi_mau() -> list[str]:
    return giai_chuoi.CHUOI_MAU


@router.post("/giai-thich")
async def giai_thich_co_che(yc: YeuCauGiaiThich) -> dict:
    return await giai_thich.giai_thich(yc.cau_hoi, yc.ma_bai)


@router.post("/giai-chuoi")
async def giai_chuoi_api(yc: YeuCauChuoi) -> dict:
    chuoi, doc_tu_anh = yc.chuoi.strip(), ""
    if not chuoi and yc.anh_base64:
        b64 = yc.anh_base64.split(",", 1)[1] if yc.anh_base64.startswith("data:") else yc.anh_base64
        try:
            anh = base64.b64decode(b64, validate=True)
        except (binascii.Error, ValueError):
            raise HTTPException(400, "anh_base64 không hợp lệ.") from None
        try:
            chuoi = doc_tu_anh = await hoi_dap.doc_chuoi_tu_anh(anh)
        except giai_de.LoiGiaiDe as e:
            raise HTTPException(e.status, e.thong_diep) from e
    if len(chuoi) < 3:
        raise HTTPException(422, "Chưa có chuỗi: gõ chuỗi (A -> B -> C) hoặc chọn ảnh đề.")
    kq = await giai_chuoi.giai(chuoi)
    if not kq.get("loi"):
        kq["tuy_chon"] = {"muc_do": yc.muc_do, "muc_thong_minh": yc.muc_thong_minh, "tra_cuu": yc.tra_cuu}
        if doc_tu_anh:
            kq["chuoi_doc_tu_anh"] = doc_tu_anh
    if kq.get("loi"):
        raise HTTPException(422, kq["loi"])
    return kq


@router.get("/mo-hinh-3d/mau")
def mau_3d() -> list[str]:
    return mo_hinh_3d.MAU


@router.post("/mo-hinh-3d")
async def mo_hinh_3d_api(yc: YeuCau3D) -> dict:
    if yc.anh_base64.strip():
        b64 = yc.anh_base64.split(",", 1)[1] if yc.anh_base64.startswith("data:") else yc.anh_base64
        try:
            anh = base64.b64decode(b64, validate=True)
        except (binascii.Error, ValueError):
            raise HTTPException(400, "anh_base64 không hợp lệ.") from None
        try:
            doc = await hoi_dap.doc_chat_tu_anh(anh)
            if doc.get("smiles"):
                return mo_hinh_3d.dung_3d(doc["smiles"], doc.get("ten") or doc["smiles"], "ai_anh",
                                          {"cach_nhan_dien": "AI nhận diện từ ảnh"})
            return mo_hinh_3d.tao_mo_hinh(doc.get("ten") or "")
        except giai_de.LoiGiaiDe as e:
            raise HTTPException(e.status, e.thong_diep) from e
        except mo_hinh_3d.LoiMoHinh as e:
            raise HTTPException(422, str(e)) from e
    if not yc.van_ban.strip():
        raise HTTPException(422, "Hãy nhập tên, công thức hoặc SMILES của chất, hoặc chọn ảnh.")
    try:
        return mo_hinh_3d.tao_mo_hinh(yc.van_ban)
    except mo_hinh_3d.LoiMoHinh as e:
        raise HTTPException(422, str(e)) from e


THAN_GIAI_DE_TOI_DA = giai_de.ANH_TOI_DA * 4 // 3 + 64 * 1024     # ảnh base64 + chữ


def _loi_giai_de(e: giai_de.LoiGiaiDe) -> HTTPException:
    return HTTPException(e.status, {"thong_diep": e.thong_diep, "ma": e.ma, "chi_tiet": e.chi_tiet})


@router.post("/giai-de")
async def giai_de_api(request: Request) -> dict:
    try:
        dai = int(request.headers.get("content-length") or 0)
    except ValueError:
        dai = 0
    if dai > THAN_GIAI_DE_TOI_DA:
        raise _loi_giai_de(giai_de.LoiGiaiDe(f"Yêu cầu quá lớn – ảnh tối đa {giai_de.ANH_TOI_DA // 1024 // 1024} MB.", "anh_qua_lon", 413))
    kieu = request.headers.get("content-type", "")
    anh: bytes | None = None
    if kieu.startswith("multipart/form-data"):
        async with request.form() as form:
            van_ban, loai = str(form.get("van_ban") or ""), str(form.get("loai") or "")
            muc = form.get("muc_tu_duy") or 1
            tep = form.get("anh")
            if tep is not None and hasattr(tep, "read"):
                anh = await tep.read(giai_de.ANH_TOI_DA + 1)
    else:
        than = await request.body()
        if len(than) > THAN_GIAI_DE_TOI_DA:
            raise _loi_giai_de(giai_de.LoiGiaiDe("Yêu cầu quá lớn.", "anh_qua_lon", 413))
        try:
            d = json.loads(than or b"{}")
            if not isinstance(d, dict):
                raise ValueError
        except ValueError:
            raise _loi_giai_de(giai_de.LoiGiaiDe("Thân yêu cầu không phải JSON.", "sai_json", 400)) from None
        van_ban, loai = str(d.get("van_ban") or ""), str(d.get("loai") or "")
        muc = d.get("muc_tu_duy", 1)
        b64 = str(d.get("anh_base64") or "")
        if b64:
            b64 = b64.split(",", 1)[1] if b64.startswith("data:") else b64
            try:
                anh = base64.b64decode(b64, validate=True)
            except (binascii.Error, ValueError):
                raise _loi_giai_de(giai_de.LoiGiaiDe("anh_base64 không hợp lệ.", "sai_base64", 400)) from None
    if len(van_ban) > giai_de.VAN_BAN_TOI_DA:
        raise _loi_giai_de(giai_de.LoiGiaiDe(f"Đề quá dài (tối đa {giai_de.VAN_BAN_TOI_DA} ký tự).", "de_qua_dai", 400))
    m = giai_de.muc_tu_duy(muc)
    giai_de.muc_hien_tai.set(max(0, min(3, int(muc) if str(muc).isdigit() else 1)))
    cho = min(float(os.environ.get("VERA_GIAI_DE_CHO") or giai_de.CHO_MAC_DINH), m["cho"])   # ≤ 3 phút cho một cơ chế
    try:
        return await asyncio.wait_for(giai_de.giai_de(van_ban, anh or None, loai), cho)
    except giai_de.LoiGiaiDe as e:
        raise _loi_giai_de(e) from e
    except asyncio.TimeoutError as e:
        raise _loi_giai_de(giai_de.LoiGiaiDe(f"Giải đề quá {cho:.0f} giây – thử lại hoặc tách đề nhỏ hơn.", "qua_gio", 504)) from e


def _loi_hoi_dap(e: giai_de.LoiGiaiDe) -> HTTPException:
    return HTTPException(e.status, {"thong_diep": e.thong_diep, "ma": e.ma})


@router.post("/giai-thich-chi-tiet")
async def giai_thich_chi_tiet_api(yc: YeuCauGiaiThichChiTiet) -> dict:
    try:
        return await asyncio.wait_for(hoi_dap.giai_thich_chi_tiet(yc.ten, yc.de, yc.loai, yc.buoc), 150)
    except giai_de.LoiGiaiDe as e:
        raise _loi_hoi_dap(e) from e
    except asyncio.TimeoutError as e:
        raise HTTPException(504, {"thong_diep": "AI trả lời quá lâu – thử lại.", "ma": "qua_gio"}) from e


@router.post("/hoi-dap")
async def hoi_dap_api(yc: YeuCauHoiDap) -> dict:
    try:
        return await asyncio.wait_for(hoi_dap.hoi_dap(yc.cau_hoi, yc.ten, yc.de, yc.buoc, yc.lich_su, yc.ma_bai), 150)
    except giai_de.LoiGiaiDe as e:
        raise _loi_hoi_dap(e) from e
    except asyncio.TimeoutError as e:
        raise HTTPException(504, {"thong_diep": "AI trả lời quá lâu – thử lại.", "ma": "qua_gio"}) from e


@router.post("/bao-sai")
def bao_sai_api(yc: YeuCauBaoSai) -> dict:
    try:
        return hoi_dap.bao_sai(yc.ma_bai, yc.ten, yc.mo_ta, yc.che_do, yc.buoc, yc.nguon)
    except giai_de.LoiGiaiDe as e:
        raise _loi_hoi_dap(e) from e
