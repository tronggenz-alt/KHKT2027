using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem.NhanVat
{
    public enum SuKienLab
    {
        MoApp,
        VaoToaNha,
        VaoBanThiNghiem,
        BamDiemNong,
        MoBangTuanHoan,
        ChoKetQua,
        TraLoiDung,
        TraLoiSai,
        HoiGoiY,
        BanMayTinh,
        TuHoSo,
        DiBo,
        Chay,
        Nhay,
        RoiTuDo,
        TiepDat,

        // --- Các sự kiện thao tác bàn thí nghiệm (26/10/2026) ---
        MoKhoaBurette,      // reaching
        NhoThuocThu,        // reaching
        CamOngNghiem,       // picking_up
        DatDungCu,          // putting_down
        BatNguonDC,         // button_push
        BamDongHoDo,        // button_push
        ChoPhanUng,         // looking_down
        XuatHienKetTua,     // surprised
        DoiMauDungDich,     // surprised
        SuiBotKhi,          // surprised
        DungMotBuoc,        // thumbs_up
        HoanThanhThiNghiem, // clapping
        SaiThaoTac,         // shaking_head_no
        QuenDoBaoHo,        // shaking_head_no
        TraLoiSaiOnThi,     // disappointed

        // --- Di chuyển & Chuyển trạng thái nâng cao ---
        BatDauDi,           // start_walking
        DungLai,            // stop_walking
        QuayTrai90,         // turn_left_90
        QuayPhai90,         // turn_right_90
        DiLui,              // walk_backward
        SangTrai,           // strafe_left
        SangPhai,           // strafe_right

        // --- Ngồi & Đứng chờ phong phú ---
        NgoiXuong,          // stand_to_sit
        DangNgoi,           // sitting_idle
        DungDay,            // sit_to_stand
        NgoiNoiChuyen,      // sitting_talking
        NhinQuanh,          // looking_around
        DonTrongTam,        // weight_shift
        XemDongHo,          // checking_watch
        GaiDau,             // scratching_head
        VuonVai             // stretching
    }

    /// <summary>
    /// Bảng ánh xạ sự kiện phòng lab -> Clip hoạt ảnh Mixamo theo manifest nhan-vat-nha-nghien-cuu.json.
    /// Viết bằng C# thuần để chạy được cả trong Unity EditMode và Runtime.
    /// </summary>
    public static class BangTrangThaiNhanVat
    {
        private static readonly Dictionary<string, string> _bangAnhXaMacDinh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "MoApp", "wave" },
            { "VaoToaNha", "wave" },
            { "VaoBanThiNghiem", "nod" },
            { "BamDiemNong", "point" },
            { "MoBangTuanHoan", "point_back" },
            { "ChoKetQua", "think" },
            { "TraLoiDung", "happy" },
            { "TraLoiSai", "think" },
            { "HoiGoiY", "ask" },
            { "BanMayTinh", "typing" },
            { "TuHoSo", "search_files" },
            { "DiBo", "walk" },
            { "Chay", "running" },
            { "Nhay", "jump" },
            { "RoiTuDo", "falling_idle" },
            { "TiepDat", "landing" },

            // Thao tác bàn thí nghiệm
            { "MoKhoaBurette", "reaching" },
            { "NhoThuocThu", "reaching" },
            { "CamOngNghiem", "picking_up" },
            { "DatDungCu", "putting_down" },
            { "BatNguonDC", "button_push" },
            { "BamDongHoDo", "button_push" },
            { "ChoPhanUng", "looking_down" },
            { "XuatHienKetTua", "surprised" },
            { "DoiMauDungDich", "surprised" },
            { "SuiBotKhi", "surprised" },
            { "DungMotBuoc", "thumbs_up" },
            { "HoanThanhThiNghiem", "clapping" },
            { "SaiThaoTac", "shaking_head_no" },
            { "QuenDoBaoHo", "shaking_head_no" },
            { "TraLoiSaiOnThi", "disappointed" },

            // Di chuyển chân thực
            { "BatDauDi", "start_walking" },
            { "DungLai", "stop_walking" },
            { "QuayTrai90", "turn_left_90" },
            { "QuayPhai90", "turn_right_90" },
            { "DiLui", "walk_backward" },
            { "SangTrai", "strafe_left" },
            { "SangPhai", "strafe_right" },

            // Ngồi & Đứng chờ
            { "NgoiXuong", "stand_to_sit" },
            { "DangNgoi", "sitting_idle" },
            { "DungDay", "sit_to_stand" },
            { "NgoiNoiChuyen", "sitting_talking" },
            { "NhinQuanh", "looking_around" },
            { "DonTrongTam", "weight_shift" },
            { "XemDongHo", "checking_watch" },
            { "GaiDau", "scratching_head" },
            { "VuonVai", "stretching" }
        };

        private static Dictionary<string, string> _bangHienTai = new Dictionary<string, string>(_bangAnhXaMacDinh, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Lấy tên clip hoạt ảnh tương ứng với một sự kiện trong phòng lab.
        /// </summary>
        public static string LayClipChoSuKien(string tenSuKien)
        {
            if (string.IsNullOrEmpty(tenSuKien)) return "idle";
            if (_bangHienTai.TryGetValue(tenSuKien, out var clip) && !string.IsNullOrEmpty(clip))
            {
                return clip;
            }
            return "idle";
        }

        /// <summary>
        /// Ánh xạ từ enum sự kiện sang tên clip Mixamo.
        /// </summary>
        public static string AnhXa(SuKienLab sk)
        {
            return LayClipChoSuKien(sk.ToString());
        }

        /// <summary>
        /// Nạp và cập nhật bảng ánh xạ từ chuỗi JSON manifest.
        /// </summary>
        public static bool DocTuManifest(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                int idxStateMap = json.IndexOf("\"stateMap\"", StringComparison.OrdinalIgnoreCase);
                if (idxStateMap >= 0)
                {
                    int startObj = json.IndexOf('{', idxStateMap);
                    int endObj = json.IndexOf('}', startObj);
                    if (startObj >= 0 && endObj > startObj)
                    {
                        string sub = json.Substring(startObj + 1, endObj - startObj - 1);
                        string[] dong = sub.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var d in dong)
                        {
                            var cap = d.Split(':');
                            if (cap.Length == 2)
                            {
                                string k = cap[0].Trim(' ', '\t', '\"', '\'');
                                string v = cap[1].Trim(' ', '\t', '\"', '\'', '[', ']');
                                if (v.Contains(",")) v = v.Split(',')[0].Trim(' ', '\t', '\"', '\'');
                                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                                {
                                    _bangHienTai[k] = v;
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BangTrangThaiNhanVat] Lỗi phân tích JSON manifest: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Khôi phục bảng ánh xạ về mặc định
        /// </summary>
        public static void DatLaiMacDinh()
        {
            _bangHienTai = new Dictionary<string, string>(_bangAnhXaMacDinh, StringComparer.OrdinalIgnoreCase);
        }
    }
}
