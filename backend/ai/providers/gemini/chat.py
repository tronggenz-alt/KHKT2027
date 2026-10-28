"""Gọi Gemini (google-genai) + GeminiProvider (giao diện AIProvider dùng chung) + kiểm tra key Gemini của người dùng.

Trong Gemini đã có dự phòng nội bộ: hết hạn mức / mô hình không tồn tại → tự lùi lần lượt sang các mô hình GEMINI_MODEL_DU_PHONG.
Hết cả → ném LoiAI để router chuyển sang AI kế tiếp (vd ChatGPT).
"""
from __future__ import annotations

import base64
import json
import logging
import re
import time
from dataclasses import dataclass
from typing import Any, Callable

from ....config import settings
from ...types import GoiCongCu, LoiAI, PhanHoi, ds_anh
from . import client as _c
from .config import GEMINI_MODEL_DU_PHONG, MO_HINH_KIEM_TRA_KEY

log = logging.getLogger(__name__)

_gemini_model_dung: str | None = None  # mô hình Gemini đã gọi thành công (sau khi thử các tên dự phòng)
_model_dung_ca_nhan: dict[str, str] = {}               # theo từng key riêng của người dùng
_het_han_muc_ca_nhan: dict[str, dict[str, float]] = {}


def _bo_additional_properties(schema: Any) -> Any:
    """Gemini chỉ hỗ trợ một phần JSON Schema: bỏ additionalProperties (pydantic vẫn kiểm tra ở phía mình)."""
    if isinstance(schema, dict):
        return {k: _bo_additional_properties(v) for k, v in schema.items() if k != "additionalProperties"}
    if isinstance(schema, list):
        return [_bo_additional_properties(v) for v in schema]
    return schema


def _goi_gemini(*, system: str, contents: list, max_tokens: int, tools: list[dict] | None, output_schema: dict | None,
                uu_tien: str = "", bao: Callable[[str], None] | None = None, lam_phang: Callable[[], list] | None = None,
                khoa: Any = None, suy_nghi: int | None = None, khi_co_chu: Callable[[str | None], None] | None = None):
    """Một lượt gọi Gemini, trả về (resp, mô hình đã trả lời, contents đã gửi).

    uu_tien: mô hình thử trước (mô hình của phiên, hoặc Pro cho chế độ HSG).
    Quá tải (5xx) → chờ rồi thử lại; giới hạn tốc độ (429) → chờ ngắn hoặc đổi sang mô hình khác (mỗi mô hình có hạn mức riêng);
    tên mô hình không tồn tại (404) → thử tên tiếp theo. lam_phang: dựng lại lịch sử dạng văn bản khi phải đổi mô hình giữa phiên.
    khoa: key riêng của người dùng (nguon="ca_nhan") → chỉ dùng key đó; lỗi/hết hạn mức thì báo lỗi, KHÔNG chuyển sang key của chủ app.
    suy_nghi: ngân sách suy nghĩ (token) theo mức độ thông minh (None = mặc định của mô hình). khi_co_chu: streaming từng đoạn chữ.
    """
    global _gemini_model_dung
    from google.genai import errors, types

    ca_nhan = khoa is not None and khoa.nguon == "ca_nhan"
    if ca_nhan:
        lay_client = lambda: _c._gemini_ca_nhan(khoa)  # noqa: E731
        het_han_muc = _het_han_muc_ca_nhan.setdefault(khoa.ma, {})
        model_dung = _model_dung_ca_nhan.get(khoa.ma)
    else:
        lay_client = _c._gemini
        het_han_muc = _gemini_het_han_muc
        model_dung = _gemini_model_dung

    cfg: dict[str, Any] = {
        "system_instruction": system,
        "max_output_tokens": max_tokens,
        "automatic_function_calling": types.AutomaticFunctionCallingConfig(disable=True),
    }
    if tools:
        cfg["tools"] = [
            types.Tool(
                function_declarations=[
                    types.FunctionDeclaration(
                        name=t["name"],
                        description=t.get("description", ""),
                        parameters_json_schema=_bo_additional_properties(t["input_schema"]),
                    )
                    for t in tools
                ]
            )
        ]
    if output_schema:
        cfg["response_mime_type"] = "application/json"
        cfg["response_json_schema"] = _bo_additional_properties(output_schema)
    bo_nghi = [suy_nghi is None]  # mô hình không nhận cấu hình suy nghĩ → bỏ, gọi lại

    def cau_hinh(model: str):
        if bo_nghi[0]:
            return types.GenerateContentConfig(**cfg)
        if "2.5" in model:  # Gemini 2.5: ngân sách token (0 = tắt suy nghĩ với Flash/Flash-Lite)
            nghi = types.ThinkingConfig(thinking_budget=suy_nghi)
        else:               # Gemini 3+: mức suy nghĩ
            nghi = types.ThinkingConfig(thinking_level="minimal" if suy_nghi <= 0 else "low" if suy_nghi <= 2048
                                        else "medium" if suy_nghi <= 8192 else "high")
        return types.GenerateContentConfig(**cfg, thinking_config=nghi)

    def goi(model: str):
        if khi_co_chu is None:
            return lay_client().models.generate_content(model=model, contents=contents, config=cau_hinh(model))
        khi_co_chu(None)  # thử lại → bên nhận xoá phần đã nhận
        chu, chu_ky, ket_thuc, dung, phan_khac = [], None, None, None, []
        for manh in lay_client().models.generate_content_stream(model=model, contents=contents, config=cau_hinh(model)):
            dung = getattr(manh, "usage_metadata", None) or dung
            for c in getattr(manh, "candidates", None) or []:
                ket_thuc = getattr(c, "finish_reason", None) or ket_thuc
                for part in getattr(getattr(c, "content", None), "parts", None) or []:
                    chu_ky = getattr(part, "thought_signature", None) or chu_ky
                    if getattr(part, "function_call", None) is not None:
                        phan_khac.append(part)
                    elif getattr(part, "text", None) and not getattr(part, "thought", False):
                        chu.append(part.text)
                        khi_co_chu(part.text)
        noi_dung = types.Content(role="model", parts=[types.Part(text="".join(chu), thought_signature=chu_ky), *phan_khac])
        return _PhanHoiGop(candidates=[_UngVienGop(content=noi_dung, finish_reason=ket_thuc)], usage_metadata=dung)

    ung_vien = [uu_tien, model_dung, settings.gemini_model, *GEMINI_MODEL_DU_PHONG]
    ung_vien = list(dict.fromkeys(m for m in ung_vien if m))
    con_han = [m for m in ung_vien if het_han_muc.get(m, 0) < time.time()]
    ung_vien = con_han or ung_vien  # bỏ qua mô hình vừa báo hết hạn mức (trừ khi không còn mô hình nào)
    bao = bao or (lambda _m: None)
    loi_cuoi: LoiAI | None = None
    for i, model in enumerate(ung_vien):
        con_mo_hinh_khac = i < len(ung_vien) - 1
        if model != uu_tien and uu_tien and lam_phang:
            contents = lam_phang()  # đổi mô hình giữa phiên: bỏ thought signature của mô hình cũ
        for lan in range(3):
            try:
                resp = goi(model)
                if model != uu_tien:
                    if ca_nhan:
                        _model_dung_ca_nhan[khoa.ma] = model
                    else:
                        _gemini_model_dung = model
                return resp, model, contents
            except errors.APIError as e:
                code = getattr(e, "code", 0) or 0
                text = f"{getattr(e, 'status', '')} {getattr(e, 'message', '')}".lower()
                if code == 404:
                    loi_cuoi = LoiAI(f"Không tìm thấy mô hình Gemini '{model}'.", "model")
                    break  # thử tên mô hình tiếp theo
                if code == 400 and "thinking" in text and not bo_nghi[0]:
                    bo_nghi[0] = True  # mô hình không nhận mức/ngân sách suy nghĩ này → gọi lại với mặc định
                    log.info("Gemini %s không nhận cấu hình suy nghĩ – dùng mặc định", model)
                    continue
                if code in (401, 403) or "api_key_invalid" in text or "api key not valid" in text:
                    if ca_nhan:
                        raise LoiAI(f"{khoa.mo_ta[0].upper()}{khoa.mo_ta[1:]} không hợp lệ, đã bị thu hồi hoặc chưa được phép gọi "
                                    "Gemini API. Vào Cài đặt để kiểm tra hoặc nhập key mới.", "key") from e
                    raise LoiAI("GEMINI_API_KEY không hợp lệ hoặc không có quyền.", "key") from e
                if code == 429:
                    cho, theo_ngay = _thoi_gian_cho(e)
                    khong_co_quyen = "limit: 0" in text  # gói miễn phí không có mô hình này (vd Pro)
                    het_han_muc_that = theo_ngay or khong_co_quyen
                    if khong_co_quyen and "free" in text:
                        ly_do = f"Gemini {model}: gói miễn phí không được dùng mô hình này (cần bật thanh toán cho key Gemini)."
                    elif theo_ngay:
                        gio = f" – có lượt mới sau khoảng {cho / 3600:.0f} giờ" if cho and cho > 3600 else ""
                        ly_do = f"Gemini {model}: đã hết lượt trong ngày của gói miễn phí{gio}."
                    else:
                        ly_do = "Gemini: hết hạn mức (quota) hoặc gọi quá nhanh."
                    loi_cuoi = LoiAI(ly_do, "gioi_han")
                    # chờ ngắn rồi thử lại cùng mô hình; hết hạn mức thật hoặc phải chờ lâu thì đổi mô hình (hạn mức riêng)
                    if not het_han_muc_that and cho is not None and lan < 2 and (cho <= 20 or not con_mo_hinh_khac) and cho <= 60:
                        bao(f"Gemini {model} giới hạn tốc độ – chờ {cho:.0f} s rồi thử lại...")
                        _ngu(cho + 1)
                        continue
                    log.warning("Gemini %s hết hạn mức (429%s) → thử mô hình khác: %s", model,
                                ", theo ngày" if theo_ngay else "", getattr(e, "message", "")[:300])
                    if het_han_muc_that:  # hết lượt theo ngày: bỏ qua tới khi có lượt mới (tối đa 24 giờ)
                        het_han_muc[model] = time.time() + (min(cho, 86400) if theo_ngay and cho else 600)
                    break
                if code >= 500:
                    loi_cuoi = LoiAI("Máy chủ Gemini đang quá tải hoặc gặp lỗi.", "qua_tai")
                    if lan < 2:
                        cho = 4 * (lan + 1)
                        bao(f"Gemini {model} đang quá tải ({code}) – thử lại sau {cho} s...")
                        _ngu(cho)
                        continue
                    break
                raise LoiAI(f"Lỗi khi gọi Gemini ({code}): {getattr(e, 'message', e)}", "khac") from e
            except Exception as e:  # noqa: BLE001 – lỗi mạng của httpx...
                loi_cuoi = LoiAI(f"Không kết nối được tới Gemini ({e.__class__.__name__}).", "mang")
                if lan < 1:
                    _ngu(3)
                    continue
                raise loi_cuoi from e
        if con_mo_hinh_khac and loi_cuoi is not None and loi_cuoi.loai != "model":
            ly_do = str(loi_cuoi).removeprefix(f"Gemini {model}: ").removeprefix("Gemini: ").rstrip(".")
            bao(f"Gemini {model}: {ly_do} → chuyển sang {ung_vien[i + 1]}.")
    if ca_nhan and loi_cuoi is not None and loi_cuoi.loai == "gioi_han":
        ly_do = str(loi_cuoi).removeprefix("Gemini ").removeprefix(": ").rstrip(".")
        raise LoiAI(
            f"{khoa.mo_ta[0].upper()}{khoa.mo_ta[1:]} đã hết hạn mức ở các mô hình Gemini đã thử ({ly_do}). App không dùng key của "
            "chủ app thay thế – hãy đợi có lượt mới, bật thanh toán cho key tại aistudio.google.com, hoặc nhập key khác trong Cài đặt.",
            "gioi_han",
        )
    raise loi_cuoi or LoiAI("Không gọi được Gemini.", "khac")


_ngu = time.sleep  # tách riêng để test thay được
_gemini_het_han_muc: dict[str, float] = {}  # mô hình → thời điểm hết bỏ qua (hết hạn mức thật: limit 0 / theo ngày)


def _thoi_gian_cho(e) -> tuple[float | None, bool]:
    """(số giây Gemini đề nghị chờ, hạn mức theo ngày?) từ lỗi 429."""
    chi_tiet = f"{getattr(e, 'details', '')} {getattr(e, 'message', '')}"
    m = re.search(r"retryDelay['\"]?\s*:\s*['\"]?(\d+(?:\.\d+)?)s", chi_tiet) or re.search(r"retry in (\d+(?:\.\d+)?)\s*s", chi_tiet)
    return (float(m.group(1)) if m else None), "PerDay" in chi_tiet


@dataclass
class _UngVienGop:
    """Ứng viên gộp từ các mảnh streaming của Gemini (cùng các trường mà GeminiProvider.chat đọc)."""
    content: Any
    finish_reason: Any = None


@dataclass
class _PhanHoiGop:
    candidates: list
    usage_metadata: Any = None
    prompt_feedback: Any = None


def kiem_tra_suc_khoe(model: str) -> None:
    """Chỉ hỏi thông tin mô hình (không sinh token); lỗi → exception."""
    from google.genai import types
    from google import genai

    client = genai.Client(api_key=settings.gemini_api_key, http_options=types.HttpOptions(timeout=8000))
    client.models.get(model=model)  # (giữ biến client: client tạm bị thu hồi sẽ tự đóng trước khi gửi)


class GeminiProvider:
    """AIProvider cho Gemini (bậc lite / flash / pro theo kế hoạch của yêu cầu; key riêng của người dùng hoặc key chủ app)."""

    ten = "gemini"
    ten_hien_thi = "Gemini"

    def co_key(self) -> bool:
        return bool(settings.gemini_api_key)

    def ly_do_bo_qua(self, phien) -> str | None:
        return None if _c.san_sang(phien.khoa) else "chưa có key Gemini"

    def mo_hinh(self, phien) -> str:
        return phien.gemini_uu_tien

    def kiem_tra_suc_khoe(self, model: str = "") -> None:
        kiem_tra_suc_khoe(model or settings.gemini_model)

    @staticmethod
    def _gemini_tu_luot(luot: dict, ban_dia: bool):
        """Đổi một lượt trung lập sang types.Content.

        ban_dia=False: lượt có từ thời Claude → làm phẳng lời gọi/kết quả công cụ thành văn bản
        (tránh yêu cầu thought signature của Gemini với lời gọi không do Gemini tạo).
        """
        from google.genai import types

        vai = luot["vai"]
        if vai == "user":
            parts = []
            for mt, data in ds_anh(luot.get("anh")):
                parts.append(types.Part.from_bytes(data=base64.b64decode(data), mime_type=mt))
            parts.append(types.Part.from_text(text=luot["text"] or "(trống)"))
            return types.Content(role="user", parts=parts)
        if vai == "assistant":
            text = luot["text"]
            for gc in luot["cong_cu"]:
                text += f"\n[Đã gọi công cụ {gc.ten} với tham số {json.dumps(gc.tham_so, ensure_ascii=False)}]"
            return types.Content(role="model", parts=[types.Part.from_text(text=text.strip() or "(đã gọi công cụ)")])
        # vai == "tool"
        if ban_dia:
            parts = [
                types.Part(function_response=types.FunctionResponse(
                    id=gc.id if not gc.id.startswith("gm_") else None, name=gc.ten, response={"ket_qua": r}))
                for gc, r in luot["ket_qua"]
            ]
            return types.Content(role="user", parts=parts)
        text = "\n".join(
            f"Kết quả công cụ {gc.ten}: {json.dumps(r, ensure_ascii=False)[:6000]}" for gc, r in luot["ket_qua"]
        )
        return types.Content(role="user", parts=[types.Part.from_text(text=text)])

    def chat(self, phien, luot: dict) -> PhanHoi:
        if phien._gemini_contents is None:
            # lượt đầu / chuyển sang Gemini giữa phiên: dựng lại toàn bộ lịch sử (kể cả lượt vừa gửi) từ bản trung lập
            phien._gemini_contents = [self._gemini_tu_luot(x, ban_dia=False) for x in phien._lich_su]
        else:
            phien._gemini_contents.append(self._gemini_tu_luot(luot, ban_dia=True))
        resp, model, phien._gemini_contents = _goi_gemini(
            system=phien.system,
            contents=phien._gemini_contents,
            max_tokens=phien.max_tokens if phien.suy_nghi is not None else max(phien.max_tokens, 16000),
            tools=phien.tools,
            output_schema=phien.output_schema,
            uu_tien=phien.gemini_uu_tien,
            bao=phien.bao,
            lam_phang=lambda: [self._gemini_tu_luot(x, ban_dia=False) for x in phien._lich_su],
            khoa=phien.khoa,
            **({"suy_nghi": phien.suy_nghi} if phien.suy_nghi is not None else {}),
            **({"khi_co_chu": phien._khi_co_chu} if phien._khi_co_chu else {}),
        )
        phien.gemini_uu_tien = model  # giữ nguyên mô hình trong cả phiên (thought signature)
        cand = (resp.candidates or [None])[0]
        if cand is None or cand.content is None:
            ly_do = getattr(getattr(resp, "prompt_feedback", None), "block_reason", None)
            raise LoiAI(f"Gemini không trả lời ({ly_do or 'không rõ lý do'}).", "tu_choi")
        phien._gemini_contents.append(cand.content)
        parts = cand.content.parts or []
        text = "".join(p.text for p in parts if getattr(p, "text", None) and not getattr(p, "thought", False)).strip()
        cong_cu = []
        for i, p in enumerate(parts):
            fc = getattr(p, "function_call", None)
            if fc is not None:
                cong_cu.append(GoiCongCu(id=fc.id or f"gm_{len(phien._lich_su)}_{i}", ten=fc.name, tham_so=dict(fc.args or {})))
        finish = str(getattr(cand, "finish_reason", "") or "")
        if "SAFETY" in finish or "PROHIBITED" in finish:
            raise LoiAI("Gemini từ chối xử lý yêu cầu này.", "tu_choi")
        phien._lich_su.append({"vai": "assistant", "text": text, "cong_cu": cong_cu})
        usage = getattr(resp, "usage_metadata", None)
        vao, ra, nghi = (int(getattr(usage, k, 0) or 0) for k in ("prompt_token_count", "candidates_token_count", "thoughts_token_count"))
        log.info("Gemini %s finish=%s in=%s out=%s think=%s", model, finish, vao, ra, nghi)
        return PhanHoi(text, cong_cu, "gemini", model, "MAX_TOKENS" in finish, tokens_in=vao, tokens_out=ra + nghi, tokens_think=nghi)


# ======================================================================= Kiểm tra key Gemini của người dùng
def kiem_tra_key_gemini(key: str) -> dict:
    """Gọi thử một yêu cầu rất nhỏ bằng key. Trả {hop_le: True/False/None (chưa rõ), het_han_muc, thong_diep, mo_hinh}.

    Thông điệp không bao giờ chứa key (lỗi từ Google cũng được lọc key ra).
    """
    from google.genai import errors, types

    def sach(t: str) -> str:
        return str(t).replace(key, "***")[:220] if key else str(t)[:220]

    client = _c._tao_client_gemini(key)
    cfg = types.GenerateContentConfig(max_output_tokens=16)
    ket_qua: dict | None = None
    for model in dict.fromkeys([*MO_HINH_KIEM_TRA_KEY, settings.gemini_model, *GEMINI_MODEL_DU_PHONG]):
        try:
            client.models.generate_content(model=model, contents="Trả lời đúng một từ: OK", config=cfg)
            return {"hop_le": True, "het_han_muc": False, "mo_hinh": model,
                    "thong_diep": f"Key hợp lệ – đã gọi thử {model} thành công."}
        except errors.APIError as e:
            code = getattr(e, "code", 0) or 0
            text = f"{getattr(e, 'status', '')} {getattr(e, 'message', '')}".lower()
            if code == 404:
                continue
            if code in (401, 403) or "api_key_invalid" in text or "api key not valid" in text or "api key expired" in text:
                return {"hop_le": False, "het_han_muc": False, "mo_hinh": model,
                        "thong_diep": "Key không hợp lệ, đã bị thu hồi/hết hạn, hoặc chưa được phép gọi Gemini API. "
                                      "Hãy tạo key mới tại aistudio.google.com/apikey."}
            if code == 429:
                ket_qua = {"hop_le": True, "het_han_muc": True, "mo_hinh": model,
                           "thong_diep": f"Key hợp lệ nhưng mô hình {model} đang hết hạn mức (quota) – thử lại sau, "
                                         "hoặc bật thanh toán cho key để có hạn mức cao hơn."}
                continue
            if code >= 500:
                ket_qua = ket_qua or {"hop_le": None, "het_han_muc": False, "mo_hinh": model,
                                      "thong_diep": "Máy chủ Gemini đang quá tải, chưa kiểm tra được – thử lại sau ít phút."}
                continue
            return {"hop_le": False, "het_han_muc": False, "mo_hinh": model,
                    "thong_diep": f"Gemini từ chối key ({code}): {sach(getattr(e, 'message', e))}"}
        except Exception as e:  # noqa: BLE001 – lỗi mạng
            return {"hop_le": None, "het_han_muc": False, "mo_hinh": model,
                    "thong_diep": f"Không kết nối được tới Gemini ({e.__class__.__name__}) – kiểm tra mạng rồi thử lại."}
    return ket_qua or {"hop_le": None, "het_han_muc": False, "mo_hinh": "",
                       "thong_diep": "Không tìm thấy mô hình Gemini nào để gọi thử."}
