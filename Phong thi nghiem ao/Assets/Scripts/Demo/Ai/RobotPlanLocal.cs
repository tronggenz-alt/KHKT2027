using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Khkt.Simulation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Khkt.Demo.Ai
{
    /// <summary>
    /// Phần việc của máy chủ khi lập kế hoạch robot, chạy ngay trong app khi người dùng gọi AI bằng khóa riêng:
    /// lời dặn hệ thống, khuôn JSON, kiểm tra từng bước, tra hóa chất, quy trình máy, chặn sinh học nguy hiểm, luật giáo viên.
    /// GIỮ TRÙNG cloudflare/src/robot_plan.ts và ai.ts (cùng quy tắc với server/ai/robot_plan.py); bài thử khoa_ai so danh sách kỹ năng.
    /// </summary>
    public static class RobotPlanLocal
    {
        public static readonly (string key, string text)[] Skills =
        {
            ("di_toi", "đi tới cạnh vật (doi_tuong)"),
            ("chi_vao", "chỉ tay vào vật để minh họa (doi_tuong)"),
            ("cam", "cầm vật lên và giữ (doi_tuong)"),
            ("dat", "đặt vật đang cầm lên chỗ (dich): mã chỗ đặt, mã vật chứa để đặt cạnh, hoặc 'ban_nguoi_dung'"),
            ("tra_ve", "trả vật (doi_tuong) về chỗ cất ban đầu (kệ, giá)"),
            ("lay_hoa_chat", "vào phòng hóa chất lấy lọ (hoa_chat: tên hoặc công thức) mang ra bàn người dùng"),
            ("rot", "rót so_luong ml từ vật chứa (doi_tuong) vào vật chứa (dich)"),
            ("nho_giot", "nhỏ so_luong giọt từ lọ (doi_tuong) vào vật chứa (dich)"),
            ("xuc", "xúc so_luong thìa chất rắn từ lọ (doi_tuong) vào vật chứa (dich)"),
            ("hut_pipet", "dùng pipet bầu 25 ml lấy chính xác so_luong ml (mặc định 25) từ (doi_tuong) cho vào (dich)"),
            ("tia_nuoc", "tia so_luong ml nước cất vào vật chứa (dich): pha loãng, tráng"),
            ("suc_khi", "sục khí từ bình khí (doi_tuong, mã hoa_chat:...) vào dung dịch (dich) trong so_luong giây; khí độc robot tự đưa vào tủ hút"),
            ("do_bo", "đổ bỏ dung dịch trong (doi_tuong) vào cốc thải / thùng chất thải (so_luong ml, 0 là đổ hết)"),
            ("lac", "lắc đều vật chứa (doi_tuong) trong so_luong giây"),
            ("khuay", "đặt (doi_tuong) lên máy khuấy từ, khuấy so_luong giây"),
            ("dun_nong", "đun (doi_tuong) tới so_luong °C (mặc định 70): ống nghiệm hơ trên đèn cồn, cốc đặt lên bếp; dich là mã bếp nếu muốn chọn"),
            ("ly_tam", "cho ống nghiệm (doi_tuong) vào máy ly tâm để kết tủa lắng xuống đáy"),
            ("dua_vao_tu_hut", "mang (doi_tuong) vào tủ hút và bật quạt hút"),
            ("doi", "chờ so_luong giây để quan sát hiện tượng"),
            ("quan_sat", "nhìn và nói rõ trong (doi_tuong) có gì: chất, pH, nhiệt độ, kết tủa, khí, hiện tượng vừa xảy ra (cả bình, mẫu, thiết bị sinh học)"),
            ("do_ph", "đo pH dung dịch trong (doi_tuong) bằng máy đo pH"),
            ("do_nhiet_do", "đo nhiệt độ (doi_tuong) bằng nhiệt kế điện tử"),
            ("do_do_dan", "đo độ dẫn điện (doi_tuong)"),
            ("thu_quy", "nhúng giấy quỳ tím vào (doi_tuong)"),
            ("thu_giay_ph", "nhúng giấy chỉ thị pH vạn năng vào (doi_tuong)"),
            ("can", "cân vật (doi_tuong) trên cân điện tử"),
            ("can_chat_ran", "đặt (dich) lên cân, trừ bì rồi xúc chất rắn từ lọ (doi_tuong) tới đủ so_luong gam"),
            ("phan_tich", "đặt mẫu (doi_tuong) vào máy phân tích (dich: mã may_...; bỏ trống thì máy gần nhất) và đọc kết quả"),
            ("bat_tat", "bật hoặc tắt thiết bị (doi_tuong): đèn cồn, bếp, máy khuấy, quạt tủ hút; ở lab sinh: bấm nút (nut_), mở / đóng nắp (nap_), bấm thiết bị (thiet_bi_sinh_)"),
            ("bat_tu_hut", "bật quạt tủ hút và quạt hút của phòng"),
            ("tat_lua", "tắt mọi nguồn lửa và bếp"),
            ("don_ban", "dọn bàn: đổ dung dịch thừa, trả dụng cụ, lau chỗ tràn"),
            ("rua_mat", "dẫn tới vòi rửa mắt khẩn cấp"),
            ("dung_dung_cu", "LAB SINH: cầm dụng cụ sinh học (doi_tuong: dung_cu_sinh_, binh_sinh_, 'vat_dang_cam' hoặc 'vat_vua_lay') rồi dùng lên vật đích " +
                "(dich: binh_sinh_, sinh_pham_, mau_sinh_, thiet_bi_sinh_), lặp so_luong lần. Bình đang cầm + chai gốc: lấy khoảng 7 ml sang bình; " +
                "bình + bình: rót khoảng 7 ml; ống nhỏ giọt: lần đầu hút, các lần sau nhỏ từng giọt; kẹp: gắp mô rồi thả; dao lam: rạch, thái; " +
                "đũa: khuấy; nhiệt kế, giấy pH: đo; que cấy: lấy / cấy vi khuẩn; micropipet: hút rồi bơm"),
            ("lay_dung_cu", "LAB SINH: lấy một dụng cụ mới từ hộp cấp (doi_tuong: hop_cap_...); sau đó gọi vật vừa lấy bằng mã 'vat_vua_lay'"),
            ("chinh_num", "LAB SINH: vặn núm chỉnh (doi_tuong: num_...) về giá trị so_luong theo đơn vị của núm (°C, V, phút...)"),
            ("chuan_do", "làm mẫu trọn vẹn bài chuẩn độ đang chọn ở bàn 1: lắp buret, nạp, đọc V đầu, pipet mẫu, chỉ thị, chuẩn độ tới điểm cuối, tính nồng độ"),
            ("thi_nghiem_mau", "làm một thí nghiệm mẫu có sẵn (ghi khóa vào hoa_chat)"),
            ("noi", "nói một câu (loi) trong lúc làm, ví dụ giải thích bước đang minh họa"),
        };

        // Khóa phải trùng RobotDemos.cs
        public static readonly (string key, string title)[] Templates =
        {
            ("nhan_biet_cu2", "Nhận biết ion Cu²⁺ bằng NaOH (kết tủa xanh lam)"),
            ("nhan_biet_cl", "Nhận biết ion Cl⁻ bằng AgNO₃ (kết tủa trắng)"),
            ("nhan_biet_so4", "Nhận biết ion SO₄²⁻ bằng BaCl₂ (kết tủa trắng)"),
            ("nhan_biet_fe3", "Nhận biết ion Fe³⁺ bằng KSCN (đỏ máu)"),
            ("chi_thi_axit_bazo", "Phenolphtalein trong bazơ và phản ứng trung hòa"),
            ("kim_loai_axit", "Kẽm tác dụng với axit clohiđric (sủi bọt H₂)"),
            ("do_ph_dung_dich", "So sánh pH của axit, bazơ và nước cất bằng máy đo pH"),
            ("ly_tam_ket_tua", "Tạo kết tủa Cu(OH)₂ rồi ly tâm tách rắn - lỏng"),
            ("glucozo_cu_oh2", "Glucozơ với Cu(OH)₂: phức xanh lam, đun ra Cu₂O đỏ gạch"),
            ("phan_tich_nuoc", "Phân tích kim loại trong nước giếng và nước thải bằng AAS"),
            ("chuan_do", "Chuẩn độ axit - bazơ trọn vẹn"),
        };

        static readonly HashSet<string> NeedsObject = new HashSet<string> { "di_toi", "chi_vao", "cam", "tra_ve", "rot", "nho_giot", "xuc", "hut_pipet", "suc_khi", "do_bo", "lac", "khuay",
            "dun_nong", "ly_tam", "dua_vao_tu_hut", "quan_sat", "do_ph", "do_nhiet_do", "do_do_dan", "thu_quy", "thu_giay_ph",
            "can", "can_chat_ran", "phan_tich", "bat_tat", "lay_dung_cu", "chinh_num" };
        static readonly HashSet<string> NeedsDest = new HashSet<string> { "rot", "nho_giot", "xuc", "hut_pipet", "tia_nuoc", "suc_khi", "can_chat_ran", "dung_dung_cu" };
        static readonly HashSet<string> SpecialIds = new HashSet<string> { "vat_dang_cam", "vat_vua_lay" };
        static readonly HashSet<string> OptionalDest = new HashSet<string> { "phan_tich", "dun_nong" };
        public static readonly Dictionary<string, (float md, float lim)> Limits = new Dictionary<string, (float, float)>
        {
            ["rot"] = (2, 100), ["nho_giot"] = (1, 20), ["xuc"] = (1, 5), ["hut_pipet"] = (25, 25), ["tia_nuoc"] = (5, 100), ["suc_khi"] = (5, 30),
            ["lac"] = (4, 30), ["khuay"] = (15, 120), ["doi"] = (5, 60), ["dun_nong"] = (70, 100), ["can_chat_ran"] = (1, 20), ["do_bo"] = (0, 1000), ["dung_dung_cu"] = (1, 20),
        };

        public const int MaxSteps = 20;
        public const int MaxObjects = RobotWorld.MaxObjects;

        static readonly HashSet<string> SkillSet = new HashSet<string>(Skills.Select(s => s.key));

        public static JObject PlanSchema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tra_loi"] = new JObject { ["type"] = "string" },
                ["ke_hoach"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["ky_nang"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Skills.Select(s => s.key)) },
                            ["doi_tuong"] = new JObject { ["type"] = "string" }, ["dich"] = new JObject { ["type"] = "string" },
                            ["so_luong"] = new JObject { ["type"] = "number" }, ["hoa_chat"] = new JObject { ["type"] = "string" },
                            ["loi"] = new JObject { ["type"] = "string" },
                        },
                        ["required"] = new JArray("ky_nang", "doi_tuong", "dich", "so_luong", "hoa_chat", "loi"),
                        ["additionalProperties"] = false,
                    },
                },
            },
            ["required"] = new JArray("tra_loi", "ke_hoach"),
            ["additionalProperties"] = false,
        };

        static string systemPlan;
        public static string SystemPlan => systemPlan ?? (systemPlan =
            "Bạn là bộ não của một robot trợ lý hình người trong phòng thí nghiệm ảo Hóa - Sinh của trường THPT Việt Nam " +
            "(tầng 1: lab hóa, tầng 2: lab sinh). " +
            "Bạn KHÔNG nhìn thấy cảnh 3D; bạn chỉ biết ẢNH CHỤP CẢNH dạng chữ đi kèm (danh sách vật với mã vật, tên, loại, " +
            "khoảng cách tới người dùng, đang chứa gì). Hãy trả lời học sinh bằng tiếng Việt ngắn gọn (tối đa 3 câu) và, nếu cần " +
            "hành động, lập KẾ HOẠCH là danh sách kỹ năng theo đúng thứ tự thực hiện. Kỹ năng có sẵn:\n" +
            string.Join("\n", Skills.Select(s => $"- {s.key}: {s.text}")) + "\n" +
            "Thí nghiệm mẫu có sẵn (kỹ năng thi_nghiem_mau, ghi khóa vào hoa_chat):\n" +
            string.Join("\n", Templates.Select(t => $"- {t.key}: {t.title}")) + "\n" +
            "Quy tắc:\n" +
            "- Học sinh xin làm mẫu cả một thí nghiệm có trong danh sách mẫu thì dùng MỘT bước thi_nghiem_mau (hoặc chuan_do), " +
            "không tự chia nhỏ. Thí nghiệm khác thì tự ghép các kỹ năng.\n" +
            "- Robot tự đi lấy dụng cụ cần cho kỹ năng (máy đo pH, nhiệt kế, máy đo độ dẫn, giấy quỳ, giấy pH, pipet, bình tia, " +
            "cân, máy ly tâm, máy khuấy từ, bếp, đèn cồn, tủ hút) và dùng xong tự cất: không cần mã vật cho các dụng cụ đó.\n" +
            "- doi_tuong và dich PHẢI là mã vật có trong ảnh chụp cảnh (hoặc 'ban_nguoi_dung' cho dich của dat); trường nào " +
            "không dùng thì để chuỗi rỗng, so_luong không dùng thì để 0.\n" +
            "- Hóa chất chưa có trên bàn thì dùng lay_hoa_chat trước (ghi tên TIẾNG VIỆT theo danh pháp trong SGK như " +
            "'axit oxalic', 'kali pemanganat', 'axit sunfuric', hoặc công thức như H2C2O4), sau đó gọi lọ đó bằng mã " +
            "'hoa_chat:<đúng chuỗi đã ghi ở hoa_chat>'.\n" +
            "- Nếu phản ứng cần môi trường hay chất xúc tác (ví dụ KMnO4 oxi hóa axit oxalic cần H2SO4 loãng) thì lấy thêm chất đó " +
            "và cho vào đúng thứ tự.\n" +
            "- Lời giải thích (noi, tra_loi) phải đúng hóa học: nêu hiện tượng thật sẽ thấy (ví dụ dung dịch thuốc tím mất màu, " +
            "sủi bọt khí CO2), không đoán bừa; nếu không chắc thì để robot quan_sat rồi nói theo hiện tượng quan sát được.\n" +
            "- Dùng ống nghiệm hoặc cốc đang trống trong ảnh chụp cảnh cho phản ứng mới. Rót lượng nhỏ (ống nghiệm 1-5 ml, cốc 10-50 ml).\n" +
            "- Sau khi trộn nên lac rồi quan_sat để học sinh thấy hiện tượng; xen kỹ năng noi để giải thích từng bước.\n" +
            "- An toàn trường phổ thông: chất sinh khí độc làm trong tủ hút; không pha nước vào axit đặc; không đun chất dễ cháy " +
            "trên lửa trần; dùng dung dịch loãng, lượng nhỏ.\n" +
            "- CÁC THÍ NGHIỆM TRONG SGK HÓA 10-12 ĐỀU ĐƯỢC LÀM, không được từ chối, ví dụ: KMnO4 với axit oxalic / H2O2 / FeSO4 " +
            "(môi trường H2SO4 loãng), kim loại với axit loãng, nhận biết ion, trung hòa, chuẩn độ, điện phân dung dịch, " +
            "glucozơ với Cu(OH)2, tốc độ phản ứng, cân bằng hóa học. Chỉ từ chối (ke_hoach rỗng, giải thích nhẹ nhàng) khi yêu cầu " +
            "chế tạo chất nổ, pháo, chất độc / ma túy, hoặc tạo lượng lớn khí độc ngoài tủ hút.\n" +
            "- Đã NÓI sẽ làm gì (mang, lấy, rót, đo, đun...) thì ke_hoach PHẢI có đủ các bước đó, không được chỉ trả lời bằng lời. " +
            "Câu ngắn như 'tôi', 'cho tôi', 'ok', 'có', 'làm đi' là trả lời câu robot vừa hỏi trong HỘI THOẠI GẦN ĐÂY: làm luôn yêu cầu " +
            "trước đó (ví dụ 'lấy KI cho' rồi 'tôi' = lay_hoa_chat KI rồi dat ban_nguoi_dung). Yêu cầu lấy hóa chất mà chưa rõ cho ai thì " +
            "mặc định mang ra bàn người dùng, không hỏi lại.\n" +
            "- Robot tự đi lên / xuống tầng bằng thang bộ khi vật cần tới ở tầng khác (lay_hoa_chat từ tầng 2 vẫn được: robot xuống " +
            "phòng hóa chất tầng 1 rồi mang lên).\n" +
            "- Khi người dùng ở TẦNG 2 (lab sinh, mã vật dạng binh_sinh_, sinh_pham_, dung_cu_sinh_, mau_sinh_, hop_cap_, num_, nut_, nap_, " +
            "thiet_bi_sinh_): chỉ dùng di_toi, chi_vao, cam, dat, tra_ve, doi, noi, quan_sat, bat_tat, dung_dung_cu, lay_dung_cu, chinh_num. " +
            "Các kỹ năng hóa học (rot, nho_giot, do_ph, dun_nong...) KHÔNG dùng được cho bình sinh học. Lab sinh là lab tự do, không có " +
            "thí nghiệm mẫu: hãy ghép từng thao tác như người làm thật, đúng quy trình sinh học (ví dụ thử enzim catalase: lay_dung_cu hộp cấp " +
            "ống nghiệm, dat xuống bàn, dung_dung_cu kẹp lên khay gan, dung_dung_cu kẹp lên 'vat_vua_lay' để thả mô, cam 'vat_vua_lay', " +
            "dung_dung_cu 'vat_dang_cam' lên chai H2O2 để lấy H2O2, doi, quan_sat). Vật đích tự kiểm tra quy trình và báo lỗi nếu làm sai " +
            "(ví dụ bóc biểu bì hành khi chưa rạch): gặp lỗi thì robot dừng và nói lý do.\n" +
            $"- Câu hỏi chỉ cần trả lời thì ke_hoach rỗng. Tối đa {MaxSteps} bước.");

        public const string SystemAnnotate = "Bạn là trợ giảng hóa học THPT Việt Nam. Phòng thí nghiệm ảo vừa ghi nhận một hiện tượng; dữ kiện (tên hiện tượng, " +
            "phương trình, thành phần dung dịch, pH, nhiệt độ) do bộ máy mô phỏng tính, coi là đúng. " +
            "Viết 1-2 câu tiếng Việt (tối đa 45 từ) giải thích BẢN CHẤT vì sao có hiện tượng này trong đúng bình đó, " +
            "theo kiến thức sách giáo khoa; thêm một lưu ý an toàn ngắn nếu chất độc, ăn mòn hoặc dễ cháy. " +
            "Không lặp lại nguyên văn tiêu đề, không bịa thêm phản ứng, không dùng định dạng markdown.";

        public const string AiOffByTeacher = "Giáo viên đang tắt trợ lý AI nên mình chỉ hiểu lệnh cơ bản.";

        // ------------------------------------------------------------------
        // Tin nhắn gửi AI
        // ------------------------------------------------------------------

        public static string FormatScene(SceneSnapshot s)
        {
            var lines = new List<string>();
            if (s == null) return "";
            if (!string.IsNullOrEmpty(s.khu_vuc)) lines.Add($"Người dùng đang ở: {s.khu_vuc}.");
            if (!string.IsNullOrEmpty(s.nguoi_dang_cam)) lines.Add($"Người dùng đang cầm: {s.nguoi_dang_cam}.");
            if (!string.IsNullOrEmpty(s.robot_dang_cam)) lines.Add($"Robot đang cầm: {s.robot_dang_cam}.");
            if (!string.IsNullOrEmpty(s.che_do)) lines.Add($"Chế độ: {s.che_do}.");
            lines.Add("Các vật gần người dùng (mã | tên | loại | cách người dùng | đang chứa):");
            if (s.vat != null)
                foreach (var v in s.vat.Where(v => v != null && v.id != null).Take(MaxObjects))
                    lines.Add($"- {v.id} | {v.ten ?? ""} | {v.loai ?? ""} | {v.khoang_cach.ToString("0.0", CultureInfo.InvariantCulture)} m | {v.chua ?? ""}");
            if (s.su_kien != null && s.su_kien.Length > 0) lines.Add("Hiện tượng gần đây: " + string.Join("; ", s.su_kien.Skip(Math.Max(0, s.su_kien.Length - 5))));
            return string.Join("\n", lines);
        }

        public static string PlanUserMessage(string question, SceneSnapshot scene, string[] history)
        {
            string sop = MachineContext(question);
            var h = history ?? Array.Empty<string>();
            return $"ẢNH CHỤP CẢNH:\n{FormatScene(scene)}\n\n" + (sop.Length > 0 ? $"QUY TRÌNH MÁY (tài liệu của phòng lab):\n{sop}\n\n" : "") +
                   $"HỘI THOẠI GẦN ĐÂY:\n{string.Join("\n", h.Skip(Math.Max(0, h.Length - 8)))}\n\nHỌC SINH: {question}";
        }

        public static string AnnotateUserMessage(AiClient.AnnotateRequest r)
        {
            var fact = new JObject
            {
                ["tieu_de"] = r.tieu_de ?? "", ["phuong_trinh"] = r.phuong_trinh ?? "", ["chi_tiet"] = r.chi_tiet ?? "",
                ["muc_do"] = string.IsNullOrEmpty(r.muc_do) ? "Info" : r.muc_do, ["loai"] = r.loai ?? "",
                ["thanh_phan"] = new JArray(r.thanh_phan ?? Array.Empty<string>()), ["ph"] = r.ph, ["nhiet_do"] = r.nhiet_do, ["ngu_canh"] = r.ngu_canh ?? "",
            };
            return "DỮ KIỆN: " + fact.ToString(Newtonsoft.Json.Formatting.None);
        }

        // ------------------------------------------------------------------
        // Kiểm tra kế hoạch (như validate() trên máy chủ)
        // ------------------------------------------------------------------

        public static string FindTemplate(string name)
        {
            string q = FoldK(name).Replace(" ", "_");
            foreach (var t in Templates) if (t.key == q) return q;
            var words = FoldK(name).Replace("_", " ").Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1).ToList();
            string best = null;
            int bestHits = 0;
            foreach (var t in Templates)
            {
                string hay = FoldK(t.title) + " " + t.key.Replace("_", " ");
                int hits = words.Count(w => hay.Contains(w));
                if (hits > bestHits) { best = t.key; bestHits = hits; }
            }
            return words.Count > 0 && bestHits >= Math.Max(1, (words.Count + 1) / 2) ? best : null;
        }

        static string FoldK(string s) => StripMarks((s ?? "").Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormKD)).ToLowerInvariant().Trim();

        static float Num(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return 0;
            if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) { float f = t.Value<float>(); return float.IsNaN(f) ? 0 : f; }
            return float.TryParse(t.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && !float.IsNaN(v) ? v : 0;
        }

        static string Str(JToken t) => t == null || t.Type == JTokenType.Null ? "" : t.Type == JTokenType.Boolean ? (t.Value<bool>() ? "true" : "") : t.ToString();

        public static List<RobotStep> Validate(JArray plan, HashSet<string> objectIds, Func<string, ChemEntry> findChemical, List<string> dropped)
        {
            var output = new List<RobotStep>();
            var fetched = new Dictionary<string, int>();
            bool Known(string v) => objectIds.Contains(v) || SpecialIds.Contains(v) || v.StartsWith("hoa_chat:");
            int count = plan?.Count ?? 0;
            for (int i = 0; i < Math.Min(count, MaxSteps); i++)
            {
                var s = plan[i] as JObject ?? new JObject();
                string k = Str(s["ky_nang"]);
                if (!SkillSet.Contains(k)) { dropped.Add($"bước {i + 1}: kỹ năng lạ '{k}'"); continue; }
                string loi = Str(s["loi"]);
                var step = new RobotStep { ky_nang = k, doi_tuong = Str(s["doi_tuong"]), dich = Str(s["dich"]), so_luong = Num(s["so_luong"]), stt = 0, loi = loi.Length > 300 ? loi.Substring(0, 300) : loi };
                string hoaChat = Str(s["hoa_chat"]);
                if (k == "thi_nghiem_mau")
                {
                    string key = FindTemplate(hoaChat.Length > 0 ? hoaChat : step.doi_tuong);
                    if (key == null) { dropped.Add($"bước {i + 1}: không có thí nghiệm mẫu '{(hoaChat.Length > 0 ? hoaChat : step.doi_tuong)}'"); continue; }
                    step.doi_tuong = key; step.dich = ""; step.so_luong = 0;
                    output.Add(step);
                    continue;
                }
                if (k == "lay_hoa_chat")
                {
                    var c = findChemical(hoaChat);
                    if (c == null) { dropped.Add($"bước {i + 1}: không tìm thấy hóa chất '{hoaChat}'"); continue; }
                    step.stt = c.stt;
                    step.doi_tuong = $"hoa_chat:{c.stt}";
                    fetched[hoaChat.Trim().ToLowerInvariant()] = c.stt;
                    fetched[c.stt.ToString()] = c.stt;
                }
                string missing = "";
                for (int f = 0; f < 2; f++)
                {
                    string v = f == 0 ? step.doi_tuong : step.dich;
                    if (!v.StartsWith("hoa_chat:")) continue;
                    string key = v.Substring(9).Trim().ToLowerInvariant();
                    string nv;
                    if (fetched.TryGetValue(key, out int st)) nv = $"hoa_chat:{st}";
                    else if (Regex.IsMatch(key, @"^\d+$")) nv = $"hoa_chat:{key}";
                    else
                    {
                        var c = findChemical(key);
                        nv = c != null ? $"hoa_chat:{c.stt}" : "";
                        if (missing.Length == 0 && c == null) missing = key;
                    }
                    if (f == 0) step.doi_tuong = nv; else step.dich = nv;
                }
                if (missing.Length > 0) { dropped.Add($"bước {i + 1}: hóa chất '{missing}' không có trong danh mục"); continue; }
                if (k == "tia_nuoc" && step.dich.Length == 0 && Known(step.doi_tuong)) step.dich = step.doi_tuong;
                if (k == "dung_dung_cu" && step.doi_tuong.Length == 0) step.doi_tuong = "vat_dang_cam";
                if (k == "dung_dung_cu" && !Known(step.doi_tuong)) { dropped.Add($"bước {i + 1}: dụng cụ '{step.doi_tuong}' không có trong cảnh"); continue; }
                if (NeedsObject.Contains(k) && !Known(step.doi_tuong)) { dropped.Add($"bước {i + 1}: mã vật '{step.doi_tuong}' không có trong cảnh"); continue; }
                if (NeedsDest.Contains(k) && !Known(step.dich)) { dropped.Add($"bước {i + 1}: nơi nhận '{step.dich}' không có trong cảnh"); continue; }
                if (OptionalDest.Contains(k) && step.dich.Length > 0 && !objectIds.Contains(step.dich)) step.dich = "";
                if (Limits.TryGetValue(k, out var lim))
                    step.so_luong = Math.Max(0, Math.Min(step.so_luong != 0 ? step.so_luong : lim.md, lim.lim));
                if (k == "dat" && !(objectIds.Contains(step.dich) || step.dich == "ban_nguoi_dung" || step.dich == "")) step.dich = "ban_nguoi_dung";
                output.Add(step);
            }
            if (count > MaxSteps) dropped.Add($"bỏ {count - MaxSteps} bước sau bước {MaxSteps}");
            return output;
        }

        /// <summary>Bỏ từ bước đầu tiên đụng tới hóa chất giáo viên đang khóa trở đi (như boBuocHoaChatKhoa trên máy chủ).</summary>
        public static List<RobotStep> CutLocked(List<RobotStep> steps, Func<int, bool> locked, List<string> dropped, out string note)
        {
            note = "";
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                var used = new List<int> { s.stt };
                foreach (var v in new[] { s.doi_tuong, s.dich })
                {
                    var m = Regex.Match(v ?? "", @"^hoa_chat:(\d+)$");
                    if (m.Success) used.Add(int.Parse(m.Groups[1].Value));
                }
                foreach (int x in used)
                    if (x > 0 && locked(x))
                    {
                        string ten = ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(x)?.ten : null;
                        note = $"bước {i + 1}: {ten ?? "hóa chất"} đang bị giáo viên khóa, dừng kế hoạch tại đây";
                        dropped.Add(note);
                        return steps.GetRange(0, i);
                    }
            }
            return steps;
        }

        // ------------------------------------------------------------------
        // Tra hóa chất theo tên / công thức (như findChemical trên máy chủ)
        // ------------------------------------------------------------------

        const string SubDigits = "₀₁₂₃₄₅₆₇₈₉";

        static string SubToDigits(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) { int i = SubDigits.IndexOf(ch); sb.Append(i >= 0 ? (char)('0' + i) : ch); }
            return sb.ToString();
        }

        static string StripMarks(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }

        public static string FoldHoa(string s) => SubToDigits(StripMarks((s ?? "").Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD)).ToLowerInvariant()).Trim();

        static readonly Dictionary<string, string> EnVi = new Dictionary<string, string>
        {
            ["acid"] = "axit", ["sodium"] = "natri", ["potassium"] = "kali", ["copper"] = "dong", ["iron"] = "sat", ["silver"] = "bac", ["barium"] = "bari",
            ["calcium"] = "canxi", ["magnesium"] = "magie", ["zinc"] = "kem", ["aluminium"] = "nhom", ["aluminum"] = "nhom", ["ammonium"] = "amoni",
            ["lead"] = "chi", ["manganese"] = "mangan", ["chromium"] = "crom", ["nickel"] = "niken", ["cobalt"] = "coban", ["lithium"] = "liti",
            ["hydroxide"] = "hidroxit", ["chloride"] = "clorua", ["sulfate"] = "sunfat", ["sulphate"] = "sunfat", ["sulfite"] = "sunfit",
            ["sulfide"] = "sunfua", ["nitrate"] = "nitrat", ["nitrite"] = "nitrit", ["carbonate"] = "cacbonat", ["bicarbonate"] = "hidrocacbonat",
            ["permanganate"] = "pemanganat", ["dichromate"] = "dicromat", ["chromate"] = "cromat", ["oxide"] = "oxit", ["peroxide"] = "peoxit",
            ["iodide"] = "iodua", ["bromide"] = "bromua", ["fluoride"] = "florua", ["phosphate"] = "photphat", ["acetate"] = "axetat",
            ["oxalate"] = "oxalat", ["thiocyanate"] = "thioxianat", ["thiosulfate"] = "thiosunfat", ["hydrogen"] = "hidro", ["water"] = "nuoc",
            ["sulfuric"] = "sunfuric", ["sulphuric"] = "sunfuric", ["hydrochloric"] = "clohidric", ["phosphoric"] = "photphoric",
            ["acetic"] = "axetic", ["formic"] = "fomic", ["carbonic"] = "cacbonic", ["glucose"] = "glucozo", ["fructose"] = "fructozo",
            ["sucrose"] = "saccarozo", ["starch"] = "tinh bot", ["ethanol"] = "etanol", ["methanol"] = "metanol", ["glycerol"] = "glixerol",
            ["phenolphthalein"] = "phenolphtalein", ["ammonia"] = "amoniac", ["iodine"] = "iot", ["chlorine"] = "clo", ["bromine"] = "brom",
            ["litmus"] = "quy", ["indicator"] = "chi thi", ["hidroxyt"] = "hidroxit", ["hydroxit"] = "hidroxit", ["sulfat"] = "sunfat",
            ["clohydric"] = "clohidric",
        };
        static readonly HashSet<string> Stop = new HashSet<string> { "dung", "dich", "dd", "solution", "loang", "dilute", "aqueous", "lo", "chai", "ong", "cua", "va", "the" };

        static List<string> Words(string text)
        {
            var o = new List<string>();
            foreach (Match m in Regex.Matches(FoldHoa(text), "[a-z]+"))
                o.AddRange((EnVi.TryGetValue(m.Value, out var vi) ? vi : m.Value).Split(' '));
            return o.Where(w => !Stop.Contains(w)).ToList();
        }

        /// <summary>Khóa công thức theo thứ tự Hill (C, H, rồi theo chữ cái); chuỗi không phải công thức thì rỗng.</summary>
        public static string FormulaKey(string f)
        {
            f = SubToDigits(f ?? "").Split('.', '·', '*')[0].Trim();
            f = Regex.Replace(f, @"\^?\d*[+\-]+$", "");
            if (!Regex.IsMatch(f.Length > 0 ? f : "-", @"^(?:[A-Z][a-z]?\d*|[()\[\]]\d*)+$")) return "";
            var stack = new List<Dictionary<string, int>> { new Dictionary<string, int>() };
            foreach (Match m in Regex.Matches(f, @"([A-Z][a-z]?)(\d*)|([()\[\]])(\d*)"))
            {
                var top = stack[stack.Count - 1];
                if (m.Groups[1].Success && m.Groups[1].Length > 0)
                {
                    string el = m.Groups[1].Value;
                    int n = m.Groups[2].Length > 0 ? int.Parse(m.Groups[2].Value) : 1;
                    top[el] = (top.TryGetValue(el, out int v) ? v : 0) + n;
                }
                else if (m.Groups[3].Value == "(" || m.Groups[3].Value == "[") stack.Add(new Dictionary<string, int>());
                else if (stack.Count > 1)
                {
                    var inner = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    int mul = m.Groups[4].Length > 0 ? int.Parse(m.Groups[4].Value) : 1;
                    var outer = stack[stack.Count - 1];
                    foreach (var kv in inner) outer[kv.Key] = (outer.TryGetValue(kv.Key, out int v) ? v : 0) + kv.Value * mul;
                }
            }
            var counts = stack[0];
            var order = counts.Keys.OrderBy(e => e != "C").ThenBy(e => e != "H").ThenBy(e => e, StringComparer.Ordinal);
            return string.Concat(order.Select(e => e + (counts[e] > 1 ? counts[e].ToString() : "")));
        }

        sealed class ChemKey { public ChemEntry c; public string name, formula; public HashSet<string> keys, words; }
        static List<ChemKey> chemKeys;
        static ChemEntry[] chemKeysFrom;

        static List<ChemKey> ChemKeys(ChemEntry[] all)
        {
            if (chemKeys != null && chemKeysFrom == all) return chemKeys;
            chemKeysFrom = all;
            chemKeys = all.Select(c =>
            {
                string name = !string.IsNullOrEmpty(c.ten_khong_dau) ? c.ten_khong_dau : FoldHoa(c.ten);
                var keys = new HashSet<string> { FormulaKey(c.cong_thuc ?? "") };
                foreach (Match m in Regex.Matches(c.ten ?? "", @"[A-Z][A-Za-z0-9()]*")) keys.Add(FormulaKey(m.Value));
                return new ChemKey { c = c, name = name, formula = (c.cong_thuc ?? "").ToLowerInvariant(), keys = keys, words = new HashSet<string>(Words(name)) };
            }).ToList();
            return chemKeys;
        }

        public static ChemEntry FindChemical(string query) => ChemicalCatalog.Instance != null ? FindChemical(query, ChemicalCatalog.Instance.Chemicals) : null;

        public static ChemEntry FindChemical(string query, ChemEntry[] all)
        {
            if (all == null) return null;
            string q = query ?? "";
            // "dung dịch KI", "lọ NaOH": phần còn lại đúng một công thức thì lấy chất đó (trước đây khớp nhầm "Dung dịch iot (I2/KI)")
            string lead = Regex.Replace(q, @"(?i)^\s*(dung d[iị]ch|dd|l[oọ]|chai|b[iì]nh)\s+", "").Trim();
            ChemEntry hit;
            if (lead != q.Trim() && (hit = FormulaOnly(lead, all)) != null) return hit;
            hit = FindChemicalExact(query, all);
            if (hit != null) return hit;
            // AI hay ghi "kali iodua (KI)", "kali iotua", "đồng sunphat": thử công thức trong ngoặc / từ viết hoa, bỏ chữ "dung dịch",
            // rồi tên gần đúng (sai 1-2 chữ cái, "ph" = "f", bỏ hóa trị "(II)")
            foreach (Match m in Regex.Matches(q, @"\(([^)]+)\)"))
                if ((hit = FindChemicalExact(m.Groups[1].Value, all)) != null) return hit;
            string bare = Regex.Replace(q, @"\([^)]*\)", " ");
            bare = Regex.Replace(bare, @"(?i)\b(dung d[iị]ch|dd|l[oọ]|chai|b[iì]nh|m[oộ]t [ií]t)\b", " ").Trim();
            if (bare.Length > 0 && bare != q && (hit = FindChemicalExact(bare, all)) != null) return hit;
            foreach (Match m in Regex.Matches(q, @"\b[A-Z][A-Za-z0-9()]{0,11}\b"))
                if ((hit = FormulaOnly(m.Value, all)) != null) return hit;
            string qf0 = Loose(bare.Length > 0 ? bare : q);
            if (qf0.Length < 5) return null;
            ChemEntry best = null;
            int bestD = 3;
            foreach (var c in all)
            {
                if (c == null || string.IsNullOrEmpty(c.ten)) continue;
                int d = Lev(Loose(c.ten), qf0, bestD);
                if (d < bestD || (d == bestD && best != null && c.trang_thai == "dd" && best.trang_thai != "dd")) { best = c; bestD = d; }
            }
            return bestD <= 2 ? best : null;
        }

        static string Loose(string s) =>
            Regex.Replace(Regex.Replace(FoldHoa(s), @"\((i|ii|iii|iv|v|vi|vii)\)", ""), @"\s+", " ").Replace("ph", "f").Trim();

        /// <summary>Chất có công thức đúng bằng chuỗi (ưu tiên dung dịch), null nếu chuỗi không phải công thức nào.</summary>
        static ChemEntry FormulaOnly(string f, ChemEntry[] all)
        {
            string ff = FoldHoa(f);
            if (ff.Length == 0 || !f.Any(char.IsUpper)) return null;
            ChemEntry best = null;
            foreach (var c in all)
                if (c != null && !string.IsNullOrEmpty(c.cong_thuc) && FoldHoa(c.cong_thuc) == ff &&
                    (best == null || (c.trang_thai == "dd" && best.trang_thai != "dd")))
                    best = c;
            return best;
        }

        static int Lev(string a, string b, int cap)
        {
            if (Math.Abs(a.Length - b.Length) >= cap) return cap;
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                int rowMin = cur[0];
                for (int j = 1; j <= b.Length; j++)
                {
                    cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                    rowMin = Math.Min(rowMin, cur[j]);
                }
                if (rowMin >= cap) return cap;
                var t = prev; prev = cur; cur = t;
            }
            return Math.Min(prev[b.Length], cap);
        }

        static ChemEntry FindChemicalExact(string query, ChemEntry[] all)
        {
            string qf = FoldHoa(query);
            if (qf.Length == 0 || all == null) return null;
            string qkey = FormulaKey((query ?? "").Trim());
            var qwords = new HashSet<string>(Words(query));
            bool wantSolid = Regex.Matches(qf, "[a-z]+").Cast<Match>().Any(m => m.Value == "ran" || m.Value == "tinh" || m.Value == "bot" || m.Value == "solid" || m.Value == "crystal" || m.Value == "powder");
            ChemEntry best = null;
            double bestScore = 0;
            foreach (var k in ChemKeys(all))
            {
                double score = 0;
                bool sameWords = qwords.Count > 0 && qwords.Count == k.words.Count && qwords.All(k.words.Contains);
                if (k.formula.Length > 0 && k.formula == qf) score = 900;
                else if (qkey.Length > 2 && k.keys.Contains(qkey)) score = 880;
                else if (k.name == qf) score = 850;
                else if (sameWords) score = 820;
                else if (k.name.StartsWith(qf, StringComparison.Ordinal)) score = 700;
                else if (qwords.Count > 0 && qwords.All(k.words.Contains)) score = 600;
                else if (k.name.Contains(qf) || (k.formula.Length > 0 && k.formula.Contains(qf) && qf.Length >= 3)) score = 400;
                bool prefer = wantSolid ? k.c.trang_thai == "ran" : k.c.trang_thai == "dd";
                score -= (prefer ? 0 : 3) + k.name.Length / 100.0 + k.c.stt / 10000.0;
                if (score > bestScore) { best = k.c; bestScore = score; }
            }
            return bestScore > 0 ? best : null;
        }

        // ------------------------------------------------------------------
        // Quy trình máy, dụng cụ sinh, chặn sinh học nguy hiểm (như ngCanhMay trên máy chủ)
        // ------------------------------------------------------------------

        static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            ["uv_vis"] = new[] { "uv-vis", "uv vis", "uvvis", "quang pho hap thu phan tu", "quang pho kha kien", "tu ngoai kha kien", "lambert", "beer" },
            ["aas"] = new[] { "aas", "hap thu nguyen tu", "ngon lua axetilen" },
            ["ftir"] = new[] { "ftir", "hong ngoai", "atr", "giao thoa" },
            ["gc_ms"] = new[] { "gc-ms", "gc ms", "gcms", "sac ky khi", "khoi pho" },
            ["hplc"] = new[] { "hplc", "sac ky long" },
            ["chuan_do_dien_the"] = new[] { "chuan do dien the", "chuan do tu dong", "dien cuc ph", "titrator" },
            ["xrd"] = new[] { "xrd", "nhieu xa tia x", "nhieu xa" },
            ["sem"] = new[] { "sem", "hien vi dien tu", "eds" },
            ["nmr"] = new[] { "nmr", "cong huong tu" },
            ["nhiet_luong"] = new[] { "bom nhiet luong", "nhiet luong", "nhiet dot chay" },
            ["tu_say"] = new[] { "tu say", "do am", "chat kho" },
            ["lo_nung"] = new[] { "lo nung", "tro tong so", "nung voi" },
            ["may_cat_nuoc"] = new[] { "may cat nuoc", "nuoc cat", "do dan dien" },
            ["noi_hap"] = new[] { "noi hap", "tiet trung", "autoclave", "noi hap tiet trung", "hap tiet trung" },
            ["kinh_hien_vi"] = new[] { "kinh hien vi", "microscope", "vat kinh", "thi kinh", "tieu ban", "soi tieu ban", "do phong dai", "soi kinh", "oc vi cap", "oc so cap" },
            ["tu_an_toan_sinh_hoc"] = new[] { "tu an toan sinh hoc", "tu thao tac vo trung", "biosafety cabinet", "bsc", "tu bsc", "dong khi laminar", "tu vo trung", "loc hepa" },
            ["may_pcr"] = new[] { "may pcr", "luan nhiet", "thermal cycler", "khuech dai gen", "chu ky nhiet", "pcr", "bien tinh", "gan moi" },
            ["be_dien_di"] = new[] { "dien di", "be dien di", "dien di gel", "electrophoresis", "agarose", "ban gel", "tae" },
            ["tu_am_vi_sinh"] = new[] { "tu am", "tu am vi sinh", "incubator", "u vi khuan", "u vi sinh", "dia petri", "37 do" },
            ["gel_doc"] = new[] { "gel doc", "chup gel", "buong chup gel", "buong toi uv", "gel documentation", "anh gel", "soi gel", "doc gel", "transilluminator" },
            ["may_do_od600"] = new[] { "od600", "od 600", "do quang", "mat do quang", "may do od600", "may quang pho od600", "so mau vi sinh", "spectrophotometer 600", "duong cong sinh truong" },
            ["micropipet_p200"] = new[] { "micropipet", "micropipette", "pipet tu dong", "hut mau", "nac 1", "nac 2", "p200" },
            ["micropipet_p10"] = new[] { "micropipet p10", "pipet p10", "p10" },
            ["micropipet_p1000"] = new[] { "micropipet p1000", "pipet p1000", "p1000" },
            ["hop_dau_con"] = new[] { "dau con", "hop dau con", "tip pipet", "thay dau con" },
            ["que_cay_vong"] = new[] { "que cay", "que cay vong", "cay ria", "dot que cay", "vong cay", "inoculating loop" },
            ["que_cay_thang"] = new[] { "que cay thang", "cay dam", "stab" },
            ["que_trang"] = new[] { "que trang", "trang dia", "cell spreader" },
            ["dao_nhuom_gram"] = new[] { "nhuom gram", "dao nhuom gram", "gram stain", "tim ket tinh", "crystal violet", "safranin", "tay con", "gram duong", "gram am" },
            ["may_ly_tam_de_ban"] = new[] { "ly tam", "may ly tam", "centrifuge", "rcf", "can bang rotor", "ong doi dien", "ly tam de ban" },
            ["may_ly_tam_mini"] = new[] { "ly tam mini", "microcentrifuge", "spin down" },
            ["may_vortex"] = new[] { "vortex", "may vortex", "may lac vortex", "lac vortex", "tron vortex" },
            ["may_dem_khuan_lac"] = new[] { "dem khuan lac", "may dem khuan lac", "colony counter", "cfu" },
            ["coi_chay_su"] = new[] { "coi chay", "coi chay su", "tach chiet adn", "nghien mau", "ket tua adn" },
            ["tram_potometer"] = new[] { "potometer", "tram potometer", "thoat hoi nuoc", "do thoat hoi nuoc", "bot khi mao quan" },
            ["tram_quang_hop"] = new[] { "tram quang hop", "rong duoi cho", "elodea", "dem bot khi quang hop", "que dom" },
            ["hop_huong_sang"] = new[] { "huong sang", "hop huong sang", "phototropism", "moc vong" },
            ["gian_thuy_canh_nft"] = new[] { "thuy canh", "gian thuy canh", "nft" },
            ["binh_len_men"] = new[] { "binh len men", "len men ruou", "bong bay co2", "nam men" },
            ["bo_lam_sua_chua"] = new[] { "lam sua chua", "len men lactic", "hu lam sua chua" },
            ["ban_giai_phau_so"] = new[] { "ban giai phau", "ban giai phau so", "giai phau 3d" },
            ["tu_thao_tac_pcr"] = new[] { "tu thao tac pcr", "tu pcr", "pcr hood" },
            ["bo_xu_ly_tran"] = new[] { "bo xu ly tran", "tran do sinh hoc", "spill kit", "vo dia petri" },
            ["lo_dau_soi"] = new[] { "dau soi", "dau soi kinh", "vat kinh 100x", "vat kinh dau" },
        };

        static readonly string[] UnsafeBio = { "bsl 2", "bsl 3", "bsl 4", "bsl2", "bsl3", "bsl4", "cap an toan 2", "cap an toan 3", "cap an toan 4",
            "tang doc luc", "tang cuong doc luc", "bien doi doc luc", "doc to sinh hoc", "vu khi sinh hoc", "tao mam benh",
            "tao virus", "nuoi virus", "cay virus", "khang khang sinh", "tao chung khang thuoc",
            "tac nhan gay benh", "vi khuan gay benh", "virus gay benh", "nam gay benh",
            "nuoi cay mam benh", "phan lap tu benh pham", "phan lap tu mau", "phan lap tu dom", "phan lap tu mu",
            "bacillus anthracis", "vi khuan than", "benh than", "anthrax", "vibrio cholerae", "vi khuan ta", "phay khuan ta",
            "mycobacterium tuberculosis", "vi khuan lao", "truc khuan lao", "yersinia pestis", "dich hach", "salmonella", "thuong han",
            "staphylococcus aureus", "tu cau vang", "mrsa", "clostridium botulinum", "doc to botulinum", "clostridium tetani", "uon van",
            "shigella", "truc khuan ly", "klebsiella pneumoniae", "pseudomonas aeruginosa",
            "ebola", "marburg", "sars cov", "mers cov", "h5n1", "cum gia cam", "virus dai", "dau mua" };

        const string Bsl1Refusal = "Phòng thí nghiệm trường học chỉ thực hiện các thí nghiệm an toàn sinh học cấp 1 (BSL-1); " +
            "mình không thể hướng dẫn nuôi cấy, phân lập từ bệnh phẩm, tạo mới hay làm tăng độc lực các tác nhân gây bệnh nguy hiểm (BSL-2/3/4). " +
            "Bạn có thể thực hành kỹ thuật vi sinh và sinh học phân tử tương đương trên các chủng dạy học an toàn BSL-1 như: " +
            "cấy ria hoặc đo OD600 với E. coli K-12, nhuộm Gram vi khuẩn sữa chua (Lactobacillus bulgaricus, Streptococcus thermophilus), " +
            "quan sát Bacillus subtilis hoặc lên men nấm men bánh mì (Saccharomyces cerevisiae).";
        const string Bsl1Context = "AN TOÀN SINH HỌC (BẮT BUỘC - CHỈ BSL-1):\n" + Bsl1Refusal + "\n" +
            "Hãy từ chối nhẹ nhàng yêu cầu nguy hiểm này và gợi ý học sinh thực hiện các bài thí nghiệm BSL-1 an toàn ở trên.";

        public static string Strip(string s)
        {
            s = StripMarks((s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD).Replace('đ', 'd'));
            return Regex.Replace(s, "[^a-z0-9]+", " ").Trim();
        }

        static Dictionary<string, JObject> machines;
        static List<JObject> bioTools;

        /// <summary>Thư mục dữ liệu; bài thử có thể trỏ chỗ khác.</summary>
        public static string DataRoot = null;
        static string Data => DataRoot ?? Path.Combine(Application.streamingAssetsPath, "data");

        static void LoadDocs()
        {
            if (machines != null) return;
            machines = new Dictionary<string, JObject>();
            bioTools = new List<JObject>();
            try
            {
                string dir = Path.Combine(Data, "may");
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.json"))
                    {
                        var d = JObject.Parse(File.ReadAllText(f));
                        if (d["quy_trinh"] is JArray && d["ma"] != null) machines[d["ma"].ToString()] = d;
                    }
                string tools = Path.Combine(Data, "sinh", "dung_cu.json");
                if (File.Exists(tools) && JObject.Parse(File.ReadAllText(tools))["dung_cu"] is JArray arr)
                    foreach (var t in arr.OfType<JObject>()) bioTools.Add(t);
            }
            catch (Exception e) { Debug.LogWarning("[khoa_ai] không đọc được tài liệu máy: " + e.Message); }
        }

        public static int MachineCount { get { LoadDocs(); return machines.Count; } }

        static IEnumerable<string> StrList(JToken t) => t is JArray a ? a.Select(x => x.ToString()) : Enumerable.Empty<string>();

        static int BestHit(string t, IEnumerable<string> keys, int minLen = 1)
        {
            int best = 0;
            foreach (var k in keys) if (k.Length >= minLen && k.Length > 0 && t.Contains(" " + k + " ")) best = Math.Max(best, k.Length);
            return best;
        }

        static string MachineSummary(JObject d)
        {
            string mau = Str(d["may_mau"]).Split('(')[0].Trim();
            var lines = new List<string> { $"MÁY: {Str(d["ten"])} (thông số theo {mau})", "Nguyên lý: " + string.Join(" ", StrList(d["nguyen_ly"]).Take(3)) };
            var steps = (d["quy_trinh"] as JArray ?? new JArray()).OfType<JObject>().Select((s, i) =>
                $"{i + 1}. {Str(s["ten"])}: {Str(s["viec"])}" + (Str(s["loi"]).Length > 0 ? $" (lỗi hay gặp: {Str(s["loi"])})" : ""));
            lines.Add("Quy trình:\n" + string.Join("\n", steps));
            lines.Add("An toàn: " + string.Join(" ", StrList(d["an_toan"])));
            return string.Join("\n", lines);
        }

        static string ToolSummary(JObject d)
        {
            var lines = new List<string> { $"DỤNG CỤ: {Str(d["ten"])} ({Str(d["phan_loai"])})" };
            if (Str(d["cong_dung"]).Length > 0) lines.Add($"Công dụng: {Str(d["cong_dung"])}");
            var use = StrList(d["cach_dung"]).Take(6).ToList();
            if (use.Count > 0) lines.Add("Cách dùng:\n" + string.Join("\n", use.Select(s => "- " + s)));
            var err = StrList(d["loi_thuong_gap"]).Take(4).ToList();
            if (err.Count > 0) lines.Add("Lỗi thường gặp: " + string.Join("; ", err));
            var safe = StrList(d["an_toan"]).Take(4).ToList();
            if (safe.Count > 0) lines.Add("An toàn: " + string.Join("; ", safe));
            string s2 = string.Join("\n", lines);
            return s2.Length > 1500 ? s2.Substring(0, 1500) : s2;
        }

        public static bool IsUnsafeBio(string text)
        {
            string t = " " + Strip(text) + " ";
            return UnsafeBio.Any(kw => t.Contains(" " + Strip(kw) + " "));
        }

        public static string MachineContext(string text)
        {
            LoadDocs();
            string t = " " + Strip(text) + " ";
            var parts = new List<string>();
            if (IsUnsafeBio(text)) parts.Add(Bsl1Context);
            var docs = new List<(int, string)>();
            foreach (var kv in Aliases)
            {
                if (!machines.TryGetValue(kv.Key, out var d)) continue;
                int best = BestHit(t, kv.Value.Select(Strip).Concat(new[] { Strip(Str(d["ten"])), kv.Key.Replace("_", " ") }));
                if (best > 0) docs.Add((best, kv.Key));
            }
            var topDocs = docs.OrderByDescending(h => h.Item1).ThenByDescending(h => h.Item2, StringComparer.Ordinal).Take(2).Select(h => machines[h.Item2]).ToList();
            var tools = new List<(int, JObject)>();
            foreach (var d in bioTools)
            {
                string ma = Str(d["ma"]);
                if (ma.Length == 0) continue;
                string may = Str(d["may_man_hinh"]);
                var words = (Aliases.TryGetValue(ma, out var a) ? a : Array.Empty<string>())
                    .Concat(may.Length > 0 && may != ma && Aliases.TryGetValue(may, out var b) ? b : Array.Empty<string>());
                var keys = words.Select(Strip).Concat(new[] { Strip(Str(d["ten"])), Strip(Str(d["ten_tieng_anh"])), ma.Replace("_", " ") });
                int best = BestHit(t, keys, 3);
                if (best > 0) tools.Add((best, d));
            }
            var topTools = tools.OrderByDescending(h => h.Item1).ThenByDescending(h => Str(h.Item2["ma"]), StringComparer.Ordinal).Take(2).Select(h => h.Item2).ToList();
            if (topDocs.Count > 0) parts.Add(string.Join("\n\n", topDocs.Select(MachineSummary)));
            if (topTools.Count > 0) parts.Add(string.Join("\n\n", topTools.Select(ToolSummary)));
            return string.Join("\n\n", parts);
        }
    }
}
