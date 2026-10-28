"""Dựng bộ xương – cơ – mạch chi – da THẬT cho Phòng Lab Sinh học 3D v1 (/sinh-hoc) từ BodyParts3D.

Dữ liệu: BodyParts3D 4.0, bộ "is-a" đã giảm 99 % đa giác (isa_BP3D_4.0_obj_99.zip, ~136 MB, KHÔNG để trong repo):
  https://dbarchive.biosciencedbc.jp/data/bodyparts3d/LATEST/isa_BP3D_4.0_obj_99.zip
Giấy phép: "BodyParts3D, © The Database Center for Life Science licensed under CC Attribution 4.0 International"
  (https://dbarchive.biosciencedbc.jp/en/bodyparts3d/lic.html – ghi chú CC BY-SA 2.1 JP cũ trong từng file OBJ đã được thay bằng CC BY 4.0).

Dùng:  .venv/bin/python tools/dung_bp3d.py <thư mục OBJ đã giải nén> [frontend/sinh-hoc/bp3d.bin.gz]

Việc làm:
1. Chọn các phần tử theo tên tiếng Anh trong đầu file OBJ → nhóm bộ phận (id dùng trong trang 3D: xuong_dui, co_delta, …).
2. Đổi hệ toạ độ BodyParts3D (mm; x = trái bệnh nhân, y = ra sau, z = lên) sang hệ của trang (x = trái, y = lên, z = ra trước):
   một phép co giãn ĐỀU K + tịnh tiến, khớp theo bình phương tối thiểu tâm não, tiểu não, thanh quản, khí quản, tim, dạ dày, lách,
   hai thận, tuỵ, tá tràng, bàng quang, trực tràng của mô hình nội tạng sẵn có → nội tạng nằm đúng trong lồng ngực / hộp sọ / chậu.
3. Giảm đa giác (gộp đỉnh theo lưới, mỗi phần tử một kích thước ô sao cho tổng tam giác mỗi lớp ≈ ngân sách theo diện tích).
4. Cơ: toạ độ vân (u quanh, v dọc trục dài của từng cơ – hai đầu v là gân) để vật liệu vân thớ của trang dùng được.
5. Ghi một file nhị phân nén gzip: "BP3D" + phiên bản + độ dài JSON + JSON (nhóm, điểm neo nhãn, khớp chi, giấy phép)
   + đỉnh Uint16 (lượng tử hoá trong hộp bao) + UV Uint16 (cơ) + chỉ số tam giác Uint16/Uint32 (mã hoá hiệu để nén tốt hơn).
"""
from __future__ import annotations

import gzip
import json
import re
import struct
import sys
from pathlib import Path

import numpy as np

# ---------- phép đặt vào hệ toạ độ của trang (xem bước 2) ----------
K = 0.00243
CX, CY, CZ = 0.003, -0.430, -0.234


def doi_toa_do(v: np.ndarray) -> np.ndarray:
    return np.c_[K * v[:, 0] + CX, K * v[:, 2] + CY, -K * v[:, 1] + CZ]


# ---------- nhóm bộ phận: (regex tên tiếng Anh, id, lớp) – luật đầu tiên khớp được dùng ----------
_CHI_TAY = r"(thumb|index finger|middle finger|ring finger|little finger)"
_CHI_CHAN = r"(big toe|second toe|third toe|fourth toe|little toe)"
LUAT: list[tuple[str, str, str]] = [
    # bộ loại trừ: cơ mắt, vòm miệng, thanh quản, cơ hoành (trang đã có), màng, dây chằng nhỏ
    (r"(superior|inferior|lateral|medial) rectus$|(superior|inferior) oblique$|levator palpebrae|check ligament|trochlea of|veli palatini|uvular|arytenoid|cricothyroid|thyrohyoid membrane"
     r"|ligament|interosseous membrane|tendinous arch|^diaphragm$|taenia|tendon of", "", ""),
    # ---- xương ----
    (r"tooth$", "rang", "xuong"),
    (r"^mandible$|^hyoid bone$", "xuong_ham_duoi", "xuong"),
    (r"^(frontal|occipital) bone$|parietal bone$|temporal bone$|^sphenoid bone$|^ethmoid$|zygomatic bone$|nasal bone$|lacrimal bone$"
     r"|palatine bone$|maxilla$|^vomer$|inferior nasal concha$", "hop_so", "xuong"),
    (r"^intervertebral disk", "dia_dem", "xuong"),
    (r"vertebra$|^atlas$|^axis$", "cot_song", "xuong"),
    (r"^sacrum$|coccyx|hip bone$", "xuong_chau", "xuong"),
    (r"costal cartilage$", "sun_suon", "xuong"),
    (r"\brib$", "xuong_suon", "xuong"),
    (r"^body of sternum$|^manubrium$|^xiphoid process$|clavicle$", "xuong_uc_don", "xuong"),
    (r"scapula$", "xuong_vai", "xuong"),
    (r"humerus$", "xuong_canh_tay", "xuong"),
    (r"(left|right) (radius|ulna)$", "xuong_cang_tay", "xuong"),
    (r"(scaphoid|lunate|triquetral|pisiform|trapezium|trapezoid|capitate|hamate)$|metacarpal bone$|phalanx of (left|right) " + _CHI_TAY,
     "xuong_ban_tay", "xuong"),
    (r"femur$", "xuong_dui", "xuong"),
    (r"patella$", "xuong_banh_che", "xuong"),
    (r"(left|right) (tibia|fibula)$", "xuong_cang_chan", "xuong"),
    (r"calcaneus$|talus$|cuboid bone$|navicular bone of|cuneiform bone$|metatarsal bone$|sesamoid bone of|phalanx of (left|right) " + _CHI_CHAN,
     "xuong_ban_chan", "xuong"),
    # ---- cơ ----
    (r"sternocleidomastoid", "co_uc_don_chum", "co"),
    (r"trapezius", "co_thang", "co"),
    (r"deltoid", "co_delta", "co"),
    (r"pectoralis major", "co_nguc_lon", "co"),
    (r"pectoralis minor", "co_nguc_be", "co"),
    (r"serratus anterior", "co_rang_truoc", "co"),
    (r"external oblique", "co_cheo_bung", "co"),
    (r"intercostal muscle|transversus thoracis|levatores costarum", "co_lien_suon", "co"),
    (r"supraspinatus|infraspinatus|teres (major|minor)|subscapularis", "co_chop_xoay", "co"),
    (r"rhomboid|levator scapulae", "co_tram", "co"),
    (r"iliocostalis|longissimus (thoracis|cervicis)|spinalis thoracis|^spinalis$", "co_dung_gai", "co"),
    (r"semispinalis|splenius|serratus posterior|interspinalis|longissimus capitis|rectus capitis|longus colli", "co_lung_sau", "co"),
    (r"platysma|omohyoid|sternohyoid|sternothyroid|thyrohyoid$|digastric|mylohyoid|stylohyoid$|geniohyoid|intermediate tendon", "co_co", "co"),
    (r"psoas|iliacus", "co_that_lung_chau", "co"),
    (r"gluteus maximus", "co_mong_lon", "co"),
    (r"gluteus (medius|minimus)|piriformis|gemellus|obturator (internus|externus)|quadratus femoris|tensor fasciae latae", "co_mong_nho", "co"),
    (r"iliotibial tract", "dai_chau_chay", "gan"),
    (r"rectus femoris|vastus", "co_tu_dau_dui", "co"),
    (r"sartorius", "co_may", "co"),
    (r"adductor (longus|brevis|magnus|minimus)|gracilis|pectineus", "co_khep_dui", "co"),
    (r"biceps femoris|semitendinosus|semimembranosus", "co_dui_sau", "co"),
    (r"gastrocnemius|soleus|plantaris", "co_bap_chan", "co"),
    (r"calcaneal tendon", "gan_achilles", "gan"),
    (r"tibialis anterior", "co_chay_truoc", "co"),
    (r"fibularis|extensor digitorum longus|extensor hallucis longus|tibialis posterior|flexor digitorum longus|flexor hallucis longus|popliteus",
     "co_cang_chan", "co"),
    (r"of (left|right) foot$|extensor hallucis brevis|flexor digitorum brevis|flexor accessorius|flexor hallucis brevis|adductor hallucis",
     "co_ban_chan", "co"),
    (r"biceps brachii", "co_nhi_dau", "co"),
    (r"(left|right) brachialis$|coracobrachialis", "co_canh_tay", "co"),
    (r"triceps brachii|anconeus", "co_tam_dau", "co"),
    (r"of (left|right) hand$|interossei of|lumbricals of|opponens pollicis|flexor pollicis brevis|adductor pollicis", "co_ban_tay", "co"),
    (r"brachioradialis|flexor carpi|extensor carpi|palmaris longus|pronator|supinator|flexor digitorum (superficialis|profundus)"
     r"|flexor pollicis longus|extensor digitorum$|extensor digiti minimi$|extensor indicis|extensor pollicis|flexor retinaculum of",
     "co_cang_tay", "co"),
    # ---- mạch máu chi & cổ (trục chính) ----
    (r"(left|right) (subclavian|axillary|brachial|radial|ulnar|femoral|popliteal|anterior tibial|posterior tibial|dorsalis pedis|common carotid)"
     r" artery$", "dong_mach_ngoai_vi", "mach_do"),
    (r"(left|right) (cephalic|basilic|median cubital|median antebrachial|great saphenous|small saphenous|femoral|popliteal|axillary"
     r"|subclavian|internal jugular) vein$", "tinh_mach_ngoai_vi", "mach_xanh"),
    # ---- da ----
    (r"^skin$", "da", "da"),
]
LUAT_RE = [(re.compile(r, re.I), bp, lop) for r, bp, lop in LUAT]
NGAN_SACH = {"xuong": 190_000, "co": 190_000, "gan": 8_000, "mach_do": 9_000, "mach_xanh": 12_000, "da": 36_000}  # tam giác / lớp
HE_SO_NGAN_SACH = float(__import__("os").environ.get("BP3D_HE_SO", "1"))  # thử nghiệm: >1 giữ nhiều tam giác hơn
# phần tử nằm khuất bên trong (ít thấy) → ít tam giác; răng, xương mặt nhỏ nhiều chi tiết → ưu tiên
TRONG_SO_RIENG = [(re.compile(r"^ethmoid$|^vomer$|nasal concha|palatine bone|intercostal|transversus thoracis|levatores costarum", re.I), 0.3),
                  (re.compile(r"tooth$|maxilla$|zygomatic bone$|nasal bone$|lacrimal bone$|^mandible$|^(frontal|occipital) bone$|parietal bone$", re.I), 2.0)]


def chia_ngan_sach(ds: list[tuple[int, float, float]], tong: float) -> list[int]:
    """Chia `tong` tam giác cho các phần tử (số tam giác gốc, diện tích, trọng số riêng): tỉ lệ diện tích^0,6 × trọng số
    (phần nhỏ giữ chi tiết tương đối nhiều hơn), không vượt số gốc – phần dư rót lại cho các phần tử còn lại."""
    dich = [0.0] * len(ds)
    con = set(range(len(ds)))
    con_lai = tong
    while con:
        w = {i: ds[i][2] * ds[i][1] ** 0.6 for i in con}
        tw = sum(w.values()) or 1
        tran = [i for i in con if con_lai * w[i] / tw >= ds[i][0]]
        if not tran:
            for i in con:
                dich[i] = con_lai * w[i] / tw
            break
        for i in tran:
            dich[i] = ds[i][0]
            con_lai -= ds[i][0]
            con.discard(i)
    return [max(60, int(x)) for x in dich]


def doc_obj(p: Path) -> tuple[str, np.ndarray, np.ndarray]:
    ten, v, f = "", [], []
    with open(p, encoding="utf-8", errors="replace") as fh:
        for dong in fh:
            if dong.startswith("v "):
                v.append(dong.split()[1:4])
            elif dong.startswith("f "):
                f.append([int(x.split("/")[0]) - 1 for x in dong.split()[1:4]])
            elif dong.startswith("# English name"):
                ten = dong.split(":", 1)[1].strip()
    return ten, np.asarray(v, float), np.asarray(f, np.int64)


def dien_tich(v: np.ndarray, f: np.ndarray) -> float:
    return float(np.linalg.norm(np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]]), axis=1).sum() / 2)


_HUONG = np.array([[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]]
                  + [[x, y, z] for x in (1, -1) for y in (1, -1) for z in (1, -1)], float)
_HUONG /= np.linalg.norm(_HUONG, axis=1, keepdims=True)


def phap_tuyen_dinh(v: np.ndarray, f: np.ndarray) -> np.ndarray:
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    nv = np.zeros_like(v)
    for k in range(3):
        np.add.at(nv, f[:, k], n)
    return nv / np.maximum(1e-12, np.linalg.norm(nv, axis=1, keepdims=True))


def gop_luoi(v: np.ndarray, f: np.ndarray, h: float, huong: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Gộp đỉnh theo ô lưới cạnh h (đỉnh mới = trung bình), bỏ tam giác suy biến / trùng.
    Chỉ gộp các đỉnh cùng ô VÀ cùng nhóm hướng pháp tuyến (14 hướng) → hai mặt của tấm mỏng (cơ dẹt, vòm sọ) không dính vào nhau."""
    q = np.floor((v - v.min(0)) / h).astype(np.int64)
    khoa = ((q[:, 0] * 1_000_003 + q[:, 1]) * 1_000_033 + q[:, 2]) * 16 + huong
    _, nhom = np.unique(khoa, return_inverse=True)
    n = nhom.max() + 1
    dem = np.bincount(nhom, minlength=n).astype(float)
    v2 = np.stack([np.bincount(nhom, v[:, i], minlength=n) / dem for i in range(3)], 1)
    f2 = nhom[f]
    tot = (f2[:, 0] != f2[:, 1]) & (f2[:, 1] != f2[:, 2]) & (f2[:, 0] != f2[:, 2])
    f2 = f2[tot]
    _, rieng = np.unique(np.sort(f2, 1), axis=0, return_index=True)
    f2 = f2[np.sort(rieng)]
    dung = np.unique(f2)
    moi = np.full(n, -1)
    moi[dung] = np.arange(len(dung))
    return v2[dung], moi[f2]


def giam(v: np.ndarray, f: np.ndarray, dich: int) -> tuple[np.ndarray, np.ndarray]:
    if len(f) <= dich * 1.1:
        return v, f
    cheo = float(np.linalg.norm(v.max(0) - v.min(0)))
    lo, hi = cheo * 1e-4, cheo / 3
    tot = (v, f)
    huong = np.argmax(phap_tuyen_dinh(v, f) @ _HUONG.T, axis=1)
    for _ in range(22):
        h = (lo * hi) ** 0.5
        v2, f2 = gop_luoi(v, f, h, huong)
        if len(f2) > dich:
            lo = h
        else:
            hi, tot = h, (v2, f2)
        if hi / lo < 1.04:
            break
    return tot


def uv_co(v: np.ndarray, doc_co_dinh: bool = False) -> np.ndarray:
    """v dọc trục dài (PCA; hoặc trục đứng nếu `doc_co_dinh`) chuẩn hoá 0..1 (hai đầu = gân), u theo trục rộng
    (đơn vị thế giới × 9 → vân thớ ~ mỗi 4 mm)."""
    tam = v - v.mean(0)
    _, _, truc = np.linalg.svd(tam, full_matrices=False)
    if doc_co_dinh:
        truc = np.array([[0.0, 1.0, 0.0], [1.0, 0.0, 0.0]])
    doc, ngang = tam @ truc[0], tam @ truc[1]
    vv = (doc - doc.min()) / max(1e-9, doc.max() - doc.min())
    uu = (ngang - ngang.min()) * 9.0
    return np.c_[uu, vv]


def trong_da_giac(x: np.ndarray, y: np.ndarray, dg: list[tuple[float, float]]) -> np.ndarray:
    trong = np.zeros(x.shape, bool)
    for (x1, y1), (x2, y2) in zip(dg, dg[1:] + dg[:1]):
        cat = ((y1 > y) != (y2 > y)) & (x < (x2 - x1) * (y - y1) / ((y2 - y1) or 1e-12) + x1)
        trong ^= cat
    return trong


def tam_co_theo_da(da: np.ndarray, da_giac_mm: list[tuple[float, float]], mat: str, sau_mm: tuple[float, float],
                   cat_mm: list[float] | None = None, o_mm: float = 5.0, gan_tren: np.ndarray | None = None) -> list[tuple[np.ndarray, np.ndarray]]:
    """Cơ dẹt BodyParts3D không có (cơ thẳng bụng, cơ lưng rộng): dựng tấm dày bám theo bề mặt da thật.
    `da` = đỉnh da (hệ trang); `da_giac_mm` = vùng (x, z) mm nhìn thẳng (bên trái bệnh nhân, x > 0 – tự lấy đối xứng cho bên phải);
    mặt ngoài / trong lùi vào trong da `sau_mm`; `cat_mm` = các độ cao (mm) chia thành nhiều bụng cơ (cơ thẳng bụng: chẽ gân ngang)."""
    huong = 1.0 if mat == "truoc" else -1.0                  # trước: lấy Z lớn nhất; sau: Z nhỏ nhất
    o = o_mm * K
    kq = []
    for ben in (1.0, -1.0):
        dg = [(ben * (K * x + 0) + CX, K * z + CY) for x, z in da_giac_mm]
        xs = np.array([p[0] for p in dg]); ys = np.array([p[1] for p in dg])
        gx = np.arange(xs.min(), xs.max() + o, o); gy = np.arange(ys.min(), ys.max() + o, o)
        X, Y = np.meshgrid(gx, gy)
        # độ cao da theo ô lưới (lấy mặt trước / sau cùng của da trong bán kính 1,5 ô), làm trơn 2 lượt
        gan = da[(da[:, 0] > xs.min() - 3 * o) & (da[:, 0] < xs.max() + 3 * o) & (da[:, 1] > ys.min() - 3 * o) & (da[:, 1] < ys.max() + 3 * o)]
        if gan_tren is not None:   # mặt chuẩn = nơi nhô ra nhất giữa các cơ sâu và da lùi vào 6 mm → tấm cơ phủ kín cơ sâu
            gan = np.r_[gan - [0, 0, huong * K * 6], gan_tren[(gan_tren[:, 0] > xs.min() - 3 * o) & (gan_tren[:, 0] < xs.max() + 3 * o)
                                                         & (gan_tren[:, 1] > ys.min() - 3 * o) & (gan_tren[:, 1] < ys.max() + 3 * o)]]
        Z = np.full(X.shape, np.nan)
        for i in range(X.shape[0]):
            for j in range(X.shape[1]):
                m = (np.abs(gan[:, 0] - X[i, j]) < 1.5 * o) & (np.abs(gan[:, 1] - Y[i, j]) < 1.5 * o)
                if m.any():
                    Z[i, j] = (gan[m, 2].max() if huong > 0 else gan[m, 2].min())
        for _ in range(2):
            P = np.pad(Z, 1, mode="edge")
            Z = np.nanmean(np.stack([P[1:-1, 1:-1], P[:-2, 1:-1], P[2:, 1:-1], P[1:-1, :-2], P[1:-1, 2:]]), 0)
        trong = trong_da_giac(X, Y, dg) & ~np.isnan(Z)
        doan = [Y.min() - 1] + [K * z + CY for z in (cat_mm or [])] + [Y.max() + 1]
        for y0, y1 in zip(doan, doan[1:]):
            # hàng sát ranh giới: thu vào 1 mm để các bụng cơ tách nhau bằng một khe gân hẹp
            o_tr = trong & (Y >= y0 + (0.0015 if y0 > Y.min() else 0)) & (Y <= y1 - (0.0015 if y1 < Y.max() else 0))
            o_tr &= np.pad(o_tr, 1)[2:, 1:-1] | np.pad(o_tr, 1)[:-2, 1:-1]   # bỏ ô lẻ
            if o_tr.sum() < 6:
                continue
            chi = -np.ones(X.shape, np.int64)
            chi[o_tr] = np.arange(o_tr.sum())
            n = int(o_tr.sum())
            dinh_ngoai = np.c_[X[o_tr], Y[o_tr], Z[o_tr] - huong * K * sau_mm[0]]
            dinh_trong = np.c_[X[o_tr], Y[o_tr], Z[o_tr] - huong * K * sau_mm[1]]
            mat_tg = []
            for i in range(X.shape[0] - 1):
                for j in range(X.shape[1] - 1):
                    a, b, c, d = chi[i, j], chi[i, j + 1], chi[i + 1, j + 1], chi[i + 1, j]
                    if min(a, b, c, d) >= 0:
                        mat_tg += [[a, b, c], [a, c, d]]
            if not mat_tg:
                continue
            f = np.array(mat_tg)
            # cạnh biên (chỉ thuộc 1 tam giác) → vách nối mặt ngoài – trong
            canh = np.sort(np.r_[f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]], 1)
            u, dem = np.unique(canh, axis=0, return_counts=True)
            bien = u[dem == 1]
            vach = np.r_[np.c_[bien[:, 0], bien[:, 1], bien[:, 1] + n], np.c_[bien[:, 0], bien[:, 1] + n, bien[:, 0] + n]]
            v = np.r_[dinh_ngoai, dinh_trong]
            f = np.r_[f, f[:, ::-1] + n, vach]
            # quay pháp tuyến ra ngoài: mặt ngoài theo hướng nhìn, mặt trong ngược lại, vách ra xa tâm tấm
            tam = v.mean(0)
            pt = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
            tam_tg = v[f].mean(1)
            ky_vong = np.where(np.arange(len(f))[:, None] < len(mat_tg), [[0, 0, huong]],
                               np.where(np.arange(len(f))[:, None] < 2 * len(mat_tg), [[0, 0, -huong]], (tam_tg - tam) * [1, 1, 0]))
            lat = (pt * ky_vong).sum(1) < 0
            f[lat] = f[lat][:, ::-1]
            kq.append((v, f))
    return kq


def tam_dinh(v: np.ndarray, dieu_kien: np.ndarray) -> list[float]:
    return [round(float(x), 4) for x in v[dieu_kien].mean(0)]


def main() -> None:
    goc = Path(sys.argv[1])
    ra = Path(sys.argv[2] if len(sys.argv) > 2 else "frontend/sinh-hoc/bp3d.bin.gz")
    phan_tu: dict[str, list] = {}
    ten_theo_bp: dict[str, list[str]] = {}
    lop_bp: dict[str, str] = {}
    tho: dict[str, np.ndarray] = {}           # đỉnh thô theo tên (tính khớp chi)
    for p in sorted(goc.glob("*.obj")):
        ten, v, f = doc_obj(p)
        luat = next(((bp, lop) for r, bp, lop in LUAT_RE if r.search(ten)), None)
        if not luat or not luat[0] or len(f) == 0:
            continue
        bp, lop = luat
        v = doi_toa_do(v)
        tho[ten] = v
        phan_tu.setdefault(bp, []).append((ten, v, f, dien_tich(v, f)))
        ten_theo_bp.setdefault(bp, []).append(ten)
        lop_bp[bp] = lop
    # hai cơ lớn BodyParts3D không có → dựng theo bề mặt da (vùng nhìn thẳng, mm, bên trái bệnh nhân)
    da = tho["Skin"]
    co_dung_them = {
        "co_thang_bung": ("Rectus abdominis (dựng theo bề mặt da)", tam_co_theo_da(
            da, [(9, 872), (42, 872), (58, 960), (72, 1080), (80, 1190), (9, 1190)], "truoc", (9, 19), cat_mm=[1015, 1085, 1140])),
        "co_lung_rong": ("Latissimus dorsi (dựng theo bề mặt da)", tam_co_theo_da(
            da, [(10, 1190), (10, 930), (55, 930), (112, 965), (140, 1040), (150, 1140), (155, 1245), (130, 1240), (90, 1205), (50, 1195)], "sau", (-2, 5),
            gan_tren=np.concatenate([v for bp, ds in phan_tu.items() if lop_bp[bp] == "co" and bp != "co_thang" for _, v, _, _ in ds]))),
    }
    for bp, (ten, ds) in co_dung_them.items():
        for v, f in ds:
            phan_tu.setdefault(bp, []).append((ten, v, f, dien_tich(v, f)))
        ten_theo_bp[bp] = [ten]
        lop_bp[bp] = "co"
    # ngân sách tam giác của từng phần tử trong mỗi lớp
    ngan_sach: dict[tuple[str, int], int] = {}
    for lop in NGAN_SACH:
        ds = [((bp, j), len(f), dt, next((w for r, w in TRONG_SO_RIENG if r.search(ten)), 1.0))
              for bp in phan_tu if lop_bp[bp] == lop for j, (ten, v, f, dt) in enumerate(phan_tu[bp])]
        ngan_sach.update(zip([x[0] for x in ds], chia_ngan_sach([x[1:] for x in ds], NGAN_SACH[lop] * HE_SO_NGAN_SACH)))
    nhom, khoi = [], []
    tong_tg = 0
    for bp in sorted(phan_tu, key=lambda b: (list(NGAN_SACH).index(lop_bp[b]), b)):
        lop = lop_bp[bp]
        vs, fs, uvs, n0 = [], [], [], 0
        for j, (ten, v, f, dt) in enumerate(phan_tu[bp]):
            v2, f2 = giam(v, f, ngan_sach[(bp, j)])
            vs.append(v2)
            fs.append(f2 + n0)
            if lop == "co":
                uvs.append(uv_co(v2, bp == "co_thang_bung"))
            n0 += len(v2)
        v = np.concatenate(vs)
        f = np.concatenate(fs)
        tong_tg += len(f)
        # điểm neo nhãn: đỉnh nhô ra trước nhất trong dải giữa chiều cao, bên trái bệnh nhân (x > 0) nếu nhóm có hai bên
        ben = v[:, 0] > 0.01 if (v[:, 0] > 0.01).any() and (v[:, 0] < -0.01).any() else np.ones(len(v), bool)
        y0, y1 = np.percentile(v[ben, 1], [30, 70])
        giua = ben & (v[:, 1] >= y0) & (v[:, 1] <= y1)
        neo = v[giua][np.argmax(v[giua][:, 2])]
        nhom.append({"bp": bp, "lop": lop, "nv": int(len(v)), "ni": int(len(f) * 3), "uv": lop == "co",
                     "neo": [round(float(x), 4) for x in neo], "phan": sorted(set(ten_theo_bp[bp]))})
        khoi.append((v, f, np.concatenate(uvs) if uvs else None))
    tat_ca = np.concatenate([k[0] for k in khoi])
    nho, lon = tat_ca.min(0) - 1e-4, tat_ca.max(0) + 1e-4
    # khớp chi (trái = +x, phải = −x) để trang dựng dây thần kinh chi, góc camera, vùng cơ thể
    khop = {}
    for ben, chu in (("trai", "Left"), ("phai", "Right")):
        h = tho[f"{chu} humerus"]
        cang = np.concatenate([tho[f"{chu} radius"], tho[f"{chu} ulna"]])
        d = tho[f"{chu} femur"]
        chay = tho[f"{chu} tibia"]
        sen = tho[f"{chu} talus"]
        ngon_tay = tho[f"Distal phalanx of {chu.lower()} middle finger"]
        dau_d = d[d[:, 1] > d[:, 1].max() - 0.09]
        khop[ben] = {
            "vai": tam_dinh(h, h[:, 1] > h[:, 1].max() - 0.06),
            "khuyu": tam_dinh(h, h[:, 1] < h[:, 1].min() + 0.045),
            "co_tay": tam_dinh(cang, cang[:, 1] < cang[:, 1].min() + 0.035),
            "dau_ngon_tay": tam_dinh(ngon_tay, np.ones(len(ngon_tay), bool)),
            "hong": tam_dinh(dau_d, np.abs(dau_d[:, 0]) < np.abs(dau_d[:, 0]).min() + 0.08),
            "goi": [round(float(x), 4) for x in (d[d[:, 1] < d[:, 1].min() + 0.04].mean(0) + chay[chay[:, 1] > chay[:, 1].max() - 0.04].mean(0)) / 2],
            "co_chan": tam_dinh(sen, np.ones(len(sen), bool)),
        }
    c7 = tho["Seventh cervical vertebra"]
    s1 = tho["Sacrum"]
    khop["c7"] = tam_dinh(c7, np.ones(len(c7), bool))
    khop["cung"] = tam_dinh(s1, s1[:, 1] > s1[:, 1].max() - 0.08)
    khop["day_chan"] = round(float(tat_ca[:, 1].min()), 4)
    khop["dinh_dau"] = round(float(tat_ca[:, 1].max()), 4)
    # ---- ghi nhị phân ----
    phan = []
    for (v, f, uv), g in zip(khoi, nhom):
        q = np.round((v - nho) / (lon - nho) * 65535).astype("<u2")
        phan.append(q.tobytes())
        if uv is not None:
            g["u_max"] = round(float(uv[:, 0].max()) + 1e-3, 3)
            phan.append(np.round(uv / [g["u_max"], 1.0] * 65535).clip(0, 65535).astype("<u2").tobytes())
        lon32 = len(v) > 65535
        g["i32"] = lon32
        chi_so = f.reshape(-1)
        hieu = np.diff(np.r_[0, chi_so]).astype(np.int64)          # mã hoá hiệu (zigzag) → gzip nén tốt hơn
        zz = ((hieu << 1) ^ (hieu >> 63)).astype("<u4" if lon32 else "<u2")
        phan.append(zz.tobytes())
    dau = {"phien_ban": 1, "k": K, "nho": [round(float(x), 5) for x in nho], "lon": [round(float(x), 5) for x in lon], "nhom": nhom, "khop": khop,
           "nguon": "BodyParts3D, © The Database Center for Life Science licensed under CC Attribution 4.0 International",
           "lien_ket": "https://dbarchive.biosciencedbc.jp/en/bodyparts3d/lic.html"}
    js = json.dumps(dau, ensure_ascii=False, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    du_lieu = b"BP3D" + struct.pack("<II", 1, len(js)) + js + b"".join(phan)
    ra.parent.mkdir(parents=True, exist_ok=True)
    ra.write_bytes(gzip.compress(du_lieu, 9, mtime=0))
    print(f"{len(nhom)} nhóm, {tong_tg} tam giác, {len(du_lieu) / 1e6:.2f} MB → gzip {ra.stat().st_size / 1e6:.2f} MB: {ra}")
    for g in nhom:
        print(f"  {g['bp']:22s} {g['lop']:9s} {g['nv']:7d} đỉnh {g['ni'] // 3:7d} tg  {len(g['phan'])} phần tử")


if __name__ == "__main__":
    main()
