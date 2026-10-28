using System.IO;
using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.NhanVat;

namespace PhongThiNghiem.Tests
{
    public class TestBangTrangThaiNhanVat
    {
        [SetUp]
        public void Setup()
        {
            BangTrangThaiNhanVat.DatLaiMacDinh();
        }

        [Test]
        public void Test_01_AnhXaMacDinh_TatCaSuKien()
        {
            // Các sự kiện ban đầu
            Assert.AreEqual("wave", BangTrangThaiNhanVat.AnhXa(SuKienLab.MoApp));
            Assert.AreEqual("wave", BangTrangThaiNhanVat.AnhXa(SuKienLab.VaoToaNha));
            Assert.AreEqual("nod", BangTrangThaiNhanVat.AnhXa(SuKienLab.VaoBanThiNghiem));
            Assert.AreEqual("point", BangTrangThaiNhanVat.AnhXa(SuKienLab.BamDiemNong));
            Assert.AreEqual("point_back", BangTrangThaiNhanVat.AnhXa(SuKienLab.MoBangTuanHoan));
            Assert.AreEqual("think", BangTrangThaiNhanVat.AnhXa(SuKienLab.ChoKetQua));
            Assert.AreEqual("happy", BangTrangThaiNhanVat.AnhXa(SuKienLab.TraLoiDung));
            Assert.AreEqual("think", BangTrangThaiNhanVat.AnhXa(SuKienLab.TraLoiSai));
            Assert.AreEqual("ask", BangTrangThaiNhanVat.AnhXa(SuKienLab.HoiGoiY));
            Assert.AreEqual("typing", BangTrangThaiNhanVat.AnhXa(SuKienLab.BanMayTinh));
            Assert.AreEqual("search_files", BangTrangThaiNhanVat.AnhXa(SuKienLab.TuHoSo));
            Assert.AreEqual("walk", BangTrangThaiNhanVat.AnhXa(SuKienLab.DiBo));
            Assert.AreEqual("running", BangTrangThaiNhanVat.AnhXa(SuKienLab.Chay));
            Assert.AreEqual("jump", BangTrangThaiNhanVat.AnhXa(SuKienLab.Nhay));
            Assert.AreEqual("falling_idle", BangTrangThaiNhanVat.AnhXa(SuKienLab.RoiTuDo));
            Assert.AreEqual("landing", BangTrangThaiNhanVat.AnhXa(SuKienLab.TiepDat));

            // Thao tác bàn thí nghiệm mới (PROMPT mục 5.3)
            Assert.AreEqual("reaching", BangTrangThaiNhanVat.AnhXa(SuKienLab.MoKhoaBurette));
            Assert.AreEqual("reaching", BangTrangThaiNhanVat.AnhXa(SuKienLab.NhoThuocThu));
            Assert.AreEqual("picking_up", BangTrangThaiNhanVat.AnhXa(SuKienLab.CamOngNghiem));
            Assert.AreEqual("putting_down", BangTrangThaiNhanVat.AnhXa(SuKienLab.DatDungCu));
            Assert.AreEqual("button_push", BangTrangThaiNhanVat.AnhXa(SuKienLab.BatNguonDC));
            Assert.AreEqual("button_push", BangTrangThaiNhanVat.AnhXa(SuKienLab.BamDongHoDo));
            Assert.AreEqual("looking_down", BangTrangThaiNhanVat.AnhXa(SuKienLab.ChoPhanUng));
            Assert.AreEqual("surprised", BangTrangThaiNhanVat.AnhXa(SuKienLab.XuatHienKetTua));
            Assert.AreEqual("surprised", BangTrangThaiNhanVat.AnhXa(SuKienLab.DoiMauDungDich));
            Assert.AreEqual("surprised", BangTrangThaiNhanVat.AnhXa(SuKienLab.SuiBotKhi));
            Assert.AreEqual("thumbs_up", BangTrangThaiNhanVat.AnhXa(SuKienLab.DungMotBuoc));
            Assert.AreEqual("clapping", BangTrangThaiNhanVat.AnhXa(SuKienLab.HoanThanhThiNghiem));
            Assert.AreEqual("shaking_head_no", BangTrangThaiNhanVat.AnhXa(SuKienLab.SaiThaoTac));
            Assert.AreEqual("shaking_head_no", BangTrangThaiNhanVat.AnhXa(SuKienLab.QuenDoBaoHo));
            Assert.AreEqual("disappointed", BangTrangThaiNhanVat.AnhXa(SuKienLab.TraLoiSaiOnThi));

            // Di chuyển & quay tại chỗ
            Assert.AreEqual("start_walking", BangTrangThaiNhanVat.AnhXa(SuKienLab.BatDauDi));
            Assert.AreEqual("stop_walking", BangTrangThaiNhanVat.AnhXa(SuKienLab.DungLai));
            Assert.AreEqual("turn_left_90", BangTrangThaiNhanVat.AnhXa(SuKienLab.QuayTrai90));
            Assert.AreEqual("turn_right_90", BangTrangThaiNhanVat.AnhXa(SuKienLab.QuayPhai90));
            Assert.AreEqual("walk_backward", BangTrangThaiNhanVat.AnhXa(SuKienLab.DiLui));
            Assert.AreEqual("strafe_left", BangTrangThaiNhanVat.AnhXa(SuKienLab.SangTrai));
            Assert.AreEqual("strafe_right", BangTrangThaiNhanVat.AnhXa(SuKienLab.SangPhai));

            // Ngồi & Đứng chờ
            Assert.AreEqual("stand_to_sit", BangTrangThaiNhanVat.AnhXa(SuKienLab.NgoiXuong));
            Assert.AreEqual("sitting_idle", BangTrangThaiNhanVat.AnhXa(SuKienLab.DangNgoi));
            Assert.AreEqual("sit_to_stand", BangTrangThaiNhanVat.AnhXa(SuKienLab.DungDay));
            Assert.AreEqual("sitting_talking", BangTrangThaiNhanVat.AnhXa(SuKienLab.NgoiNoiChuyen));
            Assert.AreEqual("looking_around", BangTrangThaiNhanVat.AnhXa(SuKienLab.NhinQuanh));
            Assert.AreEqual("weight_shift", BangTrangThaiNhanVat.AnhXa(SuKienLab.DonTrongTam));
            Assert.AreEqual("checking_watch", BangTrangThaiNhanVat.AnhXa(SuKienLab.XemDongHo));
            Assert.AreEqual("scratching_head", BangTrangThaiNhanVat.AnhXa(SuKienLab.GaiDau));
            Assert.AreEqual("stretching", BangTrangThaiNhanVat.AnhXa(SuKienLab.VuonVai));

            // Trường hợp không xác định hoặc rỗng trả về idle
            Assert.AreEqual("idle", BangTrangThaiNhanVat.LayClipChoSuKien(null));
            Assert.AreEqual("idle", BangTrangThaiNhanVat.LayClipChoSuKien(""));
            Assert.AreEqual("idle", BangTrangThaiNhanVat.LayClipChoSuKien("SuKienKhongTonTai"));
        }

        [Test]
        public void Test_02_NapTuManifestJson()
        {
            string jsonTest = @"{
                ""id"": ""nha_nghien_cuu_nam"",
                ""stateMap"": {
                    ""MoApp"": ""wave"",
                    ""VaoBanThiNghiem"": ""nod"",
                    ""TuyChonMoi"": ""celebrate""
                }
            }";

            bool ketQua = BangTrangThaiNhanVat.DocTuManifest(jsonTest);
            Assert.IsTrue(ketQua, "Phải phân tích thành công JSON manifest");
            Assert.AreEqual("celebrate", BangTrangThaiNhanVat.LayClipChoSuKien("TuyChonMoi"));

            // Đọc từ file manifest thực tế trong StreamingAssets nếu có
            string duongDan = Path.Combine(Application.streamingAssetsPath, "ptn/nhan-vat-nha-nghien-cuu.json");
            if (File.Exists(duongDan))
            {
                string jsonThuc = File.ReadAllText(duongDan);
                bool docThuc = BangTrangThaiNhanVat.DocTuManifest(jsonThuc);
                Assert.IsTrue(docThuc, "Đọc manifest thực tế từ StreamingAssets phải thành công");
                Assert.AreEqual("nod", BangTrangThaiNhanVat.LayClipChoSuKien("VaoBanThiNghiem"));
            }
        }

        [Test]
        public void Test_03_DieuKhienNhanVat_AnDauNgoiThuNhat_VaCuChi()
        {
            var go = new GameObject("TestNhanVat");
            var hinh = go.AddComponent<DungNhanVat>();
            var dk = go.AddComponent<DieuKhienNhanVat>();

            // Test gọi Đặt cử chỉ không văng exception
            Assert.DoesNotThrow(() => dk.DatCuChi(SuKienLab.MoApp));
            Assert.DoesNotThrow(() => dk.DatCuChi(SuKienLab.VaoBanThiNghiem));
            Assert.DoesNotThrow(() => dk.DatCuChi(SuKienLab.MoKhoaBurette));
            Assert.DoesNotThrow(() => dk.DatCuChi(SuKienLab.HoanThanhThiNghiem));

            // Test chuyển camera Ngôi thứ nhất ẩn đầu (scale = 0)
            dk.DatCheDoCamera(DieuKhienNhanVat.CheDoCamera.NgoiThuNhat);
            Assert.AreEqual(Vector3.zero, hinh.Dau.localScale, "Ngôi thứ nhất đầu phải co về 0 để không che camera");

            // Ngôi thứ ba hiện đầu (scale = 1)
            dk.DatCheDoCamera(DieuKhienNhanVat.CheDoCamera.NgoiThuBa);
            Assert.AreEqual(Vector3.one, hinh.Dau.localScale, "Ngôi thứ ba đầu phải khôi phục tỉ lệ 1");

            Object.DestroyImmediate(go);
        }
    }
}
