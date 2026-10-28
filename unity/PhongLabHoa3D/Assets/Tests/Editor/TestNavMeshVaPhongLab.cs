using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using PhongThiNghiem.ToaNha;

namespace PhongThiNghiem.Tests
{
    public class TestNavMeshVaPhongLab
    {
        private GameObject _goLab;
        private DungPhongLab _dungPhong;

        [SetUp]
        public void Setup()
        {
            _goLab = new GameObject("TestPhongLab");
            _dungPhong = _goLab.AddComponent<DungPhongLab>();
            string banDoPath = Path.Combine(Application.streamingAssetsPath, "ptn/ban-do.json");
            _dungPhong.DungToanBoPhong(banDoPath);
        }

        [TearDown]
        public void Teardown()
        {
            if (_goLab != null)
            {
                Object.DestroyImmediate(_goLab);
            }
        }

        [Test]
        public void Test_01_DocBanDo_Du5KhuVa5Tram()
        {
            Assert.IsNotNull(_dungPhong.DuLieu, "Dữ liệu bản đồ không được null");
            Assert.AreEqual(22f, _dungPhong.DuLieu.phong.rong, 0.01f, "Chiều rộng phòng 22m");
            Assert.AreEqual(16f, _dungPhong.DuLieu.phong.sau, 0.01f, "Chiều sâu phòng 16m");
            Assert.AreEqual(4.2f, _dungPhong.DuLieu.phong.cao, 0.01f, "Chiều cao trần 4.2m");

            Assert.AreEqual(5, _dungPhong.DuLieu.khu.Count, "Phải có đúng 5 khu vực");
            Assert.AreEqual(5, _dungPhong.DuLieu.tram.Count, "Phải có đúng 5 trạm thí nghiệm");

            string[] idCacTram = { "an-toan", "chuan-do", "nhan-biet-ion", "pin-galvani", "dien-phan" };
            foreach (var id in idCacTram)
            {
                Transform ban = _dungPhong.LayBanTram(id);
                Assert.IsNotNull(ban, $"Bàn trạm {id} phải được tạo ra");
                Assert.AreEqual(0.9f, ban.position.y, 0.05f, $"Độ cao mặt bàn trạm {id} phải là 0.9m");
            }
        }

        [Test]
        public void Test_02_NavMesh_NoiMoiTram_TuDiemXuatPhat()
        {
            Vector3 xuatPhat = new Vector3(_dungPhong.DuLieu.xuat_phat.x, 0f, _dungPhong.DuLieu.xuat_phat.z);

            // Kiểm tra điểm xuất phát nằm trên NavMesh
            bool coXuatPhat = NavMesh.SamplePosition(xuatPhat, out NavMeshHit hitXp, 1.0f, NavMesh.AllAreas);
            Assert.IsTrue(coXuatPhat, "Điểm xuất phát phải nằm trên NavMesh");

            foreach (var tram in _dungPhong.DuLieu.tram)
            {
                Vector3 diemDung = new Vector3(tram.diem_dung[0], 0f, tram.diem_dung[1]);

                bool coDiemDung = NavMesh.SamplePosition(diemDung, out NavMeshHit hitDd, 1.5f, NavMesh.AllAreas);
                Assert.IsTrue(coDiemDung, $"Điểm dừng trạm {tram.id} phải nằm trên hoặc gần NavMesh");

                var path = new NavMeshPath();
                bool timDuoc = NavMesh.CalculatePath(hitXp.position, hitDd.position, NavMesh.AllAreas, path);

                Assert.IsTrue(timDuoc, $"Không thể tìm đường tới trạm {tram.id}");
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Đường đi tới trạm {tram.id} bị đứt đoạn hoặc chặn bởi chướng ngại vật!");
                Assert.IsTrue(path.corners.Length >= 2, $"Đường đi tới trạm {tram.id} phải có ít nhất 2 điểm góc");
            }
        }
    }
}
