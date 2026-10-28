using System.IO;
using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.ThiNghiem;

namespace PhongThiNghiem.Tests
{
    public class TestBangTuanHoan
    {
        private GameObject _goBTH;
        private BangTuanHoan3D _bth;

        [SetUp]
        public void ThietLap()
        {
            _goBTH = new GameObject("BangTuanHoan_Test");
            _bth = _goBTH.AddComponent<BangTuanHoan3D>();
        }

        [TearDown]
        public void DonDep()
        {
            if (_goBTH != null) Object.DestroyImmediate(_goBTH);
        }

        [Test]
        public void Test_01_NapDuLieu_Du118NguyenTo()
        {
            Assert.IsNotNull(_bth);
            _bth.NapDuLieu();

            Assert.AreEqual(118, _bth.DanhSach.Count, "Bảng tuần hoàn phải có đủ 118 nguyên tố!");
            Assert.AreEqual(118, _bth.BangTraCuu.Count, "Bảng tra cứu phải có đủ 118 Z!");

            // Kiểm tra Z từ 1 đến 118 liên tục
            for (int z = 1; z <= 118; z++)
            {
                Assert.IsTrue(_bth.BangTraCuu.ContainsKey(z), $"Thiếu nguyên tố Z = {z}");
                Assert.AreEqual(z, _bth.BangTraCuu[z].z);
                Assert.IsFalse(string.IsNullOrEmpty(_bth.BangTraCuu[z].ki_hieu));
            }
        }

        [Test]
        public void Test_02_KiemTraThuocTinhCacNguyenToQuanTrong()
        {
            _bth.NapDuLieu();

            // 1. Hydrogen (Z=1)
            var h = _bth.BangTraCuu[1];
            Assert.AreEqual("H", h.ki_hieu);
            Assert.AreEqual(1, h.chu_ky);
            Assert.AreEqual(1, h.nhom);
            Assert.AreEqual("s", h.khoi);
            Assert.AreEqual("phi_kim", h.loai);

            // 2. Helium (Z=2)
            var he = _bth.BangTraCuu[2];
            Assert.AreEqual("He", he.ki_hieu);
            Assert.AreEqual(1, he.chu_ky);
            Assert.AreEqual(18, he.nhom);
            Assert.AreEqual("khi_hiem", he.loai);

            // 3. Carbon (Z=6)
            var c = _bth.BangTraCuu[6];
            Assert.AreEqual("C", c.ki_hieu);
            Assert.AreEqual(2, c.chu_ky);
            Assert.AreEqual(14, c.nhom);
            Assert.AreEqual("IVA", c.nhom_ab);

            // 4. Sodium / Natri (Z=11)
            var na = _bth.BangTraCuu[11];
            Assert.AreEqual("Na", na.ki_hieu);
            Assert.AreEqual("Natri", na.ten_viet);
            Assert.AreEqual(3, na.chu_ky);
            Assert.AreEqual(1, na.nhom);
            Assert.AreEqual("IA", na.nhom_ab);
            Assert.AreEqual("kim_loai_kiem", na.loai);

            // 5. Iron / Sắt (Z=26)
            var fe = _bth.BangTraCuu[26];
            Assert.AreEqual("Fe", fe.ki_hieu);
            Assert.AreEqual("Sắt", fe.ten_viet);
            Assert.AreEqual(4, fe.chu_ky);
            Assert.AreEqual(8, fe.nhom);
            Assert.AreEqual("d", fe.khoi);
            Assert.AreEqual("chuyen_tiep", fe.loai);

            // 6. Copper / Đồng (Z=29)
            var cu = _bth.BangTraCuu[29];
            Assert.AreEqual("Cu", cu.ki_hieu);
            Assert.AreEqual(4, cu.chu_ky);
            Assert.AreEqual(11, cu.nhom);

            // 7. Lead / Chì (Z=82)
            var pb = _bth.BangTraCuu[82];
            Assert.AreEqual("Pb", pb.ki_hieu);
            Assert.AreEqual("Chì", pb.ten_viet);
            Assert.AreEqual(6, pb.chu_ky);
            Assert.AreEqual(14, pb.nhom);

            // 8. Oganesson (Z=118)
            var og = _bth.BangTraCuu[118];
            Assert.AreEqual("Og", og.ki_hieu);
            Assert.AreEqual(7, og.chu_ky);
            Assert.AreEqual(18, og.nhom);
        }

        [Test]
        public void Test_03_TinhElectronCacLop_MoHinhBohr()
        {
            // Kiểm tra phân bố lớp electron mô hình Bohr cho các nguyên tố mẫu
            int[] eH = BangTuanHoan3D.TinhElectronCacLop(1);
            Assert.AreEqual(1, eH[0]);

            int[] eHe = BangTuanHoan3D.TinhElectronCacLop(2);
            Assert.AreEqual(2, eHe[0]);

            int[] eNa = BangTuanHoan3D.TinhElectronCacLop(11);
            Assert.AreEqual(2, eNa[0]);
            Assert.AreEqual(8, eNa[1]);
            Assert.AreEqual(1, eNa[2]);

            int[] eFe = BangTuanHoan3D.TinhElectronCacLop(26);
            Assert.AreEqual(2, eFe[0]);
            Assert.AreEqual(8, eFe[1]);
            Assert.AreEqual(14, eFe[2]);
            Assert.AreEqual(2, eFe[3]);

            int[] eCu = BangTuanHoan3D.TinhElectronCacLop(29);
            Assert.AreEqual(2, eCu[0]);
            Assert.AreEqual(8, eCu[1]);
            Assert.AreEqual(18, eCu[2]);
            Assert.AreEqual(1, eCu[3]);

            // Kiểm tra mọi Z từ 1 đến 118, tổng số electron các lớp phải bằng Z
            for (int z = 1; z <= 118; z++)
            {
                int[] eLop = BangTuanHoan3D.TinhElectronCacLop(z);
                int tongE = 0;
                for (int l = 0; l < eLop.Length; l++) tongE += eLop[l];
                Assert.AreEqual(z, tongE, $"Lỗi tổng số electron Bohr cho Z = {z}");
            }
        }

        [Test]
        public void Test_04_TaoBang3DSanh_Va_Poster()
        {
            var gocGiaLap = new GameObject("GocPhong_Test");
            try
            {
                var bang3D = BangTuanHoan3D.TaoBangSanh(gocGiaLap.transform);
                Assert.IsNotNull(bang3D);

                // Kiểm tra có nhóm 118 ô nguyên tố
                var goNhom = bang3D.transform.Find("CacONguyenTo_3D");
                Assert.IsNotNull(goNhom);
                Assert.AreEqual(118, goNhom.childCount, "Bảng 3D phải tạo đủ 118 ô nguyên tố!");

                // Kiểm tra ô đầu tiên và ô cuối cùng
                var oH = goNhom.Find("O_1_H");
                Assert.IsNotNull(oH);
                var oOg = goNhom.Find("O_118_Og");
                Assert.IsNotNull(oOg);

                // Kiểm tra tạo poster phòng
                var poster = BangTuanHoan3D.TaoPosterPhong(gocGiaLap.transform, new Vector3(3f, 2f, 0f), Vector3.zero);
                Assert.IsNotNull(poster);
            }
            finally
            {
                Object.DestroyImmediate(gocGiaLap);
            }
        }

        [Test]
        public void Test_05_LocKhoi_Va_CheDoToMau()
        {
            _bth.NapDuLieu();
            var na = _bth.BangTraCuu[11]; // Khối s
            var fe = _bth.BangTraCuu[26]; // Khối d

            // 1. Chế độ lọc khối
            _bth.LocKhoiHienTai = LocKhoi.Khoi_s;
            Color mauNa_s = _bth.LayMauTheoCheDo(na);
            Color mauFe_s = _bth.LayMauTheoCheDo(fe);
            Assert.AreEqual(0.35f, mauFe_s.a, 0.05f, "Fe không thuộc khối s phải bị mờ (alpha ~ 0.35)");
            Assert.Greater(mauNa_s.a, 0.9f, "Na thuộc khối s phải sáng rõ");

            // 2. Chế độ màu theo trạng thái
            _bth.LocKhoiHienTai = LocKhoi.TatCa;
            _bth.CheDoMauHienTai = CheDoToMau.TheoTrangThai;
            Color mauH = _bth.LayMauTheoCheDo(_bth.BangTraCuu[1]); // Khí -> Cyan
            Color mauHg = _bth.LayMauTheoCheDo(_bth.BangTraCuu[80]); // Lỏng -> Hồng cam
            Assert.AreNotEqual(mauH, mauHg);

            // 3. Chế độ màu theo phân loại
            _bth.CheDoMauHienTai = CheDoToMau.TheoLoai;
            Color mauKiem = BangTuanHoan3D.LayMauLoai("kim_loai_kiem");
            Color mauHalogen = BangTuanHoan3D.LayMauLoai("halogen");
            Assert.AreNotEqual(mauKiem, mauHalogen);
        }
    }
}
