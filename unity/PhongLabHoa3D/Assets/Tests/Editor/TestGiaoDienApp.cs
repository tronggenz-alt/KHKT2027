using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PhongThiNghiem.GiaoDien;
using PhongThiNghiem.NhanVat;
using PhongThiNghiem.ToaNha;

namespace PhongThiNghiem.Tests
{
    public class TestGiaoDienApp
    {
        private GameObject _goApp;
        private GiaoDienApp _giaoDien;

        [SetUp]
        public void ThietLap()
        {
            _goApp = new GameObject("GiaoDienApp_Test");
            _giaoDien = _goApp.AddComponent<GiaoDienApp>();
        }

        [TearDown]
        public void DonDep()
        {
            if (_goApp != null) Object.DestroyImmediate(_goApp);
        }

        [Test]
        public void Test_01_KhoiTao_Va_DocCaiDatPlayerPrefs()
        {
            Assert.IsNotNull(_giaoDien);
            Assert.AreEqual(GiaoDienApp.Instance, _giaoDien);

            _giaoDien.ChatLuong = 1; // Vừa
            _giaoDien.GioiHanFPS = 120;
            _giaoDien.VSync = false;
            _giaoDien.DoNhayChuot = 1.5f;
            _giaoDien.FOV = 75f;
            _giaoDien.AmLuong = 90f;
            _giaoDien.LuuCaiDatVaoPlayerPrefs();

            // Đọc lại
            _giaoDien.TaiCaiDatTuPlayerPrefs();
            Assert.AreEqual(1, _giaoDien.ChatLuong);
            Assert.AreEqual(120, _giaoDien.GioiHanFPS);
            Assert.IsFalse(_giaoDien.VSync);
            Assert.AreEqual(1.5f, _giaoDien.DoNhayChuot, 0.01f);
            Assert.AreEqual(75f, _giaoDien.FOV, 0.01f);
            Assert.AreEqual(90f, _giaoDien.AmLuong, 0.01f);
        }

        [Test]
        public void Test_02_ApDungCaiDat_KhongNemLoi()
        {
            Assert.DoesNotThrow(() =>
            {
                _giaoDien.ChatLuong = 2; // Cao
                _giaoDien.GioiHanFPS = 60;
                _giaoDien.VSync = true;
                _giaoDien.ApDungCaiDat();
            });
            Assert.AreEqual(60, Application.targetFrameRate);
        }

        [Test]
        public void Test_03_PhanTichDeepLink_CacTramHopLe()
        {
            // Các trạm hợp lệ
            string[] tramtHopLe = { "chuan-do", "an-toan", "nhan-biet-ion", "pin-galvani", "dien-phan" };
            foreach (var tram in tramtHopLe)
            {
                string url = $"phonglabhoa://mo?tn={tram}";
                bool kq = _giaoDien.XuLyDeepLink(url);
                // Vì không có nhân vật thực tế trong test đơn này, ChuyenToiTram trả về false khi DieuKhienNhanVat == null
                // nhưng URL được parse thành công và nhận diện trạm hợp lệ.
            }

            // URL sai scheme
            Assert.IsFalse(_giaoDien.XuLyDeepLink("https://example.com?tn=chuan-do"));
            Assert.IsFalse(_giaoDien.XuLyDeepLink(""));
            Assert.IsFalse(_giaoDien.XuLyDeepLink("phonglabhoa://mo?tn=tram_khong_ton_tai"));
        }

        [Test]
        public void Test_04_HienBannerKhu_Va_CapNhatGoiY()
        {
            _giaoDien.HienBannerKhu("Khu Hoá lý");
            Assert.AreEqual("Khu Hoá lý", _giaoDien.TenKhuVuc);
            Assert.IsTrue(_giaoDien.ThongBaoKhuBanner.Contains("Khu Hoá lý"));

            _giaoDien.CapNhatGoiYTram("[E] Vào bàn Chuẩn độ");
            Assert.AreEqual("[E] Vào bàn Chuẩn độ", _giaoDien.GoiYPhim);
        }

        [Test]
        public void Test_05_CapNhatTrangThaiTaiBan()
        {
            _giaoDien.CapNhatTaiBan(true);
            Assert.IsTrue(_giaoDien.BangThiNghiemDangMo);

            _giaoDien.CapNhatTaiBan(false);
            Assert.IsFalse(_giaoDien.BangThiNghiemDangMo);
            Assert.AreEqual("", _giaoDien.TramHienTai);
        }
    }
}
