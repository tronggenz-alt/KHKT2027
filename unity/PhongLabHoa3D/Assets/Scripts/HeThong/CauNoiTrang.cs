using System;
using System.Runtime.InteropServices;
using UnityEngine;
using PhongThiNghiem.Loi;
using PhongThiNghiem.ThiNghiem;

namespace PhongThiNghiem.HeThong
{
    public class CauNoiTrang : MonoBehaviour
    {
        public static CauNoiTrang Instance { get; private set; }

        private static LoiHoaHocTrinhDuyet _loiHienTai;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PTN_BaoSuKien(string ten, string json);
#else
        private static void PTN_BaoSuKien(string ten, string json)
        {
            // Dự phòng trên Editor/macOS Standalone
            Debug.Log($"[CauNoiTrang.BaoSuKien] {ten}: {json}");
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void KhoiTaoTuDong()
        {
            if (Instance == null)
            {
                var go = GameObject.Find("CauNoiTrang");
                if (go == null)
                {
                    go = new GameObject("CauNoiTrang");
                    DontDestroyOnLoad(go);
                }
                Instance = go.GetComponent<CauNoiTrang>() ?? go.AddComponent<CauNoiTrang>();
            }
        }

        private float _demThoiGianFps = 0f;
        private int _soKhungHinh = 0;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            BaoSuKien("san_sang", "{}");
        }

        void Update()
        {
            _soKhungHinh++;
            _demThoiGianFps += Time.unscaledDeltaTime;
            if (_demThoiGianFps >= 2.0f)
            {
                float fps = _soKhungHinh / _demThoiGianFps;
                _soKhungHinh = 0;
                _demThoiGianFps = 0f;
                BaoSuKien("fps", $"{{\"fps\":{fps:F1}}}");
            }
        }

        public static void DangKyLoiTrinhDuyet(LoiHoaHocTrinhDuyet loi)
        {
            _loiHienTai = loi;
        }

        public static void HuyDangKyLoiTrinhDuyet(LoiHoaHocTrinhDuyet loi)
        {
            if (_loiHienTai == loi) _loiHienTai = null;
        }

        public static void BaoSuKien(string ten, string json = "{}")
        {
            try
            {
                PTN_BaoSuKien(ten, string.IsNullOrEmpty(json) ? "{}" : json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CauNoiTrang] Lỗi báo sự kiện {ten}: {ex.Message}");
            }
        }

        public static void BaoTram(string id, bool taiBan)
        {
            BaoSuKien("tram", $"{{\"id\":\"{id}\",\"tai_ban\":{(taiBan ? "true" : "false")}}}");
        }

        public static void BaoMoCuaSoMuot()
        {
            BaoSuKien("mo_cua_so_muot", "{}");
        }

        public static void BaoThoat()
        {
            BaoSuKien("thoat", "{}");
        }

        public static void BaoLoi(string thongDiep)
        {
            BaoSuKien("loi", $"{{\"thong_diep\":\"{thongDiep}\"}}");
        }

        // --- CÁC HÀM NHẬN TỪ TRANG WEB QUA SendMessage("CauNoiTrang", ...) ---

        public void NhanTrangThai(string json)
        {
            _loiHienTai?.NhanTrangThaiTuTrang(json);
        }

        public void NhanSoTay(string json)
        {
            _loiHienTai?.NhanSoTayTuTrang(json);
        }

        public void NhanHieuUng(string json)
        {
            _loiHienTai?.NhanHieuUngTuTrang(json);
        }

        public void MoThiNghiem(string id)
        {
            if (QuanLyTram.Instance != null && !string.IsNullOrEmpty(id))
            {
                QuanLyTram.Instance.ChuyenTram(id);
            }
        }

        public void DoiGiaoDien(string json)
        {
            Debug.Log($"[CauNoiTrang] DoiGiaoDien: {json}");
        }
    }
}
