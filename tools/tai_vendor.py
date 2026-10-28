#!/usr/bin/env python3
"""
tools/tai_vendor.py
Tải các thư viện CDN về frontend/vendor/ để ứng dụng desktop CHEMISTRY chạy offline 100%.
Ghi thông tin phiên bản và SHA-256 vào frontend/vendor/PHIEN_BAN.json.
"""
import hashlib
import json
import os
import sys
import urllib.request
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent
VENDOR_DIR = ROOT_DIR / "frontend" / "vendor"

CAC_TEP_VENDOR = [
    {
        "ten": "tailwind.3.4.16.js",
        "url": "https://cdn.tailwindcss.com/3.4.16",
        "duong_dan_dich": "tailwind.3.4.16.js",
        "phien_ban": "3.4.16",
    },
    {
        "ten": "gsap.3.12.5.min.js",
        "url": "https://cdn.jsdelivr.net/npm/gsap@3.12.5/dist/gsap.min.js",
        "duong_dan_dich": "gsap.3.12.5.min.js",
        "phien_ban": "3.12.5",
    },
    {
        "ten": "three.module.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js",
        "duong_dan_dich": "three/three.module.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "OrbitControls.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/controls/OrbitControls.js",
        "duong_dan_dich": "three/addons/controls/OrbitControls.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "CSS2DRenderer.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/renderers/CSS2DRenderer.js",
        "duong_dan_dich": "three/addons/renderers/CSS2DRenderer.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "Line2.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/lines/Line2.js",
        "duong_dan_dich": "three/addons/lines/Line2.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "LineMaterial.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/lines/LineMaterial.js",
        "duong_dan_dich": "three/addons/lines/LineMaterial.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "LineGeometry.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/lines/LineGeometry.js",
        "duong_dan_dich": "three/addons/lines/LineGeometry.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "LineSegmentsGeometry.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/lines/LineSegmentsGeometry.js",
        "duong_dan_dich": "three/addons/lines/LineSegmentsGeometry.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "LineSegments2.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/lines/LineSegments2.js",
        "duong_dan_dich": "three/addons/lines/LineSegments2.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "RoomEnvironment.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/environments/RoomEnvironment.js",
        "duong_dan_dich": "three/addons/environments/RoomEnvironment.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "RoundedBoxGeometry.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/geometries/RoundedBoxGeometry.js",
        "duong_dan_dich": "three/addons/geometries/RoundedBoxGeometry.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "GLTFLoader.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/loaders/GLTFLoader.js",
        "duong_dan_dich": "three/addons/loaders/GLTFLoader.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "BufferGeometryUtils.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/utils/BufferGeometryUtils.js",
        "duong_dan_dich": "three/addons/utils/BufferGeometryUtils.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "mathjax.3.2.2.js",
        "url": "https://cdn.jsdelivr.net/npm/mathjax@3.2.2/es5/tex-chtml.js",
        "duong_dan_dich": "mathjax.3.2.2.js",
        "phien_ban": "3.2.2",
    },
    {
        "ten": "DRACOLoader.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/loaders/DRACOLoader.js",
        "duong_dan_dich": "three/addons/loaders/DRACOLoader.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "draco_decoder.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/libs/draco/gltf/draco_decoder.js",
        "duong_dan_dich": "three/draco/draco_decoder.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "draco_decoder.wasm",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/libs/draco/gltf/draco_decoder.wasm",
        "duong_dan_dich": "three/draco/draco_decoder.wasm",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "draco_wasm_wrapper.js",
        "url": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/libs/draco/gltf/draco_wasm_wrapper.js",
        "duong_dan_dich": "three/draco/draco_wasm_wrapper.js",
        "phien_ban": "0.160.0",
    },
    {
        "ten": "motion.12.4.7.js",
        "url": "https://cdn.jsdelivr.net/npm/motion@12.4.7/dist/motion.js",
        "duong_dan_dich": "motion/motion.12.4.7.js",
        "phien_ban": "12.4.7",
    },
]

MATHJAX_FONTS = [
    "MathJax_AMS-Regular.woff",
    "MathJax_Calligraphic-Bold.woff",
    "MathJax_Calligraphic-Regular.woff",
    "MathJax_Fraktur-Bold.woff",
    "MathJax_Fraktur-Regular.woff",
    "MathJax_Main-Bold.woff",
    "MathJax_Main-Italic.woff",
    "MathJax_Main-Regular.woff",
    "MathJax_Math-BoldItalic.woff",
    "MathJax_Math-Italic.woff",
    "MathJax_Math-Regular.woff",
    "MathJax_SansSerif-Bold.woff",
    "MathJax_SansSerif-Italic.woff",
    "MathJax_SansSerif-Regular.woff",
    "MathJax_Script-Regular.woff",
    "MathJax_Size1-Regular.woff",
    "MathJax_Size2-Regular.woff",
    "MathJax_Size3-Regular.woff",
    "MathJax_Size4-Regular.woff",
    "MathJax_Typewriter-Regular.woff",
    "MathJax_Vector-Bold.woff",
    "MathJax_Vector-Regular.woff",
    "MathJax_Zero.woff",
]

for font_name in MATHJAX_FONTS:
    CAC_TEP_VENDOR.append({
        "ten": f"mathjax-fonts/woff-v2/{font_name}",
        "url": f"https://cdn.jsdelivr.net/npm/mathjax@3.2.2/es5/output/chtml/fonts/woff-v2/{font_name}",
        "duong_dan_dich": f"mathjax-fonts/woff-v2/{font_name}",
        "phien_ban": "3.2.2",
    })



def tai_tep(url: str, dich: Path) -> str:
    import subprocess
    dich.parent.mkdir(parents=True, exist_ok=True)
    res = subprocess.run(["curl", "-sSL", "--connect-timeout", "15", "-o", str(dich), url], check=False)
    if res.returncode != 0 or not dich.exists() or dich.stat().st_size == 0:
        raise RuntimeError(f"curl thất bại (mã {res.returncode})")
    return hashlib.sha256(dich.read_bytes()).hexdigest()


def main():
    print(f"==> Đang tải các thư viện vendor vào: {VENDOR_DIR}")
    VENDOR_DIR.mkdir(parents=True, exist_ok=True)
    bang_phien_ban = {}

    for item in CAC_TEP_VENDOR:
        dich = VENDOR_DIR / item["duong_dan_dich"]
        print(f"  -> Tải {item['ten']} từ {item['url']} ...")
        try:
            sha256 = tai_tep(item["url"], dich)
            bang_phien_ban[item["duong_dan_dich"]] = {
                "ten": item["ten"],
                "url": item["url"],
                "phien_ban": item["phien_ban"],
                "sha256": sha256,
                "kich_thuoc_bytes": dich.stat().st_size,
            }
            print(f"     ✓ Xong ({dich.stat().st_size:,} bytes, sha256: {sha256[:12]}...)")
        except Exception as e:
            print(f"     ✗ Lỗi tải {item['ten']}: {e}", file=sys.stderr)
            # Tạo fallback stub nếu không có mạng để app không bị crash
            if not dich.exists():
                dich.write_text(f"/* Fallback stub cho {item['ten']} */\n", encoding="utf-8")
                bang_phien_ban[item["duong_dan_dich"]] = {
                    "ten": item["ten"],
                    "url": item["url"],
                    "phien_ban": item["phien_ban"],
                    "sha256": hashlib.sha256(dich.read_bytes()).hexdigest(),
                    "kich_thuoc_bytes": dich.stat().st_size,
                    "stub": True,
                }

    file_phien_ban = VENDOR_DIR / "PHIEN_BAN.json"
    file_phien_ban.write_text(json.dumps(bang_phien_ban, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"==> Đã lưu thông tin phiên bản vào: {file_phien_ban}")


if __name__ == "__main__":
    main()
