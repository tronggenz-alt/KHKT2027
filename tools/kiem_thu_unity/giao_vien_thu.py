"""Đóng vai giáo viên cho KhktSlideTest: gọi API trình chiếu thật ở cổng 8000.

Mật khẩu đọc từ data/tai_khoan_mau_mat_khau.json lúc chạy, không in ra. Bài giảng thử bị xóa khi xong.
Cách chạy: python tools/kiem_thu_unity/giao_vien_thu.py <thư mục đánh dấu>
"""
from __future__ import annotations

import io
import json
import sys
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path

from PIL import Image, ImageDraw

GOC = Path(__file__).resolve().parents[2]
MAY = "http://127.0.0.1:8000"
DIR = Path(sys.argv[1])
ma_phien = ""


def goi(phuong_thuc: str, duong_dan: str, than: bytes | None = None, kieu: str = "application/json"):
    req = urllib.request.Request(MAY + duong_dan, data=than, method=phuong_thuc)
    if than is not None:
        req.add_header("Content-Type", kieu)
    if ma_phien:
        req.add_header("Authorization", "Bearer " + ma_phien)
    with urllib.request.urlopen(req, timeout=20) as r:
        du_lieu = r.read()
    return json.loads(du_lieu) if du_lieu else None


def goi_json(phuong_thuc: str, duong_dan: str, d: dict):
    return goi(phuong_thuc, duong_dan, json.dumps(d).encode())


def anh_trang(so: int, mau: tuple[int, int, int]) -> bytes:
    im = Image.new("RGB", (1600, 900), mau)
    ve = ImageDraw.Draw(im)
    ve.rectangle((60, 60, 1540, 840), outline=(255, 255, 255), width=12)
    # số to vẽ bằng khối, không cần phông chữ
    for i in range(so):
        ve.rectangle((300 + i * 260, 300, 500 + i * 260, 600), fill=(255, 255, 255))
    b = io.BytesIO()
    im.save(b, "JPEG", quality=90)
    return b.getvalue()


def nhieu_phan(truong: dict[str, str], tep: list[tuple[str, bytes]]) -> tuple[bytes, str]:
    ranh = uuid.uuid4().hex
    b = io.BytesIO()
    for k, v in truong.items():
        b.write(f'--{ranh}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    for ten, du in tep:
        b.write(f'--{ranh}\r\nContent-Disposition: form-data; name="tep"; filename="{ten}"\r\nContent-Type: image/jpeg\r\n\r\n'.encode())
        b.write(du + b"\r\n")
    b.write(f"--{ranh}--\r\n".encode())
    return b.getvalue(), f"multipart/form-data; boundary={ranh}"


def doi(ten: str, gioi_han: float = 120) -> str:
    t0 = time.time()
    p = DIR / ten
    while not p.exists():
        if time.time() - t0 > gioi_han:
            raise TimeoutError("quá giờ đợi " + ten)
        time.sleep(0.05)
    time.sleep(0.05)
    return p.read_text(encoding="utf-8").strip()


def danh_dau(ten: str, noi_dung: str) -> None:
    tam = DIR / (ten + ".tmp")
    tam.write_text(noi_dung, encoding="utf-8")
    tam.replace(DIR / ten)


def main() -> None:
    global ma_phien
    mat_khau = json.loads((GOC / "data/tai_khoan_mau_mat_khau.json").read_text(encoding="utf-8"))["mat_khau"]["gv.thu1"]
    dn = goi_json("POST", "/auth/dang-nhap", {"ten_dang_nhap": "gv.thu1", "mat_khau": mat_khau, "kieu": "ung_dung"})
    ma_phien = dn["ma_phien"]
    lop = next(l for l in dn["nguoi_dung"]["lop"] if l["ten"] == "Lớp thử A")["id"]
    than, kieu = nhieu_phan({"ten": "thu.unity trình chiếu"}, [("trang1.jpg", anh_trang(1, (11, 99, 209))), ("trang2.jpg", anh_trang(2, (11, 122, 91)))])
    bai = goi("POST", "/api/bai-giang", than, kieu)
    bai_id = bai["id"]
    print("đã tải bài giảng thử, số trang:", bai.get("so_trang"))
    try:
        doi("app_san_sang", 900)  # Unity dựng lại toàn bộ cảnh trước khi chạy
        buoc = [
            ("s1", {"loai": "slide", "bai_id": bai_id, "trang": 1}),
            ("s2", {"loai": "slide", "bai_id": bai_id, "trang": 2}),
            ("s3", {"loai": "chu", "tieu_de": "Câu hỏi thảo luận", "noi_dung": "Vì sao dung dịch đổi màu?", "dem_nguoc_giay": 120}),
            ("s4", {"loai": "tat"}),
        ]
        for ten, d in buoc:
            danh_dau(ten + "_luc", f"{time.time():.3f}")
            goi_json("POST", f"/api/lop/{lop}/trinh-chieu", d)
            tre = doi(ten + "_xong", 60)
            # app báo đã hiện ở nhịp kế tiếp, nên đợi quá một nhịp mới đọc số đếm
            time.sleep(2.5)
            tt = goi("GET", f"/api/lop/{lop}/trinh-chieu")
            print(f"{ten}: app đo trễ {tre} s; máy chủ: đang mở {tt.get('may_dang_mo')}, đã hiện {tt.get('may_da_hien')}, trễ {tt.get('do_tre_ms')} ms")

        # Giai đoạn 6
        def hang() -> dict:
            d = goi("GET", f"/api/lop/{lop}/truc-tiep")
            for v in d.values():
                if isinstance(v, list):
                    for h in v:
                        if isinstance(h, dict) and "hs.thu1" in json.dumps(h, ensure_ascii=False) or (isinstance(h, dict) and h.get("ho_ten") == "Học sinh Thử Một"):
                            return h
            return {}

        for ten in ("vt1", "vt2"):
            noi = doi(ten, 90)
            time.sleep(0.5)
            print(f"{ten}: app ở {noi}; trực tiếp: {json.dumps(hang(), ensure_ascii=False)[:360]}")
            danh_dau(ten + "_doc", "1")

        danh_dau("s5_luc", f"{time.time():.3f}")
        goi_json("POST", f"/api/lop/{lop}/thong-bao", {"noi_dung": "Chuẩn bị kết thúc bài trong 5 phút"})
        doi("s5_xong", 60)

        goi_json("POST", f"/api/lop/{lop}/chu-y", {"bat": True, "noi_dung": "Cả lớp nhìn lên bảng"})
        danh_dau("s6_luc", f"{time.time():.3f}")
        doi("s6_xong", 60)
        print("s6: lúc chú ý: " + json.dumps(hang(), ensure_ascii=False)[:360])
        goi_json("POST", f"/api/lop/{lop}/chu-y", {"bat": False})
        danh_dau("s7_luc", f"{time.time():.3f}")
        doi("s7_xong", 60)
        time.sleep(2.5)
        print("s7: sau khi mở: " + json.dumps(hang(), ensure_ascii=False)[:360])

        goi_json("POST", f"/api/lop/{lop}/chu-y", {"bat": True, "noi_dung": "Thử mất máy chủ"})
        danh_dau("s8_luc", f"{time.time():.3f}")
        doi("s8_xong", 90)
        goi_json("POST", f"/api/lop/{lop}/chu-y", {"bat": False})
        danh_dau("s9_xong", "1")

        # Giai đoạn 7
        r = goi_json("POST", f"/api/lop/{lop}/buoi-hoc", {"che_do": "kiem_tra", "phut": 10, "khoa_them": [1]})
        danh_dau("s10_luc", f"{time.time():.3f}")
        doi("s10_xong", 60)
        print("s10 kiểm tra: " + json.dumps(r, ensure_ascii=False)[:200])
        r = goi_json("POST", f"/api/lop/{lop}/robot", {"hanh_dong": "bat_tu_hut"})
        danh_dau("s11_luc", f"{time.time():.3f}")
        doi("s11_xong", 60)
        print("s11 robot: " + json.dumps(r, ensure_ascii=False)[:200])
        goi_json("POST", f"/api/lop/{lop}/buoi-hoc", {"che_do": "luyen_tap"})
        danh_dau("s12_luc", f"{time.time():.3f}")
        doi("s12_xong", 60)
    finally:
        try:
            goi_json("POST", f"/api/lop/{lop}/chu-y", {"bat": False})
            goi_json("POST", f"/api/lop/{lop}/trinh-chieu", {"loai": "tat"})
            goi("DELETE", f"/api/bai-giang/{bai_id}")
            goi_json("POST", f"/api/lop/{lop}/buoi-hoc", {"che_do": "luyen_tap"})
            print("đã tắt và xóa bài giảng thử")
        except urllib.error.URLError as e:
            print("không dọn được:", e)


if __name__ == "__main__":
    main()
