"""Sinh "dữ liệu vàng" để kiểm bản JavaScript của bộ điều khiển tay có chạy GIỐNG HỆT bản Python mẫu.

Bản mẫu: tools/tay_python/xoay_3d.py (doc_tay + DieuKhien) và xem_tay.py (ti_le_chum, ngưỡng chụm).
Mỗi kịch bản là một chuỗi khung 30 Hz với bàn tay giả (21 điểm + toạ độ 3D). Ghi cả đầu vào lẫn kết quả
từng khung vào tests/js/du_lieu/dieu_khien_tay_vang.json; test JS phải cho ra đúng các số đó (sai số ≤ 1e-6).

Chạy (cần mediapipe + opencv vì xem_tay.py import chúng):
    ~/hand3d-env/bin/python tools/tay_python/tao_du_lieu_vang.py
"""
import json
import math
import random
from pathlib import Path
from types import SimpleNamespace as N

import xoay_3d as x3
import xem_tay as xt

RA = Path(__file__).resolve().parents[2] / "tests" / "js" / "du_lieu" / "dieu_khien_tay_vang.json"
T = 1 / 30
TI_LE = 16 / 9
NGON = [(-0.12, -0.05), (-0.05, -0.10), (0.0, -0.11), (0.05, -0.10), (0.10, -0.08)]


def tay(cx, cy, chum, ben="Right", lech=None):
    """Bàn tay giả: cổ tay ở (cx, cy), 5 ngón toả lên; chum=True thì đầu ngón cái (4) sát đầu ngón trỏ (8)."""
    p = [(0.0, 0.0)]
    for gx, gy in NGON:
        for k in (1, 2, 3, 4):
            p.append((gx * k * 0.45, gy * k * 0.45))
    if chum:
        p[4] = (p[8][0] - 0.002, p[8][1] + 0.002)
    lech = lech or [(0.0, 0.0)] * 21
    diem = [[round(cx + x + dx, 6), round(cy + y + dy, 6), 0.0] for (x, y), (dx, dy) in zip(p, lech)]
    the_gioi = [[round(x * 0.5, 6), round(y * 0.5, 6), 0.0] for x, y in p]
    return {"ben": ben, "diem": diem, "theGioi": the_gioi}


def ra_ket_qua_mediapipe(ds):
    """Đổi khung JSON → đối tượng giống HandLandmarkerResult của MediaPipe Python."""
    return N(hand_landmarks=[[N(x=a, y=b, z=c) for a, b, c in h["diem"]] for h in ds],
             hand_world_landmarks=[[N(x=a, y=b, z=c) for a, b, c in h["theGioi"]] for h in ds],
             handedness=[[N(category_name=h["ben"])] for h in ds])


def chay(khung_vao):
    """Cho chuỗi khung qua doc_tay + DieuKhien, ghi lại kết quả từng khung."""
    dk, cac_tay, t, ra = x3.DieuKhien(), {}, 0.0, []
    for tay_khung in khung_vao:
        t += T
        ds = x3.doc_tay(ra_ket_qua_mediapipe(tay_khung), TI_LE, t, cac_tay)
        dk.cap_nhat(t, T, ds)
        ra.append({
            "t": t,
            "tay": [{"ben": h["ben"], "conTro": [float(h["con_tro"][0]), float(h["con_tro"][1])],
                     "cham": h["cham"], "tl": h["tl"], "xoe": h["xoe"]} for h in ds],
            "yaw": dk.yaw, "pitch": dk.pitch, "zoom": dk.zoom, "cheDo": dk.che_do,
            "wYaw": dk.w_yaw, "wPitch": dk.w_pitch, "veDau": dk.ve_dau,
        })
    return [{"t": r["t"], "dt": T, "tiLe": TI_LE, "tay": k} for r, k in zip(ra, khung_vao)], ra


def kich_ban():
    kb = {}
    # 1. Kéo nhanh sang phải rồi thả khi vẫn đang đi → lướt, chậm dần, dừng
    k = [[tay(0.40, 0.5, False)] for _ in range(10)] + [[tay(0.40, 0.5, True)] for _ in range(6)]
    k += [[tay(0.40 + 0.03 * (i + 1), 0.5, True)] for i in range(9)]
    k += [[tay(0.67 + 0.03 * (i + 1), 0.5, False)] for i in range(3)]
    k += [[tay(0.76, 0.5, False)] for _ in range(100)]
    kb["keo_luot"] = k
    # 2. Đang lướt thì chụm lại → dừng ngay, rồi kéo tiếp
    k = [[tay(0.40, 0.5, True)] for _ in range(3)] + [[tay(0.40 + 0.04 * (i + 1), 0.5, True)] for i in range(7)]
    k += [[tay(0.68, 0.5, False)] for _ in range(12)] + [[tay(0.68, 0.5, True)] for _ in range(5)]
    k += [[tay(0.68 - 0.01 * (i + 1), 0.5, True)] for i in range(10)]
    kb["bat_lai"] = k
    # 3. Kéo chậm, giữ yên 0,5 s rồi thả → không lướt
    k = [[tay(0.4, 0.5, True)] for _ in range(3)] + [[tay(0.4 + 0.004 * i, 0.5, True)] for i in range(15)]
    k += [[tay(0.456, 0.5, True)] for _ in range(15)] + [[tay(0.456, 0.5, False)] for _ in range(5)]
    kb["keo_cham_tha"] = k
    # 4. Hai tay chụm kéo giãn → phóng to, kéo lại gần → thu nhỏ, nhả một tay → chuyển sang xoay
    k = [[tay(0.35, 0.5, True, "Left"), tay(0.65, 0.5, True, "Right")] for _ in range(3)]
    k += [[tay(0.35 - 0.004 * i, 0.5, True, "Left"), tay(0.65 + 0.004 * i, 0.5, True, "Right")] for i in range(20)]
    k += [[tay(0.274 + 0.006 * i, 0.5, True, "Left"), tay(0.726 - 0.006 * i, 0.5, True, "Right")] for i in range(15)]
    k += [[tay(0.358, 0.5, True, "Left"), tay(0.642, 0.5, False, "Right")] for _ in range(4)]
    k += [[tay(0.358 + 0.01 * i, 0.5, True, "Left"), tay(0.642, 0.5, False, "Right")] for i in range(10)]
    kb["zoom_hai_tay"] = k
    # 5. Mất tay ngắn khi kéo (nối tiếp) rồi mất lâu (huỷ, không lướt)
    k = [[tay(0.4, 0.5, True)] for _ in range(3)] + [[tay(0.4 + 0.03 * i, 0.5, True)] for i in range(8)]
    k += [[] for _ in range(3)] + [[tay(0.64 + 0.03 * i, 0.5, True)] for i in range(4)] + [[] for _ in range(10)]
    kb["mat_tay"] = k
    # 6. Xoè hai bàn tay 1,2 s → về góc đầu (có hoạt cảnh bay về)
    k = [[tay(0.4, 0.5, True)] for _ in range(3)] + [[tay(0.4 + 0.02 * i, 0.45 + 0.01 * i, True)] for i in range(12)]
    k += [[tay(0.62, 0.56, False)] for _ in range(3)]
    k += [[tay(0.3, 0.5, False, "Left"), tay(0.7, 0.5, False, "Right")] for _ in range(36)] + [[] for _ in range(60)]
    kb["xoe_dat_lai"] = k
    # 7. Kéo xuống thật xa → nghiêng tới giới hạn 75°
    k = [[tay(0.5, 0.3, True)] for _ in range(3)] + [[tay(0.5, 0.3 + 0.02 * i, True)] for i in range(30)]
    kb["keo_doc"] = k
    # 8. Tay chụm đứng yên nhưng rung (nhiễu ±0,004) → bộ lọc + ngưỡng trượt giữ mô hình đứng yên
    r = random.Random(7)
    k = [[tay(0.5, 0.5, True, lech=[(r.uniform(-0.004, 0.004), r.uniform(-0.004, 0.004))] * 21)] for _ in range(60)]
    kb["rung_tay"] = k
    # 9. Đổi tay: tay phải kéo, nhả, rồi tay trái chụm kéo
    k = [[tay(0.6, 0.5, True)] for _ in range(3)] + [[tay(0.6 + 0.01 * i, 0.5, True)] for i in range(8)]
    k += [[tay(0.68, 0.5, False), tay(0.3, 0.5, True, "Left")] for _ in range(3)]
    k += [[tay(0.68, 0.5, False), tay(0.3 - 0.01 * i, 0.5 + 0.005 * i, True, "Left")] for i in range(10)]
    kb["doi_tay"] = k
    return kb


def main():
    LOC = x3.LocOneEuro()
    hang_so = {"CHAM_BAT": xt.CHAM_BAT, "CHAM_NHA": xt.CHAM_NHA, "GOC_DAU": list(x3.GOC_DAU), "ZOOM_MIN": x3.ZOOM_MIN,
               "ZOOM_MAX": x3.ZOOM_MAX, "PITCH_MAX": x3.PITCH_MAX, "DO_NHAY": x3.DO_NHAY, "NGUONG_TRUOT": x3.NGUONG_TRUOT,
               "QT_NGUONG": x3.QT_NGUONG, "QT_TAT": x3.QT_TAT, "QT_MAX": x3.QT_MAX, "MAT_TAY_S": x3.MAT_TAY_S,
               "XOE_GIU_S": x3.XOE_GIU_S, "ONE_EURO": {"minCutoff": LOC.mc, "beta": LOC.beta, "dCutoff": LOC.dc}}
    du_lieu = {"mo_ta": "Dữ liệu vàng sinh từ tools/tay_python/tao_du_lieu_vang.py – KHÔNG sửa tay.",
               "hang_so": hang_so, "kich_ban": {}}
    for ten, khung in kich_ban().items():
        vao, ra = chay(khung)
        du_lieu["kich_ban"][ten] = {"vao": vao, "ra": ra}
        cuoi = ra[-1]
        print(f"{ten:13s} {len(khung):4d} khung | yaw {math.degrees(cuoi['yaw']):+7.1f}° pitch "
              f"{math.degrees(cuoi['pitch']):+6.1f}° zoom x{cuoi['zoom']:.2f} | che_do {cuoi['cheDo']}")
    RA.parent.mkdir(parents=True, exist_ok=True)
    RA.write_text(json.dumps(du_lieu, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"Đã ghi {RA} ({RA.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
