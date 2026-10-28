using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.Loi;

namespace PhongThiNghiem.Tests
{
    public class TestLoiHoaHoc
    {
        private LoiHoaHoc _loi;
        private string _thuMucPtn;

        [SetUp]
        public void Setup()
        {
            _thuMucPtn = Path.Combine(Application.streamingAssetsPath, "ptn");
            _loi = new LoiHoaHoc(_thuMucPtn);
        }

        [TearDown]
        public void Teardown()
        {
            _loi?.Dispose();
        }

        [Test]
        public void Test_01_pH_HCl_Manh()
        {
            // Ca = 0.1, Va = 20, Cb = 0.1
            double[] theTich = { 0.0, 10.0, 19.9, 20.0, 20.1, 30.0 };
            double[] phMongMuon = { 1.00, 1.48, 3.60, 7.00, 10.40, 12.30 };

            for (int i = 0; i < theTich.Length; i++)
            {
                double ph = _loi.TinhToan_pHChuanDo(0.1, 20.0, 0.1, theTich[i], null);
                Assert.AreEqual(phMongMuon[i], ph, 0.02, $"pH tại V={theTich[i]} không khớp!");
            }
        }

        [Test]
        public void Test_02_pH_CH3COOH_Yeu()
        {
            // Ca = 0.1, Va = 20, Cb = 0.1, Ka = 1.75e-5
            double[] theTich = { 0.0, 10.0, 20.0 };
            double[] phMongMuon = { 2.88, 4.76, 8.73 };

            for (int i = 0; i < theTich.Length; i++)
            {
                double ph = _loi.TinhToan_pHChuanDo(0.1, 20.0, 0.1, theTich[i], 1.75e-5);
                Assert.AreEqual(phMongMuon[i], ph, 0.02, $"pH CH3COOH tại V={theTich[i]} không khớp!");
            }
        }

        [Test]
        public void Test_03_DienPhan_CuSO4()
        {
            // I = 2 A, t = 965 s -> m_Cu = 0.64 g
            double mCu = _loi.TinhToan_DienPhanCu(2.0, 965.0);
            Assert.AreEqual(0.64, mCu, 0.02, "Khối lượng Cu điện phân không khớp 0.64 g");
        }

        [Test]
        public void Test_04_Pin_Zn_Cu()
        {
            // Pin Zn - Cu: E0 = 1.103 V
            double e0 = _loi.TinhToan_PinZnCu();
            Assert.AreEqual(1.103, e0, 0.001, "Suất điện động chuẩn pin Zn-Cu không khớp 1.103 V");
        }

        [Test]
        public void Test_05_MoPhong_ChuanDo_Phenolphthalein_ChuyenHong()
        {
            _loi.Tao("chuan-do", "huong_dan");
            _loi.ThaoTacDongBo("chon_chi_thi", "{\"id\":\"phenolphthalein\"}");
            
            // Ban đầu chưa thêm NaOH: pH ~ 1.0, màu không màu
            string ttJsonBanDau = _loi.TrangThai();
            Assert.IsTrue(ttJsonBanDau.Contains("không màu") || ttJsonBanDau.Contains("\"pH\":1"), "Dung dịch ban đầu phải không màu");

            // Thêm 10 mL NaOH lần 1
            _loi.ThaoTacDongBo("them", "{\"ml\":10}");
            // Thêm 10 mL NaOH lần 2 (đạt đúng 20.0 mL điểm tương đương, pH = 7)
            _loi.ThaoTacDongBo("them", "{\"ml\":10}");
            // Nhỏ thêm 1 giọt (0.05 mL -> V = 20.05 mL, pH vọt lên 10.1)
            _loi.ThaoTacDongBo("nho_giot", "{}");
            _loi.ThaoTacDongBo("lac", "{}");

            string ttJsonSau = _loi.TrangThai();
            Assert.IsTrue(ttJsonSau.Contains("hồng"), "Dung dịch phải chuyển sang màu hồng tại V ~ 20.0 mL!");
        }

        [Test]
        public void Test_06_BaoDam_Sha256_TrungKhop_VoiWeb()
        {
            string repoPtn = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents/Antigravity/frontend/hoa-hoc/phong-thi-nghiem"));
            string[] tapTin = { "tinh-toan.js", "mo-phong.js", "bang-ion.json", "ban-do.json" };

            using (var sha = SHA256.Create())
            {
                foreach (var f in tapTin)
                {
                    string fileWeb = Path.Combine(repoPtn, f);
                    string fileApp = Path.Combine(_thuMucPtn, f);

                    Assert.IsTrue(File.Exists(fileWeb), $"Thiếu file web: {fileWeb}");
                    Assert.IsTrue(File.Exists(fileApp), $"Thiếu file app: {fileApp}");

                    byte[] hashWeb = sha.ComputeHash(File.ReadAllBytes(fileWeb));
                    byte[] hashApp = sha.ComputeHash(File.ReadAllBytes(fileApp));

                    string hexWeb = BitConverter.ToString(hashWeb);
                    string hexApp = BitConverter.ToString(hashApp);

                    Assert.AreEqual(hexWeb, hexApp, $"File {f} không khớp SHA256 giữa Web và App!");
                }
            }
        }
    }
}
