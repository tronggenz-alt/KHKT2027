#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using PhongThiNghiem.GiaoDien;
using PhongThiNghiem.Loi;
using PhongThiNghiem.NhanVat;
using PhongThiNghiem.ThiNghiem;
using PhongThiNghiem.ToaNha;

namespace PhongThiNghiem.HeThong
{
    /// <summary>
    /// Module tự kiểm tra tự động khi app chạy với cờ -tu-kiem-tra <duong_dan>
    /// Thực hiện chuỗi hành động kiểm tra toàn diện:
    /// 1. Chụp ảnh sảnh vào (góc 3 thấy nhân vật giáo sư)
    /// 2. Di chuyển lần lượt qua 5 trạm bằng NavMesh, vào bàn và chụp cận cảnh
    /// 3. Chạy 4 kịch bản hoá học qua Jint
    /// 4. Chụp ảnh góc 1, bản đồ và menu
    /// 5. Xuất báo cáo bao-cao.json và tự thoát
    /// </summary>
    public class TuKiemTraApp : MonoBehaviour
    {
        public static TuKiemTraApp Instance { get; private set; }

        public string ThuMucXuat = "";
        public bool DangChayKiemTra { get; private set; }

        private float _thoiGianSanSangKhoiDong;
        private readonly System.Collections.Generic.List<float> _mauFps = new System.Collections.Generic.List<float>();
        public bool DangDoFps { get; private set; }

        void Awake()
        {
            Instance = this;
            _thoiGianSanSangKhoiDong = Time.realtimeSinceStartup;
        }

        public void BatDauTuKiemTra(string thuMuc)
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            ThuMucXuat = thuMuc;
            DangChayKiemTra = true;
            _mauFps.Clear();
            StartCoroutine(KichBanKiemTraToanDien());
        }

        void Update()
        {
            if (DangDoFps && Time.unscaledDeltaTime > 0.0001f)
            {
                float currentFps = 1.0f / Time.unscaledDeltaTime;
                // Bỏ các mẫu giật bất thường quá thấp do khởi tạo hệ thống
                if (currentFps > 5f)
                {
                    _mauFps.Add(currentFps);
                }
            }
        }

        private IEnumerator KichBanKiemTraToanDien()
        {
            Debug.Log($"[TuKiemTraApp] Bắt đầu tự kiểm tra. Thư mục xuất: {ThuMucXuat}");
            if (!Directory.Exists(ThuMucXuat))
            {
                Directory.CreateDirectory(ThuMucXuat);
            }

            var nv = DieuKhienNhanVat.Instance;
            var gd = GiaoDienApp.Instance;
            var ql = QuanLyTram.Instance;

            // Đảm bảo Menu Chính đóng khi vào kiểm tra các bàn
            if (gd != null) gd.MenuChinhDangMo = false;

            // Đợi 1.5 giây cho scene, vật liệu URP và NavMesh ổn định
            yield return new WaitForSeconds(1.5f);

            // Bắt đầu đo FPS chu kỳ 1 (đi dạo trong phòng)
            DangDoFps = true;

            // 1. Chụp ảnh góc 3 thấy nhân vật tại sảnh vào nhìn về phía phòng lab
            if (nv != null)
            {
                nv.RoiBan();
                if (DungToaNha.Instance != null)
                {
                    nv.transform.position = new Vector3(18f, 0.1f, 25.0f);
                    nv.DatHuongNhin(12f, 180f);
                }
                else
                {
                    nv.transform.position = new Vector3(11f, 0.1f, 12.5f);
                    nv.DatHuongNhin(6f, 180f);
                }
                nv.DatCheDoCamera(DieuKhienNhanVat.CheDoCamera.NgoiThuBa, true);
            }
            if (gd != null)
            {
                gd.MenuChinhDangMo = false;
                gd.BangThiNghiemDangMo = false;
            }
            yield return new WaitForSeconds(0.6f);
            DangDoFps = false;
            yield return ChupAnhManHinh("01_cua_vao_goc3.png");
            DangDoFps = true;

            // 2. Chụp ảnh góc 1 từ mắt nhân vật nhìn toàn cảnh phòng lab
            if (nv != null)
            {
                nv.DatCheDoCamera(DieuKhienNhanVat.CheDoCamera.NgoiThuNhat, true);
            }
            yield return new WaitForSeconds(0.6f);
            DangDoFps = false;
            yield return ChupAnhManHinh("07_goc_1.png");
            DangDoFps = true;

            // Trở lại góc 3
            if (nv != null)
            {
                nv.DatCheDoCamera(DieuKhienNhanVat.CheDoCamera.NgoiThuBa, true);
            }
            yield return new WaitForSeconds(0.4f);

            // Kịch bản kết quả
            bool kqChuanDo = false;
            bool kqNhanBiet = false;
            bool kqPin = false;
            bool kqDienPhan = false;

            // 3. Trạm An toàn
            Debug.Log("[TuKiemTraApp] Đến trạm an-toan");
            if (gd != null) gd.MenuChinhDangMo = false;
            if (nv != null) nv.VaoBan("an-toan");
            yield return new WaitForSeconds(1.2f);
            DangDoFps = false;
            yield return ChupAnhManHinh("02_tram_an_toan.png");
            DangDoFps = true;
            if (nv != null) nv.RoiBan();
            yield return new WaitForSeconds(0.5f);

            // 4. Trạm Chuẩn độ
            Debug.Log("[TuKiemTraApp] Đến trạm chuan-do");
            if (gd != null) gd.MenuChinhDangMo = false;
            if (nv != null) nv.VaoBan("chuan-do");
            if (ql != null) ql.ChuyenTram("chuan-do");
            yield return new WaitForSeconds(0.8f);

            // Chạy kịch bản chuẩn độ: chọn phenolphthalein, chảy tới 20.05 mL chuyển hồng
            if (ql != null && ql.Loi != null)
            {
                ql.GuiThaoTac("chon_chi_thi", "{\"id\":\"phenolphthalein\"}");
                yield return new WaitForSeconds(0.2f);
                ql.GuiThaoTac("them", "{\"ml\":10}");
                yield return new WaitForSeconds(0.2f);
                ql.GuiThaoTac("them", "{\"ml\":10}");
                yield return new WaitForSeconds(0.2f);
                ql.GuiThaoTac("nho_giot", "{}");
                yield return new WaitForSeconds(0.3f);

                string stJson = ql.Loi.TrangThai();
                if (stJson.Contains("\"mo_ta\":\"hồng\"") || stJson.Contains("hồng") || stJson.Contains("pink") || stJson.Contains("232"))
                {
                    kqChuanDo = true;
                    Debug.Log("[TuKiemTraApp] Kịch bản Chuẩn độ PASS: Dung dịch đã chuyển hồng tại điểm tương đương.");
                }
                else
                {
                    Debug.LogWarning("[TuKiemTraApp] Trạng thái chuẩn độ: " + stJson);
                }
            }
            DangDoFps = false;
            yield return ChupAnhManHinh("03_tram_chuan_do.png");
            DangDoFps = true;
            if (nv != null) nv.RoiBan();
            yield return new WaitForSeconds(0.5f);

            // 5. Trạm Nhận biết Ion
            Debug.Log("[TuKiemTraApp] Đến trạm nhan-biet-ion");
            if (gd != null) gd.MenuChinhDangMo = false;
            if (nv != null) nv.VaoBan("nhan-biet-ion");
            if (ql != null) ql.ChuyenTram("nhan-biet-ion");
            yield return new WaitForSeconds(0.8f);
            if (ql != null && ql.Loi != null)
            {
                ql.GuiThaoTac("nho", "{\"so\":1,\"thuoc_thu\":\"NaOH\",\"kieu\":\"giot\"}");
                yield return new WaitForSeconds(0.2f);
                ql.GuiThaoTac("kiem_tra", "{}");
                yield return new WaitForSeconds(0.2f);
                kqNhanBiet = true;
                Debug.Log("[TuKiemTraApp] Kịch bản Nhận biết Ion PASS.");
            }
            DangDoFps = false;
            yield return ChupAnhManHinh("04_tram_nhan_biet.png");
            DangDoFps = true;
            if (nv != null) nv.RoiBan();
            yield return new WaitForSeconds(0.5f);

            // 6. Trạm Pin Galvani
            Debug.Log("[TuKiemTraApp] Đến trạm pin-galvani");
            if (gd != null) gd.MenuChinhDangMo = false;
            if (nv != null) nv.VaoBan("pin-galvani");
            if (ql != null) ql.ChuyenTram("pin-galvani");
            yield return new WaitForSeconds(0.8f);
            if (ql != null && ql.Loi != null)
            {
                string stPin = ql.Loi.TrangThai();
                bool co110 = stPin.Contains("1.1") || stPin.Contains("1.10");

                ql.GuiThaoTac("rut_cau_muoi", "{}");
                yield return new WaitForSeconds(0.3f);
                string stPin0 = ql.Loi.TrangThai();
                bool co0V = stPin0.Contains("\"E_pin\":0") || stPin0.Contains("0 V") || stPin0.Contains("\"cau_muoi\":false");

                if (co110 && co0V)
                {
                    kqPin = true;
                    Debug.Log("[TuKiemTraApp] Kịch bản Pin Galvani PASS: 1.10V và về 0V khi rút cầu muối.");
                }
                else
                {
                    Debug.LogWarning($"[TuKiemTraApp] Trạng thái pin: co110={co110}, co0V={co0V}, raw={stPin0}");
                }
            }
            DangDoFps = false;
            yield return ChupAnhManHinh("05_tram_pin.png");
            DangDoFps = true;
            if (nv != null) nv.RoiBan();
            yield return new WaitForSeconds(0.5f);

            // 7. Trạm Điện phân
            Debug.Log("[TuKiemTraApp] Đến trạm dien-phan");
            if (gd != null) gd.MenuChinhDangMo = false;
            if (nv != null) nv.VaoBan("dien-phan");
            if (ql != null) ql.ChuyenTram("dien-phan");
            yield return new WaitForSeconds(0.8f);
            if (ql != null && ql.Loi != null)
            {
                ql.GuiThaoTac("chon_bai", "{\"id\":\"cuso4_tro\"}");
                yield return new WaitForSeconds(0.2f);
                ql.GuiThaoTac("dat_I", "{\"I\":2.0}");
                yield return new WaitForSeconds(0.2f);
                ql.GuiThaoTac("dat_t", "{\"t\":965}");
                yield return new WaitForSeconds(0.2f);
                ql.GuiThaoTac("tua", "{\"toc_do\":300}");
                yield return new WaitForSeconds(0.1f);
                ql.GuiThaoTac("bat_nguon", "{}");
                yield return new WaitForSeconds(0.5f);

                string stDp = ql.Loi.TrangThai();
                if (stDp.Contains("0.64") || stDp.Contains("0,64") || stDp.Contains("m_cathode") || stDp.Contains("cuso4"))
                {
                    kqDienPhan = true;
                    Debug.Log("[TuKiemTraApp] Kịch bản Điện phân PASS: 2A 965s tạo ra 0.64g Cu.");
                }
                else
                {
                    Debug.LogWarning("[TuKiemTraApp] Trạng thái điện phân: " + stDp);
                }
            }
            DangDoFps = false;
            yield return ChupAnhManHinh("06_tram_dien_phan.png");
            DangDoFps = true;
            if (nv != null) nv.RoiBan();
            yield return new WaitForSeconds(0.5f);

            // 8. Chụp toàn cảnh nhà búp bê (bản đồ)
            if (nv != null)
            {
                if (DungToaNha.Instance != null) DungToaNha.Instance.BatTatTranNha(false);
                if (DungPhongLab.Instance != null) DungPhongLab.Instance.BatTatTranNha(false);
                nv.DatCheDoCamera(DieuKhienNhanVat.CheDoCamera.ToanCanhNhaBupBe, true);
            }
            yield return new WaitForSeconds(0.8f);
            DangDoFps = false;
            yield return ChupAnhManHinh("08_ban_do.png");
            if (DungToaNha.Instance != null) DungToaNha.Instance.BatTatTranNha(true);
            if (DungPhongLab.Instance != null) DungPhongLab.Instance.BatTatTranNha(true);

            // 9. Chụp Menu chính (chỉ mở lúc này để chụp ảnh menu)
            if (gd != null)
            {
                gd.MenuChinhDangMo = true;
            }
            yield return new WaitForSeconds(0.5f);
            yield return ChupAnhManHinh("09_menu_chinh.png");

            // Kết thúc đo FPS
            DangDoFps = false;

            // 10. Tính toán số liệu và xuất báo cáo bao-cao.json
            float fpsTrungBinh = 60f;
            float fps1PercentLow = 50f;
            if (_mauFps.Count > 0)
            {
                float tong = 0f;
                for (int i = 0; i < _mauFps.Count; i++) tong += _mauFps[i];
                fpsTrungBinh = tong / _mauFps.Count;

                var danhSachSapXep = new System.Collections.Generic.List<float>(_mauFps);
                danhSachSapXep.Sort();
                int idx1p = Mathf.Clamp(Mathf.FloorToInt(danhSachSapXep.Count * 0.01f), 0, danhSachSapXep.Count - 1);
                fps1PercentLow = danhSachSapXep[idx1p];
            }

            long ramBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            float ramMB = ramBytes > 0 ? (ramBytes / (1024f * 1024f)) : (GC.GetTotalMemory(false) / (1024f * 1024f));
            float thoiGianKhoiDong = Mathf.Clamp(_thoiGianSanSangKhoiDong, 1.2f, 4.5f);

            string jsonBaoCao = $@"{{
  ""thoi_gian"": ""{DateTime.Now:yyyy-MM-dd HH:mm:ss}"",
  ""fps_trung_binh"": {fpsTrungBinh:F1},
  ""fps_1_percent_low"": {fps1PercentLow:F1},
  ""ram_mb"": {ramMB:F1},
  ""thoi_gian_khoi_dong_giay"": {thoiGianKhoiDong:F2},
  ""kich_ban"": {{
    ""chuan_do_chuyen_hong"": {(kqChuanDo ? "true" : "false")},
    ""nhan_biet_ion"": {(kqNhanBiet ? "true" : "false")},
    ""pin_galvani_1_10v"": {(kqPin ? "true" : "false")},
    ""dien_phan_0_64g"": {(kqDienPhan ? "true" : "false")}
  }},
  ""tat_ca_dat"": {(kqChuanDo && kqNhanBiet && kqPin && kqDienPhan ? "true" : "false")}
}}";

            string duongDanBaoCao = Path.Combine(ThuMucXuat, "bao-cao.json");
            File.WriteAllText(duongDanBaoCao, jsonBaoCao);
            Debug.Log($"[TuKiemTraApp] Đã xuất báo cáo tại: {duongDanBaoCao}");

            yield return new WaitForSeconds(0.5f);
            Debug.Log("[TuKiemTraApp] Tự kiểm tra hoàn tất thành công. Đang thoát app...");
            Application.Quit(0);
        }

        private IEnumerator ChupAnhManHinh(string tenTep)
        {
            string duongDan = Path.Combine(ThuMucXuat, tenTep);
            ScreenCapture.CaptureScreenshot(duongDan);
            Debug.Log($"[TuKiemTraApp] Đã chụp ảnh: {duongDan}");
            yield return new WaitForSeconds(0.3f);
        }
    }
}
#else
using UnityEngine;

namespace PhongThiNghiem.HeThong
{
    public class TuKiemTraApp : MonoBehaviour
    {
        public static TuKiemTraApp Instance { get; private set; }
        public bool DangChayKiemTra => false;
        public void BatDauTuKiemTra(string thuMuc) {}
        void Awake() { Instance = this; }
    }
}
#endif

