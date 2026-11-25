"""Kiểm thử nhận diện thao tác bàn tay (tools/tay_thao_tac.py) bằng bàn tay dựng giả."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import tay_thao_tac as tt  # noqa: E402


def ban_tay(duoi=(True, True, True, True), chum=False):
    """21 điểm ảnh: cổ tay ở dưới, ngón hướng lên; ngón co thì đầu ngón quay về gần cổ tay."""
    p = [(0.5, 0.8, 0.0)] * 21
    p = list(p)
    p[0] = (0.5, 0.8, 0.0)
    goc = {1: 0.42, 5: 0.45, 9: 0.5, 13: 0.55, 17: 0.6}
    for f, (b, x) in enumerate(goc.items()):
        for j in range(4):
            y = 0.7 - j * 0.05
            p[b + j] = (x, y, 0.0)
    for k, (tip, pip) in enumerate(((8, 6), (12, 10), (16, 14), (20, 18))):
        if not duoi[k]:
            p[tip] = (p[tip - 3][0], 0.74, 0.0)
            p[tip - 1] = (p[tip - 3][0], 0.72, 0.0)
    p[4] = (0.38, 0.62, 0.0)
    if chum:
        p[4] = (p[8][0] + 0.005, p[8][1], 0.0)
    return p


class TestThaoTac(unittest.TestCase):
    def test_cac_tu_the(self):
        self.assertEqual(tt.phan_loai(ban_tay())[0], "mo")
        self.assertEqual(tt.phan_loai(ban_tay((True, False, False, False)))[0], "tro")
        self.assertEqual(tt.phan_loai(ban_tay((False, False, False, True)))[0], "ut")
        self.assertEqual(tt.phan_loai(ban_tay((True, True, True, True), chum=True))[0], "chum")

    def test_chum_khi_ba_ngon_kia_co(self):
        # chụm ngón cái - ngón trỏ, giữa / áp út / út co: trước đây bị nhầm là nắm tay
        p = ban_tay((True, False, False, False), chum=True)
        self.assertEqual(tt.phan_loai(p)[0], "chum")

    def test_nam_tay_khong_bi_nham_la_chum(self):
        # nắm tay: ngón cái đè sát đầu ngón trỏ đã gập vào lòng
        p = ban_tay((False, False, False, False))
        p[4] = (p[8][0] + 0.005, p[8][1], 0.0)
        self.assertNotEqual(tt.phan_loai(p)[0], "chum")

    def test_nam_doc_ngang_theo_phap_tuyen(self):
        nam = ban_tay((False, False, False, False))
        # lòng bàn tay quay ngang: gốc ngón trỏ và ngón út xếp theo trục đứng
        doc = [(0, 0, 0)] * 21
        doc[5], doc[17] = (0.0, -0.08, 0.0), (0.0, -0.06, 0.05)
        ngang = [(0, 0, 0)] * 21
        ngang[5], ngang[17] = (-0.03, 0.0, -0.08), (0.04, 0.0, -0.07)
        self.assertEqual(tt.phan_loai(nam, doc)[0], "nam_doc")
        self.assertEqual(tt.phan_loai(nam, ngang)[0], "nam_ngang")

    def test_chum_co_tre(self):
        p = ban_tay(chum=True)
        p[4] = (p[8][0] + 0.036, p[8][1], 0.0)   # tỉ lệ 0,36: giữa hai ngưỡng
        self.assertNotEqual(tt.phan_loai(p, dang_chum=False)[0], "chum")
        self.assertEqual(tt.phan_loai(p, dang_chum=True)[0], "chum")

    def test_bo_phieu(self):
        b = tt.BoPhieu(5)
        for x in ("mo", "mo", "tro", "mo", "tro"):
            r = b.them(x)
        self.assertEqual(r, "mo")

    def test_gan_ben_theo_vi_tri_cu(self):
        g = tt.GanBen()
        self.assertEqual(g.gan([(0.3, 0.5), (0.7, 0.5)], 0.0), ["trai", "phai"])
        # tay phải đi qua nửa trái vẫn là tay phải
        self.assertEqual(g.gan([(0.45, 0.5)], 0.1), ["trai"])
        self.assertEqual(g.gan([(0.62, 0.5)], 0.2), ["phai"])


if __name__ == "__main__":
    unittest.main()
