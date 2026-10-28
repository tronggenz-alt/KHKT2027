using System.IO;
using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.ToaNha;
using PhongThiNghiem.ThiNghiem;

namespace PhongThiNghiem.Tests
{
    public class TestToaNha
    {
        private GameObject _goTest;
        private DungToaNha _dungToaNha;

        [SetUp]
        public void ThietLap()
        {
            _goTest = new GameObject("TestToaNha_Root");
            _dungToaNha = _goTest.AddComponent<DungToaNha>();
        }

        [TearDown]
        public void DonDep()
        {
            if (_goTest != null) Object.DestroyImmediate(_goTest);
        }

        [Test]
        public void Test_01_DocJsonToaNha_HopLe()
        {
            _dungToaNha.Dung();
            Assert.IsNotNull(_dungToaNha.DuLieu);

            var tn = _dungToaNha.DuLieu.toa_nha;
            Assert.AreEqual(36f, tn.rong, "Chiều rộng toà nhà phải là 36m");
            Assert.AreEqual(30f, tn.sau, "Chiều sâu toà nhà phải là 30m");
            Assert.AreEqual(8f, tn.cao_sanh, "Trần sảnh phải cao 8m");
            Assert.AreEqual(5f, tn.cao_phong, "Trần phòng thí nghiệm phải cao 5m");

            // Kiểm tra giếng trời sảnh
            var gt = _dungToaNha.DuLieu.gieng_troi;
            Assert.IsNotNull(gt);
            Assert.AreEqual(8f, gt.kich_thuoc[0]);
            Assert.AreEqual(6f, gt.kich_thuoc[1]);

            // Kiểm tra danh sách 7 phòng
            Assert.GreaterOrEqual(_dungToaNha.DuLieu.phong.Count, 7);
            Assert.IsTrue(_dungToaNha.CacPhong.ContainsKey("chuan-do"));
            Assert.IsTrue(_dungToaNha.CacPhong.ContainsKey("nhan-biet"));
            Assert.IsTrue(_dungToaNha.CacPhong.ContainsKey("pin-galvani"));
            Assert.IsTrue(_dungToaNha.CacPhong.ContainsKey("dien-phan"));
            Assert.IsTrue(_dungToaNha.CacPhong.ContainsKey("an-toan"));
            Assert.IsTrue(_dungToaNha.CacPhong.ContainsKey("pha-che"));
            Assert.IsTrue(_dungToaNha.CacPhong.ContainsKey("hoi-thao"));
        }

        [Test]
        public void Test_02_DungToaNha_TaoDayDuThanhPhan()
        {
            _dungToaNha.Dung();
            Assert.IsNotNull(_dungToaNha.GocToaNha);

            // Kiểm tra sàn vinyl
            var san = _dungToaNha.GocToaNha.Find("SanVinylToaNha");
            Assert.IsNotNull(san);

            // Kiểm tra giếng trời
            var gt = _dungToaNha.GocToaNha.Find("KinhGiengTroi_Skylight");
            Assert.IsNotNull(gt);

            // Kiểm tra 5 trạm thí nghiệm
            Assert.AreEqual(5, _dungToaNha.CacBanTram.Count);
            Assert.IsNotNull(_dungToaNha.LayBanTram("chuan-do"));
            Assert.IsNotNull(_dungToaNha.LayBanTram("nhan-biet"));
            Assert.IsNotNull(_dungToaNha.LayBanTram("pin-galvani"));
            Assert.IsNotNull(_dungToaNha.LayBanTram("dien-phan"));
            Assert.IsNotNull(_dungToaNha.LayBanTram("an-toan"));

            // Kiểm tra Bảng tuần hoàn 3D tại sảnh
            var bth = _dungToaNha.GocToaNha.Find("BangTuanHoan3D_Sanh");
            Assert.IsNotNull(bth, "Phải có Bảng tuần hoàn 3D lớn tại sảnh đón tiếp");
        }

        [Test]
        public void Test_03_CuaTruotTuDong_HoatDong()
        {
            var goCua = new GameObject("Cua_Test");
            try
            {
                var cTrai = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cTrai.transform.SetParent(goCua.transform);
                var cPhai = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cPhai.transform.SetParent(goCua.transform);

                var ctrl = goCua.AddComponent<CuaTruotTuDong>();
                ctrl.CanhTrai = cTrai.transform;
                ctrl.CanhPhai = cPhai.transform;
                ctrl.DoMoRong = 0.85f;

                Assert.IsNotNull(ctrl);
                Assert.IsFalse(ctrl.DangMo);
            }
            finally
            {
                Object.DestroyImmediate(goCua);
            }
        }

        [Test]
        public void Test_04_BatTatTranNha_Dollhouse()
        {
            _dungToaNha.Dung();
            Assert.IsNotNull(_dungToaNha.TranNhaTong);

            _dungToaNha.BatTatTranNha(false);
            Assert.IsFalse(_dungToaNha.TranNhaTong.activeSelf, "Phím V (dollhouse) phải ẩn trần nhà");

            _dungToaNha.BatTatTranNha(true);
            Assert.IsTrue(_dungToaNha.TranNhaTong.activeSelf, "Quay lại góc nhìn bình thường phải bật lại trần");
        }
    }
}
