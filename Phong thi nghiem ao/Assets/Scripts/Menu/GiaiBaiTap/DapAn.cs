using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.CoChe;
using Khkt.Demo;
using Khkt.Menu.Dashboard;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.CoChe.JsonNho;
using static Khkt.Menu.Dashboard.MenuKit;

namespace Khkt.Menu
{
    /// <summary>
    /// Luồng đáp án kiểu trợ lý AI (19/11, docs/ngay-19-11/thiet-ke-luong-dap-an.md): mỗi lần giải là một KHỐI gồm câu hỏi
    /// (đề) và thẻ đáp án bên dưới; khối mới nối vào cuối trang, các khối cũ thành lịch sử cuộn được (tối đa 20).
    /// Bấm Giải → khối mới hiện khung xương + tiến trình, trang trượt mượt tới khối; có đáp án → hình (chuỗi vẽ thành hình,
    /// sơ đồ giấy, ảnh 3D tĩnh), kết luận, tóm tắt, hàng nút (Mở lớn, Giải thích, Hỏi tiếp, Xuất PNG, Báo sai) và chip gợi ý.
    /// </summary>
    public class DongThoiGian : MonoBehaviour
    {
        public const int ToiDa = 20;
        TrangCuon t;
        float yDau;
        RectTransform rong, chan;
        public readonly List<KhoiDapAn> Khoi = new List<KhoiDapAn>();

        public static DongThoiGian Tao(TrangCuon t, string tieuDeRong, string goiYRong)
        {
            var d = t.gameObject.AddComponent<DongThoiGian>();
            d.t = t;
            d.yDau = t.Y;
            d.rong = t.Khoi("Khối kết quả", 210, 10);
            var th = TrangCuon.The(d.rong, "Thẻ trống", 0, 210);
            var o = Fill(th, "Biểu tượng", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(52, 52), Well, 12f);
            Ring(o.transform, "Viền", Hair, 12f);
            GlassKit.Label(o.transform, "Ký hiệu", "⬡", 24, InkMute, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(52, 52));
            var a = GlassKit.Label(th, "Tiêu đề", tieuDeRong, 22, Ink, true, TextAnchor.UpperCenter, new Vector2(0, -102), new Vector2(900, 32), new Vector2(0.5f, 1));
            a.rectTransform.pivot = new Vector2(0.5f, 1);
            var b = GlassKit.Label(th, "Hướng dẫn", goiYRong, 16, InkDim, false, TextAnchor.UpperCenter, new Vector2(0, -140), new Vector2(860, 48), new Vector2(0.5f, 1));
            b.rectTransform.pivot = new Vector2(0.5f, 1); b.supportRichText = true; b.horizontalOverflow = HorizontalWrapMode.Wrap;
            d.chan = t.ChanTrang();
            d.XepLai();
            return d;
        }

        public float RongNoiDung => Mathf.Max(600f, t.NoiDung.rect.width - 2 * TrangCuon.Le);

        /// <summary>Thêm khối mới (câu hỏi + khung xương), xếp lại trang và trượt tới khối.</summary>
        public KhoiDapAn Them(string cauHoi, Texture anhDe)
        {
            if (Khoi.Count >= ToiDa) { Destroy(Khoi[0].gameObject); Khoi.RemoveAt(0); }
            var k = KhoiDapAn.Tao(t.NoiDung, this, cauHoi, anhDe, Khoi.Count + 1);
            Khoi.Add(k);
            rong.gameObject.SetActive(false);
            XepLai();
            MenuAnim.Enter(k.Goc, 0f, 16f, 0.35f);
            t.TruotToi(k.YTren);
            return k;
        }

        /// <summary>Xếp các khối từ trên xuống (gọi lại khi một khối đổi chiều cao), đặt chân trang, cập nhật chiều cao cuộn.</summary>
        public void XepLai()
        {
            float y = yDau;
            if (Khoi.Count == 0)
            {
                rong.gameObject.SetActive(true);
                rong.offsetMin = new Vector2(TrangCuon.Le, y - 210); rong.offsetMax = new Vector2(-TrangCuon.Le, y);
                y -= 220;
            }
            foreach (var k in Khoi)
            {
                float h = k.ChieuCao;
                k.Goc.offsetMin = new Vector2(TrangCuon.Le, y - h);
                k.Goc.offsetMax = new Vector2(-TrangCuon.Le, y);
                k.YTren = y;
                y -= h + 34;
            }
            y -= 20;
            chan.offsetMin = new Vector2(TrangCuon.Le, y - 60); chan.offsetMax = new Vector2(-TrangCuon.Le, y);
            t.Y = y - 60;
            t.ChotChieuCao();
        }

        /// <summary>Đáp án vừa về: trượt tới đầu hình nếu người dùng chưa tự cuộn đi chỗ khác.</summary>
        public void TruotToiDapAn(KhoiDapAn k) => t.TruotToi(k.YTren, true);
    }

    /// <summary>Một lần giải: câu hỏi (đề) + thẻ đáp án. Trạng thái: đang giải (khung xương nhấp nháy) → có đáp án / lỗi.</summary>
    public class KhoiDapAn : MonoBehaviour
    {
        public RectTransform Goc { get; private set; }
        public float YTren;
        public bool CoDapAn { get; private set; }
        public bool Loi { get; private set; }
        public string TienTrinh => tienTrinh != null ? tienTrinh.text : "";
        DongThoiGian dong;
        RectTransform the, xuong, hoiTiep;
        public RectTransform Hinh { get; private set; }
        Text tienTrinh, chuHoiTiep;
        InputField oHoi;
        float caoHinh, caoTomTat, caoHoiTiep;
        bool coChip;
        readonly List<Image> thanhXuong = new List<Image>();
        Action<string, Action<string>> hoi;
        const float CaoCauHoi = 64f;

        public float ChieuCao => CaoCauHoi + 14 + (CoDapAn ? 24 + caoHinh + 18 + 34 + caoTomTat + 18 + 44 + 24 + caoHoiTiep : Loi ? 120 : 196) + (coChip ? 52 : 0);

        public static KhoiDapAn Tao(RectTransform noiDung, DongThoiGian dong, string cauHoi, Texture anhDe, int so)
        {
            var g = MenuKit.Stretch(noiDung, "Khối đáp án " + so, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            var k = g.gameObject.AddComponent<KhoiDapAn>();
            k.Goc = g; k.dong = dong;
            // câu hỏi: nhãn nhỏ + bong bóng nền Well (chữ đậm), ảnh đề thu nhỏ nếu có
            TrangCuon.NhanNho(g, "Nhãn câu hỏi", $"Lần giải {so}", new Vector2(0, 0), 300, Accent);
            var bong = Fill(g, "Câu hỏi", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -22), new Vector2(100, 42), Well, 12f);
            Ring(bong.transform, "Viền", Hair, 12f);
            float x = 16;
            if (anhDe != null)
            {
                var ri = new GameObject("Ảnh đề", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                ri.transform.SetParent(bong.transform, false);
                ri.texture = anhDe; ri.raycastTarget = false;
                var rr = ri.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0, 0.5f); rr.pivot = new Vector2(0, 0.5f);
                rr.anchoredPosition = new Vector2(6, 0); rr.sizeDelta = new Vector2(30f * anhDe.width / Mathf.Max(1, anhDe.height), 30);
                x = 14 + rr.sizeDelta.x;
            }
            string ch = cauHoi.Length > 130 ? cauHoi.Substring(0, 128) + "…" : cauHoi;
            var chu = GlassKit.Label(bong.transform, "Chữ", ch, 17, Ink, true, TextAnchor.MiddleLeft, new Vector2(x, 0), new Vector2(1400, 42));
            chu.horizontalOverflow = HorizontalWrapMode.Overflow;
            bong.rectTransform.sizeDelta = new Vector2(Mathf.Min(dong.RongNoiDung, x + GlassKit.Width(chu, ch) + 22), 42);

            var card = Card(g, "Thẻ đáp án", 0, 1, -(CaoCauHoi + 14), -0.001f, 0, false, TrangCuon.BoThe);
            card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(1, 1); card.pivot = new Vector2(0.5f, 1f);
            card.offsetMin = Vector2.zero; card.offsetMax = new Vector2(0, -(CaoCauHoi + 14));
            k.the = Body(card);
            k.the.GetComponent<Image>().raycastTarget = true;
            // khung xương: 3 thanh nhấp nháy + dòng tiến trình
            k.xuong = MenuKit.Full(k.the, "Khung xương");
            float[] rong = { 0.62f, 0.9f, 0.45f }, yx = { -24f, -96f, -130f };
            for (int i = 0; i < 3; i++)
            {
                var b = Fill(k.xuong, "Thanh " + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, yx[i]), new Vector2(10, i == 0 ? 56 : 22), Well, 8f);
                b.rectTransform.anchorMax = new Vector2(rong[i], 1);
                b.rectTransform.sizeDelta = new Vector2(-24, b.rectTransform.sizeDelta.y);
                k.thanhXuong.Add(b);
            }
            k.tienTrinh = GlassKit.Label(k.xuong, "Tiến trình", "Đang giải…", 15, InkMute, false, TextAnchor.UpperLeft, new Vector2(24, -166), new Vector2(1400, 24));
            k.tienTrinh.supportRichText = true;
            return k;
        }

        void Update()
        {
            if (CoDapAn || Loi || thanhXuong.Count == 0) return;
            float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f);       // nhấp nháy, không cấp phát
            for (int i = 0; i < thanhXuong.Count; i++)
            {
                var c = thanhXuong[i].color;
                c.a = Mathf.Lerp(0.5f, 1f, (a + i * 0.15f) % 1f);
                thanhXuong[i].color = c;
            }
        }

        public void DatTienTrinh(string chu) { if (tienTrinh != null && tienTrinh.text != chu) tienTrinh.text = chu; }

        public void DatLoi(string chu)
        {
            Loi = true;
            xuong.gameObject.SetActive(false);
            var t = TrangCuon.ChuRong(the, "Lỗi", "<b>Chưa giải được.</b>  " + chu, 16, Error, false, 24, -24, 80, 24);
            t.supportRichText = true;
            dong.XepLai();
            MenuAnim.Enter(t.rectTransform, 0f, 8f, 0.3f);
        }

        /// <summary>
        /// Có đáp án: veHinh(khung, rộng) vẽ hình vào khung và trả về chiều cao; nút: (tên đối tượng, chữ, kiểu 0 = chính / 1 = phụ, việc).
        /// </summary>
        public void DatDapAn(Func<RectTransform, float, float> veHinh, string ketLuan, string huyHieu, bool kiemChung, string tomTat,
                             IList<(string ten, string chu, int kieu, Action bam)> nut, IList<(string chu, Action bam)> chip,
                             Action<string, Action<string>> hoiFn)
        {
            CoDapAn = true;
            hoi = hoiFn;
            xuong.gameObject.SetActive(false);
            float rong = dong.RongNoiDung - 48;
            Hinh = MenuKit.Stretch(the, "Hình đáp án", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -24 - 10), new Vector2(-24, -24));
            caoHinh = Mathf.Max(60, veHinh(Hinh, rong));
            Hinh.offsetMin = new Vector2(24, -24 - caoHinh);
            float y = -24 - caoHinh - 18;
            var kl = GlassKit.Label(the, "Kết luận", ketLuan, 20, Ink, true, TextAnchor.UpperLeft, new Vector2(24, y), new Vector2(1200, 30));
            kl.horizontalOverflow = HorizontalWrapMode.Overflow; kl.supportRichText = true;
            if (!string.IsNullOrEmpty(huyHieu))
            {
                var hh = TrangCuon.HuyHieu(the, "Huy hiệu", huyHieu, new Vector2(-24, y), kiemChung ? BioSoft : WarnSoft, kiemChung ? Bio : Warn);
                hh.anchorMin = hh.anchorMax = new Vector2(1, 1); hh.pivot = new Vector2(1, 1); hh.anchoredPosition = new Vector2(-24, y);
            }
            y -= 34;
            caoTomTat = string.IsNullOrEmpty(tomTat) ? 0 : 48;
            if (caoTomTat > 0) TrangCuon.ChuRong(the, "Tóm tắt", tomTat, 15, InkDim, false, 24, y, 46, 24).verticalOverflow = VerticalWrapMode.Truncate;
            y -= caoTomTat + 18;
            float x = 24;
            foreach (var (ten, chu, kieu, bam) in nut)
            {
                float w = 30 + chu.Length * 8.6f;
                var b = MakeButton(the, ten, chu, new Vector2(x, y), new Vector2(w, 40), bam, kieu, TrangCuon.BoNut);
                b.transform.Find("Chữ").GetComponent<Text>().fontSize = 15;
                x += w + 8;
            }
            hoiTiep = MenuKit.Stretch(the, "Hỏi tiếp", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, y - 44 - 24 - 140), new Vector2(-24, y - 44 - 16));
            oHoi = TextField(hoiTiep, "Ô hỏi tiếp", "Hỏi tiếp về đáp án này… (Enter để gửi)", new Vector2(0, 0), new Vector2(100, 44), 16);
            var ort = (RectTransform)oHoi.transform; ort.anchorMax = new Vector2(1, 1); ort.offsetMax = new Vector2(-110, ort.offsetMax.y);
            oHoi.onSubmit.AddListener(_ => GuiHoi(oHoi.text));
            var gui = MakeButton(hoiTiep, "Nút Gửi hỏi tiếp", "Gửi", new Vector2(0, 0), new Vector2(98, 44), () => GuiHoi(oHoi.text), 0, TrangCuon.BoNut, new Vector2(1, 1));
            ((RectTransform)gui.transform).pivot = new Vector2(1, 1);
            chuHoiTiep = TrangCuon.ChuRong(hoiTiep, "Trả lời", "", 15, InkDim, false, 0, -54, 86, 0);
            chuHoiTiep.verticalOverflow = VerticalWrapMode.Truncate;
            hoiTiep.gameObject.SetActive(false);
            coChip = chip != null && chip.Count > 0;
            if (coChip)
            {
                var hang = MenuKit.Stretch(Goc, "Gợi ý tiếp", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 40));
                var ds = new List<(string, string, Action)>();
                foreach (var (chu, bam) in chip) ds.Add(("Gợi ý " + chu, chu, bam));
                TrangCuon.XepChip(hang, ds, 0, -2, rong, 14);
            }
            dong.XepLai();
            // các phần hiện lần lượt cách 60 ms
            int i = 0;
            foreach (Transform c in the)
                if (c.gameObject.activeSelf && c != xuong) MenuAnim.Enter((RectTransform)c, (i++) * 0.06f, 10f, 0.32f);
            dong.TruotToiDapAn(this);
        }

        /// <summary>Mở / đóng ô hỏi tiếp ngay dưới hàng nút.</summary>
        public void BatHoiTiep(string cauHoi = null)
        {
            if (hoiTiep == null || hoi == null) return;
            bool mo = !hoiTiep.gameObject.activeSelf || cauHoi != null;
            hoiTiep.gameObject.SetActive(mo);
            caoHoiTiep = mo ? 150 : 0;
            dong.XepLai();
            if (mo) { MenuAnim.Enter(hoiTiep, 0f, 8f, 0.28f); oHoi.ActivateInputField(); }
            if (cauHoi != null) GuiHoi(cauHoi);
        }

        public string TraLoiCuoi { get; private set; } = "";

        void GuiHoi(string cau)
        {
            cau = (cau ?? "").Trim();
            if (cau.Length == 0 || hoi == null) return;
            oHoi.text = "";
            chuHoiTiep.text = $"<b>{cau}</b>\n<color=#{ColorUtility.ToHtmlStringRGB(InkMute)}>Đang trả lời…</color>";
            hoi(cau, tl =>
            {
                TraLoiCuoi = tl;
                chuHoiTiep.text = $"<b>{cau}</b>\n{tl}";
                MenuAnim.Enter(chuHoiTiep.rectTransform, 0f, 6f, 0.3f);
            });
        }
    }

    /// <summary>Bảng lớn ~75% × 80% màn ở giữa, nền mờ phía sau; đóng bằng ✕, Esc hoặc bấm nền. Nội dung dựng lại mỗi lần mở.</summary>
    public class BangLon : MonoBehaviour
    {
        RectTransform goc, khung, than;
        Text tieuDe;
        public bool DangMo => goc != null && goc.gameObject.activeSelf;
        public RectTransform Than => than;

        public static BangLon Tao(RectTransform cha)
        {
            var g = MenuKit.Full(cha, "Bảng lớn");
            var b = g.gameObject.AddComponent<BangLon>();
            b.goc = g;
            var nen = g.gameObject.AddComponent<Image>();
            nen.color = GlassKit.Scrim(UiTheme.IsLight ? 0.42f : 0.6f);
            nen.raycastTarget = true;
            var bamNen = g.gameObject.AddComponent<Button>();
            bamNen.transition = Selectable.Transition.None;
            bamNen.onClick.AddListener(b.Dong);
            b.khung = MenuKit.Stretch(g, "Khung", new Vector2(0.125f, 0.1f), new Vector2(0.875f, 0.9f), Vector2.zero, Vector2.zero);
            var card = Card(b.khung, "Thẻ bảng lớn", 0, 1, 0, -0.001f, 0, false, TrangCuon.BoTheLon);
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one; card.offsetMin = card.offsetMax = Vector2.zero;
            var th = Body(card);
            th.GetComponent<Image>().raycastTarget = true;
            b.tieuDe = GlassKit.Label(th, "Tiêu đề", "", 24, Ink, true, TextAnchor.MiddleLeft, new Vector2(30, -14), new Vector2(1200, 44));
            b.tieuDe.horizontalOverflow = HorizontalWrapMode.Overflow; b.tieuDe.supportRichText = true;
            var dong = MakeButton(th, "Nút Đóng bảng lớn", "✕  Đóng", new Vector2(-20, -16), new Vector2(110, 40), b.Dong, 1, TrangCuon.BoNut, new Vector2(1, 1));
            ((RectTransform)dong.transform).pivot = new Vector2(1, 1);
            Fill(th, "Vạch", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -72), new Vector2(10, 1), Hair, 0).rectTransform.anchorMax = new Vector2(1, 1);
            b.than = MenuKit.Stretch(th, "Nội dung", Vector2.zero, Vector2.one, new Vector2(30, 24), new Vector2(-30, -88));
            g.gameObject.SetActive(false);
            return b;
        }

        /// <summary>Mở bảng: xoá nội dung cũ, gọi dung(than) dựng nội dung mới.</summary>
        public void Mo(string tieu, Action<RectTransform> dung)
        {
            for (int i = than.childCount - 1; i >= 0; i--) Destroy(than.GetChild(i).gameObject);
            tieuDe.text = tieu;
            goc.gameObject.SetActive(true);
            goc.SetAsLastSibling();
            dung?.Invoke(than);
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, 0f, 1f, 0.2f);
            MenuAnim.Pop(khung);
        }

        public void Dong()
        {
            if (!DangMo) return;
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = false;
            MenuAnim.Fade(g, g.alpha, 0f, 0.18f, 0f, () => goc.gameObject.SetActive(false));
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Dong();
        }

        /// <summary>Vùng cuộn dọc trong bảng (nội dung cao hơn bảng).</summary>
        public static RectTransform VungCuon(RectTransform cha, float caoNoiDung)
        {
            var vp = MenuKit.Full(cha, "Khung nhìn");
            vp.gameObject.AddComponent<RectMask2D>();
            var bat = vp.gameObject.AddComponent<Image>(); bat.color = new Color(1, 1, 1, 0.001f);
            var nd = new GameObject("Nội dung cuộn", typeof(RectTransform)).GetComponent<RectTransform>();
            nd.SetParent(vp, false);
            nd.anchorMin = new Vector2(0, 1); nd.anchorMax = new Vector2(1, 1); nd.pivot = new Vector2(0.5f, 1);
            nd.anchoredPosition = Vector2.zero; nd.sizeDelta = new Vector2(0, caoNoiDung);
            var sr = cha.gameObject.GetComponent<ScrollRect>() ?? cha.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = nd; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 42f;
            return nd;
        }
    }

    /// <summary>Vẽ hình đáp án bằng uGUI (nét, không cần máy chủ vẽ): chuỗi phản ứng, ảnh 3D tĩnh, sơ đồ giấy.</summary>
    public static class HinhDapAn
    {
        /// <summary>Chuỗi: ô chất – mũi tên (điều kiện trên, loại dưới) – ô chất…, tự xuống dòng. Trả về chiều cao đã dùng.</summary>
        public static float Chuoi(RectTransform cha, IList<(string tu, string den, string dk, string ten)> buoc, float rong, float co = 1f)
        {
            if (buoc.Count == 0) return 40;
            float x = 0, y = -10, hang = 96 * co;
            int fs = Mathf.RoundToInt(18 * co);
            void O(string chat, string ten)
            {
                float w = (28 + chat.Length * 10.5f) * co;
                if (x > 0 && x + w > rong) { x = 0; y -= hang; }
                var o = Fill(cha, "Chất " + chat, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y - 20 * co), new Vector2(w, 46 * co), AccentSoft, 10f * co);
                Ring(o.transform, "Viền", WithAlpha(Accent, 0.28f), 10f * co);
                var t = GlassKit.Label(o.transform, "Chữ", chat, fs, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w, 46 * co));
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (!string.IsNullOrEmpty(ten) && co > 1.05f)
                {
                    var tt = GlassKit.Label(cha, "Tên " + chat, ten, Mathf.RoundToInt(13 * co), InkMute, false, TextAnchor.UpperCenter, new Vector2(x - 20, y - 70 * co), new Vector2(w + 40, 20 * co));
                    tt.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
                x += w;
            }
            void MuiTen(string dk)
            {
                string d = string.IsNullOrEmpty(dk) ? "" : (dk.Length > 26 ? dk.Substring(0, 25) + "…" : dk);
                float w = Mathf.Max(90, 30 + d.Length * 7.2f) * co;
                if (x + w + 60 * co > rong) { x = 0; y -= hang; }
                var vach = Fill(cha, "Mũi tên", new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 10 * co, y - 43 * co), new Vector2(w - 26 * co, 2), InkDim, 0);
                vach.raycastTarget = false;
                var dau = GlassKit.Label(cha, "Đầu mũi tên", "▶", Mathf.RoundToInt(13 * co), InkDim, false, TextAnchor.MiddleCenter, new Vector2(x + w - 20 * co, y - 34 * co), new Vector2(14 * co, 18 * co));
                dau.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (d != "")
                {
                    var t = GlassKit.Label(cha, "Điều kiện", d, Mathf.RoundToInt(12 * co), InkMute, false, TextAnchor.LowerCenter, new Vector2(x, y - 6 * co), new Vector2(w, 32 * co));
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
                x += w;
            }
            O(buoc[0].tu, "");
            foreach (var b in buoc) { MuiTen(b.dk); O(b.den, b.ten); }
            return -y + hang - 16 * co;
        }

        /// <summary>Ảnh 3D tĩnh: chiếu nguyên tử (bóng–que) từ một góc đẹp, vẽ xa trước gần sau. Trả về chiều cao.</summary>
        public static float Ba(RectTransform cha, Dictionary<string, object> d, float rong, float cao)
        {
            var nt = L(Lay(d, "nguyen_tu"));
            if (nt.Count == 0) return 40;
            var nen = Fill(cha, "Nền 3D", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(rong, cao), new Color32(0x0b, 0x10, 0x20, 0xff), 12f);
            var q = Quaternion.Euler(18f, -28f, 0f);
            var p = new Vector3[nt.Count];
            var e = new string[nt.Count];
            Vector3 mn = Vector3.one * 1e9f, mx = -Vector3.one * 1e9f;
            for (int i = 0; i < nt.Count; i++)
            {
                var a = D(nt[i]);
                e[i] = S(a, "e", "C");
                p[i] = q * new Vector3(F(Lay(a, "x")), F(Lay(a, "y")), -F(Lay(a, "z")));
                mn = Vector3.Min(mn, p[i]); mx = Vector3.Max(mx, p[i]);
            }
            var tam = (mn + mx) * 0.5f;
            float k = Mathf.Min((rong - 80) / Mathf.Max(0.5f, mx.x - mn.x), (cao - 60) / Mathf.Max(0.5f, mx.y - mn.y));
            k = Mathf.Min(k, 120f);
            Vector2 C(int i) => new Vector2(rong * 0.5f + (p[i].x - tam.x) * k, -cao * 0.5f + (p[i].y - tam.y) * k);
            foreach (var o in L(Lay(d, "lien_ket")))
            {
                var l = D(o);
                int a = I(Lay(l, "a")), b = I(Lay(l, "b"));
                if (a < 0 || b < 0 || a >= p.Length || b >= p.Length) continue;
                Vector2 pa = C(a), pb = C(b), v = pb - pa;
                var rt = GlassKit.Rect(nen.transform, "Liên kết", new Vector2(0, 1), new Vector2(0, 0.5f), pa, new Vector2(v.magnitude, Mathf.Max(3f, k * 0.09f)));
                var im = rt.gameObject.AddComponent<Image>(); im.color = new Color32(0xa7, 0xaf, 0xbd, 0xff); im.raycastTarget = false;
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            }
            var thuTu = new List<int>();
            for (int i = 0; i < p.Length; i++) thuTu.Add(i);
            thuTu.Sort((a, b) => p[b].z.CompareTo(p[a].z));                       // xa (z lớn) vẽ trước
            foreach (var i in thuTu)
            {
                float r = TrinhPhat3D.BK(e[i]) * k * 1.1f;
                var c = Fill(nen.transform, "Nguyên tử " + e[i], new Vector2(0, 1), new Vector2(0.5f, 0.5f), C(i), new Vector2(r * 2, r * 2), TrinhPhat3D.Mau(e[i]), r);
                var sang = Fill(c.transform, "Ánh", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-r * 0.32f, r * 0.32f), new Vector2(r * 0.7f, r * 0.7f), new Color(1, 1, 1, 0.35f), r * 0.35f);
                sang.raycastTarget = false;
                if (e[i] != "C" && e[i] != "H")
                    GlassKit.Label(c.transform, "Ký hiệu", e[i], Mathf.Clamp(Mathf.RoundToInt(r * 0.8f), 11, 20), Color.white, true, TextAnchor.MiddleCenter, new Vector2(-r, r), new Vector2(r * 2, r * 2));
            }
            return cao;
        }

        /// <summary>Sơ đồ giấy của bài cơ chế (SoDoGiay vẽ véc-tơ, nền giấy). Trả về chiều cao.</summary>
        public static SoDoGiay Giay(RectTransform cha, BaiCoChe b, float rong, float cao)
        {
            var nen = Fill(cha, "Tờ giấy", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(rong, cao), SoDoGiay.MauGiay, 10f);
            if (cao <= 0) { var r = nen.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }   // phủ kín khung cha
            Ring(nen.transform, "Viền", new Color(0.4f, 0.32f, 0.1f, 0.18f), 10f);
            var go = new GameObject("Sơ đồ", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(nen.transform, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(18, 14); rt.offsetMax = new Vector2(-18, -14);
            var g = go.AddComponent<SoDoGiay>();
            g.raycastTarget = false;
            g.Dat(b.SoDo);
            return g;
        }
    }
}
