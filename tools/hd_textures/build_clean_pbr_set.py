"""
Script tích hợp toàn diện: Xử lý và nạp 100% texture PBR sạch, liền mép:
- Vân gỗ OakWood051 từ ambientCG CC0 (Wood051)
- Vân kim loại InoxSatin304 từ ambientCG CC0 (Metal009)
- Vân nhôm AlumAnodized029 từ ambientCG CC0 (Metal029)
- Vân đất hữu cơ SoilGarden037 từ ambientCG CC0 (Ground037)
- Vân gốm nung TerracottaClay002 từ ambientCG CC0 (Clay002)
- Tường sơn PlasterWall001: phẳng vi mô không sọc, không normal map sọc
- Sàn gạch TilesLab086: 1 viên gạch 60x60cm liền mép, ron viền 2%, khớp 100% lưới GachLatSan60
- Bàn TrespaTopLab & EpoxyResinDark: vi nhám nano mịn không sóng sin
- Độ bóng (Mask A) thiết lập trực tiếp chuẩn vật lý theo Bảng Giai đoạn 3
"""

import os
import cv2
import numpy as np
from PIL import Image

TARGET_DIR = "/Users/trantrong/VERALab_bemat/Phong thi nghiem ao/Assets/Lab/HD/Textures"
os.makedirs(TARGET_DIR, exist_ok=True)
SIZE = 1024

def save_mask(metallic, occlusion, smoothness, filename):
    m_val = np.clip(metallic * 255, 0, 255).astype(np.uint8)
    o_val = np.clip(occlusion * 255, 0, 255).astype(np.uint8)
    b_val = np.zeros((SIZE, SIZE), dtype=np.uint8)
    s_val = np.clip(smoothness * 255, 0, 255).astype(np.uint8)
    img = Image.fromarray(np.stack([m_val, o_val, b_val, s_val], axis=-1), mode="RGBA")
    path = os.path.join(TARGET_DIR, filename)
    img.save(path)
    print(f"Mask: {filename} -> Smoothness: {np.mean(smoothness):.2f}, Metallic: {np.mean(metallic):.2f}")

def save_image(img_arr, filename):
    path = os.path.join(TARGET_DIR, filename)
    if isinstance(img_arr, np.ndarray):
        if img_arr.dtype != np.uint8:
            img_arr = np.clip(img_arr * 255, 0, 255).astype(np.uint8)
        img = Image.fromarray(img_arr)
    else:
        img = img_arr
    img.save(path)
    print(f"Image: {filename}")

def height_to_normal(h, strength=2.0):
    pad = 2
    padded = np.pad(h, ((pad, pad), (pad, pad)), mode='wrap').astype(np.float32)
    dx = cv2.Sobel(padded, cv2.CV_32F, 1, 0, ksize=3)[pad:-pad, pad:-pad] * strength
    dy = cv2.Sobel(padded, cv2.CV_32F, 0, 1, ksize=3)[pad:-pad, pad:-pad] * strength
    dz = np.ones_like(dx)
    norm = np.sqrt(dx**2 + dy**2 + dz**2)
    nx = np.clip((dx / norm * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    ny = np.clip((dy / norm * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    nz = np.clip((dz / norm * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    return np.stack([nx, ny, nz], axis=-1)

rng = np.random.RandomState(42)

print("--- 1. TƯỜNG SƠN SATIN (PlasterWall001) ---")
# Albedo mịn đồng nhất, vi biến thiên mắt người nhìn tự nhiên nhưng không có sọc
albedo_plaster = np.clip(244 + rng.normal(0, 0.8, (SIZE, SIZE, 3)), 0, 255).astype(np.uint8)
save_image(albedo_plaster, "PlasterWall001_Albedo.png")
# Flat normal map (128, 128, 255) cho bề mặt sơn trơn
flat_normal = np.full((SIZE, SIZE, 3), [128, 128, 255], dtype=np.uint8)
save_image(flat_normal, "PlasterWall001_Normal.png")
save_image(flat_normal, "Normal_Plaster.png")
# Mask: Smoothness = 0.40 (tường sơn satin mờ nhẹ), Metallic = 0, Occlusion = 1.0
save_mask(np.zeros((SIZE, SIZE)), np.ones((SIZE, SIZE)), np.full((SIZE, SIZE), 0.40), "PlasterWall001_Mask.png")

print("\n--- 2. SÀN GẠCH MEN PHÒNG LAB (TilesLab086) ---")
# 1 viên gạch 60x60cm liền mép, ron viền 2% mép (khớp chính xác GachLatSan60 và tiling 16.67x12.5)
grout_w = 14
dist_y = np.minimum(np.arange(SIZE), SIZE - np.arange(SIZE))
dist_x = np.minimum(np.arange(SIZE), SIZE - np.arange(SIZE))
dist_edge = np.minimum(dist_y[:, None], dist_x[None, :])
is_grout = dist_edge < grout_w
grout_depth = np.clip((grout_w - dist_edge) / float(grout_w), 0.0, 1.0)

# Albedo
tile_alb = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
tile_alb[~is_grout] = [0.93, 0.94, 0.94]
tile_alb[is_grout] = [0.70, 0.71, 0.72]
tile_alb += rng.normal(0, 0.005, (SIZE, SIZE, 3))
save_image(tile_alb, "TilesLab086_Albedo.png")

# Normal: Mặt gạch phẳng men kính, mép ron bo tròn lún xuống
h_tile = -grout_depth * 0.4 + rng.normal(0, 0.001, (SIZE, SIZE))
tile_norm = height_to_normal(h_tile, strength=3.0)
save_image(tile_norm, "TilesLab086_Normal.png")
save_image(tile_norm, "Normal_TilesLab.png")

# Mask: Gạch bóng 0.70 (phản chiếu mềm đèn và cửa sổ), ron nhám 0.25, metallic 0
m_tile = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_tile = np.ones((SIZE, SIZE), dtype=np.float32)
occ_tile[is_grout] = 0.45 + (1.0 - grout_depth[is_grout]) * 0.55
smooth_tile = np.full((SIZE, SIZE), 0.70, dtype=np.float32)
smooth_tile[is_grout] = 0.25
save_mask(m_tile, occ_tile, smooth_tile, "TilesLab086_Mask.png")
save_image(np.stack([occ_tile, occ_tile, occ_tile], axis=-1), "AO_TilesLab.png")

print("\n--- 3. THÉP KHÔNG GỈ INOX 304 (InoxSatin304) ---")
# Dùng ảnh thật CC0 từ ambientCG Metal009
inox_col = cv2.imread("/tmp/Metal009_Color.jpg")
inox_norm = cv2.imread("/tmp/Metal009_Normal.jpg")
inox_col = cv2.resize(inox_col, (SIZE, SIZE))
inox_norm = cv2.resize(inox_norm, (SIZE, SIZE))
save_image(cv2.cvtColor(inox_col, cv2.COLOR_BGR2RGB), "InoxSatin304_Albedo.png")
save_image(cv2.cvtColor(inox_norm, cv2.COLOR_BGR2RGB), "InoxSatin304_Normal.png")
save_image(cv2.cvtColor(inox_norm, cv2.COLOR_BGR2RGB), "Normal_InoxSatin.png")
# Mask: Metallic = 0.96, Occlusion = 1.0, Smoothness = 0.68
save_mask(np.full((SIZE, SIZE), 0.96), np.ones((SIZE, SIZE)), np.full((SIZE, SIZE), 0.68), "InoxSatin304_Mask.png")

print("\n--- 4. MẶT BÀN TRESPA TOPLAB (TrespaTopLab) ---")
trespa_alb = np.clip(np.full((SIZE, SIZE, 3), [96, 102, 107]) + rng.normal(0, 1.2, (SIZE, SIZE, 3)), 0, 255).astype(np.uint8)
save_image(trespa_alb, "TrespaTopLab_Albedo.png")
h_trespa = rng.normal(0, 0.003, (SIZE, SIZE))
trespa_norm = height_to_normal(h_trespa, strength=0.8)
save_image(trespa_norm, "TrespaTopLab_Normal.png")
save_image(trespa_norm, "Normal_LabPhenolic.png")
# Mask: Metallic = 0, Occlusion = 1.0, Smoothness = 0.60
save_mask(np.zeros((SIZE, SIZE)), np.ones((SIZE, SIZE)), np.full((SIZE, SIZE), 0.60), "TrespaTopLab_Mask.png")

print("\n--- 5. MẶT BÀN EPOXY RESIN ĐEN BÓNG (EpoxyResinDark) ---")
epoxy_alb = np.full((SIZE, SIZE, 3), [30, 33, 36], dtype=np.uint8)
save_image(epoxy_alb, "EpoxyResinDark_Albedo.png")
h_epoxy = rng.normal(0, 0.001, (SIZE, SIZE))
epoxy_norm = height_to_normal(h_epoxy, strength=0.5)
save_image(epoxy_norm, "EpoxyResinDark_Normal.png")
save_image(epoxy_norm, "Normal_LabEpoxy.png")
# Mask: Metallic = 0, Occlusion = 1.0, Smoothness = 0.80
save_mask(np.zeros((SIZE, SIZE)), np.ones((SIZE, SIZE)), np.full((SIZE, SIZE), 0.80), "EpoxyResinDark_Mask.png")

print("\n--- 6. GỖ SỒI TỰ NHIÊN (OakWood051) ---")
# Dùng ảnh thật CC0 từ ambientCG Wood051
wood_col = cv2.imread("/tmp/Wood051_Color.jpg")
wood_norm = cv2.imread("/tmp/Wood051_Normal.jpg")
wood_col = cv2.resize(wood_col, (SIZE, SIZE))
wood_norm = cv2.resize(wood_norm, (SIZE, SIZE))
save_image(cv2.cvtColor(wood_col, cv2.COLOR_BGR2RGB), "OakWood051_Albedo.png")
save_image(cv2.cvtColor(wood_norm, cv2.COLOR_BGR2RGB), "OakWood051_Normal.png")
save_image(cv2.cvtColor(wood_norm, cv2.COLOR_BGR2RGB), "Normal_WoodGrain.png")
# Mask: Metallic = 0, Occlusion = 0.95, Smoothness = 0.50
save_mask(np.zeros((SIZE, SIZE)), np.full((SIZE, SIZE), 0.95), np.full((SIZE, SIZE), 0.50), "OakWood051_Mask.png")

print("\n--- 7. NHÔM ANODIZED (AlumAnodized029) ---")
alu_col = cv2.imread("/tmp/Metal029_Color.jpg")
alu_norm = cv2.imread("/tmp/Metal029_Normal.jpg")
alu_col = cv2.resize(alu_col, (SIZE, SIZE))
alu_norm = cv2.resize(alu_norm, (SIZE, SIZE))
save_image(cv2.cvtColor(alu_col, cv2.COLOR_BGR2RGB), "AlumAnodized029_Albedo.png")
save_image(cv2.cvtColor(alu_norm, cv2.COLOR_BGR2RGB), "AlumAnodized029_Normal.png")
save_image(cv2.cvtColor(alu_norm, cv2.COLOR_BGR2RGB), "Normal_AnodizedAlu.png")
# Mask: Metallic = 0.88, Occlusion = 1.0, Smoothness = 0.55
save_mask(np.full((SIZE, SIZE), 0.88), np.ones((SIZE, SIZE)), np.full((SIZE, SIZE), 0.55), "AlumAnodized029_Mask.png")

print("\n--- 8. ĐẤT HỮU CƠ (SoilGarden037) ---")
soil_norm = cv2.imread("/tmp/Ground037_Normal.jpg")
soil_norm = cv2.resize(soil_norm, (SIZE, SIZE))
save_image(cv2.cvtColor(soil_norm, cv2.COLOR_BGR2RGB), "SoilGarden037_Normal.png")
soil_alb = np.clip(np.full((SIZE, SIZE, 3), [61, 46, 33]) + rng.normal(0, 3.0, (SIZE, SIZE, 3)), 0, 255).astype(np.uint8)
save_image(soil_alb, "SoilGarden037_Albedo.png")
save_mask(np.zeros((SIZE, SIZE)), np.full((SIZE, SIZE), 0.85), np.full((SIZE, SIZE), 0.15), "SoilGarden037_Mask.png")

print("\n--- 9. GỐM NUNG TERRACOTTA (TerracottaClay002) ---")
terra_norm = cv2.imread("/tmp/Clay002_Normal.jpg")
terra_norm = cv2.resize(terra_norm, (SIZE, SIZE))
save_image(cv2.cvtColor(terra_norm, cv2.COLOR_BGR2RGB), "TerracottaClay002_Normal.png")
terra_alb = np.clip(np.full((SIZE, SIZE, 3), [184, 102, 71]) + rng.normal(0, 2.5, (SIZE, SIZE, 3)), 0, 255).astype(np.uint8)
save_image(terra_alb, "TerracottaClay002_Albedo.png")
save_mask(np.zeros((SIZE, SIZE)), np.ones((SIZE, SIZE)), np.full((SIZE, SIZE), 0.25), "TerracottaClay002_Mask.png")

print("\n=== HOÀN TẤT SINH TOÀN BỘ VÂN PBR SẠCH CHUẨN CC0 ===")
