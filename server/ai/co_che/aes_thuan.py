"""AES-256-CBC + đệm PKCS#7 viết thuần Python (FIPS-197), để khỏi thêm thư viện ngoài vào máy chủ.

Chỉ dùng để đóng gói dữ liệu cơ chế cỡ vài chục KB (đóng gói một lần, chậm vài phần mười giây cũng được). Đúng chuẩn nên bên
Unity giải được bằng System.Security.Cryptography.Aes (CBC, PKCS7) trên mọi nền tảng, kể cả IL2CPP/iOS.
Kiểm bằng véc-tơ thử của FIPS-197 phụ lục C.3 và NIST SP 800-38A F.2.5 (tests/test_co_che_dong_goi.py).
"""
from __future__ import annotations

_SBOX = bytearray(256)
_INV = bytearray(256)


def _tao_sbox() -> None:
    p = q = 1
    while True:                                   # duyệt nhóm nhân GF(2^8) bằng phần tử sinh 3
        p = p ^ ((p << 1) & 0xFF) ^ (0x1B if p & 0x80 else 0)
        q ^= q << 1
        q ^= q << 2
        q ^= q << 4
        q &= 0xFF
        if q & 0x80:
            q ^= 0x09
        x = q ^ ((q << 1) | (q >> 7)) & 0xFF ^ ((q << 2) | (q >> 6)) & 0xFF ^ ((q << 3) | (q >> 5)) & 0xFF \
            ^ ((q << 4) | (q >> 4)) & 0xFF ^ 0x63
        _SBOX[p] = x & 0xFF
        if p == 1:
            break
    _SBOX[0] = 0x63
    for i, v in enumerate(_SBOX):
        _INV[v] = i


_tao_sbox()


def _xt(a: int) -> int:
    return ((a << 1) ^ 0x1B) & 0xFF if a & 0x80 else a << 1


def _nhan(a: int, b: int) -> int:
    r = 0
    while b:
        if b & 1:
            r ^= a
        a = _xt(a)
        b >>= 1
    return r


_M2 = bytes(_nhan(i, 2) for i in range(256))
_M3 = bytes(_nhan(i, 3) for i in range(256))
_M9 = bytes(_nhan(i, 9) for i in range(256))
_M11 = bytes(_nhan(i, 11) for i in range(256))
_M13 = bytes(_nhan(i, 13) for i in range(256))
_M14 = bytes(_nhan(i, 14) for i in range(256))


def _mo_rong_khoa(khoa: bytes) -> list[bytes]:
    if len(khoa) != 32:
        raise ValueError("Khóa AES-256 phải dài 32 byte")
    w = [list(khoa[i:i + 4]) for i in range(0, 32, 4)]
    rcon = 1
    for i in range(8, 60):
        t = list(w[i - 1])
        if i % 8 == 0:
            t = [_SBOX[b] for b in t[1:] + t[:1]]
            t[0] ^= rcon
            rcon = _xt(rcon)
        elif i % 8 == 4:
            t = [_SBOX[b] for b in t]
        w.append([a ^ b for a, b in zip(w[i - 8], t)])
    return [bytes(sum(w[4 * r:4 * r + 4], [])) for r in range(15)]


def _ma_khoi(k: list[bytes], b: bytes) -> bytes:
    s = bytearray(x ^ y for x, y in zip(b, k[0]))
    for r in range(1, 15):
        s = bytearray(_SBOX[x] for x in s)
        s = bytearray(s[(i + 4 * (i % 4)) % 16] for i in range(16))          # ShiftRows (cột-chính)
        if r != 14:
            t = bytearray(16)
            for c in range(4):
                a0, a1, a2, a3 = s[4 * c:4 * c + 4]
                t[4 * c] = _M2[a0] ^ _M3[a1] ^ a2 ^ a3
                t[4 * c + 1] = a0 ^ _M2[a1] ^ _M3[a2] ^ a3
                t[4 * c + 2] = a0 ^ a1 ^ _M2[a2] ^ _M3[a3]
                t[4 * c + 3] = _M3[a0] ^ a1 ^ a2 ^ _M2[a3]
            s = t
        s = bytearray(x ^ y for x, y in zip(s, k[r]))
    return bytes(s)


def _giai_khoi(k: list[bytes], b: bytes) -> bytes:
    s = bytearray(x ^ y for x, y in zip(b, k[14]))
    for r in range(13, -1, -1):
        s = bytearray(s[(i - 4 * (i % 4)) % 16] for i in range(16))          # InvShiftRows
        s = bytearray(_INV[x] for x in s)
        s = bytearray(x ^ y for x, y in zip(s, k[r]))
        if r:
            t = bytearray(16)
            for c in range(4):
                a0, a1, a2, a3 = s[4 * c:4 * c + 4]
                t[4 * c] = _M14[a0] ^ _M11[a1] ^ _M13[a2] ^ _M9[a3]
                t[4 * c + 1] = _M9[a0] ^ _M14[a1] ^ _M11[a2] ^ _M13[a3]
                t[4 * c + 2] = _M13[a0] ^ _M9[a1] ^ _M14[a2] ^ _M11[a3]
                t[4 * c + 3] = _M11[a0] ^ _M13[a1] ^ _M9[a2] ^ _M14[a3]
            s = t
    return bytes(s)


def ma_khoi(khoa: bytes, khoi: bytes) -> bytes:
    return _ma_khoi(_mo_rong_khoa(khoa), khoi)


def ma_cbc(khoa: bytes, iv: bytes, du_lieu: bytes) -> bytes:
    if len(iv) != 16:
        raise ValueError("IV phải dài 16 byte")
    k = _mo_rong_khoa(khoa)
    n = 16 - len(du_lieu) % 16
    du_lieu = du_lieu + bytes([n]) * n                                        # PKCS#7
    ra, truoc = bytearray(), iv
    for i in range(0, len(du_lieu), 16):
        truoc = _ma_khoi(k, bytes(x ^ y for x, y in zip(du_lieu[i:i + 16], truoc)))
        ra += truoc
    return bytes(ra)


def giai_cbc(khoa: bytes, iv: bytes, ma: bytes) -> bytes:
    if len(iv) != 16 or not ma or len(ma) % 16:
        raise ValueError("Bản mã AES-CBC sai độ dài")
    k = _mo_rong_khoa(khoa)
    ra, truoc = bytearray(), iv
    for i in range(0, len(ma), 16):
        khoi = ma[i:i + 16]
        ra += bytes(x ^ y for x, y in zip(_giai_khoi(k, khoi), truoc))
        truoc = khoi
    n = ra[-1]
    if not 1 <= n <= 16 or ra[-n:] != bytes([n]) * n:
        raise ValueError("Đệm PKCS#7 sai")
    return bytes(ra[:-n])
