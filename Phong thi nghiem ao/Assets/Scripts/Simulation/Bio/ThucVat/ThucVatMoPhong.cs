// Mô phỏng sinh lý thực vật cây hai lá mầm theo thời gian thực (C# thuần, không phụ thuộc UnityEngine).
// Dùng chung các hàm chuẩn từ Photosynthesis, Transpiration, Phototropism để bảo đảm nhất quán số liệu toàn dự án.
using System;
using Khkt.Simulation.Bio;

namespace Khkt.Simulation.Bio.ThucVat
{
    public struct DieuKienMoiTruong
    {
        public double AnhSangLux;         // 0 .. 60000 lux
        public double Co2Ppm;             // 0 .. 1200 ppm (khí quyển ~400 ppm)
        public double NhietDoC;           // 5 .. 45 °C
        public double DoAmKhongKhiPct;    // 10 .. 98 %
        public double DoAmDatPct;         // 5 .. 100 %
        public double Gio01;              // 0 .. 1
        public double GocDenHuongSangDo;  // -75 .. +75 ° (0 = chiếu thẳng từ đỉnh)
        public double SoGioHuongSang;     // 0 .. 72 giờ

        public static DieuKienMoiTruong MacDinh => new DieuKienMoiTruong
        {
            AnhSangLux = 18500.0,
            Co2Ppm = 410.0,
            NhietDoC = 26.5,
            DoAmKhongKhiPct = 65.0,
            DoAmDatPct = 72.0,
            Gio01 = 0.25,
            GocDenHuongSangDo = 35.0,
            SoGioHuongSang = 12.0
        };
    }

    public struct TrangThaiThucVat
    {
        public bool BanDem;
        public double CuongDoQuangHopTho;       // bọt O2 tương đương / phút (dựa trên Photosynthesis.BubbleRatePerMinute)
        public double CuongDoHoHap;              // tốc độ tiêu thụ tương đương theo Q10
        public double CuongDoQuangHopRong;       // >0 ban ngày vượt điểm bù; <0 ban đêm hoặc dưới điểm bù (chỉ hô hấp)
        public double O2ThaiRaMoiGiay;           // hệ số biểu diễn hạt O2 ra (0..1+)
        public double Co2HutVaoMoiGiay;          // hệ số biểu diễn hạt CO2 vào
        public double TocDoThoatHoiNuocMmPhut;   // mm/phút Potometer (dựa trên Transpiration.BubbleShiftMmPerMin)
        public double TocDoMachGo;               // tốc độ dòng nước đi lên (tỉ lệ thuận với thoát hơi nước)
        public double TocDoMachRay;              // tốc độ dòng đường đi xuống (tỉ lệ với quang hợp thuần)
        public double DoMoKhiKhong01;            // 0 = đóng kín, 1 = mở tối đa
        public double DoTruongTeBaoHatDau01;     // độ trương tế bào bảo vệ (0..1)
        public double NongDoAba01;               // tín hiệu stress khô hạn từ rễ (0..1)
        public double GocUonThanDo;              // độ (+ nghiêng phải, - nghiêng trái)
        public double HeSoVuonDaiTrongToi;       // 1.0..2.25 khi tối kéo dài
        public double TiLeAuxinPhiaToi;          // 0.50..0.72
        public double TiLeAuxinPhiaSang;         // 0.28..0.50
    }

    public sealed class ThongTinBoPhan
    {
        public string Ma;
        public string Ten;
        public string Nhom;
        public string CauTao;
        public string ChucNang;
        public string CauHoiNhanh;
        public string DapAn;
        public string Nguon;
    }

    public sealed class BuocBaiGiang
    {
        public int Buoc;
        public string TieuDe;
        public string KhungNhin;
        public string BoPhanNoiBat;
        public string NoiDung;
    }

    public sealed class CauHoiTracNghiem
    {
        public string Cau;
        public string[] LuaChon;
        public int DapAnDung;
        public string GiaiThich;
    }

    /// <summary>
    /// Bộ máy tính toán sinh lý cây hai lá mầm C3 theo thời gian thực.
    /// Quy đổi chuẩn 500 lux ≈ 1 W/m² PAR để 1000 lux khớp đúng điểm bù ánh sáng Photosynthesis.DefaultCompensationPointWPerM2 (2 W/m²).
    /// </summary>
    public static class ThucVatMoPhong
    {
        public const double LuxTrenWPerM2Par = 500.0;
        public const double DiemBuAnhSangLux = Photosynthesis.DefaultCompensationPointWPerM2 * LuxTrenWPerM2Par; // 1000 lux
        public const double DiemBuCo2Ppm = 50.0;
        public const double KmCo2Ppm = 280.0;
        public const double Co2ThamChieuPpm = 400.0;
        public const double NhietDoToiUuC = 28.0;

        public static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

        public static double HeSoCo2(double co2Ppm)
        {
            if (co2Ppm <= DiemBuCo2Ppm) return 0.0;
            double num = co2Ppm - DiemBuCo2Ppm;
            double raw = num / (KmCo2Ppm + num);
            double refVal = (Co2ThamChieuPpm - DiemBuCo2Ppm) / (KmCo2Ppm + (Co2ThamChieuPpm - DiemBuCo2Ppm));
            return raw / refVal; // = 1.0 tại 400 ppm, tiệm cận ~1.43 ở 1200 ppm
        }

        public static double HeSoNhietDoQuangHop(double tempC)
        {
            if (tempC < 2.0 || tempC > 48.0) return 0.0;
            double diff = (tempC - NhietDoToiUuC) / 11.5;
            return Math.Exp(-diff * diff);
        }

        public static double HoHapToi(double tempC)
        {
            if (tempC < 0.0) return 0.25;
            double t = Math.Min(tempC, 45.0);
            // Q10 = 2.0, neo ở 25 °C ~ 2.4 đơn vị bọt tương đương
            return 2.4 * Math.Pow(2.0, (t - 25.0) / 10.0);
        }

        public static TrangThaiThucVat TinhToan(in DieuKienMoiTruong dk)
        {
            double lightLux = Math.Max(0.0, Math.Min(60000.0, dk.AnhSangLux));
            double co2Ppm = Math.Max(0.0, Math.Min(1200.0, dk.Co2Ppm));
            double tempC = Math.Max(5.0, Math.Min(45.0, dk.NhietDoC));
            double rhPct = Math.Max(5.0, Math.Min(99.0, dk.DoAmKhongKhiPct));
            double soilPct = Math.Max(5.0, Math.Min(100.0, dk.DoAmDatPct));
            double wind01 = Clamp01(dk.Gio01);

            bool banDem = lightLux < 200.0;

            // 1. Stress hạn & điều tiết khí khổng bởi ABA (khi độ ẩm đất < 35%)
            double aba01 = Clamp01((35.0 - soilPct) / 25.0);
            double lightStomataDrive = Clamp01(Math.Pow(lightLux / 16000.0, 0.72));
            double heatStress = tempC > 38.0 ? Clamp01((tempC - 38.0) / 12.0) * 0.55 : 0.0;
            double doMoKhiKhong = Clamp01((0.12 + 0.88 * lightStomataDrive) * (1.0 - 0.90 * aba01) * (1.0 - heatStress));
            if (banDem) doMoKhiKhong *= 0.14; // ban đêm cây C3 khép phần lớn khí khổng

            double doTruongHatDau = Clamp01(0.10 + 0.90 * doMoKhiKhong * (0.45 + 0.55 * Clamp01((soilPct - 8.0) / 50.0)));

            // 2. Thoát hơi nước (gọi trực tiếp hàm chuẩn Transpiration.BubbleShiftMmPerMin)
            // Cho phép thoát hơi nước biến thiên mượt theo trạng thái mở khí khổng + cắt bởi ABA khi khô hạn
            double basePotometer = Transpiration.BubbleShiftMmPerMin(wind01, Clamp01(lightLux / 32000.0), rhPct / 100.0, stomataBlocked: false);
            double cutinFloor = Transpiration.BubbleShiftMmPerMin(wind01, 0.0, rhPct / 100.0, stomataBlocked: true);
            double openBlend = 0.08 + 0.92 * (1.0 - 0.88 * aba01) * (1.0 - heatStress * 0.5);
            double thoatHoiNuoc = Math.Max(cutinFloor, basePotometer * openBlend * (0.70 + 0.012 * tempC));

            // Dòng mạch gỗ tỉ lệ thuận với tốc độ thoát hơi nước (và bị giới hạn nhẹ khi đất quá khô cạn)
            double soilSupply = 0.25 + 0.75 * Clamp01(soilPct / 30.0);
            double tocDoMachGo = thoatHoiNuoc * soilSupply;

            // 3. Quang hợp (gọi trực tiếp hàm chuẩn Photosynthesis.BubbleRatePerMinute)
            double parWPerM2 = lightLux / LuxTrenWPerM2Par;
            double rawPhotoBpm = banDem ? 0.0 : Photosynthesis.BubbleRatePerMinute(parWPerM2);
            double stomCondFactor = 0.18 + 0.82 * doMoKhiKhong;
            double quangHopTho = rawPhotoBpm * HeSoCo2(co2Ppm) * HeSoNhietDoQuangHop(tempC) * stomCondFactor;
            double hoHap = HoHapToi(tempC);
            double quangHopRong = banDem ? -hoHap : (quangHopTho - (lightLux <= DiemBuAnhSangLux ? hoHap * (1.0 - lightLux / DiemBuAnhSangLux) : 0.0));

            double normPhoto = Clamp01(quangHopTho / Photosynthesis.DefaultVmaxBpm);
            double o2Ra = quangHopRong > 0.0 ? Clamp01(quangHopRong / Photosynthesis.DefaultVmaxBpm) : 0.0;
            double co2Vao = o2Ra;
            double tocDoMachRay = o2Ra * (0.4 + 0.6 * soilSupply);

            // 4. Hướng sáng dương (gọi trực tiếp hàm chuẩn Phototropism.BendAngleDeg & EtiolationFactor)
            bool darkPhototropism = lightLux < 150.0;
            double gocUon = Phototropism.BendAngleDeg(dk.SoGioHuongSang, dk.GocDenHuongSangDo, darkPhototropism);
            double etiolation = Phototropism.EtiolationFactor(dk.SoGioHuongSang, darkPhototropism);

            double asymmetricFrac = Math.Min(1.0, Math.Abs(gocUon) / Phototropism.MaxBendAngleDeg);
            double auxinPhiaToi = 0.50 + 0.22 * asymmetricFrac;
            double auxinPhiaSang = 1.0 - auxinPhiaToi;

            return new TrangThaiThucVat
            {
                BanDem = banDem,
                CuongDoQuangHopTho = quangHopTho,
                CuongDoHoHap = hoHap,
                CuongDoQuangHopRong = quangHopRong,
                O2ThaiRaMoiGiay = o2Ra,
                Co2HutVaoMoiGiay = co2Vao,
                TocDoThoatHoiNuocMmPhut = thoatHoiNuoc,
                TocDoMachGo = tocDoMachGo,
                TocDoMachRay = tocDoMachRay,
                DoMoKhiKhong01 = doMoKhiKhong,
                DoTruongTeBaoHatDau01 = doTruongHatDau,
                NongDoAba01 = aba01,
                GocUonThanDo = gocUon,
                HeSoVuonDaiTrongToi = etiolation,
                TiLeAuxinPhiaToi = auxinPhiaToi,
                TiLeAuxinPhiaSang = auxinPhiaSang
            };
        }

        // Danh mục kiến thức mặc định khớp với data/sinh/thuc_vat.json để dùng an toàn trên mọi nền tảng (Mac, Windows, iPad).
        public static readonly ThongTinBoPhan[] DanhMucBoPhan =
        {
            new ThongTinBoPhan
            {
                Ma = "re_chop_va_long_hut", Ten = "Hệ rễ cọc (chóp rễ & miền lông hút)", Nhom = "tong_the",
                CauTao = "Rễ chính đâm sâu, phân nhiều rễ bên; đỉnh có chóp rễ bảo vệ, phía trên là miền lông hút với nhiều tế bào biểu bì kéo dài.",
                ChucNang = "Hấp thụ nước theo thẩm thấu và ion khoáng (thụ động/chủ động), bám giữ cây.",
                CauHoiNhanh = "Vì sao cây héo khi đất ngập úng lâu?", DapAn = "Rễ thiếu O₂ hô hấp hiếu khí tạo ATP, ngừng hút khoáng và tổn thương lông hút.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_cay", Ten = "Thân cây đậu (cơ quan trục & vận chuyển)", Nhom = "tong_the",
                CauTao = "Hình trụ chia đốt mang lá và hoa; bó mạch hở xếp thành vòng có tầng sinh mạch.",
                ChucNang = "Nâng đỡ tán lá hướng sáng (auxin); dẫn nước–khoáng lên (mạch gỗ) và chất hữu cơ xuống/tới cơ quan nhận (mạch rây).",
                CauHoiNhanh = "Chiều dẫn truyền trong mạch gỗ và mạch rây khác nhau thế nào?", DapAn = "Mạch gỗ đi từ rễ lên lá; mạch rây đi từ lá xuống thân, rễ và các cơ quan chứa.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_cay", Ten = "Lá cây hai lá mầm (gân hình mạng)", Nhom = "tong_the",
                CauTao = "Phiến mỏng rộng diện tích với hệ gân hình mạng phân nhánh và hàng vạn khí khổng chủ yếu ở mặt dưới.",
                ChucNang = "Quang hợp tổng hợp chất hữu cơ và thoát hơi nước tạo lực kéo mạch gỗ.",
                CauHoiNhanh = "Vì sao gân hình mạng dẫn truyền hiệu quả?", DapAn = "Mạng bó mạch tiếp cận gần từng tế bào thịt lá để cấp nước và thu đường nhanh.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "hoa_dau", Ten = "Hoa đậu lưỡng tính (mẫu 5)", Nhom = "tong_the",
                CauTao = "5 lá đài xanh, 5 cánh tràng đặc trưng, bộ nhị và nhụy chứa noãn.",
                ChucNang = "Sinh sản hữu tính, thụ phấn – thụ tinh kép tạo hạt và quả.",
                CauHoiNhanh = "Hoa cây hai lá mầm thường theo mẫu nào?", DapAn = "Mẫu 4 hoặc mẫu 5 (hoa đậu mẫu 5).",
                Nguon = "SGK Sinh học 10–11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_long_hut", Ten = "Tế bào lông hút (lát cắt rễ)", Nhom = "lat_cat_re",
                CauTao = "Tế bào biểu bì kéo dài, thành xenlulôzơ mỏng không có lớp cutin, không bào lớn chứa dịch ưu trương.",
                ChucNang = "Tăng diện tích tiếp xúc đất hàng trăm lần để hút nước bằng thẩm thấu và hấp thụ ion khoáng.",
                CauHoiNhanh = "Thế nước trong không bào lông hút so với dung dịch đất thế nào?", DapAn = "Thấp hơn dung dịch đất để nước tự động thẩm thấu vào trong.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_vo_nhu_mo", Ten = "Mô mềm vỏ rễ", Nhom = "lat_cat_re",
                CauTao = "Các lớp tế bào thành mỏng xếp thưa có khe gian bào thông suốt từ biểu bì đến nội bì.",
                ChucNang = "Dẫn nước và khoáng theo hai con đường: gian bào (nhanh) và tế bào chất (qua cầu sinh chất).",
                CauHoiNhanh = "Nước đi qua vỏ rễ theo mấy con đường chính?", DapAn = "Hai con đường: gian bào và tế bào chất.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_dai_caspari", Ten = "Nội bì có đai Caspari", Nhom = "lat_cat_re",
                CauTao = "Vòng tế bào nội bì có vách thấm suberin (đai Caspari) bít kín mọi khe gian bào.",
                ChucNang = "Chặn dòng gian bào, bắt nước và ion khoáng phải đi qua màng tế bào nội bì để kiểm soát chọn lọc trước khi vào mạch gỗ.",
                CauHoiNhanh = "Vai trò quan trọng nhất của đai Caspari là gì?", DapAn = "Điều chỉnh lượng nước và kiểm tra chọn lọc các ion khoáng vào trung trụ.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_mach_go", Ten = "Mạch gỗ rễ (Xylem — trung trụ)", Nhom = "lat_cat_re",
                CauTao = "Xếp hình chữ thập/hình sao ở giữa trung trụ; gồm tế bào chết hóa gỗ rỗng lòng và mất màng ngăn ngang.",
                ChucNang = "Dẫn nước và ion khoáng liên tục đi lên thân lá với sức cản nhỏ nhất.",
                CauHoiNhanh = "Đặc điểm nào giúp mạch gỗ chịu được sức kéo âm từ lá?", DapAn = "Thành tế bào hóa gỗ (lignin) rất dày và bền chắc.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_mach_ray", Ten = "Mạch rây rễ (Phloem)", Nhom = "lat_cat_re",
                CauTao = "Nằm xen giữa các cánh mạch gỗ; gồm ống rây tế bào sống và tế bào kèm giàu ti thể.",
                ChucNang = "Nhận dòng đường từ lá chuyển xuống nuôi mô phân sinh đỉnh rễ và tế bào lông hút.",
                CauHoiNhanh = "Chất hữu cơ vận chuyển chủ yếu trong mạch rây là gì?", DapAn = "Đường saccarôzơ hòa tan.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_bieu_bi", Ten = "Biểu bì thân & mô mềm vỏ", Nhom = "lat_cat_than",
                CauTao = "Lớp biểu bì ngoài phủ cutin; bên dưới là vòng mô dày nâng đỡ và mô mềm vỏ.",
                ChucNang = "Bảo vệ thân, giảm mất nước qua bề mặt và dự trữ tạm thời.",
                CauHoiNhanh = "Mô nào nằm sát biểu bì giúp thân non chịu gió không gãy?", DapAn = "Mô dày góc ở vùng vỏ ngoài.",
                Nguon = "SGK Sinh học 10–11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_mach_ray", Ten = "Mạch rây ở thân (nằm ngoài bó mạch)", Nhom = "lat_cat_than",
                CauTao = "Các ống rây và tế bào kèm nằm ở nửa ngoài của vòng bó mạch.",
                ChucNang = "Dẫn đường saccarôzơ, axit amin từ lá xuống rễ và tới chồi/hoa quả.",
                CauHoiNhanh = "Vì sao khoanh vỏ cắt mạch rây làm phình phần thân phía trên?", DapAn = "Dòng chất hữu cơ từ lá đi xuống bị chặn lại và tích tụ ngay trên vết cắt.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_tang_sinh_mach", Ten = "Tầng sinh mạch (tượng tầng)", Nhom = "lat_cat_than",
                CauTao = "Lớp tế bào phân sinh thứ cấp hẹp kẹp giữa mạch rây (ngoài) và mạch gỗ (trong).",
                ChucNang = "Phân chia sinh thêm gỗ thứ cấp vào trong và rây thứ cấp ra ngoài giúp thân to ra.",
                CauHoiNhanh = "Vì sao cây một lá mầm (lúa, ngô) thân không to lên mãi như cây hai lá mầm?", DapAn = "Bó mạch cây một lá mầm thường đóng kín, không có tầng sinh mạch.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_mach_go", Ten = "Mạch gỗ ở thân (nằm trong bó mạch)", Nhom = "lat_cat_than",
                CauTao = "Các mạch ống rộng thành dày hóa lignin nằm ở phía trong mỗi bó mạch.",
                ChucNang = "Nối tiếp mạch gỗ rễ chuyển nước và muối khoáng lên cành lá.",
                CauHoiNhanh = "Trong bó mạch thân hai lá mầm, mạch gỗ nằm về phía nào?", DapAn = "Phía trong (hướng về tâm ruột).",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_bieu_bi_cutin", Ten = "Biểu bì lá & lớp cutin", Nhom = "lat_cat_la",
                CauTao = "Tế bào biểu bì trong suốt xếp khít, mặt ngoài phủ màng sáp cutin.",
                ChucNang = "Cho ánh sáng xuyên qua tới lục lạp và ngăn bốc hơi nước tự do.",
                CauHoiNhanh = "Tác dụng của lớp sáp cutin trên biểu bì lá là gì?", DapAn = "Hạn chế mất nước qua bề mặt biểu bì khi nắng gió.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_mo_giau_va_mo_xop", Ten = "Thịt lá: mô giậu & mô xốp", Nhom = "lat_cat_la",
                CauTao = "Mô giậu nằm trên gồm tế bào trụ giàu lục lạp xếp khít; mô xốp bên dưới để hở nhiều khoang khí.",
                ChucNang = "Mô giậu quang hợp chính; khoang khí mô xốp khuếch tán CO₂, O₂ và hơi nước đến khí khổng.",
                CauHoiNhanh = "Vì sao mặt trên lá thường xanh đậm hơn mặt dưới?", DapAn = "Mô giậu sát mặt trên chứa mật độ lục lạp rất lớn.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_gan_bo_mach", Ten = "Gân lá (bó mạch gỗ trên – rây dưới)", Nhom = "lat_cat_la",
                CauTao = "Mạch gỗ hướng lên trên gần mô giậu, mạch rây hướng xuống dưới.",
                ChucNang = "Cung cấp nước cho quang hợp và thu gom đường về thân.",
                CauHoiNhanh = "Trong gân lá, loại mạch nào nằm ở nửa trên?", DapAn = "Mạch gỗ (xylem).",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_khi_khong", Ten = "Khí khổng & 2 tế bào hạt đậu", Nhom = "lat_cat_la",
                CauTao = "Hai tế bào hình hạt đậu có thành trong dày hơn thành ngoài, úp vào nhau tạo khe mở.",
                ChucNang = "Đóng mở khe khí khổng điều tiết trao đổi CO₂/O₂ và thoát hơi nước (trương nước thì cong mở; thiếu nước ABA cao thì xẹp đóng).",
                CauHoiNhanh = "Vì sao thành trong tế bào hạt đậu dày lại giúp mở khí khổng khi no nước?", DapAn = "Khi hút nước trương lên, thành ngoài mỏng dãn nhiều hơn kéo vách thành trong cong lõm ra hai bên làm mở khe.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            }
        };

        public static readonly BuocBaiGiang[] DanhSachBaiGiang =
        {
            new BuocBaiGiang
            {
                Buoc = 1, TieuDe = "Bước 1/5 — Cấu trúc tổng thể cây hai lá mầm", KhungNhin = "tong_the", BoPhanNoiBat = "than_cay",
                NoiDung = "Quan sát cây đậu hai lá mầm gồm rễ cọc (chóp rễ, miền lông hút), thân chia đốt, lá gân hình mạng và hoa mẫu 5. Bấm trực tiếp vào bộ phận để mở thẻ kiến thức."
            },
            new BuocBaiGiang
            {
                Buoc = 2, TieuDe = "Bước 2/5 — Hút nước ở rễ & chốt kiểm soát đai Caspari", KhungNhin = "lat_cat_re", BoPhanNoiBat = "re_dai_caspari",
                NoiDung = "Nước đi vào lông hút theo thẩm thấu qua mô mềm vỏ. Đến nội bì, đai Caspari bằng suberin chặn khe gian bào, ép toàn bộ dòng nước – ion qua màng tế bào chất trước khi vào mạch gỗ trung trụ."
            },
            new BuocBaiGiang
            {
                Buoc = 3, TieuDe = "Bước 3/5 — Đóng mở khí khổng & dòng mạch gỗ – rây", KhungNhin = "lat_cat_la", BoPhanNoiBat = "la_khi_khong",
                NoiDung = "Thoát hơi nước qua khe khí khổng tạo lực kéo nước đi lên trong mạch gỗ. Kéo giảm Độ ẩm đất xuống dưới 25% để thấy ABA làm hai tế bào hạt đậu xẹp xuống khép khe khí khổng."
            },
            new BuocBaiGiang
            {
                Buoc = 4, TieuDe = "Bước 4/5 — Quang hợp đồng hóa CO₂ & hô hấp ban đêm", KhungNhin = "lat_cat_than", BoPhanNoiBat = "than_mach_ray",
                NoiDung = "Tăng Ánh sáng và CO₂ để xem cường độ quang hợp tăng theo đường bão hòa và dòng đường đi xuống trong mạch rây. Kéo Ánh sáng về 0 lux (ban đêm): quang hợp ngừng hẳn, chỉ còn hô hấp."
            },
            new BuocBaiGiang
            {
                Buoc = 5, TieuDe = "Bước 5/5 — Tính hướng sáng & phân bố bất đối xứng Auxin", KhungNhin = "tong_the", BoPhanNoiBat = "than_cay",
                NoiDung = "Chiếu đèn lệch góc sang một bên và tăng tốc thời gian (×60 / ×600): hạt auxin (IAA) dồn về phía khuất sáng làm các tế bào bên tối dãn dài nhanh hơn, uốn ngọn cây về phía nguồn sáng."
            }
        };

        public static readonly CauHoiTracNghiem[] DanhSachCauHoi =
        {
            new CauHoiTracNghiem
            {
                Cau = "Cấu trúc nào ở nội bì rễ ép toàn bộ nước và ion khoáng phải đi qua màng sinh chất trước khi vào mạch gỗ?",
                LuaChon = new[] { "Lớp cutin biểu bì", "Đai Caspari thấm suberin", "Tầng sinh mạch", "Khoang khí mô xốp" },
                DapAnDung = 1,
                GiaiThich = "Đai Caspari thấm suberin không thấm nước bịt kín khe gian bào ở tế bào nội bì."
            },
            new CauHoiTracNghiem
            {
                Cau = "Trong bó mạch thân cây hai lá mầm, thứ tự đúng từ ngoài vào trong là gì?",
                LuaChon = new[] { "Mạch gỗ → tầng sinh mạch → mạch rây", "Mạch rây → tầng sinh mạch → mạch gỗ", "Tầng sinh mạch → mạch gỗ → mạch rây", "Mạch gỗ trộn lẫn biểu bì" },
                DapAnDung = 1,
                GiaiThich = "Mạch rây nằm phía ngoài, tầng sinh mạch ở giữa và mạch gỗ hướng vào tâm ruột."
            },
            new CauHoiTracNghiem
            {
                Cau = "Khi tăng độ ẩm không khí lên cao (các yếu tố khác không đổi), tốc độ thoát hơi nước qua lá sẽ ra sao?",
                LuaChon = new[] { "Tăng nhanh hơn nữa", "Giữ nguyên không đổi", "Giảm xuống do giảm chênh lệch thế nước/ẩm", "Chuyển hết sang mạch rây" },
                DapAnDung = 2,
                GiaiThich = "Không khí ẩm làm giảm chênh lệch hơi nước giữa khoang khí lá và môi trường nên tốc độ thoát hơi nước và lực kéo mạch gỗ giảm."
            },
            new CauHoiTracNghiem
            {
                Cau = "Ban đêm không có ánh sáng, quá trình trao đổi khí ở lá cây C3 diễn ra thế nào?",
                LuaChon = new[] { "Quang hợp tăng nhờ pha tối", "Ngừng toàn bộ trao đổi khí", "Ngừng quang hợp, chỉ còn hô hấp lấy O₂ và thải CO₂", "Tế bào hạt đậu trương tối đa" },
                DapAnDung = 2,
                GiaiThich = "Không có ánh sáng thì pha sáng ngừng cung cấp ATP/NADPH, lá chỉ còn hô hấp hiếu khí."
            },
            new CauHoiTracNghiem
            {
                Cau = "Chiếu đèn lệch về bên phải cây mầm trong nhiều giờ, auxin dồn về đâu khiến thân cong sang phải?",
                LuaChon = new[] { "Dồn về phía bên trái (khuất sáng), làm tế bào bên trái dãn dài nhanh hơn", "Dồn về phía bên phải (được chiếu sáng)", "Phân bố đều 50/50 ở hai phía", "Chuyển hết xuống rễ chính" },
                DapAnDung = 0,
                GiaiThich = "Auxin di chuyển ngang sang phía khuất sáng, kích thích tế bào vách dãn dài nhanh hơn nên đỉnh thân cong về phía nguồn sáng."
            }
        };

        public static ThongTinBoPhan TimBoPhan(string ma)
        {
            if (string.IsNullOrEmpty(ma)) return null;
            for (int i = 0; i < DanhMucBoPhan.Length; i++)
                if (string.Equals(DanhMucBoPhan[i].Ma, ma, StringComparison.OrdinalIgnoreCase))
                    return DanhMucBoPhan[i];
            return null;
        }
    }
}
