using System.IO;
using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.HeThong;
using PhongThiNghiem.ThiNghiem;
using PhongThiNghiem.Loi;

namespace PhongThiNghiem.Tests
{
    public class TestWebGLTuongThich
    {
        [Test]
        public void Test01_DocCacFileJsonQuaDocTaiNguyenKhongRong()
        {
            string[] cacFile = {
                "ptn/ban-do.json",
                "ptn/nguyen-to.json",
                "ptn/bang-ion.json",
                "ptn/toa-nha.json",
                "ptn/thi-nghiem/an-toan.json",
                "ptn/thi-nghiem/chuan-do.json",
                "ptn/thi-nghiem/nhan-biet-ion.json",
                "ptn/thi-nghiem/pin-galvani.json",
                "ptn/thi-nghiem/dien-phan.json"
            };

            foreach (var f in cacFile)
            {
                string noiDung = DocTaiNguyen.DocChuoi(f);
                Assert.IsFalse(string.IsNullOrEmpty(noiDung), $"Tệp {f} đọc qua DocTaiNguyen không được rỗng!");
            }
        }

        [Test]
        public void Test02_BangTuanHoan3DNapDu118NguyenToQuaDocTaiNguyen()
        {
            var go = new GameObject("TestBTH");
            var bth = go.AddComponent<BangTuanHoan3D>();
            bth.NapDuLieu();
            Assert.AreEqual(118, bth.DanhSach.Count, "Bảng tuần hoàn phải nạp đủ 118 nguyên tố!");
            Assert.IsTrue(bth.BangTraCuu.ContainsKey(1), "Phải có Hydro (Z=1)!");
            Assert.IsTrue(bth.BangTraCuu.ContainsKey(118), "Phải có Oganesson (Z=118)!");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Test03_LoiHoaHocTrinhDuyetChayStubKhongNemNgoaiLe()
        {
            using (var loi = new LoiHoaHocTrinhDuyet())
            {
                Assert.DoesNotThrow(() => loi.Tao("chuan-do"));
                Assert.DoesNotThrow(() => loi.ThaoTac("them_dung_dich", "{}"));
                Assert.DoesNotThrow(() => loi.ThaoTacDongBo("them_dung_dich", "{}"));
                Assert.DoesNotThrow(() => loi.BatDauMoPhong());
                Assert.DoesNotThrow(() => loi.TamDungMoPhong());
                Assert.AreEqual("{}", loi.TrangThai());
                Assert.AreEqual("[]", loi.SoTay());
                Assert.DoesNotThrow(() => loi.CapNhatLuongChinh());
            }
        }

        [Test]
        public void Test04_CauNoiTrangNhanThongDiepHopLe()
        {
            var go = GameObject.Find("CauNoiTrang");
            if (go == null)
            {
                go = new GameObject("CauNoiTrang");
                go.AddComponent<CauNoiTrang>();
            }

            var cauNoi = go.GetComponent<CauNoiTrang>();
            Assert.IsNotNull(cauNoi, "GameObject CauNoiTrang phải có component CauNoiTrang!");

            Assert.DoesNotThrow(() => cauNoi.NhanTrangThai("{}"));
            Assert.DoesNotThrow(() => cauNoi.NhanSoTay("[]"));
            Assert.DoesNotThrow(() => cauNoi.NhanHieuUng("{}"));
            Assert.DoesNotThrow(() => CauNoiTrang.BaoSuKien("kiem_tra", "{}"));
            Assert.DoesNotThrow(() => CauNoiTrang.BaoTram("an-toan", true));
        }
    }
}
