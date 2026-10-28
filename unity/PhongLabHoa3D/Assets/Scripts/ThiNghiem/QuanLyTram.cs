using System;
using System.Collections.Generic;
using UnityEngine;
using PhongThiNghiem;
using PhongThiNghiem.Loi;
using PhongThiNghiem.ToaNha;
using PhongThiNghiem.HeThong;

namespace PhongThiNghiem.ThiNghiem
{
    public class QuanLyTram : MonoBehaviour, ICauNoiLoi
    {
        public static QuanLyTram Instance { get; private set; }

        public bool Toi => false;
        public Color MauNhan => new Color(0.22f, 0.74f, 0.97f); // Sky Blue

        public ILoiHoaHoc Loi { get; private set; }
        public string TramHienTaiId { get; private set; }
        public CanhCoSo CanhHienTai => _cacCanh.TryGetValue(TramHienTaiId ?? "", out var c) ? c : null;

        private readonly Dictionary<string, CanhCoSo> _cacCanh = new Dictionary<string, CanhCoSo>();
        private readonly Dictionary<string, string> _trangThaiMoiNhat = new Dictionary<string, string>();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            KhoiTaoLoiVa5Tram();
        }

        private ILoiHoaHoc TaoLoiHoaHoc()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new LoiHoaHocTrinhDuyet();
#else
            return new LoiHoaHoc();
#endif
        }

        public void KhoiTaoLoiVa5Tram()
        {
            if (Loi == null)
            {
                Loi = TaoLoiHoaHoc();
                Loi.OnTrangThai += XuLyTrangThaiMoi;
                Loi.OnHieuUng += XuLyHieuUngMoi;
            }

            Transform LayBan(string id)
            {
                if (DungToaNha.Instance != null)
                {
                    var b = DungToaNha.Instance.LayBanTram(id);
                    if (b != null) return b;
                    if (id == "nhan-biet-ion") return DungToaNha.Instance.LayBanTram("nhan-biet");
                    if (id == "nhan-biet") return DungToaNha.Instance.LayBanTram("nhan-biet-ion");
                }
                if (DungPhongLab.Instance != null)
                {
                    var b = DungPhongLab.Instance.LayBanTram(id);
                    if (b != null) return b;
                    if (id == "nhan-biet-ion") return DungPhongLab.Instance.LayBanTram("nhan-biet");
                    if (id == "nhan-biet") return DungPhongLab.Instance.LayBanTram("nhan-biet-ion");
                }
                return null;
            }

            // Khởi tạo và dựng cả 5 cảnh trên 5 bàn cùng lúc
            DungMotTram("an-toan", new CanhAnToan(), LayBan("an-toan"));
            DungMotTram("chuan-do", new CanhChuanDo(), LayBan("chuan-do"));
            DungMotTram("nhan-biet-ion", new CanhNhanBiet(), LayBan("nhan-biet-ion"));
            DungMotTram("pin-galvani", new CanhPin(), LayBan("pin-galvani"));
            DungMotTram("dien-phan", new CanhDienPhan(), LayBan("dien-phan"));

            // Mặc định chọn trạm an-toan
            ChuyenTram("an-toan");
        }

        private void DungMotTram(string id, CanhCoSo canh, Transform gocBan)
        {
            if (gocBan == null)
            {
                Debug.LogWarning($"[QuanLyTram] Không tìm thấy bàn cho trạm: {id}, tạo gốc dự phòng.");
                var goDuPhong = new GameObject("BanDuPhong_" + id);
                goDuPhong.transform.SetParent(transform, false);
                gocBan = goDuPhong.transform;
            }

            _cacCanh[id] = canh;
            try
            {
                canh.Dung(gocBan, this, "{}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuanLyTram] Lỗi dựng trạm {id}: {ex.Message}");
            }
        }

        public void ChuyenTram(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id == "nhan-biet") id = "nhan-biet-ion";
            TramHienTaiId = id;
            if (Loi == null)
            {
                Loi = TaoLoiHoaHoc();
                Loi.OnTrangThai += XuLyTrangThaiMoi;
                Loi.OnHieuUng += XuLyHieuUngMoi;
            }
            Loi.Tao(id);
            Loi.BatDauMoPhong();
            CauNoiTrang.BaoTram(id, true);
        }

        public void RoiTram()
        {
            Loi?.TamDungMoPhong();
            CauNoiTrang.BaoTram(TramHienTaiId ?? "", false);
        }

        void Update()
        {
            Loi?.CapNhatLuongChinh();

            if (CanhHienTai != null)
            {
                CanhHienTai.Tick(Time.deltaTime);
            }

            // Xử lý click chuột chọn dụng cụ
            if (Input.GetMouseButtonDown(0))
            {
                KiemTraClickDungCu();
            }
        }

        private void KiemTraClickDungCu()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 20f))
            {
                var nhap = hit.collider.GetComponentInParent<LabNhap>();
                if (nhap != null)
                {
                    if (!string.IsNullOrEmpty(nhap.chonLoai))
                    {
                        GuiChon(nhap.chonLoai, nhap.chonSo);
                    }
                    else if (!string.IsNullOrEmpty(nhap.lenh))
                    {
                        GuiThaoTac(nhap.lenh, nhap.thamSo);
                    }
                }
            }
        }

        private void XuLyTrangThaiMoi(string json)
        {
            if (!string.IsNullOrEmpty(TramHienTaiId))
            {
                _trangThaiMoiNhat[TramHienTaiId] = json;
                if (_cacCanh.TryGetValue(TramHienTaiId, out var canh))
                {
                    canh.CapNhat(json);
                }
            }
        }

        private void XuLyHieuUngMoi(SuKienHieuUng e)
        {
            if (CanhHienTai != null)
            {
                var h = new HieuUngDL { loai = e.Ten, id = e.Ten };
                CanhHienTai.HieuUng(h);
            }
        }

        public void GuiThaoTac(string lenh, string thamSoJson = null)
        {
            Loi?.ThaoTac(lenh, thamSoJson);
        }

        public void GuiChon(string loai, int so)
        {
            GuiThaoTac("chon_" + loai, "{\"so\":" + so + "}");
        }

        void OnDestroy()
        {
            Loi?.Dispose();
        }
    }
}
