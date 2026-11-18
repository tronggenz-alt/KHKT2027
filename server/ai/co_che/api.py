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

from . import dong_goi, giai_chuoi, giai_de, giai_thich, mo_hinh_3d, tri_thuc
from .duong_dan import thu_muc_build
from ..cau_hinh_may import thong_so

router = APIRouter(prefix="/co-che", tags=["co-che"])


class YeuCauGiaiThich(BaseModel):
    cau_hoi: str = Field(min_length=1, max_length=800)
    ma_bai: str = Field(default="", max_length=40)


class YeuCauChuoi(BaseModel):
    chuoi: str = Field(min_length=3, max_length=400)


class YeuCau3D(BaseModel):
    van_ban: str = Field(min_length=1, max_length=200)


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
    kq = await giai_chuoi.giai(yc.chuoi)
    if kq.get("loi"):
        raise HTTPException(422, kq["loi"])
    return kq


@router.get("/mo-hinh-3d/mau")
def mau_3d() -> list[str]:
    return mo_hinh_3d.MAU


@router.post("/mo-hinh-3d")
def mo_hinh_3d_api(yc: YeuCau3D) -> dict:
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
        b64 = str(d.get("anh_base64") or "")
        if b64:
            b64 = b64.split(",", 1)[1] if b64.startswith("data:") else b64
            try:
                anh = base64.b64decode(b64, validate=True)
            except (binascii.Error, ValueError):
                raise _loi_giai_de(giai_de.LoiGiaiDe("anh_base64 không hợp lệ.", "sai_base64", 400)) from None
    if len(van_ban) > giai_de.VAN_BAN_TOI_DA:
        raise _loi_giai_de(giai_de.LoiGiaiDe(f"Đề quá dài (tối đa {giai_de.VAN_BAN_TOI_DA} ký tự).", "de_qua_dai", 400))
    cho = float(os.environ.get("VERA_GIAI_DE_CHO") or giai_de.CHO_MAC_DINH)
    try:
        return await asyncio.wait_for(giai_de.giai_de(van_ban, anh or None, loai), cho)
    except giai_de.LoiGiaiDe as e:
        raise _loi_giai_de(e) from e
    except asyncio.TimeoutError as e:
        raise _loi_giai_de(giai_de.LoiGiaiDe(f"Giải đề quá {cho:.0f} giây – thử lại hoặc tách đề nhỏ hơn.", "qua_gio", 504)) from e
