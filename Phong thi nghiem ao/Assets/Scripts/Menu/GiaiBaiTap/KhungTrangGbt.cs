using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Menu.Dashboard;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.Menu.Dashboard.MenuKit;

namespace Khkt.Menu
{
    /// <summary>
    /// Khung trang chung của nhánh "Giải bài tập" (19/11), lấy bố cục app web CHEMISTRY:
    ///   • trang cuộn dọc, lề hai bên cố định, khối xếp từ trên xuống (nhãn nhỏ → tiêu đề rất lớn → mô tả → các mục 01/02/03);
    ///   • thanh phụ dính trên đầu trang công cụ: nút "‹ tên công cụ" + các bước ngang (01 Nhập đề · 02 Tuỳ chọn · 03 Kết quả),
    ///     bấm bước thì cuộn tới mục đó, bước đang xem tô màu nhấn;
    ///   • thẻ trắng bo 20, viền tóc, nút chính màu nhấn VERA (CHEMISTRY dùng nút đen).
    /// Màu/phông lấy từ MenuKit/UiTheme nên theo đủ 3 nền Trắng / Đen / Xanh mực.
    /// </summary>
    public class TrangCuon : MonoBehaviour
    {
        public const float Le = 150f;          // lề trái/phải nội dung (đơn vị canvas 1920 rộng)
        public const float DinhThanhPhu = 76f;  // chiều cao thanh phụ dính
        // bo góc vừa phải (19/11, góp ý: không dùng dạng viên thuốc): đơn vị canvas 1920 rộng ≈ ×0,75 ở màn 1440
        public const float BoNho = 8f, BoNut = 12f, BoThe = 16f, BoTheLon = 20f;

        public RectTransform Goc, NoiDung;
        RectTransform vp;
        public InputField ODeDay { get; private set; }
        public ScrollRect Cuon;
        public float Y;                         // đỉnh khối kế tiếp (âm dần)
        public readonly List<RectTransform> LanLuot = new List<RectTransform>();
        readonly List<(float y, Button nut, Text chu)> buoc = new List<(float, Button, Text)>();
        int buocDang = -1;

        /// <summary>Trang cuộn trong vùng cha. coThanhPhu: chừa chỗ cho thanh phụ dính ở đỉnh.</summary>
        public static TrangCuon Tao(RectTransform cha, string ten, bool coThanhPhu)
        {
            var g = Full(cha, ten);
            var t = g.gameObject.AddComponent<TrangCuon>();
            t.Goc = g;
            var vp = Stretch(g, "Khung nhìn", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, coThanhPhu ? -DinhThanhPhu : 0));
            vp.gameObject.AddComponent<RectMask2D>();
            var bat = vp.gameObject.AddComponent<Image>();               // nhận cuộn chuột ở chỗ trống
            bat.color = new Color(1, 1, 1, 0.001f);
            var nd = new GameObject("Nội dung", typeof(RectTransform)).GetComponent<RectTransform>();
            nd.SetParent(vp, false);
            nd.anchorMin = new Vector2(0, 1); nd.anchorMax = new Vector2(1, 1); nd.pivot = new Vector2(0.5f, 1f);
            nd.anchoredPosition = Vector2.zero; nd.sizeDelta = new Vector2(0, 1000);
            var sr = g.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = nd; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 42f; sr.inertia = true; sr.decelerationRate = 0.12f;
            t.NoiDung = nd; t.Cuon = sr; t.vp = vp;
            t.Y = -36f;
            g.gameObject.SetActive(false);
            return t;
        }

        /// <summary>Khối mới rộng hết nội dung (trừ lề), cao h, đặt ở Y rồi dời Y xuống h + cach.</summary>
        public RectTransform Khoi(string ten, float h, float cach = 0f)
        {
            var rt = Stretch(NoiDung, ten, new Vector2(0, 1), new Vector2(1, 1), new Vector2(Le, Y - h), new Vector2(-Le, Y));
            Y -= h + cach;
            return rt;
        }

        public void ChotChieuCao(float duoi = 60f) => NoiDung.sizeDelta = new Vector2(0, -Y + duoi);

        public void LenDinh() { NoiDung.anchoredPosition = Vector2.zero; Cuon.velocity = Vector2.zero; }

        public void CuonToi(float y)
        {
            float toiDa = Mathf.Max(0, NoiDung.rect.height - ((RectTransform)Cuon.viewport).rect.height);
            NoiDung.anchoredPosition = new Vector2(0, Mathf.Clamp(-y - 12f, 0, toiDa));
            Cuon.velocity = Vector2.zero;
        }

        // ------------------------------------------------------------------ thanh phụ dính (nút quay lại + bước ngang)
        public void ThanhPhu(string ten, Action quayLai, string[] cacBuoc)
        {
            var thanh = Stretch(Goc, "Thanh phụ", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -DinhThanhPhu), Vector2.zero);
            var vach = Fill(thanh, "Vạch dưới", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(10, 1), Hair, 0);
            vach.rectTransform.anchorMax = new Vector2(1, 0); vach.rectTransform.sizeDelta = new Vector2(0, 1);
            // viên nang trắng chứa nút quay lại + các bước
            float rongBuoc = 0;
            foreach (var b in cacBuoc) rongBuoc += 40 + b.Length * 9.5f + 22;
            float rong = 32 + ten.Length * 10f + 60 + rongBuoc + 10;
            var vien = GlassKit.Rect(thanh, "Viên", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(Le - 6, 0), new Vector2(rong, 52));
            Shadow(vien, "Bóng", 6f, 14f, -1f, UiTheme.IsLight ? 0.06f : 0.3f);
            Paint(Full(vien, "Mặt"), Paper, 14f);
            Ring(vien, "Viền", Hair, 14f);
            var ve = MakeButton(vien, "Nút quay lại " + ten, "‹  " + ten, new Vector2(6, -6), new Vector2(32 + ten.Length * 10f + 30, 40), () => quayLai?.Invoke(), 1, 10f);
            ve.transform.Find("Chữ").GetComponent<Text>().fontSize = 16;
            float x = 6 + 32 + ten.Length * 10f + 30 + 14;
            for (int i = 0; i < cacBuoc.Length; i++)
            {
                float w = 40 + cacBuoc[i].Length * 9.5f;
                int ii = i;
                var nut = MakeButton(vien, "Bước ngang " + (i + 1), "", new Vector2(x, -6), new Vector2(w, 40), () => CuonToi(buoc[ii].y), 2, 10f);
                var chu = nut.transform.Find("Chữ").GetComponent<Text>();
                chu.supportRichText = true;
                chu.fontSize = 15;
                chu.text = $"<size=13>{i + 1:00}</size>  {cacBuoc[i]}";
                buoc.Add((0f, nut, chu));
                x += w + 8;
            }
        }

        /// <summary>Ghi vị trí mục ứng với bước ngang i (gọi lúc dựng mục).</summary>
        public void GhiBuoc(int i, float y) { if (i < buoc.Count) buoc[i] = (y, buoc[i].nut, buoc[i].chu); }

        void Update()
        {
            if (buoc.Count == 0) return;
            float dang = NoiDung.anchoredPosition.y + 160f;
            int k = 0;
            for (int i = 0; i < buoc.Count; i++) if (-buoc[i].y <= dang) k = i;
            if (k == buocDang) return;
            buocDang = k;
            for (int i = 0; i < buoc.Count; i++)
            {
                buoc[i].chu.color = i == k ? Accent : InkDim;
                buoc[i].chu.fontStyle = i == k ? FontStyle.Bold : FontStyle.Normal;
                var mat = Face(buoc[i].nut);
                if (mat != null) mat.color = i == k ? AccentSoft : new Color(1, 1, 1, 0.001f);
            }
        }

        // ------------------------------------------------------------------ chữ
        /// <summary>Nhãn nhỏ viết hoa, giãn chữ (thay cho chữ đơn cách của CHEMISTRY).</summary>
        public static Text NhanNho(Transform cha, string ten, string chu, Vector2 pos, float rong, Color? mau = null)
        {
            var t = GlassKit.Label(cha, ten, GianChu(chu.ToUpperInvariant()), 13, mau ?? InkMute, true, TextAnchor.UpperLeft, pos, new Vector2(rong, 20));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        public static string GianChu(string s) => s.Replace(" · ", "  ·  ");

        /// <summary>Chữ kéo giãn theo bề ngang cha (trừ lề trái x và lề phải xPhai), tự xuống dòng.</summary>
        public static Text ChuRong(Transform cha, string ten, string chu, int co, Color mau, bool dam, float x, float y, float h, float xPhai = 0)
        {
            var t = GlassKit.Label(cha, ten, chu, co, mau, dam, TextAnchor.UpperLeft, new Vector2(x, y), new Vector2(100, h));
            var rt = t.rectTransform;
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(x, y - h);
            rt.offsetMax = new Vector2(-xPhai, y);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.supportRichText = true;
            return t;
        }

        /// <summary>Đầu trang công cụ: nhãn nhỏ, tiêu đề rất lớn, mô tả.</summary>
        public void DauTrang(string nhan, string tieuDe, string moTa)
        {
            var k = Khoi("Đầu trang", 178, 34);
            NhanNho(k, "Nhãn trang", nhan, Vector2.zero, 900);
            var td = GlassKit.Label(k, "Tiêu đề", tieuDe, 54, Ink, true, TextAnchor.UpperLeft, new Vector2(-2, -28), new Vector2(1500, 72));
            td.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextShadow(td, UiTheme.IsLight ? 0.05f : 0.4f, 2f);
            ChuRong(k, "Mô tả", moTa, 18, InkDim, false, 0, -108, 66, 360);
            LanLuot.Add(k);
        }

        /// <summary>Đầu mục đánh số: số nhỏ màu nhấn, tiêu đề mục, dòng phụ. Trả về y của mục (để gắn bước ngang).</summary>
        public float DauMuc(string so, string tieuDe, string phu)
        {
            float y = Y;
            var k = Khoi("Mục " + so, 92, 16);
            GlassKit.Label(k, "Số mục", so, 17, Accent, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(80, 24));
            var td = GlassKit.Label(k, "Tên mục", tieuDe, 32, Ink, true, TextAnchor.UpperLeft, new Vector2(-1, -24), new Vector2(1200, 44));
            td.horizontalOverflow = HorizontalWrapMode.Overflow;
            GlassKit.Label(k, "Phụ mục", phu, 16, InkMute, false, TextAnchor.UpperLeft, new Vector2(0, -68), new Vector2(1400, 24)).horizontalOverflow = HorizontalWrapMode.Overflow;
            LanLuot.Add(k);
            return y;
        }

        /// <summary>Thẻ trắng hết bề ngang khối, không nhấc khi rê chuột.</summary>
        public static RectTransform The(RectTransform khoi, string ten, float y = 0, float h = -0.001f)
        {
            var the = h > 0 ? Card(khoi, ten, 0, 1, y, h, 0, false, BoThe) : Card(khoi, ten, 0, 1, y, -0f, 0, false, BoThe);
            if (h <= 0) { the.offsetMin = new Vector2(0, 0); the.offsetMax = new Vector2(0, y); }
            Body(the).GetComponent<Image>().raycastTarget = true;
            return Body(the);
        }

        /// <summary>Chân trang như CHEMISTRY: tên app trái, các nguồn phải.</summary>
        public RectTransform ChanTrang()
        {
            Y -= 20;
            var k = Khoi("Chân trang", 60);
            var vach = Fill(k, "Vạch", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(10, 1), Hair, 0).rectTransform;
            vach.anchorMax = new Vector2(1, 1); vach.sizeDelta = new Vector2(0, 1);
            var a = GlassKit.Label(k, "Tên", "<b>VERA Lab</b>  –  máy chủ giải bài chạy trên máy của bạn, khóa API chỉ nằm ở máy chủ.", 15, InkMute, false, TextAnchor.MiddleLeft, new Vector2(0, -12), new Vector2(1100, 40));
            a.supportRichText = true;
            var b = GlassKit.Label(k, "Nguồn", GianChu("PubChem · RDKit · Claude · Gemini"), 14, InkMute, false, TextAnchor.MiddleRight, new Vector2(0, -12), new Vector2(600, 40), new Vector2(1, 1));
            b.rectTransform.pivot = new Vector2(1, 1);
            return k;
        }

        // ------------------------------------------------------------------ ô đề dính đáy (như ô chat của trợ lý AI)
        /// <summary>Thanh dính đáy trang: ô đề một dòng + nút chính. Enter hoặc bấm nút thì gui(chữ). Khung cuộn chừa chỗ phía dưới.</summary>
        public InputField ThanhSoan(string goiY, string nhanNut, Action<string> gui)
        {
            const float Cao = 92f;
            var thanh = Stretch(Goc, "Ô đề dính đáy", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, Cao));
            var nen = thanh.gameObject.AddComponent<Image>();
            nen.color = WithAlpha(PaperAlt, 0.96f); nen.raycastTarget = true;
            var vach = Fill(thanh, "Vạch trên", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(10, 1), Hair, 0).rectTransform;
            vach.anchorMax = new Vector2(1, 1); vach.sizeDelta = new Vector2(0, 1);
            var o = TextField(thanh, "Ô đề nhanh", goiY, new Vector2(Le, -18), new Vector2(100, 54), 17);
            var ort = (RectTransform)o.transform; ort.anchorMax = new Vector2(1, 1); ort.offsetMax = new Vector2(-(Le + 200), ort.offsetMax.y);
            void Gui() { var c = (o.text ?? "").Trim(); if (c.Length == 0) return; o.text = ""; gui(c); }
            o.onSubmit.AddListener(_ => Gui());
            var nut = MakeButton(thanh, "Nút Giải nhanh", nhanNut + "  →", new Vector2(-Le, -18), new Vector2(188, 54), Gui, 0, BoNut, new Vector2(1, 1));
            ((RectTransform)nut.transform).pivot = new Vector2(1, 1);
            var g = GlassKit.Label(thanh, "Gợi ý phím", "Enter để giải  ·  ⌘/Ctrl + Enter ở ô nhiều dòng", 12, InkMute, false, TextAnchor.UpperLeft, new Vector2(Le + 4, -76), new Vector2(600, 16));
            g.horizontalOverflow = HorizontalWrapMode.Overflow;
            vp.offsetMin = new Vector2(0, Cao);
            ODeDay = o;
            return o;
        }

        // ------------------------------------------------------------------ trượt mượt (0,45 s, ease-out bậc 3, theo thời gian thật)
        Coroutine truot;
        float yDaDat = -1f;

        /// <summary>Cuộn để mốc yTren (toạ độ nội dung, âm) nằm sát đỉnh khung nhìn. tonTrong: bỏ qua nếu người dùng đã tự cuộn đi.</summary>
        public void TruotToi(float yTren, bool tonTrong = false)
        {
            if (tonTrong && yDaDat >= 0 && Mathf.Abs(NoiDung.anchoredPosition.y - yDaDat) > 60f) return;
            float toiDa = Mathf.Max(0, NoiDung.rect.height - ((RectTransform)Cuon.viewport).rect.height);
            float den = Mathf.Clamp(-yTren - 16f, 0, toiDa);
            if (!isActiveAndEnabled) { NoiDung.anchoredPosition = new Vector2(0, den); yDaDat = den; return; }
            if (truot != null) StopCoroutine(truot);
            truot = StartCoroutine(Truot(den));
        }

        IEnumerator Truot(float den)
        {
            float tu = NoiDung.anchoredPosition.y;
            Cuon.velocity = Vector2.zero;
            const float lau = 0.45f;
            for (float t = 0; t < lau; t += Time.unscaledDeltaTime)
            {
                float u = 1f - Mathf.Pow(1f - t / lau, 3f);
                NoiDung.anchoredPosition = new Vector2(0, Mathf.Lerp(tu, den, u));
                yield return null;
            }
            NoiDung.anchoredPosition = new Vector2(0, den);
            yDaDat = den;
            truot = null;
        }

        // ------------------------------------------------------------------ điều khiển
        /// <summary>Thẻ tab lớn (biểu tượng, tên, dòng phụ) – nhóm chọn một như "Văn bản / Hình ảnh / Chuỗi mẫu".</summary>
        public static TheTab NhomTheTab(RectTransform khoi, string[] ten, string[] phu, string[] kyHieu, Action<int> chon)
        {
            var nhom = new TheTab { chon = chon };
            int n = ten.Length;
            for (int i = 0; i < n; i++)
            {
                int ii = i;
                var the = Card(khoi, "Tab " + ten[i], i / (float)n, (i + 1) / (float)n, 0, 116, 18, true, BoThe);
                MakeCardClickable(the, () => nhom.Chon(ii, true), 2f);
                var than = Body(the);
                var o = Fill(than, "Biểu tượng", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -16), new Vector2(34, 34), Well, 9f);
                Ring(o.transform, "Viền", Hair, 9f);
                GlassKit.Label(o.transform, "Ký hiệu", kyHieu[i], 17, InkDim, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(34, 34));
                GlassKit.Label(than, "Tên", ten[i], 19, Ink, true, TextAnchor.UpperLeft, new Vector2(22, -56), new Vector2(400, 26));
                GlassKit.Label(than, "Phụ", phu[i], 14, InkMute, false, TextAnchor.UpperLeft, new Vector2(22, -82), new Vector2(420, 20)).horizontalOverflow = HorizontalWrapMode.Overflow;
                nhom.the.Add(the);
            }
            nhom.Chon(0, false);
            return nhom;
        }

        public class TheTab
        {
            public readonly List<RectTransform> the = new List<RectTransform>();
            public Action<int> chon;
            public int Dang = -1;

            public void Chon(int i, bool baoTin)
            {
                bool doi = Dang >= 0 && Dang != i;
                Dang = i;
                for (int k = 0; k < the.Count; k++)
                {
                    bool d = k == i;
                    var vien = CardRing(the[k]);
                    var mau = d ? WithAlpha(Accent, 0.85f) : Hair;
                    if (vien != null) vien.color = mau;
                    var hv = the[k].GetComponent<MenuHover>();
                    if (hv != null) hv.ringRest = mau;
                    Body(the[k]).GetComponent<Image>().color = d ? (UiTheme.IsLight ? Hex(0xf5f9ff) : PaperHover) : Paper;
                    var o = Body(the[k]).Find("Biểu tượng")?.GetComponent<Image>();
                    if (o != null) o.color = d ? AccentSoft : Well;
                    var kh = Body(the[k]).Find("Biểu tượng/Ký hiệu")?.GetComponent<Text>();
                    if (kh != null) kh.color = d ? Accent : InkDim;
                }
                if (doi && i >= 0 && i < the.Count && the[i] != null && the[i].gameObject.activeInHierarchy)
                    MenuAnim.PulseClick(the[i], 0.965f, 1.015f, 0.20f);
                if (baoTin) chon?.Invoke(i);
            }
        }

        /// <summary>Ô nhập nhiều dòng (TextField của MenuKit đổi sang nhiều dòng, chữ canh trên trái).</summary>
        public static InputField ONhieuDong(Transform cha, string ten, string goiY, Vector2 pos, Vector2 co, int coChu = 17)
        {
            var o = TextField(cha, ten, goiY, pos, co, coChu);
            o.lineType = InputField.LineType.MultiLineNewline;
            o.characterLimit = 4000;
            foreach (var t in new[] { o.textComponent, o.placeholder as Text })
            {
                if (t == null) continue;
                t.alignment = TextAnchor.UpperLeft;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                var rt = t.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(18, 12); rt.offsetMax = new Vector2(-18, -14);
            }
            return o;
        }

        /// <summary>Chip viên nang nhỏ (ví dụ, gợi ý). Chiều rộng theo chữ.</summary>
        public static Button Chip(Transform cha, string ten, string chu, Vector2 pos, Action bam, int coChu = 14)
        {
            float w = Mathf.Min(620, 34 + chu.Length * coChu * 0.6f);
            var b = MakeButton(cha, ten, chu, pos, new Vector2(w, 36), bam, 1, BoNut);
            var t = b.transform.Find("Chữ").GetComponent<Text>();
            t.fontSize = coChu; t.color = InkDim; t.fontStyle = FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;          // chip không bao giờ gãy 2 dòng
            return b;
        }

        /// <summary>Xếp chip xuống dòng khi hết chỗ; trả về chiều cao đã dùng.</summary>
        public static float XepChip(Transform cha, IList<(string ten, string chu, Action bam)> ds, float x0, float y0, float rong, int coChu = 14)
        {
            float x = x0, y = y0;
            foreach (var (ten, chu, bam) in ds)
            {
                float w = Mathf.Min(620, 34 + chu.Length * coChu * 0.6f);
                if (x > x0 && x + w > rong) { x = x0; y -= 44; }
                Chip(cha, ten, chu, new Vector2(x, y), bam, coChu);
                x += w + 8;
            }
            return y0 - y + 36;
        }

        /// <summary>Huy hiệu viên nang nhỏ (nguồn kết quả, kiểm chứng…).</summary>
        public static RectTransform HuyHieu(Transform cha, string ten, string chu, Vector2 pos, Color nen, Color mauChu)
        {
            float w = 26 + chu.Length * 8.2f;
            var rt = GlassKit.Rect(cha, ten, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(w, 28));
            Paint(Full(rt, "Mặt"), nen, BoNho);
            var t = GlassKit.Label(rt, "Chữ", chu, 13, mauChu, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w, 28));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return rt;
        }

        /// <summary>Công tắc gọn (rãnh 42×24 bo 12, núm 18) thay cho công tắc viên nang lớn của MenuKit.</summary>
        public static Toggle CongTac(Transform cha, string ten, string nhan, Vector2 pos, Vector2 co, bool batDau, Action<bool> doi)
        {
            var t = Switch(cha, ten, nhan, pos, co, batDau, doi);
            var sw = t.GetComponent<MenuSwitch>();
            var ranh = sw.track.rectTransform;
            ranh.sizeDelta = new Vector2(42, 24);
            sw.track.sprite = Round(12f);
            if (sw.trackRing != null) sw.trackRing.sprite = RingSprite(12f);
            sw.knob.sizeDelta = new Vector2(18, 18);
            foreach (var img in sw.knob.GetComponentsInChildren<Image>(true))
                img.sprite = img.name == "Bóng" ? ShadowSprite(3f, 9f) : Round(9f);
            sw.travel = 9f;
            var lbl = t.transform.Find("Nhãn") as RectTransform;
            if (lbl != null) lbl.sizeDelta = new Vector2(co.x - 60, lbl.sizeDelta.y);
            return t;
        }

        /// <summary>Nút "?" nhỏ: bấm thì hiện/ẩn dòng giải thích ngay dưới nhãn.</summary>
        public static void NutHoi(Transform cha, string ten, Vector2 pos, Text giaiThich)
        {
            var b = MakeButton(cha, "Nút ? " + ten, "?", pos, new Vector2(22, 22), () =>
            {
                bool mo = !giaiThich.gameObject.activeSelf;
                giaiThich.gameObject.SetActive(mo);
                if (mo) MenuAnim.Pop(giaiThich.rectTransform, 0.95f, 0.18f);
            }, 1, BoNut);
            var t = b.transform.Find("Chữ").GetComponent<Text>();
            t.fontSize = 13; t.color = InkMute;
            giaiThich.gameObject.SetActive(false);
        }
    }
}
