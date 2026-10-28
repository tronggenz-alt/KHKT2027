using System.IO;
using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.ToaNha;
using PhongThiNghiem.NhanVat;

namespace PhongThiNghiem.Tests
{
    public class TestNhanVatVaDieuKhien
    {
        private GameObject _goLab;
        private GameObject _goNv;
        private DungPhongLab _lab;
        private DieuKhienNhanVat _dk;
        private DungNhanVat _hinh;
        private Camera _cam;

        [SetUp]
        public void Setup()
        {
            _goLab = new GameObject("TestPhongLab");
            _lab = _goLab.AddComponent<DungPhongLab>();
            _goLab.AddComponent<PhongThiNghiem.ThiNghiem.QuanLyTram>();
            string banDoPath = Path.Combine(Application.streamingAssetsPath, "ptn/ban-do.json");
            _lab.DungToanBoPhong(banDoPath);

            var goCam = new GameObject("MainCamera");
            _cam = goCam.AddComponent<Camera>();
            goCam.tag = "MainCamera";

            _goNv = new GameObject("NhanVatGiaoSu");
            _goNv.transform.position = new Vector3(11f, 0.05f, 13.6f); // Điểm xuất phát
            _hinh = _goNv.AddComponent<DungNhanVat>();
            _dk = _goNv.AddComponent<DieuKhienNhanVat>();
            _dk.CameraChinh = _cam;
        }

        [TearDown]
        public void Teardown()
        {
            if (_goNv != null) Object.DestroyImmediate(_goNv);
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
            if (_goLab != null) Object.DestroyImmediate(_goLab);
        }

        [Test]
        public void Test_01_DungHinhGiaoSu_VaTrangBiBaoHo()
        {
            Assert.IsNotNull(_hinh.Than, "Phần thân phải tồn tại");
            Assert.IsNotNull(_hinh.Dau, "Phần đầu phải tồn tại");
            Assert.IsNotNull(_hinh.CanhTayTrai, "Cánh tay trái phải tồn tại");
            Assert.IsNotNull(_hinh.CanhTayPhai, "Cánh tay phải phải tồn tại");
            Assert.IsNotNull(_hinh.DuiTrai, "Đùi trái phải tồn tại");
            Assert.IsNotNull(_hinh.DuiPhai, "Đùi phải phải tồn tại");
            Assert.IsNotNull(_hinh.KinhBaoHo, "Kính bảo hộ phải tồn tại");

            // Kiểm tra ẩn đầu ở góc nhìn thứ nhất
            _hinh.SetFirstPerson(true);
            Assert.IsFalse(_hinh.Dau.gameObject.activeSelf, "Góc nhìn thứ nhất phải ẩn đầu");

            _hinh.SetFirstPerson(false);
            Assert.IsTrue(_hinh.Dau.gameObject.activeSelf, "Góc nhìn thứ ba phải hiện đầu");

            // Tương tác đeo bảo hộ
            Assert.IsFalse(_hinh.DaDeoBaoHo, "Ban đầu chưa đeo găng tay");
            _hinh.DeoDoBaoHo();
            Assert.IsTrue(_hinh.DaDeoBaoHo, "Sau khi tương tác phải có trạng thái đã đeo đồ bảo hộ");
        }

        [Test]
        public void Test_02_CheDoCamera_Goc3_Goc1_NhaBupBe()
        {
            _dk.CheDoCam = DieuKhienNhanVat.CheDoCamera.NgoiThuBa;
            Assert.AreEqual(DieuKhienNhanVat.CheDoCamera.NgoiThuBa, _dk.CheDoCam);

            // Chuyển sang góc 1
            _dk.CheDoCam = DieuKhienNhanVat.CheDoCamera.NgoiThuNhat;
            Assert.AreEqual(DieuKhienNhanVat.CheDoCamera.NgoiThuNhat, _dk.CheDoCam);

            // Chuyển sang toàn cảnh nhà búp bê
            _dk.CheDoCam = DieuKhienNhanVat.CheDoCamera.ToanCanhNhaBupBe;
            Assert.AreEqual(DieuKhienNhanVat.CheDoCamera.ToanCanhNhaBupBe, _dk.CheDoCam);
        }

        [Test]
        public void Test_03_VaoBan_Va_RoiBan()
        {
            Assert.IsFalse(_dk.TaiBan, "Ban đầu không ở tại bàn");

            _dk.VaoBan("chuan-do");
            Assert.IsTrue(_dk.TaiBan, "Sau khi VaoBan phải ở trạng thái TaiBan");
            Assert.AreEqual("chuan-do", _dk.TramHienTai, "Trạm hiện tại phải là chuan-do");
            Assert.AreEqual(DieuKhienNhanVat.CheDoCamera.CanCanhBan, _dk.CheDoCam, "Camera phải chuyển sang CanCanhBan");

            // Vị trí nhân vật phải khớp với diem_dung của chuan-do
            Assert.AreEqual(3.5f, _goNv.transform.position.x, 0.1f, "X nhân vật phải ở điểm dừng");
            Assert.AreEqual(12.7f, _goNv.transform.position.z, 0.1f, "Z nhân vật phải ở điểm dừng");

            _dk.RoiBan();
            Assert.IsFalse(_dk.TaiBan, "Sau khi RoiBan không còn ở tại bàn");
            Assert.AreEqual(DieuKhienNhanVat.CheDoCamera.NgoiThuBa, _dk.CheDoCam, "Camera phải quay lại NgoiThuBa");
        }
    }
}
