"""Chạy trên máy chủ (Render, Docker): dữ liệu ra ổ đĩa riêng, tắt chế độ cục bộ, font Linux, không có key trong file triển khai."""
import os
import subprocess
import sys
from pathlib import Path

GOC = Path(__file__).resolve().parents[1]


def test_storage_dir_dua_du_lieu_ra_o_dia_rieng(tmp_path):
    env = {**os.environ, "STORAGE_DIR": str(tmp_path)}
    ra = subprocess.run([sys.executable, "-c", "from backend import config as c; print(c.DATA_DIR); print(c.OUTPUT_DIR); print(c.LOGS_DIR)"],
                        cwd=GOC, env=env, capture_output=True, text=True, check=True).stdout.split()
    assert ra == [str(tmp_path / "data"), str(tmp_path / "outputs"), str(tmp_path / "logs")]


def test_tat_che_do_cuc_bo(monkeypatch):
    from starlette.requests import Request
    from backend import auth
    from backend.config import settings
    yeu_cau = Request({"type": "http", "method": "GET", "path": "/", "headers": [], "client": ("127.0.0.1", 5000)})
    monkeypatch.setattr(settings, "google_client_id", "")
    assert auth.che_do_cuc_bo(yeu_cau)                     # máy chạy app (Mac) – vào thẳng như cũ
    monkeypatch.setattr(settings, "che_do_cuc_bo", False)  # máy chủ: CHE_DO_CUC_BO=0 → không ai được vào thẳng
    assert not auth.che_do_cuc_bo(yeu_cau)


def test_font_linux_cho_anh_xuat():
    from backend import config
    for ds in (config.FONT_REGULAR, config.FONT_BOLD, config.FONT_ITALIC, config.FONT_FALLBACK):
        assert any(f.startswith("/usr/share/fonts/truetype/") for f in ds)


def test_file_trien_khai_khong_chua_bi_mat():
    docker = (GOC / "Dockerfile").read_text(encoding="utf-8")
    assert "CHE_DO_CUC_BO=0" in docker and "STORAGE_DIR=/var/data" in docker and "fonts-liberation2" in docker
    bo_qua = (GOC / ".dockerignore").read_text(encoding="utf-8").split()
    assert {".env", "data", "outputs", ".venv", ".git"} <= set(bo_qua)
    render = (GOC / "render.yaml").read_text(encoding="utf-8")
    # gói Free: không ổ đĩa → bật sao lưu GitHub; khoá chính do Render tự sinh (không nằm trong bản sao lưu)
    assert "plan: free" in render and "SAO_LUU_GITHUB_REPO" in render and "key: APP_SECRET_KEY" in render and "generateValue: true" in render
    dong = render.splitlines()
    for i, d in enumerate(dong):       # mọi biến key / mật khẩu: "sync: false" (nhập trên Render) hoặc Render tự sinh – không ghi giá trị
        if d.strip().startswith("- key:") and any(t in d for t in ("KEY", "PASS", "SECRET", "TOKEN")):
            assert dong[i + 1].strip() in ("sync: false", "generateValue: true"), d
