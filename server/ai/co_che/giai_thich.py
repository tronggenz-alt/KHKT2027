"""Giải thích cơ chế bằng RAG + mô hình AI (tại máy trước, trực tuyến sau). AI KHÔNG tự tạo cơ chế mới.

Luồng:
  1. Tìm đoạn liên quan trong kho kiến thức cơ chế (tri_thuc.py: 8 bài + quy tắc đã kiểm chứng), ưu tiên đoạn của bài đang mở.
  2. Gọi mô hình tại máy qua Ollama (/api/chat, keep_alive theo cau_hinh_may) với lời dặn: chỉ giải thích theo TÀI LIỆU,
     trích nguồn [n], không đề xuất cơ chế mới. Lỗi/không có Ollama → chuỗi nhà cung cấp trực tuyến (providers.pick()).
  3. Nếu câu trả lời vẫn chứa một cơ chế dạng JSON (có "buoc"), cơ chế đó phải qua co_che.kiem_tra; trượt thì bỏ, trả lời theo
     mẫu đã kiểm chứng. Không có AI nào: trả lời theo mẫu (lời giải thích soạn sẵn của bài).
"""
from __future__ import annotations

import json
import re
import time

import httpx

from server.ai import providers

from . import co_che as cc_mod
from . import tri_thuc
from .duong_dan import thu_muc_bai_demo
from ..cau_hinh_may import thong_so

SYSTEM = """Bạn là VERA-Hoa, trợ giảng Hóa học THPT (chương trình GDPT 2018) trong phòng thí nghiệm ảo VERA Lab.
Nhiệm vụ: GIẢI THÍCH cơ chế phản ứng cho học sinh, bằng tiếng Việt, ngắn gọn (tối đa khoảng 150 từ), dễ hiểu.
Quy tắc bắt buộc:
- Chỉ dựa vào TÀI LIỆU THAM KHẢO bên dưới. Sau mỗi ý lấy từ tài liệu, ghi số nguồn trong ngoặc vuông, ví dụ [1] hoặc [2].
- KHÔNG tự đề xuất cơ chế mới, không viết SMILES, không viết JSON. Nếu học sinh hỏi chất khác, chỉ ra cơ chế mẫu gần nhất trong
  tài liệu và nói rõ là "áp dụng tương tự, thay chất vào mẫu".
- Tài liệu không đủ để trả lời thì nói "Tài liệu của VERA chưa có nội dung này", không bịa.
- Dùng đúng thuật ngữ: cacbocation, nucleophin, electrophin, mũi tên cong, quy tắc Markovnikov, quy tắc Zaitsev."""


def doc_bai(ma: str) -> dict | None:
    if not ma or not re.fullmatch(r"[a-z0-9_]{1,40}", ma):
        return None
    t = thu_muc_bai_demo() / ma
    try:
        bai = json.loads((t / "bai.json").read_text(encoding="utf-8"))
        cc = json.loads((t / "co_che.json").read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    return {"bai": bai, "co_che": cc}


def tra_loi_mau(ma: str, cau_hoi: str) -> str:
    """Lời giải thích soạn sẵn (đã kiểm chứng) của bài – dùng khi không có AI hoặc AI trượt kiểm tra."""
    d = doc_bai(ma)
    if d is None:
        return "Chưa có AI và chưa chọn bài cơ chế: hãy mở một bài trong danh sách để xem lời giải thích soạn sẵn."
    bai, cc = d["bai"], d["co_che"]
    dong = [f"{cc.get('ten', ma)}: {cc.get('giai_thich', '')}"]
    for b in bai.get("cac_buoc", []):
        dong.append(f"• {b.get('ten', '')}: {b.get('giai_thich', '')}")
    return "\n".join(dong)


def _co_che_trong_cau(text: str) -> list[dict]:
    """Các khối JSON có 'buoc' trong câu trả lời (AI lỡ đề xuất cơ chế)."""
    ra = []
    for m in re.finditer(r"\{[\s\S]*\}", text):
        try:
            d = json.loads(m.group(0))
        except ValueError:
            continue
        if isinstance(d, dict) and isinstance(d.get("buoc"), list):
            ra.append(d)
    return ra


def kiem_co_che_ai(text: str) -> tuple[bool, list[str]]:
    """(đạt?, lỗi). Không có cơ chế nào trong câu trả lời → đạt."""
    loi: list[str] = []
    for d in _co_che_trong_cau(text):
        try:
            loi += cc_mod.kiem_tra(cc_mod.tu_json(d, "ai"))
        except cc_mod.LoiCoChe as e:
            loi.append(str(e))
    return not loi, loi


async def goi_tai_may(system: str, user: str, max_tokens: int = 400) -> tuple[str, str]:
    ts = thong_so()
    body = {"model": ts.mo_hinh, "stream": False, "keep_alive": ts.keep_alive,
            "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}],
            "options": {"num_ctx": 8192, "temperature": 0.3, "num_predict": max_tokens}}
    async with httpx.AsyncClient(timeout=ts.thoi_gian_cho) as c:
        r = await c.post(f"{ts.ollama_url}/api/chat", json=body)
        r.raise_for_status()
        return r.json()["message"]["content"].strip(), ts.mo_hinh


async def giai_thich(cau_hoi: str, ma_bai: str = "", cho_phep_truc_tuyen: bool = True) -> dict:
    t0 = time.time()
    cau_hoi = (cau_hoi or "").strip()[:800]
    d = doc_bai(ma_bai)
    ten_bai = d["co_che"].get("ten", "") if d else ""
    doan = await tri_thuc.tim(f"{ten_bai} {cau_hoi}".strip(), 5)
    if d:                                          # đoạn của bài đang mở lên đầu
        doan.sort(key=lambda x: 0 if x.nguon.endswith("/" + ma_bai) else 1)
    doan = doan[:4]
    nguon = [{"so": i, "tieu_de": x.tieu_de, "nguon": x.nguon} for i, x in enumerate(doan, 1)]
    tai_lieu = "\n\n".join(f"[{i}] {x.tieu_de}\n{x.noi_dung[:1500]}" for i, x in enumerate(doan, 1))
    user = (f"Bài đang xem: {ten_bai}\n" if ten_bai else "") + f"TÀI LIỆU THAM KHẢO:\n{tai_lieu or '(không có)'}\n\nCÂU HỎI: {cau_hoi}"
    tra_loi, nha, mo_hinh, loi_ai = "", "mau", "", []
    try:
        tra_loi, mo_hinh = await goi_tai_may(SYSTEM, user)
        nha = "tai_may"
    except Exception as e:  # noqa: BLE001 – không có Ollama/mô hình: thử trực tuyến
        loi_ai.append(f"tại máy: {type(e).__name__}")
        chuoi = providers.pick() if cho_phep_truc_tuyen else None
        if chuoi is not None:
            try:
                tra_loi = await chuoi.text(SYSTEM, user, 400)
                nha, mo_hinh = chuoi.NAME, chuoi.model_name()
            except Exception as e2:  # noqa: BLE001
                loi_ai.append(f"trực tuyến: {type(e2).__name__}")
    kiem_chung = "ai_theo_tai_lieu"
    if tra_loi:
        dat, loi = kiem_co_che_ai(tra_loi)
        if not dat:
            tra_loi = tra_loi_mau(ma_bai, cau_hoi) + "\n(AI đề xuất cơ chế chưa qua kiểm tra RDKit nên đã thay bằng lời giải thích mẫu.)"
            kiem_chung, nha = "mau_do_ai_truot", nha + "+mau"
            loi_ai += loi[:3]
    else:
        tra_loi = tra_loi_mau(ma_bai, cau_hoi)
        kiem_chung = "mau"
    return {"tra_loi": tra_loi, "nguon": nguon, "nha_cung_cap": nha, "mo_hinh": mo_hinh, "ma_bai": ma_bai,
            "kiem_chung": kiem_chung, "giay": round(time.time() - t0, 2), "ghi_chu": loi_ai}
