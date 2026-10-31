"""Tạo dữ liệu data/sinh/dung_cu.json cho 108 dụng cụ sinh học tầng 2."""

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA_SINH = ROOT / "data" / "sinh"
HOP_DONG = ROOT / "phong_sinh" / "HOP_DONG_GIAO_DIEN_BACKEND.md"
THIET_BI_T1 = ROOT / "data" / "chem" / "thiet_bi.json"


def get_contract_tools():
    text = HOP_DONG.read_text(encoding="utf-8")
    m2 = re.search(r"## 2\..*?(?=## 3\.)", text, re.DOTALL)
    danh_muc = {}
    for line in m2.group(0).splitlines():
        line = line.strip()
        if not line.startswith("|") or line.startswith("|---") or "Thành phần Unity" in line or "| Mã |" in line:
            continue
        parts = [p.strip() for p in line.strip("|").split("|")]
        if len(parts) < 6:
            continue
        ma = parts[0].strip("`")
        ten = parts[1]
        phong_raw = parts[2]
        unity_raw = parts[3]
        may_raw = parts[4].strip("`") if parts[4].strip() else None
        muc = parts[5].strip()

        def _chuan_hoa_unity(m: re.Match) -> str:
            val = m.group(1)
            if "." in val:
                return val
            return f"Khkt.Demo.Bio.{val}"

        unity_norm = re.sub(r"`([^`]+)`", _chuan_hoa_unity, unity_raw)
        danh_muc[ma] = {
            "ma": ma,
            "ten": ten,
            "phong_raw": phong_raw,
            "thanh_phan_unity": unity_norm,
            "may_man_hinh": may_raw,
            "muc_dac_ta": muc,
        }
    return danh_muc


def main():
    import sys
    sys.path.insert(0, str(ROOT))
    from tools.dulieu_108 import DETAILS, MAP_STT_T1

    DATA_SINH.mkdir(parents=True, exist_ok=True)
    hd = get_contract_tools()

    dung_cu_list = []
    for ma, item in hd.items():
        p_raw = item["phong_raw"]
        if "mọi phòng" in p_raw:
            phong = ["phong_sinh", "phong_vi_sinh", "phong_sinh_phan_tu", "nha_kinh"]
        else:
            phong = [p.strip() for p in p_raw.split(",") if p.strip()]

        det = DETAILS[ma]
        (ten_en, phan_loai, so_luong, vi_tri, cong_dung, thong_so, cach_dung, loi_thuong_gap, an_toan, hieu_chuan) = det

        rec = {
            "ma": ma,
            "ten": item["ten"],
            "ten_tieng_anh": ten_en,
            "phan_loai": phan_loai,
            "phong": phong,
            "so_luong": so_luong,
            "vi_tri": vi_tri,
            "cong_dung": cong_dung,
            "thong_so": thong_so,
            "cach_dung": cach_dung,
            "loi_thuong_gap": loi_thuong_gap,
            "an_toan": an_toan,
            "hieu_chuan_ngay": hieu_chuan,
            "thanh_phan_unity": item["thanh_phan_unity"],
            "may_man_hinh": item["may_man_hinh"],
            "muc_dac_ta": item["muc_dac_ta"],
        }
        if ma in MAP_STT_T1:
            rec["stt_thiet_bi_tang_1"] = MAP_STT_T1[ma]

        dung_cu_list.append(rec)

    out = {
        "phien_ban": 1,
        "dung_cu": dung_cu_list
    }

    out_file = DATA_SINH / "dung_cu.json"
    out_file.write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Created {out_file} with {len(dung_cu_list)} items.")


if __name__ == "__main__":
    main()
