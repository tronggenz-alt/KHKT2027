using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.ThiNghiem;

namespace PhongThiNghiem.Tests
{
    public class TestDungCuISO
    {
        private GameObject _goGoc;

        [SetUp]
        public void ThietLap()
        {
            _goGoc = new GameObject("DungCuISO_TestRoot");
        }

        [TearDown]
        public void DonDep()
        {
            if (_goGoc != null) Object.DestroyImmediate(_goGoc);
        }

        [Test]
        public void Test_01_Burette50mL_KichThuocChuanISO385()
        {
            var bur = DungCuISO.TaoBurette50mL(_goGoc.transform, Vector3.zero);
            Assert.IsNotNull(bur);

            // Kiểm tra thân ống dài 75 cm (0.75m)
            var than = bur.transform.Find("ThanOngBurette");
            Assert.IsNotNull(than);
            Assert.AreEqual(0.014f, than.localScale.x, 0.001f, "Đường kính burette chuẩn 14mm");
            Assert.AreEqual(0.375f, than.localScale.y, 0.001f, "Bán chiều cao thân trụ 0.375m (tổng dài 0.75m)");

            // Kiểm tra khoá van Teflon có gắn điểm nóng tương tác
            var khoa = bur.transform.Find("KhoaVoiTeflon");
            Assert.IsNotNull(khoa);
            var dn = khoa.GetComponentInChildren<DiemNongTuongTac>();
            Assert.IsNotNull(dn, "Khoá vòi burette phải có điểm nóng tương tác ①");
        }

        [Test]
        public void Test_02_BinhTamGiac250mL_ChuanISO1773()
        {
            var binh = DungCuISO.TaoBinhTamGiac250mL(_goGoc.transform, Vector3.zero, 0.04f, Color.magenta);
            Assert.IsNotNull(binh);

            // Kiểm tra đáy bình 8.5 cm
            var than = binh.transform.Find("ThanNon");
            Assert.IsNotNull(than);
            Assert.AreEqual(0.085f, than.localScale.x, 0.002f, "Đáy bình tam giác 8.5 cm chuẩn ISO 1773");

            // Kiểm tra cổ bình 3.4 cm
            var co = binh.transform.Find("CoBinh");
            Assert.IsNotNull(co);
            Assert.AreEqual(0.034f, co.localScale.x, 0.002f, "Cổ bình tam giác 3.4 cm chuẩn ISO 1773");

            // Kiểm tra dung dịch mặt khum meniscus
            var dd = binh.transform.Find("DungDich_Meniscus");
            Assert.IsNotNull(dd, "Phải có dung dịch bên trong bình tam giác");
        }

        [Test]
        public void Test_03_CocDot250mL_ChuanISO3819()
        {
            var coc = DungCuISO.TaoCocDot250mL(_goGoc.transform, Vector3.zero, 0.05f);
            Assert.IsNotNull(coc);

            var than = coc.transform.Find("ThanCoc");
            Assert.IsNotNull(than);
            Assert.AreEqual(0.070f, than.localScale.x, 0.002f, "Đường kính cốc 250mL chuẩn 7.0 cm ISO 3819");
            Assert.AreEqual(0.0475f, than.localScale.y, 0.002f, "Bán chiều cao 0.0475m (tổng cao 9.5 cm)");

            // Có mỏ rót
            var mo = coc.transform.Find("MoRot");
            Assert.IsNotNull(mo, "Cốc đong phải có mỏ rót");
        }

        [Test]
        public void Test_04_GiaOngNghiem_Va_CauMuoi()
        {
            // 1. Giá ống nghiệm gỗ 6 lỗ + 4 ống nghiệm 16 × 150 mm
            var gia = DungCuISO.TaoGiaOngNghiem6Lo(_goGoc.transform, Vector3.zero);
            Assert.IsNotNull(gia);
            var o1 = gia.transform.Find("OngNghiem_1");
            Assert.IsNotNull(o1);
            Assert.AreEqual(0.016f, o1.localScale.x, 0.002f, "Đường kính ống nghiệm chuẩn 16 mm ISO 4142");
            Assert.AreEqual(0.075f, o1.localScale.y, 0.002f, "Bán chiều cao 0.075m (dài 150 mm)");

            // 2. Cầu muối chữ U
            var cm = DungCuISO.TaoCauMuoiChuU(_goGoc.transform, Vector3.zero);
            Assert.IsNotNull(cm);
            var dn = cm.GetComponentInChildren<DiemNongTuongTac>();
            Assert.IsNotNull(dn, "Cầu muối phải có điểm nóng tương tác ②");
        }

        [Test]
        public void Test_05_BotKhi_Va_KetTua()
        {
            var goBot = new GameObject("BotKhi_Test");
            goBot.transform.SetParent(_goGoc.transform);

            var bk = goBot.AddComponent<BotKhiThiNghiem>();
            bk.Loai = BotKhiThiNghiem.LoaiHieuUng.BotKhiDienPhan;
            bk.CuongDo = 2.0f;
            bk.SoLuongHat = 16;
            bk.KhoiTaoCacHat();

            Assert.AreEqual(16, goBot.transform.childCount, "Phải khởi tạo đủ 16 hạt bọt khí");

            // Thay đổi cường độ dòng điện
            bk.DatCuongDo(3.5f);
            Assert.AreEqual(3.5f, bk.CuongDo);

            // Kiểm tra loại kết tủa lắng
            bk.Loai = BotKhiThiNghiem.LoaiHieuUng.KetTuaLang;
            bk.KhoiTaoCacHat();
            Assert.AreEqual(16, goBot.transform.childCount);
        }
    }
}
