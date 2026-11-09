"""Kiểm thử hợp đồng API của bản Cloudflare qua HTTP (cùng đường dẫn, mã lỗi, dữ liệu với bản FastAPI).

    python cloudflare/kiem_thu.py http://127.0.0.1:8788            đủ bộ, trên `wrangler pages dev` (D1 cục bộ)
    python cloudflare/kiem_thu.py https://verlab.pages.dev --nhe   bộ nhẹ trên mạng: chỉ đăng nhập, quyền, nhịp; không tạo lớp, học sinh

Mật khẩu tài khoản thử đọc từ data/tai_khoan_mau_mat_khau.json (git bỏ qua), không in ra.
"""

from __future__ import annotations

import io
import json
import secrets
import sys
import time
from pathlib import Path

import httpx

GOC = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://127.0.0.1:8788"
NHE = "--nhe" in sys.argv
MK = json.loads((Path(__file__).resolve().parents[1] / "data/tai_khoan_mau_mat_khau.json").read_text(encoding="utf-8"))["mat_khau"]
DAT, HONG = [], []


def kiem(ten: str, dieu_kien: bool, chi_tiet: object = "") -> None:
    (DAT if dieu_kien else HONG).append(ten)
    print(("  đạt " if dieu_kien else "  HỎNG ") + ten + ("" if dieu_kien else f"  ← {str(chi_tiet)[:300]}"))


def phien() -> httpx.Client:
    return httpx.Client(base_url=GOC, follow_redirects=False, timeout=30, headers={"origin": GOC})


def dang_nhap(ten: str, mat_khau: str, kieu: str = "web") -> tuple[httpx.Client, httpx.Response]:
    c = phien()
    t0 = time.perf_counter()
    r = c.post("/auth/dang-nhap", json={"ten_dang_nhap": ten, "mat_khau": mat_khau, "kieu": kieu})
    r.extensions["giay"] = time.perf_counter() - t0
    if kieu == "ung_dung" and r.status_code == 200:
        c.headers["authorization"] = "Bearer " + r.json()["ma_phien"]
        del c.headers["origin"]
    return c, r


def anh_jpeg(chu: str) -> bytes:
    from PIL import Image, ImageDraw
    a = Image.new("RGB", (1600, 900), "white")
    ImageDraw.Draw(a).text((80, 80), chu, fill="black")
    ra = io.BytesIO()
    a.save(ra, "JPEG", quality=80)
    return ra.getvalue()


print(f"Kiểm thử {GOC}{' (bộ nhẹ)' if NHE else ''}")
an = phien()

# ---- trang và quyền khi chưa đăng nhập ----
r = an.get("/")
kiem("trang giới thiệu vẫn ở /", r.status_code == 200 and "VERA" in r.text, r.status_code)
r = an.get("/quan-ly")
kiem("/quan-ly chưa đăng nhập về /dang-nhap", r.status_code == 303 and r.headers["location"] == "/dang-nhap", r.headers.get("location"))
r = an.get("/dang-nhap")
kiem("trang đăng nhập", r.status_code == 200 and "text/html" in r.headers["content-type"] and r.headers.get("cache-control") == "no-store", r.headers)
r = an.get("/giao-vien")
kiem("trang giáo viên chưa đăng nhập chuyển hướng", r.status_code == 303 and r.headers["location"].startswith("/dang-nhap?tiep="), r.headers.get("location"))
for pm, dd in [("GET", "/api/lop"), ("GET", "/api/khoa"), ("POST", "/chu-thich"), ("POST", "/app/nhip"), ("GET", "/lab/sinh/ket-qua")]:
    r = an.request(pm, dd, json={})
    kiem(f"{pm} {dd} chưa đăng nhập trả 401", r.status_code == 401, r.status_code)
r = an.get("/suc-khoe")
kiem("/suc-khoe công khai, ẩn nhà cung cấp", r.status_code == 200 and r.json()["nha_cung_cap"] == "" and r.json()["mo_hinh"] == "", r.text)
r = an.get("/tinh/phien.js")
kiem("tệp tĩnh /tinh", r.status_code == 200, r.status_code)

# ---- đăng nhập giáo viên ----
_, r = dang_nhap("gv.thu1", "sai-mat-khau-123")
kiem("sai mật khẩu trả 401", r.status_code == 401, r.text)
gv, r = dang_nhap("gv.thu1", MK["gv.thu1"])
kiem("giáo viên đăng nhập", r.status_code == 200 and r.json()["nguoi_dung"]["vai_tro"] == "giao_vien", r.text)
print(f"    (thời gian đăng nhập {r.extensions['giay'] * 1000:.0f} ms)")
ck = r.headers.get("set-cookie", "")
kiem("cookie HttpOnly, SameSite=Lax", "vera_phien=" in ck and "HttpOnly" in ck and "SameSite=Lax" in ck, ck)
if GOC.startswith("https"):
    kiem("cookie Secure qua https", "Secure" in ck, ck)
kiem("không trả ma_phien cho web", "ma_phien" not in r.json(), r.text)
r = gv.get("/auth/toi")
kiem("/auth/toi", r.status_code == 200 and r.json()["ten_dang_nhap"] == "gv.thu1" and "mat_khau_bam" not in r.text, r.text)
lop_a = r.json()["lop"][0]["id"] if r.json().get("lop") else None
r = gv.get("/quan-ly")
kiem("/quan-ly đã đăng nhập vào trang giáo viên", r.status_code == 303 and r.headers["location"] == "/giao-vien", r.headers.get("location"))
r = gv.get("/giao-vien")
kiem("trang giáo viên", r.status_code == 200 and "<html" in r.text.lower(), r.status_code)
r = gv.post("/api/lop", json={"ten": "x"}, headers={"origin": "https://trang-la.example"})
kiem("POST từ trang khác bị chặn 403", r.status_code == 403, r.status_code)

# ---- học sinh ----
hs, r = dang_nhap("hs.thu1", MK["hs.thu1"], "ung_dung")
kiem("học sinh đăng nhập kiểu ứng dụng nhận ma_phien", r.status_code == 200 and len(r.json().get("ma_phien", "")) > 30, r.text)
r = hs.get("/api/lop")
kiem("học sinh không vào API giáo viên (403)", r.status_code == 403, r.status_code)
hs_web, _ = dang_nhap("hs.thu1", MK["hs.thu1"])
r = hs_web.get("/giao-vien")
kiem("học sinh mở trang giáo viên bị chuyển về /hoc-sinh", r.status_code == 303 and r.headers["location"] == "/hoc-sinh", r.headers.get("location"))
r = hs_web.get("/hoc-sinh")
kiem("trang học sinh", r.status_code == 200, r.status_code)
r = phien().get("/auth/toi", headers={"authorization": "Bearer ma-sai"})
kiem("Bearer sai trả 401", r.status_code == 401, r.status_code)

# ---- nhịp app ----
r = hs.post("/app/nhip", json={"tang": 1, "x": 16.9, "z": 13.1, "dang_cam": "Bình cầu đáy tròn"})
kiem("nhịp app", r.status_code == 200 and {"may_chu_luc", "trinh_chieu", "lenh", "chu_y", "buoi"} <= set(r.json()), r.text)
r = hs.post("/app/nhip", json={"tang": 7})
kiem("nhịp sai tầng trả 422", r.status_code == 422, r.status_code)
if lop_a:
    r = gv.get(f"/api/lop/{lop_a}/truc-tiep")
    me = next((h for h in r.json().get("hoc_sinh", []) if h["ten_dang_nhap"] == "hs.thu1"), {})
    kiem("theo dõi trực tiếp thấy học sinh trong phòng thí nghiệm", r.status_code == 200 and me.get("dang_mo") and me.get("khu") == "phong_lab" and me.get("dang_cam") == "Bình cầu đáy tròn", me)

if not NHE:
    # ---- lớp, học sinh ----
    ten_lop = "Lớp kiểm thử " + secrets.token_hex(3)
    r = gv.post("/api/lop", json={"ten": ten_lop, "nam_hoc": "2026-2027"})
    kiem("tạo lớp", r.status_code == 200 and len(r.json()["ma_lop"]) == 6 and r.json()["ma_lop_mo"] is True, r.text)
    lop = r.json()["id"]
    goc = "kt" + secrets.token_hex(3)
    r = gv.post(f"/api/lop/{lop}/hoc-sinh", json={"danh_sach": f"Nguyễn Văn An\nTrần Thị Bình, {goc}.binh\nHọc sinh Thử Một\ths.thu1\nX\nHọc sinh Lạ, gv.thu2"})
    j = r.json()
    kiem("dán danh sách: 2 tạo, 1 thêm, 2 lỗi", r.status_code == 200 and len(j["tao"]) == 2 and len(j["them"]) == 1 and len(j["loi"]) == 2, r.text)
    kiem("tên đăng nhập tự sinh từ họ tên", any(t["ten_dang_nhap"].startswith("nguyen.van.an") for t in j["tao"]), j)
    moi = next(t for t in j["tao"] if t["ten_dang_nhap"] == f"{goc}.binh")
    r = gv.get(f"/api/lop/{lop}/hoc-sinh")
    kiem("danh sách học sinh của lớp", r.status_code == 200 and len(r.json()) == 3, r.text)
    r = gv.post("/api/lop", json={"ten": ""})
    kiem("tên lớp trống trả 422", r.status_code == 422, r.status_code)
    gv2, _ = dang_nhap("gv.thu2", MK["gv.thu2"])
    r = gv2.get(f"/api/lop/{lop}/hoc-sinh")
    kiem("giáo viên khác không thấy lớp (404)", r.status_code == 404, r.status_code)

    hsm, r = dang_nhap(moi["ten_dang_nhap"], moi["mat_khau_tam"], "ung_dung")
    kiem("học sinh mới đăng nhập bằng mật khẩu tạm", r.status_code == 200 and r.json()["nguoi_dung"]["phai_doi_mat_khau"], r.text)
    r = hsm.get("/api/toi/ket-qua")
    kiem("phải đổi mật khẩu trước (403)", r.status_code == 403 and r.json().get("phai_doi_mat_khau"), r.text)
    r = hsm.post("/auth/doi-mat-khau", json={"mat_khau_cu": moi["mat_khau_tam"], "mat_khau_moi": "Moi-" + secrets.token_hex(6)})
    kiem("đổi mật khẩu", r.status_code == 200 and not r.json()["phai_doi_mat_khau"], r.text)
    r = hsm.get("/api/toi/ket-qua")
    kiem("sau khi đổi dùng được", r.status_code == 200 and r.json() == [], r.text)
    r = gv.post(f"/api/lop/{lop}/hoc-sinh/{moi['id']}/dat-lai-mat-khau")
    kiem("đặt lại mật khẩu, hủy phiên cũ", r.status_code == 200 and len(r.json()["mat_khau_tam"]) == 10 and hsm.get("/auth/toi").status_code == 401, r.text)

    # ---- kết quả, chấm điểm ----
    r = hs.post("/lab/sinh/su-kien", json={"su_kien": [{"loai": "ket_qua_tn", "du_lieu": {"ma_bai": "bai_kiem_thu", "hoc_sinh": "Giả", "diem": 10, "du_lieu": {"tom_tat": "OK"}}}]})
    kiem("app nộp kết quả", r.status_code == 200 and r.json()["nhan"] == 1, r.text)
    r = hs.get("/api/toi/ket-qua")
    kq = r.json()[0] if r.status_code == 200 and r.json() else {}
    kiem("kết quả gắn tài khoản, điểm học sinh tự ghi bị bỏ", kq.get("diem") is None and kq.get("tom_tat") == "OK", r.text)
    r = gv.patch(f"/lab/sinh/ket-qua/{kq.get('id')}", json={"diem": 8.5, "nhan_xet": "Tốt"})
    kiem("giáo viên chấm điểm", r.status_code == 200 and r.json()["diem"] == 8.5, r.text)
    r = gv.patch(f"/lab/sinh/ket-qua/{kq.get('id')}", json={"diem": 11})
    kiem("điểm ngoài 0–10 trả 422", r.status_code == 422, r.status_code)
    r = gv2.patch(f"/lab/sinh/ket-qua/{kq.get('id')}", json={"diem": 1})
    kiem("giáo viên khác không chấm được (404)", r.status_code == 404, r.status_code)
    r = gv.get(f"/api/lop/{lop_a}/ket-qua.csv")
    kiem("xuất CSV", r.status_code == 200 and r.content.startswith(b"\xef\xbb\xbf") and "Học sinh" in r.text, r.status_code)

    # ---- trình chiếu ----
    r = gv.post("/api/bai-giang", data={"ten": "Bài kiểm thử"}, files=[("tep", ("2.jpg", anh_jpeg("Trang 2"), "image/jpeg")), ("tep", ("1.jpg", anh_jpeg("Trang 1"), "image/jpeg"))])
    kiem("tải bài giảng (ảnh)", r.status_code == 200 and r.json()["so_trang"] == 2, r.text)
    bai = r.json().get("id")
    r = gv.post("/api/bai-giang", data={"ten": "PDF thô"}, files=[("tep", ("a.pdf", b"%PDF-1.4 abc", "application/pdf"))])
    kiem("PDF chưa chuyển ảnh bị từ chối 422", r.status_code == 422, r.text)
    r = gv.post(f"/api/lop/{lop_a}/trinh-chieu", json={"loai": "slide", "bai_id": bai, "trang": 2})
    kiem("chiếu trang 2", r.status_code == 200 and r.json()["dang_chieu"]["anh"] == f"/api/bai-giang/{bai}/trang/2", r.text)
    r = hs.post("/app/nhip", json={"tang": 1, "x": 5, "z": 5})
    tc = r.json()["trinh_chieu"]
    kiem("app nhận slide qua nhịp", tc["loai"] == "slide" and tc["anh"].endswith("/trang/2"), tc)
    r = hs.get(tc["anh"])
    kiem("học sinh lớp đang chiếu tải được ảnh", r.status_code == 200 and r.headers["content-type"] == "image/jpeg" and r.content[:3] == b"\xff\xd8\xff", r.status_code)
    hs3, _ = dang_nhap("hs.thu3", MK["hs.thu3"], "ung_dung")
    kiem("học sinh lớp khác không tải được (404)", hs3.get(tc["anh"]).status_code == 404)
    r = hs.post("/app/nhip", json={"da_hien": tc["phien_ban"], "tang": 1, "x": 5, "z": 5})
    r = gv.get(f"/api/lop/{lop_a}/trinh-chieu")
    kiem("giáo viên thấy máy đã hiện và độ trễ", r.json()["may_da_hien"] >= 1 and r.json()["do_tre_ms"] is not None, r.text)

    # ---- buổi học, robot, thông báo ----
    r = gv.post(f"/api/lop/{lop_a}/buoi-hoc", json={"che_do": "kiem_tra", "phut": 15, "khoa_loai": ["doc"]})
    kiem("bật chế độ Kiểm tra, khóa chất độc", r.status_code == 200 and r.json()["che_do"] == "kiem_tra" and r.json()["so_chat_khoa"] > 0, r.text)
    b = hs.post("/app/nhip", json={}).json()["buoi"]
    kiem("app nhận Kiểm tra và danh sách khóa", b["che_do"] == "kiem_tra" and not b["ai_bat"] and len(b["hoa_chat_khoa"]) > 0, b)
    b2 = hs.post("/app/nhip", json={"da_buoi": b["phien_ban"]}).json()["buoi"]
    kiem("không gửi lại danh sách khi app đã có", b2["hoa_chat_khoa"] is None, b2)
    r = hs.post("/hoi", json={"cau_hoi": "Vì sao?", "ngu_canh": {}})
    kiem("AI của học sinh bị tắt khi Kiểm tra", r.json().get("nguon") == "khoa", r.text)
    r = gv.post(f"/api/lop/{lop_a}/robot", json={"hanh_dong": "lay_hoa_chat", "stt": 1})
    buoc = r.json()["ke_hoach"][0] if r.status_code == 200 else {}
    kiem("lệnh robot lấy hóa chất gửi stt số", buoc.get("stt") == 1 and buoc.get("doi_tuong") == "hoa_chat:1" and "hoa_chat" not in buoc, r.text)
    r = gv.post(f"/api/lop/{lop_a}/thong-bao", json={"noi_dung": "Cả lớp chú ý"})
    r2 = gv.post(f"/api/lop/{lop_a}/chu-y", json={"bat": True})
    j = hs.post("/app/nhip", json={}).json()
    kiem("app nhận thông báo, lệnh robot, chú ý", {"robot", "thong_bao"} <= {x["loai"] for x in j["lenh"]} and j["chu_y"]["bat"], j)
    gv.post(f"/api/lop/{lop_a}/chu-y", json={"bat": False})
    gv.post(f"/api/lop/{lop_a}/buoi-hoc", json={"che_do": "luyen_tap"})
    gv.post(f"/api/lop/{lop_a}/trinh-chieu", json={"loai": "tat"})
    kiem("xóa bài giảng", gv.delete(f"/api/bai-giang/{bai}").json() == {"ok": True})

    # ---- phòng hóa, phòng sinh ----
    r = gv.get("/api/hoa-chat", params={"q": "dong sunfat"})
    kiem("tìm hóa chất không dấu", r.status_code == 200 and r.json()["tong"] >= 1, r.text)
    r = gv.get("/api/tong-quan")
    kiem("tổng quan kho hóa chất", r.status_code == 200 and r.json()["so_hoa_chat"] >= 700, r.text[:200])
    r = gv.get("/api/thiet-bi")
    kiem("thiết bị", r.status_code == 200 and len(r.json()) > 10, r.status_code)
    r = gv.get("/lab/sinh/dung-cu")
    kiem("dụng cụ sinh học", r.status_code == 200 and r.json()["tong"] >= 100, r.status_code)
    r = gv.get("/lab/sinh/tong-quan")
    kiem("tổng quan phòng sinh", r.status_code == 200 and "dung_cu" in r.json(), r.text[:200])
    r = hs.get("/lab/sinh/bai-thi-nghiem")
    kiem("học sinh xem bài thí nghiệm không có đáp án", r.status_code == 200 and all("ket_qua_mong_doi" not in str(b["buoc"]) for b in r.json()["muc"]), r.status_code)
    r = gv.get("/may")
    kiem("hồ sơ máy", r.status_code == 200 and len(r.json()) >= 10, r.status_code)

    # ---- giới hạn sai ----
    ten_la = "khongco" + secrets.token_hex(3)
    ma = [phien().post("/auth/dang-nhap", json={"ten_dang_nhap": ten_la, "mat_khau": "sai-sai-sai"}).status_code for _ in range(6)]
    kiem("sai 5 lần bị khóa 429", ma[:5] == [401] * 5 and ma[5] == 429, ma)

# ---- đăng xuất ----
r = gv.post("/auth/dang-xuat")
kiem("đăng xuất", r.status_code == 200 and gv.get("/auth/toi").status_code == 401, r.text)
r = hs.post("/auth/dang-xuat")
kiem("app đăng xuất thu hồi mã", r.status_code == 200 and hs.get("/auth/toi").status_code == 401, r.text)

print(f"\n{len(DAT)} đạt, {len(HONG)} hỏng")
sys.exit(1 if HONG else 0)
