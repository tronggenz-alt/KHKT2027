"""
Cập nhật các file .mat trong Assets/Lab/Materials sang chuẩn PBR không sọc:
1. Tường sơn, trần, thân tủ (TuongSon, Tran, TuongNgoai, ThanTu, T2_TuongKem):
   Bỏ Normal Map (_BumpMap: {fileID: 0}, _BumpScale: 0, xóa keyword _NORMALMAP).
2. Xóa bỏ nhân đôi độ bóng (Smoothness Double-Multiplication):
   Đặt _Smoothness: 1 trên mọi vật liệu có _MetallicGlossMap, để kênh Alpha của mask
   trực tiếp quyết định 100% độ bóng thật (True Smoothness = mask.a).
3. Sàn gạch (SanPhongLab, SanHanhLang, SanPhongHoaChat, SanBanCong):
   Đồng bộ m_Scale của _BumpMap và _MetallicGlossMap khớp với _BaseMap (16.67, 12.5).
"""

import os
import re

MAT_DIR = "/Users/trantrong/VERALab_bemat/Phong thi nghiem ao/Assets/Lab/Materials"

SMOOTH_PAINT_MATERIALS = {
    "TuongSon.mat", "Tran.mat", "TuongNgoai.mat", "ThanTu.mat", "T2_TuongKem.mat"
}

FLOOR_MATERIALS = {
    "SanPhongLab.mat", "SanHanhLang.mat", "SanPhongHoaChat.mat", "SanBanCong.mat"
}

PBR_TARGET_MATERIALS = {
    "TuongSon.mat", "Tran.mat", "TuongNgoai.mat", "ThanTu.mat", "T2_TuongKem.mat",
    "SanPhongLab.mat", "SanHanhLang.mat", "SanPhongHoaChat.mat", "SanBanCong.mat",
    "MatBanPhenolic.mat", "MatBanEpoxy.mat", "CanhTu.mat", "Inox.mat", "KimLoaiInox.mat",
    "GiaInox.mat", "ChanGhe.mat", "KhungCuaTuDong.mat", "AlumAnodized029.mat",
    "T2_SanGo.mat", "T2_BanSinhHoc.mat", "T2_TuAnToanSinhHoc.mat", "T2_TuAm.mat",
    "T2_NapRanh.mat", "T2_DatAm.mat", "T2_GomTerracotta.mat"
}

def update_material(path):
    fname = os.path.basename(path)
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    orig_content = content

    # 1. Nếu là vật liệu sơn trơn: bỏ Normal Map
    if fname in SMOOTH_PAINT_MATERIALS:
        # Thay _BumpMap texture thành {fileID: 0}
        content = re.sub(
            r'(- _BumpMap:\s+m_Texture:\s*)\{[^}]+\}',
            r'\1{fileID: 0}',
            content
        )
        content = re.sub(r'(- _BumpScale:\s*)[0-9.]+', r'\g<1>0', content)
        # Xóa _NORMALMAP khỏi shader keywords nếu có
        content = re.sub(r'\b_NORMALMAP\b\s*', '', content)

    # 2. Xóa lỗi nhân đôi: đặt _Smoothness: 1 trên các vật liệu PBR dùng mask
    if fname in PBR_TARGET_MATERIALS or "_MetallicGlossMap:" in content:
        # Kiểm tra xem có gán texture cho _MetallicGlossMap không
        has_mask = not bool(re.search(r'- _MetallicGlossMap:\s+m_Texture:\s*\{fileID:\s*0\}', content))
        if has_mask:
            content = re.sub(r'(- _Smoothness:\s*)[0-9.]+', r'\g<1>1', content)

    # 3. Đồng bộ tiling sàn gạch
    if fname in FLOOR_MATERIALS:
        # Tìm scale của _BaseMap
        m = re.search(r'- _BaseMap:\s+m_Texture:[^\n]+\n\s+m_Scale:\s*\{x:\s*([0-9.]+),\s*y:\s*([0-9.]+)\}', content)
        if m:
            sx, sy = m.group(1), m.group(2)
            # Thay scale của _BumpMap và _MetallicGlossMap
            content = re.sub(
                r'(- _BumpMap:\s+m_Texture:[^\n]+\n\s+m_Scale:\s*)\{x:\s*[0-9.]+,\s*y:\s*[0-9.]+\}',
                rf'\g<1>{{x: {sx}, y: {sy}}}',
                content
            )
            content = re.sub(
                r'(- _MetallicGlossMap:\s+m_Texture:[^\n]+\n\s+m_Scale:\s*)\{x:\s*[0-9.]+,\s*y:\s*[0-9.]+\}',
                rf'\g<1>{{x: {sx}, y: {sy}}}',
                content
            )

    if content != orig_content:
        with open(path, "w", encoding="utf-8") as f:
            f.write(content)
        return True
    return False

updated_count = 0
for f in os.listdir(MAT_DIR):
    if f.endswith(".mat"):
        p = os.path.join(MAT_DIR, f)
        if update_material(p):
            updated_count += 1
            print(f"Updated: {f}")

print(f"\nTổng số file .mat đã cập nhật: {updated_count}")
