using System.IO;
using Khkt.Demo;
using Khkt.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Phong cách "công nghệ cao, năng động": tường video và màn hình chiếu bài / thảo luận (LabScreen), đèn dải LED trần,
    /// đèn hắt viền xanh, vạch dẫn hướng trên sàn, biển khu treo trần, cây xanh, tường và hành lang các nhà hóa học,
    /// dải "Lịch sử hóa học". Ảnh nhà hóa học: đặt file Assets/Lab/NhaHoaHoc/&lt;mã&gt;.jpg (hoặc .png) rồi dựng lại cảnh;
    /// chưa có ảnh thì dùng chân dung tạm.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        // ---------------------------------------------------------------------
        // Màn hình
        // ---------------------------------------------------------------------

        /// <summary>
        /// Một màn hình phẳng: viền, mặt hiển thị (vật liệu phát sáng riêng), chữ tiêu đề + nội dung, thành phần LabScreen.
        /// pos là tâm mặt màn hình, facing là hướng mặt màn hình nhìn ra.
        /// </summary>
        static LabScreen ScreenUnit(Transform p, string name, string id, Vector3 pos, Vector3 facing, float w, float h,
                                    float autoAdvance, params (string title, string body)[] pages)
        {
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var g = Group(name, p, pos, yaw);
            Box(g, "Viền màn hình", new Vector3(0, 0, -0.022f), new Vector3(w + 0.05f, h + 0.05f, 0.04f), blackPlastic);
            var surface = Panel(g, "Mặt màn hình", new Vector3(0, 0, 0.0015f), Vector3.forward, w, h, screenContent);
            NoShadow(surface);
            NoShadow(Box(g, "Đèn viền", new Vector3(0, -h / 2 - 0.03f, -0.01f), new Vector3(w * 0.6f, 0.006f, 0.01f), ledBlue, false));
            var title = Label(g, "", new Vector3(0, h * 0.405f, 0.004f), Vector3.forward, h * 0.07f, Color.white).GetComponent<TextMesh>();
            var body = Label(g, "", new Vector3(0, -h * 0.03f, 0.004f), Vector3.forward, h * 0.048f, new Color(0.82f, 0.92f, 1f)).GetComponent<TextMesh>();
            title.gameObject.name = "Chữ tiêu đề";
            body.gameObject.name = "Chữ nội dung";
            var s = g.gameObject.AddComponent<LabScreen>();
            s.screenId = id;
            s.slideFolder = id;
            s.surface = surface.GetComponent<Renderer>();
            s.titleText = title;
            s.bodyText = body;
            s.background = screenBg;
            s.autoAdvanceS = autoAdvance;
            foreach (var pg in pages) s.pages.Add(new LabScreen.Page { title = pg.title, body = pg.body });
            return s;
        }

        /// <summary>Tường video 3 màn hình 75 inch thay bảng trắng + camera nhìn xuống bàn giáo viên chiếu lên màn hình giữa.</summary>
        static void VideoWall(Transform g, float zc)
        {
            // Mảng ốp lam gỗ sồi (tiêu âm) phía sau tường video, viền nhôm tối, đèn hắt dịu trên và dưới
            var wall = Group("Tường video", g);
            NoShadow(Box(wall, "Mảng ốp lam gỗ", new Vector3(0.104f, 1.78f, zc), new Vector3(0.006f, 1.9f, 6.2f), accentWall, false));
            foreach (float ey in new[] { 0.82f, 2.74f })
                Box(wall, "Viền mảng ốp", new Vector3(0.108f, ey, zc), new Vector3(0.014f, 0.03f, 6.24f), doorFrame, false);
            NoShadow(Box(wall, "Dải LED trên", new Vector3(0.118f, 2.71f, zc), new Vector3(0.008f, 0.012f, 6.1f), ledBlue, false));

            const float w = 1.66f, h = 0.94f, gap = 0.06f, y = 1.78f;
            var s1 = ScreenUnit(wall, "Màn hình tường video 1", "tuong_video_1", new Vector3(0.135f, y, zc - (w + gap)), Vector3.right, w, h, 14f,
                ("PHÒNG THÍ NGHIỆM HÓA HỌC ẢO", "Thực hành tự do · Chuẩn độ · Nhận biết chất · Lắp ráp dụng cụ\n720 hóa chất · Robot trợ lý AI · Máy phân tích hiện đại"),
                ("BÀI HỌC HÔM NAY: CHUẨN ĐỘ AXIT - BAZƠ", "Xác định nồng độ HCl bằng dung dịch NaOH 0,1 M\n1. Tráng và nạp buret NaOH, đọc thể tích đầu\n2. Hút 25,00 ml HCl vào bình nón, thêm phenolphtalein\n3. Nhỏ NaOH, lắc đều tới khi hồng nhạt bền 30 giây\n4. Đọc thể tích cuối, làm 3 lần, lấy trung bình"));
            var s2 = ScreenUnit(wall, "Màn hình tường video 2", "tuong_video_2", new Vector3(0.135f, y, zc), Vector3.right, w, h, 0f,
                ("HƯỚNG DẪN CHIẾU BÀI", "Thả ảnh slide (PNG, JPG) vào thư mục\nStreamingAssets/BaiGiang/tuong_video_2\nrồi mở lại game. Bấm vào màn hình để sang trang."));
            var s3 = ScreenUnit(wall, "Màn hình tường video 3", "tuong_video_3", new Vector3(0.135f, y, zc + (w + gap)), Vector3.right, w, h, 16f,
                ("THẢO LUẬN NHÓM", "Vì sao phải lắc đều bình nón khi chuẩn độ?\nChọn chất chỉ thị nào khi chuẩn độ axit yếu bằng bazơ mạnh?\nNhấn T để hỏi trợ lý AI"));
            s3.pages.Insert(0, new LabScreen.Page { image = (Texture2D)poster.GetTexture("_BaseMap") });
            Info(s1.transform, "Tường video (3 màn hình 75 inch)",
                "Chiếu bài giảng, camera bàn giáo viên, câu hỏi thảo luận. Bấm vào màn hình để sang trang.",
                "Ảnh slide: StreamingAssets/BaiGiang/tuong_video_1 (2, 3). Sau này web quản lý đẩy nội dung lên được.");
            Info(s2.transform, "Màn hình giữa: camera bàn giáo viên", "Camera nhìn từ trên xuống mặt bàn biểu diễn của giáo viên, cả lớp cùng xem thao tác.");
            Info(s3.transform, "Màn hình thảo luận", "Bảng tuần hoàn và câu hỏi thảo luận cho cả lớp.");

            // Camera nhìn xuống bàn giáo viên (bàn ở x 1,9, dọc trục z)
            var rig = Group("Camera bàn giáo viên", g, new Vector3(1.55f, 0, 7.5f));
            Box(rig, "Cần camera", new Vector3(0, 1.32f, 0), new Vector3(0.03f, 0.84f, 0.03f), alu, false);
            Box(rig, "Tay vươn", new Vector3(0.18f, 1.74f, 0), new Vector3(0.36f, 0.03f, 0.03f), alu, false);
            Box(rig, "Thân camera", new Vector3(0.36f, 1.71f, 0), new Vector3(0.07f, 0.05f, 0.09f), blackPlastic, false);
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(rig, false);
            camGo.transform.localPosition = new Vector3(0.36f, 1.68f, 0);
            camGo.transform.localRotation = Quaternion.Euler(90f, -90f, 0);  // nhìn thẳng xuống, chiều dài bàn nằm ngang trên hình
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 4f;
            cam.enabled = false;
            // Hình nhỏ 640 × 360 nhìn từ trên xuống: không vẽ bóng đổ, không hậu kỳ (đỡ một lượt vẽ bản đồ bóng mỗi lần chụp)
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderShadows = false;
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            s2.liveCamera = cam;
            s2.pages.Insert(0, new LabScreen.Page { live = true, title = "CAMERA BÀN GIÁO VIÊN" });
        }

        /// <summary>Màn hình nhóm nhỏ ở mỗi bàn học sinh (thảo luận, mẹo an toàn). b là nhóm bàn, s là chiều bàn (+1 / -1).</summary>
        static void BenchScreen(Transform b, string benchName, int number, int s)
        {
            string[] tips =
            {
                "Pha loãng axit đặc: rót từ từ axit vào nước, không làm ngược lại",
                "Đun ống nghiệm: hướng miệng ống ra chỗ không có người",
                "Ngửi khí: phẩy nhẹ tay trên miệng ống, không hít trực tiếp",
                "Khí độc (Cl₂, NO₂, H₂S): làm trong tủ hút",
                "Hóa chất dính da: rửa ngay dưới vòi nước 15 phút",
                "Đọc buret: mắt ngang đáy mặt khum",
                "Không đổ hóa chất thừa trở lại chai gốc",
                "Tắt đèn cồn bằng nắp, không thổi",
                "Ghi nhãn mọi ống nghiệm, cốc đang dùng",
            };
            // Đặt ở đầu bàn (phía không có bồn rửa), trên hàng lọ hóa chất, để không che tầm nhìn lên tường video
            var scr = ScreenUnit(b, "Màn hình nhóm", "ban_" + number, new Vector3(-0.36f, 1.47f, -1.05f * s), Vector3.right, 0.4f, 0.24f, 12f,
                ($"BÀN {number} · NHÓM {number}", "Sẵn sàng thực hành\nĐeo kính, mặc áo blouse"),
                ("MẸO AN TOÀN", tips[(number - 1) % tips.Length].Replace(": ", ":\n")),
                ("THẢO LUẬN", "Ghi kết quả, so sánh với nhóm bạn\nNhấn T để hỏi trợ lý AI"));
            Box(b, "Chân màn hình", new Vector3(-0.36f, 1.25f, -1.05f * s), new Vector3(0.03f, 0.2f, 0.03f), alu, false);
            Info(scr.transform, $"Màn hình nhóm {benchName.Replace("Bàn thí nghiệm", "bàn")}",
                "Màn hình thảo luận của nhóm: mẹo an toàn, câu hỏi, sau này nhận bài từ giáo viên. Bấm để sang trang.");
        }

        // ---------------------------------------------------------------------
        // Đèn LED, sàn, biển, cây
        // ---------------------------------------------------------------------

        static GameObject LedStrip(Transform p, string name, Vector3 from, Vector3 to, float width, Material m)
        {
            Vector3 d = to - from;
            bool alongX = Mathf.Abs(d.x) >= Mathf.Abs(d.z);
            var size = alongX ? new Vector3(Mathf.Abs(d.x), 0.012f, width) : new Vector3(width, 0.012f, Mathf.Abs(d.z));
            var go = Box(p, name, (from + to) / 2, size, m, false);
            NoShadow(go);
            return go;
        }

        /// <summary>Trần phòng lab chính: tấm trần gỗ treo trên từng hàng bàn (đèn dải dưới tấm), đèn hắt viền dọc tường, dải LED trên khu giáo viên.</summary>
        static void LabCeilingLeds(Transform lights)
        {
            float y = H - 0.008f;
            CeilingRafts(lights);
            foreach (float x in new[] { 1.3f, 2.5f })
                LedStrip(lights, "Dải LED trần", new Vector3(x, y, 4.2f), new Vector3(x, y, 10.8f), 0.11f, tubeGlow);
            // đèn hắt viền xanh sát mép trần quanh phòng
            const float inset = 0.16f;
            LedStrip(lights, "Đèn hắt viền", new Vector3(inset, y, inset), new Vector3(LabX - inset, y, inset), 0.04f, ledBlue);
            LedStrip(lights, "Đèn hắt viền", new Vector3(inset, y, LabZ - inset), new Vector3(LabX - inset, y, LabZ - inset), 0.04f, ledBlue);
            LedStrip(lights, "Đèn hắt viền", new Vector3(inset, y, inset), new Vector3(inset, y, LabZ - inset), 0.04f, ledBlue);
        }

        /// <summary>
        /// Dải sọc màu (supergraphic) chạy ngang tường, cao trên mép cửa (y 2,8 - 3,14), không đụng thiết bị: bốn sọc màu nhận diện
        /// các khu trên nền trắng ấm. Sọc nằm ngang nên kéo dài bao nhiêu cũng không méo.
        /// </summary>
        static void Frieze(Transform p, Vector3 a, Vector3 b)
        {
            const float h = 0.34f, y = 2.97f;
            float len = Vector3.Distance(a, b);
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            var size = alongX ? new Vector3(len, h, 0.006f) : new Vector3(0.006f, h, len);
            var mid = (a + b) / 2;
            NoShadow(Box(p, "Dải sọc màu", new Vector3(mid.x, y, mid.z), size, stripeMat, false));
        }

        /// <summary>Dải sọc màu cho dãy phòng phía nam, hành lang, tường bắc / nam phòng lab và phòng hóa chất; đèn hắt viền trần.</summary>
        static void WingAccents(Transform root)
        {
            var g = Group("Điểm nhấn công nghệ", root);
            const float f = 0.104f;  // mặt tường trong = tâm tường + nửa độ dày
            float zs = WingZ + f, zc = CorridorZ - f, y = H - 0.008f;
            // Phòng máy phân tích (x 0..13,5) và phòng robot (x 13,5..27)
            foreach (var (x0, x1) in new[] { (0f, RoomSplitX), (RoomSplitX, StoreX) })
            {
                float a = x0 + f, b = x1 - f;
                Frieze(g, new Vector3(a, 0, zs), new Vector3(b, 0, zs));
                Frieze(g, new Vector3(a, 0, zc), new Vector3(b, 0, zc));
                Frieze(g, new Vector3(a, 0, zs), new Vector3(a, 0, zc));
                Frieze(g, new Vector3(b, 0, zs), new Vector3(b, 0, zc));
                LedStrip(g, "Đèn hắt viền", new Vector3(a + 0.1f, y, zs + 0.1f), new Vector3(b - 0.1f, y, zs + 0.1f), 0.04f, ledBlue);
                LedStrip(g, "Đèn hắt viền", new Vector3(a + 0.1f, y, zc - 0.1f), new Vector3(b - 0.1f, y, zc - 0.1f), 0.04f, ledBlue);
            }
            // Hành lang: hai bên, trên mép cửa sổ
            Frieze(g, new Vector3(0.2f, 0, -f), new Vector3(StoreX - 0.2f, 0, -f));
            Frieze(g, new Vector3(0.2f, 0, CorridorZ + f), new Vector3(StoreX - 0.2f, 0, CorridorZ + f));
            // Phòng lab chính: tường bắc và tường nam (tường tây đã có tường video, tường đông là vách kính)
            Frieze(g, new Vector3(0.2f, 0, f), new Vector3(LabX - 0.1f, 0, f));
            Frieze(g, new Vector3(0.2f, 0, LabZ - f), new Vector3(LabX - 0.1f, 0, LabZ - f));
            // Phòng hóa chất: tường đông (trên dãy kệ) và tường nam
            Frieze(g, new Vector3(StoreX - f, 0, 0.2f), new Vector3(StoreX - f, 0, 12.6f));  // dừng trước lỗ quạt hút (z 12,8)
            Frieze(g, new Vector3(LabX + 0.1f, 0, f), new Vector3(StoreX - 0.2f, 0, f));
        }

        /// <summary>Biển khu treo trần hai mặt, song ngữ: nền xanh ngọc đậm, chữ trắng, dòng tiếng Anh nhỏ bên dưới, viền đèn dịu.</summary>
        static void ZoneSign(Transform p, string text, Vector3 pos, float yaw, float width = 2.4f, string en = null)
        {
            var g = Group("Biển khu " + text, p, pos, yaw);
            float rope = H - pos.y - 0.16f;  // từ mép trên biển tới trần
            foreach (float x in new[] { -width / 2 + 0.15f, width / 2 - 0.15f })
                Box(g, "Dây treo", new Vector3(x, 0.16f + rope / 2, 0), new Vector3(0.008f, rope, 0.008f), steel, false);
            Box(g, "Biển", Vector3.zero, new Vector3(width, 0.32f, 0.03f), accentDark, false);
            NoShadow(Box(g, "Viền đèn", new Vector3(0, -0.165f, 0), new Vector3(width, 0.012f, 0.032f), ledBlue, false));
            foreach (int sd in new[] { 1, -1 })
            {
                var face = sd > 0 ? Vector3.forward : Vector3.back;
                if (en == null) { Label(g, text, new Vector3(0, 0, sd * 0.017f), face, 0.09f, Color.white); continue; }
                Label(g, text, new Vector3(0, 0.045f, sd * 0.017f), face, 0.08f, Color.white);
                Label(g, en, new Vector3(0, -0.08f, sd * 0.017f), face, 0.04f, new Color(0.65f, 0.9f, 0.9f));
            }
        }

        /// <summary>Cây xanh trong chậu trắng cao (trang trí phòng hiện đại).</summary>
        static void ModernPlant(Transform p, Vector3 pos, float scale = 1f)
        {
            var g = Group("Cây xanh", p, pos);
            var potMesh = Lathe("ChauTrangCao", 24, V(0, 0), V(0.16f, 0), V(0.16f, 0), V(0.19f, 0.5f), V(0.2f, 0.52f), V(0.2f, 0.55f), V(0, 0.55f));
            MeshObj(g, "Chậu", potMesh, Vector3.zero, potWhite, true, null, scale);
            Ball(g, "Tán lá", new Vector3(0, 0.95f, 0) * scale, new Vector3(0.55f, 0.75f, 0.55f) * scale, foliage);
            Ball(g, "Tán lá", new Vector3(0.12f, 1.2f, 0.05f) * scale, new Vector3(0.38f, 0.5f, 0.38f) * scale, foliageDark);
            Ball(g, "Tán lá", new Vector3(-0.1f, 1.1f, -0.08f) * scale, new Vector3(0.34f, 0.45f, 0.34f) * scale, foliage);
        }

        // ---------------------------------------------------------------------
        // Các nhà hóa học
        // ---------------------------------------------------------------------

        static readonly (string id, string name, string years, string work)[] Chemists =
        {
            ("lavoisier", "Antoine Lavoisier", "1743 – 1794 · Pháp", "Định luật bảo toàn khối lượng\nĐặt tên oxi, hiđro\n\"Không có gì tự mất đi, không có gì\ntự sinh ra, tất cả chỉ biến đổi\""),
            ("dalton", "John Dalton", "1766 – 1844 · Anh", "Thuyết nguyên tử (1803 – 1808)\nĐịnh luật áp suất riêng phần"),
            ("mendeleev", "Dmitri Mendeleev", "1834 – 1907 · Nga", "Lập bảng tuần hoàn (1869)\nDự đoán Ga, Sc, Ge trước khi tìm thấy"),
            ("curie", "Marie Curie", "1867 – 1934 · Ba Lan – Pháp", "Phát hiện poloni, rađi (1898)\nNobel Vật lí 1903, Nobel Hóa học 1911\n\"Trong đời không có gì đáng sợ,\nchỉ có những điều cần hiểu\""),
            ("avogadro", "Amedeo Avogadro", "1776 – 1856 · Ý", "Giả thuyết Avogadro (1811): cùng thể tích,\ncùng điều kiện, các khí có cùng số phân tử"),
            ("faraday", "Michael Faraday", "1791 – 1867 · Anh", "Định luật điện phân (1834)\nĐặt tên anot, catot, ion · Tìm ra benzen (1825)"),
            ("arrhenius", "Svante Arrhenius", "1859 – 1927 · Thụy Điển", "Thuyết điện li, axit - bazơ (1887)\nNobel Hóa học 1903"),
        };

        /// <summary>
        /// Ảnh nhà hóa học từ Assets/Lab/NhaHoaHoc/&lt;mã&gt;.jpg|png nếu có (ảnh thuộc phạm vi công cộng), không thì chân dung tạm.
        /// Ảnh được cắt cho vừa tỉ lệ khung (frameAspect = rộng / cao): ảnh rộng hơn thì cắt đều hai bên, ảnh cao hơn thì giữ phần đầu.
        /// </summary>
        static Material PortraitMaterial(string id, float frameAspect)
        {
            Texture2D tex = null;
            string path = null;
            foreach (var ext in new[] { ".jpg", ".png", ".jpeg" })
            {
                path = $"{Root}/NhaHoaHoc/{id}{ext}";
                if (File.Exists(path)) { tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path); break; }
            }
            if (tex == null) return EmissiveTexMat("ChanDungTam", portraitTex, new Color(0.2f, 0.2f, 0.22f), Vector2.one);
            // Kích thước ảnh gốc (tex.width có thể đã bị nhập về lũy thừa của 2)
            int sw = tex.width, sh = tex.height;
            if (AssetImporter.GetAtPath(path) is TextureImporter imp) imp.GetSourceTextureWidthAndHeight(out sw, out sh);
            float a = (float)sw / sh;
            var scale = a > frameAspect ? new Vector2(frameAspect / a, 1) : new Vector2(1, a / frameAspect);
            var m = EmissiveTexMat("ChanDung_" + id, tex, new Color(0.22f, 0.22f, 0.22f), scale);
            m.SetTextureOffset("_BaseMap", new Vector2((1 - scale.x) / 2, 1 - scale.y));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Khung ảnh nhà hóa học: ảnh 0,6 × 0,78 m, đèn rọi tranh, tên, năm, cống hiến. pos là tâm ảnh trên mặt tường.</summary>
        static void ChemistPortrait(Transform p, int index, Vector3 pos, Vector3 facing)
        {
            var c = Chemists[index];
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var g = Group("Nhà hóa học " + c.name, p, pos, yaw);
            const float w = 0.6f, h = 0.78f;
            Box(g, "Khung", new Vector3(0, 0, -0.012f), new Vector3(w + 0.06f, h + 0.06f, 0.024f), frameMat, false);
            NoShadow(Panel(g, "Ảnh", new Vector3(0, 0, 0.001f), Vector3.forward, w, h, PortraitMaterial(c.id, w / h)));
            NoShadow(Box(g, "Đèn rọi tranh", new Vector3(0, h / 2 + 0.07f, 0.05f), new Vector3(w * 0.7f, 0.025f, 0.05f), tubeGlow, false));
            Box(g, "Bảng tên", new Vector3(0, -h / 2 - 0.2f, -0.004f), new Vector3(w + 0.3f, 0.3f, 0.008f), accentDark, false);
            Label(g, c.name.ToUpperInvariant(), new Vector3(0, -h / 2 - 0.09f, 0.002f), Vector3.forward, 0.042f, Color.white);
            Label(g, c.years, new Vector3(0, -h / 2 - 0.14f, 0.002f), Vector3.forward, 0.026f, new Color(0.6f, 0.82f, 1f));
            Label(g, c.work, new Vector3(0, -h / 2 - 0.25f, 0.002f), Vector3.forward, 0.02f, new Color(0.9f, 0.95f, 1f));
            Info(g, c.name, c.years + ". " + c.work.Replace("\n", "; "));
        }

        /// <summary>Tường "Các nhà hóa học tiêu biểu" ở tường tây phòng lab, phía bắc tường video.</summary>
        static void ChemistWall(Transform lab)
        {
            var g = Group("Tường các nhà hóa học", lab);
            Label(g, "CÁC NHÀ HÓA HỌC TIÊU BIỂU", new Vector3(0.108f, 2.66f, 12.6f), Vector3.right, 0.075f, new Color(0.0f, 0.36f, 0.4f));
            Label(g, "GREAT CHEMISTS", new Vector3(0.108f, 2.55f, 12.6f), Vector3.right, 0.04f, new Color(0.35f, 0.38f, 0.4f));
            Box(g, "Dải màu", new Vector3(0.106f, 2.48f, 12.6f), new Vector3(0.006f, 0.02f, 3.6f), accent, false);
            for (int i = 0; i < 4; i++)
                ChemistPortrait(g, i, new Vector3(0.112f, 1.78f, 11.1f + i * 1.0f), Vector3.right);
        }

        /// <summary>Hành lang: 3 nhà hóa học giữa các ô cửa sổ, dải "Lịch sử hóa học", màn hình bảng tin điện tử.</summary>
        static void CorridorGallery(Transform hall)
        {
            var g = Group("Hành lang các nhà hóa học", hall);
            float[] xs = { 10.1f, 13.1f, 16.1f };
            for (int i = 0; i < xs.Length; i++)
                ChemistPortrait(g, 4 + i, new Vector3(xs[i], 1.7f, -0.112f), Vector3.back);

            // Dải lịch sử hóa học trên đoạn tường đặc x 4,1..8,1 (mặt hướng về hành lang), chừa chỗ cánh cửa trượt phòng lab
            var tl = Group("Lịch sử hóa học", g, new Vector3(6.15f, 1.55f, -0.108f), 180);
            Box(tl, "Nền", new Vector3(0, 0, -0.004f), new Vector3(4.0f, 1.1f, 0.008f), accentDark, false);
            Label(tl, "LỊCH SỬ HÓA HỌC", new Vector3(0, 0.44f, 0.002f), Vector3.forward, 0.07f, Color.white);
            NoShadow(Box(tl, "Trục thời gian", new Vector3(0, 0.05f, 0.002f), new Vector3(3.7f, 0.012f, 0.004f), ledBlue, false));
            var events = new (string year, string text)[]
            {
                ("1661", "Boyle\nNhà hóa học\nhoài nghi"), ("1789", "Lavoisier\nBảo toàn\nkhối lượng"), ("1803", "Dalton\nThuyết\nnguyên tử"),
                ("1811", "Avogadro\nGiả thuyết\nphân tử khí"), ("1834", "Faraday\nĐịnh luật\nđiện phân"), ("1869", "Mendeleev\nBảng\ntuần hoàn"),
                ("1884", "Le Chatelier\nNguyên lí chuyển\ndịch cân bằng"), ("1887", "Arrhenius\nThuyết\nđiện li"), ("1898", "Curie\nPoloni,\nrađi"),
                ("1909", "Sørensen\nThang\npH"),
            };
            for (int i = 0; i < events.Length; i++)
            {
                // nhóm xoay 180° (mặt nhìn về hành lang): trục x riêng ngược trục x thế giới, nên đi từ +x để năm tăng dần từ trái sang phải
                float x = 1.75f - i * (3.5f / (events.Length - 1));
                NoShadow(Ball(tl, "Mốc", new Vector3(x, 0.05f, 0.004f), new Vector3(0.045f, 0.045f, 0.01f), ledBlue));
                Label(tl, events[i].year, new Vector3(x, 0.16f, 0.003f), Vector3.forward, 0.05f, new Color(0.55f, 0.8f, 1f));
                Label(tl, events[i].text, new Vector3(x, -0.2f, 0.003f), Vector3.forward, 0.03f, new Color(0.9f, 0.95f, 1f));
            }

            ScreenUnit(hall, "Màn hình bảng tin", "hanh_lang", new Vector3(13.5f, 1.6f, CorridorZ + 0.12f), Vector3.forward, 1.2f, 0.68f, 10f,
                ("BẢNG TIN KHKT", "Web quản lý phòng lab: http://127.0.0.1:8000\nLịch phòng máy · Nhật ký sự cố · Tồn kho hóa chất"),
                ("PHÒNG MÁY PHÂN TÍCH HIỆN ĐẠI", "UV-Vis · FTIR · AAS · GC-MS · HPLC\nChuẩn độ điện thế tự động"),
                ("PHÒNG TỰ ĐỘNG HÓA & ROBOT", "Robot trợ lý AI · Cánh tay cộng tác\nMáy hút nhả tự động · Máy in 3D"));
        }
    }
}
