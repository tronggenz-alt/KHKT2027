using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Phong cách phòng lab trường quốc tế: gỗ sồi sáng, màu nhận diện theo khu, tấm trần gỗ treo trên các hàng bàn,
    /// tường triển lãm dự án học sinh (bảng ghim, poster), tủ khay nhựa nhiều màu, mô hình phân tử, lối đi trên sàn,
    /// biển song ngữ Việt – Anh, mảng màu quanh cửa ở hành lang.
    ///
    /// Mọi ảnh vân sinh bằng hàm tuần hoàn (lặp liền mạch), tần số thấp, tương phản nhẹ và có mipmap: nhìn xa hay nhìn
    /// nghiêng đều không nhấp nháy, không lấm tấm hạt.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        const float Tau = Mathf.PI * 2f;

        // =====================================================================
        // Ảnh vân
        // =====================================================================

        /// <summary>Vân gỗ sồi sáng, thớ dọc (theo v), lặp liền mạch.</summary>
        static Color OakPixel(int x, int y)
        {
            float u = x / 256f, v = y / 256f;
            float warp = 0.035f * Mathf.Sin(Tau * (v * 2f + 0.3f)) + 0.02f * Mathf.Sin(Tau * (u + v * 3f));
            float grain = 0.5f + 0.5f * Mathf.Sin(Tau * (u + warp) * 18f);
            float rings = 0.5f + 0.5f * Mathf.Sin(Tau * (u * 3f + warp * 4f));
            float k = 0.9f + 0.05f * grain + 0.05f * rings;
            return new Color(0.84f * k, 0.68f * k, 0.5f * k);
        }

        /// <summary>Ốp lam gỗ: 8 lam mỗi ô ảnh, khe tối giữa các lam.</summary>
        static Color SlatPixel(int x, int y)
        {
            int lx = x % 32, slat = x / 32;
            if (lx < 4) return new Color(0.16f, 0.12f, 0.09f);
            float v = y / 256f;
            float grain = 0.5f + 0.5f * Mathf.Sin(Tau * (v * 3f + slat * 0.37f));
            float k = (0.9f + 0.04f * (slat % 3)) * (0.95f + 0.05f * grain);
            if (lx < 6 || lx > 30) k *= 0.88f;  // mép lam hơi tối: nổi khối
            return new Color(0.82f * k, 0.65f * k, 0.47f * k);
        }

        /// <summary>Dải sọc màu chạy ngang tường (supergraphic): xanh ngọc, xanh cốm, vàng, cam san hô trên nền trắng ấm.</summary>
        static Color StripePixel(int x, int y)
        {
            var bg = new Color(0.94f, 0.93f, 0.89f);
            if (y >= 10 && y < 30) return zoneLab;
            if (y >= 38 && y < 58) return zoneAnalysis;
            if (y >= 66 && y < 86) return zoneStore;
            if (y >= 94 && y < 114) return zoneRobot;
            return bg;
        }

        /// <summary>Bảng ghim bần (lie): nâu vàng, lốm đốm rất nhẹ, lặp liền mạch.</summary>
        static Color CorkPixel(int x, int y)
        {
            float u = x / 128f, v = y / 128f;
            float n = 0.5f + 0.25f * Mathf.Sin(Tau * (u * 5f + v * 2f)) + 0.25f * Mathf.Sin(Tau * (u * 3f - v * 7f + 0.4f));
            float speck = (Hash(x, y) % 97) == 0 ? 0.82f : 1f;
            float k = (0.92f + 0.08f * n) * speck;
            return new Color(0.74f * k, 0.57f * k, 0.39f * k);
        }

        /// <summary>Lối đi trên sàn: vinyl xám xanh, vân rất nhẹ.</summary>
        static Color WalkwayPixel(int x, int y)
        {
            float u = x / 128f, v = y / 128f;
            float n = 0.5f + 0.5f * Mathf.Sin(Tau * (u * 2f + v)) * Mathf.Sin(Tau * (v * 3f));
            float k = 0.96f + 0.04f * n;
            return new Color(k, k, k);
        }

        static readonly Color[] PosterColors =
        {
            new Color(0.0f, 0.56f, 0.6f), new Color(0.95f, 0.43f, 0.26f), new Color(0.47f, 0.72f, 0.2f), new Color(0.97f, 0.7f, 0.12f),
            new Color(0.25f, 0.5f, 0.85f), new Color(0.55f, 0.35f, 0.75f), new Color(0.85f, 0.25f, 0.4f),
        };

        /// <summary>Poster dự án (128 × 176): dải đầu màu, biểu đồ cột / sơ đồ phân tử / đồ thị đường, các dòng chữ giả.</summary>
        static Color PosterPixel(int variant, int x, int y)
        {
            const int W = 128, Hh = 176;
            var c = PosterColors[variant % PosterColors.Length];
            var paper = new Color(0.97f, 0.96f, 0.93f);
            if (x < 3 || x >= W - 3 || y < 3 || y >= Hh - 3) return Color.Lerp(c, Color.white, 0.3f);  // viền
            if (y >= Hh - 36) return c;                                                                 // dải đầu (tiêu đề ghi bằng chữ 3D)
            if (y >= 66 && y < Hh - 44)
            {
                int kind = variant % 3;
                float fx = (x - 14) / 100f, fy = (y - 66) / 60f;
                if (kind == 0)  // biểu đồ cột
                {
                    int bar = (x - 16) / 20;
                    float hgt = 0.3f + 0.15f * ((bar * 7 + variant * 3) % 5);
                    if (x >= 16 && x < 112 && (x - 16) % 20 < 14 && fy < hgt) return PosterColors[(bar + variant + 1) % PosterColors.Length];
                    if (y == 66 || x == 14) return new Color(0.3f, 0.3f, 0.32f);
                }
                else if (kind == 1)  // sơ đồ phân tử: vòng 6 nguyên tử
                {
                    var center = new Vector2(64, 96);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Tau * i / 6f;
                        var atom = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 22f;
                        if (Vector2.Distance(new Vector2(x, y), atom) < 6f) return i % 2 == 0 ? new Color(0.25f, 0.25f, 0.28f) : c;
                    }
                    if (Mathf.Abs(Vector2.Distance(new Vector2(x, y), center) - 22f) < 1.2f) return new Color(0.4f, 0.4f, 0.42f);
                }
                else  // đồ thị đường
                {
                    float curve = 0.15f + 0.7f * (1f - Mathf.Exp(-3f * fx)) + 0.05f * Mathf.Sin(fx * 9f);
                    if (fx >= 0 && fx <= 1 && Mathf.Abs(fy - curve) < 0.035f) return c * 0.85f;
                    if (y == 66 || x == 14) return new Color(0.3f, 0.3f, 0.32f);
                }
                return paper;
            }
            // dòng chữ giả
            if (y >= 14 && y < 58 && (y - 14) % 9 < 4 && x >= 12 && x < 116 - ((y / 9 + variant) % 3) * 14)
                return new Color(0.62f, 0.62f, 0.64f);
            return paper;
        }

        /// <summary>
        /// Biểu tượng cảnh báo GHS (128 × 128, nền trong suốt ngoài hình thoi): viền đỏ, nền trắng, hình đen.
        /// 0 dễ cháy (ngọn lửa), 1 ăn mòn (chất nhỏ xuống thanh), 2 độc cấp tính (đầu lâu), 3 cảnh báo (dấu chấm than).
        /// </summary>
        static Color GhsPixel(int kind, int x, int y)
        {
            float dx = x - 63.5f, dy = y - 63.5f;
            float d = Mathf.Abs(dx) + Mathf.Abs(dy);
            if (d > 61f) return new Color(1, 1, 1, 0);
            if (d > 52f) return new Color(0.86f, 0.06f, 0.05f, 1);
            var black = new Color(0.06f, 0.06f, 0.07f, 1);
            var p = new Vector2(x, y);
            bool on = false;
            switch (kind)
            {
                case 0:  // ngọn lửa trên một vạch
                    on = Mathf.Pow((x - 64f) / 15f, 2) + Mathf.Pow((y - 58f) / 17f, 2) < 1f
                         || (y >= 58 && y < 92 && Mathf.Abs(x - 64f + (y - 58f) * 0.12f) < 15f * (1f - (y - 58f) / 34f))
                         || (y >= 34 && y < 39 && x >= 40 && x < 88);
                    if (on && Mathf.Pow((x - 66f) / 6f, 2) + Mathf.Pow((y - 56f) / 8f, 2) < 1f) on = false;  // lõi lửa
                    break;
                case 1:  // hai ống nghiêng nhỏ giọt xuống thanh
                    on = (x >= 42 && x < 54 && y >= 72 && y < 90) || (x >= 74 && x < 86 && y >= 72 && y < 90)
                         || Vector2.Distance(p, new Vector2(48, 63)) < 4f || Vector2.Distance(p, new Vector2(80, 63)) < 4f
                         || (y >= 40 && y < 47 && x >= 36 && x < 92);
                    break;
                case 2:  // đầu lâu xương chéo
                    on = Vector2.Distance(p, new Vector2(64, 74)) < 17f || (x >= 55 && x < 73 && y >= 56 && y < 66);
                    if (Vector2.Distance(p, new Vector2(57, 76)) < 4.5f || Vector2.Distance(p, new Vector2(71, 76)) < 4.5f) on = false;
                    on |= Mathf.Abs((x - 64f) - (y - 44f) * 1.2f) < 3.5f && y >= 34 && y < 54;
                    on |= Mathf.Abs((x - 64f) + (y - 44f) * 1.2f) < 3.5f && y >= 34 && y < 54;
                    break;
                default:  // dấu chấm than
                    on = (x >= 59 && x < 69 && y >= 54 && y < 92) || Vector2.Distance(p, new Vector2(64, 44)) < 5.5f;
                    break;
            }
            return on ? black : new Color(1, 1, 1, 1);
        }

        // =====================================================================
        // Trần: tấm gỗ treo trên các hàng bàn, đèn dải dưới tấm
        // =====================================================================

        /// <summary>
        /// Tấm trần gỗ (cloud) treo trên mỗi hàng bàn, hai dải đèn LED dưới mỗi tấm. Phần trên khu học sinh và phần trên khu
        /// tổng hợp / tủ hút tách đôi để chừa chỗ biển khu.
        /// </summary>
        static void CeilingRafts(Transform lights)
        {
            const float y = 3.27f;
            foreach (float z in BenchZ)
                foreach (var (x0, x1) in new[] { (3.8f, 13.4f), (14.3f, 19.3f) })
                {
                    float len = x1 - x0, cx = (x0 + x1) / 2;
                    var raft = Group("Tấm trần gỗ", lights, new Vector3(cx, y, z));
                    NoShadow(Box(raft, "Tấm gỗ", Vector3.zero, new Vector3(len, 0.04f, 1.7f), raftMat, false));
                    foreach (float ez in new[] { -0.86f, 0.86f })  // nẹp mép dọc hai cạnh dài (không che mặt dưới tấm gỗ)
                        NoShadow(Box(raft, "Nẹp mép tấm", new Vector3(0, 0, ez), new Vector3(len, 0.05f, 0.02f), Mat("VienTamTran", new Color(0.2f, 0.21f, 0.23f), 0.4f, 0.5f), false));
                    foreach (float ex in new[] { -len / 2 + 0.3f, len / 2 - 0.3f })
                        foreach (float ez in new[] { -0.7f, 0.7f })
                            NoShadow(Box(raft, "Dây treo", new Vector3(ex, (H - y) / 2, ez), new Vector3(0.006f, H - y, 0.006f), steel, false));
                    foreach (float dz in new[] { -0.5f, 0.5f })
                        LedStrip(lights, "Dải LED trần", new Vector3(x0 + 0.25f, y - 0.03f, z + dz), new Vector3(x1 - 0.25f, y - 0.03f, z + dz), 0.09f, tubeGlow);
                }
        }

        // =====================================================================
        // Tường triển lãm dự án học sinh (tường nam phòng lab, x 7,6..13,6)
        // =====================================================================

        static void ProjectWall(Transform lab)
        {
            var g = Group("Tường triển lãm dự án", lab);
            const float zf = 0.1f, x0 = 7.75f, x1 = 13.45f;
            float cx = (x0 + x1) / 2, len = x1 - x0;
            Box(g, "Bảng ghim", new Vector3(cx, 1.78f, zf + 0.012f), new Vector3(len, 1.2f, 0.02f), corkMat, false);
            foreach (float ey in new[] { 1.17f, 2.39f })
                Box(g, "Khung gỗ", new Vector3(cx, ey, zf + 0.016f), new Vector3(len + 0.06f, 0.04f, 0.03f), oak, false);
            foreach (float ex in new[] { x0 - 0.015f, x1 + 0.015f })
                Box(g, "Khung gỗ", new Vector3(ex, 1.78f, zf + 0.016f), new Vector3(0.04f, 1.26f, 0.03f), oak, false);
            Box(g, "Dải tiêu đề", new Vector3(cx, 2.55f, zf + 0.008f), new Vector3(len, 0.2f, 0.012f), accent, false);
            Label(g, "TRIỂN LÃM DỰ ÁN HỌC SINH · STUDENT PROJECTS", new Vector3(cx, 2.55f, zf + 0.016f), Vector3.forward, 0.07f, Color.white);

            var titles = new (string vi, string en)[]
            {
                ("Axit – Bazơ quanh ta", "Acids & Bases"), ("Pin điện hóa tự chế", "Electrochemistry"), ("Hóa học xanh", "Green Chemistry"),
                ("Xu hướng tuần hoàn", "Periodic Trends"), ("Tốc độ phản ứng", "Rates of Reaction"), ("Polime & nhựa tái chế", "Polymers"),
                ("Hội thi KHKT 2026", "Science Fair 2026"),
            };
            for (int i = 0; i < titles.Length; i++)
            {
                float x = x0 + 0.42f + i * 0.81f;
                float y = 1.74f + (i % 2 == 0 ? 0.03f : -0.03f);
                int v = i;
                var m = Mat("Poster" + i, Color.white, 0.2f, 0, Tex("Poster" + i, 128, 176, (px, py) => PosterPixel(v, px, py), repeat: false));
                NoShadow(Panel(g, "Poster " + titles[i].en, new Vector3(x, y, zf + 0.024f), Vector3.forward, 0.62f, 0.85f, m));
                Label(g, titles[i].vi, new Vector3(x, y + 0.36f, zf + 0.026f), Vector3.forward, 0.032f, Color.white);
                Label(g, titles[i].en, new Vector3(x, y + 0.315f, zf + 0.026f), Vector3.forward, 0.022f, new Color(1f, 1f, 1f, 0.85f));
                NoShadow(Ball(g, "Đinh ghim", new Vector3(x, y + 0.4f, zf + 0.03f), Vector3.one * 0.02f, Mat("DinhGhim" + i % 3, PosterColors[(i + 3) % PosterColors.Length], 0.5f)));
            }
            Info(g, "Tường triển lãm dự án học sinh", "Poster dự án của các nhóm: axit – bazơ, pin điện hóa, hóa học xanh, polime...",
                 "Sau này giáo viên đẩy ảnh dự án mới lên từ web quản lý.");

            TrayUnit(g, new Vector3(9.0f, 0, zf), 0);
            TrayUnit(g, new Vector3(10.2f, 0, zf), 1);
            Bookshelf(g, new Vector3(12.25f, 0, zf));
            MoleculeModel(g, "Mô hình phân tử nước (H₂O)", new Vector3(8.75f, 1.02f, zf + 0.25f), 0);
            MoleculeModel(g, "Mô hình phân tử cacbon đioxit (CO₂)", new Vector3(9.3f, 1.02f, zf + 0.25f), 1);
            MoleculeModel(g, "Mô hình phân tử metan (CH₄)", new Vector3(9.95f, 1.02f, zf + 0.25f), 2);
            MoleculeModel(g, "Mô hình phân tử benzen (C₆H₆)", new Vector3(10.5f, 1.02f, zf + 0.25f), 3);
        }

        /// <summary>Tủ khay nhựa nhiều màu (kiểu tủ khay trường học): 3 cột × 5 tầng khay. pos: giữa mép tủ sát tường.</summary>
        static void TrayUnit(Transform p, Vector3 pos, int seed)
        {
            var g = Group("Tủ khay nhiều màu", p, pos);
            const float w = 1.1f, d = 0.46f, h = 0.98f;
            Box(g, "Thân tủ", new Vector3(0, h / 2, d / 2), new Vector3(w, h, d), cabinet);
            Box(g, "Mặt tủ gỗ", new Vector3(0, h + 0.012f, d / 2), new Vector3(w + 0.02f, 0.024f, d + 0.02f), oak);
            var dark = Mat("KheKhay", new Color(0.12f, 0.12f, 0.13f), 0.3f);
            for (int col = 0; col < 3; col++)
                for (int row = 0; row < 5; row++)
                {
                    float x = -w / 2 + 0.05f + (col + 0.5f) * (w - 0.1f) / 3f, y = 0.1f + row * 0.17f;
                    var c = PosterColors[(col * 5 + row * 2 + seed * 3) % PosterColors.Length];
                    Box(g, "Khay", new Vector3(x, y + 0.07f, d + 0.004f), new Vector3((w - 0.1f) / 3f - 0.03f, 0.14f, 0.012f), Mat("Khay" + ColorUtility.ToHtmlStringRGB(c), c, 0.55f), false);
                    Box(g, "Tay kéo khay", new Vector3(x, y + 0.1f, d + 0.011f), new Vector3(0.1f, 0.022f, 0.004f), dark, false);
                }
            Info(g, "Tủ khay đồ dùng", "Mỗi nhóm một màu khay: kính bảo hộ, giấy lọc, que khuấy, nhãn dán, bút viết kính.");
        }

        /// <summary>Kệ sách gỗ thấp với các bìa hồ sơ nhiều màu (sổ thí nghiệm của các nhóm).</summary>
        static void Bookshelf(Transform p, Vector3 pos)
        {
            var g = Group("Kệ sổ thí nghiệm", p, pos);
            const float w = 1.3f, d = 0.34f, h = 0.98f;
            Box(g, "Hông trái", new Vector3(-w / 2 + 0.01f, h / 2, d / 2), new Vector3(0.02f, h, d), oak);
            Box(g, "Hông phải", new Vector3(w / 2 - 0.01f, h / 2, d / 2), new Vector3(0.02f, h, d), oak);
            foreach (float y in new[] { 0.02f, 0.5f, h - 0.01f })
                Box(g, "Tầng kệ", new Vector3(0, y, d / 2), new Vector3(w, 0.02f, d), oak);
            Box(g, "Tấm sau", new Vector3(0, h / 2, 0.01f), new Vector3(w, h, 0.01f), cabinet, false);
            for (int shelf = 0; shelf < 2; shelf++)
            {
                float x = -w / 2 + 0.05f;
                for (int i = 0; x < w / 2 - 0.08f; i++)
                {
                    float bw = 0.045f + 0.012f * ((i * 3 + shelf) % 3);
                    var c = PosterColors[(i * 2 + shelf * 3) % PosterColors.Length];
                    Box(g, "Bìa hồ sơ", new Vector3(x + bw / 2, 0.03f + shelf * 0.48f + 0.16f, d / 2), new Vector3(bw - 0.006f, 0.32f, 0.25f), Mat("Bia" + ColorUtility.ToHtmlStringRGB(c), c, 0.35f), false);
                    x += bw;
                }
            }
            Info(g, "Kệ sổ thí nghiệm", "Mỗi nhóm một bìa hồ sơ: phiếu thí nghiệm, số liệu, ảnh kết quả.");
        }

        /// <summary>Mô hình phân tử dạng que – bi: 0 H₂O, 1 CO₂, 2 CH₄, 3 benzen. pos: điểm trên mặt tủ.</summary>
        static void MoleculeModel(Transform p, string name, Vector3 pos, int kind)
        {
            var g = Group(name, p, pos);
            var o = Mat("NguyenTuOxi", new Color(0.9f, 0.15f, 0.12f), 0.55f);
            var hMat = Mat("NguyenTuHidro", new Color(0.95f, 0.95f, 0.95f), 0.55f);
            var cMat = Mat("NguyenTuCacbon", new Color(0.18f, 0.18f, 0.2f), 0.55f);
            var stick = Mat("QueNoi", new Color(0.75f, 0.76f, 0.78f), 0.5f, 0.3f);
            Cyl(g, "Đế", new Vector3(0, 0.008f, 0), 0.06f, 0.016f, darkMetal);
            Cyl(g, "Trụ", new Vector3(0, 0.06f, 0), 0.005f, 0.1f, stick);
            var c0 = new Vector3(0, 0.17f, 0);
            void Atom(Vector3 at, float r, Material m) => NoShadow(Ball(g, "Nguyên tử", at, Vector3.one * r * 2, m));
            void Bond(Vector3 a, Vector3 b)
            {
                var mid = (a + b) / 2;
                var dir = b - a;
                var go = Cyl(g, "Liên kết", mid, 0.006f, dir.magnitude, stick);
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                NoShadow(go);
            }
            switch (kind)
            {
                case 0:
                {
                    Atom(c0, 0.035f, o);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var hp = c0 + new Vector3(s * Mathf.Sin(52.25f * Mathf.Deg2Rad), -Mathf.Cos(52.25f * Mathf.Deg2Rad), 0) * 0.07f;
                        Bond(c0, hp);
                        Atom(hp, 0.022f, hMat);
                    }
                    break;
                }
                case 1:
                {
                    Atom(c0, 0.03f, cMat);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var op = c0 + new Vector3(s * 0.075f, 0, 0);
                        Bond(c0, op);
                        Atom(op, 0.033f, o);
                    }
                    break;
                }
                case 2:
                {
                    Atom(c0, 0.03f, cMat);
                    var dirs = new[] { new Vector3(1, 1, 1), new Vector3(1, -1, -1), new Vector3(-1, 1, -1), new Vector3(-1, -1, 1) };
                    foreach (var dv in dirs)
                    {
                        var hp = c0 + dv.normalized * 0.065f;
                        Bond(c0, hp);
                        Atom(hp, 0.02f, hMat);
                    }
                    break;
                }
                default:
                {
                    var ring = new Vector3[6];
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Tau * i / 6f;
                        ring[i] = c0 + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.06f;
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        Bond(ring[i], ring[(i + 1) % 6]);
                        var hp = c0 + (ring[i] - c0) * 1.75f;
                        Bond(ring[i], hp);
                        Atom(ring[i], 0.018f, cMat);
                        Atom(hp, 0.013f, hMat);
                    }
                    break;
                }
            }
            Info(g, name, "Mô hình que – bi: đỏ là oxi, đen là cacbon, trắng là hiđro. Góc liên kết như phân tử thật.");
        }

        // =====================================================================
        // Sàn: lối đi
        // =====================================================================

        /// <summary>Lối đi giữa các hàng bàn và lối dọc trước khu giáo viên (vinyl xám xanh, viền xanh ngọc), không chồng lên nhau.</summary>
        static void LabWalkways(Transform lab)
        {
            var g = Group("Lối đi trên sàn", lab);
            const float y = 0.002f, t = 0.004f;
            var edge = Mat("VienLoiDi", zoneLab, 0.4f);
            foreach (float z in new[] { 5.05f, 9.95f })
            {
                Box(g, "Lối đi ngang", new Vector3(11.75f, y, z), new Vector3(15.3f, t, 1.0f), floorGuide, false);
                foreach (float e in new[] { -0.52f, 0.52f })
                    Box(g, "Viền lối đi", new Vector3(11.75f, y, z + e), new Vector3(15.3f, t, 0.04f), edge, false);
            }
            Box(g, "Lối đi dọc", new Vector3(3.6f, y, 7.5f), new Vector3(1.0f, t, 13.4f), floorGuide, false);
            foreach (float e in new[] { -0.52f, 0.52f })
                Box(g, "Viền lối đi", new Vector3(3.6f + e, y, 7.5f), new Vector3(0.04f, t, 13.4f), edge, false);
            foreach (Transform c in g) NoShadow(c.gameObject);
        }

        // =====================================================================
        // Hành lang: mảng màu quanh cửa, biển song ngữ
        // =====================================================================

        /// <summary>
        /// Mảng màu nhận diện quanh một cửa ở mặt tường hành lang (trái, phải, trên lỗ cửa) và biển phòng song ngữ phía trên.
        /// wallZ: tâm tường; side: mặt tường phía hành lang (+1 / -1 theo z).
        /// </summary>
        static void DoorZoneGraphic(Transform p, float a, float b, float wallZ, int side, Color color, string vi, string en)
        {
            var g = Group("Mảng màu cửa " + vi, p);
            var m = Mat("MangMau" + ColorUtility.ToHtmlStringRGB(color), color, 0.35f);
            float z = wallZ + side * (T / 2 + 0.004f), w = b - a;
            float l0 = a - w * 0.5f, r1 = b + w * 0.5f;
            Box(g, "Mảng trái", new Vector3((l0 + a) / 2, 1.4f, z), new Vector3(a - l0, 2.8f, 0.006f), m, false);
            Box(g, "Mảng phải", new Vector3((b + r1) / 2, 1.4f, z), new Vector3(r1 - b, 2.8f, 0.006f), m, false);
            Box(g, "Mảng trên", new Vector3((a + b) / 2, 2.55f, z), new Vector3(w, 0.5f, 0.006f), m, false);
            var sign = Group("Biển phòng " + vi, g, new Vector3((a + b) / 2, 2.98f, wallZ + side * (T / 2 + 0.03f)), side > 0 ? 0f : 180f);
            Box(sign, "Biển", Vector3.zero, new Vector3(Mathf.Max(2.0f, w + 0.6f), 0.36f, 0.03f), accentDark, false);
            Box(sign, "Dải màu", new Vector3(0, -0.17f, 0.005f), new Vector3(Mathf.Max(2.0f, w + 0.6f), 0.025f, 0.03f), m, false);
            Label(sign, vi, new Vector3(0, 0.055f, 0.017f), Vector3.forward, 0.075f, Color.white);
            Label(sign, en, new Vector3(0, -0.08f, 0.017f), Vector3.forward, 0.04f, Color.Lerp(color, Color.white, 0.55f));
        }
    }
}
