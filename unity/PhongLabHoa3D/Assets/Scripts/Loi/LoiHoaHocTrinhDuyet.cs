using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;
using PhongThiNghiem.HeThong;

namespace PhongThiNghiem.Loi
{
    public class LoiHoaHocTrinhDuyet : ILoiHoaHoc
    {
        public event Action<string> OnTrangThai;
        public event Action<string> OnSoTay;
        public event Action<SuKienHieuUng> OnHieuUng;

        public bool DangMoPhong => !string.IsNullOrEmpty(_moPhongHienTaiId);
        public string MoPhongId => _moPhongHienTaiId;

        private string _moPhongHienTaiId;
        private string _trangThaiMoiNhatJson = "{}";
        private string _soTayMoiNhatJson = "[]";

        private readonly ConcurrentQueue<string> _hangDoiTrangThai = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _hangDoiSoTay = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<SuKienHieuUng> _hangDoiHieuUng = new ConcurrentQueue<SuKienHieuUng>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PTN_Tao(string id, string cheDo);

        [DllImport("__Internal")]
        private static extern void PTN_ThaoTac(string id, string lenh, string json);

        [DllImport("__Internal")]
        private static extern string PTN_TrangThai(string id);

        [DllImport("__Internal")]
        private static extern string PTN_SoTay(string id);

        [DllImport("__Internal")]
        private static extern void PTN_ChoChay(bool choChay);
#else
        private static void PTN_Tao(string id, string cheDo) {}
        private static void PTN_ThaoTac(string id, string lenh, string json) {}
        private static string PTN_TrangThai(string id) => "{}";
        private static string PTN_SoTay(string id) => "[]";
        private static void PTN_ChoChay(bool choChay) {}
#endif

        public LoiHoaHocTrinhDuyet()
        {
            CauNoiTrang.DangKyLoiTrinhDuyet(this);
        }

        public void Tao(string id, string cheDo = "huong_dan")
        {
            _moPhongHienTaiId = id;
            try
            {
                PTN_Tao(id, cheDo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoiHoaHocTrinhDuyet] Lỗi Tao: {ex.Message}");
            }
        }

        public void ThaoTac(string lenh, string thamSoJson = null)
        {
            ThaoTacDongBo(lenh, thamSoJson);
        }

        public void ThaoTacDongBo(string lenh, string thamSoJson = null)
        {
            if (string.IsNullOrEmpty(_moPhongHienTaiId)) return;
            try
            {
                PTN_ThaoTac(_moPhongHienTaiId, lenh, string.IsNullOrEmpty(thamSoJson) ? "{}" : thamSoJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoiHoaHocTrinhDuyet] Lỗi ThaoTac: {ex.Message}");
            }
        }

        public void BatDauMoPhong()
        {
            try
            {
                PTN_ChoChay(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoiHoaHocTrinhDuyet] Lỗi BatDauMoPhong: {ex.Message}");
            }
        }

        public void TamDungMoPhong()
        {
            try
            {
                PTN_ChoChay(false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoiHoaHocTrinhDuyet] Lỗi TamDungMoPhong: {ex.Message}");
            }
        }

        public string TrangThai()
        {
            try
            {
                if (!string.IsNullOrEmpty(_moPhongHienTaiId))
                {
                    string tt = PTN_TrangThai(_moPhongHienTaiId);
                    if (!string.IsNullOrEmpty(tt)) _trangThaiMoiNhatJson = tt;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoiHoaHocTrinhDuyet] Lỗi TrangThai: {ex.Message}");
            }
            return _trangThaiMoiNhatJson;
        }

        public string SoTay()
        {
            try
            {
                if (!string.IsNullOrEmpty(_moPhongHienTaiId))
                {
                    string st = PTN_SoTay(_moPhongHienTaiId);
                    if (!string.IsNullOrEmpty(st)) _soTayMoiNhatJson = st;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoiHoaHocTrinhDuyet] Lỗi SoTay: {ex.Message}");
            }
            return _soTayMoiNhatJson;
        }

        public void NhanTrangThaiTuTrang(string json)
        {
            _trangThaiMoiNhatJson = json;
            _hangDoiTrangThai.Enqueue(json);
        }

        public void NhanSoTayTuTrang(string json)
        {
            _soTayMoiNhatJson = json;
            _hangDoiSoTay.Enqueue(json);
        }

        public void NhanHieuUngTuTrang(string json)
        {
            _hangDoiHieuUng.Enqueue(new SuKienHieuUng { Ten = "hieu_ung", DuLieuJson = json });
        }

        public void CapNhatLuongChinh()
        {
            while (_hangDoiTrangThai.TryDequeue(out var tt))
            {
                OnTrangThai?.Invoke(tt);
            }

            while (_hangDoiSoTay.TryDequeue(out var st))
            {
                OnSoTay?.Invoke(st);
            }

            while (_hangDoiHieuUng.TryDequeue(out var e))
            {
                OnHieuUng?.Invoke(e);
            }
        }

        public void Dispose()
        {
            TamDungMoPhong();
            CauNoiTrang.HuyDangKyLoiTrinhDuyet(this);
            _moPhongHienTaiId = null;
        }
    }
}
