// Kiểm thử đơn vị C# thuần cho bộ máy mô phỏng thực vật (không phụ thuộc UnityEngine).
using System;
using System.Text;

namespace Khkt.Simulation.Bio.ThucVat
{
    public static class ThucVatSimTests
    {
        public static bool RunAll(out int passed, out int failed, out string report)
        {
            int p = 0, f = 0;
            var sb = new StringBuilder();

            void Check(bool ok, string label, string detail = "")
            {
                if (ok)
                {
                    p++;
                    sb.AppendLine($"  ok   {label}");
                }
                else
                {
                    f++;
                    sb.AppendLine($"  SAI  {label} ({detail})");
                }
            }

            // 1. Đường bão hòa ánh sáng + điểm bù + ban đêm chỉ còn hô hấp
            var dk = DieuKienMoiTruong.MacDinh;
            dk.AnhSangLux = 0.0;
            var dem = ThucVatMoPhong.TinhToan(dk);
            Check(dem.BanDem && dem.CuongDoQuangHopTho == 0.0 && dem.CuongDoQuangHopRong < 0.0 && dem.CuongDoHoHap > 0.0,
                "Ban đêm ngừng quang hợp, chỉ còn hô hấp",
                $"qhTho={dem.CuongDoQuangHopTho:F2}, qhRong={dem.CuongDoQuangHopRong:F2}, hoHap={dem.CuongDoHoHap:F2}");

            dk.AnhSangLux = 900.0; // dưới điểm bù 1000 lux
            var duoiBu = ThucVatMoPhong.TinhToan(dk);
            Check(duoiBu.CuongDoQuangHopTho == 0.0 && duoiBu.CuongDoQuangHopRong <= 0.0,
                "Dưới điểm bù 1000 lux: quang hợp thực <= 0");

            dk.AnhSangLux = 6000.0;
            var sangVua = ThucVatMoPhong.TinhToan(dk);
            dk.AnhSangLux = 25000.0;
            var sangCao = ThucVatMoPhong.TinhToan(dk);
            dk.AnhSangLux = 55000.0;
            var sangBaoHoa = ThucVatMoPhong.TinhToan(dk);
            double tangDau = sangCao.CuongDoQuangHopRong - sangVua.CuongDoQuangHopRong;
            double tangSau = sangBaoHoa.CuongDoQuangHopRong - sangCao.CuongDoQuangHopRong;
            Check(sangVua.CuongDoQuangHopRong > 0.0 &&
                  sangCao.CuongDoQuangHopRong > sangVua.CuongDoQuangHopRong &&
                  sangBaoHoa.CuongDoQuangHopRong >= sangCao.CuongDoQuangHopRong &&
                  tangSau < tangDau,
                "Quang hợp tăng theo ánh sáng và tiệm cận bão hòa",
                $"6k={sangVua.CuongDoQuangHopRong:F2}, 25k={sangCao.CuongDoQuangHopRong:F2}, 55k={sangBaoHoa.CuongDoQuangHopRong:F2}");

            // 2. Bão hòa CO2 + nhiệt độ tối ưu
            dk = DieuKienMoiTruong.MacDinh;
            dk.Co2Ppm = 50.0;
            var co2Bu = ThucVatMoPhong.TinhToan(dk);
            dk.Co2Ppm = 400.0;
            var co2Thuong = ThucVatMoPhong.TinhToan(dk);
            dk.Co2Ppm = 1000.0;
            var co2Cao = ThucVatMoPhong.TinhToan(dk);
            Check(co2Bu.CuongDoQuangHopTho == 0.0 && co2Thuong.CuongDoQuangHopTho > 0.0 && co2Cao.CuongDoQuangHopTho > co2Thuong.CuongDoQuangHopTho,
                "Quang hợp tuân thủ điểm bù và bão hòa CO2");

            dk = DieuKienMoiTruong.MacDinh;
            dk.NhietDoC = 12.0; var nhietThap = ThucVatMoPhong.TinhToan(dk);
            dk.NhietDoC = 28.0; var nhietToiUu = ThucVatMoPhong.TinhToan(dk);
            dk.NhietDoC = 43.0; var nhietCao = ThucVatMoPhong.TinhToan(dk);
            Check(nhietToiUu.CuongDoQuangHopTho > nhietThap.CuongDoQuangHopTho &&
                  nhietToiUu.CuongDoQuangHopTho > nhietCao.CuongDoQuangHopTho,
                "Nhiệt độ tối ưu ~28 °C cho quang hợp cao hơn 12 °C và 43 °C");

            // 3. Độ ẩm không khí tăng -> thoát hơi nước giảm; Gió tăng -> thoát hơi nước tăng; mạch gỗ tỉ lệ thoát hơi nước
            dk = DieuKienMoiTruong.MacDinh;
            dk.DoAmKhongKhiPct = 25.0; var rhThap = ThucVatMoPhong.TinhToan(dk);
            dk.DoAmKhongKhiPct = 85.0; var rhCao = ThucVatMoPhong.TinhToan(dk);
            Check(rhCao.TocDoThoatHoiNuocMmPhut < rhThap.TocDoThoatHoiNuocMmPhut &&
                  rhCao.TocDoMachGo < rhThap.TocDoMachGo,
                "Độ ẩm không khí tăng thì thoát hơi nước và dòng mạch gỗ giảm",
                $"rh25={rhThap.TocDoThoatHoiNuocMmPhut:F2}, rh85={rhCao.TocDoThoatHoiNuocMmPhut:F2}");

            dk = DieuKienMoiTruong.MacDinh;
            dk.Gio01 = 0.0; var khongGio = ThucVatMoPhong.TinhToan(dk);
            dk.Gio01 = 0.9; var gioManh = ThucVatMoPhong.TinhToan(dk);
            Check(gioManh.TocDoThoatHoiNuocMmPhut > khongGio.TocDoThoatHoiNuocMmPhut,
                "Gió tăng làm tăng tốc độ thoát hơi nước");

            // 4. Thiếu nước đất -> ABA tăng -> tế bào hạt đậu xẹp, khí khổng khép
            dk = DieuKienMoiTruong.MacDinh;
            dk.DoAmDatPct = 75.0; var datDu = ThucVatMoPhong.TinhToan(dk);
            dk.DoAmDatPct = 12.0; var datHan = ThucVatMoPhong.TinhToan(dk);
            Check(datHan.NongDoAba01 > 0.7 &&
                  datHan.DoMoKhiKhong01 < datDu.DoMoKhiKhong01 * 0.35 &&
                  datHan.DoTruongTeBaoHatDau01 < datDu.DoTruongTeBaoHatDau01 * 0.5 &&
                  datHan.TocDoThoatHoiNuocMmPhut < datDu.TocDoThoatHoiNuocMmPhut * 0.4,
                "Thiếu nước đất kích hoạt ABA làm xẹp tế bào hạt đậu và đóng khí khổng");

            // 5. Dòng đường mạch rây đi theo quang hợp
            Check(sangBaoHoa.TocDoMachRay > dem.TocDoMachRay && dem.TocDoMachRay == 0.0,
                "Dòng đường mạch rây tỉ lệ với cường độ quang hợp thuần");

            // 6. Hướng sáng: chiếu lệch -> auxin dồn phía tối > 0.5 và thân uốn cong
            dk = DieuKienMoiTruong.MacDinh;
            dk.GocDenHuongSangDo = 45.0;
            dk.SoGioHuongSang = 36.0;
            var huongSang = ThucVatMoPhong.TinhToan(dk);
            Check(huongSang.GocUonThanDo > 30.0 && huongSang.TiLeAuxinPhiaToi > 0.60 && huongSang.TiLeAuxinPhiaSang < 0.40,
                "Chiếu đèn lệch pha: auxin dồn phía tối và ngọn uốn về phía sáng",
                $"uon={huongSang.GocUonThanDo:F1}°, auxinToi={huongSang.TiLeAuxinPhiaToi:F2}");

            dk.AnhSangLux = 0.0;
            var etiol = ThucVatMoPhong.TinhToan(dk);
            Check(etiol.GocUonThanDo == 0.0 && etiol.HeSoVuonDaiTrongToi > 1.5,
                "Trong tối hoàn toàn thân mọc thẳng và vươn dài (etiolation)");

            // 7. Dữ liệu giáo khoa đầy đủ: 3 lát cắt, 5 bước bài giảng, 5 câu hỏi
            bool hasRe = false, hasThan = false, hasLa = false;
            foreach (var bp in ThucVatMoPhong.DanhMucBoPhan)
            {
                if (bp.Nhom == "lat_cat_re") hasRe = true;
                else if (bp.Nhom == "lat_cat_than") hasThan = true;
                else if (bp.Nhom == "lat_cat_la") hasLa = true;
            }
            Check(hasRe && hasThan && hasLa &&
                  ThucVatMoPhong.DanhSachBaiGiang.Length == 5 &&
                  ThucVatMoPhong.DanhSachCauHoi.Length == 5,
                "Đủ danh mục 3 lát cắt, 5 bước bài giảng và 5 câu hỏi nhanh");

            passed = p;
            failed = f;
            report = sb.ToString();
            return f == 0;
        }
    }
}
