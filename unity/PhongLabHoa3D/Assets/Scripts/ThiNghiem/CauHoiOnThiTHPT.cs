using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem.ThiNghiem
{
    [Serializable]
    public class CauHoiTracNghiem
    {
        public string NoiDung;
        public string[] LuaChon; // 4 lua chon [A, B, C, D]
        public int DapAnDung;    // 0: A, 1: B, 2: C, 3: D
        public string GiaiThich;
        public string NguonDeThi;
    }

    /// <summary>
    /// Ngân hàng câu hỏi trắc nghiệm ôn thi THPT chính thức gắn liền với 5 trạm thí nghiệm:
    /// Nguồn: Đề thi tốt nghiệp THPT Quốc gia môn Hoá học chính thức của Bộ GD&ĐT (2021 - 2024).
    /// </summary>
    public static class CauHoiOnThiTHPT
    {
        private static readonly Dictionary<string, List<CauHoiTracNghiem>> _nganHang = new Dictionary<string, List<CauHoiTracNghiem>>();

        static CauHoiOnThiTHPT()
        {
            KhoiTaoNganHangCauHoi();
        }

        public static List<CauHoiTracNghiem> LayCauHoi(string tramId)
        {
            if (_nganHang.TryGetValue(tramId, out var ds)) return ds;
            return new List<CauHoiTracNghiem>();
        }

        private static void KhoiTaoNganHangCauHoi()
        {
            // 1. Trạm Chuẩn độ Acid - Base
            _nganHang["chuan-do"] = new List<CauHoiTracNghiem>
            {
                new CauHoiTracNghiem
                {
                    NoiDung = "Trong phép chuẩn độ dung dịch HCl bằng dung dịch NaOH tiêu chuẩn sử dụng chỉ thị phenolphthalein, thời điểm kết thúc chuẩn độ (điểm tương đương) được xác định khi dung dịch trong bình tam giác:",
                    LuaChon = new string[] {
                        "A. Chuyển từ không màu sang xuất hiện màu hồng nhạt bền vững trong 30 giây.",
                        "B. Chuyển từ màu hồng đậm sang không màu.",
                        "C. Xuất hiện kết tủa màu trắng đục.",
                        "D. Chuyển từ màu đỏ sang màu vàng cam."
                    },
                    DapAnDung = 0,
                    GiaiThich = "Phenolphthalein không màu trong môi trường axit (pH < 8.2). Khi NaOH chuẩn độ vừa hết HCl, thêm 1 giọt NaOH dư làm pH vượt qua 8.3, dung dịch chuyển sang màu hồng nhạt bền vững.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2023 - Mã đề 201"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Chuẩn độ 20,0 mL dung dịch HCl chưa biết nồng độ cần dùng vừa đủ 20,0 mL dung dịch NaOH 0,10 M. Nồng độ mol của dung dịch HCl là:",
                    LuaChon = new string[] {
                        "A. 0,05 M",
                        "B. 0,10 M",
                        "C. 0,20 M",
                        "D. 0,15 M"
                    },
                    DapAnDung = 1,
                    GiaiThich = "Phản ứng: HCl + NaOH -> NaCl + H2O. Số mol NaOH = 0.02 * 0.1 = 0.002 mol => n(HCl) = 0.002 mol => C_M(HCl) = 0.002 / 0.02 = 0.10 M.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2022 - Mã đề 204"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Dụng cụ chính xác nhất dùng để đong lấy thể tích dung dịch chuẩn NaOH khi tiến hành chuẩn độ là:",
                    LuaChon = new string[] {
                        "A. Ống đong hình trụ 50 mL.",
                        "B. Cốc thuỷ tinh có vạch chia 100 mL.",
                        "C. Burette (buret) 50 mL có khoá vòi.",
                        "D. Bình tam giác (Erlenmeyer) 250 mL."
                    },
                    DapAnDung = 2,
                    GiaiThich = "Burette là dụng cụ chuẩn độ chuyên dụng có độ chính xác cao nhất (vạch chia đến 0.1 mL và sai số <= 0.05 mL theo ISO 385).",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2021 - Mã đề 218"
                }
            };

            // 2. Trạm Nhận biết Ion & Thử ngọn lửa
            _nganHang["nhan-biet"] = new List<CauHoiTracNghiem>
            {
                new CauHoiTracNghiem
                {
                    NoiDung = "Nhỏ từ từ dung dịch NaOH đến dư vào ống nghiệm chứa dung dịch AlCl3, hiện tượng quan sát được là:",
                    LuaChon = new string[] {
                        "A. Xuất hiện kết tủa trắng keo không tan trong NaOH dư.",
                        "B. Xuất hiện kết tủa trắng keo, sau đó kết tủa tan dần tạo dung dịch trong suốt.",
                        "C. Xuất hiện kết tủa màu nâu đỏ và có khí mùi khai bay ra.",
                        "D. Không có hiện tượng gì xuất hiện."
                    },
                    DapAnDung = 1,
                    GiaiThich = "Al3+ + 3OH- -> Al(OH)3 (kết tủa keo trắng). Khi NaOH dư: Al(OH)3 + OH- -> [Al(OH)4]- (tan thành dung dịch aluminat trong suốt).",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2024 - Mã đề 202"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Dung dịch chứa ion Fe2+ khi tác dụng với dung dịch NaOH trong không khí tạo kết tủa màu trắng hơi xanh, sau đó chuyển dần thành:",
                    LuaChon = new string[] {
                        "A. Màu vàng tươi của FeS.",
                        "B. Màu xanh lam thẫm của phức chất.",
                        "C. Màu nâu đỏ của Fe(OH)3 do bị oxi hoá bởi oxi không khí.",
                        "D. Màu đen của FeO."
                    },
                    DapAnDung = 2,
                    GiaiThich = "Fe2+ + 2OH- -> Fe(OH)2 (trắng xanh). Để ngoài không khí: 4Fe(OH)2 + O2 + 2H2O -> 4Fe(OH)3 (kết tủa nâu đỏ).",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2023 - Mã đề 215"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Khi nhúng que dây platin sạch vào dung dịch muối của kim loại kiềm rồi đưa vào ngọn lửa đèn cồn, ngọn lửa nhuộm màu vàng tươi đặc trưng. Kim loại đó là:",
                    LuaChon = new string[] {
                        "A. Kali (K).",
                        "B. Natri (Na).",
                        "C. Liti (Li).",
                        "D. Canxi (Ca)."
                    },
                    DapAnDung = 1,
                    GiaiThich = "Ngọn lửa hợp chất Natri nhuộm màu vàng tươi rực rỡ, Kali màu tím nhạt, Liti màu đỏ son, Canxi màu đỏ cam.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2022 - Mã đề 207"
                }
            };

            // 3. Trạm Pin Galvani Zn - Cu
            _nganHang["pin-galvani"] = new List<CauHoiTracNghiem>
            {
                new CauHoiTracNghiem
                {
                    NoiDung = "Trong pin điện hoá Zn - Cu (pin Daniel), phát biểu nào sau đây là ĐÚNG?",
                    LuaChon = new string[] {
                        "A. Thanh Zn là cực dương (cathode), thanh Cu là cực âm (anode).",
                        "B. Dòng electron chuyển dịch từ thanh Cu sang thanh Zn qua dây dẫn ngoài.",
                        "C. Thanh Zn là cực âm (anode) xảy ra quá trình oxi hoá: Zn -> Zn2+ + 2e.",
                        "D. Khối lượng thanh Zn tăng dần và khối lượng thanh Cu giảm dần."
                    },
                    DapAnDung = 2,
                    GiaiThich = "Zn có tính khử mạnh hơn Cu nên Zn đóng vai trò là cực âm (anode) bị oxi hoá (Zn -> Zn2+ + 2e). Electron đi từ Zn sang Cu qua dây dẫn. Tại cathode (Cu), Cu2+ + 2e -> Cu bám vào.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2024 - Mã đề 210"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Cho thế điện cực chuẩn E°(Zn2+/Zn) = -0,76 V và E°(Cu2+/Cu) = +0,34 V. Sức điện động chuẩn của pin Zn - Cu là:",
                    LuaChon = new string[] {
                        "A. 1,10 V",
                        "B. 0,42 V",
                        "C. -1,10 V",
                        "D. 0,76 V"
                    },
                    DapAnDung = 0,
                    GiaiThich = "E°pin = E°(cathode) - E°(anode) = E°(Cu2+/Cu) - E°(Zn2+/Zn) = 0.34 - (-0.76) = +1.10 V.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2023 - Mã đề 209"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Vai trò quan trọng nhất của cầu muối chứa dung dịch bão hoà KCl trong pin Galvani là:",
                    LuaChon = new string[] {
                        "A. Dẫn các electron chuyển động từ cốc này sang cốc kia.",
                        "B. Đóng kín mạch điện và trung hoà điện tích ở hai nửa tế bào pin.",
                        "C. Làm tăng điện thế của pin lên gấp đôi.",
                        "D. Ngăn cản hoàn toàn sự chuyển động của các ion."
                    },
                    DapAnDung = 1,
                    GiaiThich = "Cầu muối chứa chất điện li trơ giúp các cation và anion khuếch tán để trung hoà điện tích tích tụ ở 2 dung dịch điện cực, đồng thời đóng kín mạch điện.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2021 - Mã đề 205"
                }
            };

            // 4. Trạm Điện phân dung dịch CuSO4
            _nganHang["dien-phan"] = new List<CauHoiTracNghiem>
            {
                new CauHoiTracNghiem
                {
                    NoiDung = "Điện phân 200 mL dung dịch CuSO4 với điện cực trơ (than chì). Ở cực âm (cathode) xảy ra quá trình:",
                    LuaChon = new string[] {
                        "A. Oxi hoá ion Cu2+: Cu2+ + 2e -> Cu.",
                        "B. Khử ion Cu2+: Cu2+ + 2e -> Cu.",
                        "C. Khử phân tử H2O: 2H2O + 2e -> H2 + 2OH-.",
                        "D. Oxi hoá phân tử H2O: 2H2O -> O2 + 4H+ + 4e."
                    },
                    DapAnDung = 1,
                    GiaiThich = "Ở cực âm (cathode) luôn xảy ra quá trình KHỬ: ion Cu2+ có tính oxi hoá mạnh hơn nước nên nhận electron trước: Cu2+ + 2e -> Cu.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2024 - Mã đề 221"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Điện phân dung dịch CuSO4 bằng dòng điện không đổi I = 2,0 A trong thời gian t = 965 giây. Biết Cu = 64, hằng số Faraday F = 96500 C/mol. Khối lượng Cu bám vào cathode là:",
                    LuaChon = new string[] {
                        "A. 1,28 gam.",
                        "B. 0,32 gam.",
                        "C. 0,64 gam.",
                        "D. 0,96 gam."
                    },
                    DapAnDung = 2,
                    GiaiThich = "Áp dụng định luật Faraday: m = (A * I * t) / (n * F) = (64 * 2 * 965) / (2 * 96500) = 0.64 gam.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2022 - Mã đề 212"
                }
            };

            // 5. Trạm An toàn & Sơ cứu Phòng Lab
            _nganHang["an-toan"] = new List<CauHoiTracNghiem>
            {
                new CauHoiTracNghiem
                {
                    NoiDung = "Quy tắc pha loãng axit sunfuric đặc (H2SO4 đặc) an toàn trong phòng thí nghiệm là:",
                    LuaChon = new string[] {
                        "A. Rót nhanh nước vào bình chứa axit sunfuric đặc.",
                        "B. Rót từ từ axit sunfuric đặc dọc theo đũa thuỷ tinh vào cốc nước và khuấy đều.",
                        "C. Đổ đồng thời cả axit đặc và nước vào cốc cùng một lúc.",
                        "D. Đun nóng axit đặc trước rồi mới đổ nước vào."
                    },
                    DapAnDung = 1,
                    GiaiThich = "H2SO4 đặc tan trong nước toả nhiệt cực lớn. Axit nặng hơn nước, rót từ từ axit vào nước thì nhiệt toả ra được nước hấp thụ; ngược lại đổ nước vào axit sẽ làm nước sôi đột ngột bắn tung toé gây bỏng nặng.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2023 - Mã đề 206"
                },
                new CauHoiTracNghiem
                {
                    NoiDung = "Khi bị axit đặc hoặc kiềm đặc bắn vào da hoặc mắt, bước sơ cứu khẩn cấp đầu tiên quan trọng nhất là:",
                    LuaChon = new string[] {
                        "A. Bôi ngay mỡ trăn hoặc dầu ăn lên vết bỏng.",
                        "B. Rửa ngay lập tức dưới vòi nước sạch chảy liên tục ít nhất 15 phút, sau đó đến cơ sở y tế.",
                        "C. Rắc bột baking soda hoặc đổ giấm ăn đậm đặc để trung hoà lập tức.",
                        "D. Băng kín vết thương bằng gạc y tế khô ngay."
                    },
                    DapAnDung = 1,
                    GiaiThich = "Rửa bằng dòng nước sạch chảy liên tục >= 15 phút giúp làm loãng nhanh hoá chất và cuốn trôi nhiệt phản ứng, giảm tối đa mức độ tổn thương mô.",
                    NguonDeThi = "Đề thi TN THPT Hoá học 2021 - Mã đề 224"
                }
            };
        }
    }
}
