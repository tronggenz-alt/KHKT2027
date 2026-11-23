"""Kiểm thử gói mã hóa .verapack: AES-256 đúng véc-tơ chuẩn, đóng rồi mở phải khớp, sửa 1 byte hay sai khóa thì bị từ chối."""

import json
import os
import secrets
import unittest

from server.ai.co_che import aes_thuan, dong_goi


class TestAes(unittest.TestCase):
    def test_fips197_c3(self):
        k = bytes(range(32))
        self.assertEqual(aes_thuan.ma_khoi(k, bytes.fromhex("00112233445566778899aabbccddeeff")).hex(), "8ea2b7ca516745bfeafc49904b496089")

    def test_sp800_38a_cbc_aes256(self):
        k = bytes.fromhex("603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4")
        iv = bytes.fromhex("000102030405060708090a0b0c0d0e0f")
        pt = bytes.fromhex("6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e51")
        ct = aes_thuan.ma_cbc(k, iv, pt)
        self.assertEqual(ct[:32].hex(), "f58c4c04d6e5f1ba779eabfb5f7bfbd69cfc4e967edb808d679f777bc6702c7d")
        self.assertEqual(aes_thuan.giai_cbc(k, iv, ct), pt)

    def test_dem_pkcs7_moi_do_dai(self):
        k, iv = secrets.token_bytes(32), secrets.token_bytes(16)
        for n in (0, 1, 15, 16, 17, 100):
            d = os.urandom(n)
            self.assertEqual(aes_thuan.giai_cbc(k, iv, aes_thuan.ma_cbc(k, iv, d)), d)


class TestGoi(unittest.TestCase):
    def setUp(self):
        self.khoa = secrets.token_bytes(32)
        self.du_lieu = {"ma": "thu", "bai": {"de": "Cơ chế SN2 – tiếng Việt có dấu"}, "kich_ban_3d": {"trang_thai": [1, 2]}, "so_do_giay": {}}

    def test_dong_mo_khop(self):
        g = dong_goi.dong_goi_du_lieu(self.du_lieu, self.khoa)
        self.assertEqual(g[:5], b"VERA1")
        self.assertEqual(g[5], 1)
        self.assertEqual(dong_goi.mo_goi(g, self.khoa), self.du_lieu)

    def test_khong_lo_ban_ro(self):
        g = dong_goi.dong_goi_du_lieu(self.du_lieu, self.khoa)
        self.assertNotIn("tiếng Việt".encode(), g)
        self.assertNotIn(b"trang_thai", g)

    def test_sua_mot_byte_bi_tu_choi(self):
        g = dong_goi.dong_goi_du_lieu(self.du_lieu, self.khoa)
        for vi_tri in (5, 10, 30, dong_goi.DO_DAI_DAU + 3, len(g) - 40, len(g) - 1):
            sua = bytearray(g)
            sua[vi_tri] ^= 0x01
            with self.subTest(vi_tri=vi_tri), self.assertRaises(dong_goi.LoiGoi):
                dong_goi.mo_goi(bytes(sua), self.khoa)

    def test_sai_khoa_bi_tu_choi(self):
        g = dong_goi.dong_goi_du_lieu(self.du_lieu, self.khoa)
        with self.assertRaises(dong_goi.LoiGoi):
            dong_goi.mo_goi(g, secrets.token_bytes(32))

    def test_cat_cut_bi_tu_choi(self):
        g = dong_goi.dong_goi_du_lieu(self.du_lieu, self.khoa)
        for g2 in (g[:-1], g[:40], b"VERA2" + g[5:], b""):
            with self.assertRaises(dong_goi.LoiGoi):
                dong_goi.mo_goi(g2, self.khoa)

    def test_moi_lan_dong_khac_nhau(self):
        a = dong_goi.dong_goi_du_lieu(self.du_lieu, self.khoa)
        b = dong_goi.dong_goi_du_lieu(self.du_lieu, self.khoa)
        self.assertNotEqual(a, b)                                          # salt, IV ngẫu nhiên

    def test_vector_co_dinh_cho_unity(self):
        """Gói cố định (khóa/salt/IV cố định) để C# kiểm chéo: ghi ra tệp nếu đặt VERA_GHI_GOI_MAU."""
        khoa, salt, iv = bytes(range(32)), bytes(range(16)), bytes(range(16, 32))
        g = dong_goi.dong_goi_du_lieu({"ma": "mau", "x": "Ặ ỷ ữ"}, khoa, salt, iv)
        self.assertEqual(dong_goi.mo_goi(g, khoa)["x"], "Ặ ỷ ữ")
        if os.environ.get("VERA_GHI_GOI_MAU"):
            open(os.environ["VERA_GHI_GOI_MAU"], "wb").write(g)


if __name__ == "__main__":
    unittest.main()
