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
            var nhom = MenuAnim.Group(k.Goc); MenuAnim.Fade(nhom, 0f, 1f, 0.3f);   // chỉ mờ dần: trượt vị trí sẽ đè khi trang xếp lại
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
        float caoHinh, caoTomTat, caoHoiTiep, caoLoiGiai, cao3D, yNut;
        RectTransform khu3D;
        public bool Khu3DMo => khu3D != null && khu3D.gameObject.activeSelf;
        bool coChip;
        readonly List<Image> thanhXuong = new List<Image>();
        Action<string, Action<string>> hoi;
        const float CaoCauHoi = 64f;

        public float ChieuCao => CaoCauHoi + 14 + (CoDapAn ? 24 + caoHinh + 18 + 34 + caoTomTat + caoLoiGiai + 18 + 44 + 24 + (Khu3DMo ? cao3D + 16 : 0) + caoHoiTiep : Loi ? 120 : 196) + (coChip ? 52 : 0);

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
        }

        /// <summary>
        /// Có đáp án: veHinh(khung, rộng) vẽ hình vào khung và trả về chiều cao; nút: (tên đối tượng, chữ, kiểu 0 = chính / 1 = phụ, việc).
        /// </summary>
        public void DatDapAn(Func<RectTransform, float, float> veHinh, string ketLuan, string huyHieu, bool kiemChung, string tomTat,
                             IList<(string ten, string chu, int kieu, Action bam)> nut, IList<(string chu, Action bam)> chip,
                             Action<string, Action<string>> hoiFn, string loiGiai = null, Action phongTo = null)
        {
            CoDapAn = true;
            hoi = hoiFn;
            xuong.gameObject.SetActive(false);
            float rong = dong.RongNoiDung - 48;
            Hinh = MenuKit.Stretch(the, "Hình đáp án", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -24 - 10), new Vector2(-24, -24));
            caoHinh = Mathf.Max(60, veHinh(Hinh, rong));
            Hinh.offsetMin = new Vector2(24, -24 - caoHinh);
            if (phongTo != null)
            {
                // bấm vào hình (hoặc nhãn góc trên phải) → mở hình chiếm 90% màn hình
                var phu = MenuKit.Full(Hinh, "Lớp bấm phóng to");
                var pi = phu.gameObject.AddComponent<Image>(); pi.color = new Color(1, 1, 1, 0.001f);
                var pb = phu.gameObject.AddComponent<Button>(); pb.transition = Selectable.Transition.None; pb.onClick.AddListener(() => phongTo());
                var tag = MakeButton(Hinh, "Nút Phóng to hình", "⤢  Phóng to", new Vector2(-10, -10), new Vector2(118, 34), phongTo, 1, 8f, new Vector2(1, 1));
                ((RectTransform)tag.transform).pivot = new Vector2(1, 1);
                var tt = tag.transform.Find("Chữ").GetComponent<Text>(); tt.fontSize = 13; tt.fontStyle = FontStyle.Normal; tt.color = InkDim;
            }
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
            y -= caoTomTat;
            caoLoiGiai = 0;
            if (!string.IsNullOrEmpty(loiGiai))
            {
                int dong = 1;
                foreach (var dg in loiGiai.Split('\n')) dong += 1 + dg.Length / 150;
                caoLoiGiai = 34 + dong * 24;
                TrangCuon.NhanNho(the, "Nhãn lời giải", "Lời giải", new Vector2(24, y - 14), 300, Accent);
                var lg = TrangCuon.ChuRong(the, "Lời giải", loiGiai, 16, Ink, false, 24, y - 38, dong * 24, 24);
                lg.lineSpacing = 1.1f;
                y -= caoLoiGiai;
            }
            y -= 18;
            yNut = y;
            float x = 24, xp = 24;
            foreach (var (ten, chu, kieu, bam) in nut)
            {
                if (kieu == 2)
                {   // việc phụ (Xuất ảnh, Báo sai): chữ nhỏ bên phải, không tranh chỗ với nút chính
                    float wl = 20 + chu.Length * 7.6f;
                    var l = MakeButton(the, ten, chu, new Vector2(-xp, y), new Vector2(wl, 40), bam, 2, TrangCuon.BoNut, new Vector2(1, 1));
                    ((RectTransform)l.transform).pivot = new Vector2(1, 1);
                    var lt = l.transform.Find("Chữ").GetComponent<Text>(); lt.fontSize = 14; lt.color = InkDim; lt.fontStyle = FontStyle.Normal;
                    xp += wl + 4;
                    continue;
                }
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
            MenuAnim.Fade(MenuAnim.Group(the), 0.2f, 1f, 0.3f);
            dong.TruotToiDapAn(this);
        }

        /// <summary>
        /// Mở / đóng khu 3D ngay dưới hàng nút (lần đầu gọi dung(khu) để dựng nội dung; đóng thì gọi dong()).
        /// Ô hỏi tiếp (nếu mở) dời xuống dưới khu 3D.
        /// </summary>
        public void BatKhu3D(float cao, Action<RectTransform> dung, Action dong)
        {
            bool mo = !Khu3DMo;
            if (khu3D == null)
            {
                cao3D = cao;
                khu3D = MenuKit.Stretch(the, "Khu 3D", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, yNut - 44 - 16 - cao), new Vector2(-24, yNut - 44 - 16));
            }
            khu3D.gameObject.SetActive(mo);
            if (mo && khu3D.childCount == 0) dung?.Invoke(khu3D);
            if (!mo) dong?.Invoke();
            DoiHoiTiep();
            dong3DNhan?.Invoke(mo);
            this.dong.XepLai();
            if (mo) { MenuAnim.Fade(MenuAnim.Group(khu3D), 0f, 1f, 0.25f); this.dong.TruotToiDapAn(this); }
        }

        public Action<bool> dong3DNhan;

        void DoiHoiTiep()
        {
            if (hoiTiep == null) return;
            float lui = Khu3DMo ? cao3D + 16 : 0;
            hoiTiep.offsetMin = new Vector2(24, yNut - 44 - 24 - 140 - lui);
            hoiTiep.offsetMax = new Vector2(-24, yNut - 44 - 16 - lui);
        }

        /// <summary>Mở / đóng ô hỏi tiếp ngay dưới hàng nút.</summary>
        public void BatHoiTiep(string cauHoi = null)
        {
            if (hoiTiep == null || hoi == null) return;
            bool mo = !hoiTiep.gameObject.activeSelf || cauHoi != null;
            hoiTiep.gameObject.SetActive(mo);
            caoHoiTiep = mo ? 150 : 0;
            dong.XepLai();
            if (mo) oHoi.ActivateInputField();
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
        public float TiLeMo { get; private set; }

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
        public void Mo(string tieu, Action<RectTransform> dung) => Mo(tieu, dung, 0.75f);

        /// <summary>Mở bảng chiếm tiLe bề ngang (0,75 bảng lớn thường; 0,9 khi phóng to hình đáp án).</summary>
        public void Mo(string tieu, Action<RectTransform> dung, float tiLe)
        {
            float lx = (1f - tiLe) * 0.5f, ly = tiLe >= 0.85f ? 0.05f : 0.1f;
            khung.anchorMin = new Vector2(lx, ly); khung.anchorMax = new Vector2(1f - lx, 1f - ly);
            khung.offsetMin = khung.offsetMax = Vector2.zero;
            TiLeMo = tiLe;
            var kd = KhiDong; KhiDong = null; kd?.Invoke();
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

        public Action KhiDong;

        public void Dong()
        {
            if (!DangMo) return;
            var kd = KhiDong; KhiDong = null; kd?.Invoke();
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
            if (!cha.TryGetComponent<ScrollRect>(out var sr)) sr = cha.gameObject.AddComponent<ScrollRect>();
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

        /// <summary>Ảnh 3D kiểu trình xem 3Dmol của CHEMISTRY: nền sáng, bóng–que (cầu 0,27 vdW, que 0,14 Å hai nửa màu), nhãn nguyên tố, tự xoay chậm.</summary>
        public static float Ba(RectTransform cha, Dictionary<string, object> d, float rong, float cao)
        {
            var nt = L(Lay(d, "nguyen_tu"));
            if (nt.Count == 0) return 40;
            var nen = Fill(cha, "Nền 3D", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(rong, cao), Well, 12f);
            Ring(nen.transform, "Viền", Hair, 12f);
            var x = nen.gameObject.AddComponent<Anh3DXoay>();
            x.Dung(d, rong, cao);
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
            g.AnTieuDe = true;                         // lời giải đưa xuống phần chữ bên dưới, không gạch lượn sóng trên hình
            g.Dat(b.SoDo);
            return g;
        }
    }
}

namespace Khkt.Menu
{
    /// <summary>
    /// Ảnh 3D bóng–que vẽ bằng uGUI, tự xoay quanh trục đứng (24°/s) như spin("y") của 3Dmol trong CHEMISTRY.
    /// Mỗi khung chỉ đặt lại vị trí/kích thước/góc các Image có sẵn và sắp lại thứ tự xa–gần bằng mảng dựng sẵn (không cấp phát).
    /// </summary>
    public class Anh3DXoay : MonoBehaviour
    {
        public float TocDo = 24f;
        Vector3[] p0;
        string[] e;
        RectTransform[] cau, nhan, nuaA, nuaB;
        int[] lkA, lkB, thuTu;
        float[] z;
        Vector2[] p2;
        float k, goc, thoiGian, rong, cao;
        RectTransform lopCau;

        public int SoNguyenTu => p0 != null ? p0.Length : 0;

        public void Dung(Dictionary<string, object> d, float rong, float cao)
        {
            this.rong = rong; this.cao = cao;
            var nt = L(Lay(d, "nguyen_tu"));
            int n = nt.Count;
            p0 = new Vector3[n]; e = new string[n]; z = new float[n]; p2 = new Vector2[n]; thuTu = new int[n];
            Vector3 tam = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var a = D(nt[i]);
                e[i] = S(a, "e", "C");
                p0[i] = new Vector3(F(Lay(a, "x")), F(Lay(a, "y")), -F(Lay(a, "z")));
                tam += p0[i];
                thuTu[i] = i;
            }
            tam /= Mathf.Max(1, n);
            float R = 0.5f;
            for (int i = 0; i < n; i++) p0[i] -= tam;
            // quay mặt phẳng "dẹt nhất" của phân tử (benzen, vòng…) ra chính diện: pháp tuyến = hướng ít trải nhất (lặp lũy thừa trên ma trận hiệp phương sai đảo dấu)
            float xx = 0, yy = 0, zz = 0, xy = 0, xz = 0, yz = 0;
            foreach (var p in p0) { xx += p.x * p.x; yy += p.y * p.y; zz += p.z * p.z; xy += p.x * p.y; xz += p.x * p.z; yz += p.y * p.z; }
            float tr = xx + yy + zz;
            var v = new Vector3(0.3f, 0.4f, 0.87f);
            for (int it = 0; it < 40; it++)
            {
                var w = new Vector3((tr - xx) * v.x - xy * v.y - xz * v.z, -xy * v.x + (tr - yy) * v.y - yz * v.z, -xz * v.x - yz * v.y + (tr - zz) * v.z);
                if (w.sqrMagnitude < 1e-8f) break;
                v = w.normalized;
            }
            var quayMat = Quaternion.FromToRotation(v, Vector3.back);
            for (int i = 0; i < n; i++) { p0[i] = quayMat * p0[i]; R = Mathf.Max(R, p0[i].magnitude + TrinhPhat3D.VdW(e[i]) * 0.27f); }
            k = Mathf.Min(rong * 0.8f, cao) * 0.5f / R;
            var lk = L(Lay(d, "lien_ket"));
            var a2 = new List<int>(); var b2 = new List<int>();
            foreach (var o in lk) { var l = D(o); int a = I(Lay(l, "a")), b = I(Lay(l, "b")); if (a >= 0 && b >= 0 && a < n && b < n) { a2.Add(a); b2.Add(b); } }
            lkA = a2.ToArray(); lkB = b2.ToArray();
            var lopQue = MenuKit.Full(transform, "Que");
            nuaA = new RectTransform[lkA.Length]; nuaB = new RectTransform[lkA.Length];
            for (int i = 0; i < lkA.Length; i++)
            {
                nuaA[i] = Que(lopQue, TrinhPhat3D.MauCH(e[lkA[i]]));
                nuaB[i] = Que(lopQue, TrinhPhat3D.MauCH(e[lkB[i]]));
            }
            lopCau = MenuKit.Full(transform, "Cầu");
            cau = new RectTransform[n];
            for (int i = 0; i < n; i++)
            {
                float r = TrinhPhat3D.VdW(e[i]) * 0.27f * k;
                var c = Fill(lopCau, "Nguyên tử " + e[i], new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r * 2, r * 2), TrinhPhat3D.MauCH(e[i]), r);
                c.raycastTarget = false;
                if (e[i] == "H") { c.color = Color.white; Ring(c.transform, "Viền", new Color(0, 0, 0, 0.22f), r); }
                Fill(c.transform, "Ánh", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-r * 0.3f, r * 0.3f), new Vector2(r * 0.8f, r * 0.8f), new Color(1, 1, 1, 0.28f), r * 0.4f).raycastTarget = false;
                cau[i] = c.rectTransform;
            }
            var lopNhan = MenuKit.Full(transform, "Nhãn");
            nhan = new RectTransform[n];
            for (int i = 0; i < n; i++)
            {
                if (e[i] == "H") continue;
                float w = 14 + e[i].Length * 9;
                var nn = Fill(lopNhan, "Nhãn " + e[i], new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, 20), WithAlpha(Paper, 0.8f), 6f);
                nn.raycastTarget = false;
                GlassKit.Label(nn.transform, "Chữ", e[i], 13, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w, 20)).raycastTarget = false;
                nhan[i] = nn.rectTransform;
            }
            VeKhung();
        }

        static RectTransform Que(Transform cha, Color mau)
        {
            var rt = GlassKit.Rect(cha, "Nửa que", new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 4));
            var im = rt.gameObject.AddComponent<Image>();
            im.color = mau; im.raycastTarget = false;
            return rt;
        }

        void Update()
        {
            if (p0 == null) return;
            thoiGian += Time.unscaledDeltaTime;
            goc = 40f * Mathf.Sin(thoiGian * 0.55f);          // lắc qua lại ±40° quanh trục đứng: luôn thấy rõ, không bao giờ nhìn ngang mép
            VeKhung();
        }

        void VeKhung()
        {
            var q = Quaternion.Euler(12f, goc, 0f);
            var c = new Vector2(rong * 0.5f, -cao * 0.5f);
            for (int i = 0; i < p0.Length; i++)
            {
                var p = q * p0[i];
                p2[i] = c + new Vector2(p.x, p.y) * k;
                z[i] = p.z;
                cau[i].anchoredPosition = p2[i];
                if (nhan[i] != null) nhan[i].anchoredPosition = p2[i];
            }
            float day = 0.28f * k;
            for (int i = 0; i < lkA.Length; i++)
            {
                Vector2 a = p2[lkA[i]], b = p2[lkB[i]], m = (a + b) * 0.5f;
                Dat(nuaA[i], a, m, day);
                Dat(nuaB[i], m, b, day);
            }
            // sắp xa (z lớn) vẽ trước: sắp chèn trên mảng dựng sẵn, rồi đặt thứ tự anh em
            for (int i = 1; i < thuTu.Length; i++)
            {
                int v = thuTu[i], j = i - 1;
                while (j >= 0 && z[thuTu[j]] < z[v]) { thuTu[j + 1] = thuTu[j]; j--; }
                thuTu[j + 1] = v;
            }
            for (int i = 0; i < thuTu.Length; i++)
                if (cau[thuTu[i]].GetSiblingIndex() != i) cau[thuTu[i]].SetSiblingIndex(i);
        }

        static void Dat(RectTransform r, Vector2 tu, Vector2 den, float day)
        {
            var v = den - tu;
            r.anchoredPosition = tu;
            r.sizeDelta = new Vector2(v.magnitude, day);
            r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        }
    }

    /// <summary>
    /// Trình phát 3D cơ chế nhúng trong một khung uGUI (bảng lớn): camera riêng vẽ vào RenderTexture hiện bằng RawImage,
    /// kéo chuột trong khung để xoay. Huỷ đối tượng này thì giải phóng camera và RenderTexture.
    /// </summary>
    public class Xem3DNho : MonoBehaviour
    {
        public TrinhPhat3D TP { get; private set; }
        Camera cam;
        RenderTexture rt;
        RectTransform khung;
        Camera camUi;
        RawImage anh;
        Light den, sunCu;

        public static Xem3DNho Tao(RectTransform khung, KichBan3D kb, Camera camUi)
        {
            var go = new GameObject("Trình phát 3D trong bảng");
            var x = go.AddComponent<Xem3DNho>();
            x.khung = khung; x.camUi = camUi;
            var cgo = new GameObject("Camera");
            cgo.transform.SetParent(go.transform, false);
            x.cam = cgo.AddComponent<Camera>();
            x.cam.clearFlags = CameraClearFlags.SolidColor;
            x.cam.backgroundColor = CoCheMoDun.NenToi;
            x.cam.cullingMask = 1 << TrinhPhat3D.LopVe;
            x.cam.fieldOfView = 32f; x.cam.nearClipPlane = 0.05f; x.cam.farClipPlane = 300f;
            x.cam.depth = -50;
            // đèn riêng cho lớp vẽ 3D: cảnh nền (menu) có thể không có đèn, phân tử Lit sẽ tối đen
            var lgo = new GameObject("Đèn 3D bảng");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            var den = lgo.AddComponent<Light>();
            den.type = LightType.Directional; den.intensity = 1.15f; den.color = Color.white;
            den.cullingMask = 1 << TrinhPhat3D.LopVe; den.shadows = LightShadows.None;
            x.den = den;
            x.sunCu = RenderSettings.sun;
            RenderSettings.sun = den;                 // URP lấy RenderSettings.sun làm đèn chính → phân tử chắc chắn được chiếu
            Khkt.Rendering.CameraKhongHauKy.Gan(x.cam);
            // cỡ ảnh theo TỈ LỆ khung (không theo điểm ảnh màn lúc tạo – màn batchmode/màn nhỏ cho ảnh quá thô): rộng 1600
            float tl = khung.rect.height > 1 ? khung.rect.width / khung.rect.height : 1.5f;
            int w = 1600, h = Mathf.Clamp(Mathf.RoundToInt(1600f / Mathf.Max(0.3f, tl)), 200, 2048);
            x.rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Bang3D" };
            x.rt.Create();
            x.cam.targetTexture = x.rt;
            if (!khung.TryGetComponent(out x.anh)) x.anh = khung.gameObject.AddComponent<RawImage>();
            x.anh.texture = x.rt; x.anh.raycastTarget = false; x.anh.color = Color.white;
            x.TP = go.AddComponent<TrinhPhat3D>();
            x.TP.TuSang = 0.3f;
            x.TP.Cam = x.cam;
            x.TP.VungNhin = new Rect(0, 0, 1, 1);
            x.TP.ViTriChuot = x.Chuot;
            x.TP.Nap(kb);
            return x;
        }

        Vector2? Chuot(Vector2 p)
        {
            if (khung == null || !khung.gameObject.activeInHierarchy) return null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(khung, p, camUi, out var lp)) return null;
            var r = khung.rect;
            var n = new Vector2((lp.x - r.xMin) / Mathf.Max(1, r.width), (lp.y - r.yMin) / Mathf.Max(1, r.height));
            return n.x >= 0 && n.x <= 1 && n.y >= 0 && n.y <= 1 ? n : (Vector2?)null;
        }

        /// <summary>Vẽ ngay một khung (kiểm thử/chụp ảnh ở batchmode, nơi camera không tự vẽ theo vòng lặp).</summary>
        public void VeNgay() { if (cam != null) cam.Render(); }

        /// <summary>Kiểm thử: lưu nội dung RenderTexture ra PNG.</summary>
        public void LuuAnh(string duong)
        {
            var cu = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = cu;
            System.IO.File.WriteAllBytes(duong, t.EncodeToPNG()); Destroy(t);
        }

        /// <summary>Kiểm thử: màu điểm ảnh giữa khung 3D và số điểm ảnh khác màu nền (đo trên một hàng ngang giữa ảnh).</summary>
        public (Color giua, int khacNen) DoAnh()
        {
            var cu = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = cu;
            int n = 0;
            var nen = (Color)cam.backgroundColor;
            for (int yy = 0; yy < rt.height; yy += 8)                       // đếm trên lưới thưa cả ảnh (không chỉ hàng giữa)
                for (int i = 0; i < rt.width; i += 8) { var c = t.GetPixel(i, yy); if (Mathf.Abs(c.r - nen.r) + Mathf.Abs(c.g - nen.g) + Mathf.Abs(c.b - nen.b) > 0.08f) n++; }
            var g = t.GetPixel(rt.width / 2, rt.height / 2);
            Destroy(t);
            Debug.Log($"[BANG3D] cam {cam.transform.position} kc {TP.KhoangCach:0.0} rt {rt.width}x{rt.height} nt {TP.SoNguyenTuDangVe} hat {TP.SoHatDangVe} tam {TrinhPhat3D.Tam}");
            return (g, n);
        }

        // nhãn ký hiệu trên mọi nguyên tử (C, H, O, Br…) + điện tích, đặt theo vị trí chiếu của camera 3D lên khung
        readonly List<Text> nhan = new List<Text>();
        RectTransform lopNhan;

        void LateUpdate()
        {
            if (TP == null || khung == null || cam == null) return;
            if (lopNhan == null) { lopNhan = MenuKit.Full(khung, "Nhãn nguyên tử"); lopNhan.SetAsLastSibling(); }
            int dung = 0;
            var r = khung.rect;
            if (cam.enabled && TP.KichBan != null)
                foreach (var kv in TP.ViTriNguyenTu)
                {
                    if (TP.CoNguyenTu(kv.Key) < 0.5f) continue;
                    string e = TP.NguyenTo(kv.Key);
                    if (TP.AnH && e == "H") continue;
                    var sp = cam.WorldToScreenPoint(TP.TheGioi(kv.Value));
                    if (sp.z <= 0f) continue;
                    var lp = new Vector2((sp.x / Mathf.Max(1, cam.pixelWidth) - 0.5f) * r.width, (sp.y / Mathf.Max(1, cam.pixelHeight) - 0.5f) * r.height);
                    int dt = TP.DienTichHienTai(kv.Key);
                    string chu = e + (dt == 0 ? "" : dt > 0 ? "⊕" : "⊖");
                    while (nhan.Count <= dung)
                    {
                        var t = GlassKit.Label(lopNhan, "Nhãn", "", 14, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(44, 22), new Vector2(0.5f, 0.5f));
                        t.rectTransform.pivot = new Vector2(0.5f, 0.5f); t.raycastTarget = false;
                        var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.8f);
                        nhan.Add(t);
                    }
                    var n = nhan[dung++];
                    if (n.text != chu) n.text = chu;
                    n.color = e == "H" ? new Color32(0x33, 0x3a, 0x48, 0xff) : Color.white;
                    n.fontSize = e == "H" ? 11 : 14;
                    n.rectTransform.anchoredPosition = lp;
                    if (!n.gameObject.activeSelf) n.gameObject.SetActive(true);
                }
            for (int i = dung; i < nhan.Count; i++) if (nhan[i].gameObject.activeSelf) nhan[i].gameObject.SetActive(false);
        }

        /// <summary>Bật/tắt vẽ (khung đang ẩn thì không tốn GPU).</summary>
        public void Hien(bool co) { if (cam != null) cam.enabled = co; }

        void OnDestroy()
        {
            if (RenderSettings.sun == den) RenderSettings.sun = sunCu;      // trả đèn chính cũ của cảnh
            if (cam != null) cam.targetTexture = null;
            if (anh != null) anh.texture = null;
            if (lopNhan != null) Destroy(lopNhan.gameObject);
            if (rt != null) { rt.Release(); Destroy(rt); }
        }
    }
}
