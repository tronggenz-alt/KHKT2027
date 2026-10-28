"""AI Bio-Tutor của Phòng Lab Sinh học 3D chạy qua MÁY CHỦ.

Key AI (Claude / Gemini / ChatGPT) chỉ nằm ở server – trình duyệt gửi câu hỏi + danh sách id bộ phận mà mô hình 3D đang có,
nhận về {answer_html, action}. Trang 3D vẫn tự làm sạch HTML (lamSach) và kiểm tra hành động (hanhDongHopLe) trước khi dùng.
Chuỗi AI theo hệ thống "Mô hình 3D" của router (Claude chỉ dùng khi yêu cầu có quyền Claude – mặc định không) → thường là Gemini.
"""
from __future__ import annotations

import re
import logging
from typing import Any

from .ai.system_prompt_sinh_hoc import SYSTEM_PROMPT
from .ai.types import LoiAI
from .llm import PhienHoiThoai, doc_json
from .sinh_hoc_rag import dinh_dang_tai_lieu_prompt, lay_doan_tai_lieu

log = logging.getLogger(__name__)

_ID = re.compile(r"^[a-z0-9_]{1,40}$")
_KHONG_IN = re.compile(r"[\x00-\x1f\x7f]")

SYSTEM = SYSTEM_PROMPT


def _sach(s: Any, toi_da: int) -> str:
    return _KHONG_IN.sub(" ", str(s or ""))[:toi_da].strip()


def _ds_id(ds: list[Any], toi_da: int) -> list[str]:
    return [x for x in (str(v).strip() for v in (ds or [])[:toi_da]) if _ID.match(x)]


def tao_system(bo_phan: list[dict], duong: list[str], vi_mo: list[str], co_quan: list[str]) -> str:
    """System prompt = phần cố định ở server + danh sách id do trang 3D gửi (đã lọc: id a-z0-9_, tên ngắn, không ký tự điều khiển)."""
    dong = []
    for bp in (bo_phan or [])[:220]:
        i = str((bp or {}).get("id", "")).strip()
        if _ID.match(i):
            dong.append(f"{i} ({_sach((bp or {}).get('vi'), 60)})")
    them = [f"- id bộ phận hợp lệ (MESH_NAMES): {', '.join(dong) or '(không có)'}."]
    if duong:
        them.append(f"- duong (dòng hạt minh hoạ lộ trình): null | {' | '.join(_ds_id(duong, 24))}.")
    if vi_mo:
        them.append(f"- vi_mo (mở cảnh hiển vi tế bào): null | {' | '.join(_ds_id(vi_mo, 12))}.")
    if co_quan:
        them.append(f"- co_quan hợp lệ: {' | '.join(_ds_id(co_quan, 12))}.")
    return SYSTEM + "\n\n" + "\n".join(them)


def _kiem_tra_va_chuan_hoa_tools(du_lieu: dict[str, Any], tap_id_hop_le: set[str], cau_hoi: str = "") -> tuple[dict[str, Any], list[dict[str, Any]]]:
    """Kiểm tra mọi meshId, loại bỏ ID không hợp lệ kèm log warning, đồng bộ giữa tool_calls và action."""
    hanh_dong = du_lieu.get("action") if isinstance(du_lieu.get("action"), dict) else {}
    tool_calls = du_lieu.get("tool_calls") if isinstance(du_lieu.get("tool_calls"), list) else []

    # 1. Nếu có tool_calls, ánh xạ sang action
    for tc in tool_calls:
        if not isinstance(tc, dict):
            continue
        name = tc.get("name")
        args = tc.get("arguments") or {}
        if not isinstance(args, dict):
            continue
            
        if name == "focus":
            m_ids = args.get("meshIds") or []
            if isinstance(m_ids, list):
                hanh_dong.setdefault("tap_trung", m_ids)
            if args.get("view"):
                hanh_dong["goc_nhin"] = args.get("view")
        elif name == "isolate":
            m_ids = args.get("meshIds") or []
            if isinstance(m_ids, list):
                hanh_dong["tap_trung"] = m_ids
                hanh_dong["co_lap"] = True
        elif name == "showAll":
            hanh_dong["co_lap"] = False
            hanh_dong["tap_trung"] = []
        elif name == "label":
            m_ids = args.get("meshIds") or []
            if isinstance(m_ids, list):
                hanh_dong.setdefault("nhan", [{"bp": mid, "chu": mid} for mid in m_ids])
        elif name == "setLayer":
            if args.get("layer"):
                hanh_dong["lop"] = args.get("layer")
        elif name == "drawPath":
            if args.get("path"):
                hanh_dong["duong"] = args.get("path")
        elif name == "runProcess":
            pid = args.get("presetId") or args.get("processId") or args.get("process")
            if pid in ("systemic_circulation", "vong_tuan_hoan_lon", "tuan_hoan_lon"):
                hanh_dong["quy_trinh"] = "tuan_hoan_lon"
            elif pid in ("pulmonary_circulation", "vong_tuan_hoan_nho", "tuan_hoan_nho"):
                hanh_dong["quy_trinh"] = "tuan_hoan_nho"
            elif pid:
                hanh_dong["quy_trinh"] = pid
            elif args.get("steps"):
                hanh_dong["quy_trinh"] = {
                    "process": args.get("label") or "Quá trình sinh học",
                    "steps": args.get("steps")
                }
        elif name == "setVitals":
            hanh_dong["vitals"] = {
                "bpm": args.get("bpm"),
                "rr": args.get("rr")
            }

    # 2. Kiểm tra và lọc bỏ meshId không hợp lệ trong action
    if tap_id_hop_le:
        # Lọc tap_trung
        if "tap_trung" in hanh_dong and isinstance(hanh_dong["tap_trung"], list):
            tap_moi = []
            for mid in hanh_dong["tap_trung"]:
                if mid in tap_id_hop_le:
                    tap_moi.append(mid)
                else:
                    log.warning(f"Bỏ qua meshId không hợp lệ trong tap_trung: {mid}")
            hanh_dong["tap_trung"] = tap_moi

        # Lọc nhan
        if "nhan" in hanh_dong and isinstance(hanh_dong["nhan"], list):
            nhan_moi = []
            for n in hanh_dong["nhan"]:
                if isinstance(n, dict) and n.get("bp") in tap_id_hop_le:
                    nhan_moi.append(n)
                elif isinstance(n, dict):
                    log.warning(f"Bỏ qua meshId không hợp lệ trong nhan: {n.get('bp')}")
            hanh_dong["nhan"] = nhan_moi

        # Lọc quy_trinh nếu là dict có steps
        if "quy_trinh" in hanh_dong and isinstance(hanh_dong["quy_trinh"], dict):
            qt = hanh_dong["quy_trinh"]
            if "steps" in qt and isinstance(qt["steps"], list):
                steps_moi = []
                for s in qt["steps"]:
                    if not isinstance(s, dict):
                        continue
                    fr = s.get("from")
                    to = s.get("to")
                    fr_ok = fr in tap_id_hop_le if isinstance(fr, str) else True
                    to_ok = to in tap_id_hop_le if isinstance(to, str) else True
                    if fr_ok and to_ok:
                        steps_moi.append(s)
                    else:
                        log.warning(f"Bỏ qua bước quy trình có meshId không hợp lệ: from={fr}, to={to}")
                qt["steps"] = steps_moi

    # 3. Đồng bộ ngược lại sang tool_calls nếu ban đầu chưa có
    if not tool_calls:
        if hanh_dong.get("tap_trung"):
            tool_calls.append({"name": "focus", "arguments": {"meshIds": hanh_dong["tap_trung"]}})
        if hanh_dong.get("co_lap"):
            tool_calls.append({"name": "isolate", "arguments": {"meshIds": hanh_dong.get("tap_trung", [])}})
        if hanh_dong.get("nhan"):
            tool_calls.append({"name": "label", "arguments": {"meshIds": [n["bp"] for n in hanh_dong["nhan"] if isinstance(n, dict) and "bp" in n]}})
        if hanh_dong.get("duong"):
            tool_calls.append({"name": "drawPath", "arguments": {"path": hanh_dong["duong"]}})
        if hanh_dong.get("quy_trinh"):
            qt_arg = {"presetId": hanh_dong["quy_trinh"]} if isinstance(hanh_dong["quy_trinh"], str) else hanh_dong["quy_trinh"]
            tool_calls.append({"name": "runProcess", "arguments": qt_arg})
        if hanh_dong.get("vitals"):
            tool_calls.append({"name": "setVitals", "arguments": hanh_dong["vitals"]})

    # 4. Tự động gắn quy trình / dòng chảy nếu câu hỏi hoặc câu trả lời nói về lộ trình tuần hoàn/sinh lý
    text_check = f"{cau_hoi} {du_lieu.get('answer_html') or ''}".lower()
    if any(k in text_check for k in ("máu giàu oxy", "giàu o2", "máu đỏ tươi", "tuần hoàn lớn", "tuan_hoan_lon", "systemic", "máu giàu")):
        if "quy_trinh" not in hanh_dong:
            hanh_dong["quy_trinh"] = "tuan_hoan_lon"
            if not any(tc.get("name") == "runProcess" for tc in tool_calls):
                tool_calls.append({"name": "runProcess", "arguments": {"presetId": "tuan_hoan_lon"}})
        if "duong" not in hanh_dong:
            hanh_dong["duong"] = "oxy"
            if not any(tc.get("name") == "drawPath" for tc in tool_calls):
                tool_calls.append({"name": "drawPath", "arguments": {"path": "oxy"}})
    elif any(k in text_check for k in ("máu nghèo oxy", "nghèo o2", "máu đỏ thẫm", "tuần hoàn nhỏ", "tuan_hoan_nho", "pulmonary", "chu kỳ tim", "chu kì tim")):
        if "quy_trinh" not in hanh_dong:
            hanh_dong["quy_trinh"] = "tuan_hoan_nho"
            if not any(tc.get("name") == "runProcess" for tc in tool_calls):
                tool_calls.append({"name": "runProcess", "arguments": {"presetId": "tuan_hoan_nho"}})
        if "duong" not in hanh_dong:
            hanh_dong["duong"] = "khu_oxy"
            if not any(tc.get("name") == "drawPath" for tc in tool_calls):
                tool_calls.append({"name": "drawPath", "arguments": {"path": "khu_oxy"}})
    elif any(k in text_check for k in ("tiêu hoá", "đường đi thức ăn", "ống tiêu hoá", "nhu động ruột")):
        if "duong" not in hanh_dong:
            hanh_dong["duong"] = "thuc_an"
            if not any(tc.get("name") == "drawPath" for tc in tool_calls):
                tool_calls.append({"name": "drawPath", "arguments": {"path": "thuc_an"}})
    elif any(k in text_check for k in ("nước tiểu", "bài tiết nước tiểu", "thận lọc máu")):
        if "duong" not in hanh_dong:
            hanh_dong["duong"] = "nuoc_tieu"
            if not any(tc.get("name") == "drawPath" for tc in tool_calls):
                tool_calls.append({"name": "drawPath", "arguments": {"path": "nuoc_tieu"}})
    elif any(k in text_check for k in ("đường dẫn khí", "đường thở", "hít vào")):
        if "duong" not in hanh_dong:
            hanh_dong["duong"] = "khong_khi"
            if not any(tc.get("name") == "drawPath" for tc in tool_calls):
                tool_calls.append({"name": "drawPath", "arguments": {"path": "khong_khi"}})
    elif any(k in text_check for k in ("xung thần kinh", "điện thế hoạt động")):
        if "duong" not in hanh_dong:
            hanh_dong["duong"] = "than_kinh"
            hanh_dong["xung"] = True
            if not any(tc.get("name") == "drawPath" for tc in tool_calls):
                tool_calls.append({"name": "drawPath", "arguments": {"path": "than_kinh"}})

    return hanh_dong, tool_calls


def hoi(cau_hoi: str, lich_su: list[dict] | None = None, bo_phan: list[dict] | None = None, duong: list[str] | None = None,
        vi_mo: list[str] | None = None, co_quan: list[str] | None = None) -> dict:
    """Một câu hỏi → {answer_html, action, tool_calls, ai, model}. Lỗi AI / JSON hỏng → LoiAI."""
    cau_hoi = _sach(cau_hoi, 500)
    if not cau_hoi:
        raise LoiAI("Câu hỏi trống.")

    # 1. Truy xuất RAG từ kho kiến thức
    doan_rag = lay_doan_tai_lieu(cau_hoi, toi_da=4)
    tai_lieu_text = dinh_dang_tai_lieu_prompt(doan_rag)

    # 2. Xây dựng ngữ cảnh hội thoại
    phan = []
    for m in (lich_su or [])[-6:]:
        vai = "Học sinh" if (m or {}).get("vai") == "nguoi" else "Bio-Tutor"
        phan.append(f"{vai}: {_sach((m or {}).get('chu'), 800)}")

    doan_hoi_thoai = ("Đoạn hội thoại trước:\n" + "\n".join(phan) + "\n\n" if phan else "")
    phan_tai_lieu = (tai_lieu_text + "\n\n") if tai_lieu_text else ""
    yeu_cau = f"{phan_tai_lieu}{doan_hoi_thoai}Câu hỏi mới của học sinh: {cau_hoi}"

    # 3. Gửi prompt đến LLM
    phien = PhienHoiThoai(tao_system(bo_phan or [], duong or [], vi_mo or [], co_quan or []), effort="low", max_tokens=3000)
    ph = phien.gui(text=yeu_cau)

    try:
        du_lieu = doc_json(ph.text)
    except ValueError as e:
        raise LoiAI("AI trả về sai định dạng – thử hỏi lại.") from e
    if not isinstance(du_lieu, dict):
        raise LoiAI("AI trả về sai định dạng – thử hỏi lại.")

    # 4. Tập hợp ID hợp lệ để kiểm tra
    tap_id_hop_le = {str(bp.get("id")).strip() for bp in (bo_phan or []) if bp and bp.get("id")}
    # Bổ sung các ID cơ quan tiêu chuẩn
    tap_id_hop_le.update({
        "tim", "tam_that_trai", "tam_that_phai", "tam_nhi_trai", "tam_nhi_phai",
        "dong_mach_chu", "quai_dong_mach_chu", "dong_mach_phoi", "tinh_mach_phoi",
        "tinh_mach_chu_tren", "tinh_mach_chu_duoi", "phoi_trai", "phoi_phai", "khi_quan",
        "da_day", "gan", "tuy", "tui_mat", "ruot_non", "ruot_gia", "than_trai", "than_phai",
        "bang_quang", "nao", "tuy_song", "co_nhi_dau", "co_tam_dau"
    })
    if co_quan:
        tap_id_hop_le.update(_ds_id(co_quan, 30))

    hanh_dong, tool_calls = _kiem_tra_va_chuan_hoa_tools(du_lieu, tap_id_hop_le, cau_hoi=cau_hoi)

    return {
        "answer_html": str(du_lieu.get("answer_html") or du_lieu.get("tra_loi") or "")[:6000],
        "action": hanh_dong,
        "tool_calls": tool_calls,
        "ai": ph.nha_cung_cap,
        "model": ph.model
    }


# =====================================================================================================================
# GIẢI THÍCH TỪ ẢNH (VISION OCR + THUẬT NGỮ + MÔ HÌNH 3D)
# =====================================================================================================================
import base64
import logging
from pydantic import BaseModel, Field, ValidationError

log = logging.getLogger(__name__)

TEN_MESH_CHUAN: dict[str, str] = {
    "right_ventricle": "tam_that_phai",
    "left_ventricle": "tam_that_trai",
    "right_atrium": "tam_nhi_phai",
    "left_atrium": "tam_nhi_trai",
    "pulmonary_artery": "dong_mach_phoi",
    "aorta": "dong_mach_chu",
    "superior_vena_cava": "tinh_mach_chu",
    "inferior_vena_cava": "tinh_mach_chu",
    "vena_cava": "tinh_mach_chu",
    "pulmonary_veins": "tinh_mach_phoi",
    "pulmonary_vein": "tinh_mach_phoi",
    "pulmonary_capillaries": "phoi_phai",
    "lungs": "phoi_phai",
    "lung": "phoi_phai",
    "lung_right": "phoi_phai",
    "lung_left": "phoi_trai",
    "mitral_valve": "van_hai_la",
    "tricuspid_valve": "van_ba_la",
    "aortic_valve": "van_dong_mach_chu",
    "pulmonary_valve": "van_dong_mach_phoi",
    "heart": "tam_that_trai",
    "brain": "dai_nao",
    "cerebrum": "dai_nao",
    "cerebellum": "tieu_nao",
    "brainstem": "than_nao",
    "spinal_cord": "tuy_song",
    "nerves": "day_than_kinh",
    "stomach": "da_day",
    "liver": "gan",
    "gallbladder": "tui_mat",
    "pancreas": "tuy",
    "small_intestine": "ruot_non",
    "large_intestine": "dai_trang",
    "colon": "dai_trang",
    "rectum": "truc_trang",
    "kidney": "than_trai",
    "kidneys": "than_trai",
    "kidney_left": "than_trai",
    "kidney_right": "than_phai",
    "bladder": "bang_quang",
    "ureter": "nieu_quan",
    "urethra": "nieu_dao",
    "trachea": "khi_quan",
    "bronchus": "phe_quan",
    "bronchi": "phe_quan",
    "larynx": "thanh_quan",
    "diaphragm": "co_hoanh",
    "pelvic_vessels": "mach_chau",
    "body_tissues": "mach_chau",
    "capillaries": "mach_chau",
}

DS_MESH_MAC_DINH = {
    "tam_that_phai", "tam_that_trai", "tam_nhi_phai", "tam_nhi_trai",
    "dong_mach_chu", "dong_mach_phoi", "tinh_mach_chu", "tinh_mach_phoi",
    "phoi_phai", "phoi_trai", "khi_quan", "phe_quan", "thanh_quan", "co_hoanh",
    "dai_nao", "tieu_nao", "than_nao", "tuy_song", "day_than_kinh",
    "da_day", "gan", "tui_mat", "tuy", "ruot_non", "dai_trang", "truc_trang",
    "than_phai", "than_trai", "nieu_quan", "bang_quang", "nieu_dao",
    "mach_chau", "van_hai_la", "van_ba_la", "van_dong_mach_chu", "van_dong_mach_phoi",
    "co_nguc_lon", "co_thang", "co_tu_dau_dui", "co_nhi_dau", "da", "hop_so", "xuong_suon",
    # hỗ trợ alias tiếng Anh chuẩn
    *TEN_MESH_CHUAN.keys()
}

PRESETS_QUY_TRINH = {
    "pulmonary_circulation": {
        "name": "Vòng tuần hoàn nhỏ (Phổi)",
        "matchedPreset": "pulmonary_circulation",
        "steps": [
            {
                "id": 1,
                "from": "right_ventricle",
                "to": "pulmonary_artery",
                "color": "#1E63D6",
                "title": "Tâm thất phải → ĐM phổi",
                "what": "Tâm thất phải co bóp (thất thu ~0,3 s)",
                "why": "Chênh lệch áp lực làm mở van bán nguyệt động mạch phổi, van ba lá đóng",
                "result": "Tống máu nghèo O2 (đỏ thẫm, giàu CO2) vào thân động mạch phổi với áp lực ~25 mmHg",
                "bloodState": "nghèo O2 (đỏ thẫm)",
                "valves": "Van động mạch phổi mở, van ba lá đóng",
                "keyword": "tâm thất phải",
                "text": "Tâm thất phải co (thất thu ~0,3 s), đẩy máu nghèo O2 (đỏ thẫm, giàu CO2) qua van bán nguyệt vào thân động mạch phổi với áp suất ~25 mmHg.",
                "focus": "right_ventricle",
                "duration": 2.3
            },
            {
                "id": 2,
                "from": "pulmonary_artery",
                "to": "phoi_phai",
                "color": "#1E63D6",
                "title": "ĐM phổi → Mao mạch phổi",
                "what": "Máu chảy theo 2 nhánh động mạch phổi trái và phải",
                "why": "Động mạch phổi phân nhánh thành mạng lưới mao mạch bao quanh phế nang",
                "result": "Dẫn dòng máu nghèo O2 tới vị trí trao đổi khí tại phế nang",
                "bloodState": "nghèo O2 (đỏ thẫm)",
                "valves": "Không có van",
                "keyword": "mao mạch phổi",
                "text": "Động mạch phổi phân nhánh thành nhánh trái và phải, dẫn dòng máu nghèo O2 tới mạng lưới mao mạch bao quanh các phế nang.",
                "focus": "phoi_phai",
                "duration": 2.2
            },
            {
                "id": 3,
                "from": "phoi_phai",
                "to": "phoi_phai",
                "color": "#E53935",
                "title": "Trao đổi khí ở phế nang",
                "what": "Khuếch tán khí O2 và CO2 qua màng phế nang – mao mạch",
                "why": "Chênh lệch phân áp: pO2 phế nang cao hơn máu nên O2 khuếch tán vào máu; pCO2 máu cao hơn nên CO2 khuếch tán ra phế nang",
                "result": "O2 gắn kết Hemoglobin (Hb + 4O2 ⇌ Hb(O2)4), máu chuyển từ đỏ thẫm sang đỏ tươi",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Không có van",
                "keyword": "trao đổi khí phế nang",
                "text": "Tại mao mạch phế nang: CO2 từ máu khuếch tán ra phế nang để thở ra; O2 từ phế nang khuếch tán vào máu kết hợp với Hemoglobin (Hb + 4O2 ⇌ Hb(O2)4), làm máu đổi từ đỏ thẫm sang đỏ tươi.",
                "focus": "phoi_phai",
                "duration": 2.6
            },
            {
                "id": 4,
                "from": "phoi_phai",
                "to": "phoi_trai",
                "color": "#E53935",
                "title": "Gom máu giàu O2",
                "what": "Mao mạch phế nang hợp lưu thành các tiểu tĩnh mạch phổi",
                "why": "Chênh lệch áp suất thủy tĩnh đẩy máu giàu O2 từ hai bên phổi hướng về tĩnh mạch lớn",
                "result": "Gom toàn bộ dòng máu giàu O2 chuẩn bị đổ về tim",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Không có van",
                "keyword": "tĩnh mạch phổi",
                "text": "Máu đỏ tươi giàu O2 từ các mao mạch phế nang hai bên phổi tập trung vào các tiểu tĩnh mạch rồi hợp lưu thành các tĩnh mạch phổi lớn.",
                "focus": "phoi_trai",
                "duration": 2.0
            },
            {
                "id": 5,
                "from": "phoi_trai",
                "to": "tinh_mach_phoi",
                "color": "#E53935",
                "title": "Tĩnh mạch phổi dẫn máu về",
                "what": "4 tĩnh mạch phổi dẫn máu về tim",
                "why": "Áp lực trong tĩnh mạch phổi cao hơn áp lực trong tâm nhĩ trái",
                "result": "Dẫn dòng máu giàu O2 hướng thẳng về phía sau tâm nhĩ trái",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Không có van",
                "keyword": "tĩnh mạch phổi",
                "text": "4 tĩnh mạch phổi dẫn dòng máu giàu O2 hướng thẳng về phía sau tâm nhĩ trái của tim.",
                "focus": "tinh_mach_phoi",
                "duration": 2.2
            },
            {
                "id": 6,
                "from": "tinh_mach_phoi",
                "to": "tam_nhi_trai",
                "color": "#E53935",
                "title": "Đổ vào tâm nhĩ trái",
                "what": "Máu giàu O2 đổ vào buồng tâm nhĩ trái",
                "why": "Tâm trương tâm nhĩ trái hút máu về đầy buồng",
                "result": "Kết thúc vòng tuần hoàn nhỏ, máu chuẩn bị qua van hai lá xuống thất trái sang vòng lớn",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Van hai lá mở trong kỳ tâm trương",
                "keyword": "tâm nhĩ trái",
                "text": "Máu giàu O2 đổ vào tâm nhĩ trái, kết thúc vòng tuần hoàn nhỏ và chuẩn bị qua van hai lá xuống tâm thất trái để sang vòng tuần hoàn lớn.",
                "focus": "tam_nhi_trai",
                "duration": 2.2
            }
        ]
    },
    "systemic_circulation": {
        "name": "Vòng tuần hoàn lớn (Toàn thân)",
        "matchedPreset": "systemic_circulation",
        "steps": [
            {
                "id": 1,
                "from": "tam_that_trai",
                "to": "dong_mach_chu",
                "color": "#E53935",
                "title": "Tâm thất trái → ĐM chủ",
                "what": "Tâm thất trái co bóp cực mạnh (thất thu ~0,3 s)",
                "why": "Thành cơ thất trái dày gấp 3 lần thất phải, tạo áp lực ~120 mmHg mở van động mạch chủ",
                "result": "Tống lượng máu giàu O2 (~70 mL) vào quai động mạch chủ",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Van động mạch chủ mở, van hai lá đóng",
                "keyword": "tâm thất trái",
                "text": "Tâm thất trái có thành cơ dày nhất co bóp mạnh tạo áp suất ~120 mmHg, mở van bán nguyệt chủ tống máu giàu O2 (đỏ tươi) vào quai động mạch chủ.",
                "focus": "tam_that_trai",
                "duration": 2.4
            },
            {
                "id": 2,
                "from": "dong_mach_chu",
                "to": "dai_nao",
                "color": "#E53935",
                "title": "ĐM cảnh → Nuôi não",
                "what": "Máu phân nhánh lên cung cấp cho não bộ",
                "why": "Não tiêu thụ 20% lượng O2 toàn thân, cần dòng cấp máu liên tục",
                "result": "Nuôi dưỡng tế bào thần kinh đại não",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Không có van",
                "keyword": "đại não",
                "text": "Từ quai động mạch chủ, các nhánh động mạch cảnh dẫn máu giàu O2 và glucose lên cấp máu nuôi các tế bào thần kinh đại não.",
                "focus": "dai_nao",
                "duration": 2.2
            },
            {
                "id": 3,
                "from": "dong_mach_chu",
                "to": "da_day",
                "color": "#E53935",
                "title": "ĐM thân tạng → Hệ tiêu hóa",
                "what": "Máu qua động mạch thân tạng cấp máu cho dạ dày, gan, ruột",
                "why": "Cung cấp năng lượng cho co bóp cơ trơn và hấp thu dưỡng chất",
                "result": "Duy trì hoạt động tiêu hoá và chuyển hoá",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Không có van",
                "keyword": "hệ tiêu hóa",
                "text": "Động mạch chủ ngực và bụng phân nhánh nuôi dạ dày, gan, tụy và ruột non thực hiện chức năng tiêu hoá và hấp thu dưỡng chất.",
                "focus": "da_day",
                "duration": 2.2
            },
            {
                "id": 4,
                "from": "dong_mach_chu",
                "to": "than_trai",
                "color": "#E53935",
                "title": "ĐM thận → Thận lọc máu",
                "what": "Dòng máu vào 2 thận với lưu lượng ~1,2 L/phút",
                "why": "Thận nhận ~25% cung lượng tim để lọc máu tạo nước tiểu đầu qua nephron",
                "result": "Thực hiện chức năng bài tiết và lọc sạch chất độc hại",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Không có van",
                "keyword": "thận",
                "text": "Động mạch thận đưa khoảng 25% cung lượng tim vào thận để lọc sạch chất cặn bã và duy trì cân bằng nội môi qua hàng triệu nephron.",
                "focus": "than_trai",
                "duration": 2.2
            },
            {
                "id": 5,
                "from": "dong_mach_chu",
                "to": "mach_chau",
                "color": "#E53935",
                "title": "ĐM chậu → Thân dưới & Chi",
                "what": "Máu theo động mạch chậu xuống nuôi chi dưới và khung chậu",
                "why": "Cơ đùi và cẳng chân tiêu thụ năng lượng khi vận động",
                "result": "Cấp máu cho toàn bộ nửa dưới cơ thể",
                "bloodState": "giàu O2 (đỏ tươi)",
                "valves": "Không có van",
                "keyword": "mạch chậu",
                "text": "Động mạch chủ bụng chia đôi thành các động mạch chậu dẫn máu giàu oxy tới nuôi các cơ quan vùng chậu và hai chi dưới.",
                "focus": "mach_chau",
                "duration": 2.2
            },
            {
                "id": 6,
                "from": "mach_chau",
                "to": "mach_chau",
                "color": "#1E63D6",
                "title": "Trao đổi chất ở mô",
                "what": "Trao đổi khí và dưỡng chất tại mao mạch các cơ quan",
                "why": "Khuếch tán: O2 và glucose vào tế bào mô; CO2 và chất thải từ tế bào khuếch tán vào máu",
                "result": "Máu mất O2, chuyển màu từ đỏ tươi sang đỏ thẫm",
                "bloodState": "nghèo O2 (đỏ thẫm)",
                "valves": "Không có van",
                "keyword": "trao đổi chất",
                "text": "Tại mao mạch mô: O2 và chất dinh dưỡng khuếch tán vào tế bào; CO2 và chất bã khuếch tán vào máu, làm máu đổi từ đỏ tươi thành đỏ thẫm.",
                "focus": "mach_chau",
                "duration": 2.6
            },
            {
                "id": 7,
                "from": "mach_chau",
                "to": "tinh_mach_chu",
                "color": "#1E63D6",
                "title": "Tĩnh mạch chủ gom máu",
                "what": "Máu theo tĩnh mạch chủ dưới và trên chảy về tim",
                "why": "Bơm cơ vân và van tĩnh mạch một chiều hỗ trợ đẩy máu ngược trọng lực",
                "result": "Hợp lưu vào hai tĩnh mạch chủ lớn",
                "bloodState": "nghèo O2 (đỏ thẫm)",
                "valves": "Van tĩnh mạch chống dòng chảy ngược",
                "keyword": "tĩnh mạch chủ",
                "text": "Máu nghèo O2 từ nửa trên cơ thể theo tĩnh mạch chủ trên, từ nửa dưới theo tĩnh mạch chủ dưới cùng hướng về phía tim.",
                "focus": "tinh_mach_chu",
                "duration": 2.2
            },
            {
                "id": 8,
                "from": "tinh_mach_chu",
                "to": "tam_nhi_phai",
                "color": "#1E63D6",
                "title": "Đổ vào tâm nhĩ phải",
                "what": "Dòng máu nghèo O2 đổ về tâm nhĩ phải",
                "why": "Tâm trương nhĩ phải tạo áp lực âm hút máu về",
                "result": "Khép kín vòng tuần hoàn lớn, sẵn sàng qua van ba lá xuống thất phải",
                "bloodState": "nghèo O2 (đỏ thẫm)",
                "valves": "Van ba lá chuẩn bị mở",
                "keyword": "tâm nhĩ phải",
                "text": "Máu nghèo O2 đổ vào tâm nhĩ phải, khép kín vòng tuần hoàn lớn và chuẩn bị qua van ba lá xuống tâm thất phải để bắt đầu vòng tuần hoàn nhỏ.",
                "focus": "tam_nhi_phai",
                "duration": 2.2
            }
        ]
    }
}


class TermItem(BaseModel):
    text: str = Field(..., max_length=120)
    bbox: list[float] = Field(default_factory=lambda: [0.0, 0.0, 1.0, 1.0])
    mesh: str | None = None
    short: str = Field(default="", max_length=300)


class StepItem(BaseModel):
    id: int = 1
    from_: str = Field("", alias="from", max_length=60)
    to: str = Field("", max_length=60)
    color: str = "#E53935"
    title: str = ""
    what: str = ""
    why: str = ""
    result: str = ""
    bloodState: str = ""
    valves: str = ""
    keyword: str = ""
    text: str = ""
    focus: str | None = None
    duration: float = 2.5
    aiGenerated: bool = True


class ProcessItem(BaseModel):
    name: str = ""
    matchedPreset: str | None = None
    steps: list[dict[str, Any]] = Field(default_factory=list)


class TermOnlyItem(BaseModel):
    text: str = ""
    explain: str = ""


class QuestionItem(BaseModel):
    answer: str = ""
    reason: str = ""


class GiaiThichAnhOutput(BaseModel):
    status: str = "ok"  # "ok" | "unrelated" | "unreadable"
    ocrText: str = ""
    terms: list[TermItem] = Field(default_factory=list)
    processes: list[ProcessItem] = Field(default_factory=list)
    termsOnly: list[TermOnlyItem] = Field(default_factory=list)
    question: QuestionItem | None = None
    note: str = ""


def _chuan_hoa_mesh(ten: str | None, hop_le: set[str]) -> str | None:
    if not ten or not isinstance(ten, str):
        return None
    s = ten.strip().lower()
    if s in hop_le:
        return s
    s2 = s.replace(" ", "_")
    if s2 in hop_le:
        return s2
    if s in TEN_MESH_CHUAN and TEN_MESH_CHUAN[s] in hop_le:
        return TEN_MESH_CHUAN[s]
    if s2 in TEN_MESH_CHUAN and TEN_MESH_CHUAN[s2] in hop_le:
        return TEN_MESH_CHUAN[s2]
    # Thử tìm kiếm mờ hoặc ánh xạ
    if "ventricle" in s and "right" in s: return "tam_that_phai" if "tam_that_phai" in hop_le else None
    if "ventricle" in s and "left" in s: return "tam_that_trai" if "tam_that_trai" in hop_le else None
    if "atrium" in s and "right" in s: return "tam_nhi_phai" if "tam_nhi_phai" in hop_le else None
    if "atrium" in s and "left" in s: return "tam_nhi_trai" if "tam_nhi_trai" in hop_le else None
    if "aorta" in s: return "dong_mach_chu" if "dong_mach_chu" in hop_le else None
    if "pulmonary" in s and "artery" in s: return "dong_mach_phoi" if "dong_mach_phoi" in hop_le else None
    return None


SYSTEM_DOC_ANH = """Bạn là giáo viên Sinh học THPT Việt Nam (chương trình GDPT 2018, bám sát đề thi tốt nghiệp THPT).
Nhiệm vụ:
1. Đọc toàn bộ chữ trong ảnh (OCR), giữ nguyên tiếng Việt có dấu, lưu vào "ocrText".
2. Xác định các THUẬT NGỮ sinh học và QUÁ TRÌNH được nhắc đến (ví dụ: vòng tuần hoàn nhỏ, van hai lá, chu kì tim, trao đổi khí ở phế nang, lọc ở cầu thận).
3. Với mỗi quá trình có thể thể hiện trên mô hình người 3D, tạo danh sách bước theo đúng định dạng:
   - from, to: CHỈ dùng tên trong danh sách MESH_NAMES bên dưới. Bước nội bộ (diễn ra trong 1 bộ phận) thì from = to.
   - color: mã màu hex (#E53935 cho máu giàu O2, #1E63D6 cho máu nghèo O2, #F2C94C cho thần kinh, #43A047 cho tiêu hoá, #8E44AD cho tiết niệu).
   - title: tiêu đề ngắn của bước (vd: "Tâm thất phải → ĐM phổi").
   - what: hiện tượng gì xảy ra (vd: "Tâm thất phải co bóp").
   - why: NGUYÊN NHÂN sinh học (chênh lệch áp lực, co/giãn cơ, van đóng/mở, khuếch tán theo gradient nồng độ...), không chỉ mô tả lại.
   - result: kết quả (vd: "Tống máu nghèo O2 vào động mạch phổi").
   - bloodState: trạng thái máu nếu là tuần hoàn (vd: "nghèo O2, đỏ thẫm" hoặc "giàu O2, đỏ tươi").
   - valves: trạng thái van tim liên quan (vd: "van bán nguyệt mở, van ba lá đóng").
   - keyword: thuật ngữ chính của bước (vd: "tâm thất phải").
   - text: 1-2 câu giải thích hoàn chỉnh cho học sinh.
4. Thuật ngữ nào không gắn được với bộ phận trên mô hình (ví dụ khái niệm thuần lý thuyết như "tính tự động của tim", "quy luật di truyền", "điện thế nghỉ") thì vẫn giải thích bằng chữ trong mục "termsOnly", KHÔNG bịa tên mesh.
5. Nếu ảnh là câu hỏi trắc nghiệm hoặc đúng/sai: giải thích bản chất trước, rồi mới nêu đáp án và lý do trong mục "question": {"answer": "A/B/C/D", "reason": "..."}.
6. Nếu ảnh không liên quan đến sinh học / cơ thể người (ví dụ ảnh phong cảnh, đồ vật, hoá đơn): đặt status = "unrelated" và ghi lý do ngắn gọn trong "note".
7. Nếu ảnh quá mờ hoặc nhoè không đọc được chữ: đặt status = "unreadable" và ghi lý do trong "note".
8. Chỉ dùng kiến thức đúng SGK Sinh học 11 GDPT 2018. Không bịa số liệu hay cơ chế.

Định dạng JSON BẮT BUỘC trả về:
{
  "status": "ok" | "unrelated" | "unreadable",
  "ocrText": "toàn bộ chữ đọc được từ ảnh",
  "terms": [
    { "text": "thuật ngữ", "bbox": [x, y, w, h], "mesh": "tên_mesh_hợp_lệ_hoặc_null", "short": "giải thích ngắn 1 câu" }
  ],
  "processes": [
    {
      "name": "Tên quá trình",
      "matchedPreset": "pulmonary_circulation" | "systemic_circulation" | null,
      "steps": [
        {
          "id": 1,
          "from": "tên_mesh_hợp_lệ",
          "to": "tên_mesh_hợp_lệ",
          "color": "#1E63D6",
          "title": "Tiêu đề bước",
          "what": "Điều gì xảy ra",
          "why": "Vì sao (nguyên nhân sinh học)",
          "result": "Kết quả",
          "bloodState": "giàu O2 / nghèo O2",
          "valves": "van đóng / mở",
          "keyword": "từ khoá",
          "text": "lời giải thích đầy đủ",
          "focus": "tên_mesh_hợp_lệ",
          "duration": 2.5
        }
      ]
    }
  ],
  "termsOnly": [
    { "text": "thuật ngữ lý thuyết", "explain": "giải thích chi tiết" }
  ],
  "question": {
    "answer": "B",
    "reason": "Giải thích chi tiết vì sao chọn B..."
  } | null,
  "note": ""
}
Lưu ý quan trọng về bbox: [x, y, w, h] là toạ độ tương đối (từ 0.0 đến 1.0) của từ khoá trong ảnh:
x = toạ độ mép trái / chiều rộng ảnh
y = toạ độ mép trên / chiều cao ảnh
w = chiều rộng hộp chữ / chiều rộng ảnh
h = chiều cao hộp chữ / chiều cao ảnh
Trả về DUY NHẤT một khối JSON, không bao bọc bởi markdown hay giải thích thêm bên ngoài.
"""


def _lam_sach_bbox(bbox: Any) -> list[float]:
    if not isinstance(bbox, list) or len(bbox) < 4:
        return [0.0, 0.0, 1.0, 1.0]
    try:
        x, y, w, h = float(bbox[0]), float(bbox[1]), float(bbox[2]), float(bbox[3])
        x = max(0.0, min(1.0, x))
        y = max(0.0, min(1.0, y))
        w = max(0.01, min(1.0 - x, w))
        h = max(0.01, min(1.0 - y, h))
        return [round(x, 4), round(y, 4), round(w, 4), round(h, 4)]
    except Exception:
        return [0.0, 0.0, 1.0, 1.0]


def giai_thich_anh(
    anh_data: str,
    mesh_names: list[str] | None = None,
    processes: list[str] | None = None,
) -> dict:
    """Đọc ảnh sinh học qua Gemini Vision, phân tích OCR, trích xuất thuật ngữ và các bước lộ trình 3D."""
    anh_data = (anh_data or "").strip()
    if not anh_data:
        raise LoiAI("Dữ liệu ảnh trống.")

    # 1. Tách mime type và base64
    mime = "image/jpeg"
    b64 = anh_data
    if "," in anh_data:
        dau, b64 = anh_data.split(",", 1)
        if "image/png" in dau:
            mime = "image/png"
        elif "image/webp" in dau:
            mime = "image/webp"
        elif "image/heic" in dau or "image/heif" in dau:
            mime = "image/heic"

    try:
        raw_bytes = base64.b64decode(b64)
    except Exception as e:
        raise LoiAI("Dữ liệu ảnh không đúng chuẩn base64.") from e

    if len(raw_bytes) > 10 * 1024 * 1024:
        raise LoiAI("Ảnh vượt quá kích thước cho phép (tối đa 10 MB).")

    # 2. Xây dựng tập hợp mesh hợp lệ
    hop_le = set(mesh_names or [])
    if not hop_le:
        hop_le = set(DS_MESH_MAC_DINH)
    else:
        # Bổ sung các mesh chuẩn
        hop_le.update(DS_MESH_MAC_DINH)

    ds_mesh_str = ", ".join(sorted(hop_le))
    ds_presets_str = "pulmonary_circulation (Vòng tuần hoàn nhỏ), systemic_circulation (Vòng tuần hoàn lớn)"
    prompt_he_thong = (
        SYSTEM_DOC_ANH
        + f"\n\nDanh sách MESH_NAMES hợp lệ: {ds_mesh_str}."
        + f"\nDanh sách PROCESSES mẫu: {ds_presets_str}."
    )

    # 3. Gọi Gemini Vision với cơ chế tự động thử lại 1 lần nếu JSON sai
    phien = PhienHoiThoai(prompt_he_thong, effort="low", max_tokens=3500)
    anh_tuple = (mime, b64)

    du_lieu: dict | None = None
    lan_thu = 0
    loi_cuoi = ""

    while lan_thu < 2 and du_lieu is None:
        lan_thu += 1
        try:
            if lan_thu == 1:
                ph = phien.gui(text="Đọc ảnh này và trích xuất JSON theo đúng schema.", anh=anh_tuple)
            else:
                ph = phien.gui(
                    text="JSON trước đó bị lỗi hoặc sai định dạng. Hãy đọc lại và trả về DUY NHẤT một JSON hợp lệ.",
                    anh=anh_tuple
                )
            du_lieu = doc_json(ph.text)
        except Exception as e:
            loi_cuoi = str(e)
            log.warning("Lần %d đọc ảnh thất bại: %s", lan_thu, e)

    if not du_lieu or not isinstance(du_lieu, dict):
        raise LoiAI(f"AI không trích xuất được thông tin từ ảnh: {loi_cuoi or 'Lỗi định dạng JSON'}")

    # 4. Kiểm tra trạng thái trả về (status)
    status = str(du_lieu.get("status") or "ok").strip().lower()
    ocr_text = str(du_lieu.get("ocrText") or "").strip()
    note = str(du_lieu.get("note") or "").strip()

    if status == "unreadable":
        return {
            "status": "unreadable",
            "ocrText": ocr_text,
            "note": note or "Ảnh hơi mờ nên chưa đọc được chữ. Bạn thử chụp lại gần hơn và đủ sáng nhé.",
            "terms": [],
            "processes": [],
            "termsOnly": [],
            "question": None
        }

    if status == "unrelated":
        return {
            "status": "unrelated",
            "ocrText": ocr_text,
            "note": note or "Ảnh này không có nội dung về cơ thể người. Bạn thử ảnh trang sách hoặc câu hỏi Sinh học nhé.",
            "terms": [],
            "processes": [],
            "termsOnly": [],
            "question": None
        }

    # 5. Làm sạch và kiểm tra danh sách thuật ngữ (terms)
    terms_raw = du_lieu.get("terms") if isinstance(du_lieu.get("terms"), list) else []
    terms_out: list[dict] = []
    terms_only_out: list[dict] = []

    for t in terms_raw:
        if not isinstance(t, dict):
            continue
        text = str(t.get("text") or "").strip()
        if not text:
            continue
        bbox = _lam_sach_bbox(t.get("bbox"))
        mesh_raw = t.get("mesh")
        mesh_chuan = _chuan_hoa_mesh(mesh_raw, hop_le)
        short = str(t.get("short") or "").strip()

        if mesh_chuan:
            terms_out.append({
                "text": text,
                "bbox": bbox,
                "mesh": mesh_chuan,
                "short": short or f"Bộ phận {text} trên mô hình 3D."
            })
        else:
            terms_out.append({
                "text": text,
                "bbox": bbox,
                "mesh": None,
                "short": short or text
            })
            terms_only_out.append({
                "text": text,
                "explain": short or text
            })

    # Bổ sung termsOnly do AI trả về
    raw_terms_only = du_lieu.get("termsOnly") if isinstance(du_lieu.get("termsOnly"), list) else []
    for to in raw_terms_only:
        if isinstance(to, dict):
            t_txt = str(to.get("text") or "").strip()
            t_exp = str(to.get("explain") or "").strip()
            if t_txt and not any(x["text"].lower() == t_txt.lower() for x in terms_only_out):
                terms_only_out.append({"text": t_txt, "explain": t_exp or t_txt})

    # 6. Làm sạch và kiểm tra quy trình (processes)
    proc_raw = du_lieu.get("processes") if isinstance(du_lieu.get("processes"), list) else []
    proc_out: list[dict] = []

    for p in proc_raw:
        if not isinstance(p, dict):
            continue
        name = str(p.get("name") or "Quá trình sinh học").strip()
        matched = p.get("matchedPreset")

        # Kiểm tra nếu khớp với preset đã có
        if matched and matched in PRESETS_QUY_TRINH:
            preset_data = PRESETS_QUY_TRINH[matched]
            proc_out.append({
                "name": preset_data["name"],
                "matchedPreset": matched,
                "aiGenerated": False,
                "steps": preset_data["steps"]
            })
            continue

        # Thử nhận diện tự động preset qua tên
        ten_thuong = name.lower()
        if "tuần hoàn nhỏ" in ten_thuong or "phổi" in ten_thuong and "tuần hoàn" in ten_thuong:
            preset_data = PRESETS_QUY_TRINH["pulmonary_circulation"]
            proc_out.append({
                "name": preset_data["name"],
                "matchedPreset": "pulmonary_circulation",
                "aiGenerated": False,
                "steps": preset_data["steps"]
            })
            continue
        elif "tuần hoàn lớn" in ten_thuong or "toàn thân" in ten_thuong and "tuần hoàn" in ten_thuong:
            preset_data = PRESETS_QUY_TRINH["systemic_circulation"]
            proc_out.append({
                "name": preset_data["name"],
                "matchedPreset": "systemic_circulation",
                "aiGenerated": False,
                "steps": preset_data["steps"]
            })
            continue

        # Quá trình mới do AI tự tạo: kiểm tra từng bước theo MESH_NAMES
        steps_raw = p.get("steps") if isinstance(p.get("steps"), list) else []
        steps_valid: list[dict] = []

        for idx, s in enumerate(steps_raw):
            if not isinstance(s, dict):
                continue
            f_mesh = _chuan_hoa_mesh(s.get("from"), hop_le)
            t_mesh = _chuan_hoa_mesh(s.get("to"), hop_le)

            if not f_mesh or not t_mesh:
                log.warning("Bỏ bước %d của '%s' vì mesh không hợp lệ: from=%s, to=%s", idx + 1, name, s.get("from"), s.get("to"))
                continue

            color = str(s.get("color") or "#E53935").strip()
            if not color.startswith("#"):
                color = "#E53935"

            title = str(s.get("title") or s.get("label") or f"Bước {len(steps_valid) + 1}").strip()
            what = str(s.get("what") or "").strip()
            why = str(s.get("why") or "").strip()
            result = str(s.get("result") or "").strip()
            bloodState = str(s.get("bloodState") or "").strip()
            valves = str(s.get("valves") or "").strip()
            keyword = str(s.get("keyword") or "").strip()

            text = str(s.get("text") or "").strip()
            if not text:
                parts = [p_str for p_str in [what, why, f"Kết quả: {result}" if result else ""] if p_str]
                text = ". ".join(parts) or title

            steps_valid.append({
                "id": len(steps_valid) + 1,
                "from": f_mesh,
                "to": t_mesh,
                "color": color,
                "title": title,
                "label": title,
                "what": what,
                "why": why,
                "result": result,
                "bloodState": bloodState,
                "valves": valves,
                "keyword": keyword,
                "text": text,
                "focus": _chuan_hoa_mesh(s.get("focus"), hop_le) or f_mesh,
                "duration": float(s.get("duration") or 2.5),
                "aiGenerated": True
            })

        if steps_valid:
            proc_out.append({
                "name": name,
                "matchedPreset": None,
                "aiGenerated": True,
                "steps": steps_valid
            })
        else:
            # Nếu lọc xong không còn bước nào, đưa mô tả vào termsOnly
            terms_only_out.append({
                "text": name,
                "explain": f"Quá trình {name} trong ảnh không chứa đủ toạ độ khớp với mô hình 3D."
            })

    # 7. Xử lý câu hỏi trắc nghiệm / đúng sai
    question_out = None
    q_raw = du_lieu.get("question")
    if isinstance(q_raw, dict) and (q_raw.get("answer") or q_raw.get("reason")):
        question_out = {
            "answer": str(q_raw.get("answer") or "").strip(),
            "reason": str(q_raw.get("reason") or "").strip()
        }

    return {
        "status": "ok",
        "ocrText": ocr_text,
        "terms": terms_out,
        "processes": proc_out,
        "termsOnly": terms_only_out,
        "question": question_out,
        "note": note
    }

