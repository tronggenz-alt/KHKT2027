#!/usr/bin/env python3
"""
tools/tao_icon_app.py
Tạo bộ biểu tượng ứng dụng (icon.icns cho macOS, icon.ico cho Windows)
hình bình tam giác theo logo CHEMISTRY chuẩn Apple Human Interface Guidelines.
"""
import os
import shutil
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter


def ve_icon(size=1024) -> Image.Image:
    # Tạo ảnh nền RGBA
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 1. Nền bo góc chuẩn Apple squircle (corner radius ~ 22.4% = 224px ở 1024)
    r = int(size * 0.22)
    # Gradient nền: xanh sẫm #0b192c đến #1e3e62
    base = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    base_draw = ImageDraw.Draw(base)

    # Tạo mask bo tròn góc
    mask = Image.new("L", (size, size), 0)
    mask_draw = ImageDraw.Draw(mask)
    margin = int(size * 0.05)
    mask_draw.rounded_rectangle(
        [margin, margin, size - margin, size - margin],
        radius=r,
        fill=255
    )

    # Vẽ dải màu gradient dọc
    for y in range(size):
        ratio = y / size
        # #0a1727 -> #132f4c -> #0284c7
        r_col = int(10 + ratio * 20)
        g_col = int(25 + ratio * 60)
        b_col = int(45 + ratio * 120)
        base_draw.line([(0, y), (size, y)], fill=(r_col, g_col, b_col, 255))

    img.paste(base, (0, 0), mask)

    # Viền sáng tinh tế xung quanh squircle
    border = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(border)
    b_draw.rounded_rectangle(
        [margin, margin, size - margin, size - margin],
        radius=r,
        outline=(56, 189, 248, 120),  # Cyan glow
        width=int(size * 0.015)
    )
    img = Image.alpha_composite(img, border)

    # 2. Vẽ bình tam giác (Erlenmeyer flask)
    # Tỉ lệ toạ độ theo kích thước
    cx = size // 2
    cy = int(size * 0.52)
    s = size / 1024.0

    neck_w = int(70 * s)
    neck_top = int(220 * s)
    neck_bot = int(450 * s)
    lip_w = int(110 * s)
    lip_h = int(22 * s)

    body_bot = int(780 * s)
    body_w_half = int(260 * s)
    liquid_y = int(580 * s)

    flask_layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    f_draw = ImageDraw.Draw(flask_layer)

    # Khối chất lỏng màu tím/hồng huỳnh quang (#f43f5e đến #a855f7)
    liquid_poly = [
        (cx - int(body_w_half * 0.65), liquid_y),
        (cx + int(body_w_half * 0.65), liquid_y),
        (cx + body_w_half - int(20 * s), body_bot - int(10 * s)),
        (cx - body_w_half + int(20 * s), body_bot - int(10 * s)),
    ]
    f_draw.polygon(liquid_poly, fill=(244, 63, 94, 210))  # Rose pink liquid
    # Bong bóng khí trong chất lỏng
    f_draw.ellipse([cx - 40 * s, body_bot - 80 * s, cx - 20 * s, body_bot - 60 * s], fill=(255, 255, 255, 180))
    f_draw.ellipse([cx + 60 * s, body_bot - 110 * s, cx + 75 * s, body_bot - 95 * s], fill=(255, 255, 255, 180))
    f_draw.ellipse([cx - 10 * s, body_bot - 140 * s, cx + 2 * s, body_bot - 128 * s], fill=(255, 255, 255, 180))

    # Miệng bình & vành miệng
    f_draw.rounded_rectangle([cx - lip_w // 2, neck_top - lip_h, cx + lip_w // 2, neck_top], radius=int(6 * s), fill=(224, 242, 254, 230))
    # Cổ bình
    f_draw.rectangle([cx - neck_w // 2, neck_top, cx + neck_w // 2, neck_bot], fill=(186, 230, 253, 50), outline=(56, 189, 248, 240), width=int(14 * s))

    # Thân bình tam giác
    body_poly = [
        (cx - neck_w // 2, neck_bot),
        (cx - body_w_half, body_bot),
        (cx + body_w_half, body_bot),
        (cx + neck_w // 2, neck_bot),
    ]
    f_draw.polygon(body_poly, fill=(186, 230, 253, 40), outline=(56, 189, 248, 240))
    # Đáy bình hơi cong nhẹ
    f_draw.line([(cx - body_w_half, body_bot), (cx + body_w_half, body_bot)], fill=(56, 189, 248, 240), width=int(16 * s))

    # Vạch chia độ (meniscus marks)
    for i, mark_y in enumerate([680 * s, 620 * s, 560 * s, 500 * s]):
        w_mark = int(35 * s) if i % 2 == 0 else int(22 * s)
        mx = cx - int(body_w_half * (0.35 + (800 * s - mark_y) / (800 * s) * 0.45))
        f_draw.line([(mx, mark_y), (mx + w_mark, mark_y)], fill=(255, 255, 255, 220), width=int(8 * s))

    # Ghép lớp bình thuỷ tinh vào hình chính
    img = Image.alpha_composite(img, flask_layer)

    return img


def tao_icons():
    assets_dir = Path("desktop/assets")
    assets_dir.mkdir(parents=True, exist_ok=True)

    print("-> Đang tạo biểu tượng 1024x1024...")
    icon1024 = ve_icon(1024)
    png_path = assets_dir / "icon.png"
    icon1024.save(png_path, "PNG")
    print(f"   Đã lưu: {png_path}")

    # Tạo file .ico cho Windows (chứa nhiều kích thước 16..256)
    ico_sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    ico_path = assets_dir / "icon.ico"
    icon1024.save(ico_path, format="ICO", sizes=ico_sizes)
    print(f"   Đã lưu: {ico_path}")

    # Tạo file .icns cho macOS bằng iconutil nếu có trên Mac
    iconset_dir = assets_dir / "icon.iconset"
    if iconset_dir.exists():
        shutil.rmtree(iconset_dir)
    iconset_dir.mkdir(parents=True, exist_ok=True)

    sizes = [
        ("icon_16x16.png", 16),
        ("icon_16x16@2x.png", 32),
        ("icon_32x32.png", 32),
        ("icon_32x32@2x.png", 64),
        ("icon_128x128.png", 128),
        ("icon_128x128@2x.png", 256),
        ("icon_256x256.png", 256),
        ("icon_256x256@2x.png", 512),
        ("icon_512x512.png", 512),
        ("icon_512x512@2x.png", 1024),
    ]

    for fname, sz in sizes:
        resized = icon1024.resize((sz, sz), Image.Resampling.LANCZOS)
        resized.save(iconset_dir / fname, "PNG")

    icns_path = assets_dir / "icon.icns"
    try:
        subprocess.run(["iconutil", "-c", "icns", str(iconset_dir), "-o", str(icns_path)], check=True)
        print(f"   Đã tạo icon macOS: {icns_path}")
        shutil.rmtree(iconset_dir)
    except Exception as e:
        print(f"   Lỗi khi chạy iconutil: {e}")

    print("✔ Hoàn tất tạo bộ biểu tượng ứng dụng.")


if __name__ == "__main__":
    tao_icons()
