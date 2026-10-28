#!/usr/bin/env python3
"""tools/phat_hanh_app.py – tạo config/phien_ban_app.json cho cơ chế tự cập nhật của app CHEMISTRY.

Quy trình phát hành:
  1. Tăng "version" trong desktop/package.json (vd 1.0.1) rồi đóng gói: ./desktop/build-mac.sh   (ra ~/AppBuild/CHEMISTRY/dist/CHEMISTRY-mac.dmg)
  2. Tải file .dmg lên nơi công khai thuộc danh sách cho phép (GitHub Releases của repo công khai, hoặc giai-chuoi-huu-co.onrender.com).
  3. Chạy:
       .venv/bin/python tools/phat_hanh_app.py --phien-ban 1.0.1 \\
           --mac-arm64 ~/AppBuild/CHEMISTRY/dist/CHEMISTRY-mac.dmg --url-mac-arm64 https://github.com/<tài-khoản>/<repo>/releases/download/v1.0.1/CHEMISTRY-mac.dmg \\
           --ghi-chu "Sửa lỗi phòng thí nghiệm" --ghi-chu "Thêm chi tiết phòng thật"
     (Windows: thêm --win-x64 <file .exe> --url-win-x64 <url>.  Bắt buộc mọi người phải cập nhật từ bản cũ hơn X: --bat-buoc-tu X)
  4. Commit config/phien_ban_app.json rồi đưa web lên (./dua-len-web.command). App đang chạy sẽ thấy bản mới trong vòng 6 giờ
     hoặc ngay khi người dùng chọn Trợ giúp ▸ Kiểm tra cập nhật.
Tự tính SHA-256 + kích thước từ file thật; dữ liệu đi qua cùng bộ kiểm tra mà backend dùng nên sai là báo lỗi ngay, không ghi file.
"""
import argparse
import hashlib
import json
import sys
from datetime import date
from pathlib import Path

GOC = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(GOC))
from backend.cap_nhat_app import TEP_PHAT_HANH, kiem_tra_ban_phat_hanh  # noqa: E402


def sha256_tep(duong_dan: Path) -> str:
    h = hashlib.sha256()
    with duong_dan.open("rb") as f:
        for khoi in iter(lambda: f.read(1 << 20), b""):
            h.update(khoi)
    return h.hexdigest()


def tao_ban_phat_hanh(phien_ban: str, tep_va_url: dict[str, tuple[Path, str]], ghi_chu: list[str], bat_buoc_tu: str | None, ngay: str | None = None) -> dict:
    tai_ve = {}
    for nen, (tep, url) in tep_va_url.items():
        if not tep.is_file():
            raise SystemExit(f"Không thấy file {tep}")
        tai_ve[nen] = {"url": url, "sha256": sha256_tep(tep), "kich_thuoc": tep.stat().st_size}
    d = {"phien_ban": phien_ban, "ngay": ngay or date.today().isoformat(), "ghi_chu": ghi_chu, "bat_buoc_tu": bat_buoc_tu, "tai_ve": tai_ve}
    ban, loi = kiem_tra_ban_phat_hanh(d)
    if ban is None:
        raise SystemExit(f"Dữ liệu phát hành sai: {loi}")
    return ban


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--phien-ban", required=True)
    ap.add_argument("--ghi-chu", action="append", default=[])
    ap.add_argument("--bat-buoc-tu", default=None)
    ap.add_argument("--ra", default=str(TEP_PHAT_HANH), help="file kết quả (mặc định config/phien_ban_app.json)")
    for nen in ("mac-arm64", "mac-x64", "win-x64"):
        ap.add_argument(f"--{nen}", help=f"đường dẫn file cài cho {nen}")
        ap.add_argument(f"--url-{nen}", help=f"địa chỉ https công khai của file cài {nen}")
    a = ap.parse_args(argv)
    tep_va_url = {}
    for nen in ("mac-arm64", "mac-x64", "win-x64"):
        tep, url = getattr(a, nen.replace("-", "_")), getattr(a, "url_" + nen.replace("-", "_"))
        if bool(tep) != bool(url):
            raise SystemExit(f"--{nen} và --url-{nen} phải đi cùng nhau")
        if tep:
            tep_va_url[nen] = (Path(tep).expanduser(), url)
    ban = tao_ban_phat_hanh(a.phien_ban, tep_va_url, a.ghi_chu, a.bat_buoc_tu)
    Path(a.ra).write_text(json.dumps(ban, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"✓ Đã ghi {a.ra}: bản {ban['phien_ban']}, {len(ban['tai_ve'])} bộ cài")
    for nen, t in ban["tai_ve"].items():
        print(f"   {nen}: {t['kich_thuoc']:,} byte, sha256 {t['sha256'][:16]}…")
    print("Tiếp theo: commit file này rồi đưa web lên thì app mới thấy bản mới.")


if __name__ == "__main__":
    main()
