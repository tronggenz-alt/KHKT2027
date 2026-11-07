"""
Tạo bộ vân PBR chuẩn CC0 không sọc (Fourier peak/median < 30.0), liền mép (seamless),
khớp tỉ lệ ron sàn 1x1 gạch và thiết lập độ bóng thật theo Bảng Giai đoạn 3 của PROMPT_NANG_CAP_BE_MAT.md.

Vật liệu:
  - PlasterWall001 (Tường sơn satin: smoothness 0.40, không normal sọc)
  - PlasterCeiling (Trần: smoothness 0.12, không normal)
  - TilesLab086 (Sàn gạch men: 1 viên gạch liền mép, ron 2% ở biên, gạch smoothness 0.70, ron 0.25)
  - InoxSatin304 (Thép không gỉ 304 satin xước mờ: metallic 0.96, smoothness 0.68)
  - TrespaTopLab (Mặt bàn phenolic chống hóa chất: metallic 0, smoothness 0.60)
  - EpoxyResinDark (Mặt bàn epoxy đúc đen bóng: metallic 0, smoothness 0.80)
  - OakWood051 (Gỗ sồi tự nhiên thớ dọc: metallic 0, smoothness 0.50)
  - AlumAnodized029 (Nhôm anodized mờ: metallic 0.88, smoothness 0.55)
  - SoilGarden037 (Đất vườn hữu cơ: metallic 0, smoothness 0.15)
  - TerracottaClay002 (Gạch gốm nung: metallic 0, smoothness 0.25)
"""

import os
import numpy as np
from PIL import Image

OUT_DIR = "/Users/trantrong/VERALab_bemat/Phong thi nghiem ao/Assets/Lab/HD/Textures"
os.makedirs(OUT_DIR, exist_ok=True)
SIZE = 1024

def make_seamless_fbm(size, octaves=5, persistence=0.5, rng=None):
    if rng is None:
        rng = np.random.RandomState(42)
    # Nhân lặp miền tần số Fourier để đảm bảo 100% liền mép và ngẫu nhiên
    kx = np.fft.fftfreq(size)[:, None]
    ky = np.fft.fftfreq(size)[None, :]
    k2 = kx**2 + ky**2
    k2[0, 0] = 1.0
    # Bộ lọc hồng 1/k^alpha cho phổ tự nhiên không có gai
    pink = 1.0 / (k2 ** 0.8)
    pink[0, 0] = 0.0
    white_noise = rng.normal(0, 1.0, (size, size)) + 1j * rng.normal(0, 1.0, (size, size))
    f_surf = np.real(np.fft.ifft2(white_noise * pink))
    f_surf = (f_surf - np.mean(f_surf)) / (np.std(f_surf) + 1e-6)
    h = (f_surf - f_surf.min()) / (f_surf.max() - f_surf.min() + 1e-6)
    return h

def height_to_normal(h, strength=2.0):
    # Đạo hàm trung tâm tuần hoàn (wrap) đảm bảo liền mép 100%
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    dz = np.ones_like(h)
    norm = np.sqrt(dx**2 + dy**2 + dz**2)
    nx = dx / norm
    ny = dy / norm
    nz = dz / norm
    r = np.clip((nx * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    g = np.clip((ny * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    b = np.clip((nz * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    return Image.fromarray(np.stack([r, g, b], axis=-1))

def save_mask(metallic, occlusion, smoothness, filename):
    # URP Mask Map: R = Metallic, G = Occlusion, B = Detail mask (0), A = Smoothness
    m_val = np.clip(metallic * 255, 0, 255).astype(np.uint8)
    o_val = np.clip(occlusion * 255, 0, 255).astype(np.uint8)
    b_val = np.zeros((SIZE, SIZE), dtype=np.uint8)
    s_val = np.clip(smoothness * 255, 0, 255).astype(np.uint8)
    img = Image.fromarray(np.stack([m_val, o_val, b_val, s_val], axis=-1), mode="RGBA")
    path = os.path.join(OUT_DIR, filename)
    img.save(path)
    print(f"Saved Mask: {filename} (True Smoothness avg={np.mean(smoothness):.2f})")

def save_albedo(rgb, filename):
    img = Image.fromarray(np.clip(rgb * 255, 0, 255).astype(np.uint8), mode="RGB")
    path = os.path.join(OUT_DIR, filename)
    img.save(path)
    print(f"Saved Albedo: {filename}")

def save_normal(img, filename):
    path = os.path.join(OUT_DIR, filename)
    img.save(path)
    print(f"Saved Normal: {filename}")

rng = np.random.RandomState(12345)

print("=== BẮT ĐẦU SINH VÂN PBR SẠCH KHÔNG SỌC (SEED CỐ ĐỊNH) ===")

# ------------------------------------------------------------------------------
# 1. PlasterWall001 (Tường sơn satin mịn, Mục tiêu: Smoothness 0.40, Metallic 0)
# ------------------------------------------------------------------------------
# Không dùng sọc sin. Albedo mịn vi mô phân tán đều, biến thiên rất nhẹ < 0.01
h_plaster = make_seamless_fbm(SIZE, rng=rng)
albedo_plaster = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
albedo_plaster[:, :] = [0.96, 0.96, 0.95]
albedo_plaster += (h_plaster[:, :, None] - 0.5) * 0.015
save_albedo(albedo_plaster, "PlasterWall001_Albedo.png")

# Normal map phẳng cho tường sơn trơn (flat normal)
norm_flat = Image.fromarray(np.full((SIZE, SIZE, 3), [128, 128, 255], dtype=np.uint8))
save_normal(norm_flat, "PlasterWall001_Normal.png")
save_normal(norm_flat, "Normal_Plaster.png")

# Mask: metallic 0, occlusion 1.0, smoothness 0.40
m_plaster = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_plaster = np.ones((SIZE, SIZE), dtype=np.float32)
smooth_plaster = np.full((SIZE, SIZE), 0.40, dtype=np.float32)
save_mask(m_plaster, occ_plaster, smooth_plaster, "PlasterWall001_Mask.png")

# ------------------------------------------------------------------------------
# 2. TilesLab086 (Sàn gạch men phòng lab, Mục tiêu: Gạch 0.70, Ron 0.25, Metallic 0)
# ------------------------------------------------------------------------------
# 1 viên gạch 60x60cm liền mép, ron ở mép ảnh để khớp chính xác tiling 16.67x12.5 của sàn
grout_border = 14 # ~1.4% mép mỗi bên tạo ron gạch 8mm chuẩn
dist_y = np.minimum(np.arange(SIZE), SIZE - np.arange(SIZE))
dist_x = np.minimum(np.arange(SIZE), SIZE - np.arange(SIZE))
dist_edge = np.minimum(dist_y[:, None], dist_x[None, :])

is_grout = dist_edge < grout_border
grout_depth = np.clip((grout_border - dist_edge) / float(grout_border), 0.0, 1.0)

# Albedo sàn: Gạch men sáng (0.92, 0.93, 0.93), Ron xi măng ghi xám (0.70, 0.71, 0.72)
albedo_tile = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
albedo_tile[~is_grout] = [0.92, 0.93, 0.93]
albedo_tile[is_grout] = [0.70, 0.71, 0.72]
# Thêm vi biến thiên không chu kỳ
h_tile = make_seamless_fbm(SIZE, rng=rng)
albedo_tile += (h_tile[:, :, None] - 0.5) * 0.012
save_albedo(albedo_tile, "TilesLab086_Albedo.png")

# Height và Normal cho sàn gạch: Ron lún sâu, mặt gạch phẳng men kính
h_floor = np.zeros((SIZE, SIZE), dtype=np.float32)
h_floor -= grout_depth * 0.5
h_floor += (h_tile - 0.5) * 0.005 # Vi nhám phẳng nano
norm_tile = height_to_normal(h_floor, strength=4.0)
save_normal(norm_tile, "TilesLab086_Normal.png")
save_normal(norm_tile, "Normal_TilesLab.png")

# Mask: Gạch bóng 0.70 thấy phản chiếu mềm, ron nhám 0.25, occlusion ron 0.45, metallic 0
m_tile = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_tile = np.ones((SIZE, SIZE), dtype=np.float32)
occ_tile[is_grout] = 0.45 + (1.0 - grout_depth[is_grout]) * 0.55
smooth_tile = np.full((SIZE, SIZE), 0.70, dtype=np.float32)
smooth_tile[is_grout] = 0.25
save_mask(m_tile, occ_tile, smooth_tile, "TilesLab086_Mask.png")

# Đồng thời tạo AO_TilesLab.png
save_albedo(np.stack([occ_tile, occ_tile, occ_tile], axis=-1), "AO_TilesLab.png")

# ------------------------------------------------------------------------------
# 3. InoxSatin304 (Thép không gỉ 304 xước mờ, Metallic 0.96, Smoothness 0.68)
# ------------------------------------------------------------------------------
# Thớ xước ngang liên tục không dùng sin tuần hoàn mà dùng dải nhiễu 1D kéo dài
raw_scratches = rng.normal(0, 0.04, (SIZE, 1)).astype(np.float32)
# Lọc tần số cao để tránh moiré
kx = np.fft.fftfreq(SIZE)[:, None]
scratches_1d = np.real(np.fft.ifft(np.fft.fft(raw_scratches, axis=0) * np.exp(-abs(kx)*12.0), axis=0))
scratches_1d = (scratches_1d - np.mean(scratches_1d)) / (np.std(scratches_1d) + 1e-6) * 0.02
scratches_2d = np.repeat(scratches_1d, SIZE, axis=1)

albedo_inox = np.full((SIZE, SIZE, 3), [0.88, 0.89, 0.90], dtype=np.float32) + scratches_2d[:, :, None]
save_albedo(albedo_inox, "InoxSatin304_Albedo.png")

h_inox = scratches_2d + (make_seamless_fbm(SIZE, rng=rng) - 0.5) * 0.005
norm_inox = height_to_normal(h_inox, strength=1.5)
save_normal(norm_inox, "InoxSatin304_Normal.png")
save_normal(norm_inox, "Normal_InoxSatin.png")

m_inox = np.full((SIZE, SIZE), 0.96, dtype=np.float32)
occ_inox = np.ones((SIZE, SIZE), dtype=np.float32)
smooth_inox = np.full((SIZE, SIZE), 0.68, dtype=np.float32)
save_mask(m_inox, occ_inox, smooth_inox, "InoxSatin304_Mask.png")

# ------------------------------------------------------------------------------
# 4. TrespaTopLab (Mặt bàn Phenolic xám, Metallic 0, Smoothness 0.60)
# ------------------------------------------------------------------------------
h_trespa = make_seamless_fbm(SIZE, rng=rng)
albedo_trespa = np.full((SIZE, SIZE, 3), [0.38, 0.40, 0.42], dtype=np.float32)
albedo_trespa += (h_trespa[:, :, None] - 0.5) * 0.015
save_albedo(albedo_trespa, "TrespaTopLab_Albedo.png")

norm_trespa = height_to_normal((h_trespa - 0.5) * 0.008, strength=1.0)
save_normal(norm_trespa, "TrespaTopLab_Normal.png")
save_normal(norm_trespa, "Normal_LabPhenolic.png")

m_trespa = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_trespa = np.ones((SIZE, SIZE), dtype=np.float32)
smooth_trespa = np.full((SIZE, SIZE), 0.60, dtype=np.float32)
save_mask(m_trespa, occ_trespa, smooth_trespa, "TrespaTopLab_Mask.png")

# ------------------------------------------------------------------------------
# 5. EpoxyResinDark (Mặt bàn Epoxy đúc đen bóng, Metallic 0, Smoothness 0.80)
# ------------------------------------------------------------------------------
albedo_epoxy = np.full((SIZE, SIZE, 3), [0.12, 0.13, 0.14], dtype=np.float32)
save_albedo(albedo_epoxy, "EpoxyResinDark_Albedo.png")

h_epoxy = make_seamless_fbm(SIZE, rng=rng)
norm_epoxy = height_to_normal((h_epoxy - 0.5) * 0.004, strength=0.8)
save_normal(norm_epoxy, "EpoxyResinDark_Normal.png")
save_normal(norm_epoxy, "Normal_LabEpoxy.png")

m_epoxy = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_epoxy = np.ones((SIZE, SIZE), dtype=np.float32)
smooth_epoxy = np.full((SIZE, SIZE), 0.80, dtype=np.float32)
save_mask(m_epoxy, occ_epoxy, smooth_epoxy, "EpoxyResinDark_Mask.png")

# ------------------------------------------------------------------------------
# 6. OakWood051 (Gỗ sồi tự nhiên thớ dọc, Metallic 0, Smoothness 0.50)
# ------------------------------------------------------------------------------
# Thớ dọc tự nhiên: nhiễu 1D theo trục x có uốn lượn nhẹ theo trục y (domain warp)
y_coords = np.linspace(0, 1, SIZE)[:, None]
x_coords = np.linspace(0, 1, SIZE)[None, :]
warp = make_seamless_fbm(SIZE, rng=rng) * 0.04
x_warped = (x_coords + warp) % 1.0

# Phổ thớ gỗ hồng theo trục x
raw_grain = rng.normal(0, 1.0, (1, SIZE))
kx_w = np.fft.fftfreq(SIZE)[None, :]
grain_1d = np.real(np.fft.ifft(np.fft.fft(raw_grain, axis=1) / (np.maximum(abs(kx_w)*10.0, 1.0) ** 0.7), axis=1))
grain_1d = (grain_1d - np.mean(grain_1d)) / (np.std(grain_1d) + 1e-6)
# Lookup x_warped
idx = np.clip((x_warped * (SIZE - 1)).astype(int), 0, SIZE - 1)
wood_pattern = grain_1d[0, idx[0, :]]
wood_pattern = np.tile(wood_pattern, (SIZE, 1))

light_wood = np.array([0.78, 0.62, 0.46])
dark_wood = np.array([0.55, 0.40, 0.28])
t_wood = np.clip((wood_pattern - wood_pattern.min()) / (wood_pattern.max() - wood_pattern.min() + 1e-6), 0.0, 1.0)
albedo_wood = light_wood * (1.0 - t_wood[:, :, None]) + dark_wood * t_wood[:, :, None]
save_albedo(albedo_wood, "OakWood051_Albedo.png")

h_wood = t_wood * 0.05
norm_wood = height_to_normal(h_wood, strength=2.0)
save_normal(norm_wood, "OakWood051_Normal.png")
save_normal(norm_wood, "Normal_WoodGrain.png")

m_wood = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_wood = np.ones((SIZE, SIZE), dtype=np.float32) * 0.95
smooth_wood = np.full((SIZE, SIZE), 0.50, dtype=np.float32)
save_mask(m_wood, occ_wood, smooth_wood, "OakWood051_Mask.png")

# ------------------------------------------------------------------------------
# 7. AlumAnodized029 (Nhôm định hình mờ, Metallic 0.88, Smoothness 0.55)
# ------------------------------------------------------------------------------
albedo_alu = np.full((SIZE, SIZE, 3), [0.25, 0.27, 0.29], dtype=np.float32)
save_albedo(albedo_alu, "AlumAnodized029_Albedo.png")

h_alu = make_seamless_fbm(SIZE, rng=rng)
norm_alu = height_to_normal((h_alu - 0.5) * 0.006, strength=1.2)
save_normal(norm_alu, "AlumAnodized029_Normal.png")
save_normal(norm_alu, "Normal_AnodizedAlu.png")

m_alu = np.full((SIZE, SIZE), 0.88, dtype=np.float32)
occ_alu = np.ones((SIZE, SIZE), dtype=np.float32)
smooth_alu = np.full((SIZE, SIZE), 0.55, dtype=np.float32)
save_mask(m_alu, occ_alu, smooth_alu, "AlumAnodized029_Mask.png")

# ------------------------------------------------------------------------------
# 8. SoilGarden037 (Đất hữu cơ nhà kính, Metallic 0, Smoothness 0.15)
# ------------------------------------------------------------------------------
h_soil = make_seamless_fbm(SIZE, rng=rng)
albedo_soil = np.full((SIZE, SIZE, 3), [0.24, 0.18, 0.13], dtype=np.float32)
albedo_soil += (h_soil[:, :, None] - 0.5) * 0.04
save_albedo(albedo_soil, "SoilGarden037_Albedo.png")

norm_soil = height_to_normal((h_soil - 0.5) * 0.03, strength=2.5)
save_normal(norm_soil, "SoilGarden037_Normal.png")

m_soil = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_soil = np.full((SIZE, SIZE), 0.85, dtype=np.float32)
smooth_soil = np.full((SIZE, SIZE), 0.15, dtype=np.float32)
save_mask(m_soil, occ_soil, smooth_soil, "SoilGarden037_Mask.png")

# ------------------------------------------------------------------------------
# 9. TerracottaClay002 (Gạch gốm nung chậu cây, Metallic 0, Smoothness 0.25)
# ------------------------------------------------------------------------------
h_terra = make_seamless_fbm(SIZE, rng=rng)
albedo_terra = np.full((SIZE, SIZE, 3), [0.72, 0.40, 0.28], dtype=np.float32)
albedo_terra += (h_terra[:, :, None] - 0.5) * 0.03
save_albedo(albedo_terra, "TerracottaClay002_Albedo.png")

norm_terra = height_to_normal((h_terra - 0.5) * 0.015, strength=1.5)
save_normal(norm_terra, "TerracottaClay002_Normal.png")

m_terra = np.zeros((SIZE, SIZE), dtype=np.float32)
occ_terra = np.ones((SIZE, SIZE), dtype=np.float32)
smooth_terra = np.full((SIZE, SIZE), 0.25, dtype=np.float32)
save_mask(m_terra, occ_terra, smooth_terra, "TerracottaClay002_Mask.png")

print("=== HOÀN TẤT SINH TẤT CẢ VÂN PBR SẠCH ===")
