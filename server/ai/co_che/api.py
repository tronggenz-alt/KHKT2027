"""Điểm cuối máy chủ cơ chế (APIRouter /co-che), dùng trong may_chu.py (cổng 8010).

    GET  /co-che/suc-khoe        mô hình tại máy, RAG, số bài
    GET  /co-che/bai             danh sách bài (mã, tên, loại, lớp, đề)
    GET  /co-che/bai/{ma}        gói .verapack của bài (mã hóa, app giải trong bộ nhớ)
    GET  /co-che/chuoi-mau       3 chuỗi mẫu chạy được khi không có mạng
    POST /co-che/giai-thich      {"cau_hoi", "ma_bai"?} → lời giải thích có trích nguồn
    POST /co-che/giai-chuoi      {"chuoi": "C2H4 -> C2H5OH -> CH3CHO -> CH3COOH"} → từng bước + mã cơ chế nếu khớp mẫu
"""
from __future__ import annotations

import json
import re
from dataclasses import asdict

from fastapi import APIRouter, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel, Field

from . import dong_goi, giai_chuoi, giai_thich, tri_thuc
from .duong_dan import thu_muc_build
from ..cau_hinh_may import thong_so

router = APIRouter(prefix="/co-che", tags=["co-che"])


class YeuCauGiaiThich(BaseModel):
    cau_hoi: str = Field(min_length=1, max_length=800)
    ma_bai: str = Field(default="", max_length=40)


class YeuCauChuoi(BaseModel):
    chuoi: str = Field(min_length=3, max_length=400)


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
