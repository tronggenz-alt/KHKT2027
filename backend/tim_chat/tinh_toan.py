"""Module tính toán hoá học thuần bằng CODE (SGK Hoá học Việt Nam).
Không để AI tự nhẩm; đảm bảo độ chính xác tuyệt đối về số liệu định lượng,
bảo toàn nguyên tố, độ bất bão hoà, công thức đơn giản nhất và CTPT khả dĩ.
"""
from __future__ import annotations

import math
import re
from fractions import Fraction
from typing import Any

# Bảng nguyên tử khối chuẩn SGK Hoá học Việt Nam (g/mol)
NGUYEN_TU_KHOI: dict[str, float] = {
    "H": 1.0,
    "C": 12.0,
    "N": 14.0,
    "O": 16.0,
    "F": 19.0,
    "Na": 23.0,
    "Mg": 24.0,
    "Al": 27.0,
    "P": 31.0,
    "S": 32.0,
    "Cl": 35.5,
    "K": 39.0,
    "Ca": 40.0,
    "Fe": 56.0,
    "Cu": 64.0,
    "Zn": 65.0,
    "Br": 80.0,
    "Ag": 108.0,
    "I": 127.0,
    "Ba": 137.0,
    "Pb": 207.0,
}

# Các nhóm nguyên tử thông dụng
NHOM_NGUYEN_TU: dict[str, dict[str, int]] = {
    "OH": {"O": 1, "H": 1},
    "COOH": {"C": 1, "O": 2, "H": 1},
    "COO": {"C": 1, "O": 2},
    "CHO": {"C": 1, "H": 1, "O": 1},
    "NH2": {"N": 1, "H": 2},
    "NO2": {"N": 1, "O": 2},
    "SO3H": {"S": 1, "O": 3, "H": 1},
    "CH3": {"C": 1, "H": 3},
    "C2H5": {"C": 2, "H": 5},
    "C6H5": {"C": 6, "H": 5},
}


def phan_tich_cong_thuc(ct: str) -> dict[str, int]:
    """Phân tích chuỗi CTPT hoặc CTCT thu gọn (có thể có ngoặc) thành số lượng từng nguyên tố.
    Ví dụ: 'C6H8O6' -> {'C': 6, 'H': 8, 'O': 6}
           'CH3COOCH=CH2' -> {'C': 4, 'H': 6, 'O': 2}
           'HOOC-CH2-COOH' -> {'C': 3, 'H': 4, 'O': 4}
           '(NH4)2CO3' -> {'N': 2, 'H': 8, 'C': 1, 'O': 3}
    """
    if not ct or not ct.strip():
        return {}

    chuoi = ct.strip()
    # Loại bỏ các ký tự nối liên kết: -, =, ≡, #, space
    chuoi = re.sub(r"[\-=\s≡#~·\.]", "", chuoi)

    # Đệ quy xử lý ngoặc
    def _xu_ly_ngoac(s: str) -> dict[str, int]:
        pattern = r"\(([^()]+)\)(\d*)"
        while True:
            match = re.search(pattern, s)
            if not match:
                break
            noi_dung = match.group(1)
            he_so = int(match.group(2)) if match.group(2) else 1
            con = _xu_ly_don_gian(noi_dung)
            # Thay thế ngoặc bằng chuỗi mở rộng hoặc xử lý ghép dict
            # Để đơn giản, thay thế bằng chuỗi các nguyên tố lặp lại
            mo_rong = "".join(f"{nt}{so * he_so}" for nt, so in con.items())
            s = s[: match.start()] + mo_rong + s[match.end() :]
        return _xu_ly_don_gian(s)

    def _xu_ly_don_gian(s: str) -> dict[str, int]:
        ket_qua: dict[str, int] = {}
        # Pattern tìm nguyên tố (chữ cái hoa + chữ cái thường tuỳ chọn) và số chỉ
        tokens = re.findall(r"([A-Z][a-z]?)(\d*)", s)
        for nt, so_str in tokens:
            if not nt:
                continue
            so = int(so_str) if so_str else 1
            ket_qua[nt] = ket_qua.get(nt, 0) + so
        return ket_qua

    return _xu_ly_ngoac(chuoi)


def tinh_khoi_luong_mol(ct: str) -> float:
    """Tính phân tử khối / khối lượng mol theo nguyên tử khối chuẩn SGK."""
    nguyen_to = phan_tich_cong_thuc(ct)
    if not nguyen_to:
        return 0.0
    tong_m = 0.0
    for nt, so in nguyen_to.items():
        if nt not in NGUYEN_TU_KHOI:
            raise ValueError(f"Nguyên tố lạ '{nt}' không có trong bảng nguyên tử khối SGK.")
        tong_m += NGUYEN_TU_KHOI[nt] * so
    return round(tong_m, 2)


def tinh_do_bat_bao_hoa(c: int, h: int, n: int = 0, x_halogen: int = 0) -> float:
    """Tính độ bất bão hoà k (hay Delta) theo công thức SGK:
    k = (2*C + 2 + N - H - X) / 2
    """
    if c <= 0:
        return 0.0
    k = (2 * c + 2 + n - h - x_halogen) / 2.0
    return round(k, 2)


def tinh_do_bat_bao_hoa_tu_ct(ct: str) -> float:
    """Tính độ bất bão hoà k trực tiếp từ công thức."""
    nt = phan_tich_cong_thuc(ct)
    c = nt.get("C", 0)
    h = nt.get("H", 0)
    n = nt.get("N", 0)
    x = nt.get("Cl", 0) + nt.get("Br", 0) + nt.get("I", 0) + nt.get("F", 0)
    return tinh_do_bat_bao_hoa(c, h, n, x)


def tinh_ct_don_gian_nhat_tu_phan_tram(
    phan_tram: dict[str, float],
    sai_so: float = 0.05,
    max_he_so: int = 15,
) -> dict[str, Any]:
    """Tính công thức đơn giản nhất từ % khối lượng các nguyên tố.
    Ví dụ: phan_tram = {'C': 40.0, 'H': 6.67, 'O': 53.33}
    -> {'ct_don_gian': 'CH2O', 'ti_le': {'C': 1, 'H': 2, 'O': 1}, 'M_don_gian': 30.0}
    """
    if not phan_tram:
        return {"ct_don_gian": "", "ti_le": {}, "M_don_gian": 0.0}

    # Tính n = % / M
    mol_rel: dict[str, float] = {}
    for nt, pct in phan_tram.items():
        if nt not in NGUYEN_TU_KHOI:
            raise ValueError(f"Nguyên tố '{nt}' không xác định.")
        mol_rel[nt] = pct / NGUYEN_TU_KHOI[nt]

    min_mol = min(mol_rel.values())
    if min_mol <= 0:
        return {"ct_don_gian": "", "ti_le": {}, "M_don_gian": 0.0}

    # Chuẩn hoá tỉ lệ về min_mol = 1
    ti_le_thuc = {nt: v / min_mol for nt, v in mol_rel.items()}

    # Tìm bội số nguyên nhỏ nhất (nhân với 1, 2, 3, 4, 5, 6... đến khi tất cả gần nguyên)
    best_ti_le: dict[str, int] = {}
    best_error = 999.0

    for boi in range(1, max_he_so + 1):
        tam_ti_le = {}
        error = 0.0
        for nt, r in ti_le_thuc.items():
            val = r * boi
            tron = round(val)
            diff = abs(val - tron)
            error += diff
            tam_ti_le[nt] = max(1, tron)
        if error < best_error:
            best_error = error
            best_ti_le = tam_ti_le
            if error < sai_so * len(ti_le_thuc):
                break

    # Dựng chuỗi công thức đơn giản nhất theo thứ tự C, H, O, N, các nguyên tố khác
    thu_tu_nt = ["C", "H", "O", "N", "S", "P", "Cl", "Br", "I", "Na", "K"]
    cac_nt = sorted(best_ti_le.keys(), key=lambda x: thu_tu_nt.index(x) if x in thu_tu_nt else 99)

    ct_parts = []
    m_dg = 0.0
    for nt in cac_nt:
        so = best_ti_le[nt]
        ct_parts.append(f"{nt}{so if so > 1 else ''}")
        m_dg += NGUYEN_TU_KHOI[nt] * so

    ct_don_gian = "".join(ct_parts)
    return {
        "ct_don_gian": ct_don_gian,
        "ti_le": best_ti_le,
        "M_don_gian": round(m_dg, 2),
        "ti_le_mol_goc": {k: round(v, 4) for k, v in mol_rel.items()},
    }


def tinh_dot_chay(
    m_chat: float | None = None,
    m_co2: float | None = None,
    v_co2: float | None = None,
    m_h2o: float | None = None,
    v_n2: float | None = None,
    m_n2: float | None = None,
    dktc: bool = True,
) -> dict[str, Any]:
    """Tính toán dữ liệu đốt cháy hợp chất hữu cơ (C, H, O, N).
    - Tính số mol C, H, N và O (bảo toàn khối lượng nếu có m_chat).
    - Tính công thức đơn giản nhất.
    """
    V_MOL = 22.4 if dktc else 24.79

    # Tính mol C
    n_c = 0.0
    if m_co2 is not None:
        n_c = m_co2 / 44.0
    elif v_co2 is not None:
        n_c = v_co2 / V_MOL

    # Tính mol H
    n_h = 0.0
    if m_h2o is not None:
        n_h = (m_h2o / 18.0) * 2.0

    # Tính mol N
    n_n = 0.0
    if v_n2 is not None:
        n_n = (v_n2 / V_MOL) * 2.0
    elif m_n2 is not None:
        n_n = (m_n2 / 28.0) * 2.0

    # Tính khối lượng C, H, N
    m_c = n_c * 12.0
    m_h = n_h * 1.0
    m_n = n_n * 14.0

    # Tính khối lượng và mol O nếu biết m_chat
    m_o = 0.0
    n_o = 0.0
    co_oxy = False

    if m_chat is not None:
        m_c_h_n = m_c + m_h + m_n
        chenh_lech = m_chat - m_c_h_n
        if chenh_lech > 0.005:  # lớn hơn sai số làm tròn
            m_o = chenh_lech
            n_o = m_o / 16.0
            co_oxy = True

    # Tạo dict mol để tính công thức đơn giản nhất
    ti_le_mol = {"C": n_c, "H": n_h}
    if co_oxy or n_o > 0:
        ti_le_mol["O"] = n_o
    if n_n > 0:
        ti_le_mol["N"] = n_n

    # Tính % khối lượng nếu có m_chat
    phan_tram: dict[str, float] = {}
    if m_chat and m_chat > 0:
        phan_tram["C"] = round((m_c / m_chat) * 100, 2)
        phan_tram["H"] = round((m_h / m_chat) * 100, 2)
        if co_oxy:
            phan_tram["O"] = round((m_o / m_chat) * 100, 2)
        if n_n > 0:
            phan_tram["N"] = round((m_n / m_chat) * 100, 2)

    ct_dg = tinh_ct_don_gian_nhat_tu_phan_tram(phan_tram) if phan_tram else {}

    return {
        "n_C": round(n_c, 5),
        "n_H": round(n_h, 5),
        "n_O": round(n_o, 5),
        "n_N": round(n_n, 5),
        "m_C": round(m_c, 4),
        "m_H": round(m_h, 4),
        "m_O": round(m_o, 4),
        "m_N": round(m_n, 4),
        "phan_tram": phan_tram,
        "ct_don_gian": ct_dg.get("ct_don_gian", ""),
        "ti_le": ct_dg.get("ti_le", {}),
        "M_don_gian": ct_dg.get("M_don_gian", 0.0),
    }


def liet_ke_ctpt_kha_di(
    M: float | None = None,
    M_min: float | None = None,
    M_max: float | None = None,
    ct_don_gian: str | None = None,
    chua_nguyen_to: list[str] | None = None,
    k_toi_da: int = 8,
    c_toi_da: int = 25,
) -> list[dict[str, Any]]:
    """Liệt kê các CTPT khả dĩ theo phân tử khối M hoặc bội số công thức đơn giản nhất.
    Tuân thủ các ràng buộc hoá học hữu cơ:
    - k >= 0 (nguyên)
    - H <= 2*C + 2 + N
    - H và N cùng tính chẵn lẻ
    """
    ket_qua: list[dict[str, Any]] = []

    # Trường hợp 1: Có công thức đơn giản nhất
    if ct_don_gian:
        nt_dg = phan_tich_cong_thuc(ct_don_gian)
        m_dg = tinh_khoi_luong_mol(ct_don_gian)
        if m_dg > 0:
            # Nếu có M chính xác
            if M is not None and M > 0:
                n_boi = round(M / m_dg)
                if abs(M - n_boi * m_dg) <= 1.0 and n_boi >= 1:
                    ct_boi = "".join(f"{nt}{so * n_boi}" for nt, so in nt_dg.items())
                    k = tinh_do_bat_bao_hoa_tu_ct(ct_boi)
                    if k >= 0 and k.is_integer():
                        ket_qua.append({
                            "ctpt": ct_boi,
                            "M": round(n_boi * m_dg, 2),
                            "k": int(k),
                            "n_boi": n_boi,
                        })
                return ket_qua

            # Nếu cho khoảng M
            gioi_han_min = M_min if M_min is not None else m_dg
            gioi_han_max = M_max if M_max is not None else 500.0

            for n_boi in range(1, 15):
                m_curr = n_boi * m_dg
                if m_curr > gioi_han_max + 1.0:
                    break
                if m_curr >= gioi_han_min - 1.0:
                    ct_boi = "".join(f"{nt}{so * n_boi}" for nt, so in nt_dg.items())
                    k = tinh_do_bat_bao_hoa_tu_ct(ct_boi)
                    if k >= 0 and k.is_integer():
                        ket_qua.append({
                            "ctpt": ct_boi,
                            "M": round(m_curr, 2),
                            "k": int(k),
                            "n_boi": n_boi,
                        })
            return ket_qua

    # Trường hợp 2: Cho M, duyệt tìm CTPT phù hợp (C_x H_y O_z N_t)
    if M is not None and M > 0:
        target_m = round(M)
        nguyen_to_set = set(chua_nguyen_to or ["C", "H", "O"])
        co_o = "O" in nguyen_to_set
        co_n = "N" in nguyen_to_set

        # Duyệt C từ 1 đến c_toi_da
        for c in range(1, c_toi_da + 1):
            if c * 12 >= target_m:
                break
            max_n = 4 if co_n else 0
            for n in range(0, max_n + 1):
                if c * 12 + n * 14 >= target_m:
                    break
                max_o = 8 if co_o else 0
                for o in range(0, max_o + 1):
                    con_lai = target_m - (c * 12 + n * 14 + o * 16)
                    h = con_lai
                    if h < 0:
                        break
                    # Kiểm tra tính hợp lệ
                    # H và N cùng tính chẵn lẻ
                    if (h % 2) != (n % 2):
                        continue
                    if h > 2 * c + 2 + n:
                        continue
                    k = tinh_do_bat_bao_hoa(c, h, n)
                    if k < 0 or not k.is_integer() or k > k_toi_da:
                        continue

                    # Dựng CTPT
                    ct = f"C{c}H{h}"
                    if o > 0:
                        ct += f"O{o if o > 1 else ''}"
                    if n > 0:
                        ct += f"N{n if n > 1 else ''}"

                    ket_qua.append({
                        "ctpt": ct,
                        "M": float(target_m),
                        "k": int(k),
                        "C": c,
                        "H": h,
                        "O": o,
                        "N": n,
                    })

    # Giới hạn tối đa 15 ứng viên khả dĩ nhất
    return ket_qua[:15]


def kiem_tra_ctct_khop_du_kien(
    ctct: str,
    ctpt_mong_doi: str | None = None,
    k_mong_doi: float | None = None,
    m_mong_doi: float | None = None,
) -> dict[str, Any]:
    """Kiểm tra một công thức cấu tạo thu gọn có khớp các tiêu chí CTPT, k, M hay không."""
    nt = phan_tich_cong_thuc(ctct)
    if not nt:
        return {"hop_le": False, "ly_do": "Không phân tích được CTCT."}

    m_thuc = tinh_khoi_luong_mol(ctct)
    k_thuc = tinh_do_bat_bao_hoa_tu_ct(ctct)

    ctpt_thuc = "".join(f"{k}{v if v > 1 else ''}" for k, v in sorted(nt.items()))

    loi: list[str] = []

    if ctpt_mong_doi:
        nt_mong_doi = phan_tich_cong_thuc(ctpt_mong_doi)
        if nt != nt_mong_doi:
            loi.append(f"CTPT thực tế ({ctpt_thuc}) khác CTPT mong đợi ({ctpt_mong_doi})")

    if k_mong_doi is not None and abs(k_thuc - k_mong_doi) > 0.01:
        loi.append(f"Độ bất bão hoà k = {k_thuc} khác k mong đợi = {k_mong_doi}")

    if m_mong_doi is not None and abs(m_thuc - m_mong_doi) > 0.5:
        loi.append(f"Phân tử khối M = {m_thuc} khác M mong đợi = {m_mong_doi}")

    return {
        "hop_le": len(loi) == 0,
        "ctpt_thuc": ctpt_thuc,
        "M": m_thuc,
        "k": k_thuc,
        "loi": loi,
    }
