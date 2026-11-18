"""Thông số phần cứng phía máy chủ: đọc RAM, chip/GPU rồi chọn mô hình AI tại máy và mức tính toán RDKit.

    python -m server.ai.cau_hinh_may          in thông số đã chọn (JSON)

Chọn mô hình (Ollama):
    GPU (VRAM) ≥ 24 GB              → vera-hoa-32b   (server/ai/Modelfile, nền qwen2.5:32b-instruct)
    RAM ≥ 16 GB                     → qwen2.5:7b-instruct
    còn lại (RAM ≤ 8 GB, máy này)   → vera-hoa-3b    (server/ai/co_che/Modelfile.vera-hoa-3b, nền qwen2.5:3b-instruct)
Cùng mức đó chọn số lần thử dựng cấu dạng RDKit (ETKDG) và thời gian chờ mô hình trả lời.

Đọc phần cứng: macOS dùng sysctl (hw.memsize, machdep.cpu.brand_string; Apple Silicon dùng chung RAM cho GPU nên không tính là
GPU rời); Windows dùng psutil (RAM) và wmic (tên GPU, VRAM); Linux dùng /proc/meminfo và nvidia-smi. Thiếu công cụ nào thì bỏ qua.

Ghi đè bằng biến môi trường: VERA_MO_HINH, VERA_RAM_GB, VERA_VRAM_GB, VERA_SO_CAU_DANG, VERA_THOI_GIAN_CHO, OLLAMA_URL.
"""
from __future__ import annotations

import json
import os
import platform
import shutil
import subprocess
from dataclasses import asdict, dataclass

MO_HINH_GOC = {"vera-hoa-3b": "qwen2.5:3b-instruct", "qwen2.5:7b-instruct": "qwen2.5:7b-instruct",
               "vera-hoa-32b": "qwen2.5:32b-instruct-q4_K_M"}


@dataclass
class ThongSo:
    he_dieu_hanh: str
    chip: str
    ram_gb: float
    gpu: str
    vram_gb: float
    muc: str                 # "thap" | "vua" | "cao"
    mo_hinh: str             # tên mô hình Ollama dùng để trả lời
    mo_hinh_goc: str         # mô hình nền cần `ollama pull`
    so_cau_dang: int         # số cấu dạng ETKDG thử khi dựng lại 3D
    thoi_gian_cho: float     # giây chờ mô hình trả lời
    ollama_url: str
    keep_alive: str          # giữ mô hình trong RAM bao lâu sau câu cuối (trả RAM cho Unity)


def _chay(lenh: list[str], cho: float = 4.0) -> str:
    if not shutil.which(lenh[0]):
        return ""
    try:
        return subprocess.run(lenh, capture_output=True, text=True, timeout=cho).stdout.strip()
    except (OSError, subprocess.SubprocessError):
        return ""


def doc_phan_cung() -> tuple[str, float, str, float]:
    """(chip, RAM GB, GPU, VRAM GB) – VRAM 0 nếu không có GPU rời đọc được."""
    he = platform.system()
    chip, ram, gpu, vram = platform.processor() or platform.machine(), 0.0, "", 0.0
    if he == "Darwin":
        chip = _chay(["sysctl", "-n", "machdep.cpu.brand_string"]) or chip
        try:
            ram = int(_chay(["sysctl", "-n", "hw.memsize"]) or 0) / 2**30
        except ValueError:
            pass
        gpu = "Apple GPU (bộ nhớ dùng chung)" if platform.machine() == "arm64" else ""
    else:
        try:
            import psutil
            ram = psutil.virtual_memory().total / 2**30
        except ImportError:
            if os.path.exists("/proc/meminfo"):
                with open("/proc/meminfo", encoding="utf-8") as f:
                    for d in f:
                        if d.startswith("MemTotal:"):
                            ram = int(d.split()[1]) / 2**20
        nv = _chay(["nvidia-smi", "--query-gpu=name,memory.total", "--format=csv,noheader,nounits"])
        if nv:
            ten, mb = nv.splitlines()[0].rsplit(",", 1)
            gpu, vram = ten.strip(), float(mb) / 1024
        elif he == "Windows":
            w = _chay(["wmic", "path", "win32_VideoController", "get", "Name,AdapterRAM", "/format:csv"])
            for d in w.splitlines()[1:]:
                p = [x for x in d.split(",") if x.strip()]
                if len(p) >= 3 and p[1].strip().isdigit():
                    if int(p[1]) / 2**30 > vram:
                        gpu, vram = p[2].strip(), int(p[1]) / 2**30   # wmic báo tối đa ~4 GB (giới hạn 32 bit)
    return chip, round(ram, 1), gpu, round(vram, 1)


def chon(ram_gb: float, vram_gb: float) -> tuple[str, str, int, float]:
    """(mức, mô hình, số cấu dạng ETKDG, thời gian chờ giây) theo phần cứng."""
    if vram_gb >= 24:
        return "cao", "vera-hoa-32b", 60, 90.0
    if ram_gb >= 16:
        return "vua", "qwen2.5:7b-instruct", 40, 90.0
    return "thap", "vera-hoa-3b", 20, 120.0


def thong_so() -> ThongSo:
    chip, ram, gpu, vram = doc_phan_cung()
    ram = float(os.environ.get("VERA_RAM_GB") or ram)
    vram = float(os.environ.get("VERA_VRAM_GB") or vram)
    muc, mh, so, cho = chon(ram, vram)
    mh = os.environ.get("VERA_MO_HINH") or mh
    return ThongSo(platform.system(), chip, ram, gpu, vram, muc, mh, MO_HINH_GOC.get(mh, mh),
                   int(os.environ.get("VERA_SO_CAU_DANG") or so), float(os.environ.get("VERA_THOI_GIAN_CHO") or cho),
                   (os.environ.get("OLLAMA_URL") or "http://127.0.0.1:11434").rstrip("/"), os.environ.get("VERA_KEEP_ALIVE", "5m"))


if __name__ == "__main__":
    print(json.dumps(asdict(thong_so()), ensure_ascii=False, indent=1))
