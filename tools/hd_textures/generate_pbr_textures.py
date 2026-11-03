import os
import numpy as np
from PIL import Image

OUT_DIR = "/Users/trantrong/VERALabHD/Phong thi nghiem ao/Assets/Lab/HD/Textures"
os.makedirs(OUT_DIR, exist_ok=True)
SIZE = 1024

def height_to_normal(h, strength=2.0):
    # Central difference with wrapping (seamless)
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    dz = np.ones_like(h)
    norm = np.sqrt(dx**2 + dy**2 + dz**2)
    nx = dx / norm
    ny = dy / norm
    nz = dz / norm
    r = ((nx * 0.5 + 0.5) * 255).astype(np.uint8)
    g = ((ny * 0.5 + 0.5) * 255).astype(np.uint8)
    b = ((nz * 0.5 + 0.5) * 255).astype(np.uint8)
    return Image.fromarray(np.stack([r, g, b], axis=-1))

print("Generating 1024x1024 PBR Normal and AO textures...")

# 1. Normal_TilesLab: 60x60 cm floor tile grid with grout recess and smooth porcelain micro-texture
grid = np.zeros((SIZE, SIZE), dtype=np.float32)
tile_size = 256  # 4x4 tiles in 1024 texture
grout_width = 8
for i in range(SIZE):
    for j in range(SIZE):
        dist_x = min(i % tile_size, tile_size - (i % tile_size))
        dist_y = min(j % tile_size, tile_size - (j % tile_size))
        dist_edge = min(dist_x, dist_y)
        if dist_edge < grout_width:
            grid[i, j] = -(grout_width - dist_edge) / float(grout_width) * 0.8
        else:
            grid[i, j] = 0.0

rng = np.random.RandomState(42)
micro_porcelain = rng.normal(0, 0.02, (SIZE, SIZE)).astype(np.float32)
norm_tiles = height_to_normal(grid + micro_porcelain, strength=3.0)
norm_tiles.save(os.path.join(OUT_DIR, "Normal_TilesLab.png"))

# AO_TilesLab: darker in grout recesses
ao_tiles = np.clip(1.0 + grid * 0.6, 0.2, 1.0)
Image.fromarray((ao_tiles * 255).astype(np.uint8)).save(os.path.join(OUT_DIR, "AO_TilesLab.png"))

# 2. Normal_TilesChem: anti-slip raised circular studs
h_studs = np.zeros((SIZE, SIZE), dtype=np.float32)
spacing = 64
radius = 18
for cx in range(spacing // 2, SIZE, spacing):
    for cy in range(spacing // 2, SIZE, spacing):
        y, x = np.ogrid[:SIZE, :SIZE]
        dist = np.sqrt((x - cx)**2 + (y - cy)**2)
        mask = dist < radius
        h_studs[mask] = np.maximum(h_studs[mask], np.cos((dist[mask] / radius) * (np.pi / 2)) * 0.5)
norm_chem = height_to_normal(h_studs + micro_porcelain * 1.5, strength=2.5)
norm_chem.save(os.path.join(OUT_DIR, "Normal_TilesChem.png"))

# 3. Normal_Plaster: fine interior architectural plaster
h_plaster = rng.normal(0, 0.05, (SIZE, SIZE)).astype(np.float32)
# Low-pass blend for smooth plaster texture
for s in [2, 4, 8]:
    scaled = np.repeat(np.repeat(rng.normal(0, 0.03, (SIZE//s, SIZE//s)), s, axis=0), s, axis=1)
    h_plaster += scaled[:SIZE, :SIZE].astype(np.float32)
norm_plaster = height_to_normal(h_plaster, strength=1.2)
norm_plaster.save(os.path.join(OUT_DIR, "Normal_Plaster.png"))

# 4. Normal_Facade: rougher exterior cement stucco
h_facade = rng.normal(0, 0.1, (SIZE, SIZE)).astype(np.float32)
for s in [4, 8, 16]:
    scaled = np.repeat(np.repeat(rng.normal(0, 0.06, (SIZE//s, SIZE//s)), s, axis=0), s, axis=1)
    h_facade += scaled[:SIZE, :SIZE].astype(np.float32)
norm_facade = height_to_normal(h_facade, strength=2.2)
norm_facade.save(os.path.join(OUT_DIR, "Normal_Facade.png"))

# 5. Normal_InoxSatin: AISI 304 satin #4 brushed steel (directional horizontal hairline scratches)
brush = rng.normal(0, 0.08, (SIZE, 1)).astype(np.float32)
brush = np.repeat(brush, SIZE, axis=1)
# Add slight micro variation along the brush length
noise_2d = rng.normal(0, 0.015, (SIZE, SIZE)).astype(np.float32)
norm_inox = height_to_normal(brush + noise_2d, strength=2.0)
norm_inox.save(os.path.join(OUT_DIR, "Normal_InoxSatin.png"))

# 6. Normal_AnodizedAlu: ultra-fine bead-blasted anodized aluminum
alu_noise = rng.normal(0, 0.03, (SIZE, SIZE)).astype(np.float32)
norm_alu = height_to_normal(alu_noise, strength=1.5)
norm_alu.save(os.path.join(OUT_DIR, "Normal_AnodizedAlu.png"))

# 7. Normal_LabPhenolic: Trespa TopLab chemical-resistant satin micro-particle
phenolic = rng.normal(0, 0.025, (SIZE, SIZE)).astype(np.float32)
norm_phenolic = height_to_normal(phenolic, strength=1.6)
norm_phenolic.save(os.path.join(OUT_DIR, "Normal_LabPhenolic.png"))

# 8. Normal_LabEpoxy: cast epoxy resin with subtle surface waviness and micro-scratches
x = np.linspace(0, 8*np.pi, SIZE)
y = np.linspace(0, 8*np.pi, SIZE)
xv, yv = np.meshgrid(x, y)
waviness = (np.sin(xv * 0.7 + yv * 0.3) * 0.02 + np.cos(yv * 0.8 - xv * 0.4) * 0.02).astype(np.float32)
norm_epoxy = height_to_normal(waviness + rng.normal(0, 0.01, (SIZE, SIZE)).astype(np.float32), strength=1.8)
norm_epoxy.save(os.path.join(OUT_DIR, "Normal_LabEpoxy.png"))

# 9. Normal_WoodGrain: natural oak wood grain fibers
wood_bands = np.sin(np.linspace(0, 32*np.pi, SIZE)[:, None] + rng.normal(0, 0.2, (SIZE, SIZE))) * 0.05
wood_micro = rng.normal(0, 0.02, (SIZE, SIZE))
norm_wood = height_to_normal((wood_bands + wood_micro).astype(np.float32), strength=2.0)
norm_wood.save(os.path.join(OUT_DIR, "Normal_WoodGrain.png"))

# 10. Normal_Concrete: smooth polished concrete
h_conc = rng.normal(0, 0.04, (SIZE, SIZE)).astype(np.float32)
for s in [8, 16]:
    scaled = np.repeat(np.repeat(rng.normal(0, 0.04, (SIZE//s, SIZE//s)), s, axis=0), s, axis=1)
    h_conc += scaled[:SIZE, :SIZE].astype(np.float32)
norm_conc = height_to_normal(h_conc, strength=1.8)
norm_conc.save(os.path.join(OUT_DIR, "Normal_Concrete.png"))

# 11. Normal_GlassOptic: optical glass with subtle micro-edge Fresnel
glass_micro = rng.normal(0, 0.005, (SIZE, SIZE)).astype(np.float32)
norm_glass = height_to_normal(glass_micro, strength=0.8)
norm_glass.save(os.path.join(OUT_DIR, "Normal_GlassOptic.png"))

# 12. Normal_Rubber: synthetic rubber molded micro-texture
rubber_micro = rng.normal(0, 0.035, (SIZE, SIZE)).astype(np.float32)
norm_rubber = height_to_normal(rubber_micro, strength=1.5)
norm_rubber.save(os.path.join(OUT_DIR, "Normal_Rubber.png"))

print("All PBR textures generated successfully in:", OUT_DIR)
