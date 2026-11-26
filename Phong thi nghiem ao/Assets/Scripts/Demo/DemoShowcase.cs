using System;
using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Bài demo mẫu cho ban giám khảo (phím F2 hoặc nút "Bài demo" ở góc trên trái):
    ///   • Bảng chọn: 7 bài ngắn (3–5 phút) trải đều Hóa học và Sinh học, mỗi bài ghi rõ khu, thời lượng và các bước.
    ///   • "Bắt đầu": đưa người dùng tới đúng trạm, hiện thẻ hướng dẫn từng bước ở góc trên trái (F3: bước tiếp,
    ///     Shift + F3: bước trước, F4: kết thúc). Thẻ không khóa điều khiển nên vừa đọc vừa thao tác được.
    ///   • "Robot làm mẫu": với bài Hóa có kịch bản sẵn (RobotDemos), robot trợ lý tự làm để giám khảo quan sát.
    ///   • Bản đi thi (26/11/2026): 3 bài nổi bật đứng đầu danh sách (phản ứng đổi màu glucozơ, trợ lý AI ra lệnh cho
    ///     robot bằng tiếng Việt, mổ ếch tự do) và nút "Hỏi trợ lý AI" (F5) ở mọi bài: gửi sẵn câu hỏi về đúng bài đó.
    /// Tự tạo khi cảnh phòng thí nghiệm nạp xong (cảnh có DesktopViewer), không cần gắn sẵn trong cảnh.
    /// </summary>
    public class DemoShowcase : MonoBehaviour
    {
        public class Demo
        {
            public string id, area, title, where, minutes, summary;
            public int station;
            public string[] steps;
            /// <summary>Khóa kịch bản trong RobotDemos (bài Hóa) để robot làm mẫu; null nếu không có.</summary>
            public string robotKey;
            /// <summary>Việc làm thêm sau khi tới trạm (ví dụ đứng trước cổng Cơ thể người 3D).</summary>
            public Action<DesktopViewer> onArrive;
            /// <summary>Câu gửi trợ lý AI khi bấm "Hỏi trợ lý AI" (F5); bài AI điều khiển robot thì là câu lệnh cho robot.</summary>
            public string aiQuestion;
        }

        public static readonly Demo[] Demos =
        {
            new Demo
            {
                id = "glucozo_doi_mau", area = "HÓA HỌC", title = "Phản ứng đổi màu: glucozơ + Cu(OH)₂", where = "Tầng 1 · Bàn định tính (bàn 4)", minutes = "4 phút", station = 2, robotKey = "glucozo_cu_oh2",
                summary = "Phản ứng đẹp nhất để trình diễn: kết tủa xanh Cu(OH)₂ tan thành phức xanh lam thẫm, đun nóng thì chuyển kết tủa đỏ gạch Cu₂O. Màu và lượng sản phẩm do bộ máy hóa học tính từ đúng lượng chất, không dựng sẵn.",
                steps = new[]
                {
                    "Bấm \"Robot làm mẫu\" (hoặc tự làm): robot tạo Cu(OH)₂ từ CuSO₄ và NaOH trong ống nghiệm, kết tủa xanh lam.",
                    "Robot thêm dung dịch glucozơ và lắc: kết tủa tan, dung dịch xanh lam thẫm (phức đồng – glucozơ).",
                    "Robot đun nóng ống nghiệm: xuất hiện kết tủa đỏ gạch Cu₂O. Nhìn vào ống để đọc thẻ thành phần và phương trình.",
                    "Bấm \"Hỏi trợ lý AI\" (F5): trợ lý giải thích vì sao đổi màu và ứng dụng nhận biết đường.",
                },
                aiQuestion = "Giải thích hiện tượng glucozơ với Cu(OH)₂: vì sao lúc đầu kết tủa tan thành dung dịch xanh lam, đun nóng lại ra kết tủa đỏ gạch? Viết phương trình và nêu ứng dụng thực tế.",
            },
            new Demo
            {
                id = "ai_robot", area = "TRỢ LÝ AI", title = "Trợ lý AI ra lệnh cho robot bằng tiếng Việt", where = "Tầng 1 · Bàn định tính (bàn 4)", minutes = "4 phút", station = 2, robotKey = "nhan_biet_fe3",
                summary = "Nói với robot như nói với bạn: AI hiểu yêu cầu, nhìn danh sách dụng cụ và hóa chất quanh bàn, lập kế hoạch từng bước rồi robot tự làm thật trong lab và giải thích hiện tượng.",
                steps = new[]
                {
                    "Bấm \"Hỏi trợ lý AI\" (F5): gửi sẵn lệnh \"làm thí nghiệm nhận biết ion Fe³⁺ bằng KSCN rồi giải thích\".",
                    "Quan sát robot đi lấy lọ FeCl₃, KSCN, rót, nhỏ giọt: dung dịch chuyển đỏ máu. Lời giải thích hiện trong khung trò chuyện.",
                    "Mời giám khảo tự gõ một yêu cầu (phím T), ví dụ: \"đo pH của dung dịch trong ống nghiệm\" hoặc \"dọn bàn giúp mình\".",
                    "Chưa có mạng / khóa AI: robot vẫn hiểu các lệnh thường dùng; bấm \"Robot làm mẫu\" để xem cùng thí nghiệm.",
                },
                aiQuestion = "Robot ơi, hãy làm thí nghiệm nhận biết ion Fe³⁺ bằng dung dịch KSCN cho mình xem, rồi giải thích hiện tượng và viết phương trình.",
            },
            new Demo
            {
                id = "mo_ech", area = "SINH HỌC", title = "Mổ ếch tự do trên bàn giải phẫu số", where = "Tầng 2 · Bàn giải phẫu số", minutes = "5 phút", station = 22,
                summary = "Bàn mổ tự do theo luật giải phẫu thật: chọn dụng cụ, rạch da, mở xoang bụng, quan sát tim, phổi, gan, ruột. Không có thao tác dựng sẵn: làm sai thứ tự thì bàn báo lỗi như ngoài đời.",
                steps = new[]
                {
                    "Bàn mổ ếch mở ra ngay khi tới trạm. Chọn kéo hoặc dao mổ ở khay dụng cụ.",
                    "Rạch da bụng theo đường giữa, rồi rạch lớp cơ để mở xoang bụng.",
                    "Quan sát và chạm vào từng cơ quan: tim 3 ngăn, phổi, gan, dạ dày, ruột để đọc thẻ kiến thức.",
                    "Bấm \"Hỏi trợ lý AI\" (F5) để trợ lý so sánh tim ếch với tim người. Có thể đi qua cổng \"Cơ thể người 3D\" bên cạnh để xem tiếp.",
                },
                aiQuestion = "Giải thích cấu tạo tim 3 ngăn của ếch, so sánh với tim 4 ngăn của người và ý nghĩa với đời sống lưỡng cư.",
                onArrive = viewer =>
                {
                    try { Bio.GiaiPhau.BanMoTuDoMoDun.Mo(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            },
            new Demo
            {
                id = "chuan_do", area = "HÓA HỌC", title = "Chuẩn độ axit – bazơ", where = "Tầng 1 · Bàn chuẩn độ (bàn 1)", minutes = "4 phút", station = 1, robotKey = "chuan_do", aiQuestion = "Giải thích vì sao chuẩn độ HCl bằng NaOH dùng phenolphtalein, điểm tương đương nằm ở pH bao nhiêu và các sai số thường gặp.",
                summary = "Xác định nồng độ HCl bằng NaOH chuẩn, chỉ thị phenolphtalein. Kết quả so với nồng độ thật và chấm sai số.",
                steps = new[]
                {
                    "Nhìn vào khóa buret, cuộn chuột (hoặc nút ▲ ▼ trên màn cảm ứng) để mở khóa cho NaOH nhỏ xuống bình nón.",
                    "Giữ chuột trái vào bình nón để lắc đều. Khi gần điểm tương đương, đóng bớt khóa để nhỏ từng giọt.",
                    "Dừng khi dung dịch chuyển hồng nhạt bền 30 giây. Bấm Enter (nút Ghi số đọc) để ghi thể tích.",
                    "Xem bảng kết quả: nồng độ tính được, sai số, và đồ thị pH theo thể tích NaOH (phím G).",
                }
            },
            new Demo
            {
                id = "nhan_biet_ion", area = "HÓA HỌC", title = "Nhận biết ion Cu²⁺ bằng NaOH", where = "Tầng 1 · Bàn định tính (bàn 4)", minutes = "3 phút", station = 2, robotKey = "nhan_biet_cu2", aiQuestion = "Giải thích phản ứng nhận biết ion Cu²⁺ bằng NaOH, màu kết tủa, phương trình ion rút gọn, và vì sao thêm HCl thì kết tủa tan.",
                summary = "Phản ứng tạo kết tủa xanh lam Cu(OH)₂. Bộ máy hóa học tính sản phẩm từ đúng lượng chất đã rót, không dựng sẵn.",
                steps = new[]
                {
                    "Cầm lọ CuSO₄ trên bàn, đưa tới miệng ống nghiệm rồi giữ chuột trái để rót khoảng 2 ml.",
                    "Cầm lọ NaOH, nhỏ từ từ vào ống nghiệm: xuất hiện kết tủa xanh lam Cu(OH)₂.",
                    "Nhìn vào ống nghiệm để đọc thẻ thông tin: thành phần, pH và phương trình phản ứng vừa xảy ra.",
                    "Thử thêm: nhỏ HCl vào, kết tủa tan trở lại thành dung dịch xanh.",
                }
            },
            new Demo
            {
                id = "may_phan_tich", area = "HÓA HỌC", title = "Đo phổ UV-Vis bằng bảng điều khiển máy", where = "Tầng 1 · Phòng máy phân tích hiện đại", minutes = "3 phút", station = 7, robotKey = "phan_tich_nuoc", aiQuestion = "Giải thích nguyên lý máy quang phổ UV-Vis, định luật Lambert-Beer và cách đọc λmax của dung dịch CuSO₄.",
                summary = "Bấm vào máy để mở bảng điều khiển: chỉnh thông số bằng thanh trượt rồi chạy. Phổ vẽ từ đúng thành phần mẫu.",
                steps = new[]
                {
                    "Cầm một ống nghiệm có dung dịch màu (ví dụ CuSO₄) đặt vào ổ mẫu của máy quang phổ UV-Vis.",
                    "Bấm vào máy: bảng điều khiển hiện ra giữa màn hình.",
                    "Kéo thanh trượt chọn khoảng bước sóng và độ rộng khe, rồi bấm \"Chạy phân tích\".",
                    "Đọc λmax và độ hấp thụ ở cột Kết quả. Bấm \"Màn hình vận hành chi tiết\" để xem sơ đồ bên trong máy.",
                }
            },
            new Demo
            {
                id = "kinh_hien_vi", aiQuestion = "Giải thích hiện tượng co nguyên sinh và phản co nguyên sinh ở tế bào biểu bì hành tím.", area = "SINH HỌC", title = "Soi tế bào hành và co nguyên sinh", where = "Tầng 2 · Bàn kính hiển vi (bàn 1)", minutes = "4 phút", station = 21,
                summary = "Kính hiển vi quang học với màn hình soi mới: chọn tiêu bản, đổi vật kính, lấy nét, nhuộm và đo kích thước tế bào.",
                steps = new[]
                {
                    "Bấm vào kính hiển vi trên bàn để mở màn hình soi.",
                    "Chọn tiêu bản \"Biểu bì vảy hành tím\". Bắt đầu ở vật kính 4×, kéo thanh lấy nét tới khi báo \"Ảnh rõ nét\".",
                    "Tăng lên 10× rồi 40×, lấy nét lại. Bật \"Thước đo\" để ước lượng kích thước tế bào.",
                    "Bấm \"Nhỏ NaCl 5 %\": quan sát màng sinh chất co lại, tách khỏi thành tế bào (co nguyên sinh).",
                    "Bấm \"Rửa bằng nước cất\" để tế bào phản co nguyên sinh, rồi \"Chụp ảnh\" để lưu.",
                }
            },
            new Demo
            {
                id = "pcr_dien_di", aiQuestion = "Giải thích 3 giai đoạn của PCR và vì sao nhiệt độ gắn mồi quá cao làm giảm sản phẩm.", area = "SINH HỌC", title = "PCR và điện di ADN", where = "Tầng 2 · Phòng sinh học phân tử", minutes = "5 phút", station = 28,
                summary = "Nhân bản đoạn ADN bằng máy PCR rồi kiểm tra bằng điện di gel. Băng ADN tính từ thành phần ống và thông số máy.",
                steps = new[]
                {
                    "Dùng micropipet hút hỗn hợp PCR vào ống 0,2 mL, cầm ống rồi bấm vào máy PCR để đặt vào khối nhiệt.",
                    "Bấm tay không vào máy PCR: bảng điều khiển hiện ra. Đặt nhiệt độ gắn mồi 55 °C, 30 chu kỳ, bấm \"Chạy chu trình\".",
                    "Thử đặt nhiệt độ gắn mồi 70 °C rồi chạy lại để thấy sản phẩm giảm (mồi không bám được khuôn).",
                    "Sang trạm \"Điện di và soi gel\": bấm vào bộ nguồn, đặt 100 V, bật nguồn, rồi soi gel ở buồng chụp.",
                }
            },
            new Demo
            {
                id = "tu_am_noi_hap", aiQuestion = "Vì sao hấp tiệt trùng cần 121 °C trong ít nhất 15 phút? Giải thích vai trò của áp suất.", area = "SINH HỌC", title = "Tủ ấm và nồi hấp tiệt trùng", where = "Tầng 2 · Tủ ấm và nồi hấp", minutes = "3 phút", station = 24,
                summary = "Hai máy có bảng điều khiển: thấy ngay vì sao 121 °C trong 15 phút mới đạt tiệt trùng.",
                steps = new[]
                {
                    "Bấm vào nồi hấp: đặt 110 °C, 10 phút rồi \"Chạy chu trình hấp\". Máy báo CHƯA đạt tiệt trùng.",
                    "Đặt lại 121 °C, 20 phút và chạy: băng chỉ thị chuyển sọc đen, mẻ hấp đạt. Xem áp suất dư tương ứng 103 kPa.",
                    "Bấm vào tủ ấm vi sinh: đặt 37 °C, 24 giờ, \"Bắt đầu ủ\" để tua nhanh thời gian nuôi khuẩn lạc.",
                }
            },
            new Demo
            {
                id = "co_the_nguoi", aiQuestion = "Giới thiệu ngắn gọn cấu tạo và chức năng của tim người cho học sinh lớp 11.", area = "SINH HỌC", title = "Cơ thể người 3D qua cổng liên kết", where = "Tầng 2 · Khu giải phẫu", minutes = "4 phút", station = 22,
                summary = "Bước qua cổng để vào phòng mô hình người thật (BodyParts3D): 3 lớp da, cơ, xương, 15 cơ quan và 6 cảnh hiển vi.",
                steps = new[]
                {
                    "Đi vào khung cổng phát sáng cạnh bàn giải phẫu rồi bấm \"Vào phòng\" (phím E).",
                    "Kéo để xoay quanh mô hình, cuộn (hoặc chụm hai ngón) để phóng to. Đổi lớp Da, Cơ, Xương ở thanh trên.",
                    "Chạm vào một cơ quan (ví dụ tim) để mở thẻ kiến thức và làm câu trắc nghiệm.",
                    "Bấm \"Soi hiển vi\" để xuống cấp tế bào, rồi \"Về phòng thí nghiệm\" để quay lại đúng chỗ cũ.",
                },
                onArrive = viewer =>
                {
                    var portal = FindAnyObjectByType<Bio.Portal.BioPortal>();
                    if (portal != null) viewer.GoNear(portal.transform.position + Vector3.up * 1.2f, portal.StandDirection, 2.0f);
                }
            },
        };

        public static DemoShowcase Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public Demo Active { get; private set; }
        public int Step { get; private set; }

        const float PanelW = 1320f, PanelH = 880f, RowH = 56f;

        DesktopViewer viewer;
        GameObject root, launcher, guide;
        RectTransform panel;
        CanvasGroup group;
        bool captured;
        int selected;
        Button[] rows;
        Text dArea, dTitle, dWhere, dSummary, dSteps;
        Button robotButton, aiButtonPanel, aiButtonGuide;
        Text gTitle, gStepNo, gStep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        static void TryCreate()
        {
            if (Instance != null) return;
            // bài kiểm thử tự động so ảnh từng điểm ảnh: không thêm nút mới lên màn hình
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("KHKT_TEST"))) return;
            var v = FindAnyObjectByType<DesktopViewer>();
            if (v == null) return;
            var go = new GameObject("Bài demo cho giám khảo");
            Instance = go.AddComponent<DemoShowcase>();
            Instance.viewer = v;
        }

        void OnDestroy()
        {
            if (captured) UiBridge.SetCapture(ref captured, false);
            if (Instance == this) Instance = null;
        }

        void Start() => Build();

        // ------------------------------------------------------------------
        // Dựng
        // ------------------------------------------------------------------

        void Build()
        {
            var canvas = HostCanvas(transform, 44);
            var shader = Shader.Find("Khkt/KinhMo");
            bool glassOk = shader != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera;

            // ----- Nút mở ở góc trên trái -----
            bool touch = PadInput.UsingTouch;
            var lb = MakeButton(canvas.transform, "Nút bài demo", touch ? "◈  Bài demo" : "◈  Bài demo   F2", new Vector2(24, touch ? -112 : -24), new Vector2(touch ? 190 : 230, touch ? 65 : 50), Toggle, false, 16);
            launcher = lb.gameObject;

            // ----- Bảng chọn bài -----
            root = new GameObject("Bảng bài demo", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(canvas.transform, false);
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            group = root.AddComponent<CanvasGroup>();
            Backdrop(root.transform, "Nền mờ", Close);
            var img = Window(root.transform, "Khung bài demo", new Vector2(PanelW, PanelH), new Vector2(0, 6));
            panel = (RectTransform)img.transform;
            var c = panel;
            Header(c, "BÀI DEMO · DÀNH CHO BAN GIÁM KHẢO", "Chọn một bài để trải nghiệm", "Mỗi bài 3–5 phút. Ứng dụng đưa bạn tới đúng trạm, hướng dẫn từng bước; 3 bài đầu là bài nổi bật, mỗi bài hỏi được trợ lý AI.", PanelW - 240f);
            MakeButton(c, "Đóng", touch ? "Đóng ✕" : "Đóng  Esc", new Vector2(-150, -24), new Vector2(124, 52), Close, false, 15, new Vector2(1, 1));

            const float lx = 30f, lw = 470f;
            Label(c, "Nhãn danh sách", "DANH SÁCH BÀI", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(lx, -122), new Vector2(lw, 18));
            rows = new Button[Demos.Length];
            for (int i = 0; i < Demos.Length; i++)
            {
                int k = i;
                var d = Demos[i];
                var b = MakeChip(c, "Bài " + d.id, "", new Vector2(lx, -146 - i * RowH), new Vector2(lw, RowH - 6f), () => Select(k), false, 16);
                var t = b.GetComponentInChildren<Text>();
                t.text = d.title;
                t.alignment = TextAnchor.UpperLeft;
                t.rectTransform.anchoredPosition = new Vector2(16, -5);
                t.rectTransform.sizeDelta = new Vector2(lw - 30, 24);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var sub = Label(b.transform, "Phụ", $"{d.area} · {d.minutes}" + (i < 3 ? " · NỔI BẬT" : ""), 13, AreaColor(d.area), true, TextAnchor.UpperLeft, new Vector2(16, -29), new Vector2(lw - 30, 18));
                sub.name = "Phụ";
                rows[i] = b;
            }

            float rx = lx + lw + 34f, rw = PanelW - rx - 30f;
            Fill(c, "Vạch chia", new Vector2(rx - 17f, -122f), new Vector2(1.5f, PanelH - 122f - 108f), Line);
            dArea = Label(c, "Khu", "", 13, Accent2, true, TextAnchor.UpperLeft, new Vector2(rx, -122), new Vector2(rw, 18));
            dTitle = Label(c, "Tên bài", "", 28, Ink, true, TextAnchor.UpperLeft, new Vector2(rx, -144), new Vector2(rw, 40));
            dWhere = Label(c, "Nơi làm", "", 15, Accent, true, TextAnchor.UpperLeft, new Vector2(rx, -188), new Vector2(rw, 22));
            dSummary = Label(c, "Tóm tắt", "", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(rx, -220), new Vector2(rw, 70));
            Label(c, "Nhãn bước", "CÁC BƯỚC", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -300), new Vector2(rw, 18));
            var stepBg = Fill(c, "Ô các bước", new Vector2(rx, -324), new Vector2(rw, PanelH - 324f - 116f), Well, null, true);
            dSteps = Label(stepBg.transform, "Các bước", "", 16, Ink, false, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(rw - 36, PanelH - 324f - 144f));
            dSteps.verticalOverflow = VerticalWrapMode.Truncate;
            dSteps.lineSpacing = 1.25f;

            Rule(c, new Vector2(30, -PanelH + 98), PanelW - 60);
            MakeButton(c, "Bắt đầu", "Bắt đầu: đưa tôi tới trạm", new Vector2(-370, -PanelH + 82), new Vector2(340, 64), StartSelected, true, 19, new Vector2(1, 1));
            robotButton = MakeButton(c, "Robot làm mẫu", "Robot làm mẫu cho xem", new Vector2(-650, -PanelH + 82), new Vector2(270, 64), RobotSelected, false, 16, new Vector2(1, 1));
            aiButtonPanel = MakeButton(c, "Hỏi trợ lý AI (bảng)", "Hỏi trợ lý AI", new Vector2(-900, -PanelH + 82), new Vector2(240, 64), () => { var d = Demos[selected]; StartDemo(d); AskAi(); }, false, 16, new Vector2(1, 1));
            root.SetActive(false);

            // ----- Thẻ hướng dẫn từng bước (không khóa điều khiển) -----
            var gi = Panel(canvas.transform, "Thẻ hướng dẫn demo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, touch ? -112 : -24), new Vector2(520, 304),
                           glassOk ? Glass(shader) : null, 1.4f);
            if (!glassOk && !ReducedTransparency) { gi.sprite = Solid; gi.pixelsPerUnitMultiplier = UiTheme.IsVera ? 1.4f : 2.8f; gi.color = Surface; }
            gi.raycastTarget = true;
            guide = gi.gameObject;
            var g = guide.transform;
            Label(g, "Mã", "BÀI DEMO", 12, Accent2, true, TextAnchor.UpperLeft, new Vector2(22, -14), new Vector2(200, 16));
            gStepNo = Label(g, "Số bước", "", 13, Accent, true, TextAnchor.UpperRight, new Vector2(300, -14), new Vector2(198, 18));
            gTitle = Label(g, "Tên bài", "", 19, Ink, true, TextAnchor.UpperLeft, new Vector2(22, -32), new Vector2(476, 28));
            gTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            Rule(g, new Vector2(22, -66), 476);
            gStep = Label(g, "Bước", "", 16, Ink, false, TextAnchor.UpperLeft, new Vector2(22, -76), new Vector2(476, 104));
            gStep.verticalOverflow = VerticalWrapMode.Truncate;
            aiButtonGuide = MakeButton(g, "Hỏi trợ lý AI", touch ? "Hỏi trợ lý AI về bài này" : "Hỏi trợ lý AI về bài này   F5", new Vector2(22, -190), new Vector2(476, 46), AskAi, false, 15);
            float by = -304 + 60;
            MakeButton(g, "Bước trước", "◂", new Vector2(22, by), new Vector2(64, 46), () => MoveStep(-1), false, 18);
            MakeButton(g, "Bước tiếp", touch ? "Bước tiếp ▸" : "Bước tiếp ▸  F3", new Vector2(94, by), new Vector2(214, 46), () => MoveStep(1), true, 15);
            MakeButton(g, "Kết thúc", touch ? "Kết thúc" : "Kết thúc  F4", new Vector2(316, by), new Vector2(182, 46), EndDemo, false, 15);
            guide.SetActive(false);
            Select(0);
        }

        // ------------------------------------------------------------------
        // Bảng chọn
        // ------------------------------------------------------------------

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (IsOpen || root == null) return;
            IsOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            UiBridge.SetCapture(ref captured, true);
            group.alpha = 0f;
            UiMotion.Popup(this, panel, group, 0.18f);
            Select(selected);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
        }

        void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, Demos.Length - 1);
            var d = Demos[selected];
            for (int i = 0; i < rows.Length; i++)
            {
                SetChip(rows[i], i == selected);
                var sub = rows[i].transform.Find("Phụ");
                if (sub != null) sub.GetComponent<Text>().color = AreaColor(Demos[i].area);
            }
            dArea.text = $"{d.area} · {d.minutes}";
            dTitle.text = d.title;
            dWhere.text = d.where;
            dSummary.text = d.summary;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < d.steps.Length; i++) sb.Append(i > 0 ? "\n\n" : "").Append($"<b>{i + 1}.</b>  ").Append(d.steps[i]);
            dSteps.text = sb.ToString();
            robotButton.gameObject.SetActive(d.robotKey != null && LabRobot.Instance != null);
            aiButtonPanel.gameObject.SetActive(!string.IsNullOrEmpty(d.aiQuestion));
        }

        void StartSelected() => StartDemo(Demos[selected]);

        void RobotSelected()
        {
            var d = Demos[selected];
            if (d.robotKey == null || LabRobot.Instance == null) return;
            StartDemo(d);
            LabRobot.Instance.RunDemo(d.robotKey);
            LabMessages.Say("Robot trợ lý đang làm mẫu: " + d.title, LabMessages.Tone.Good);
        }

        // ------------------------------------------------------------------
        // Chạy bài
        // ------------------------------------------------------------------

        public void StartDemo(Demo d)
        {
            Close();
            Active = d;
            Step = 0;
            if (viewer == null) viewer = FindAnyObjectByType<DesktopViewer>();
            if (viewer != null)
            {
                viewer.GoTo(d.station);
                try { d.onArrive?.Invoke(viewer); }
                catch (Exception e) { Debug.LogException(e); }
            }
            guide.SetActive(true);
            ShowStep();
            LabMessages.Say($"Bài demo: {d.title} · {d.where}", LabMessages.Tone.Good);
            if (AiClient.Instance != null) AiClient.Instance.Log("bai_demo", d.id, "Bắt đầu bài demo cho giám khảo: " + d.title);
        }

        void MoveStep(int dir)
        {
            if (Active == null) return;
            if (dir > 0 && Step >= Active.steps.Length - 1) { EndDemo(); return; }
            Step = Mathf.Clamp(Step + dir, 0, Active.steps.Length - 1);
            ShowStep();
        }

        static Color AreaColor(string area) => area == "HÓA HỌC" ? Accent2 : area == "TRỢ LÝ AI" ? Accent : Good;

        /// <summary>Mở trợ lý AI (khung trò chuyện robot, phím T) và gửi ngay câu hỏi của bài đang chạy.</summary>
        public void AskAi()
        {
            var d = Active ?? Demos[selected];
            if (d == null || string.IsNullOrEmpty(d.aiQuestion)) return;
            if (RobotChatUI.AskAbout(d.title, d.aiQuestion))
                LabMessages.Say("Đã gửi câu hỏi cho trợ lý AI: xem trả lời trong khung trò chuyện (T để đóng / mở)", LabMessages.Tone.Info);
        }

        void ShowStep()
        {
            if (aiButtonGuide != null) aiButtonGuide.gameObject.SetActive(!string.IsNullOrEmpty(Active.aiQuestion));
            gTitle.text = Active.title;
            gStepNo.text = $"BƯỚC {Step + 1}/{Active.steps.Length}";
            gStep.text = Active.steps[Step];
        }

        public void EndDemo()
        {
            if (Active == null) return;
            LabMessages.Say("Đã kết thúc bài demo: " + Active.title + ". Bấm F2 để chọn bài khác.", LabMessages.Tone.Info);
            Active = null;
            guide.SetActive(false);
        }

        void Update()
        {
            if (root == null) return;
            var kb = Keyboard.current;
            bool typing = UiBridge.Typing;
            if (kb != null && !typing)
            {
                if (kb.f2Key.wasPressedThisFrame && (IsOpen || DesktopViewer.UiCapture == 0)) Toggle();
                else if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
                else if (Active != null && kb.f3Key.wasPressedThisFrame) MoveStep(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? -1 : 1);
                else if (Active != null && kb.f4Key.wasPressedThisFrame) EndDemo();
                else if (Active != null && kb.f5Key.wasPressedThisFrame && DesktopViewer.UiCapture == 0) AskAi();
            }
            // Nút mở chỉ hiện khi không có bảng nào che và chưa có bài đang chạy; thẻ hướng dẫn ẩn tạm khi có bảng khác mở
            bool free = DesktopViewer.UiCapture == 0;
            if (launcher.activeSelf != (free && Active == null)) launcher.SetActive(free && Active == null);
            if (Active != null && guide.activeSelf != free) guide.SetActive(free);
        }
    }
}
