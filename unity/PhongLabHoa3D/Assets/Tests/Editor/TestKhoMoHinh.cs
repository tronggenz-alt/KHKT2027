using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.ThiNghiem;

namespace PhongThiNghiem.Tests
{
    [TestFixture]
    public class TestKhoMoHinh
    {
        private GameObject _holder;
        private KhoMoHinh _kho;

        [SetUp]
        public void Setup()
        {
            _holder = new GameObject("Test_KhoMoHinh_Holder");
            _kho = _holder.AddComponent<KhoMoHinh>();
            KhoMoHinh.Instance = _kho;
            _kho.KhoiTaoTraCuu();
        }

        [TearDown]
        public void Teardown()
        {
            if (_holder != null)
            {
                Object.DestroyImmediate(_holder);
            }
        }

        [Test]
        public void Test01_KhoMoHinh_KhoiTaoVaDangKy()
        {
            Assert.IsNotNull(_kho);
            Assert.IsNotNull(KhoMoHinh.Instance);

            var dummyPrefab = new GameObject("DummyPrefab");
            dummyPrefab.AddComponent<BoxCollider>();
            _kho.DangKyMoHinh("test_role", dummyPrefab, Vector3.one * 1.5f);

            Assert.IsTrue(_kho.CoPrefab("test_role"));
            var instance = _kho.TaoVatThe("test_role");
            Assert.IsNotNull(instance);
            Assert.IsNotNull(instance.GetComponentInChildren<Collider>(), "Mô hình phải có Collider!");

            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(dummyPrefab);
        }

        [Test]
        public void Test02_KhoMoHinh_FallbackProcedural_KhongLoi()
        {
            // Kiểm tra các vai trò ưu tiên 1 & 2 khi chưa có prefab tải về
            string[] cacVaiTro = {
                "ban_chinh", "ban_chau", "ban_tuong", "ghe",
                "tu_hut", "an_toan", "binh_chua_chay",
                "burette", "binh_tam_giac", "ong_nghiem", "gia_ong_nghiem", "coc_dong",
                "den_bunsen", "ngon_lua", "dong_ho_van_nang", "nguon_dc"
            };

            foreach (var vaiTro in cacVaiTro)
            {
                var obj = _kho.TaoVatThe(vaiTro);
                Assert.IsNotNull(obj, $"Fallback cho vai trò '{vaiTro}' không được null!");
                Assert.IsTrue(obj.GetComponent<Collider>() != null || obj.GetComponentInChildren<Collider>() != null,
                    $"Vật thể '{vaiTro}' phải có Collider bảo vệ!");

                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void Test03_KichThuocChuanVaBoundingBox()
        {
            // 1. Bàn chính cao mặt ~0.90m
            var ban = KhoMoHinh.TaoBanChinhProcedural();
            var colBan = ban.GetComponent<BoxCollider>();
            Assert.IsNotNull(colBan);
            Assert.AreEqual(0.90f, colBan.size.y, 0.05f, "Bàn chính phải có chiều cao mặt chuẩn 0.90m");
            Object.DestroyImmediate(ban);

            // 2. Burette ~0.75m
            var burette = DungCuISO.TaoBurette50mL(null, Vector3.zero);
            var thanBu = burette.transform.Find("ThanOngBurette");
            Assert.IsNotNull(thanBu, "Burette phải có thân ống!");
            float chieuCaoBurette = thanBu.localScale.y * 2f;
            Assert.AreEqual(0.75f, chieuCaoBurette, 0.05f, "Burette phải dài ~0.75m theo ISO 385");
            Object.DestroyImmediate(burette);

            // 3. Bình tam giác ~0.14m
            var binh = DungCuISO.TaoBinhTamGiac250mL(null, Vector3.zero);
            var thanBinh = binh.transform.Find("ThanNon");
            var coBinh = binh.transform.Find("CoBinh");
            Assert.IsNotNull(thanBinh, "Bình tam giác phải có thân nón!");
            Assert.IsNotNull(coBinh, "Bình tam giác phải có cổ bình!");
            float chieuCaoBinh = thanBinh.localScale.y * 2f + coBinh.localScale.y * 2f;
            Assert.AreEqual(0.14f, chieuCaoBinh, 0.03f, "Bình tam giác 250mL cao ~0.14m theo ISO 1773");
            Object.DestroyImmediate(binh);

            // 4. Tủ hút ~2.3m
            var tuHut = KhoMoHinh.TaoTuHutProcedural();
            var colTu = tuHut.GetComponent<BoxCollider>();
            Assert.IsNotNull(colTu);
            Assert.AreEqual(2.30f, colTu.size.y, 0.15f, "Tủ hút phải cao ~2.30m");
            Object.DestroyImmediate(tuHut);

            // 5. Bình chữa cháy ~0.50m
            var binhChuaChay = KhoMoHinh.TaoBinhChuaChayProcedural();
            var colBCC = binhChuaChay.GetComponent<BoxCollider>();
            Assert.IsNotNull(colBCC);
            Assert.AreEqual(0.50f, colBCC.size.y, 0.05f, "Bình chữa cháy phải cao ~0.50m");
            Object.DestroyImmediate(binhChuaChay);

            // 6. Trạm rửa mắt & vòi sen ~2.2m
            var tramRuaMat = KhoMoHinh.TaoTramRuaMatProcedural();
            var colTram = tramRuaMat.GetComponent<BoxCollider>();
            Assert.IsNotNull(colTram);
            Assert.AreEqual(2.20f, colTram.size.y, 0.10f, "Trạm rửa mắt vòi sen phải cao ~2.20m");
            Object.DestroyImmediate(tramRuaMat);
        }

        [Test]
        public void Test04_TaiNguyenJson_HopLeVaGiayPhep()
        {
            string path = Path.Combine(Application.dataPath, "..", "TAI_NGUYEN.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(Application.streamingAssetsPath, "ptn", "TAI_NGUYEN.json");
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(Application.dataPath, "StreamingAssets", "ptn", "TAI_NGUYEN.json");
            }

            Assert.IsTrue(File.Exists(path), "File TAI_NGUYEN.json phải tồn tại!");
            string json = File.ReadAllText(path);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            // Kiểm tra giấy phép: Bắt buộc không có NonCommercial
            Assert.IsFalse(json.Contains("NonCommercial"), "Tuyệt đối không sử dụng mô hình có giấy phép NonCommercial!");
            Assert.IsTrue(json.Contains("CC Attribution") || json.Contains("CC0"), "Chỉ chấp nhận CC Attribution hoặc CC0!");
            Assert.IsTrue(json.Contains("danh_sach"), "Phải có danh sách mô hình!");
        }

        [Test]
        public void Test05_GhiCongMd_DungDinhDang()
        {
            string path = Path.Combine(Application.dataPath, "..", "GHI_CONG.md");
            if (!File.Exists(path))
            {
                path = Path.Combine(Application.dataPath, "..", "..", "GHI_CONG.md");
            }

            Assert.IsTrue(File.Exists(path), "File GHI_CONG.md phải tồn tại!");
            string content = File.ReadAllText(path);

            Assert.IsTrue(content.Contains("Sketchfab"), "GHI_CONG.md phải ghi nguồn Sketchfab!");
            Assert.IsTrue(content.Contains("Creative Commons") || content.Contains("CC BY"), "GHI_CONG.md phải có thông tin bản quyền!");
            Assert.IsTrue(content.Contains("Konstantin_Keller") || content.Contains("lab-bench"), "GHI_CONG.md phải ghi công tác giả!");
        }
    }
}
