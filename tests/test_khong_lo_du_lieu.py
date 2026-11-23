"""Dò kho git (git ls-files) tìm tệp hoặc nội dung giống khóa bí mật và dữ liệu kín. Kho GitHub đang CÔNG KHAI.

Dữ liệu kín phải nằm ở VERA_DU_LIEU_RIENG (mặc định ~/Developer/VERA_du_lieu_rieng), không bao giờ vào git:
kho mẫu học từ đáp án HSG, báo cáo bộ đo, bài demo cơ chế, gói .verapack, khóa gói, chỉ mục RAG cơ chế, tài khoản, dữ liệu AI riêng.
"""

import re
import subprocess
import unittest
from pathlib import Path

GOC = Path(__file__).resolve().parents[1]

TEN_CAM = [
    r"khung_mau_dap_an", r"bao_cao_bo_do", r"bao_cao_so_do", r"bao_cao_de_nhin", r"\.verapack$", r"(^|/)khoa\.bytes$",
    r"vera_co_che\.key$", r"\.key$", r"\.pem$", r"\.p12$", r"(^|/)\.env(\..+)?$", r"tai_khoan_admin\.txt$",
    r"email_tam_thoi\.txt$", r"^data/ai_rieng/", r"^data/geoip/", r"rag_co_che\.db$", r"VERA_du_lieu_rieng/",
    r"co_che/bai_demo/", r"propellane_dmad_A4", r"^nguyen_mau/co_che_3d/du_lieu/", r"nhat_ky_dong_goi",
]
NOI_DUNG_CAM = {
    "khóa riêng PEM": rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----",
    "khóa Anthropic": rb"sk-ant-[A-Za-z0-9_\-]{30,}",
    "khóa Google": rb"AIza[0-9A-Za-z_\-]{35}",
    "khóa Groq": rb"gsk_[A-Za-z0-9]{40,}",
    "mã GitHub": rb"gh[pousr]_[A-Za-z0-9]{36,}",
    "khóa OpenAI": rb"sk-(?:proj-)?[A-Za-z0-9]{40,}",
    "kho mẫu đáp án (cấu trúc)": rb'"khung_vo"\s*:\s*\[\s*\{\s*"khoa"',
    "cơ chế HSG propellane (SMILES có map)": rb"\[C:1\]123\[C:2\]\(\[CH2:3\]1\)",
}
DUOI_NHI_PHAN = {".png", ".jpg", ".jpeg", ".gif", ".ico", ".icns", ".mp3", ".mp4", ".wav", ".ogg", ".fbx", ".obj", ".glb",
                 ".unity", ".asset", ".mat", ".prefab", ".anim", ".controller", ".ttf", ".otf", ".dll", ".so", ".dylib", ".a",
                 ".bin", ".gz", ".zip", ".task", ".tflite", ".onnx", ".pdf", ".psd", ".tga", ".exr", ".hdr"}


def ds_tep() -> list[str]:
    try:
        r = subprocess.run(["git", "ls-files", "-z"], cwd=GOC, capture_output=True, check=True)
    except (OSError, subprocess.CalledProcessError):
        return []
    return [p for p in r.stdout.decode("utf-8", "replace").split("\0") if p]


@unittest.skipUnless((GOC / ".git").exists(), "không phải bản sao git")
class TestKhongLoDuLieu(unittest.TestCase):
    def test_ten_tep(self):
        lo = [p for p in ds_tep() if any(re.search(m, p) for m in TEN_CAM)]
        self.assertEqual(lo, [], "tệp kín đang được git theo dõi")

    def test_noi_dung(self):
        lo = []
        for p in ds_tep():
            f = GOC / p
            if f.suffix.lower() in DUOI_NHI_PHAN or not f.is_file() or f.stat().st_size > 3_000_000:
                continue
            b = f.read_bytes()
            for ten, mau in NOI_DUNG_CAM.items():
                for m in re.finditer(mau, b):
                    if ten.startswith(("khóa", "mã")) and len(set(m.group(0)[6:])) <= 4:
                        continue                                   # khóa giả trong test: "AIzaSyAAAA…"
                    lo.append(f"{p}: {ten}")
                    break
        self.assertEqual(lo, [], "nội dung giống khóa/dữ liệu kín trong kho")

    def test_bo_do_bat_duoc_khoa_that(self):
        gia_ma_that = b"AIzaSy" + bytes("k3Jd9QxP2mZ7vL0aR5tY8wE1nB6cH4fGuIo", "ascii")
        self.assertTrue(re.search(NOI_DUNG_CAM["khóa Google"], gia_ma_that))
        self.assertGreater(len(set(gia_ma_that[6:])), 4)

    def test_gitignore_chan_du_lieu_kin(self):
        mau = ["build/co_che/goi/x.verapack", "Phong thi nghiem ao/Assets/Resources/CoChe/khoa.bytes",
               "Phong thi nghiem ao/Assets/StreamingAssets/CoChe/x.verapack", "nguyen_mau/co_che_3d/du_lieu/khung_mau_dap_an.json",
               ".env", "x.key"]
        r = subprocess.run(["git", "check-ignore", *mau], cwd=GOC, capture_output=True, text=True)
        self.assertEqual(sorted(r.stdout.split("\n")[:-1]), sorted(mau))


if __name__ == "__main__":
    unittest.main()
