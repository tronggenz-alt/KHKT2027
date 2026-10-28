# -*- mode: python ; coding: utf-8 -*-
"""
desktop/may_chu/chemistry.spec
Cấu hình PyInstaller đóng gói máy chủ backend CHEMISTRY thành thư mục (onedir).
Gói kèm: backend, frontend, config, knowledge, RDKit, Pillow...
Loại bỏ: pytest, test suites, tài liệu thừa.
"""
import os
import sys
from pathlib import Path

block_cipher = None

# Thư mục gốc của dự án
SPEC_DIR = Path(SPECPATH).resolve()
ROOT_DIR = SPEC_DIR.parent.parent

# Các thư mục dữ liệu tĩnh cần đóng gói
datas = [
    (str(ROOT_DIR / "frontend"), "frontend"),
    (str(ROOT_DIR / "config"), "config"),
    (str(ROOT_DIR / "knowledge"), "knowledge"),
    (str(ROOT_DIR / "TAI_NGUYEN.json"), "."),
    (str(ROOT_DIR / "GHI_CONG.md"), "."),
]

# Hidden imports cần thiết cho FastAPI, Uvicorn, RDKit, Cryptography
hidden_imports = [
    "uvicorn",
    "uvicorn.logging",
    "uvicorn.loops",
    "uvicorn.loops.auto",
    "uvicorn.protocols",
    "uvicorn.protocols.http",
    "uvicorn.protocols.http.auto",
    "uvicorn.protocols.websockets",
    "uvicorn.protocols.websockets.auto",
    "uvicorn.lifespan",
    "uvicorn.lifespan.on",
    "fastapi",
    "starlette",
    "pydantic",
    "rdkit",
    "rdkit.Chem",
    "rdkit.Chem.Draw",
    "rdkit.Chem.AllChem",
    "PIL",
    "PIL.Image",
    "PIL.ImageDraw",
    "PIL.ImageFont",
    "sqlite3",
    "cryptography",
    "cryptography.fernet",
    "email_validator",
    "requests",
    "backend.main",
    "backend.config",
    "backend.db",
    "backend.auth",
    "backend.quan_tri_web",
    "backend.phong_thi_nghiem",
    "backend.sinh_hoc_ai",
    "backend.pipeline",
    "backend.giai_nhanh",
    "backend.co_che",
    "backend.mo_hinh_3d",
    "backend.ai_rieng",
    "backend.lo_trinh",
    "backend.chi_phi_ai",
    "backend.email_rac",
    "backend.cap_nhat_app",
]

# Loại bỏ các module không cần thiết trong ứng dụng máy tính
excludes = [
    "pytest",
    "pytest_asyncio",
    "_pytest",
    "unittest",
    "doctest",
    "rich",
    "tkinter",
    "matplotlib",
    "IPython",
    "jupyter",
    "notebook",
]

a = Analysis(
    [str(SPEC_DIR / "khoi_dong.py")],
    pathex=[str(ROOT_DIR)],
    binaries=[],
    datas=datas,
    hiddenimports=hidden_imports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=excludes,
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="may_chu_chemistry",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=False,
    upx_exclude=[],
    name="may_chu_chemistry",
)
