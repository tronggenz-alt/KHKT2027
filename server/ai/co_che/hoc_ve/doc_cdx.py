"""Đọc hình ChemDraw (CDX nhị phân) nhúng trong file Word đáp án → nguyên tử, liên kết, nhãn, mũi tên kèm toạ độ gốc.

Định dạng CDX: phần đầu 28 byte ("VjCD0100" ...), sau đó là cây đối tượng. Mỗi mục bắt đầu bằng thẻ uint16:
  - thẻ có bit 0x8000 → đối tượng: uint32 id, rồi các mục con, kết thúc bằng thẻ 0x0000;
  - còn lại → thuộc tính: uint16 độ dài (0xFFFF thì theo sau là uint32 độ dài thật), rồi dữ liệu.
Toạ độ là int32 đơn vị 1/65536 point, thứ tự (y, x), trục y hướng xuống.
"""
from __future__ import annotations

import io
import struct
import zipfile

try:  # chỉ cần khi admin đọc đáp án Word (.doc); không có thì bỏ qua
    import olefile
except ImportError:  # pragma: no cover
    olefile = None

DOI_TUONG = {0x8000: "tai_lieu", 0x8001: "trang", 0x8002: "nhom", 0x8003: "manh", 0x8004: "nut", 0x8005: "lien_ket",
             0x8006: "chu", 0x8007: "hinh", 0x8008: "duong_cong", 0x800E: "buoc_pu", 0x8021: "mui_ten"}
T_VI_TRI, T_LOAI_NUT, T_NGUYEN_TO, T_SO_H, T_DIEN_TICH = 0x0200, 0x0400, 0x0402, 0x042B, 0x0421
T_BAC, T_KIEU_NET, T_DAU, T_CUOI, T_CHU = 0x0600, 0x0601, 0x0604, 0x0605, 0x0700
T_KIEU_HINH, T_KIEU_MUI_TEN, T_HOP = 0x0A00, 0x0A02, 0x0204
T_DAU3D, T_DUOI3D, T_TAM3D = 0x0211, 0x0212, 0x0213          # mũi tên (0x8021): đầu, đuôi, tâm (x, y, z)
T_DAU_MUI, T_DUOI_MUI, T_GOC_CUNG = 0x0A20, 0x0A21, 0x0A25    # kiểu đầu (1 đầy đủ, 2 nửa trái, 3 nửa phải), góc cung


def _chuoi(b: bytes) -> str:
    """CDXString: uint16 số đoạn kiểu chữ, mỗi đoạn 10 byte, phần còn lại là chữ."""
    if len(b) < 2:
        return ""
    n = struct.unpack_from("<H", b)[0]
    return b[2 + 10 * n:].decode("latin-1", "replace").replace("\r", " ").strip()


def doc_cay(b: bytes):
    """CDX nhị phân → cây {loai, id, tt: {thẻ: bytes}, con: [...]}."""
    if not b.startswith(b"VjCD0100"):
        return None
    i, goc = 28, {"loai": "goc", "id": 0, "tt": {}, "con": []}
    ngan = [goc]
    while i + 2 <= len(b):
        the = struct.unpack_from("<H", b, i)[0]; i += 2
        if the == 0:
            if len(ngan) > 1:
                ngan.pop()
            continue
        if the & 0x8000:
            if i + 4 > len(b):
                break
            nid = struct.unpack_from("<I", b, i)[0]; i += 4
            o = {"loai": DOI_TUONG.get(the, hex(the)), "id": nid, "tt": {}, "con": []}
            ngan[-1]["con"].append(o); ngan.append(o)
        else:
            if i + 2 > len(b):
                break
            n = struct.unpack_from("<H", b, i)[0]; i += 2
            if n == 0xFFFF:
                n = struct.unpack_from("<I", b, i)[0]; i += 4
            ngan[-1]["tt"][the] = b[i:i + n]; i += n
    return goc


def _vi_tri(tt):
    v = tt.get(T_VI_TRI)
    if v and len(v) >= 8:
        y, x = struct.unpack_from("<ii", v)
        return x / 65536, -y / 65536            # point, trục y hướng lên
    return None


def _nhan(o) -> str:
    for c in o["con"]:
        if c["loai"] == "chu" and T_CHU in c["tt"]:
            return _chuoi(c["tt"][T_CHU])
    return ""


def cac_manh(cay) -> list[dict]:
    """Các mảnh phân tử cấp trên cùng (bỏ mảnh lồng trong nút viết tắt như CO2Me)."""
    ra = []

    def duyet(o, trong_nut=False):
        for c in o["con"]:
            if c["loai"] == "manh" and not trong_nut:
                ra.append(c)
                for n in c["con"]:
                    if n["loai"] == "nut":
                        duyet(n, True)
            elif c["loai"] in ("trang", "nhom", "tai_lieu", "goc"):
                duyet(c, trong_nut)
    duyet(cay)
    mol = []
    for m in ra:
        nut, lk = {}, []
        for c in m["con"]:
            if c["loai"] == "nut":
                p = _vi_tri(c["tt"])
                if p is None:
                    continue
                loai = struct.unpack_from("<H", c["tt"][T_LOAI_NUT])[0] if T_LOAI_NUT in c["tt"] else 1
                z = struct.unpack_from("<H", c["tt"][T_NGUYEN_TO])[0] if T_NGUYEN_TO in c["tt"] else 6
                q = struct.unpack_from("<b", c["tt"][T_DIEN_TICH])[0] if T_DIEN_TICH in c["tt"] else 0
                nut[c["id"]] = {"x": p[0], "y": p[1], "z": z, "loai": loai, "nhan": _nhan(c), "dien_tich": q}
            elif c["loai"] == "lien_ket" and T_DAU in c["tt"] and T_CUOI in c["tt"]:
                bac = struct.unpack_from("<H", c["tt"][T_BAC])[0] if T_BAC in c["tt"] else 1
                net = struct.unpack_from("<H", c["tt"][T_KIEU_NET])[0] if T_KIEU_NET in c["tt"] else 0
                lk.append({"a": struct.unpack_from("<I", c["tt"][T_DAU])[0], "b": struct.unpack_from("<I", c["tt"][T_CUOI])[0],
                           "bac": bac, "net": net})
        lk = [x for x in lk if x["a"] in nut and x["b"] in nut]
        if nut:
            mol.append({"nut": nut, "lk": lk})
    return mol


def cac_mui_ten(cay) -> list[dict]:
    """Mũi tên (đối tượng 0x8021 của ChemDraw mới): đầu, đuôi, tâm cung, kiểu đầu (đầy đủ / nửa)."""
    ra = []

    def duyet(o):
        for c in o["con"]:
            if c["loai"] == "mui_ten":
                tt = c["tt"]
                def d3(t):
                    v = tt.get(t)
                    if v and len(v) >= 12:
                        x, y, _ = struct.unpack_from("<iii", v)
                        return x / 65536, -y / 65536
                    return None
                dau_kieu = struct.unpack_from("<H", tt[T_DAU_MUI])[0] if T_DAU_MUI in tt else None
                goc = struct.unpack_from("<i", tt[T_GOC_CUNG])[0] / 65536 if T_GOC_CUNG in tt and len(tt[T_GOC_CUNG]) >= 4 else 0
                ra.append({"dau": d3(T_DAU3D), "duoi": d3(T_DUOI3D), "tam": d3(T_TAM3D), "kieu_dau": dau_kieu, "goc_cung": goc,
                           "cong": T_DUOI_MUI in tt})   # có thẻ cỡ cung (0x0A21) → mũi tên cong (cơ chế)
            duyet(c)
    duyet(cay)
    return ra


def chemdraw_trong_doc(duong_dan) -> list[tuple[str, bytes]]:
    """File Word đời cũ (.doc): hình nhúng nằm trong ObjectPool/<id>/CONTENTS của tệp OLE."""
    ra = []
    try:
        ole = olefile.OleFileIO(str(duong_dan))
    except Exception:  # noqa: BLE001
        return ra
    for st in ole.listdir(streams=True):
        if st and st[-1] == "CONTENTS":
            try:
                c = ole.openstream(st).read()
            except Exception:  # noqa: BLE001
                continue
            if c.startswith(b"VjCD0100"):
                ra.append(("/".join(st), c))
    return ra


def chemdraw_trong_file(duong_dan) -> list[tuple[str, bytes]]:
    s = str(duong_dan).lower()
    if s.endswith(".docx"):
        return chemdraw_trong_docx(duong_dan)
    if s.endswith(".doc"):
        return chemdraw_trong_doc(duong_dan)
    return []


def chemdraw_trong_docx(duong_dan) -> list[tuple[str, bytes]]:
    """(tên OLE, CDX) của mọi hình ChemDraw nhúng trong một file .docx."""
    ra = []
    try:
        z = zipfile.ZipFile(duong_dan)
    except Exception:  # noqa: BLE001
        return ra
    for n in z.namelist():
        if not n.startswith("word/embeddings/"):
            continue
        try:
            b = z.read(n)
        except Exception:  # noqa: BLE001
            continue
        if not olefile.isOleFile(io.BytesIO(b)):
            continue
        try:
            ole = olefile.OleFileIO(io.BytesIO(b))
            if ole.exists("CONTENTS"):
                c = ole.openstream("CONTENTS").read()
                if c.startswith(b"VjCD0100"):
                    ra.append((n, c))
        except Exception:  # noqa: BLE001
            continue
    return ra
