// Dữ liệu 6 quá trình sinh lý thực vật từng bước có mũi tên 3D (C# thuần, không phụ thuộc UnityEngine).
// Thiết kế đồng bộ cấu trúc "Giải thích từng bước (Mũi tên 3D)" của Phòng Giải phẫu (Vòng nhỏ / Vòng lớn tim).
using System;

namespace Khkt.Simulation.Bio.ThucVat
{
    public sealed class BuocQuaTrinhThucVat
    {
        public string TieuDe;
        public string NhanLuong;
        public string DieuGiXayRa;
        public string ViSao;
        public string GhiNho;
        public string KhungNhin;        // "tong_the" | "lat_cat_re" | "lat_cat_than" | "lat_cat_la"
        public string BoPhanNoiBat;
        public string[] NeoTu;          // Điểm neo giải phẫu xuất phát chính xác
        public string[] NeoDen;         // Điểm neo giải phẫu đích chính xác
        public bool TaiCho;             // true = vòng xung nhịp đập tại đúng cơ quan
        public string MauHex;           // Màu mũi tên / vòng xung (#RRGGBB)
        public float ThoiGianVe;        // Thời gian mọc mũi tên (giây)
        public float CamYaw;
        public float CamPitch;
        public float CamDist;
        public float PivotOffX;
        public float PivotOffY;
        public float PivotOffZ;
        public bool CoDatMoiTruong;
        public DieuKienMoiTruong MoiTruongGoiY;
    }

    public sealed class QuaTrinhSinhLyThucVat
    {
        public string Ma;
        public string Ten;
        public string TenNgan;
        public string BieuTuong;
        public string MauHex;
        public string TomTat;
        public BuocQuaTrinhThucVat[] CacBuoc;
    }

    /// <summary>
    /// Danh mục 6 quá trình sinh lý thực vật cốt lõi theo SGK Sinh học 11 (GDPT 2018):
    ///   1. Hấp thu nước & ion khoáng (Rễ → Thân → Lá)
    ///   2. Quang hợp C3 ở lá (Ánh sáng + CO₂ + H₂O → Đường + O₂)
    ///   3. Hô hấp tế bào ở thực vật (Ban đêm: Lá, Thân & Rễ)
    ///   4. Thoát hơi nước & Điều tiết khí khổng (Lực kéo mạch gỗ & ABA chống hạn)
    ///   5. Vận chuyển dòng Mạch rây (Thuyết dòng áp suất Münch từ Nguồn → Chứa)
    ///   6. Tính hướng sáng dương & Phân bố Auxin (IAA)
    /// </summary>
    public static class ThucVatQuaTrinhData
    {
        static readonly DieuKienMoiTruong EnvNgayToiUu = new DieuKienMoiTruong
        {
            AnhSangLux = 28000, Co2Ppm = 420, NhietDoC = 26.5, DoAmKhongKhiPct = 62, DoAmDatPct = 80,
            Gio01 = 0.25, GocDenHuongSangDo = 18, SoGioHuongSang = 8
        };

        static readonly DieuKienMoiTruong EnvBanDem = new DieuKienMoiTruong
        {
            AnhSangLux = 0, Co2Ppm = 410, NhietDoC = 24.5, DoAmKhongKhiPct = 76, DoAmDatPct = 74,
            Gio01 = 0.12, GocDenHuongSangDo = 0, SoGioHuongSang = 0
        };

        static readonly DieuKienMoiTruong EnvThoatHoiManh = new DieuKienMoiTruong
        {
            AnhSangLux = 34000, Co2Ppm = 410, NhietDoC = 30.0, DoAmKhongKhiPct = 38, DoAmDatPct = 84,
            Gio01 = 0.52, GocDenHuongSangDo = 15, SoGioHuongSang = 6
        };

        static readonly DieuKienMoiTruong EnvKhoHanAba = new DieuKienMoiTruong
        {
            AnhSangLux = 36000, Co2Ppm = 400, NhietDoC = 36.5, DoAmKhongKhiPct = 30, DoAmDatPct = 10,
            Gio01 = 0.50, GocDenHuongSangDo = 15, SoGioHuongSang = 6
        };

        static readonly DieuKienMoiTruong EnvHuongSang = new DieuKienMoiTruong
        {
            AnhSangLux = 32000, Co2Ppm = 410, NhietDoC = 26.0, DoAmKhongKhiPct = 65, DoAmDatPct = 76,
            Gio01 = 0.18, GocDenHuongSangDo = 58, SoGioHuongSang = 42
        };

        public static readonly QuaTrinhSinhLyThucVat[] DanhSach =
        {
            // =================================================================
            // 1. QUÁ TRÌNH HẤP THU NƯỚC VÀ ION KHOÁNG (6 BƯỚC)
            // =================================================================
            new QuaTrinhSinhLyThucVat
            {
                Ma = "hap_thu_nuoc",
                Ten = "Hấp thu nước & ion khoáng",
                TenNgan = "Hút nước & khoáng",
                BieuTuong = "1.",
                MauHex = "#38BDF8",
                TomTat = "Dung dịch đất → Lông hút (thẩm thấu/bơm ATP) → Mô mềm vỏ (Gian bào & Tế bào chất) → Đai Caspari kiểm soát chọn lọc → Mạch gỗ hình sao → Dâng theo thân lên tán lá.",
                CacBuoc = new[]
                {
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Thẩm thấu nước & hút khoáng vào Lông hút",
                        NhanLuong = "Hấp thu tại rễ",
                        DieuGiXayRa = "Nước từ màng nước trong khe đất thẩm thấu qua thành xenlulôzơ mỏng không thấm cutin đi vào không bào tế bào lông hút; đồng thời ion khoáng (K⁺, NO₃⁻, NH₄⁺) đi vào theo cơ chế thụ động hoặc chủ động.",
                        ViSao = "Dịch không bào lông hút luôn ưu trương hơn dung dịch đất (do thoát hơi nước ở lá và tích lũy chất tan trong rễ), tạo chênh lệch thế nước kéo nước tự động vào rễ.",
                        GhiNho = "Rễ hút nước thụ động (thẩm thấu), hút khoáng vừa thụ động vừa chủ động (cần ATP từ hô hấp rễ).",
                        KhungNhin = "lat_cat_re", BoPhanNoiBat = "re_long_hut",
                        NeoTu = new[] { "re_dat_trai", "re_dat_phai" },
                        NeoDen = new[] { "re_long_hut_trai", "re_long_hut_phai" },
                        TaiCho = false, MauHex = "#38BDF8", ThoiGianVe = 1.8f,
                        CamYaw = 20f, CamPitch = 36f, CamDist = 0.78f,
                        CoDatMoiTruong = true, MoiTruongGoiY = EnvNgayToiUu
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Vận chuyển hướng tâm qua Mô mềm vỏ",
                        NhanLuong = "Gian bào & Tế bào chất",
                        DieuGiXayRa = "Từ biểu bì, nước và ion khoáng di chuyển sâu vào vòng nội bì theo 2 con đường song song: Con đường gian bào (xanh ngọc — đi theo vách & khe gian bào) và Con đường tế bào chất (đi xuyên màng & cầu sinh chất).",
                        ViSao = "Con đường gian bào di chuyển nhanh và ít sức cản nhưng chưa có sự kiểm soát chọn lọc nồng độ ion khoáng.",
                        GhiNho = "Gồm 2 con đường hướng tâm: Gian bào (nhanh, chưa chọn lọc) và Tế bào chất (chậm hơn, có chọn lọc).",
                        KhungNhin = "lat_cat_re", BoPhanNoiBat = "re_vo_nhu_mo",
                        NeoTu = new[] { "re_bieu_bi_apo", "re_bieu_bi_sym" },
                        NeoDen = new[] { "re_caspari_apo", "re_caspari_sym" },
                        TaiCho = false, MauHex = "#06B6D4", ThoiGianVe = 1.9f,
                        CamYaw = 14f, CamPitch = 40f, CamDist = 0.66f, PivotOffY = 0.01f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Chốt kiểm soát sinh học tại Đai Caspari",
                        NhanLuong = "Chọn lọc tại Nội bì",
                        DieuGiXayRa = "Đai Caspari thấm chất bần suberin kị nước trên vách xuyên tâm tế bào nội bì chặn đứng dòng gian bào, buộc toàn bộ nước và ion phải xuyên qua màng sinh chất của tế bào nội bì.",
                        ViSao = "Màng sinh chất nội bì chứa các kênh protein vận chuyển đặc hiệu giúp điều chỉnh lượng nước và ngăn độc chất, kim loại nặng xâm nhập vào mạch gỗ.",
                        GhiNho = "Đai Caspari = 'cửa khẩu kiểm tra' chuyển toàn bộ dòng gian bào sang con đường tế bào chất.",
                        KhungNhin = "lat_cat_re", BoPhanNoiBat = "re_dai_caspari",
                        NeoTu = new[] { "re_caspari_apo", "re_caspari_sym" },
                        NeoDen = new[] { "re_caspari_apo", "re_caspari_sym" },
                        TaiCho = true, MauHex = "#EF4444", ThoiGianVe = 1.6f,
                        CamYaw = 10f, CamPitch = 42f, CamDist = 0.56f, PivotOffY = 0.02f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Đổ vào Mạch gỗ hình sao (Trung trụ)",
                        NhanLuong = "Vào trụ dẫn Xylem",
                        DieuGiXayRa = "Sau khi qua tế bào nội bì và vòng trụ bì, nước cùng ion khoáng được đẩy vào các ống mạch gỗ rỗng lòng xếp thành hình sao 4 cánh ở chính tâm trung trụ rễ và dâng thẳng lên trên.",
                        ViSao = "Các tế bào mạch gỗ là tế bào chết đã tiêu biến màng ngăn ngang, vách hóa gỗ lignin dày bền chắc tạo thành hệ ống mao dẫn thông suốt.",
                        GhiNho = "Mạch gỗ rễ hai lá mầm nằm ở tâm, xếp hình chữ thập / ngôi sao 4 cánh xen kẽ với 4 bó mạch rây.",
                        KhungNhin = "lat_cat_re", BoPhanNoiBat = "re_mach_go",
                        NeoTu = new[] { "re_caspari_sym", "re_xylem_tam" },
                        NeoDen = new[] { "re_xylem_tam", "re_xylem_dinh" },
                        TaiCho = false, MauHex = "#3B82F6", ThoiGianVe = 1.8f,
                        CamYaw = 24f, CamPitch = 36f, CamDist = 0.64f, PivotOffY = 0.04f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Dòng mạch gỗ đi lên dọc trục thân",
                        NhanLuong = "Dòng mạch gỗ ↑",
                        DieuGiXayRa = "Cột nước liên tục chứa ion khoáng hòa tan dâng một chiều từ hệ rễ cọc đi xuyên qua các bó mạch gỗ trong thân cây đậu lên tới các đốt trên.",
                        ViSao = "Nhờ sự phối hợp của 3 động lực: Áp suất rễ (lực đẩy từ dưới), lực liên kết H₂O–H₂O và bám dính thành mạch (trung gian), cùng lực kéo thoát hơi nước ở lá (lực hút chính).",
                        GhiNho = "Dòng mạch gỗ luôn đi MỘT CHIỀU từ rễ lên thân và lá.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "than_cay",
                        NeoTu = new[] { "cay_re_giua", "cay_than_cat_duoi" },
                        NeoDen = new[] { "cay_than_cat_duoi", "cay_than_dot3" },
                        TaiCho = false, MauHex = "#38BDF8", ThoiGianVe = 2.0f,
                        CamYaw = 18f, CamPitch = 12f, CamDist = 1.48f, PivotOffY = -0.02f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Phân phối nước tới tán lá kép",
                        NhanLuong = "Cấp nước quang hợp",
                        DieuGiXayRa = "Từ mấu đốt thân, dòng mạch gỗ rẽ theo cuống lá vào hệ gân lá hình mạng, cấp nước tức thì cho hàng vạn tế bào mô giậu quang hợp và bù lượng nước thoát qua khí khổng.",
                        ViSao = "Khoảng 98% lượng nước cây hút vào được thoát ra dạng hơi để tạo lực kéo và làm mát lá, còn ~2% tạo môi trường sống và tham gia quang phân li nước.",
                        GhiNho = "Hệ gân hình mạng phân nhánh dày đặc giúp phân phối nước nhanh đều khắp phiến lá hai lá mầm.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "la_cay",
                        NeoTu = new[] { "cay_than_dot3", "cay_than_dot4" },
                        NeoDen = new[] { "cay_la3_phien", "cay_la4_phien" },
                        TaiCho = false, MauHex = "#0EA5E9", ThoiGianVe = 1.8f,
                        CamYaw = 22f, CamPitch = 16f, CamDist = 1.32f, PivotOffY = 0.14f
                    }
                }
            },

            // =================================================================
            // 2. QUÁ TRÌNH QUANG HỢP C3 Ở LÁ (5 BƯỚC)
            // =================================================================
            new QuaTrinhSinhLyThucVat
            {
                Ma = "quang_hop_c3",
                Ten = "Quá trình Quang hợp C3 ở lá",
                TenNgan = "Quang hợp C3",
                BieuTuong = "2.",
                MauHex = "#10B981",
                TomTat = "Ánh sáng PAR xuyên biểu bì + Nước từ mạch gỗ gân lá + CO₂ qua khí khổng → Quang hợp tại lục lạp mô giậu → Thải O₂ ra khí khổng & Nạp đường vào mạch rây.",
                CacBuoc = new[]
                {
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Ánh sáng PAR xuyên tầng Cutin & Biểu bì trên",
                        NhanLuong = "Năng lượng ánh sáng",
                        DieuGiXayRa = "Các photon ánh sáng vùng khả kiến (đỏ & xanh tím, 400–700 nm) chiếu xuống mặt trên lá, xuyên qua lớp sáp cutin và hàng tế bào biểu bì trong suốt đi thẳng tới tầng mô giậu.",
                        ViSao = "Tế bào biểu bì trên không chứa lục lạp nên hoàn toàn trong suốt, vừa bảo vệ lá vừa dẫn tối đa năng lượng ánh sáng xuống mô giậu bên dưới.",
                        GhiNho = "Diệp lục a và b hấp thụ mạnh nhất ánh sáng đỏ và xanh tím, phản xạ ánh sáng lục nên lá có màu xanh.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_bieu_bi_cutin",
                        NeoTu = new[] { "la_anh_sang_trai", "la_anh_sang_phai" },
                        NeoDen = new[] { "la_mo_giau_trai", "la_mo_giau_phai" },
                        TaiCho = false, MauHex = "#FACC15", ThoiGianVe = 1.7f,
                        CamYaw = 18f, CamPitch = 22f, CamDist = 0.78f,
                        CoDatMoiTruong = true, MoiTruongGoiY = EnvNgayToiUu
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Cấp nước từ Mạch gỗ gân lá sang Thịt lá",
                        NhanLuong = "Nguyên liệu H₂O",
                        DieuGiXayRa = "Nước đi dọc theo các ống mạch gỗ ở nửa trên bó mạch gân chính, xuyên qua vòng tế bào bao bó mạch lan tỏa tới từng tế bào mô giậu và mô xốp.",
                        ViSao = "Nước là nguyên liệu cho phản ứng quang phân li nước ở pha sáng (2H₂O → 4H⁺ + 4e⁻ + O₂↑) cung cấp electron bù cho diệp lục và giải phóng khí O₂.",
                        GhiNho = "Quy tắc bó mạch lá hai lá mầm: Mạch gỗ nằm TRÊN (sát mô giậu), Mạch rây nằm DƯỚI.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_gan_bo_mach",
                        NeoTu = new[] { "la_gan_go_truoc", "la_gan_go_truoc" },
                        NeoDen = new[] { "la_mo_giau_trai", "la_mo_giau_phai" },
                        TaiCho = false, MauHex = "#38BDF8", ThoiGianVe = 1.8f,
                        CamYaw = 14f, CamPitch = 18f, CamDist = 0.70f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Khí CO₂ khuếch tán qua Khí khổng vào Mô giậu",
                        NhanLuong = "Khuếch tán CO₂ vào",
                        DieuGiXayRa = "Khi hai tế bào hạt đậu trương nước mở khe khí khổng, CO₂ khí quyển đi vào buồng dưới khí khổng, luồn qua các khoang khí rộng của mô xốp lên tới màng tế bào mô giậu.",
                        ViSao = "Mô xốp xếp thưa tạo hệ thống khoang khí liên thông giúp CO₂ khuếch tán nhanh gấp hàng nghìn lần so với đi qua pha lỏng.",
                        GhiNho = "100% lượng CO₂ cho quang hợp đi vào lá qua hệ thống khí khổng ở mặt dưới lá.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_khi_khong",
                        NeoTu = new[] { "la_ngoai_kk_phai", "la_buong_khi_phai" },
                        NeoDen = new[] { "la_buong_khi_phai", "la_mo_giau_phai" },
                        TaiCho = false, MauHex = "#CBD5E1", ThoiGianVe = 1.9f,
                        CamYaw = 24f, CamPitch = 12f, CamDist = 0.72f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Pha sáng & Chu trình Calvin tại Lục lạp",
                        NhanLuong = "Đồng hóa C3",
                        DieuGiXayRa = "Trên màng tilacôit (pha sáng), ánh sáng tạo ATP, NADPH và giải phóng O₂. Trong chất nền strôma (pha tối — chu trình Calvin), enzym Rubisco cố định CO₂ thành đường Glucôzơ.",
                        ViSao = "Tế bào mô giậu hình trụ xếp đứng khít ngay dưới mặt trên chứa mật độ lục lạp dày đặc nhất, tối ưu hoá hiệu suất quang hợp của cây đậu C3.",
                        GhiNho = "6CO₂ + 6H₂O —(Ánh sáng, Diệp lục)→ C₆H₁₂O₆ + 6O₂.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_mo_giau_va_mo_xop",
                        NeoTu = new[] { "la_mo_giau_trai", "la_mo_giau_phai" },
                        NeoDen = new[] { "la_mo_giau_trai", "la_mo_giau_phai" },
                        TaiCho = true, MauHex = "#10B981", ThoiGianVe = 1.7f,
                        CamYaw = 16f, CamPitch = 18f, CamDist = 0.66f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Thải khí O₂ qua Khí khổng & Nạp đường vào Mạch rây",
                        NhanLuong = "Sản phẩm O₂ & Đường",
                        DieuGiXayRa = "Khí O₂ sinh ra từ quang phân li nước khuếch tán qua mô xốp thoát ra ngoài qua khí khổng; đồng thời đường tổng hợp được chuyển thành Saccarôzơ nạp vào Mạch rây (nửa dưới gân lá).",
                        ViSao = "Giải phóng O₂ điều hoà khí quyển Trái Đất, còn nạp nhanh đường vào mạch rây giúp tránh ứ đọng sản phẩm làm ức chế phản ứng quang hợp.",
                        GhiNho = "Quang hợp tạo 2 đầu ra song song: O₂ thoát ra khí khổng và Saccarôzơ chảy về thân qua mạch rây.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_gan_bo_mach",
                        NeoTu = new[] { "la_mo_giau_phai", "la_mo_giau_trai" },
                        NeoDen = new[] { "la_ngoai_kk_phai", "la_gan_ray_truoc" },
                        TaiCho = false, MauHex = "#34D399", ThoiGianVe = 2.0f,
                        CamYaw = 20f, CamPitch = 16f, CamDist = 0.74f
                    }
                }
            },

            // =================================================================
            // 3. QUÁ TRÌNH HÔ HẤP TẾ BÀO Ở THỰC VẬT (5 BƯỚC — BAN ĐÊM 0 LUX)
            // =================================================================
            new QuaTrinhSinhLyThucVat
            {
                Ma = "ho_hap_te_bao",
                Ten = "Quá trình Hô hấp tế bào ở thực vật",
                TenNgan = "Hô hấp tế bào",
                BieuTuong = "3.",
                MauHex = "#F97316",
                TomTat = "Ban đêm (0 lux): O₂ khuếch tán vào tế bào lá & rễ → Oxi hoá Glucôzơ trong Tế bào chất & Ti thể tạo 30–32 ATP → Thải khí CO₂ ra ngoài & cấp năng lượng cho rễ hút khoáng.",
                CacBuoc = new[]
                {
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Khuếch tán khí O₂ vào thịt lá (Ban đêm)",
                        NhanLuong = "Lấy O₂ hô hấp",
                        DieuGiXayRa = "Khi ánh sáng tắt (0 lux), pha sáng quang hợp dừng hoàn toàn. Khí O₂ từ không khí ngoài khuếch tán qua khe khí khổng khép hờ và gian bào đi vào tế bào thịt lá.",
                        ViSao = "Thực vật hô hấp liên tục 24/24 giờ ở mọi cơ quan sống, nhưng ban đêm không có quang hợp bù lại nên cây lấy O₂ và thải ròng CO₂.",
                        GhiNho = "Ban đêm: Quang hợp = 0, cây chỉ thực hiện Hô hấp hiếu khí (quang hợp ròng mang giá trị âm).",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_khi_khong",
                        NeoTu = new[] { "la_ngoai_kk_phai", "la_buong_khi_phai" },
                        NeoDen = new[] { "la_buong_khi_phai", "la_mo_xop_phai" },
                        TaiCho = false, MauHex = "#34D399", ThoiGianVe = 1.8f,
                        CamYaw = 22f, CamPitch = 14f, CamDist = 0.74f,
                        CoDatMoiTruong = true, MoiTruongGoiY = EnvBanDem
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Phân giải Glucôzơ tạo ATP trong Ti thể",
                        NhanLuong = "Đường phân & Krebs",
                        DieuGiXayRa = "Trong tế bào chất (đường phân) và ti thể (chu trình Krebs + chuỗi chuyền electron), phân tử Glucôzơ bị oxi hoá hoàn toàn bởi O₂ tạo năng lượng sinh học ATP và nhiệt lượng.",
                        ViSao = "Năng lượng ATP duy trì các hoạt động sống của tế bào thực vật: tổng hợp prôtêin, phân chia tế bào, sửa chữa màng và vận chuyển chủ động các chất.",
                        GhiNho = "C₆H₁₂O₆ + 6O₂ → 6CO₂ + 6H₂O + Năng lượng (30–32 ATP + Nhiệt).",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_mo_giau_va_mo_xop",
                        NeoTu = new[] { "la_mo_giau_trai", "la_mo_xop_phai" },
                        NeoDen = new[] { "la_mo_giau_trai", "la_mo_xop_phai" },
                        TaiCho = true, MauHex = "#F97316", ThoiGianVe = 1.7f,
                        CamYaw = 16f, CamPitch = 18f, CamDist = 0.68f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Thải khí CO₂ hô hấp ra không khí",
                        NhanLuong = "Thải CO₂ ban đêm",
                        DieuGiXayRa = "Khí CO₂ sinh ra từ chu trình Krebs tích tụ trong tế bào thịt lá, khuếch tán ra khoang khí mô xốp và thoát ngược ra môi trường không khí bên ngoài.",
                        ViSao = "Vì ban đêm cây hấp thụ O₂ và thải CO₂ (không thải O₂), không nên đặt quá nhiều chậu cây xanh trong phòng ngủ đóng kín cửa.",
                        GhiNho = "Cường độ hô hấp tăng gấp đôi khi nhiệt độ tăng thêm 10 °C (quy luật Q₁₀ ≈ 2).",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_khi_khong",
                        NeoTu = new[] { "la_mo_giau_trai", "la_buong_khi_trai" },
                        NeoDen = new[] { "la_buong_khi_trai", "la_ngoai_kk_trai" },
                        TaiCho = false, MauHex = "#94A3B8", ThoiGianVe = 1.8f,
                        CamYaw = -16f, CamPitch = 15f, CamDist = 0.74f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Chuyển đường dự trữ xuống nuôi rễ suốt đêm",
                        NhanLuong = "Cấp cơ chất cho rễ",
                        DieuGiXayRa = "Tinh bột tích luỹ ở lá ban ngày được thuỷ phân thành Saccarôzơ, tiếp tục vận chuyển theo dòng mạch rây đi dọc thân xuống hệ rễ cọc trong đất.",
                        ViSao = "Rễ nằm trong đất tối không thể tự quang hợp nên phụ thuộc hoàn toàn vào nguồn đường từ lá chuyển xuống làm nguyên liệu hô hấp.",
                        GhiNho = "Quang hợp ban ngày tạo nguyên liệu cho Hô hấp ban đêm ở toàn bộ cơ thể cây.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "than_cay",
                        NeoTu = new[] { "cay_la3_phien", "cay_than_dot3" },
                        NeoDen = new[] { "cay_than_dot3", "cay_re_giua" },
                        TaiCho = false, MauHex = "#F59E0B", ThoiGianVe = 2.0f,
                        CamYaw = 20f, CamPitch = 12f, CamDist = 1.56f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Hô hấp hiếu khí ở Rễ tạo ATP bơm ion khoáng",
                        NhanLuong = "Hô hấp tại rễ",
                        DieuGiXayRa = "O₂ trong khe thoáng của đất tơi xốp thấm qua tế bào lông hút vào mô mềm vỏ rễ để oxi hoá đường tạo ATP cho bơm ion khoáng, đồng thời thải CO₂ ra đất.",
                        ViSao = "Nếu đất bị ngập úng kéo dài, rễ thiếu O₂ chuyển sang hô hấp kị khí tạo ít ATP và sinh cồn/axit độc gây thối rễ, làm cây héo chết.",
                        GhiNho = "Xới đất tơi xốp & thoát nước tốt giúp rễ hô hấp mạnh, hấp thu phân bón hiệu quả nhất.",
                        KhungNhin = "lat_cat_re", BoPhanNoiBat = "re_vo_nhu_mo",
                        NeoTu = new[] { "re_dat_phai", "re_long_hut_phai" },
                        NeoDen = new[] { "re_long_hut_phai", "re_caspari_sym" },
                        TaiCho = false, MauHex = "#EF4444", ThoiGianVe = 1.9f,
                        CamYaw = 22f, CamPitch = 36f, CamDist = 0.78f
                    }
                }
            },

            // =================================================================
            // 4. QUÁ TRÌNH THOÁT HƠI NƯỚC & ĐIỀU TIẾT KHÍ KHỔNG (5 BƯỚC)
            // =================================================================
            new QuaTrinhSinhLyThucVat
            {
                Ma = "thoat_hoi_nuoc",
                Ten = "Thoát hơi nước & Điều tiết khí khổng",
                TenNgan = "Thoát hơi nước",
                BieuTuong = "4.",
                MauHex = "#0EA5E9",
                TomTat = "Mạch gỗ thân → Mạch gỗ gân lá → Màng ẩm vách tế bào mô xốp → Bốc hơi vào khoang khí → Thoát qua 2 khe khí khổng tạo lực kéo nước; khi khô hạn ABA đóng kín khí khổng.",
                CacBuoc = new[]
                {
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Cột nước liên tục dâng lên trong Mạch gỗ thân",
                        NhanLuong = "Lực kéo mạch gỗ ↑",
                        DieuGiXayRa = "Dưới lực hút áp suất âm từ tán lá, các phân tử nước liên kết chặt bằng liên kết hiđrô kéo nhau dâng liên tục trong các ống Metaxylem và Protoxylem của bó mạch thân.",
                        ViSao = "Các vòng/lò xo vân xoắn lignin gia cố thành trong của ống mạch gỗ giúp ống không bị xẹp dưới lực kéo căng rất lớn.",
                        GhiNho = "Thoát hơi nước ở lá là 'động cơ phía trên' kéo cột nước lên đỉnh cây cao hàng chục mét.",
                        KhungNhin = "lat_cat_than", BoPhanNoiBat = "than_mach_go",
                        NeoTu = new[] { "than_xylem_trai_duoi", "than_xylem_phai_duoi" },
                        NeoDen = new[] { "than_xylem_trai_tren", "than_xylem_phai_tren" },
                        TaiCho = false, MauHex = "#38BDF8", ThoiGianVe = 1.8f,
                        CamYaw = 22f, CamPitch = 34f, CamDist = 0.74f,
                        CoDatMoiTruong = true, MoiTruongGoiY = EnvThoatHoiManh
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Nước thấm từ Gân lá sang vách tế bào Mô xốp",
                        NhanLuong = "Vào thịt lá ẩm",
                        DieuGiXayRa = "Nước lỏng thoát khỏi mạch gỗ gân chính, lan theo vách tế bào xenlulôzơ tạo thành lớp màng nước mỏng bao quanh các tế bào mô xốp.",
                        ViSao = "Khi nước trên bề mặt mô xốp bốc hơi, sức căng mặt ngoài kéo thêm nước lỏng từ mạch gỗ gân lá thay thế liên tục.",
                        GhiNho = "Mạch gỗ gân lá nối thẳng với mạng vách tế bào thịt lá tạo dòng cấp ẩm không gián đoạn.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_gan_bo_mach",
                        NeoTu = new[] { "la_gan_go_truoc", "la_gan_go_truoc" },
                        NeoDen = new[] { "la_mo_xop_trai", "la_mo_xop_phai" },
                        TaiCho = false, MauHex = "#0EA5E9", ThoiGianVe = 1.8f,
                        CamYaw = 18f, CamPitch = 16f, CamDist = 0.72f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Bốc hơi nước vào Buồng dưới khí khổng",
                        NhanLuong = "Bay hơi làm mát lá",
                        DieuGiXayRa = "Nhiệt năng từ bức xạ Mặt Trời làm nước lỏng trên màng tế bào mô xốp hoá hơi, tích tụ trong khoang khí dưới khí khổng (độ ẩm gần 100%).",
                        ViSao = "Quá trình chuyển thể lỏng → hơi thu ẩn nhiệt bay hơi rất lớn, giúp hạ nhiệt độ phiến lá 2–5 °C tránh cháy lá giữa trưa nắng.",
                        GhiNho = "Thoát hơi nước chính là 'hệ thống điều hoà nhiệt độ tự nhiên' bảo vệ enzym quang hợp.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_mo_giau_va_mo_xop",
                        NeoTu = new[] { "la_mo_xop_trai", "la_mo_xop_phai" },
                        NeoDen = new[] { "la_buong_khi_trai", "la_buong_khi_phai" },
                        TaiCho = false, MauHex = "#7DD3FC", ThoiGianVe = 1.7f,
                        CamYaw = 18f, CamPitch = 12f, CamDist = 0.70f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Khuếch tán hơi nước ra ngoài qua Khe khí khổng",
                        NhanLuong = "Thoát hơi qua khí khổng",
                        DieuGiXayRa = "Hơi nước từ buồng dưới khí khổng khuếch tán mạnh qua 2 khe khí khổng đang mở rộng ra lớp không khí khô hơn bên ngoài (chiếm >90% tổng lượng nước thoát).",
                        ViSao = "Khi hai tế bào hạt đậu trương nước, thành ngoài mỏng dãn mạnh hơn thành trong dày, kéo cong hai vách tạo khe mở rộng.",
                        GhiNho = "Gió thổi và không khí khô làm tăng chênh lệch ẩm, đẩy tốc độ thoát hơi nước lên rất cao.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_khi_khong",
                        NeoTu = new[] { "la_buong_khi_trai", "la_buong_khi_phai" },
                        NeoDen = new[] { "la_ngoai_kk_trai", "la_ngoai_kk_phai" },
                        TaiCho = false, MauHex = "#38BDF8", ThoiGianVe = 1.9f,
                        CamYaw = 18f, CamPitch = 10f, CamDist = 0.74f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Cơ chế chống hạn: ABA đóng kín Khí khổng",
                        NhanLuong = "Tín hiệu hạn ABA",
                        DieuGiXayRa = "Quan sát 2 tế bào hạt đậu vừa xẹp khép khe lại! Khi đất khô hạn (10%), rễ tiết hoocmôn axit abxixic (ABA) gửi lên lá kích hoạt thoát ion K⁺ ra khỏi tế bào bảo vệ.",
                        ViSao = "Mất K⁺ làm tế bào hạt đậu mất nước nhanh, giảm sức trương nên thành trong khép chặt lại, giữ nước cứu cây khỏi héo chết.",
                        GhiNho = "Thoát hơi nước qua khí khổng có vận tốc lớn và được điều chỉnh chủ động bởi ánh sáng & ABA.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_khi_khong",
                        NeoTu = new[] { "la_khikhong_trai", "la_khikhong_phai" },
                        NeoDen = new[] { "la_khikhong_trai", "la_khikhong_phai" },
                        TaiCho = true, MauHex = "#F59E0B", ThoiGianVe = 1.7f,
                        CamYaw = 22f, CamPitch = 8f, CamDist = 0.66f,
                        CoDatMoiTruong = true, MoiTruongGoiY = EnvKhoHanAba
                    }
                }
            },

            // =================================================================
            // 5. VẬN CHUYỂN CHẤT HỮU CƠ TRONG MẠCH RÂY (5 BƯỚC)
            // =================================================================
            new QuaTrinhSinhLyThucVat
            {
                Ma = "dong_mach_ray",
                Ten = "Vận chuyển dòng Mạch rây (Saccarôzơ)",
                TenNgan = "Dòng mạch rây",
                BieuTuong = "5.",
                MauHex = "#F59E0B",
                TomTat = "Lá nguồn nạp chủ động Saccarôzơ vào mạch rây → Hút nước từ mạch gỗ tạo áp suất đẩy (Münch) → Chạy qua bản rây ngoài bó mạch thân → Phân phối nuôi Quả đậu non, Hoa & Hệ rễ.",
                CacBuoc = new[]
                {
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Nạp chủ động Saccarôzơ từ Mô giậu vào Mạch rây",
                        NhanLuong = "Nạp đường tại lá nguồn",
                        DieuGiXayRa = "Đường tổng hợp sau quang hợp được chuyển thành Saccarôzơ và bơm chủ động qua tế bào kèm vào các ống rây nằm ở nửa dưới bó mạch gân lá.",
                        ViSao = "Saccarôzơ là đường đôi không có tính khử, bền vững về mặt hóa học nên không bị phản ứng hao hụt dọc đường vận chuyển xa.",
                        GhiNho = "Thành phần chủ yếu của dịch mạch rây là Saccarôzơ (10–20%), axit amin, vitamin và hoocmôn.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_gan_bo_mach",
                        NeoTu = new[] { "la_mo_giau_trai", "la_mo_giau_phai" },
                        NeoDen = new[] { "la_gan_ray_truoc", "la_gan_ray_truoc" },
                        TaiCho = false, MauHex = "#FBBF24", ThoiGianVe = 1.8f,
                        CamYaw = 16f, CamPitch = 16f, CamDist = 0.72f,
                        CoDatMoiTruong = true, MoiTruongGoiY = EnvNgayToiUu
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Hút nước từ Mạch gỗ kế cận tạo áp suất đẩy",
                        NhanLuong = "Dòng áp suất Münch",
                        DieuGiXayRa = "Nồng độ đường tăng vọt trong ống rây tạo áp suất thẩm thấu lớn, kéo nước từ các ống mạch gỗ ngay phía trên thấm sang làm tăng vọt áp suất thuỷ tĩnh trong mạch rây.",
                        ViSao = "Sự chênh lệch áp suất thuỷ tĩnh giữa cơ quan nguồn (áp suất cao ở lá) và cơ quan chứa (áp suất thấp ở rễ/quả) tạo động lực đẩy dòng dịch mạch rây.",
                        GhiNho = "Mạch gỗ và mạch rây luôn nằm kề nhau để trao đổi nước duy trì động lực áp suất cho mạch rây.",
                        KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_gan_bo_mach",
                        NeoTu = new[] { "la_gan_go_truoc", "la_gan_ray_truoc" },
                        NeoDen = new[] { "la_gan_ray_truoc", "la_gan_ray_sau" },
                        TaiCho = false, MauHex = "#F59E0B", ThoiGianVe = 1.9f,
                        CamYaw = 28f, CamPitch = 16f, CamDist = 0.68f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Dòng dịch hữu cơ đi qua Bản rây ở vỏ ngoài thân",
                        NhanLuong = "Mạch rây thân ↓",
                        DieuGiXayRa = "Dịch mạch rây chảy qua các lỗ nhỏ trên bản rây của ống rây sống nằm ở PHÍA NGOÀI vòng tầng sinh mạch (ngay dưới mũ sợi mô cứng của mỗi bó mạch thân).",
                        ViSao = "Vì mạch rây nằm ở phần vỏ ngoài nên khi khoanh một vòng vỏ thân cây hai lá mầm sẽ chặn đứt dòng đường đi xuống, làm phình mép vỏ trên vết cắt.",
                        GhiNho = "Ống rây là tế bào sống đã mất nhân nhưng còn tế bào chất và có tế bào kèm giàu ti thể hỗ trợ.",
                        KhungNhin = "lat_cat_than", BoPhanNoiBat = "than_mach_ray",
                        NeoTu = new[] { "than_ray_trai_tren", "than_ray_phai_tren" },
                        NeoDen = new[] { "than_ray_trai_duoi", "than_ray_phai_duoi" },
                        TaiCho = false, MauHex = "#F59E0B", ThoiGianVe = 1.9f,
                        CamYaw = 24f, CamPitch = 34f, CamDist = 0.74f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Phân phối lên Quả đậu non & Chùm hoa đỉnh",
                        NhanLuong = "Vận chuyển 2 chiều",
                        DieuGiXayRa = "Từ lá trưởng thành ở các đốt trên, dòng mạch rây không chỉ đi xuống mà còn rẽ nhánh đi lên nuôi chùm hoa mẫu 5 và tích lũy tinh bột, prôtêin vào các hạt đậu non trong quả.",
                        ViSao = "Mạch rây có thể vận chuyển 2 chiều tùy vị trí tương đối giữa cơ quan nguồn (lá quang hợp) và cơ quan chứa đang tiêu thụ đường mạnh.",
                        GhiNho = "Khác với mạch gỗ 1 chiều, dòng mạch rây đi từ NGUỒN tới CHỨA (cả đi xuống rễ lẫn đi lên hoa/quả).",
                        KhungNhin = "tong_the", BoPhanNoiBat = "hoa_dau",
                        NeoTu = new[] { "cay_la4_phien", "cay_than_dot4" },
                        NeoDen = new[] { "cay_qua_dau", "cay_hoa_dinh" },
                        TaiCho = false, MauHex = "#FBBF24", ThoiGianVe = 1.8f,
                        CamYaw = 18f, CamPitch = 14f, CamDist = 1.18f, PivotOffY = 0.24f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Chuyển xuống nuôi Hệ rễ cọc & Nốt sần Rhizobium",
                        NhanLuong = "Nuôi rễ & cộng sinh",
                        DieuGiXayRa = "Dòng mạch rây chính chảy dọc xuống cổ rễ vào 4 bó mạch rây trong trung trụ rễ, cung cấp đường cho chóp rễ sinh trưởng và cho vi khuẩn nốt sần cố định đạm N₂.",
                        ViSao = "Khi đường được rút ra tại rễ, áp suất thẩm thấu ống rây giảm xuống, nước trả lại về mạch gỗ hoàn thành vòng tuần hoàn nước–chất hữu cơ khép kín.",
                        GhiNho = "Cây cấp đường cho vi khuẩn Rhizobium ở nốt sần, đổi lại vi khuẩn cố định N₂ thành NH₄⁺ nuôi cây.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "re_chop_va_long_hut",
                        NeoTu = new[] { "cay_than_dot3", "cay_than_cat_duoi" },
                        NeoDen = new[] { "cay_than_cat_duoi", "cay_re_giua" },
                        TaiCho = false, MauHex = "#D97706", ThoiGianVe = 1.9f,
                        CamYaw = 18f, CamPitch = 12f, CamDist = 1.50f, PivotOffY = -0.04f
                    }
                }
            },

            // =================================================================
            // 6. TÍNH HƯỚNG SÁNG DƯƠNG & PHÂN BỐ AUXIN (5 BƯỚC)
            // =================================================================
            new QuaTrinhSinhLyThucVat
            {
                Ma = "huong_sang_auxin",
                Ten = "Tính hướng sáng dương & Hoocmôn Auxin",
                TenNgan = "Hướng sáng Auxin",
                BieuTuong = "6.",
                MauHex = "#EAB308",
                TomTat = "Ánh sáng đơn hướng chiếu đỉnh chồi → Phototropin kích hoạt dồn Auxin (IAA) sang phía tối → Auxin đi xuống phía tối thân → Tế bào phía tối dãn dài nhanh hơn → Ngọn uốn về phía sáng.",
                CacBuoc = new[]
                {
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Tiếp nhận ánh sáng đơn hướng tại Đỉnh chồi",
                        NhanLuong = "Kích thích ánh sáng",
                        DieuGiXayRa = "Chùm ánh sáng từ đèn nghiêng +58° chiếu lệch một bên vào đỉnh sinh trưởng ngọn cây — nơi tập trung các sắc tố cảm quang Phototropin.",
                        ViSao = "Thí nghiệm Darwin và Boysen-Jensen chứng minh chính đỉnh chồi (chứ không phải vùng uốn bên dưới) là cơ quan cảm nhận hướng ánh sáng.",
                        GhiNho = "Ánh sáng xanh lam (400–500 nm) kích hoạt thụ thể Phototropin ở đỉnh ngọn cây.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "hoa_dau",
                        NeoTu = new[] { "cay_den_bong" },
                        NeoDen = new[] { "cay_ngon_sang" },
                        TaiCho = false, MauHex = "#FDE047", ThoiGianVe = 1.8f,
                        CamYaw = 14f, CamPitch = 12f, CamDist = 1.62f, PivotOffY = 0.16f,
                        CoDatMoiTruong = true, MoiTruongGoiY = EnvHuongSang
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Chuyển vị ngang Auxin (IAA) sang phía khuất sáng",
                        NhanLuong = "Dồn Auxin sang tối",
                        DieuGiXayRa = "Dưới tín hiệu Phototropin, các bơm protein PIN3 phân bố lại khiến hoocmôn Auxin (IAA) tổng hợp ở đỉnh di chuyển ngang từ phía được chiếu sáng sang tập trung dày đặc ở phía khuất sáng.",
                        ViSao = "Sự phân bố bất đối xứng này tạo tỉ lệ nồng độ Auxin ~70% ở phía tối so với ~30% ở phía sáng.",
                        GhiNho = "Ánh sáng không phá huỷ Auxin mà làm Auxin dịch chuyển ngang sang phía đối diện (phía tối).",
                        KhungNhin = "tong_the", BoPhanNoiBat = "than_cay",
                        NeoTu = new[] { "cay_ngon_sang" },
                        NeoDen = new[] { "cay_ngon_toi" },
                        TaiCho = false, MauHex = "#FACC15", ThoiGianVe = 1.7f,
                        CamYaw = 12f, CamPitch = 14f, CamDist = 1.15f, PivotOffY = 0.28f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Vận chuyển phân cực Auxin dọc xuống phía tối thân",
                        NhanLuong = "Dòng Auxin hướng gốc",
                        DieuGiXayRa = "Lượng Auxin cao ở phía khuất sáng tiếp tục được vận chuyển phân cực từ ngọn xuống vùng sinh trưởng dãn dài của các đốt thân phía trên.",
                        ViSao = "Các kênh xuất PIN1 nằm cố định ở đáy mỗi tế bào thân tạo dòng vận chuyển Auxin một chiều từ đỉnh xuống gốc.",
                        GhiNho = "Auxin di chuyển phân cực từ đỉnh chồi xuống vùng dãn dài dọc theo mặt khuất sáng của thân.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "than_cay",
                        NeoTu = new[] { "cay_ngon_toi", "cay_than5_toi" },
                        NeoDen = new[] { "cay_than5_toi", "cay_than3_toi" },
                        TaiCho = false, MauHex = "#EAB308", ThoiGianVe = 1.9f,
                        CamYaw = 14f, CamPitch = 14f, CamDist = 1.24f, PivotOffY = 0.20f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Tế bào phía tối dãn dài nhanh hơn phía sáng",
                        NhanLuong = "Tăng trưởng không đều",
                        DieuGiXayRa = "Tại vùng dãn dài phía tối, nồng độ Auxin cao kích thích bơm proton H⁺ làm axit hoá vách tế bào, hoạt hoá enzym expansin làm giãn lỏng sợi xenlulôzơ để tế bào dãn dài vượt trội.",
                        ViSao = "Phía khuất sáng dài ra nhanh hơn hẳn phía được chiếu sáng tạo ra mô-men uốn đẩy trục thân nghiêng dần về phía đèn.",
                        GhiNho = "Ở thân, nồng độ Auxin cao (phía tối) kích thích tế bào dãn dài mạnh → uốn hướng sáng DƯƠNG.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "than_cay",
                        NeoTu = new[] { "cay_than4_toi", "cay_than3_toi" },
                        NeoDen = new[] { "cay_than4_toi", "cay_than3_toi" },
                        TaiCho = true, MauHex = "#F59E0B", ThoiGianVe = 1.6f,
                        CamYaw = 14f, CamPitch = 14f, CamDist = 1.20f, PivotOffY = 0.18f
                    },
                    new BuocQuaTrinhThucVat
                    {
                        TieuDe = "Ngọn cây uốn cong hướng về nguồn sáng",
                        NhanLuong = "Thích nghi quang hợp",
                        DieuGiXayRa = "Toàn bộ thân trên uốn cong một góc gần 48° hướng đỉnh ngọn và các phiến lá kép trực diện về chùm sáng của đèn, giúp cây nhận tối đa bức xạ PAR.",
                        ViSao = "Đây là hình thức cảm ứng chậm giúp thực vật sống cố định vẫn chủ động tìm kiếm nguồn ánh sáng để quang hợp và sinh tồn.",
                        GhiNho = "Thân có tính hướng sáng DƯƠNG (uốn về phía sáng); rễ có tính hướng sáng ÂM và hướng trọng lực DƯƠNG.",
                        KhungNhin = "tong_the", BoPhanNoiBat = "la_cay",
                        NeoTu = new[] { "cay_than5_toi", "cay_ngon_toi" },
                        NeoDen = new[] { "cay_ngon_sang", "cay_den_bong" },
                        TaiCho = false, MauHex = "#10B981", ThoiGianVe = 1.9f,
                        CamYaw = 16f, CamPitch = 12f, CamDist = 1.68f, PivotOffY = 0.14f
                    }
                }
            }
        };

        public static QuaTrinhSinhLyThucVat TimTheoMa(string ma)
        {
            if (string.IsNullOrEmpty(ma)) return null;
            for (int i = 0; i < DanhSach.Length; i++)
                if (string.Equals(DanhSach[i].Ma, ma, StringComparison.OrdinalIgnoreCase))
                    return DanhSach[i];
            return null;
        }
    }
}
