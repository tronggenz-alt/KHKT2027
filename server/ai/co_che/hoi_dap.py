"""Giải thích chi tiết, hỏi đáp theo bài, báo sai và đọc chuỗi từ ảnh cho nhánh "Giải bài tập" của VERA Lab.

Tương đương các API của app web CHEMISTRY (/api/co-che/giai-thich, /api/co-che/chat), viết lại gọn cho máy chủ cơ chế:
    POST /co-che/giai-thich-chi-tiet  {ten, de, loai, buoc:[{ten, ghi_chu}]}           → tổng quan + từng bước + lưu ý
    POST /co-che/hoi-dap              {cau_hoi, ma_bai?, ten, de, buoc, lich_su:[…]}    → câu trả lời theo đúng bài đang xem
    POST /co-che/bao-sai              {ma_bai?, ten, mo_ta, che_do?, buoc?}              → ghi data/bao_sai/co_che.jsonl
    POST /co-che/giai-chuoi           có thêm anh_base64: AI đọc chuỗi trong ảnh rồi giải như chữ

AI: Claude trước, Gemini dự phòng (giai_de.goi_ai_mac_dinh). Không có AI dùng được → trả lời theo mẫu soạn sẵn
(ghi chú từng bước của bài) hoặc RAG tại máy (giai_thich.py), và ghi rõ nguồn "mau" để app báo cho học sinh.
"""
from __future__ import annotations

import json
import re
import time
import uuid
from pathlib import Path

from . import giai_de

# ---------------------------------------------------------------- giải thích chi tiết từng bước
SYSTEM_GIAI_THICH = """Bạn là giáo viên Hóa học THPT (chương trình GDPT 2018, có ôn thi HSG) của VERA Lab.
Viết GIẢI THÍCH CHI TIẾT cho một cơ chế phản ứng đã được dựng sẵn và kiểm chứng. Tiếng Việt, câu ngắn, đúng thuật ngữ
(nucleophin, electrophin, cacbocation, mũi tên cong, quy tắc Markovnikov, Zaitsev, trạng thái chuyển tiếp).
- Giữ ĐÚNG số bước và thứ tự bước đã cho; không thêm, không bỏ, không đổi chất.
- Mỗi bước: tiêu đề ngắn (≤ 8 từ) và 2–4 câu: cặp electron đi từ đâu tới đâu, liên kết nào tạo/đứt, vì sao bước này xảy ra.
- tong_quan: 2–3 câu nói kiểu cơ chế và yếu tố quyết định (bậc chất nền, dung môi, nhiệt độ, xúc tác).
- luu_y: 1–3 lỗi học sinh hay mắc hoặc mẹo nhớ khi làm bài thi.
Không viết SMILES, không viết mã."""

SCHEMA_GIAI_THICH = {
    "type": "object",
    "properties": {
        "tong_quan": {"type": "string"},
        "buoc": {"type": "array", "items": {"type": "object", "properties": {
            "so": {"type": "integer"}, "tieu_de": {"type": "string"}, "noi_dung": {"type": "string"}},
            "required": ["so", "tieu_de", "noi_dung"], "additionalProperties": False}},
        "luu_y": {"type": "array", "items": {"type": "string"}},
    },
    "required": ["tong_quan", "buoc", "luu_y"],
    "additionalProperties": False,
}


def _sach(s, toi_da: int = 600) -> str:
    return re.sub(r"\s+", " ", str(s or "")).strip()[:toi_da]


def _ds_buoc(buoc) -> list[dict]:
    out = []
    for i, b in enumerate(buoc or []):
        if not isinstance(b, dict):
            continue
        out.append({"so": i + 1, "ten": _sach(b.get("ten"), 120), "ghi_chu": _sach(b.get("ghi_chu"), 500)})
    return out[:20]


def giai_thich_theo_mau(ten: str, loai: str, buoc: list[dict]) -> dict:
    """Không có AI: dựng giải thích từ ghi chú từng bước đã kiểm chứng của bài."""
    return {
        "tong_quan": f"{ten}" + (f" – cơ chế {loai}." if loai else ".") + " Xem từng bước bên dưới; mỗi bước ứng với một khung trên sơ đồ.",
        "buoc": [{"so": b["so"], "tieu_de": b["ten"] or f"Bước {b['so']}", "noi_dung": b["ghi_chu"] or "(chưa có ghi chú)"} for b in buoc],
        "luu_y": ["Mũi tên cong luôn đi từ cặp electron (liên kết hoặc cặp riêng) tới nguyên tử nhận electron."],
        "nguon": "mau",
    }


async def giai_thich_chi_tiet(ten: str, de: str, loai: str, buoc, goi_ai: giai_de.GoiAI | None = None) -> dict:
    ten, de, loai = _sach(ten, 200), _sach(de, 1500), _sach(loai, 40)
    ds = _ds_buoc(buoc)
    if not ds:
        raise giai_de.LoiGiaiDe("Bài chưa có bước nào để giải thích.", "thieu_buoc", 400)
    yc = (f"Bài: {ten}\nĐề: {de or '(không có)'}\nKiểu cơ chế: {loai or 'chưa rõ'}\nCác bước đã dựng:\n"
          + "\n".join(f"{b['so']}. {b['ten']}: {b['ghi_chu']}" for b in ds))
    goi = goi_ai or giai_de.goi_ai_mac_dinh
    t0 = time.time()
    try:
        chu = await goi(SYSTEM_GIAI_THICH, [{"role": "user", "content": yc}], SCHEMA_GIAI_THICH)
        d = giai_de.doc_json(chu)
    except giai_de.LoiGiaiDe as e:
        if e.ma in ("thieu_key", "sai_key", "thieu_key_gemini", "sai_key_gemini", "thieu_thu_vien"):
            kq = giai_thich_theo_mau(ten, loai, ds)
            kq["ly_do"] = e.thong_diep
            return kq
        raise
    # giữ đúng số bước: AI trả thừa/thiếu thì ghép theo số thứ tự, bước thiếu lấy ghi chú gốc
    theo_so = {int(b.get("so") or 0): b for b in d.get("buoc") or [] if isinstance(b, dict)}
    d["buoc"] = [{"so": b["so"], "tieu_de": _sach(theo_so.get(b["so"], {}).get("tieu_de") or b["ten"], 120),
                  "noi_dung": _sach(theo_so.get(b["so"], {}).get("noi_dung") or b["ghi_chu"], 1200)} for b in ds]
    d["tong_quan"] = _sach(d.get("tong_quan"), 800)
    d["luu_y"] = [_sach(x, 300) for x in (d.get("luu_y") or [])][:3]
    d["nguon"] = "ai"
    d["mo_hinh"] = ", ".join(dict.fromkeys(giai_de._da_dung)) if goi_ai is None else "gia_lap"
    d["giay"] = round(time.time() - t0, 1)
    return d


# ---------------------------------------------------------------- hỏi đáp theo bài
SYSTEM_HOI_DAP = """Bạn là VERA-Hoa, trợ giảng Hóa học THPT của VERA Lab. Học sinh đang xem một cơ chế phản ứng ĐÃ KIỂM CHỨNG
(ghi bên dưới) và hỏi về nó. Trả lời tiếng Việt, ngắn gọn (≤ 120 từ), bám sát bài đang xem; nhắc tới bước thì ghi "Bước n".
Không tự đổi cơ chế đã cho. Câu hỏi ngoài Hóa học thì nhẹ nhàng đưa học sinh về bài. Không viết SMILES, không viết mã."""

SCHEMA_HOI_DAP = {
    "type": "object",
    "properties": {"tra_loi": {"type": "string"}, "goi_y": {"type": "array", "items": {"type": "string"}}},
    "required": ["tra_loi", "goi_y"],
    "additionalProperties": False,
}


async def hoi_dap(cau_hoi: str, ten: str, de: str, buoc, lich_su, ma_bai: str = "",
                  goi_ai: giai_de.GoiAI | None = None) -> dict:
    cau_hoi = _sach(cau_hoi, 800)
    if not cau_hoi:
        raise giai_de.LoiGiaiDe("Chưa có câu hỏi.", "thieu_cau_hoi", 400)
    ds = _ds_buoc(buoc)
    bai = (f"BÀI ĐANG XEM: {_sach(ten, 200)}\nĐề: {_sach(de, 1500)}\n"
           + "\n".join(f"Bước {b['so']} – {b['ten']}: {b['ghi_chu']}" for b in ds))
    tin = []
    for m in (lich_su or [])[-8:]:
        if not isinstance(m, dict):
            continue
        noi = _sach(m.get("noi_dung"), 1200)
        if noi:
            tin.append({"role": "assistant" if m.get("vai") == "ai" else "user", "content": noi})
    while tin and tin[0]["role"] == "assistant":          # API yêu cầu tin đầu là của người dùng
        tin.pop(0)
    tin.append({"role": "user", "content": cau_hoi})
    tin[0] = {"role": "user", "content": bai + "\n\nCÂU HỎI: " + tin[0]["content"]}
    goi = goi_ai or giai_de.goi_ai_mac_dinh
    t0 = time.time()
    try:
        d = giai_de.doc_json(await goi(SYSTEM_HOI_DAP, tin, SCHEMA_HOI_DAP))
    except giai_de.LoiGiaiDe as e:
        if e.ma not in ("thieu_key", "sai_key", "thieu_key_gemini", "sai_key_gemini", "thieu_thu_vien"):
            raise
        # không có AI trực tuyến: RAG + mô hình tại máy (giai_thich.py), cuối cùng là lời giải thích soạn sẵn
        from . import giai_thich
        try:
            kq = await giai_thich.giai_thich(cau_hoi, ma_bai)
            return {"tra_loi": kq.get("tra_loi", ""), "goi_y": [], "nguon": "tai_may", "ly_do": e.thong_diep,
                    "mo_hinh": kq.get("mo_hinh", ""), "giay": round(time.time() - t0, 1)}
        except Exception:  # noqa: BLE001 – mọi lỗi tầng dự phòng đều trả lời theo mẫu
            tom = " ".join(f"Bước {b['so']}: {b['ghi_chu']}" for b in ds)[:900]
            return {"tra_loi": "Chưa có AI dùng được trên máy chủ. Tóm tắt theo bài: " + tom, "goi_y": [], "nguon": "mau",
                    "ly_do": e.thong_diep}
    return {"tra_loi": _sach(d.get("tra_loi"), 2000), "goi_y": [_sach(x, 120) for x in (d.get("goi_y") or [])][:3],
            "nguon": "ai", "mo_hinh": ", ".join(dict.fromkeys(giai_de._da_dung)) if goi_ai is None else "gia_lap",
            "giay": round(time.time() - t0, 1)}


# ---------------------------------------------------------------- báo sai
def thu_muc_bao_sai() -> Path:
    import os
    goc = os.environ.get("VERA_BAO_SAI_DIR")
    return Path(goc) if goc else Path(__file__).resolve().parents[3] / "data" / "bao_sai"


def bao_sai(ma_bai: str, ten: str, mo_ta: str, che_do: str = "", buoc: int | None = None, nguon: str = "co_che") -> dict:
    mo_ta = _sach(mo_ta, 2000)
    if len(mo_ta) < 4:
        raise giai_de.LoiGiaiDe("Hãy viết vài chữ mô tả chỗ sai.", "thieu_mo_ta", 400)
    ma = time.strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:6]
    dong = {"ma": ma, "luc": time.strftime("%Y-%m-%dT%H:%M:%S"), "nguon": _sach(nguon, 20) or "co_che",
            "ma_bai": _sach(ma_bai, 60), "ten": _sach(ten, 200), "che_do": _sach(che_do, 20), "buoc": buoc, "mo_ta": mo_ta}
    t = thu_muc_bao_sai()
    t.mkdir(parents=True, exist_ok=True)
    with (t / f"{dong['nguon']}.jsonl").open("a", encoding="utf-8") as f:
        f.write(json.dumps(dong, ensure_ascii=False) + "\n")
    return {"ok": True, "ma": ma}


# ---------------------------------------------------------------- đọc chuỗi trong ảnh
SYSTEM_DOC_CHUOI = """Ảnh là một đề bài chuỗi phản ứng hữu cơ (sách, vở hoặc ảnh chụp màn hình). Chép lại CHUỖI thành một dòng,
các chất nối bằng " -> ", chất chưa biết giữ nguyên chữ cái (X, Y, A…), tác nhân/điều kiện bỏ đi. Dùng công thức thu gọn
(ví dụ CH3CHO, C2H5OH). Không đọc được chuỗi thì để chuoi rỗng và ghi lý do."""

SCHEMA_DOC_CHUOI = {
    "type": "object",
    "properties": {"chuoi": {"type": "string"}, "ly_do": {"type": "string"}},
    "required": ["chuoi", "ly_do"],
    "additionalProperties": False,
}


async def doc_chuoi_tu_anh(anh: bytes, goi_ai: giai_de.GoiAI | None = None) -> str:
    kieu, b64 = giai_de.chuan_bi_anh(anh)
    noi = [{"type": "image", "source": {"type": "base64", "media_type": kieu, "data": b64}},
           {"type": "text", "text": "Chép chuỗi phản ứng trong ảnh."}]
    d = giai_de.doc_json(await (goi_ai or giai_de.goi_ai_mac_dinh)(SYSTEM_DOC_CHUOI, [{"role": "user", "content": noi}], SCHEMA_DOC_CHUOI))
    chuoi = _sach(d.get("chuoi"), 400).replace("→", "->")
    if len(chuoi) < 3 or "->" not in chuoi:
        raise giai_de.LoiGiaiDe("Không đọc được chuỗi trong ảnh" + (f": {_sach(d.get('ly_do'), 200)}" if d.get("ly_do") else ".")
                                + " Hãy chụp rõ hơn hoặc gõ chuỗi.", "anh_khong_ro", 422)
    return chuoi


# ---------------------------------------------------------------- đọc chất trong ảnh cho Mô hình 3D
SYSTEM_DOC_CHAT = """Ảnh chứa công thức cấu tạo hoặc tên một chất hoá học. Hãy nhận diện MỘT chất chính để dựng mô hình 3D:
- ten: tên tiếng Việt hoặc tiếng Anh ngắn gọn (vd "Benzene", "Ethanol", "Aspirin").
- smiles: SMILES chuẩn hợp lệ của chất đó (vd "c1ccccc1", "CCO").
Nếu không đọc được chất nào, để smiles rỗng và ghi lý do."""

SCHEMA_DOC_CHAT = {
    "type": "object",
    "properties": {"ten": {"type": "string"}, "smiles": {"type": "string"}, "ly_do": {"type": "string"}},
    "required": ["ten", "smiles", "ly_do"],
    "additionalProperties": False,
}


async def doc_chat_tu_anh(anh: bytes, goi_ai: giai_de.GoiAI | None = None) -> dict:
    kieu, b64 = giai_de.chuan_bi_anh(anh)
    noi = [{"type": "image", "source": {"type": "base64", "media_type": kieu, "data": b64}},
           {"type": "text", "text": "Nhận diện chất hoá học trong ảnh và trả về tên + SMILES."}]
    d = giai_de.doc_json(await (goi_ai or giai_de.goi_ai_mac_dinh)(SYSTEM_DOC_CHAT, [{"role": "user", "content": noi}], SCHEMA_DOC_CHAT))
    smi = _sach(d.get("smiles"), 300)
    ten = _sach(d.get("ten"), 150)
    if not smi and not ten:
        raise giai_de.LoiGiaiDe("Không đọc được chất trong ảnh" + (f": {_sach(d.get('ly_do'), 200)}" if d.get("ly_do") else ".")
                                + " Hãy chụp rõ hơn hoặc gõ tên chất.", "anh_khong_ro", 422)
    return {"ten": ten, "smiles": smi}
