using NUnit.Framework;
using UnityEngine;
using PhongThiNghiem.ThiNghiem;
using PhongThiNghiem.HeThong;

namespace PhongThiNghiem.Tests
{
    public class TestOnThiVaAmThanh
    {
        private GameObject _goAmThanh;
        private AmThanhLab _amThanh;

        [SetUp]
        public void ThietLap()
        {
            _goAmThanh = new GameObject("AmThanhLab_Test");
            _amThanh = _goAmThanh.AddComponent<AmThanhLab>();
        }

        [TearDown]
        public void DonDep()
        {
            if (_goAmThanh != null) Object.DestroyImmediate(_goAmThanh);
        }

        [Test]
        public void Test_01_NganHangCauHoi_Du5Tram()
        {
            string[] dsTram = { "chuan-do", "nhan-biet", "pin-galvani", "dien-phan", "an-toan" };

            foreach (var tram in dsTram)
            {
                var dsCauHoi = CauHoiOnThiTHPT.LayCauHoi(tram);
                Assert.IsNotNull(dsCauHoi, $"Danh sách câu hỏi trạm {tram} không được null");
                Assert.GreaterOrEqual(dsCauHoi.Count, 2, $"Trạm {tram} phải có ít nhất 2 câu hỏi ôn thi THPT");

                foreach (var ch in dsCauHoi)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(ch.NoiDung), "Nội dung câu hỏi không được rỗng");
                    Assert.AreEqual(4, ch.LuaChon.Length, "Mỗi câu hỏi phải có đủ 4 lựa chọn A, B, C, D");
                    Assert.GreaterOrEqual(ch.DapAnDung, 0);
                    Assert.LessOrEqual(ch.DapAnDung, 3);
                    Assert.IsFalse(string.IsNullOrEmpty(ch.GiaiThich), "Phải có giải thích chi tiết đáp án");
                    Assert.IsFalse(string.IsNullOrEmpty(ch.NguonDeThi), "Phải ghi rõ nguồn đề thi tốt nghiệp THPT");
                }
            }
        }

        [Test]
        public void Test_02_AmThanhLab_KhoiTaoVaPhatAmThanh()
        {
            Assert.IsNotNull(_amThanh);
            Assert.IsNotNull(AmThanhLab.Instance);
            Assert.AreEqual(_amThanh, AmThanhLab.Instance);

            // Kiểm tra các hàm phát âm thanh thực thi an toàn không ném lỗi
            Assert.DoesNotThrow(() =>
            {
                _amThanh.PhatTiengBuocChan(Vector3.zero);
                _amThanh.PhatTiengRotNuoc(Vector3.zero);
                _amThanh.PhatTiengBotSui(Vector3.zero);
                _amThanh.PhatTiengCongTac(Vector3.zero);
                _amThanh.PhatTiengThanhCong(Vector3.zero);
            });
        }
    }
}
