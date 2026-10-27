using System.Collections.Generic;

namespace Khkt.Demo
{
    /// <summary>
    /// Thư viện thí nghiệm mẫu robot làm được (cả khi không có AI). Mỗi thí nghiệm là một kế hoạch kỹ năng như kế hoạch
    /// AI trả về; "@ong1", "@ong2"... là ống nghiệm trống gần người dùng, "@nhan:Tên" là bình theo nhãn, "@may:Aas" là máy.
    /// Danh sách khóa phải trùng với TEMPLATES trong server/ai/robot_plan.py (kiểm thử Python đối chiếu file này).
    /// </summary>
    public static class RobotDemos
    {
        public static readonly (string key, string title)[] All =
        {
            ("nhan_biet_cu2", "Nhận biết ion Cu²⁺ bằng NaOH"),
            ("nhan_biet_cl", "Nhận biết ion Cl⁻ bằng AgNO₃"),
            ("nhan_biet_so4", "Nhận biết ion SO₄²⁻ bằng BaCl₂"),
            ("nhan_biet_fe3", "Nhận biết ion Fe³⁺ bằng KSCN"),
            ("chi_thi_axit_bazo", "Phenolphtalein và phản ứng trung hòa"),
            ("kim_loai_axit", "Kẽm tác dụng với axit clohiđric"),
            ("do_ph_dung_dich", "So sánh pH của axit, bazơ và nước cất"),
            ("ly_tam_ket_tua", "Tạo kết tủa rồi ly tâm tách rắn - lỏng"),
            ("glucozo_cu_oh2", "Glucozơ với Cu(OH)₂: phức xanh lam, đun ra Cu₂O đỏ gạch"),
            ("phan_tich_nuoc", "Phân tích kim loại trong mẫu nước bằng AAS"),
            ("chuan_do", "Chuẩn độ axit - bazơ trọn vẹn"),
        };

        // Số thứ tự trong danh mục: lọ có sẵn ở bàn định tính (bàn 4) hoặc trong phòng hóa chất (robot tự đi lấy)
        const int HCl = 98, NaOH = 124, BaCl2 = 155, FeCl3 = 159, AgNO3 = 230, CuSO4 = 253, KSCN = 351, PP = 394, Zn = 19, Glucozo = 578;

        public static string Title(string key)
        {
            foreach (var (k, t) in All) if (k == key) return t;
            return null;
        }

        static RobotStep S(string skill, string obj = "", string dest = "", float n = 0, string say = "", int stt = 0) =>
            new RobotStep { ky_nang = skill, doi_tuong = obj, dich = dest, so_luong = n, loi = say, stt = stt };

        static string H(int stt) => "hoa_chat:" + stt;
        static RobotStep Say(string s) => S("noi", say: s);

        public static List<RobotStep> Build(string key)
        {
            switch (key)
            {
                case "nhan_biet_cu2":
                    return new List<RobotStep>
                    {
                        Say("Cho dung dịch NaOH vào dung dịch muối đồng(II), nếu có Cu²⁺ sẽ thấy kết tủa xanh lam."),
                        S("rot", H(CuSO4), "@ong1", 2), S("nho_giot", H(NaOH), "@ong1", 6), S("lac", "@ong1", n: 3), S("quan_sat", "@ong1"),
                        Say("Cu²⁺ + 2OH⁻ → Cu(OH)₂↓ màu xanh lam. Đây là cách nhận biết ion đồng(II)."),
                    };
                case "nhan_biet_cl":
                    return new List<RobotStep>
                    {
                        Say("Nhỏ dung dịch AgNO₃ vào dung dịch có ion clorua sẽ có kết tủa trắng."),
                        S("rot", H(HCl), "@ong1", 2), S("nho_giot", H(AgNO3), "@ong1", 4), S("lac", "@ong1", n: 3), S("quan_sat", "@ong1"),
                        Say("Ag⁺ + Cl⁻ → AgCl↓ trắng, không tan trong axit HNO₃, để ngoài sáng hóa đen dần."),
                    };
                case "nhan_biet_so4":
                    return new List<RobotStep>
                    {
                        Say("Nhỏ BaCl₂ vào dung dịch chứa ion sunfat sẽ có kết tủa trắng BaSO₄."),
                        S("rot", H(CuSO4), "@ong1", 2), S("nho_giot", H(BaCl2), "@ong1", 5), S("lac", "@ong1", n: 3), S("quan_sat", "@ong1"),
                        Say("Ba²⁺ + SO₄²⁻ → BaSO₄↓ trắng, không tan cả trong axit mạnh."),
                    };
                case "nhan_biet_fe3":
                    return new List<RobotStep>
                    {
                        Say("Ion Fe³⁺ tạo phức màu đỏ máu với thuốc thử KSCN."),
                        S("rot", H(FeCl3), "@ong1", 2), S("nho_giot", H(KSCN), "@ong1", 2), S("lac", "@ong1", n: 2), S("quan_sat", "@ong1"),
                        Say("Fe³⁺ + SCN⁻ → [Fe(SCN)]²⁺ đỏ máu, rất nhạy, phát hiện được lượng Fe³⁺ rất nhỏ."),
                    };
                case "chi_thi_axit_bazo":
                    return new List<RobotStep>
                    {
                        Say("Phenolphtalein không màu trong axit và trung tính, hóa hồng trong môi trường bazơ."),
                        S("rot", H(NaOH), "@ong1", 2), S("nho_giot", H(PP), "@ong1", 2), S("lac", "@ong1", n: 2), S("quan_sat", "@ong1"),
                        Say("Giờ thêm axit HCl dư để trung hòa hết NaOH."),
                        S("rot", H(HCl), "@ong1", 4), S("lac", "@ong1", n: 3), S("quan_sat", "@ong1"),
                        Say("H⁺ + OH⁻ → H₂O: hết bazơ nên màu hồng biến mất. Phản ứng trung hòa tỏa nhiệt."),
                    };
                case "kim_loai_axit":
                    return new List<RobotStep>
                    {
                        Say("Kim loại đứng trước hiđro tác dụng với axit HCl giải phóng khí H₂. Mình lấy kẽm hạt trong phòng hóa chất."),
                        S("lay_hoa_chat", stt: Zn), S("xuc", H(Zn), "@ong1", 2), S("rot", H(HCl), "@ong1", 4), S("doi", n: 4), S("quan_sat", "@ong1"),
                        Say("Zn + 2HCl → ZnCl₂ + H₂↑: kẽm tan dần, sủi bọt khí hiđro không màu. Không để gần lửa vì H₂ dễ cháy."),
                    };
                case "do_ph_dung_dich":
                    return new List<RobotStep>
                    {
                        Say("Mình lấy axit, bazơ và nước cất vào ba ống rồi đo pH bằng máy đo pH cầm tay."),
                        S("rot", H(HCl), "@ong1", 3), S("rot", H(NaOH), "@ong2", 3), S("tia_nuoc", "", "@ong3", 3),
                        S("do_ph", "@ong1"), S("do_ph", "@ong2"), S("do_ph", "@ong3"),
                        Say("Axit có pH nhỏ hơn 7, bazơ lớn hơn 7, nước cất gần 7. pH càng nhỏ, môi trường càng axit."),
                    };
                case "ly_tam_ket_tua":
                    return new List<RobotStep>
                    {
                        Say("Tạo kết tủa Cu(OH)₂ rồi dùng máy ly tâm tách kết tủa ra khỏi dung dịch."),
                        // CuSO4 0,1 M 1 ml (0,1 mmol) + NaOH 0,1 M 2 ml (0,2 mmol): vừa đủ kết tủa hết Cu(OH)2
                        S("rot", H(CuSO4), "@ong1", 1), S("rot", H(NaOH), "@ong1", 2), S("lac", "@ong1", n: 2), S("ly_tam", "@ong1"), S("quan_sat", "@ong1"),
                        Say("Lực ly tâm làm kết tủa lắng nhanh xuống đáy; gạn phần dung dịch trong phía trên là tách được rắn khỏi lỏng."),
                    };
                case "glucozo_cu_oh2":
                    return new List<RobotStep>
                    {
                        Say("Glucozơ có nhiều nhóm OH kề nhau nên hòa tan Cu(OH)₂ tạo dung dịch xanh lam; đun nóng thì nhóm anđehit khử Cu(II) thành Cu₂O đỏ gạch."),
                        S("lay_hoa_chat", stt: Glucozo),
                        // NaOH dư (0,3 mmol so với 0,1 mmol Cu²⁺): Cu(OH)2 trong môi trường kiềm, điều kiện để glucozơ khử Cu(II) khi đun
                        S("rot", H(CuSO4), "@ong1", 1), S("rot", H(NaOH), "@ong1", 3), S("lac", "@ong1", n: 2), S("quan_sat", "@ong1"),
                        S("xuc", H(Glucozo), "@ong1", 2), S("lac", "@ong1", n: 4), S("quan_sat", "@ong1"),
                        S("dun_nong", "@ong1", n: 85), S("quan_sat", "@ong1"),
                        Say("Kết tủa đỏ gạch Cu₂O chứng tỏ glucozơ có nhóm -CHO (tính khử)."),
                    };
                case "phan_tich_nuoc":
                    return new List<RobotStep>
                    {
                        Say("So sánh kim loại tan trong nước giếng và nước thải mạ điện bằng máy quang phổ hấp thụ nguyên tử AAS."),
                        S("phan_tich", "@nhan:Mẫu nước giếng", "@may:Aas"),
                        S("phan_tich", "@nhan:Mẫu nước thải mạ điện", "@may:Aas"),
                        Say("Nước thải mạ điện chứa Cu, Zn, Ni hàng trăm mg/L, phải xử lý (kết tủa hiđroxit) trước khi thải ra môi trường."),
                    };
                case "chuan_do":
                    return new List<RobotStep> { S("chuan_do") };
            }
            return null;
        }
    }
}
