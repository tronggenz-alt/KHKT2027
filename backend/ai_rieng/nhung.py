"""Embedding cho AI Riêng: Gemini (key MÁY CHỦ, mô hình EMBED_MODEL, EMBED_DIM chiều) + bộ nhớ vector numpy.

- Mỗi đoạn gửi thành MỘT types.Content riêng: gemini-embedding-2 gộp danh sách chuỗi trần thành một vector duy nhất.
- Vector chuẩn hoá (độ dài 1) → cosine = tích vô hướng. Kho cá nhân (vài nghìn mục × 768 chiều) so khớp toàn bộ chỉ mất vài ms,
  không cần extension sqlite-vec. Bộ nhớ nạp lại khi phiên bản kho đổi.
- Không có key / hết hạn mức → ném LoiAI; tìm kiếm tự lùi về chỉ tìm theo chữ (FTS5), việc nền thử nhúng lại sau.
"""
from __future__ import annotations

import logging
import threading

import numpy as np

from .. import db
from ..config import settings
from ..llm import LoiAI
from . import kho

log = logging.getLogger(__name__)
LO = 50  # số đoạn mỗi lần gọi
LO_CUNG_LUC = 3  # số lần gọi embedding cùng lúc

_khoa = threading.Lock()
_bo_nho: dict = {"phien_ban": None, "ma_tran": None, "nhan": []}


def co_the() -> bool:
    return bool(settings.gemini_api_key)


def nhung(ds: list[str], truy_van: bool = False) -> np.ndarray:
    """Danh sách chữ → ma trận (n, EMBED_DIM) float32 đã chuẩn hoá. Lỗi → LoiAI."""
    if not ds:
        return np.zeros((0, settings.embed_dim), dtype=np.float32)
    if not co_the():
        raise LoiAI("Chưa có GEMINI_API_KEY – chỉ tìm kiến thức theo chữ.", "thieu_key")
    from google.genai import types

    from ..ai.providers.gemini import client as gemini_client

    cfg = types.EmbedContentConfig(output_dimensionality=settings.embed_dim, task_type="RETRIEVAL_QUERY" if truy_van else "RETRIEVAL_DOCUMENT")
    def mot_lo(i: int) -> list[list[float]]:
        lo = [types.Content(parts=[types.Part(text=(t or " ")[:20000])]) for t in ds[i:i + LO]]
        try:
            r = gemini_client._gemini().models.embed_content(model=settings.embed_model, contents=lo, config=cfg)
        except Exception as e:  # noqa: BLE001
            ten = type(e).__name__
            t = str(e)
            loai = "gioi_han" if "429" in t or "RESOURCE_EXHAUSTED" in t else "key" if "API key" in t or "401" in t or "403" in t else "khac"
            raise LoiAI(f"Không tạo được embedding ({ten}).", loai) from e
        if len(r.embeddings) != len(lo):
            raise LoiAI(f"Embedding trả {len(r.embeddings)} vector cho {len(lo)} đoạn.", "dinh_dang")
        return [e.values for e in r.embeddings]

    from .song_song import ban_do

    ket_qua: list[list[float]] = []
    for phan in ban_do(mot_lo, range(0, len(ds), LO), LO_CUNG_LUC):  # nhiều lô gọi cùng lúc, giữ thứ tự
        if isinstance(phan, Exception):
            raise phan
        ket_qua += phan
    m = np.asarray(ket_qua, dtype=np.float32)
    n = np.linalg.norm(m, axis=1, keepdims=True)
    return m / np.where(n == 0, 1, n)


def sang_blob(v: np.ndarray) -> bytes:
    return np.asarray(v, dtype=np.float32).tobytes()


def tu_blob(b: bytes) -> np.ndarray:
    return np.frombuffer(b, dtype=np.float32)


_SQL_VEC = {
    "doan": "SELECT d.id, d.vec FROM ar_doan d JOIN ar_tai_lieu t ON t.id = d.tai_lieu_id "
            "WHERE d.vec IS NOT NULL AND t.bat = 1 AND t.trang_thai = 'da_nap'",
    "kien_thuc": "SELECT id, vec FROM ar_kien_thuc WHERE vec IS NOT NULL AND bat = 1",
    "bai_mau": "SELECT id, vec FROM ar_bai_mau WHERE vec IS NOT NULL AND bat = 1 AND trang_thai != 'sai'",
    "sua_sai": "SELECT id, vec FROM ar_sua_sai WHERE vec IS NOT NULL AND bat = 1",
}


def _nap() -> tuple[np.ndarray | None, list[tuple[str, int]]]:
    pb = kho.phien_ban()
    with _khoa:
        if _bo_nho["phien_ban"] == pb:
            return _bo_nho["ma_tran"], _bo_nho["nhan"]
        nhan, vec = [], []
        with db.ket_noi() as con:
            for loai, sql in _SQL_VEC.items():
                for r in con.execute(sql):
                    v = tu_blob(r["vec"])
                    if v.shape[0] == settings.embed_dim:  # đổi EMBED_DIM → vector cũ bị bỏ qua cho tới khi nhúng lại
                        nhan.append((loai, int(r["id"])))
                        vec.append(v)
        _bo_nho.update(phien_ban=pb, ma_tran=np.vstack(vec) if vec else None, nhan=nhan)
        return _bo_nho["ma_tran"], nhan


def tim(qv: np.ndarray, k: int = 60, nguong: float = 0.0) -> list[tuple[str, int, float]]:
    m, nhan = _nap()
    if m is None or not len(nhan):
        return []
    diem = m @ qv.astype(np.float32)
    thu_tu = np.argsort(-diem)[:k]
    return [(nhan[i][0], nhan[i][1], float(diem[i])) for i in thu_tu if diem[i] >= nguong]


_BANG = {"doan": "ar_doan", "kien_thuc": "ar_kien_thuc", "bai_mau": "ar_bai_mau", "sua_sai": "ar_sua_sai"}


def con_thieu(gioi_han: int = 400) -> list[tuple[str, int, str]]:
    """Các mục chưa có vector: [(loại, id, chữ để nhúng)]."""
    out: list[tuple[str, int, str]] = []
    with db.ket_noi() as con:
        for r in con.execute("SELECT d.id, d.noi_dung, t.ten FROM ar_doan d JOIN ar_tai_lieu t ON t.id = d.tai_lieu_id "
                             "WHERE d.vec IS NULL AND t.trang_thai = 'da_nap' LIMIT ?", (gioi_han,)):
            out.append(("doan", r["id"], kho.chu_cua("doan", dict(r), r["ten"])))
        for loai in ("kien_thuc", "bai_mau", "sua_sai"):
            for r in con.execute(f"SELECT * FROM {_BANG[loai]} WHERE vec IS NULL LIMIT ?", (gioi_han,)):
                out.append((loai, r["id"], kho.chu_cua(loai, dict(r))))
    return out[:gioi_han]


def nhung_con_thieu(gioi_han: int = 400) -> int:
    """Nhúng các mục còn thiếu vector (việc nền). Trả số mục đã nhúng; lỗi Gemini → LoiAI (thử lại sau)."""
    ds = con_thieu(gioi_han)
    if not ds:
        return 0
    m = nhung([x[2] for x in ds])
    with db.ket_noi() as con:
        for (loai, mid, _), v in zip(ds, m):
            con.execute(f"UPDATE {_BANG[loai]} SET vec = ? WHERE id = ?", (sang_blob(v), mid))
    kho.tang_phien_ban()
    return len(ds)
