"""Mã hoá cookie NotebookLM bằng AES-256-GCM trước khi lưu CSDL.

Khoá: biến môi trường NOTEBOOKLM_COOKIE_KEY (chuỗi ngẫu nhiên ≥ 32 ký tự, hoặc base64 của đúng 32 byte) – KHÔNG viết cứng trong code.
Bản mã mới có tiền tố "gcm1:"; bản cũ (Fernet suy từ khoá chính của app) vẫn giải mã được → lần lưu sau tự chuyển sang AES-GCM.
"""
from __future__ import annotations

import base64
import binascii
import os

from cryptography.exceptions import InvalidTag
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives.kdf.hkdf import HKDF

from backend.bao_mat import giai_ma_cookie_notebooklm as _giai_ma_fernet_cu
from backend.config import settings

from .client import LoiNotebookLM

TIEN_TO = "gcm1:"
_AAD = b"notebooklm-cookie"


def _khoa() -> bytes:
    k = (settings.notebooklm_cookie_key or "").strip()
    if len(k) < 32:
        raise LoiNotebookLM("Máy chủ chưa đặt khoá mã hoá cookie NOTEBOOKLM_COOKIE_KEY (chuỗi ngẫu nhiên ≥ 32 ký tự) – "
                            "chạy cau-hinh-notebooklm.command (máy Mac) hoặc thêm biến này ở mục Environment (Render).",
                            code="ENCRYPTION_KEY_MISSING", status_code=503)
    try:
        tho = base64.b64decode(k + "=" * (-len(k) % 4), altchars=b"-_", validate=True)
        if len(tho) == 32:
            return tho
    except (binascii.Error, ValueError):
        pass
    return HKDF(algorithm=hashes.SHA256(), length=32, salt=b"notebooklm-cookie-key", info=b"aes-256-gcm v1").derive(k.encode("utf-8"))


def ma_hoa(cookie: str) -> str:
    nonce = os.urandom(12)
    ban_ma = AESGCM(_khoa()).encrypt(nonce, cookie.encode("utf-8"), _AAD)
    return TIEN_TO + base64.urlsafe_b64encode(nonce + ban_ma).decode("ascii")


def giai_ma(ban_ma: str) -> str:
    if not (ban_ma or "").startswith(TIEN_TO):
        return _giai_ma_fernet_cu(ban_ma)                     # dữ liệu cũ (Fernet) – vẫn đọc được
    try:
        tho = base64.urlsafe_b64decode(ban_ma[len(TIEN_TO):].encode("ascii"))
        return AESGCM(_khoa()).decrypt(tho[:12], tho[12:], _AAD).decode("utf-8")
    except (InvalidTag, ValueError, binascii.Error) as e:
        raise LoiNotebookLM("Không giải mã được cookie đã lưu (khoá NOTEBOOKLM_COOKIE_KEY đã đổi?) – hãy bấm Sửa và dán lại cookie.",
                            code="DECRYPT_FAILED", status_code=409) from e
