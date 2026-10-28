"""Sao lưu / khôi phục dữ liệu vào một repo GitHub RIÊNG TƯ – cho máy chủ không có ổ đĩa lưu lâu dài
(Render Free: thư mục dữ liệu bị xoá mỗi lần web ngủ, khởi động lại hoặc cập nhật code).

Bật bằng biến môi trường (trống = tắt, trên Mac không cần):
- SAO_LUU_GITHUB_REPO: "tài-khoản/tên-repo" (repo private, đã có ít nhất 1 commit, vd README)
- SAO_LUU_GITHUB_TOKEN: token fine-grained CHỈ cho repo đó, quyền Contents: Read and write (không in / ghi log)
- SAO_LUU_PHUT: mấy phút sao lưu một lần (mặc định 5; chỉ gửi khi dữ liệu thay đổi)

Nội dung: data/app.db (ảnh chụp nhất quán bằng sqlite3 backup) + data/ai_rieng/ → MỘT file sao-luu.tar.gz, ghi thành một commit
KHÔNG cha rồi đẩy đè nhánh main (repo không phình to theo thời gian). Khoá chính KHÔNG nằm trong bản sao lưu: đặt APP_SECRET_KEY
ở biến môi trường. Ảnh kết quả từng bài (outputs/) không sao lưu – lời giải vẫn còn trong CSDL.
"""
from __future__ import annotations

import base64
import hashlib
import io
import logging
import os
import sqlite3
import tarfile
import tempfile
import threading
import time
from pathlib import Path

import requests

from .config import DATA_DIR

log = logging.getLogger(__name__)

TEN_TEP = "sao-luu.tar.gz"
API = "https://api.github.com"
_THU_MUC_PHU = ("ai_rieng",)          # thư mục con của data/ cần giữ (tài liệu AI Riêng)
_dung = threading.Event()
_khoa = threading.Lock()
_ban_cuoi = {"bam": ""}               # băm bản đã gửi gần nhất → không gửi lại khi dữ liệu không đổi


def _cau_hinh() -> tuple[str, str] | None:
    repo, token = os.environ.get("SAO_LUU_GITHUB_REPO", "").strip(), os.environ.get("SAO_LUU_GITHUB_TOKEN", "").strip()
    return (repo, token) if repo and token and repo.count("/") == 1 else None


def bat() -> bool:
    if os.environ.get("CHE_DO_APP") in {"1", "true", "True"}:
        return False
    return _cau_hinh() is not None


def _dau(token: str, raw: bool = False) -> dict:
    return {"Authorization": f"Bearer {token}", "X-GitHub-Api-Version": "2022-11-28",
            "Accept": "application/vnd.github.raw" if raw else "application/vnd.github+json"}


def _goi_tin(thu_muc: Path = DATA_DIR) -> bytes | None:
    """Đóng gói CSDL (ảnh chụp nhất quán) + thư mục phụ thành tar.gz (thời gian cố định = 0 → cùng dữ liệu cho cùng kết quả)."""
    db = thu_muc / "app.db"
    if not db.is_file():
        return None
    with tempfile.TemporaryDirectory() as tam:
        ban = Path(tam) / "app.db"
        nguon, dich = sqlite3.connect(db), sqlite3.connect(ban)
        try:
            nguon.backup(dich)
        finally:
            dich.close()
            nguon.close()
        dem = io.BytesIO()
        with tarfile.open(fileobj=dem, mode="w:gz", compresslevel=9) as tar:
            def them(duong: Path, ten: str) -> None:
                ti = tar.gettarinfo(str(duong), arcname=ten)
                ti.mtime, ti.uid, ti.gid, ti.uname, ti.gname = 0, 0, 0, "", ""
                if ti.isfile():
                    with open(duong, "rb") as f:
                        tar.addfile(ti, f)
                else:
                    tar.addfile(ti)
            them(ban, "app.db")
            for con in _THU_MUC_PHU:
                goc = thu_muc / con
                if goc.is_dir():
                    for p in sorted(goc.rglob("*")):
                        if p.is_file() or p.is_dir():
                            them(p, f"{con}/{p.relative_to(goc).as_posix()}")
        # gzip ghi thời gian vào đầu file → đặt 0 để băm ổn định
        du_lieu = bytearray(dem.getvalue())
        du_lieu[4:8] = b"\0\0\0\0"
        return bytes(du_lieu)


def sao_luu_ngay(ly_do: str = "dinh_ky") -> bool:
    """Gửi bản sao lưu nếu dữ liệu đã đổi. Lỗi mạng / GitHub → ghi log, trả False (lần sau thử lại)."""
    ch = _cau_hinh()
    if not ch:
        return False
    repo, token = ch
    with _khoa:
        goi = _goi_tin()
        if goi is None:
            return False
        bam = hashlib.sha256(goi).hexdigest()
        if bam == _ban_cuoi["bam"]:
            return True
        try:
            s = requests.Session()
            s.headers.update(_dau(token))
            blob = s.post(f"{API}/repos/{repo}/git/blobs", json={"content": base64.b64encode(goi).decode(), "encoding": "base64"}, timeout=60)
            blob.raise_for_status()
            cay = s.post(f"{API}/repos/{repo}/git/trees",
                         json={"tree": [{"path": TEN_TEP, "mode": "100644", "type": "blob", "sha": blob.json()["sha"]}]}, timeout=30)
            cay.raise_for_status()
            cm = s.post(f"{API}/repos/{repo}/git/commits", json={"message": f"Sao lưu {time.strftime('%Y-%m-%d %H:%M:%S')} ({ly_do})",
                                                                  "tree": cay.json()["sha"], "parents": []}, timeout=30)
            cm.raise_for_status()
            r = s.patch(f"{API}/repos/{repo}/git/refs/heads/main", json={"sha": cm.json()["sha"], "force": True}, timeout=30)
            if r.status_code in (404, 422):   # repo chưa có nhánh main
                r = s.post(f"{API}/repos/{repo}/git/refs", json={"ref": "refs/heads/main", "sha": cm.json()["sha"]}, timeout=30)
            r.raise_for_status()
        except (requests.RequestException, KeyError, ValueError) as e:
            code = getattr(getattr(e, "response", None), "status_code", "")
            log.warning("Sao lưu GitHub thất bại (%s %s) – sẽ thử lại", type(e).__name__, code)
            return False
        _ban_cuoi["bam"] = bam
        log.info("Đã sao lưu dữ liệu lên GitHub (%d KB, %s)", len(goi) // 1024, ly_do)
        return True


def khoi_phuc_neu_can(thu_muc: Path = DATA_DIR) -> bool:
    """Lúc khởi động: chưa có data/app.db (ổ đĩa mới / web vừa thức dậy) → tải bản sao lưu về. Không có bản nào → bắt đầu mới."""
    ch = _cau_hinh()
    if not ch or (thu_muc / "app.db").exists():
        return False
    repo, token = ch
    try:
        r = requests.get(f"{API}/repos/{repo}/contents/{TEN_TEP}", headers=_dau(token, raw=True), timeout=60)
        if r.status_code == 404:
            log.info("Chưa có bản sao lưu trên GitHub – bắt đầu dữ liệu mới")
            return False
        r.raise_for_status()
    except requests.RequestException as e:
        log.error("Không tải được bản sao lưu từ GitHub (%s) – bắt đầu dữ liệu mới", type(e).__name__)
        return False
    thu_muc.mkdir(parents=True, exist_ok=True)
    with tarfile.open(fileobj=io.BytesIO(r.content), mode="r:gz") as tar:
        hop_le = [m for m in tar.getmembers() if m.name == "app.db" or m.name.split("/", 1)[0] in _THU_MUC_PHU]
        tar.extractall(thu_muc, members=hop_le, filter="data")   # filter="data": chặn đường dẫn tuyệt đối / "..", liên kết lạ
    _ban_cuoi["bam"] = hashlib.sha256(r.content).hexdigest()
    log.info("Đã khôi phục dữ liệu từ bản sao lưu GitHub (%d KB)", len(r.content) // 1024)
    return True


def bat_dau() -> None:
    """Luồng nền: sao lưu mỗi SAO_LUU_PHUT phút (chỉ khi dữ liệu đổi)."""
    if not bat():
        return
    try:
        phut = max(1.0, float(os.environ.get("SAO_LUU_PHUT", "5")))
    except ValueError:
        phut = 5.0

    def vong() -> None:
        while not _dung.wait(phut * 60):
            sao_luu_ngay()

    threading.Thread(target=vong, name="sao-luu-github", daemon=True).start()
    log.info("Bật sao lưu dữ liệu lên GitHub mỗi %g phút", phut)


def dung() -> None:
    """Khi app tắt (web đi ngủ / cập nhật): dừng luồng nền và gửi bản cuối."""
    if not bat():
        return
    _dung.set()
    sao_luu_ngay("tat_app")
