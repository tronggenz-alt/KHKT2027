"""Kiểm tra tính năng 'Giải thích từ ảnh' của Lab Sinh học 3D:
- 4 kịch bản ảnh mẫu: (1) SGK vòng tuần hoàn nhỏ, (2) Trắc nghiệm chu kì tim, (3) Thuật ngữ lý thuyết không có mesh, (4) Ảnh không liên quan.
- Xử lý ảnh mờ (unreadable).
- Giới hạn tần suất 20 ảnh/ngày (theo người dùng hoặc IP).
- Lọc bỏ bước có mesh không hợp lệ.
- Bảo mật: không để lộ API key ra frontend.
- Cả hai route /api/sinh-hoc/giai-thich-anh và /api/explain-image.
"""
import base64
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import MagicMock

import pytest

from backend import sinh_hoc_ai
from backend.bao_mat import gioi_han_tan_suat
from backend.llm import PhienHoiThoai

# Ảnh 1x1 GIF base64 hợp lệ để test
ANH_DUMMY_B64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="


def _tao_csrf(client):
    r = client.get("/api/toi")
    assert r.status_code == 200
    return {"X-CSRF-Token": r.json()["csrf"]}


# =====================================================================================================================
# 1. KỊCH BẢN 1: Đoạn SGK về vòng tuần hoàn nhỏ -> Khớp với quá trình mẫu (pulmonary_circulation)
# =====================================================================================================================
def test_kich_ban_1_sgk_tuan_hoan_nho(monkeypatch):
    phan_hoi_ai = {
        "status": "ok",
        "ocrText": "Vòng tuần hoàn nhỏ dẫn máu từ tâm thất phải lên phổi qua động mạch phổi...",
        "terms": [
            {"text": "tâm thất phải", "bbox": [0.1, 0.2, 0.3, 0.05], "mesh": "tam_that_phai", "short": "Buồng tim bơm máu lên phổi"},
            {"text": "động mạch phổi", "bbox": [0.1, 0.3, 0.35, 0.05], "mesh": "dong_mach_phoi", "short": "Dẫn máu nghèo O2 lên phổi"},
            {"text": "mao mạch phổi", "bbox": [0.2, 0.4, 0.3, 0.05], "mesh": "phoi_phai", "short": "Nơi diễn ra trao đổi khí O2 và CO2"}
        ],
        "processes": [
            {
                "name": "Vòng tuần hoàn nhỏ",
                "matchedPreset": "pulmonary_circulation",
                "steps": []
            }
        ],
        "termsOnly": [],
        "question": None,
        "note": ""
    }

    mock_phien = MagicMock()
    mock_phien.gui.return_value = SimpleNamespace(text=json.dumps(phan_hoi_ai, ensure_ascii=False), nha_cung_cap="gemini", model="gemini-2.5-flash")
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", lambda *a, **k: mock_phien)

    kq = sinh_hoc_ai.giai_thich_anh(ANH_DUMMY_B64)
    assert kq["status"] == "ok"
    assert len(kq["processes"]) == 1
    p = kq["processes"][0]
    assert p["matchedPreset"] == "pulmonary_circulation"
    assert p["aiGenerated"] is False
    assert len(p["steps"]) == 6  # preset mẫu chuẩn có 6 bước
    assert p["steps"][0]["from"] == "right_ventricle"
    assert p["steps"][0]["to"] == "pulmonary_artery"
    assert "what" in p["steps"][0] and "why" in p["steps"][0] and "valves" in p["steps"][0] and "bloodState" in p["steps"][0]


# =====================================================================================================================
# 2. KỊCH BẢN 2: Câu trắc nghiệm về chu kì tim -> Có giải thích bản chất + đáp án & lý do
# =====================================================================================================================
def test_kich_ban_2_trac_nghiem_chu_ki_tim(monkeypatch):
    phan_hoi_ai = {
        "status": "ok",
        "ocrText": "Câu 12: Trong một chu kì tim của người bình thường kéo dài 0,8s, thời gian pha thất thu là:\nA. 0,1s\nB. 0,3s\nC. 0,4s\nD. 0,5s",
        "terms": [
            {"text": "chu kì tim", "bbox": [0.05, 0.1, 0.25, 0.04], "mesh": "tam_that_trai", "short": "Một chu kì hoạt động của tim gồm pha co tâm nhĩ, co tâm thất và dãn chung"},
            {"text": "thất thu", "bbox": [0.1, 0.2, 0.2, 0.04], "mesh": "tam_that_trai", "short": "Pha co tâm thất, kéo dài 0,3s"}
        ],
        "processes": [],
        "termsOnly": [
            {"text": "chu kì tim", "explain": "Chu kì tim kéo dài 0,8s: co nhĩ 0,1s, co thất 0,3s, dãn chung 0,4s."}
        ],
        "question": {
            "answer": "B",
            "reason": "Theo SGK Sinh học 11, chu kì tim ở người bình thường kéo dài khoảng 0,8 s gồm: pha co tâm nhĩ (0,1 s), pha co tâm thất hay thất thu (0,3 s), pha dãn chung (0,4 s). Do đó đáp án đúng là B."
        },
        "note": ""
    }

    mock_phien = MagicMock()
    mock_phien.gui.return_value = SimpleNamespace(text=json.dumps(phan_hoi_ai, ensure_ascii=False), nha_cung_cap="gemini", model="gemini-2.5-flash")
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", lambda *a, **k: mock_phien)

    kq = sinh_hoc_ai.giai_thich_anh(ANH_DUMMY_B64)
    assert kq["status"] == "ok"
    assert kq["question"] is not None
    assert kq["question"]["answer"] == "B"
    assert "0,3" in kq["question"]["reason"]
    assert len(kq["terms"]) == 2


# =====================================================================================================================
# 3. KỊCH BẢN 3: Thuật ngữ không gắn được mesh ("tính tự động của tim") -> Đưa vào termsOnly
# =====================================================================================================================
def test_kich_ban_3_thuat_ngu_ly_thuyet_khong_mesh(monkeypatch):
    phan_hoi_ai = {
        "status": "ok",
        "ocrText": "Tính tự động của tim là khả năng co dãn tự động theo chu kì của tim nhờ hệ dẫn truyền tim.",
        "terms": [
            {"text": "tính tự động của tim", "bbox": [0.05, 0.1, 0.4, 0.05], "mesh": None, "short": "Khả năng co dãn tự động theo chu kì của tim"},
            {"text": "hệ dẫn truyền tim", "bbox": [0.05, 0.2, 0.35, 0.05], "mesh": "nút_xoang_nhĩ", "short": "Tập hợp các cấu trúc phát và truyền xung điện"}
        ],
        "processes": [],
        "termsOnly": [
            {"text": "tính tự động của tim", "explain": "Khả năng tim tự co dãn nhịp nhàng do các xung điện phát ra từ nút xoang nhĩ."}
        ],
        "question": None,
        "note": ""
    }

    mock_phien = MagicMock()
    mock_phien.gui.return_value = SimpleNamespace(text=json.dumps(phan_hoi_ai, ensure_ascii=False), nha_cung_cap="gemini", model="gemini-2.5-flash")
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", lambda *a, **k: mock_phien)

    kq = sinh_hoc_ai.giai_thich_anh(ANH_DUMMY_B64)
    assert kq["status"] == "ok"
    assert any(t["text"] == "tính tự động của tim" and t["mesh"] is None for t in kq["terms"])
    assert any("tính tự động của tim" in to["text"] for to in kq["termsOnly"])


# =====================================================================================================================
# 4. KỊCH BẢN 4: Ảnh không liên quan (phong cảnh, hoá đơn...) -> Báo status = "unrelated"
# =====================================================================================================================
def test_kich_ban_4_anh_khong_lien_quan(monkeypatch):
    phan_hoi_ai = {
        "status": "unrelated",
        "ocrText": "Hoá đơn thanh toán tiền điện tháng 10...",
        "terms": [],
        "processes": [],
        "termsOnly": [],
        "question": None,
        "note": "Ảnh này không có nội dung về cơ thể người hay Sinh học."
    }

    mock_phien = MagicMock()
    mock_phien.gui.return_value = SimpleNamespace(text=json.dumps(phan_hoi_ai, ensure_ascii=False), nha_cung_cap="gemini", model="gemini-2.5-flash")
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", lambda *a, **k: mock_phien)

    kq = sinh_hoc_ai.giai_thich_anh(ANH_DUMMY_B64)
    assert kq["status"] == "unrelated"
    assert "cơ thể người" in kq["note"] or "Sinh học" in kq["note"]


# =====================================================================================================================
# 5. XỬ LÝ ẢNH MỜ (status = "unreadable")
# =====================================================================================================================
def test_anh_mo_unreadable(monkeypatch):
    phan_hoi_ai = {
        "status": "unreadable",
        "ocrText": "",
        "terms": [],
        "processes": [],
        "termsOnly": [],
        "question": None,
        "note": "Ảnh hơi mờ nên chưa đọc được chữ. Bạn thử chụp lại gần hơn và đủ sáng nhé."
    }

    mock_phien = MagicMock()
    mock_phien.gui.return_value = SimpleNamespace(text=json.dumps(phan_hoi_ai, ensure_ascii=False), nha_cung_cap="gemini", model="gemini-2.5-flash")
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", lambda *a, **k: mock_phien)

    kq = sinh_hoc_ai.giai_thich_anh(ANH_DUMMY_B64)
    assert kq["status"] == "unreadable"
    assert "chụp lại gần hơn" in kq["note"]


# =====================================================================================================================
# 6. LỌC BỎ BƯỚC CÓ MESH KHÔNG HỢP LỆ VÀ GẮN NHÃN AI
# =====================================================================================================================
def test_loc_bo_buoc_mesh_khong_hop_le(monkeypatch):
    phan_hoi_ai = {
        "status": "ok",
        "ocrText": "Đường dẫn truyền...",
        "terms": [],
        "processes": [
            {
                "name": "Quy trình thử nghiệm",
                "matchedPreset": None,
                "steps": [
                    {
                        "id": 1,
                        "from": "tam_that_phai",
                        "to": "dong_mach_phoi",
                        "color": "#1E63D6",
                        "title": "Hợp lệ",
                        "what": "Co bóp",
                        "why": "Áp lực tăng"
                    },
                    {
                        "id": 2,
                        "from": "mesh_bip_bop_khong_co_that",
                        "to": "tam_that_phai",
                        "color": "#E53935",
                        "title": "Không hợp lệ",
                        "what": "Bịa đặt",
                        "why": "Không có"
                    }
                ]
            }
        ],
        "termsOnly": [],
        "question": None,
        "note": ""
    }

    mock_phien = MagicMock()
    mock_phien.gui.return_value = SimpleNamespace(text=json.dumps(phan_hoi_ai, ensure_ascii=False), nha_cung_cap="gemini", model="gemini-2.5-flash")
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", lambda *a, **k: mock_phien)

    kq = sinh_hoc_ai.giai_thich_anh(ANH_DUMMY_B64, mesh_names=["tam_that_phai", "dong_mach_phoi"])
    assert len(kq["processes"]) == 1
    p = kq["processes"][0]
    assert p["aiGenerated"] is True
    # Bước 2 bị bỏ do mesh_bip_bop_khong_co_that không tồn tại
    assert len(p["steps"]) == 1
    assert p["steps"][0]["title"] == "Hợp lệ"
    assert p["steps"][0]["aiGenerated"] is True


# =====================================================================================================================
# 7. GIỚI HẠN TẦN SUẤT: 20 ẢNH/NGÀY (THEO IP VÀ THEO USER)
# =====================================================================================================================
def test_gioi_han_tan_suat_20_anh_ngay(client, monkeypatch):
    gioi_han_tan_suat.xoa()
    h = _tao_csrf(client)

    # Giả lập backend AI luôn trả về kết quả nhanh
    monkeypatch.setattr(sinh_hoc_ai, "giai_thich_anh", lambda *a, **k: {"status": "ok", "terms": [], "processes": [], "termsOnly": []})
    from backend import tai_khoan
    from backend.llm import KHOA_CHU_APP
    monkeypatch.setattr(tai_khoan, "chon_khoa_doc_anh", lambda u: KHOA_CHU_APP)

    payload = {"anh": ANH_DUMMY_B64}

    mock_time = 1000.0
    monkeypatch.setattr(gioi_han_tan_suat, "_dong_ho", lambda: mock_time)

    # Quy tắc: [(2, 60), (10, 3600), (20, 86400)]
    # Tăng mock_time mỗi lần 400s (>60s chống spam phút, <3600s/10 chống spam giờ, tổng 20*400s = 8000s < 86400s)
    for i in range(20):

        mock_time += 400.0
        r = client.post("/api/sinh-hoc/giai-thich-anh", json=payload, headers=h)
        assert r.status_code == 200, f"Lần thứ {i+1} phải thành công nhưng nhận {r.status_code}: {r.text}"

    # Lần thứ 21 trong cùng 24h (86400s) phải bị từ chối 429 với thông báo quá 20 lượt
    mock_time += 400.0
    r_chan = client.post("/api/sinh-hoc/giai-thich-anh", json=payload, headers=h)
    assert r_chan.status_code == 429
    assert "20 lượt giải thích ảnh" in r_chan.json()["loi"]



# =====================================================================================================================
# 8. CẢ HAI ROUTE /api/sinh-hoc/giai-thich-anh VÀ /api/explain-image HOẠT ĐỘNG TƯƠNG ĐƯƠNG
# =====================================================================================================================
def test_endpoint_explain_image_alias(client, monkeypatch):
    gioi_han_tan_suat.xoa()
    h = _tao_csrf(client)

    monkeypatch.setattr(sinh_hoc_ai, "giai_thich_anh", lambda *a, **k: {
        "status": "ok",
        "ocrText": "Vòng tuần hoàn",
        "terms": [{"text": "tim", "bbox": [0,0,1,1], "mesh": "tam_that_trai", "short": "Tim"}],
        "processes": [],
        "termsOnly": [],
        "question": None,
        "note": ""
    })
    from backend import tai_khoan
    from backend.llm import KHOA_CHU_APP
    monkeypatch.setattr(tai_khoan, "chon_khoa_doc_anh", lambda u: KHOA_CHU_APP)

    payload = {"anh": ANH_DUMMY_B64}
    r = client.post("/api/explain-image", json=payload, headers=h)
    assert r.status_code == 200
    d = r.json()
    assert d["status"] == "ok"
    assert d["ocrText"] == "Vòng tuần hoàn"


# =====================================================================================================================
# 9. BẢO MẬT: KHÔNG LỘ API KEY RA FRONTEND
# =====================================================================================================================
def test_bao_mat_khong_co_api_key_o_frontend():
    html_path = Path(__file__).resolve().parents[1] / "frontend" / "biology-lab-3d.html"
    noi_dung = html_path.read_text(encoding="utf-8")

    # Kiểm tra các chuỗi nhạy cảm
    assert "AIzaSy" not in noi_dung  # Tiền tố Google Gemini API Key thật
    assert "sk-ant-" not in noi_dung  # Tiền tố Anthropic Claude API Key
    assert "sk-proj-" not in noi_dung  # Tiền tố OpenAI API Key
    assert "gemini_api_key" not in noi_dung.lower()
    assert "x-goog-api-key" not in noi_dung.lower()

    # Kiểm tra chỉ gọi backend
    assert "/api/sinh-hoc/giai-thich-anh" in noi_dung
    assert "X-CSRF-Token" in noi_dung
