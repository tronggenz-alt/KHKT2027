"""Chép dữ liệu kiến thức sinh học 3D (phong_sinh/tu_app_cu/kien_thuc/*.json) sang Unity StreamingAssets (Phong thi nghiem ao/Assets/StreamingAssets/data/sinh/kien_thuc/).

Chạy:
    python tools/chep_kien_thuc_sinh.py
"""

from __future__ import annotations

import hashlib
import shutil
from pathlib import Path

GOC = Path(__file__).resolve().parents[1]
NGUON = GOC / "phong_sinh" / "tu_app_cu" / "kien_thuc"
DICH = GOC / "Phong thi nghiem ao" / "Assets" / "StreamingAssets" / "data" / "sinh" / "kien_thuc"


def sinh_guid(khoa: str) -> str:
    """Tạo GUID MD5 xác định từ chuỗi khóa để giữ ổn định qua các lần chạy."""
    return hashlib.md5(khoa.encode("utf-8")).hexdigest()


def tao_meta_thu_muc(duong_dan_thu_muc: Path) -> None:
    meta_path = duong_dan_thu_muc.with_suffix(duong_dan_thu_muc.suffix + ".meta")
    if not meta_path.exists():
        guid = sinh_guid(f"dir:{duong_dan_thu_muc.name}")
        noi_dung = (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
        meta_path.write_text(noi_dung, encoding="utf-8")


def tao_meta_tep(duong_dan_tep: Path) -> None:
    meta_path = duong_dan_tep.with_suffix(duong_dan_tep.suffix + ".meta")
    if not meta_path.exists():
        guid = sinh_guid(f"file:{duong_dan_tep.name}")
        noi_dung = (
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
        meta_path.write_text(noi_dung, encoding="utf-8")


def chep() -> list[Path]:
    DICH.mkdir(parents=True, exist_ok=True)
    tao_meta_thu_muc(DICH)

    da_chep = []
    for f in sorted(NGUON.glob("*.json")):
        d = DICH / f.name
        shutil.copyfile(f, d)
        tao_meta_tep(d)
        da_chep.append(d)
    return da_chep


if __name__ == "__main__":
    for p in chep():
        print("→", p.relative_to(GOC))
