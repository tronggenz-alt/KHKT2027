"""Gói mã hóa .verapack cho app: bài + kịch bản 3D + sơ đồ giấy của một bài cơ chế.

    python -m server.ai.co_che.dong_goi [ma ...] [--unity]     → build/co_che/goi/<ma>.verapack
        --unity: chép thêm gói vào Assets/StreamingAssets/CoChe/ và khóa ĐÃ CHE vào Assets/Resources/CoChe/khoa.bytes (git bỏ qua)

Cấu trúc (mọi số nguyên big-endian):
    "VERA1" (5 byte) | phiên bản (1 byte = 1) | salt (16) | IV (16) | AES-256-CBC(gzip(JSON), PKCS#7) | HMAC-SHA256 (32)
    khóa mã  = HMAC-SHA256(khóa gốc, "VERA-ma"  || salt)
    khóa ký  = HMAC-SHA256(khóa gốc, "VERA-ky"  || salt)
    HMAC tính trên MỌI byte đứng trước nó (mã hóa trước rồi ký – encrypt-then-MAC); khi mở: so HMAC bằng hàm thời gian hằng
    TRƯỚC khi giải mã, sai một byte là từ chối.
JSON gộp: {"phien_ban": 1, "ma", "bai", "kich_ban_3d", "so_do_giay", "_d": dấu ẩn}. Dấu ẩn = mã máy dựng (băm) + ngày + mã gói
ngẫu nhiên; mỗi lần đóng gói ghi nhật ký vào thư mục khóa riêng để lần ra nguồn nếu gói bị rò.

AES-CBC + HMAC thay cho AES-GCM vì Aes.Create() chạy trên mọi nền tảng Unity/IL2CPP, còn AesGcm có thể không có trên iOS.
Khóa gốc 32 byte sinh ngẫu nhiên MỘT lần, nằm ở <VERA_DU_LIEU_RIENG>/khoa/vera_co_che.key (không vào git).

GIỚI HẠN (nói thật): khóa phải nằm trong app để app tự giải được, nên người quyết tâm dịch ngược app vẫn lấy được khóa và mở gói.
Mã hóa chỉ chặn sao chép thông thường (mở thư mục app, chép file JSON). Bảo vệ thật là: dữ liệu gốc không vào kho công khai,
và "bộ não" (kho mẫu, tư duy vẽ, bộ đo, RAG) chỉ chạy ở máy chủ.
"""
from __future__ import annotations

import gzip
import hashlib
import hmac
import json
import os
import platform
import secrets
import shutil
import sys
import time
import uuid
from pathlib import Path

from . import aes_thuan
from .duong_dan import GOC_KHO, thu_muc_build, thu_muc_rieng

MA_DAU = b"VERA1"
PHIEN_BAN = 1
DO_DAI_DAU = len(MA_DAU) + 1 + 16 + 16
UNITY = GOC_KHO / "Phong thi nghiem ao" / "Assets"


class LoiGoi(ValueError):
    pass


def duong_dan_khoa() -> Path:
    return Path(os.environ.get("VERA_KHOA_CO_CHE") or thu_muc_rieng() / "khoa" / "vera_co_che.key")


def doc_khoa(tao_neu_chua_co: bool = False) -> bytes:
    p = duong_dan_khoa()
    if not p.exists():
        if not tao_neu_chua_co:
            raise LoiGoi(f"Chưa có khóa gói cơ chế: {p}")
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(secrets.token_bytes(32))
        try:
            os.chmod(p, 0o600)
        except OSError:  # Windows: bỏ qua quyền kiểu POSIX
            pass
    k = p.read_bytes()
    if len(k) != 32:
        raise LoiGoi("Khóa gói cơ chế phải dài đúng 32 byte")
    return k


def _khoa_con(goc: bytes, nhan: bytes, salt: bytes) -> bytes:
    return hmac.new(goc, nhan + salt, hashlib.sha256).digest()


def ma_may() -> str:
    """Mã máy dựng (băm, không lộ tên máy)."""
    tho = f"{platform.node()}|{uuid.getnode()}|{platform.system()}"
    return hashlib.sha256(tho.encode()).hexdigest()[:12]


def dong_goi_du_lieu(du_lieu: dict, khoa: bytes, salt: bytes | None = None, iv: bytes | None = None) -> bytes:
    salt = salt or secrets.token_bytes(16)
    iv = iv or secrets.token_bytes(16)
    tho = gzip.compress(json.dumps(du_lieu, ensure_ascii=False, separators=(",", ":")).encode("utf-8"), mtime=0)
    ma = aes_thuan.ma_cbc(_khoa_con(khoa, b"VERA-ma", salt), iv, tho)
    dau = MA_DAU + bytes([PHIEN_BAN]) + salt + iv
    ky = hmac.new(_khoa_con(khoa, b"VERA-ky", salt), dau + ma, hashlib.sha256).digest()
    return dau + ma + ky


def mo_goi(goi: bytes, khoa: bytes) -> dict:
    if len(goi) < DO_DAI_DAU + 16 + 32 or goi[:5] != MA_DAU:
        raise LoiGoi("Không phải gói VERA")
    if goi[5] != PHIEN_BAN:
        raise LoiGoi(f"Phiên bản gói {goi[5]} chưa hỗ trợ")
    salt, iv = goi[6:22], goi[22:38]
    than, ky = goi[:-32], goi[-32:]
    dung = hmac.new(_khoa_con(khoa, b"VERA-ky", salt), than, hashlib.sha256).digest()
    if not hmac.compare_digest(dung, ky):                       # so thời gian hằng, TRƯỚC khi giải mã
        raise LoiGoi("Gói bị sửa hoặc sai khóa (HMAC không khớp)")
    try:
        tho = aes_thuan.giai_cbc(_khoa_con(khoa, b"VERA-ma", salt), iv, goi[DO_DAI_DAU:-32])
        return json.loads(gzip.decompress(tho).decode("utf-8"))
    except (ValueError, OSError) as e:
        raise LoiGoi(f"Không giải được gói: {e}") from e


def gop_bai(ma: str, build: Path | None = None) -> dict:
    o = (build or thu_muc_build()) / ma
    doc = lambda duoi: json.loads((o / f"{ma}.{duoi}.json").read_text(encoding="utf-8"))
    return {"phien_ban": 1, "ma": ma, "bai": doc("bai"), "kich_ban_3d": doc("3d"), "so_do_giay": doc("giay")}


def dong_goi_bai(ma: str, khoa: bytes, build: Path | None = None) -> tuple[bytes, dict]:
    du_lieu = gop_bai(ma, build)
    dau = {"m": ma_may(), "n": time.strftime("%Y-%m-%d"), "id": secrets.token_hex(4)}
    du_lieu["_d"] = dau
    return dong_goi_du_lieu(du_lieu, khoa), dau


def ghi_nhat_ky(ma: str, dau: dict, dich: str) -> None:
    p = duong_dan_khoa().parent / "nhat_ky_dong_goi.jsonl"
    p.parent.mkdir(parents=True, exist_ok=True)
    with p.open("a", encoding="utf-8") as f:
        f.write(json.dumps({"ma": ma, **dau, "dich": dich, "luc": time.strftime("%Y-%m-%d %H:%M:%S")}, ensure_ascii=False) + "\n")


# Che khóa trong app (C3): khoa.bytes = khóa gốc XOR SHA256("VERA·" + A + "·" + B + "·" + C); ba hằng A, B, C nằm rải ở ba lớp
# C# (GoiCoChe, TrinhPhat3D, CoCheMoDun). Chỉ làm khó thêm việc lấy khóa từ app, KHÔNG phải bảo mật thật.
CHE_A, CHE_B, CHE_C = "lều-propellane-1.1.1", "electron-đi-trước-35%", "VERA-Lab-18/11"


def mat_na_che() -> bytes:
    return hashlib.sha256(f"VERA·{CHE_A}·{CHE_B}·{CHE_C}".encode("utf-8")).digest()


def che_khoa(khoa: bytes) -> bytes:
    return bytes(a ^ b for a, b in zip(khoa, mat_na_che()))


def main(argv: list[str]) -> int:
    unity = "--unity" in argv
    chon = [a for a in argv if not a.startswith("--")]
    khoa = doc_khoa(tao_neu_chua_co=True)
    ml = json.loads((thu_muc_build() / "muc_luc.json").read_text(encoding="utf-8"))
    ma_ds = chon or [b["ma"] for b in ml["bai"]]
    ra = thu_muc_build() / "goi"
    ra.mkdir(parents=True, exist_ok=True)
    for ma in ma_ds:
        goi, dau = dong_goi_bai(ma, khoa)
        mo_goi(goi, khoa)                                       # tự kiểm: giải lại được
        (ra / f"{ma}.verapack").write_bytes(goi)
        ghi_nhat_ky(ma, dau, "unity" if unity else "build")
        print(f"✓ {ma:<20} {len(goi):>7} byte  dấu {dau['id']}")
    if unity:
        sa = UNITY / "StreamingAssets" / "CoChe"
        rs = UNITY / "Resources" / "CoChe"
        sa.mkdir(parents=True, exist_ok=True)
        rs.mkdir(parents=True, exist_ok=True)
        for ma in ma_ds:
            shutil.copy2(ra / f"{ma}.verapack", sa / f"{ma}.verapack")
        (rs / "khoa.bytes").write_bytes(che_khoa(khoa))
        print(f"→ {sa} ({len(ma_ds)} gói), khóa → {rs / 'khoa.bytes'}")
    print(f"→ {ra}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
