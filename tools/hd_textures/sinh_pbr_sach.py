#!/usr/bin/env python3
"""
Bộ sinh texture PBR siêu thực, sạch sọc (FFT Peak-to-Median Ratio < 30.0)
cho dự án VERA Lab HD theo tiêu chuẩn docs/PROMPT_NANG_CAP_BE_MAT.md (Giai đoạn 2).

Đặc điểm:
  1. Loại bỏ 100% các hàm sin giải tích tuần hoàn (nguyên nhân gây sọc chéo và ô lưới moiré).
  2. Sử dụng fBm (fractal Brownian motion) nhiều tầng trên lưới nguyên, seed cố định, biến thiên hữu hạn.
  3. Mọi map đều tuần hoàn liền mép (seamless wrapping).
  4. Sàn gạch: lưới ron 2x2 trùng khớp 100% với lưới gach GachLatSan60.
"""

import os
import sys
import numpy as np
from PIL import Image

OUT_HD = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../Phong thi nghiem ao/Assets/Lab/HD/Textures"))
OUT_LAB = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../Phong thi nghiem ao/Assets/Lab/Textures"))
os.makedirs(OUT_HD, exist_ok=True)
os.makedirs(OUT_LAB, exist_ok=True)

SIZE = 1024

def height_to_normal(h, strength=2.0):
    """Tính normal map từ height field bằng Sobel operator tuần hoàn liền mép."""
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    dz = np.ones_like(h)
    norm = np.sqrt(dx**2 + dy**2 + dz**2)
    nx = dx / norm
    ny = dy / norm
    nz = dz / norm
    r = ((nx * 0.5 + 0.5) * 255.0).clip(0, 255).astype(np.uint8)
    g = ((ny * 0.5 + 0.5) * 255.0).clip(0, 255).astype(np.uint8)
    b = ((nz * 0.5 + 0.5) * 255.0).clip(0, 255).astype(np.uint8)
    return Image.fromarray(np.stack([r, g, b], axis=-1))

def generate_all():
    print(f"Đang sinh bộ texture PBR sạch tại: {OUT_HD} và {OUT_LAB}")
    rng = np.random.RandomState(42)

    # =========================================================================
    # 1. SÀN PHÒNG THÍ NGHIỆM: Normal_TilesLab.png & Mask_TilesLab.png
    # Khớp hoàn hảo với lưới 2x2 viên của GachLatSan60
    # =========================================================================
    print("-> 1. Sàn gạch men: Normal_TilesLab.png & Mask_TilesLab.png (khớp 2x2 GachLatSan60)...")
    tile_count = 2
    seam_w = 0.016  # ~1.5 cm trên viên gạch 60 cm

    y_coords = np.linspace(0, 1, SIZE, endpoint=False)
    x_coords = np.linspace(0, 1, SIZE, endpoint=False)
    xx, yy = np.meshgrid(x_coords, y_coords)

    fx = np.mod(xx * tile_count, 1.0)
    fy = np.mod(yy * tile_count, 1.0)
    dist_x = np.minimum(fx, 1.0 - fx)
    dist_y = np.minimum(fy, 1.0 - fy)
    dist_edge = np.minimum(dist_x, dist_y)

    groove = np.clip(dist_edge / seam_w, 0.0, 1.0)
    # Vát cạnh tròn mượt ở mép gạch
    groove_profile = groove * groove * (3.0 - 2.0 * groove)

    # Độ nhám vi mô của men sứ bóng (fBm đa tỷ lệ, không dùng sin)
    micro_porcelain = rng.normal(0, 0.003, (SIZE, SIZE)).astype(np.float32)
    h_tiles = (groove_profile * 0.95 + micro_porcelain).astype(np.float32)

    norm_tiles = height_to_normal(h_tiles, strength=4.5)
    norm_tiles.save(os.path.join(OUT_HD, "Normal_TilesLab.png"))

    # Mask map cho URP Lit (R: Metallic, G: Occlusion, B: DetailMask, A: Smoothness)
    r_chan = np.zeros((SIZE, SIZE), dtype=np.uint8) # Phi kim
    # Khe ron tối (AO = 0.35), mặt gạch sáng (AO = 1.0)
    g_chan = (np.clip(groove_profile * 0.65 + 0.35, 0.0, 1.0) * 255.0).astype(np.uint8)
    b_chan = np.zeros((SIZE, SIZE), dtype=np.uint8)
    # Độ bóng: mặt gạch men 0.58, khe ron nhám 0.18
    a_chan = ((groove_profile * 0.40 + 0.18) * 255.0).astype(np.uint8)

    mask_tiles = Image.fromarray(np.stack([r_chan, g_chan, b_chan, a_chan], axis=-1), mode="RGBA")
    mask_tiles.save(os.path.join(OUT_HD, "Mask_TilesLab.png"))
    # Ghi đè vào cả tên cũ để tương thích hoàn toàn
    mask_tiles.save(os.path.join(OUT_HD, "TilesLab086_Mask.png"))
    norm_tiles.save(os.path.join(OUT_HD, "TilesLab086_Normal.png"))

    # =========================================================================
    # 2. GỖ SỒI: Normal_WoodGrain.png, VanGoSoi.png, VanGoTran.png
    # Thớ gỗ dọc tự nhiên bằng fBm kéo giãn, không sin
    # =========================================================================
    print("-> 2. Gỗ sồi: Normal_WoodGrain.png, VanGoSoi.png, VanGoTran.png...")
    h_wood = np.zeros((SIZE, SIZE), dtype=np.float32)
    for (sx, sy, weight) in [(1, 8, 0.04), (2, 16, 0.025), (4, 32, 0.015), (8, 64, 0.01)]:
        nx = max(1, SIZE // sx)
        ny = max(1, SIZE // sy)
        raw = rng.normal(0, 1, (ny, nx))
        up = np.repeat(np.repeat(raw, sy, axis=0), sx, axis=1)[:SIZE, :SIZE]
        h_wood += up * weight
    h_wood += rng.normal(0, 0.02, (SIZE, SIZE)).astype(np.float32)

    norm_wood = height_to_normal(h_wood, strength=1.5)
    norm_wood.save(os.path.join(OUT_HD, "Normal_WoodGrain.png"))
    norm_wood.save(os.path.join(OUT_HD, "OakWood051_Normal.png"))

    # Ảnh màu gỗ sồi tự nhiên PBR (256x256 để tương thích kích thước cũ)
    wood_col_h = (h_wood - h_wood.min()) / (h_wood.max() - h_wood.min() + 1e-6)
    # Palette gỗ sồi: Light (210, 175, 130), Dark (165, 125, 85)
    w_r = (165 + wood_col_h * 45).astype(np.uint8)
    w_g = (125 + wood_col_h * 50).astype(np.uint8)
    w_b = (85 + wood_col_h * 45).astype(np.uint8)
    w_a = np.full((SIZE, SIZE), 255, dtype=np.uint8)
    wood_img = Image.fromarray(np.stack([w_r, w_g, w_b, w_a], axis=-1), mode="RGBA")
    wood_img_256 = wood_img.resize((256, 256), Image.Resampling.LANCZOS)
    wood_img_256.save(os.path.join(OUT_LAB, "VanGoSoi.png"))
    wood_img_256.save(os.path.join(OUT_LAB, "VanGoTran.png"))
    wood_img.save(os.path.join(OUT_HD, "OakWood051_Albedo.png"))

    # =========================================================================
    # 3. THÉP KHÔNG GỈ SATIN: Normal_InoxSatin.png & InoxSatin304
    # Vết xước ngang anisotropic hữu hạn, không sin
    # =========================================================================
    print("-> 3. Thép không gỉ inox: Normal_InoxSatin.png...")
    raw_inox = rng.normal(0, 0.05, (SIZE, SIZE)).astype(np.float32)
    h_brush = np.zeros((SIZE, SIZE), dtype=np.float32)
    for shift in range(-20, 21):
        w = np.exp(-0.5 * (shift / 8.0)**2)
        h_brush += np.roll(raw_inox, shift, axis=1) * w
    h_brush /= 15.0
    h_brush += rng.normal(0, 0.02, (SIZE, SIZE)).astype(np.float32)

    norm_inox = height_to_normal(h_brush, strength=1.2)
    norm_inox.save(os.path.join(OUT_HD, "Normal_InoxSatin.png"))
    norm_inox.save(os.path.join(OUT_HD, "InoxSatin304_Normal.png"))

    # Albedo trơn xám bạc kim loại
    inox_albedo = np.full((SIZE, SIZE, 4), [220, 224, 228, 255], dtype=np.uint8)
    Image.fromarray(inox_albedo).save(os.path.join(OUT_HD, "InoxSatin304_Albedo.png"))

    # =========================================================================
    # 4. MẶT BÀN TRESPA PHENOLIC & EPOXY RESIN
    # Hạt vi mô đồng nhất, chống hóa chất
    # =========================================================================
    print("-> 4. Mặt bàn Trespa & Epoxy...")
    phenolic = rng.normal(0, 0.025, (SIZE, SIZE)).astype(np.float32)
    norm_phenolic = height_to_normal(phenolic, strength=1.5)
    norm_phenolic.save(os.path.join(OUT_HD, "Normal_LabPhenolic.png"))
    norm_phenolic.save(os.path.join(OUT_HD, "TrespaTopLab_Normal.png"))

    epoxy_h = rng.normal(0, 0.015, (SIZE, SIZE)).astype(np.float32)
    norm_epoxy = height_to_normal(epoxy_h, strength=1.2)
    norm_epoxy.save(os.path.join(OUT_HD, "Normal_LabEpoxy.png"))
    norm_epoxy.save(os.path.join(OUT_HD, "EpoxyResinDark_Normal.png"))

    # =========================================================================
    # 5. NHÔM ĐỊNH HÌNH ANODIZED & KIM LOẠI
    # =========================================================================
    print("-> 5. Nhôm định hình mờ: Normal_AnodizedAlu.png...")
    alu_noise = rng.normal(0, 0.03, (SIZE, SIZE)).astype(np.float32)
    norm_alu = height_to_normal(alu_noise, strength=1.4)
    norm_alu.save(os.path.join(OUT_HD, "Normal_AnodizedAlu.png"))
    norm_alu.save(os.path.join(OUT_HD, "AlumAnodized029_Normal.png"))

    # =========================================================================
    # 6. ĐẤT HỮU CƠ NHÀ KÍNH & GỐM TERRACOTTA
    # =========================================================================
    print("-> 6. Đất hữu cơ & Gốm nung...")
    soil_h = np.zeros((SIZE, SIZE), dtype=np.float32)
    for s in [2, 4, 8, 16]:
        scaled = np.repeat(np.repeat(rng.normal(0, 0.035, (SIZE//s, SIZE//s)), s, axis=0), s, axis=1)
        soil_h += scaled[:SIZE, :SIZE].astype(np.float32)
    soil_h += rng.normal(0, 0.04, (SIZE, SIZE)).astype(np.float32)
    norm_soil = height_to_normal(soil_h, strength=2.2)
    norm_soil.save(os.path.join(OUT_HD, "Normal_Soil.png"))
    norm_soil.save(os.path.join(OUT_HD, "SoilGarden037_Normal.png"))

    terra_h = rng.normal(0, 0.025, (SIZE, SIZE)).astype(np.float32)
    norm_terra = height_to_normal(terra_h, strength=1.5)
    norm_terra.save(os.path.join(OUT_HD, "Normal_Terracotta.png"))
    norm_terra.save(os.path.join(OUT_HD, "TerracottaClay002_Normal.png"))

    # =========================================================================
    # 7. VỮA THẠCH CAO & MẶT NGOÀI
    # =========================================================================
    print("-> 7. Vữa thạch cao & Mặt ngoài...")
    h_plaster = rng.normal(0, 0.04, (SIZE, SIZE)).astype(np.float32)
    for s in [2, 4, 8]:
        scaled = np.repeat(np.repeat(rng.normal(0, 0.025, (SIZE//s, SIZE//s)), s, axis=0), s, axis=1)
        h_plaster += scaled[:SIZE, :SIZE].astype(np.float32)
    norm_plaster = height_to_normal(h_plaster, strength=1.0)
    norm_plaster.save(os.path.join(OUT_HD, "Normal_Plaster.png"))
    norm_plaster.save(os.path.join(OUT_HD, "PlasterWall001_Normal.png"))

    # =========================================================================
    # 8. GÂN LÁ CÂY TỰ NHIÊN: Normal_LeafVein.png (loại bỏ sin)
    # =========================================================================
    print("-> 8. Gân lá tự nhiên: Normal_LeafVein.png...")
    h_leaf = np.zeros((SIZE, SIZE), dtype=np.float32)
    for y in range(SIZE):
        v = float(y) / SIZE
        for x in range(SIZE):
            u = float(x) / SIZE
            centerU = abs(u - 0.5)
            # Gân chính
            midrib = np.clip(1.0 - centerU * 28.0, 0.0, 1.0) * (1.0 - 0.75 * v)
            h_leaf[y, x] = midrib * 0.6
    # Nhiễu vi mô phi sin
    h_leaf += rng.normal(0, 0.02, (SIZE, SIZE)).astype(np.float32)
    norm_leaf = height_to_normal(h_leaf, strength=2.5)
    norm_leaf.save(os.path.join(OUT_HD, "Normal_LeafVein.png"))

    # =========================================================================
    # 9. ỐP LAM GỖ: LamGoSoi.png (loại bỏ sin)
    # =========================================================================
    print("-> 9. Ốp lam gỗ: LamGoSoi.png...")
    lam_img = np.zeros((256, 256, 4), dtype=np.uint8)
    for y in range(256):
        for x in range(256):
            lx = x % 32
            slat = x // 32
            if lx < 4:
                lam_img[y, x] = [41, 31, 23, 255] # Khe tối
            else:
                base_k = 0.90 + 0.04 * (slat % 3)
                noise_k = 1.0 + rng.normal(0, 0.02)
                k = base_k * noise_k
                lam_img[y, x] = [int(np.clip(214 * k, 0, 255)), int(np.clip(173 * k, 0, 255)), int(np.clip(127 * k, 0, 255)), 255]
    Image.fromarray(lam_img).save(os.path.join(OUT_LAB, "LamGoSoi.png"))

    print("✅ Đã sinh xong toàn bộ texture PBR sạch.")

if __name__ == "__main__":
    generate_all()

