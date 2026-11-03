import os
import glob
import re
import json
import numpy as np
from PIL import Image

def tinh_ti_so(path):
    try:
        im = Image.open(path).convert("L")
        arr = np.array(im, dtype=float)
        if np.std(arr) < 1e-4:
            return 0.0
        h, w = arr.shape
        win = np.outer(np.hanning(h), np.hanning(w))
        f = np.fft.fftshift(np.fft.fft2((arr - np.mean(arr)) * win))
        mag = np.abs(f)
        cy, cx = h // 2, w // 2
        y, x = np.ogrid[:h, :w]
        mag[(x - cx)**2 + (y - cy)**2 <= 25] = 0.0
        nz = mag[mag > 0]
        return float(np.max(nz) / (np.median(nz) + 1e-8)) if len(nz) > 0 else 0.0
    except Exception as e:
        return -1.0

# 1. Quét GUID của các texture
tex_guids = {}
for meta_path in glob.glob("/Users/trantrong/VERALabHD/Phong thi nghiem ao/Assets/Lab/**/*.png.meta", recursive=True):
    with open(meta_path, "r", encoding="utf-8") as fp:
        m = re.search(r"guid:\s*([a-f0-9]+)", fp.read())
        if m:
            tex_file = meta_path[:-5]
            tex_guids[m.group(1)] = tex_file

# 2. Quét các material xem dùng GUID nào
mat_uses = {}
for mat_path in glob.glob("/Users/trantrong/VERALabHD/Phong thi nghiem ao/Assets/Lab/Materials/*.mat"):
    mat_name = os.path.basename(mat_path)[:-4]
    with open(mat_path, "r", encoding="utf-8") as fp:
        content = fp.read()
        for guid in set(re.findall(r"guid:\s*([a-f0-9]{32})", content)):
            if guid in tex_guids:
                tex_file = tex_guids[guid]
                mat_uses.setdefault(tex_file, []).append(mat_name)

# 3. Phân tích FFT các textures
inventory = []
for folder in ["Assets/Lab/HD/Textures", "Assets/Lab/Textures"]:
    dir_path = f"/Users/trantrong/VERALabHD/Phong thi nghiem ao/{folder}"
    for f in sorted(glob.glob(f"{dir_path}/*.png")):
        fname = os.path.basename(f)
        r = tinh_ti_so(f)
        # Ngoại lệ có chủ đích
        is_intentional = any(k in fname.lower() for k in ["poster", "ghs", "bieutuong", "co", "vach", "apphich", "socmau", "buret", "bangtuanhoan", "lavoisier", "chandung", "tile", "gach", "lamgo", "machdien", "banghim", "luoiban", "nenmanhinh", "sanloidi", "anhgel", "mangiaiphau"])
        has_stripes = (r >= 30.0) and not is_intentional
        mats = mat_uses.get(f, [])
        inventory.append({
            "folder": folder,
            "filename": fname,
            "path": f,
            "ratio": r,
            "is_intentional": is_intentional,
            "has_stripes": has_stripes,
            "materials_used": sorted(mats)
        })

out_json = "/Users/trantrong/VERALabHD/docs/do-hoa-hd/be-mat/danh_sach_van_soc.json"
os.makedirs(os.path.dirname(out_json), exist_ok=True)
with open(out_json, "w", encoding="utf-8") as fp:
    json.dump(inventory, fp, indent=2, ensure_ascii=False)

print(f"Tổng số texture đã quét: {len(inventory)}")
striped = [x for x in inventory if x["has_stripes"]]
print(f"Số texture bị sọc bất thường: {len(striped)}")
for x in striped:
    fn = x["filename"]
    rat = x["ratio"]
    mu = str(x["materials_used"])
    print(f"  • {fn:32s} | Ratio: {rat:12.2f} | Dùng bởi: {mu}")
