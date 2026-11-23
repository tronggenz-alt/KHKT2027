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
        public string SoSanhMotLaMam;
        public string UngDungThucTe;
        public string CauHoiNhanh;
        public string DapAn;
        public string Nguon;
    }

    public sealed class KichBanThucHanh
    {
        public string Ma;
        public string Ten;
        public string MoTaNgan;
        public string KhungNhinGoiY;
        public string BoPhanGoiY;
        public DieuKienMoiTruong DieuKien;
        public string KetLuanKhoaHoc;
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
                Ma = "re_chop_va_long_hut", Ten = "Hệ rễ cọc (chóp rễ, miền lông hút & nốt sần)", Nhom = "tong_the",
                CauTao = "Rễ cái đâm sâu thẳng đứng, phân nhiều cấp rễ bên và mang nốt sần cố định đạm (vi khuẩn Rhizobium); đỉnh có chóp rễ bảo vệ mô phân sinh, phía trên là miền lông hút dày đặc.",
                ChucNang = "Hấp thụ nước theo cơ chế thẩm thấu và ion khoáng (thụ động/chủ động tiêu tốn ATP), cộng sinh cố định N₂ thành NH₄⁺, neo giữ cây vững chắc.",
                SoSanhMotLaMam = "Cây hai lá mầm (đậu) có hệ rễ cọc với rễ cái to rõ; cây một lá mầm (lúa, ngô) có hệ rễ chùm gồm các rễ phụ dài tương đương mọc từ gốc thân.",
                UngDungThucTe = "Làm đất tơi xốp, thoát nước tốt để cung cấp O₂ cho rễ hô hấp hiếu khí; khi bứng cây đi trồng phải giữ bầu đất để không làm đứt miền lông hút.",
                CauHoiNhanh = "Vì sao cây héo khi đất ngập úng lâu hoặc bón phân quá đặc?", DapAn = "Ngập úng làm rễ thiếu O₂ tạo ATP và tích tụ độc chất; bón phân quá đặc làm thế nước đất thấp hơn dịch bào khiến lông hút mất nước (hạn sinh lý).",
                Nguon = "SGK Sinh học 11 GDPT 2018 — Bài trao đổi nước & khoáng"
            },
            new ThongTinBoPhan
            {
                Ma = "than_cay", Ten = "Thân cây đậu (trục đốt, chồi nách & vận chuyển 2 chiều)", Nhom = "tong_the",
                CauTao = "Thân thảo hình trụ chia đốt và gióng, mang lá kép 3 lá chét, lá kèm và chồi nách; bên trong các bó mạch hở xếp thành 1 vòng đối xứng qua tầng sinh mạch.",
                ChucNang = "Nâng đỡ tán lá vươn tới ánh sáng (tính hướng sáng dương do Auxin); dẫn nước–khoáng một chiều đi lên (mạch gỗ) và dịch hữu cơ đi xuống/đến cơ quan chứa (mạch rây).",
                SoSanhMotLaMam = "Thân hai lá mầm có bó mạch xếp 1 vòng và có tầng sinh mạch giúp thân to ra thứ cấp; thân một lá mầm có bó mạch xếp phân tán và không có tầng sinh mạch.",
                UngDungThucTe = "Bấm ngọn cây đậu để loại bỏ ưu thế ngọn (giảm Auxin từ đỉnh), kích thích chồi nách bật mạnh giúp tăng số cành mang hoa và quả.",
                CauHoiNhanh = "Chiều dẫn truyền và động lực trong mạch gỗ và mạch rây khác nhau thế nào?", DapAn = "Mạch gỗ đi từ rễ lên lá nhờ lực kéo thoát hơi nước, lực liên kết và áp suất rễ; mạch rây đi từ cơ quan nguồn (lá) tới cơ quan chứa nhờ chênh lệch áp suất thẩm thấu.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_cay", Ten = "Lá kép hai lá mầm (gân hình mạng & phiến quang hợp)", Nhom = "tong_the",
                CauTao = "Lá kép gồm 3 lá chét hình tim nhọn đầu, cuống lá có gối lá cử động theo ánh sáng; hệ gân hình mạng lông chim phân nhánh đến từng cụm tế bào thịt lá, mặt dưới tập trung hàng vạn khí khổng.",
                ChucNang = "Quang hợp thu năng lượng ánh sáng cố định CO₂ theo chu trình Calvin (C3) tạo đường; thoát hơi nước qua khí khổng tạo lực kéo dòng mạch gỗ và hạ nhiệt độ lá 2–5 °C.",
                SoSanhMotLaMam = "Lá hai lá mầm có gân hình mạng và cuống lá rõ; lá một lá mầm thường có gân song song hoặc hình cung với bẹ lá ôm thân.",
                UngDungThucTe = "Trồng cây đúng mật độ, tỉa lá già khuất sáng dưới tán để giảm tiêu hao hô hấp và tối ưu diện tích lá nhận bức xạ PAR.",
                CauHoiNhanh = "Vì sao gân lá hình mạng giúp lá hai lá mầm quang hợp và thoát hơi nước hiệu quả?", DapAn = "Mạng mạch phân nhánh dày đặc tiếp cận sát từng tế bào mô giậu để cấp nước tức thì và thu hồi đường saccarôzơ nhanh chóng.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "hoa_dau", Ten = "Hoa đậu lưỡng tính mẫu 5 & Quả đậu non", Nhom = "tong_the",
                CauTao = "Hoa hình bướm mẫu 5 đặc trưng họ Đậu (Fabaceae): 5 lá đài hợp, tràng 5 cánh (1 cánh cờ lớn dựng đứng, 2 cánh bên, 2 cánh thìa úp bọc nhị và bầu nhụy), 10 nhị (9 hợp 1 rời); quả đậu phát triển từ bầu nhụy chứa dãy hạt hai lá mầm.",
                ChucNang = "Thực hiện thụ phấn và thụ tinh kép (đặc trưng thực vật hạt kín): 1 tinh tử hợp với trứng tạo hợp tử (2n), 1 tinh tử hợp với nhân cực tạo nội nhũ (3n) nuôi phôi.",
                SoSanhMotLaMam = "Hoa hai lá mầm thường theo mẫu 4 hoặc mẫu 5, hạt có 2 lá mầm dày tích trữ dinh dưỡng; hoa một lá mầm theo mẫu 3, hạt có 1 lá mầm.",
                UngDungThucTe = "Bổ sung Bo, Lân và Kali giai đoạn phân hóa mầm hoa giúp tăng tỉ lệ đậu quả và hạt đậu chắc mẩy.",
                CauHoiNhanh = "Quả và hạt cây đậu được hình thành từ bộ phận nào của hoa sau thụ tinh kép?", DapAn = "Quả hình thành từ bầu nhụy phát triển lên, còn hạt hình thành từ noãn đã thụ tinh.",
                Nguon = "SGK Sinh học 11 GDPT 2018 — Sinh sản ở thực vật"
            },
            new ThongTinBoPhan
            {
                Ma = "re_long_hut", Ten = "Tế bào lông hút & Biểu bì rễ (Lát cắt rễ)", Nhom = "lat_cat_re",
                CauTao = "Tế bào biểu bì rễ biến đổi kéo dài vươn sâu vào khe đất: thành tế bào bằng xenlulôzơ rất mỏng, hoàn toàn không thấm cutin, có không bào trung tâm cực lớn chứa dịch ưu trương và nhiều ti thể.",
                ChucNang = "Tăng diện tích bề mặt tiếp xúc đất lên hàng chục lần; hút nước tự động theo gradien thế nước (thẩm thấu) và bơm chọn lọc các ion khoáng (K⁺, NO₃⁻, NH₄⁺, PO₄³⁻).",
                SoSanhMotLaMam = "Cả hai nhóm đều có miền lông hút cấu tạo từ tế bào biểu bì thành mỏng, dễ gãy rụng khi đất khô hạn hoặc thiếu oxy.",
                UngDungThucTe = "Tưới nước giữ ẩm đất 65–80% và bón phân pha loãng đúng nồng độ để duy trì thế nước đất cao hơn thế nước tế bào lông hút.",
                CauHoiNhanh = "Thế nước trong không bào tế bào lông hút phải như thế nào so với dung dịch đất để cây hút được nước?", DapAn = "Thế nước trong lông hút phải thấp hơn (áp suất thẩm thấu cao hơn) dung dịch đất để nước thẩm thấu từ đất vào rễ.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_vo_nhu_mo", Ten = "Mô mềm vỏ rễ & 2 con đường vận chuyển (Lát cắt rễ)", Nhom = "lat_cat_re",
                CauTao = "Nhiều lớp tế bào nhu mô hình đa giác tròn xếp thưa tạo mạng lưới khe gian bào rộng thông từ biểu bì tới sát vòng nội bì; các tế bào nối với nhau qua cầu sinh chất.",
                ChucNang = "Dẫn truyền nước và ion khoáng hướng tâm qua 2 con đường song song: con đường gian bào (đi theo thành tế bào và khe gian bào — nhanh, chưa chọn lọc) và con đường tế bào chất (đi xuyên qua màng và cầu sinh chất — có chọn lọc).",
                SoSanhMotLaMam = "Mô mềm vỏ rễ chiếm tỉ lệ tiết diện lớn ở cả rễ non hai lá mầm và một lá mầm, đồng thời dự trữ tinh bột cung cấp cho hô hấp rễ.",
                UngDungThucTe = "Giữ nhiệt độ đất ấm áp (20–30 °C) giúp màng sinh chất linh hoạt và tăng tốc độ vận chuyển qua cầu sinh chất.",
                CauHoiNhanh = "Nước và ion khoáng đi từ lông hút vào trung trụ rễ theo hai con đường nào?", DapAn = "Con đường gian bào (qua thành tế bào và khoảng gian bào) và con đường tế bào chất (xuyên qua tế bào chất và cầu sinh chất).",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_dai_caspari", Ten = "Nội bì & Đai Caspari thấm suberin (Lát cắt rễ)", Nhom = "lat_cat_re",
                CauTao = "Một vòng đơn tế bào nội bì xếp khít bao quanh trung trụ; trên vách xuyên tâm và vách ngang có dải đai Caspari dày thấm chất bần suberin và lignin kị nước hoàn toàn.",
                ChucNang = "Đóng vai trò 'trạm kiểm soát sinh học': chặn đứng hoàn toàn dòng nước và ion đi theo con đường gian bào, bắt buộc mọi phân tử nước và ion khoáng phải đi xuyên qua màng sinh chất của tế bào nội bì để kiểm tra chọn lọc trước khi vào mạch gỗ.",
                SoSanhMotLaMam = "Rễ hai lá mầm có đai Caspari dạng dải băng quanh vách xuyên tâm; rễ một lá mầm già thường hóa gỗ dày hình chữ U ở nội bì.",
                UngDungThucTe = "Nhờ đai Caspari và màng nội bì, cây ngăn chặn được nhiều kim loại nặng hoặc độc chất hòa tan trong nước đất xâm nhập lên thân lá.",
                CauHoiNhanh = "Vai trò sinh lý quan trọng nhất của đai Caspari ở nội bì rễ là gì?", DapAn = "Chặn con đường gian bào, buộc nước và ion khoáng phải đi qua màng sinh chất nội bì để kiểm soát chọn lọc lượng nước và chất khoáng vào mạch gỗ.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_mach_go", Ten = "Mạch gỗ rễ hình sao (Xylem trung trụ — Lát cắt rễ)", Nhom = "lat_cat_re",
                CauTao = "Nằm ở chính giữa trụ dẫn (trung trụ), xếp thành hình chữ thập hoặc ngôi sao 4 cánh đặc trưng của rễ hai lá mầm; gồm các quản bào và mạch ống là tế bào chết đã tiêu biến màng ngăn ngang, vách dày hóa gỗ (lignin) bền chắc.",
                ChucNang = "Tiếp nhận nước và ion khoáng đã qua nội bì, tạo thành dòng mạch gỗ liên tục đi lên thân và lá nhờ sự phối hợp của áp suất rễ, lực liên kết giữa các phân tử nước và lực kéo thoát hơi nước.",
                SoSanhMotLaMam = "Rễ hai lá mầm có mạch gỗ xếp hình sao (3–5 cánh) ngay tâm trung trụ; rễ một lá mầm có nhiều bó mạch gỗ (8–20 bó) xếp vòng quanh tâm ruột mềm.",
                UngDungThucTe = "Hiện tượng rỉ nhựa ở gốc cây cắt ngang hoặc ứa giọt ở mép lá vào sáng sớm chứng minh sự tồn tại của áp suất rễ trong mạch gỗ.",
                CauHoiNhanh = "Đặc điểm cấu tạo nào giúp mạch gỗ chịu được áp suất âm (lực kéo) rất lớn mà không bị bẹp ống?", DapAn = "Các tế bào mạch gỗ có thành dày hóa gỗ (thấm lignin) cứng chắc tạo thành ống dẫn bền cơ học.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "re_mach_ray", Ten = "Mạch rây rễ & Vòng trụ bì (Phloem — Lát cắt rễ)", Nhom = "lat_cat_re",
                CauTao = "4 bó mạch rây nằm xen kẽ giữa 4 cánh của ngôi sao mạch gỗ; cấu tạo gồm các ống rây (tế bào sống mất nhân, vách ngăn có bản rây đục lỗ) kết hợp chặt với tế bào kèm giàu ti thể.",
                ChucNang = "Nhận dòng dịch mạch rây (giàu đường saccarôzơ, axit amin, hoocmôn) từ lá chuyển xuống để nuôi đỉnh sinh trưởng rễ, tái tạo lông hút và cung cấp năng lượng ATP cho bơm ion khoáng.",
                SoSanhMotLaMam = "Ở cả hai nhóm, các bó mạch rây trong rễ luôn nằm xen kẽ (luân phiên) với các cánh/bó mạch gỗ trên cùng một vòng bán kính.",
                UngDungThucTe = "Khi lá quang hợp tốt, rễ nhận đủ đường saccarôzơ nên phát triển bộ rễ trắng khỏe và hút khoáng mạnh mẽ hơn.",
                CauHoiNhanh = "Hợp chất hữu cơ chủ yếu nào được vận chuyển trong mạch rây từ lá xuống nuôi rễ?", DapAn = "Đường saccarôzơ (cùng các axit amin, vitamin và hoocmôn thực vật).",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_bieu_bi", Ten = "Biểu bì thân, Mô dày nâng đỡ & Vỏ (Lát cắt thân)", Nhom = "lat_cat_than",
                CauTao = "Ngoài cùng là lớp tế bào biểu bì xếp khít phủ cutin; sát dưới là vòng mô dày góc (thành xenlulôzơ dày lên ở các góc) và nhiều lớp mô mềm vỏ chứa lục lạp non, trong cùng là trụ ruột mô mềm rộng.",
                ChucNang = "Che chở thân non khỏi mất nước và vi sinh vật xâm nhập; mô dày tạo khung đàn hồi giúp thân cây đậu đứng vững và uốn dẻo trước gió mà không gãy.",
                SoSanhMotLaMam = "Thân hai lá mầm phân biệt rõ vùng vỏ ngoài, vòng bó mạch và vùng ruột ở tâm; thân một lá mầm có mô cứng dưới biểu bì và mô mềm đồng nhất khắp tiết diện.",
                UngDungThucTe = "Cây trồng ngoài sáng có gió nhẹ thường phát triển lớp mô dày và thành tế bào cứng cáp hơn cây trồng trong bóng râm.",
                CauHoiNhanh = "Loại mô cơ học nào nằm ngay dưới biểu bì thân non cây hai lá mầm giúp thân dẻo dai chịu gió?", DapAn = "Mô dày (collenchyma) gồm các tế bào sống có thành dày lên bằng xenlulôzơ và pectin.",
                Nguon = "SGK Sinh học 10–11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_mach_ray", Ten = "Mạch rây thân & Mũ sợi trụ bì (Ngoài bó mạch — Lát cắt thân)", Nhom = "lat_cat_than",
                CauTao = "Nằm ở nửa phía ngoài của mỗi bó mạch trong vòng bó mạch thân; phía ngoài cùng được bảo vệ bởi mũ sợi mô cứng (sclerenchyma), bên trong là các ống rây có bản rây và tế bào kèm.",
                ChucNang = "Vận chuyển dòng dịch mạch rây (đường saccarôzơ nồng độ 10–20%, axit amin, K⁺) từ lá đi xuống rễ hoặc đi lên nuôi chồi non, hoa và quả đậu đang lớn.",
                SoSanhMotLaMam = "Ở thân hai lá mầm, các bó mạch rây xếp thành một vòng đồng tâm gần vỏ; vì vậy khi bóc/khoanh vỏ thân cây hai lá mầm sẽ cắt đứt hoàn toàn mạch rây.",
                UngDungThucTe = "Kỹ thuật khoanh vỏ hoặc chiết cành ở cây hai lá mầm giữ chất hữu cơ và auxin tích tụ ở mép trên vết cắt, kích thích ra rễ bất định nhanh.",
                CauHoiNhanh = "Vì sao khi khoanh một vòng vỏ thân cây hai lá mầm, sau một thời gian mép vỏ phía trên vết cắt lại phình to ra?", DapAn = "Do mạch rây nằm ở phần vỏ ngoài bị cắt đứt, dòng chất hữu cơ từ lá vận chuyển xuống bị ứ đọng lại ở mép trên vết cắt.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "than_tang_sinh_mach", Ten = "Tầng sinh mạch / Tượng tầng (Vòng phân sinh thứ cấp — Lát cắt thân)", Nhom = "lat_cat_than",
                CauTao = "Dải tế bào mô phân sinh bên (1–3 lớp tế bào dẹp, thành mỏng, giàu nguyên sinh chất) nằm kẹp chính giữa phần mạch rây ở phía ngoài và phần mạch gỗ ở phía trong của từng bó mạch và nối liền thành vòng.",
                ChucNang = "Thực hiện sinh trưởng thứ cấp: phân chia nguyên phân tạo thêm mạch gỗ thứ cấp vào phía trong và mạch rây thứ cấp ra phía ngoài, làm đường kính thân và rễ cây hai lá mầm tăng lên hàng năm.",
                SoSanhMotLaMam = "Đây là điểm khác biệt giải phẫu cốt lõi: cây hai lá mầm có bó mạch hở (có tầng sinh mạch); cây một lá mầm có bó mạch kín (không có tầng sinh mạch nên thân không to thêm).",
                UngDungThucTe = "Khi ghép mắt hoặc ghép cành cây hai lá mầm, bắt buộc phải áp khít tầng sinh mạch của cành ghép vào tầng sinh mạch của gốc ghép để mô dẫn liền mạch.",
                CauHoiNhanh = "Tại sao khi ghép cành ở cây hai lá mầm, người làm vườn phải đặt trùng khít lớp vỏ trong giữa cành ghép và gốc ghép?", DapAn = "Để hai lớp tầng sinh mạch (tượng tầng) tiếp xúc nhau, phân chia tạo mô sẹo và nối thông mạch gỗ – mạch rây mới.",
                Nguon = "SGK Sinh học 11 GDPT 2018 — Sinh trưởng & phát triển ở thực vật"
            },
            new ThongTinBoPhan
            {
                Ma = "than_mach_go", Ten = "Mạch gỗ thân (Trong bó mạch — Lát cắt thân)", Nhom = "lat_cat_than",
                CauTao = "Nằm ở nửa phía trong của mỗi bó mạch (hướng về tâm ruột thân): gồm các mạch ống lớn (metaxylem) ở ngoài và mạch gỗ trước (protoxylem) nhỏ hơn ở sát mũi trong; lòng ống rỗng thông suốt, vách hóa gỗ dày có vân xoắn hoặc mạch điểm.",
                ChucNang = "Dẫn nước và các ion khoáng hòa tan từ hệ mạch gỗ rễ đi thẳng lên cuống lá và tỏa vào mạng gân lá với lưu lượng thay đổi trực tiếp theo cường độ thoát hơi nước.",
                SoSanhMotLaMam = "Bó mạch thân hai lá mầm xếp thành 1 vòng đều đặn quanh ruột; bó mạch thân một lá mầm xếp lộn xộn khắp tiết diện thân.",
                UngDungThucTe = "Cắm cành hoa trắng vào dung dịch màu thực phẩm rồi cắt ngang thân sẽ quan sát thấy các chấm màu chỉ xuất hiện đúng ở vòng mạch gỗ phía trong bó mạch.",
                CauHoiNhanh = "Trong bó mạch của thân cây hai lá mầm, mạch gỗ và mạch rây được sắp xếp theo trật tự nào từ ngoài vào trong?", DapAn = "Mạch rây ở phía ngoài → Tầng sinh mạch ở giữa → Mạch gỗ ở phía trong (hướng vào tâm ruột).",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_bieu_bi_cutin", Ten = "Tầng sáp Cutin & Biểu bì lá (Lát cắt lá)", Nhom = "lat_cat_la",
                CauTao = "Một lớp tế bào biểu bì sống hình chữ nhật ghép khít nhau như lát gạch, trong suốt (không chứa lục lạp trừ tế bào bảo vệ khí khổng); mặt ngoài được phủ lớp màng sáp cutin không thấm nước.",
                ChucNang = "Bảo vệ các mô mềm bên trong lá, ngăn sự bốc hơi nước tự do không kiểm soát qua bề mặt lá nhưng vẫn hoàn toàn trong suốt để ánh sáng PAR đi xuyên xuống lớp mô giậu.",
                SoSanhMotLaMam = "Lá cây hai lá mầm có tầng cutin dày ở mặt trên và khí khổng tập trung chủ yếu ở biểu bì mặt dưới; lá cây một lá mầm mọc đứng (lúa, ngô) có khí khổng phân bố đều ở cả hai mặt.",
                UngDungThucTe = "Lá non mới mọc có lớp cutin rất mỏng nên thoát hơi nước qua cutin mạnh, dễ bị héo rũ khi gặp nắng gắt buổi trưa.",
                CauHoiNhanh = "Vì sao tế bào biểu bì trên của lá lại trong suốt và không chứa lục lạp?", DapAn = "Để cho các tia sáng mặt trời dễ dàng xuyên qua lớp biểu bì đi tới các tế bào mô giậu bên dưới thực hiện quang hợp.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_mo_giau_va_mo_xop", Ten = "Thịt lá: Mô giậu giàu lục lạp & Mô xốp khoang khí (Lát cắt lá)", Nhom = "lat_cat_la",
                CauTao = "Chia 2 tầng rõ rệt: tầng trên là mô giậu gồm các tế bào hình trụ dài xếp sát nhau vuông góc với biểu bì trên, chứa dày đặc hạt lục lạp; tầng dưới là mô xốp gồm tế bào tròn cạnh xếp thưa tạo hệ thống khoang chứa khí lớn thông thẳng xuống khí khổng.",
                ChucNang = "Mô giậu là trung tâm quang hợp chính thu nhận photon ánh sáng và cố định CO₂; mô xốp vừa quang hợp vừa tạo buồng thông khí giúp CO₂ khuếch tán nhanh từ khí khổng lên mô giậu và hơi nước/O₂ thoát ra ngoài.",
                SoSanhMotLaMam = "Lá hai lá mầm phân hóa thịt lá dị diện (mô giậu ở trên, mô xốp ở dưới); nhiều lá một lá mầm có thịt lá đồng질 (không phân biệt rõ mô giậu và mô xốp).",
                UngDungThucTe = "Mặt trên của lá hai lá mầm luôn có màu xanh sẫm hơn mặt dưới chính vì mật độ lục lạp tập trung cực cao ở lớp tế bào mô giậu.",
                CauHoiNhanh = "Sự sắp xếp khít của mô giậu ở mặt trên và xếp thưa nhiều khoang khí của mô xốp ở mặt dưới có ý nghĩa thích nghi gì?", DapAn = "Mô giậu xếp khít giúp hứng tối đa ánh sáng chiếu từ trên xuống; mô xốp xếp thưa tạo khoang khí lớn giúp CO₂ và O₂ khuếch tán nhanh qua các khí khổng ở mặt dưới.",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_gan_bo_mach", Ten = "Bó mạch Gân lá & Bao bó mạch (Mạch gỗ trên – Mạch rây dưới)", Nhom = "lat_cat_la",
                CauTao = "Nằm giữa thịt lá, được bao quanh bởi vòng tế bào bao bó mạch (parenchyma sheath); nửa phía trên của bó mạch là các ống mạch gỗ (xylem) hướng về mô giậu, nửa phía dưới là các ống mạch rây (phloem) hướng về biểu bì dưới.",
                ChucNang = "Cấp nước và ion khoáng trực tiếp từ mạch gỗ cho các tế bào mô giậu quang hợp, đồng thời nạp đường saccarôzơ vừa tổng hợp vào mạch rây để chuyển về thân.",
                SoSanhMotLaMam = "Ở cây C3 hai lá mầm (đậu), tế bào bao bó mạch ít lục lạp và quang hợp hoàn toàn ở thịt lá; ở cây C4 một lá mầm (ngô, mía), bao bó mạch rất phát triển (cấu trúc Kranz) để cố định CO₂.",
                UngDungThucTe = "Quan sát lát cắt lá giúp học sinh nhớ quy tắc định hướng bó mạch lá: 'Gỗ trên — Rây dưới' (do bó mạch từ thân uốn cong ra cuống lá).",
                CauHoiNhanh = "Trong bó mạch của gân lá cây hai lá mầm, mạch gỗ và mạch rây nằm ở vị trí nào?", DapAn = "Mạch gỗ nằm ở nửa phía trên (gần biểu bì trên và mô giậu), còn mạch rây nằm ở nửa phía dưới (gần biểu bì dưới).",
                Nguon = "SGK Sinh học 11 GDPT 2018"
            },
            new ThongTinBoPhan
            {
                Ma = "la_khi_khong", Ten = "Khí khổng & 2 Tế bào hình hạt đậu (Cơ chế trương nước & ABA)", Nhom = "lat_cat_la",
                CauTao = "Gồm 2 tế bào bảo vệ hình hạt đậu (chứa lục lạp, không bào và nhân) quay phần lõm vào nhau tạo khe khí khổng; đặc điểm giải phẫu quyết định là thành tế bào phía trong (sát khe) rất dày, còn thành phía ngoài mỏng và đàn hồi.",
                ChucNang = "Đóng mở chủ động điều tiết >90% lượng hơi nước thoát ra và 100% lượng CO₂/O₂ trao đổi: khi có ánh sáng, bơm K⁺ và tích lũy đường làm tế bào hạt đậu hút nước trương lên → thành ngoài dãn mạnh kéo thành trong cong mở khe; khi đất khô hạn, rễ tiết hoocmôn ABA làm K⁺ thoát ra, tế bào mất nước xẹp xuống → khe đóng kín.",
                SoSanhMotLaMam = "Tế bào khí khổng cây hai lá mầm có hình hạt đậu (hình thận); tế bào khí khổng cây một lá mầm họ Lúa có hình quả tạ.",
                UngDungThucTe = "Phun chế phẩm chống hạn hoặc tưới tiết kiệm giúp cây kiểm soát độ mở khí khổng vừa đủ để vẫn nhận CO₂ quang hợp mà không bị mất nước quá mức.",
                CauHoiNhanh = "Vì sao khi hai tế bào hình hạt đậu hút no nước thì khe khí khổng lại mở rộng ra thay vì phồng khít vào nhau?", DapAn = "Vì thành ngoài mỏng dãn nở mạnh hơn nhiều so với thành trong dày, từ đó kéo vách thành trong uốn cong về hai phía làm mở rộng khe khí khổng.",
                Nguon = "SGK Sinh học 11 GDPT 2018 — Trao đổi nước & Quang hợp"
            }
        };

        // 5 kịch bản thí nghiệm mẫu một chạm phục vụ dạy học trực quan trên lớp
        public static readonly KichBanThucHanh[] DanhSachKichBan =
        {
            new KichBanThucHanh
            {
                Ma = "toi_uu_c3", Ten = "1. Ban ngày tối ưu",
                MoTaNgan = "Ánh sáng 28.000 lux, CO₂ 420 ppm, 27 °C, ẩm đất 75%: khí khổng mở rộng, quang hợp & dòng mạch đạt đỉnh.",
                KhungNhinGoiY = "tong_the", BoPhanGoiY = "than_cay",
                DieuKien = new DieuKienMoiTruong { AnhSangLux = 28000, Co2Ppm = 420, NhietDoC = 27, DoAmKhongKhiPct = 62, DoAmDatPct = 75, Gio01 = 0.28, GocDenHuongSangDo = 20, SoGioHuongSang = 8 },
                KetLuanKhoaHoc = "Ở điều kiện tối ưu C3, khí khổng mở >80%, cường độ quang hợp ròng vượt xa hô hấp, dòng mạch gỗ (xanh ↑) và mạch rây (vàng ↓) lưu thông mạnh."
            },
            new KichBanThucHanh
            {
                Ma = "kho_han_aba", Ten = "2. Khô hạn & ABA đóng khí khổng",
                MoTaNgan = "Độ ẩm đất tụt xuống 10%, nắng nóng 37 °C: rễ tiết ABA làm tế bào hạt đậu mất nước, khép chặt khí khổng.",
                KhungNhinGoiY = "lat_cat_la", BoPhanGoiY = "la_khi_khong",
                DieuKien = new DieuKienMoiTruong { AnhSangLux = 36000, Co2Ppm = 400, NhietDoC = 37, DoAmKhongKhiPct = 32, DoAmDatPct = 10, Gio01 = 0.55, GocDenHuongSangDo = 15, SoGioHuongSang = 10 },
                KetLuanKhoaHoc = "Khi đất khô hạn (<15%), nồng độ hoocmôn ABA tăng vọt kích hoạt kênh thoát K⁺ làm tế bào hạt đậu giảm sức trương, đóng khe khí khổng để cứu cây khỏi héo chết."
            },
            new KichBanThucHanh
            {
                Ma = "ban_dem_ho_hap", Ten = "3. Ban đêm (Chỉ hô hấp)",
                MoTaNgan = "Ánh sáng 0 lux: pha sáng ngừng hoàn toàn, khí khổng khép, cây ngừng thải O₂ và chỉ còn hô hấp tối.",
                KhungNhinGoiY = "lat_cat_la", BoPhanGoiY = "la_mo_giau_va_mo_xop",
                DieuKien = new DieuKienMoiTruong { AnhSangLux = 0, Co2Ppm = 410, NhietDoC = 25, DoAmKhongKhiPct = 78, DoAmDatPct = 72, Gio01 = 0.15, GocDenHuongSangDo = 0, SoGioHuongSang = 0 },
                KetLuanKhoaHoc = "Ban đêm (0 lux < điểm bù ánh sáng 1000 lux), quang hợp thô bằng 0; cây chỉ thực hiện hô hấp hiếu khí (Q₁₀ = 2) nên quang hợp ròng mang giá trị âm."
            },
            new KichBanThucHanh
            {
                Ma = "huong_sang_auxin", Ten = "4. Hướng sáng dương (Auxin)",
                MoTaNgan = "Chiếu đèn nghiêng +60° trong 42 giờ: Auxin (IAA) dồn sang phía tối (~70%) làm thân uốn cong về phía đèn.",
                KhungNhinGoiY = "tong_the", BoPhanGoiY = "than_cay",
                DieuKien = new DieuKienMoiTruong { AnhSangLux = 32000, Co2Ppm = 410, NhietDoC = 26, DoAmKhongKhiPct = 65, DoAmDatPct = 74, Gio01 = 0.20, GocDenHuongSangDo = 60, SoGioHuongSang = 42 },
                KetLuanKhoaHoc = "Ánh sáng đơn hướng làm Auxin (hạt vàng phát sáng) di chuyển ngang về phía khuất sáng của thân, kích thích tế bào phía tối dãn dài nhanh hơn làm ngọn uốn về nguồn sáng."
            },
            new KichBanThucHanh
            {
                Ma = "nha_kinh_co2", Ten = "5. Nhà kính tăng CO₂",
                MoTaNgan = "Bổ sung CO₂ lên 1050 ppm kết hợp ánh sáng 42.000 lux: bứt phá giới hạn Rubisco, tăng mạnh dòng đường mạch rây.",
                KhungNhinGoiY = "lat_cat_than", BoPhanGoiY = "than_mach_ray",
                DieuKien = new DieuKienMoiTruong { AnhSangLux = 42000, Co2Ppm = 1050, NhietDoC = 28, DoAmKhongKhiPct = 68, DoAmDatPct = 80, Gio01 = 0.25, GocDenHuongSangDo = 10, SoGioHuongSang = 6 },
                KetLuanKhoaHoc = "Tăng nồng độ CO₂ trong nhà kính từ 400 lên 1050 ppm giúp ức chế hô hấp sáng ở cây C3, đẩy tốc độ đồng hóa đường và dòng chảy mạch rây lên mức tối đa."
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
            },
            new CauHoiTracNghiem
            {
                Cau = "Vì sao khi hai tế bào hình hạt đậu ở khí khổng hút no nước thì khe khí khổng lại mở rộng ra?",
                LuaChon = new[] { "Vì thành trong mỏng hơn thành ngoài", "Vì thành ngoài mỏng dãn nở mạnh kéo thành trong dày cong ra hai bên", "Vì lục lạp đẩy nhân tế bào ra ngoài", "Vì lớp cutin co rút lại khi gặp nước" },
                DapAnDung = 1,
                GiaiThich = "Tế bào hình hạt đậu có thành ngoài mỏng và thành trong (sát khe) rất dày; khi trương nước, thành ngoài dãn nhiều hơn kéo thành trong uốn cong làm mở khe."
            },
            new CauHoiTracNghiem
            {
                Cau = "Khi khoanh một vòng vỏ thân cây gỗ hai lá mầm (bóc đứt mạch rây), hiện tượng nào sẽ xảy ra sau vài tuần?",
                LuaChon = new[] { "Lá cây héo ngay lập tức vì đứt mạch gỗ", "Mép vỏ phía dưới vết cắt phình to", "Mép vỏ phía trên vết cắt phình to do ứ đọng chất hữu cơ từ lá chuyển xuống", "Toàn bộ dòng nước đi xuống rễ tăng gấp đôi" },
                DapAnDung = 2,
                GiaiThich = "Mạch rây nằm ở phần vỏ ngoài dẫn chất hữu cơ từ lá xuống rễ; khi bị cắt đứt, chất hữu cơ tích tụ ở mép trên vết cắt làm mô phình to."
            },
            new CauHoiTracNghiem
            {
                Cau = "Tại sao mặt trên của lá cây hai lá mầm thường có màu xanh đậm hơn mặt dưới?",
                LuaChon = new[] { "Mặt trên có nhiều khí khổng hơn mặt dưới", "Tế bào mô giậu nằm sát biểu bì mặt trên xếp khít và chứa rất nhiều lục lạp", "Mặt trên không có lớp cutin", "Mạch rây tập trung ở sát mặt trên của lá" },
                DapAnDung = 1,
                GiaiThich = "Lớp tế bào mô giậu nằm ngay dưới biểu bì trên có mật độ lục lạp cao gấp nhiều lần lớp mô xốp ở mặt dưới để hứng ánh sáng hiệu quả."
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
