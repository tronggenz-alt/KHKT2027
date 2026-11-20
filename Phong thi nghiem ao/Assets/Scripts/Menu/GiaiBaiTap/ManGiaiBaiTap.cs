using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Màn "Giải bài tập" (làm lại 19/11 theo bố cục app web CHEMISTRY, chủ đề VERA).
    ///   • Thanh điều hướng trên: Trang chủ · Chuỗi phản ứng · Cơ chế · Mô hình 3D · Hóa lý, phải là Cài đặt và ← Màn chính.
    ///   • Trang chủ: hàng thẻ lớn (Chuỗi trắng, Cơ chế navy, Mô hình 3D đen) + thẻ ngang Hóa lý.
    ///   • Chuỗi phản ứng: 01 Nhập đề (Văn bản / Hình ảnh / Chuỗi mẫu) · 02 Tuỳ chọn giải · 03 Kết quả từng bước (gập/mở, báo sai).
    ///   • Cơ chế: 01 Nhập đề (Văn bản / Hình ảnh / Bài mẫu) · 02 Kiểu cơ chế · 03 Kết quả (danh sách bước, mở 3D ⇄ Giấy,
    ///     giải thích chi tiết, hỏi đáp, báo sai). Trình phát 3D ⇄ Giấy là CoCheMoDun (lớp phủ riêng, đóng thì về đây).
    ///   • Mô hình 3D: ô chất + chip mẫu → MoHinh3DMoDun. Hóa lý: 4 thẻ → Khkt.HoaLy.HoaLyMoDun (tìm theo tên lớp).
    /// API máy chủ cổng 8010: /co-che/giai-chuoi, /co-che/giai-de, /co-che/mo-hinh-3d, /co-che/bao-sai.
    /// </summary>
    public class ManGiaiBaiTap : MonoBehaviour
    {
        public Action QuayLai;
        public Action MoChuoi, Mo3D, MoCoChe;
        public Action<string> MoHoaLy;
        public Action MoCaiDat;

        public enum Trang { Chu = 0, Chuoi = 1, CoChe = 2, MoHinh3D = 3, HoaLy = 4 }
        public Trang TrangDang { get; private set; } = Trang.Chu;

        RectTransform goc, vung;
        Text trangThai;
        readonly TrangCuon[] trang = new TrangCuon[5];
        readonly List<(Button nut, Text chu)> nav = new List<(Button, Text)>();
        HopBaoSai baoSai;

        public static ManGiaiBaiTap Tao(RectTransform cha, Texture2D logo)
        {
            var rt = Full(cha, "Màn Giải bài tập");
            var m = rt.gameObject.AddComponent<ManGiaiBaiTap>();
            m.goc = rt;
            m.Dung(logo);
            rt.gameObject.SetActive(false);
            return m;
        }

        public bool DangHien => goc != null && goc.gameObject.activeSelf;

        public void Hien()
        {
            goc.gameObject.SetActive(true);
            MoTrang(Trang.Chu, true);
            KhoiDau.HienMan(goc, new List<RectTransform>());
            trangThai.text = "";
        }

        public void An() => KhoiDau.AnMan(goc);

        /// <summary>Giải đề của trang đang mở (phím Enter / ⌘↵).</summary>
        public void GiaiTrangDang()
        {
            if (TrangDang == Trang.Chuoi) { if (oChuoi != null) oChuoi.text = (oChuoi.text ?? "").TrimEnd('\n', '\r'); GiaiChuoi(); }
            else if (TrangDang == Trang.CoChe) { if (oDe != null) oDe.text = (oDe.text ?? "").TrimEnd('\n', '\r'); VeCoChe(); }
            else if (TrangDang == Trang.MoHinh3D) Dung3D();
        }

        /// <summary>Phím tắt (khi không gõ chữ và không có lớp phủ): 1–5 chuyển trang, Esc về trang chủ.</summary>
        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || CoCheMoDun.DangMo || MoHinh3DMoDun.DangMo || (baoSai != null && baoSai.DangMo) || (bangLon != null && bangLon.DangMo)) return;
            var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            bool dangGo = sel != null && sel.GetComponent<InputField>() is InputField f && f.isFocused && f.gameObject.activeInHierarchy;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            bool phimLenh = kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed || kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (enter && (phimLenh || !dangGo)) { GiaiTrangDang(); return; }       // Enter (không gõ) hoặc ⌘/Ctrl + Enter (ô nhiều dòng)
            if (dangGo) return;
            int so = kb.digit1Key.wasPressedThisFrame ? 0 : kb.digit2Key.wasPressedThisFrame ? 1 : kb.digit3Key.wasPressedThisFrame ? 2
                   : kb.digit4Key.wasPressedThisFrame ? 3 : kb.digit5Key.wasPressedThisFrame ? 4 : -1;      // không tạo mảng mỗi khung hình
            if (so >= 0) { MoTrang((Trang)so); return; }
            if (kb.escapeKey.wasPressedThisFrame && TrangDang != Trang.Chu) MoTrang(Trang.Chu);
        }

        public void Bao(string chu, bool loi = true)
        {
            trangThai.color = loi ? Error : Bio;
            trangThai.text = chu;
            if (loi) MenuAnim.Shake(trangThai.rectTransform);
        }

        void Dung(Texture2D logo)
        {
            Backdrop(goc);
            // ---- thanh điều hướng trên
            KhoiDau.ThanhTren(goc, logo);
            var ve = MakeButton(goc, "Nút Về màn chính", "← Màn chính", new Vector2(-40, -28), new Vector2(160, 46), () => QuayLai?.Invoke(), 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)ve.transform).pivot = new Vector2(1, 1);
            var caiDat = MakeButton(goc, "Nút Cài đặt Giải bài tập", "Cài đặt", new Vector2(-212, -28), new Vector2(120, 46), () => MoCaiDat?.Invoke(), 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)caiDat.transform).pivot = new Vector2(1, 1);
            string[] tenNav = { "Trang chủ", "Chuỗi phản ứng", "Cơ chế", "Mô hình 3D", "Hóa lý" };
            float tong = 0;
            foreach (var t in tenNav) tong += 36 + t.Length * 10f + 6;
            float x = -tong / 2f;
            for (int i = 0; i < tenNav.Length; i++)
            {
                float w = 36 + tenNav[i].Length * 10f;
                var tr = (Trang)i;
                var b = MakeButton(goc, "Điều hướng " + tenNav[i], tenNav[i], new Vector2(x, -29), new Vector2(w, 44), () => MoTrang(tr), 2, TrangCuon.BoNut, new Vector2(0.5f, 1));
                ((RectTransform)b.transform).pivot = new Vector2(0, 1);
                var chu = b.transform.Find("Chữ").GetComponent<Text>();
                chu.fontSize = 16;
                nav.Add((b, chu));
                x += w + 6;
            }
            var vach = Fill(goc, "Vạch dưới thanh trên", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -92), new Vector2(10, 1), Hair, 0);
            vach.rectTransform.anchorMax = new Vector2(1, 1); vach.rectTransform.sizeDelta = new Vector2(0, 1);

            vung = Stretch(goc, "Vùng trang", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -93));
            trang[0] = DungTrangChu();
            trang[1] = DungTrangChuoi();
            trang[2] = DungTrangCoChe();
            trang[3] = DungTrang3D();
            trang[4] = DungTrangHoaLy();

            trangThai = GlassKit.Label(goc, "Thông báo", "", 17, Error, false, TextAnchor.MiddleCenter, new Vector2(0, 24), new Vector2(1400, 30), new Vector2(0.5f, 0));
            trangThai.rectTransform.pivot = new Vector2(0.5f, 0);
            bangLon = BangLon.Tao(goc);
            baoSai = HopBaoSai.Tao(goc);
        }

        /// <summary>Chuyển trang (mờ chéo, các khối trượt lên lần lượt như .hien-dan của CHEMISTRY).</summary>
        public void MoTrang(Trang t, bool tucThi = false)
        {
            TrangDang = t;
            for (int i = 0; i < trang.Length; i++)
            {
                bool dang = i == (int)t;
                trang[i].Goc.gameObject.SetActive(dang);
                nav[i].chu.color = dang ? Ink : InkDim;
                nav[i].chu.fontStyle = dang ? FontStyle.Bold : FontStyle.Normal;
                var mat = Face(nav[i].nut);
                if (mat != null) mat.color = dang ? (UiTheme.IsLight ? new Color(0, 0, 0, 0.05f) : new Color(1, 1, 1, 0.08f)) : new Color(1, 1, 1, 0.001f);
            }
            var tr = trang[(int)t];
            tr.LenDinh();
            if (!tucThi)
            {
                var g = MenuAnim.Group(tr.Goc);
                MenuAnim.Fade(g, 0f, 1f, 0.22f);
            }
            for (int i = 0; i < tr.LanLuot.Count; i++)
                MenuAnim.Enter(tr.LanLuot[i], 0.02f + Mathf.Min(i, 8) * 0.045f, 14f, 0.42f);
            if (t == Trang.CoChe) NapBaiMau();
        }

        // ================================================================== TRANG CHỦ
        TrangCuon DungTrangChu()
        {
            var t = TrangCuon.Tao(vung, "Trang chủ", false);
            t.DauTrang("Hóa hữu cơ · THPT & HSG", "Giải bài tập", "Chọn công cụ. Dán đề hoặc chọn bài mẫu – máy giải từng bước, vẽ cơ chế như bài thi và dựng chất trong không gian 3D.");
            var hang = t.Khoi("Hàng công cụ", 300, 26);
            t.LanLuot.Add(TheCongCu(hang, 0, "Chuỗi phản ứng", "01 · Cơ bản đến nâng cao", "⇢",
                "Giải chuỗi A → B → C: tìm từng chất, nêu điều kiện, loại phản ứng.", new[] { "A → B → C", "Ảnh đề", "Từng bước" },
                Paper, Paper, Ink, InkDim, () => MoTrang(Trang.Chuoi)));
            t.LanLuot.Add(TheCongCu(hang, 1, "Cơ chế", "02 · Bài thi & HSG", "↷",
                "Mũi tên cong, electron chạy trong 3D rồi chuyển sang tờ giấy như đáp án.", new[] { "SN1 / SN2", "E1 / E2", "3D ⇄ Giấy" },
                Hex(0x1d3b6e), Hex(0x0b1b36), Color.white, Hex(0xbfdbfe), () => MoTrang(Trang.CoChe)));
            t.LanLuot.Add(TheCongCu(hang, 2, "Mô hình 3D", "03 · Không gian 3 chiều", "◇",
                "Gõ tên hoặc công thức, xoay phân tử 360°, bật tắt H, xem liên kết.", new[] { "Tên Việt / Anh", "SMILES", "Bóng–que" },
                Hex(0x27272a), Hex(0x09090b), Color.white, Hex(0xd4d4d8), () => MoTrang(Trang.MoHinh3D)));

            // thẻ ngang Hóa lý
            var k = t.Khoi("Hàng Hóa lý", 132, 26);
            var the = Card(k, "Thẻ Hóa lý", 0, 1, 0, 132, 0, true, TrangCuon.BoThe);
            MakeCardClickable(the, () => MoTrang(Trang.HoaLy), 2f);
            var than = Body(the);
            var o = Fill(than, "Biểu tượng", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(64, 64), AccentSoft, 16f);
            GlassKit.Label(o.transform, "Ký hiệu", "pV", 22, Accent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(64, 64));
            TrangCuon.NhanNho(than, "Nhãn", "Hóa lý · 4 mô phỏng tương tác", new Vector2(118, -26), 600, Accent);
            GlassKit.Label(than, "Tên", "Hóa lý", 26, Ink, true, TextAnchor.UpperLeft, new Vector2(118, -48), new Vector2(400, 36));
            GlassKit.Label(than, "Mô tả", "Khí lý tưởng · Tốc độ phản ứng · Cân bằng Le Chatelier · Động cơ nhiệt", 16, InkDim, false, TextAnchor.UpperLeft, new Vector2(118, -88), new Vector2(900, 24));
            var nut = MakeButton(than, "Nút Mở Hóa lý", "Mở Hóa lý  →", new Vector2(-30, 0), new Vector2(170, 48), () => MoTrang(Trang.HoaLy), 0, TrangCuon.BoNut, new Vector2(1, 0.5f));
            ((RectTransform)nut.transform).pivot = new Vector2(1, 0.5f);
            t.LanLuot.Add(k);
            t.ChanTrang();
            t.ChotChieuCao();
            return t;
        }

        /// <summary>Thẻ lớn công cụ như CHEMISTRY: ô biểu tượng trái trên, nhãn phải trên, tên, mô tả, hàng chip; dải màu chéo.</summary>
        static RectTransform TheCongCu(RectTransform hang, int i, string ten, string nhan, string kyHieu, string moTa, string[] chip,
                                       Color dau, Color cuoi, Color chu, Color phu, Action bam)
        {
            var the = Card(hang, "Công cụ " + ten, i / 3f, (i + 1) / 3f, 0, 300, 24, true, TrangCuon.BoTheLon);
            MakeCardClickable(the, bam, 4f);
            var than = Body(the);
            var nen = than.GetComponent<Image>();
            bool trang = dau == Paper;
            if (!trang) { nen.color = Color.white; Gradient(nen, dau, cuoi, 150f); var v = CardRing(the); if (v != null) v.color = new Color(1, 1, 1, 0.10f); }
            var o = Fill(than, "Biểu tượng", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(48, 48),
                         trang ? Well : new Color(1, 1, 1, 0.10f), 12f);
            Ring(o.transform, "Viền", trang ? Hair : new Color(1, 1, 1, 0.16f), 12f);
            GlassKit.Label(o.transform, "Ký hiệu", kyHieu, 22, trang ? Accent : Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(48, 48));
            var n = TrangCuon.NhanNho(than, "Nhãn", nhan, new Vector2(-30, -44), 300, trang ? InkMute : WithAlpha(phu, 0.85f));
            n.rectTransform.anchorMin = n.rectTransform.anchorMax = new Vector2(1, 1);
            n.rectTransform.pivot = new Vector2(1, 1);
            n.alignment = TextAnchor.UpperRight;
            var td = GlassKit.Label(than, "Tên", ten, 32, chu, true, TextAnchor.UpperLeft, new Vector2(30, -104), new Vector2(480, 44));
            if (!trang) TextShadow(td, 0.3f, 2f);
            TrangCuon.ChuRong(than, "Mô tả", moTa, 17, trang ? InkDim : WithAlpha(phu, 0.92f), false, 30, -156, 56, 30);
            float x = 30;
            foreach (var c in chip)
            {
                float w = 24 + c.Length * 8.4f;
                var rt = GlassKit.Rect(than, "Chip " + c, new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 28), new Vector2(w, 30));
                Paint(Full(rt, "Mặt"), trang ? Well : new Color(1, 1, 1, 0.10f), 8f);
                Ring(rt, "Viền", trang ? Hair : new Color(1, 1, 1, 0.14f), 8f);
                GlassKit.Label(rt, "Chữ", c, 13, trang ? InkDim : phu, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w, 30)).horizontalOverflow = HorizontalWrapMode.Overflow;
                x += w + 8;
            }
            var mui = GlassKit.Label(than, "Mũi tên", "→", 24, trang ? Accent : Color.white, true, TextAnchor.MiddleRight, new Vector2(-30, 28), new Vector2(40, 30), new Vector2(1, 0));
            mui.rectTransform.pivot = new Vector2(1, 0);
            var hv = the.GetComponent<MenuHover>();
            if (hv != null) { hv.nudge = mui.rectTransform; hv.nudgeX = 6f; }
            return the;
        }

        // ================================================================== CHUỖI PHẢN ỨNG
        InputField oChuoi;
        RectTransform vanBanChuoi, anhChuoi, mauChuoi, ketQuaChuoi;
        RawImage xemAnhChuoi;
        Text tenAnhChuoi, trangThaiChuoi;
        Button nutGiaiChuoi;
        byte[] anhChuoiDu;
        int mucDo, mucThongMinh = 1;
        bool traCuu;
        TrangCuon.TheTab tabChuoi;
        string kqChuoi = "";
        readonly List<bool> moBuocChuoi = new List<bool>();
        float yKetQuaChuoi;
        public int SoBuocChuoi { get; private set; }
        public bool DangGiaiChuoi { get; private set; }
        public string KetQuaChuoiCuoi => kqChuoi;

        TrangCuon DungTrangChuoi()
        {
            var t = TrangCuon.Tao(vung, "Trang Chuỗi phản ứng", true);
            trang[1] = t;
            t.ThanhPhu("Giải chuỗi", () => MoTrang(Trang.Chu), new[] { "Nhập đề", "Tuỳ chọn", "Kết quả" });
            t.DauTrang("Công cụ · Giải chuỗi phản ứng", "Giải chuỗi phản ứng hữu cơ",
                       "Nhập chuỗi bằng <b>văn bản</b> hoặc <b>hình ảnh</b> – máy tìm từng chất, nêu điều kiện và loại phản ứng, " +
                       "bước có cơ chế mẫu thì mở được <b>3D ⇄ Giấy</b>.");
            t.GhiBuoc(0, t.DauMuc("01", "Nhập đề bài", "Chọn cách nhập: gõ chuỗi, chọn ảnh chụp đề, hoặc lấy một chuỗi mẫu."));
            var hangTab = t.Khoi("Tab nhập", 116, 18);
            tabChuoi = TrangCuon.NhomTheTab(hangTab, new[] { "Văn bản", "Hình ảnh", "Chuỗi mẫu" },
                new[] { "Gõ chuỗi, nối các chất bằng mũi tên", "Chọn ảnh chụp đề (JPG, PNG, HEIC)", $"{CoCheMoDun.ChuoiMau.Length} chuỗi giải được khi không có mạng" },
                new[] { "T", "▣", "≡" }, ChonTabChuoi);
            t.LanLuot.Add(hangTab);

            var khoiNhap = t.Khoi("Khối nhập chuỗi", 230, 40);
            var than = TrangCuon.The(khoiNhap, "Thẻ nhập chuỗi", 0, 230);
            t.LanLuot.Add(khoiNhap);
            // văn bản
            vanBanChuoi = Full(than, "Nhập văn bản");
            TrangCuon.NhanNho(vanBanChuoi, "Nhãn", "Chuỗi phản ứng", new Vector2(28, -24), 400);
            oChuoi = TrangCuon.ONhieuDong(vanBanChuoi, "Ô chuỗi", "Ví dụ: CH4 → C2H2 → CH3CHO → CH3COOH → CH3COOC2H5\nChất chưa biết: C2H4 → X → C2H5OH → Y",
                                         new Vector2(28, -56), new Vector2(100, 146), 18);
            var ort = (RectTransform)oChuoi.transform;
            ort.anchorMax = new Vector2(1, 1); ort.offsetMax = new Vector2(-28, ort.offsetMax.y);
            var chen = MakeButton(vanBanChuoi, "Nút Chèn mũi tên", "Chèn →", new Vector2(-28, -16), new Vector2(104, 34), () => { oChuoi.text = (oChuoi.text ?? "").TrimEnd() + " -> "; oChuoi.ActivateInputField(); oChuoi.MoveTextEnd(false); }, 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)chen.transform).pivot = new Vector2(1, 1);
            chen.transform.Find("Chữ").GetComponent<Text>().fontSize = 14;
            var xoa = MakeButton(vanBanChuoi, "Nút Xoá chuỗi", "Xoá", new Vector2(-140, -16), new Vector2(76, 34), () => oChuoi.text = "", 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)xoa.transform).pivot = new Vector2(1, 1);
            xoa.transform.Find("Chữ").GetComponent<Text>().fontSize = 14;
            // hình ảnh
            anhChuoi = Full(than, "Nhập hình ảnh");
            TrangCuon.NhanNho(anhChuoi, "Nhãn", "Ảnh đề", new Vector2(28, -24), 400);
            var khung = Fill(anhChuoi, "Khung ảnh", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -56), new Vector2(240, 150), Well, 12f);
            Ring(khung.transform, "Viền", HairStrong, 12f);
            xemAnhChuoi = new GameObject("Ảnh", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            xemAnhChuoi.transform.SetParent(khung.transform, false);
            var xr = xemAnhChuoi.rectTransform; xr.anchorMin = Vector2.zero; xr.anchorMax = Vector2.one; xr.offsetMin = new Vector2(6, 6); xr.offsetMax = new Vector2(-6, -6);
            xemAnhChuoi.gameObject.SetActive(false);
            GlassKit.Label(khung.transform, "Gợi ý", "Chưa có ảnh", 15, InkMute, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(240, 150));
            var chon = MakeButton(anhChuoi, "Nút Chọn ảnh chuỗi", ChonTepHeThong.HoTro ? "Chọn ảnh đề…" : "Ảnh đề (sắp có trên iPad)", new Vector2(300, -56), new Vector2(220, 48), ChonAnhChuoi, 1);
            chon.interactable = ChonTepHeThong.HoTro;
            MakeButton(anhChuoi, "Nút Bỏ ảnh chuỗi", "Bỏ ảnh", new Vector2(532, -56), new Vector2(110, 48), () => DatAnhChuoi(null, ""), 1);
            tenAnhChuoi = GlassKit.Label(anhChuoi, "Tên ảnh", "AI đọc chuỗi trong ảnh (Claude, dự phòng Gemini) rồi giải như khi gõ chữ.", 15, InkDim, false, TextAnchor.UpperLeft, new Vector2(300, -120), new Vector2(900, 60));
            tenAnhChuoi.horizontalOverflow = HorizontalWrapMode.Wrap;
            // chuỗi mẫu
            mauChuoi = Full(than, "Nhập chuỗi mẫu");
            TrangCuon.NhanNho(mauChuoi, "Nhãn", "Chuỗi mẫu – bấm để giải ngay", new Vector2(28, -24), 600);
            for (int i = 0; i < CoCheMoDun.ChuoiMau.Length; i++)
            {
                string c = CoCheMoDun.ChuoiMau[i];
                var hang = MakeButton(mauChuoi, "Chuỗi mẫu " + (i + 1), "", new Vector2(28, -56 - i * 54), new Vector2(100, 46), () => { oChuoi.text = c; tabChuoi.Chon(0, true); GiaiChuoi(); }, 1);
                var hrt = (RectTransform)hang.transform; hrt.anchorMax = new Vector2(1, 1); hrt.offsetMax = new Vector2(-28, hrt.offsetMax.y);
                var chu = hang.transform.Find("Chữ").GetComponent<Text>();
                chu.supportRichText = true; chu.alignment = TextAnchor.MiddleLeft; chu.fontSize = 16; chu.fontStyle = FontStyle.Normal;
                chu.rectTransform.anchorMin = Vector2.zero; chu.rectTransform.anchorMax = Vector2.one; chu.rectTransform.offsetMin = new Vector2(18, 0); chu.rectTransform.offsetMax = new Vector2(-18, 0);
                chu.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Accent)}><b>{i + 1:00}</b></color>   {c.Replace("->", "→")}";
            }
            ChonTabChuoi(0);

            // 02 tuỳ chọn
            t.GhiBuoc(1, t.DauMuc("02", "Tuỳ chọn giải", "Chọn mức độ đề và cách giải, rồi bấm Giải chuỗi."));
            var khoiTc = t.Khoi("Khối tuỳ chọn", 200, 30);
            var tc = Card(khoiTc, "Thẻ tuỳ chọn", 0, 0.62f, 0, 200, 18, false, TrangCuon.BoThe);
            var tcThan = Body(tc);
            TrangCuon.NhanNho(tcThan, "Nhãn mức độ", "Mức độ đề", new Vector2(28, -26), 200);
            var gtMucDo = GlassKit.Label(tcThan, "Giải thích mức độ", "Tự động: máy đoán theo chuỗi · Trung bình: lời giải gọn · HSG: thêm điều kiện, sản phẩm phụ.", 13, InkMute, false, TextAnchor.UpperLeft, new Vector2(28, -104), new Vector2(980, 20));
            TrangCuon.NutHoi(tcThan, "mức độ", new Vector2(124, -22), gtMucDo);
            Tabs(tcThan, "Mức độ đề", new[] { "Mức Tự động", "Mức Trung bình", "Mức HSG" }, new[] { "Tự động", "Trung bình", "HSG" }, new Vector2(28, -54), new Vector2(150, 40), false, 4f, 0, i => mucDo = i);
            TrangCuon.NhanNho(tcThan, "Nhãn thông minh", "Mức thông minh", new Vector2(500, -26), 200);
            var gtTm = GlassKit.Label(tcThan, "Giải thích thông minh", "Mức cao cho AI nghĩ kỹ hơn ở bước chưa có trong bảng (chậm hơn).", 13, InkMute, false, TextAnchor.UpperLeft, new Vector2(28, -104), new Vector2(980, 20));
            TrangCuon.NutHoi(tcThan, "thông minh", new Vector2(644, -22), gtTm);
            Tabs(tcThan, "Mức thông minh", new[] { "Nhanh", "Cân bằng", "Kỹ", "Tối đa" }, new[] { "Nhanh", "Cân bằng", "Kỹ", "Tối đa" }, new Vector2(500, -54), new Vector2(94, 40), false, 4f, 1, i => mucThongMinh = i);
            TrangCuon.CongTac(tcThan, "Công tắc tra cứu", "Tra cứu tài liệu khoa học (kèm nguồn khi AI phải tự giải)", new Vector2(28, -134), new Vector2(480, 44), false, b => traCuu = b);
            var tc2 = Card(khoiTc, "Thẻ lời giải", 0.62f, 1f, 0, 200, 18, false, TrangCuon.BoThe);
            var tc2Than = Body(tc2);
            TrangCuon.NhanNho(tc2Than, "Nhãn", "Lời giải", new Vector2(28, -26), 200);
            TrangCuon.ChuRong(tc2Than, "Cách giải",
                "Bước có trong bảng đã kiểm chứng: lấy ngay, ghi <b>đã kiểm chứng</b>.\nBước mới: AI (Claude, dự phòng Gemini, rồi AI tại máy) giải, RDKit kiểm công thức, ghi <b>chưa kiểm chứng</b>.",
                15, InkDim, false, 28, -56, 130, 28);
            t.LanLuot.Add(khoiTc);

            var khoiNut = t.Khoi("Khối nút giải", 60, 60);
            nutGiaiChuoi = MakeButton(khoiNut, "Nút Giải chuỗi", "Giải chuỗi  →", Vector2.zero, new Vector2(210, 56), GiaiChuoi, 0, RadiusButton);
            trangThaiChuoi = GlassKit.Label(khoiNut, "Trạng thái", "Chưa nhập chuỗi", 15, InkMute, false, TextAnchor.MiddleLeft, new Vector2(232, 0), new Vector2(1300, 56));
            trangThaiChuoi.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.LanLuot.Add(khoiNut);

            // 03 kết quả: dòng thời gian các lần giải (câu hỏi ở trên, đáp án hiện dần bên dưới)
            t.GhiBuoc(2, t.DauMuc("03", "Kết quả", "Mỗi lần giải là một khối: đề ở trên, đáp án vẽ thành hình bên dưới. Bấm Mở lớn để xem chi tiết."));
            t.ThanhSoan("Gõ chuỗi rồi Enter, ví dụ: C2H4 -> C2H5OH -> CH3CHO", "Giải chuỗi", c =>
            {
                oChuoi.text = c; tabChuoi.Chon(0, false); ChonTabChuoi(0); GiaiChuoi();
            });
            dongChuoi = DongThoiGian.Tao(t, "Chưa có kết quả", "Nhập đề ở mục <b>01</b>, chọn tuỳ chọn ở mục <b>02</b> rồi bấm <b>Giải chuỗi</b>. Đáp án hiện ngay dưới đây.");
            return t;
        }

        void ChonTabChuoi(int i)
        {
            vanBanChuoi.gameObject.SetActive(i == 0);
            anhChuoi.gameObject.SetActive(i == 1);
            mauChuoi.gameObject.SetActive(i == 2);
            MenuAnim.Enter(i == 0 ? vanBanChuoi : i == 1 ? anhChuoi : mauChuoi, 0f, 6f, 0.25f);
        }

        void ChonAnhChuoi()
        {
            StartCoroutine(ChonTepHeThong.ChonAnh("Chọn ảnh đề chuỗi", (duong, loi) =>
            {
                if (!string.IsNullOrEmpty(loi)) { DatTrangThaiChuoi(loi, true); return; }
                if (string.IsNullOrEmpty(duong)) return;
                var (du, loiDoc) = ChonTepHeThong.DocTep(duong, CoCheMoDun.AnhToiDa);
                if (du == null) { DatTrangThaiChuoi(loiDoc, true); return; }
                DatAnhChuoi(du, System.IO.Path.GetFileName(duong));
            }));
        }

        public void DatAnhChuoi(byte[] du, string ten)
        {
            anhChuoiDu = du;
            if (xemAnhChuoi.texture != null) Destroy(xemAnhChuoi.texture);
            xemAnhChuoi.texture = null;
            xemAnhChuoi.gameObject.SetActive(false);
            if (du != null)
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(du))
                {
                    xemAnhChuoi.texture = tex;
                    xemAnhChuoi.gameObject.SetActive(true);
                    var f = xemAnhChuoi.gameObject.GetComponent<AspectRatioFitter>() ?? xemAnhChuoi.gameObject.AddComponent<AspectRatioFitter>();
                    f.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    f.aspectRatio = tex.width / (float)Mathf.Max(1, tex.height);
                }
                else Destroy(tex);
            }
            tenAnhChuoi.text = du == null ? "AI đọc chuỗi trong ảnh (Claude, dự phòng Gemini) rồi giải như khi gõ chữ."
                                          : $"<b>{ten}</b> ({du.Length / 1024f:0} KB). Bấm Giải chuỗi – AI chép chuỗi trong ảnh rồi giải.";
            tenAnhChuoi.supportRichText = true;
        }

        void DatTrangThaiChuoi(string chu, bool loi)
        {
            trangThaiChuoi.text = chu;
            trangThaiChuoi.color = loi ? Error : InkMute;
            if (loi) MenuAnim.Shake(trangThaiChuoi.rectTransform);
        }

        DongThoiGian dongChuoi, dongCoChe, dong3D;
        public DongThoiGian DongChuoi => dongChuoi;
        public DongThoiGian DongCoChe => dongCoChe;
        public DongThoiGian Dong3D => dong3D;
        public BangLon Bang => bangLon;
        BangLon bangLon;

        public void GiaiChuoi()
        {
            if (DangGiaiChuoi) return;
            bool dungAnh = tabChuoi.Dang == 1;
            string chuoi = (oChuoi.text ?? "").Trim().Replace("→", "->");
            if (dungAnh ? anhChuoiDu == null : chuoi.Length < 3) { DatTrangThaiChuoi(dungAnh ? "Hãy chọn ảnh đề trước." : "Hãy gõ chuỗi, ví dụ C2H4 -> C2H5OH -> CH3CHO.", true); return; }
            string[] md = { "tu_dong", "trung_binh", "hsg" }, tm = { "nhanh", "can_bang", "ky", "toi_da" };
            var sb = new StringBuilder("{");
            if (dungAnh) sb.Append("\"anh_base64\":\"").Append(Convert.ToBase64String(anhChuoiDu)).Append("\",");
            else sb.Append("\"chuoi\":").Append(MayChuCoChe.ChuoiJson(chuoi)).Append(',');
            sb.Append("\"muc_do\":\"").Append(md[mucDo]).Append("\",\"muc_thong_minh\":\"").Append(tm[mucThongMinh]).Append("\",\"tra_cuu\":").Append(traCuu ? "true" : "false").Append('}');
            var k = dongChuoi.Them(dungAnh ? "Ảnh đề chuỗi" : chuoi.Replace("->", "→"), dungAnh ? xemAnhChuoi.texture : null);
            StartCoroutine(GoiChuoi(sb.ToString(), dungAnh, k));
        }

        IEnumerator GoiChuoi(string than, bool coAnh, KhoiDapAn k)
        {
            DangGiaiChuoi = true;
            nutGiaiChuoi.interactable = false;
            float t0 = Time.realtimeSinceStartup;
            bool xong = false, ok = false;
            string kq = "";
            StartCoroutine(MayChuCoChe.Goi("POST", "/co-che/giai-chuoi", than, coAnh ? 200f : 120f, (o, x) => { ok = o; kq = x; xong = true; }));
            string[] buocLam = coAnh ? new[] { "Đọc ảnh đề", "Tìm từng chất", "Kiểm chứng", "Vẽ chuỗi" } : new[] { "Tách chuỗi", "Tìm từng chất", "Kiểm chứng", "Vẽ chuỗi" };
            while (!xong)
            {
                float s = Time.realtimeSinceStartup - t0;
                int b = Mathf.Min(buocLam.Length - 1, (int)(s / 1.5f));
                k.DatTienTrinh(TienTrinh(buocLam, b, s));
                DatTrangThaiChuoi($"Đang giải… {s:0} s", false);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            DangGiaiChuoi = false;
            nutGiaiChuoi.interactable = true;
            if (!ok) { k.DatLoi(kq); DatTrangThaiChuoi("Chưa giải được – xem khối đáp án.", true); yield break; }
            try
            {
                var d = D(Doc(kq));
                kqChuoi = kq;
                DatDapAnChuoi(k, d);
                DatTrangThaiChuoi($"Xong · {SoBuocChuoi} bước · {Time.realtimeSinceStartup - t0:0.0} s", false);
            }
            catch (Exception e) { k.DatLoi("Kết quả lỗi định dạng: " + e.Message); }
        }

        /// <summary>Dòng tiến trình kiểu trợ lý AI: bước đã xong ✓, bước đang làm ●, bước sau mờ.</summary>
        static string TienTrinh(string[] ds, int dang, float giay)
        {
            var sb = new StringBuilder();
            string mo = ColorUtility.ToHtmlStringRGB(InkMute), nhan = ColorUtility.ToHtmlStringRGB(Accent);
            for (int i = 0; i < ds.Length; i++)
            {
                if (i > 0) sb.Append("   ");
                if (i < dang) sb.Append("✓ ").Append(ds[i]);
                else if (i == dang) sb.Append($"<color=#{nhan}><b>● {ds[i]}…</b></color>");
                else sb.Append($"<color=#{mo}>{ds[i]}</color>");
            }
            return sb.Append($"   <color=#{mo}>{giay:0} s</color>").ToString();
        }

        static List<(string tu, string den, string dk, string ten)> BuocChuoi(Dictionary<string, object> d)
        {
            var chat = new Dictionary<string, string>();
            foreach (var o in L(Lay(d, "chat"))) { var c = D(o); chat[S(c, "ki_hieu")] = S(c, "ten"); }
            var ds = new List<(string, string, string, string)>();
            foreach (var o in L(Lay(d, "buoc")))
            {
                var b = D(o);
                ds.Add((S(b, "tu"), S(b, "den"), S(b, "dieu_kien"), chat.TryGetValue(S(b, "den"), out var tn) ? tn : ""));
            }
            return ds;
        }

        void DatDapAnChuoi(KhoiDapAn k, Dictionary<string, object> d)
        {
            var buoc = BuocChuoi(d);
            var ds = L(Lay(d, "buoc"));
            SoBuocChuoi = ds.Count;
            bool kc = B(d, "da_kiem_chung");
            string chuoi = S(d, "chuoi").Replace("->", "→");
            var loai = new List<string>();
            foreach (var o in ds) { var p = S(D(o), "phan_ung"); if (p != "" && loai.Count < 4) loai.Add(p); }
            string doc = S(d, "chuoi_doc_tu_anh");
            string tomTat = (doc != "" ? "Máy đọc từ ảnh: " + doc.Replace("->", "→") + ".  " : "") + string.Join(" · ", loai) + (ds.Count > 4 ? " · …" : "");
            var maCoChe = new HashSet<string>(GoiCoChe.DanhSachTrongMay());
            var chip = new List<(string, Action)>();
            for (int i = 0; i < ds.Count && chip.Count < 2; i++)
            {
                var b = D(ds[i]);
                string ma = Lay(b, "ma_co_che") as string;
                if (!string.IsNullOrEmpty(ma) && maCoChe.Contains(ma)) chip.Add(($"Xem cơ chế: {S(b, "tu")} → {S(b, "den")}", () => CoCheMoDun.MoMau(ma)));
            }

            k.DatDapAn((rt, w) => HinhDapAn.Chuoi(rt, buoc, w),
                $"{ds.Count} bước  ·  {chuoi}", kc ? "Đã kiểm chứng" : "Có bước chưa kiểm chứng", kc, tomTat,
                new List<(string, string, int, Action)> {
                    ("Nút Mở lớn", "Xem chi tiết từng bước", 0, () => MoLonChuoi(d)),
                    ("Nút Hỏi tiếp", "Hỏi tiếp", 1, () => k.BatHoiTiep()),
                    ("Nút Xuất PNG", "Xuất ảnh", 2, () => StartCoroutine(XuatPng(k.Hinh, "chuoi"))),
                    ("Nút Báo sai chuỗi", "Báo sai", 2, () => baoSai.Mo("chuoi", "", S(d, "chuoi"), -1, "")) },
                chip, (cau, tra) => StartCoroutine(HoiDap(cau, "Chuỗi " + S(d, "chuoi"), "", ds, tra)), null,
                () => bangLon.Mo("Chuỗi phản ứng  ·  " + chuoi, than =>
                {
                    var nd = BangLon.VungCuon(than, 1000);
                    var h = MenuKit.Stretch(nd, "Hình phóng to", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -10), new Vector2(0, -10));
                    float cao = HinhDapAn.Chuoi(h, buoc, Mathf.Max(800, than.rect.width - 20), 1.7f);
                    h.offsetMin = new Vector2(0, -10 - cao);
                    nd.sizeDelta = new Vector2(0, cao + 30);
                }, 0.9f));
        }

        /// <summary>Bảng lớn của chuỗi: hình chuỗi to (có tên chất) + từng bước chi tiết, bước có cơ chế mẫu mở được 3D ⇄ Giấy.</summary>
        void MoLonChuoi(Dictionary<string, object> d)
        {
            bangLon.Mo("Chuỗi phản ứng  ·  " + S(d, "chuoi").Replace("->", "→"), than =>
            {
                var nd = BangLon.VungCuon(than, 2000);
                float rong = Mathf.Max(800, than.rect.width - 10);
                var hinh = MenuKit.Stretch(nd, "Hình lớn", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -10), Vector2.zero);
                float cao = HinhDapAn.Chuoi(hinh, BuocChuoi(d), rong, 1.3f);
                hinh.offsetMin = new Vector2(0, -cao);
                float y = -cao - 24;
                var maCoChe = new HashSet<string>(GoiCoChe.DanhSachTrongMay());
                int i = 0;
                foreach (var o in L(Lay(d, "buoc")))
                {
                    var b = D(o);
                    i++;
                    bool kcb = S(b, "kiem_chung") == "da_kiem_chung";
                    var the = Card(nd, "Bước lớn " + i, 0, 1, y, 96, 0, false, TrangCuon.BoThe);
                    var th = Body(the);
                    var so = Fill(th, "Số", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -24), new Vector2(40, 40), AccentSoft, 10f);
                    GlassKit.Label(so.transform, "Chữ", i.ToString(), 17, Accent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(40, 40));
                    GlassKit.Label(th, "Phương trình", $"{S(b, "tu")}  →  {S(b, "den")}    {S(b, "phan_ung")}", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(80, -16), new Vector2(1000, 28)).horizontalOverflow = HorizontalWrapMode.Overflow;
                    GlassKit.Label(th, "Chi tiết", $"Điều kiện: {S(b, "dieu_kien", "–")}   ·   kiểu: {S(b, "loai", "–")}   ·   {(kcb ? "đã kiểm chứng" : "chưa kiểm chứng")}", 15, kcb ? InkDim : Warn, false, TextAnchor.UpperLeft, new Vector2(80, -52), new Vector2(1000, 24)).horizontalOverflow = HorizontalWrapMode.Overflow;
                    string ma = Lay(b, "ma_co_che") as string;
                    if (!string.IsNullOrEmpty(ma) && maCoChe.Contains(ma))
                    {
                        var nx = MakeButton(th, "Nút Xem cơ chế " + i, (S(b, "khop_mau") == "dung_mau" ? "Xem cơ chế" : "Cơ chế tương tự") + "  →", new Vector2(-20, 0), new Vector2(200, 42), () => { bangLon.Dong(); CoCheMoDun.MoMau(ma); }, 0, TrangCuon.BoNut, new Vector2(1, 0.5f));
                        ((RectTransform)nx.transform).pivot = new Vector2(1, 0.5f);
                    }
                    y -= 106;
                }
                nd.sizeDelta = new Vector2(0, -y + 20);
            });
        }

        /// <summary>Hỏi tiếp (dùng /co-che/hoi-dap): gửi kèm đề và các bước của đáp án đang xem.</summary>
        IEnumerator HoiDap(string cau, string ten, string de, List<object> buoc, Action<string> tra)
        {
            var sb = new StringBuilder("{\"cau_hoi\":").Append(MayChuCoChe.ChuoiJson(cau)).Append(",\"ten\":").Append(MayChuCoChe.ChuoiJson(ten))
                .Append(",\"de\":").Append(MayChuCoChe.ChuoiJson(de)).Append(",\"buoc\":[");
            for (int i = 0; i < buoc.Count; i++)
            {
                var b = D(buoc[i]);
                sb.Append(i > 0 ? "," : "").Append("{\"ten\":").Append(MayChuCoChe.ChuoiJson($"{S(b, "tu")} → {S(b, "den")}"))
                  .Append(",\"ghi_chu\":").Append(MayChuCoChe.ChuoiJson($"{S(b, "phan_ung")} ({S(b, "dieu_kien")})")).Append('}');
            }
            sb.Append("]}");
            yield return MayChuCoChe.Goi("POST", "/co-che/hoi-dap", sb.ToString(), 160f, (ok, kq) =>
            {
                if (!ok) { tra("Chưa hỏi được: " + kq); return; }
                try { tra(S(D(Doc(kq)), "tra_loi")); } catch (Exception e) { tra("Trả lời lỗi định dạng: " + e.Message); }
            });
        }

        /// <summary>Xuất PNG một vùng (hình đáp án) bằng camera của canvas, gấp đôi độ phân giải, lưu vào Ảnh/VERA Lab.</summary>
        public IEnumerator XuatPng(RectTransform vung, string ten)
        {
            DuongPngCuoi = "";
            var cv = GetComponentInParent<Canvas>()?.rootCanvas;
            var cam = cv != null ? cv.worldCamera : null;
            if (cam == null || vung == null) { Bao("Không xuất được ảnh trên màn này."); yield break; }
            yield return null;
            var c = new Vector3[4];
            vung.GetWorldCorners(c);
            var cu = cam.targetTexture;
            int W = cam.pixelWidth, H = cam.pixelHeight;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            var rt = RenderTexture.GetTemporary(W * 2, H * 2, 24, RenderTextureFormat.ARGB32);
            float kx = 2f * W / Mathf.Max(1, cam.pixelWidth), ky = 2f * H / Mathf.Max(1, cam.pixelHeight);
            cam.targetTexture = rt;
            foreach (var t in cv.GetComponentsInChildren<Text>()) t.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = cu;
            var r = Rect.MinMaxRect(Mathf.Clamp(a.x * kx - 12, 0, W * 2), Mathf.Clamp(a.y * ky - 12, 0, H * 2), Mathf.Clamp(b.x * kx + 12, 0, W * 2), Mathf.Clamp(b.y * ky + 12, 0, H * 2));
            var truoc = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Mathf.Max(1, (int)r.width), Mathf.Max(1, (int)r.height), TextureFormat.RGB24, false);
            tex.ReadPixels(r, 0, 0);
            tex.Apply();
            RenderTexture.active = truoc;
            RenderTexture.ReleaseTemporary(rt);
            foreach (var t in cv.GetComponentsInChildren<Text>()) t.SetAllDirty();
            try
            {
                string goc = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
                string thu = string.IsNullOrEmpty(goc) ? System.IO.Path.Combine(Application.persistentDataPath, "Anh") : System.IO.Path.Combine(goc, "VERA Lab");
                System.IO.Directory.CreateDirectory(thu);
                DuongPngCuoi = System.IO.Path.Combine(thu, $"{ten}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                System.IO.File.WriteAllBytes(DuongPngCuoi, tex.EncodeToPNG());
                Bao("Đã lưu ảnh: " + DuongPngCuoi, false);
            }
            catch (Exception e) { Bao("Không lưu được ảnh: " + e.Message); }
            Destroy(tex);
        }

        public string DuongPngCuoi { get; private set; } = "";

        // ================================================================== CƠ CHẾ
        InputField oDe;
        RectTransform vanBanDe, anhDe, mauDe, luoiMau;
        RawImage xemAnhDe;
        Text tenAnhDe, trangThaiDe;
        Button nutVe;
        byte[] anhDeDu;
        TrangCuon.TheTab tabDe;
        string loaiCoChe = "";
        float yKetQuaDe;
        int mucTuDuy = 1;
        MenuTabs tabTuDuy;
        public int MucTuDuy => mucTuDuy;
        static readonly float[] ChoTuDuy = { 60f, 120f, 150f, 180f };
        bool daNapMau;
        public BaiCoChe BaiKetQua { get; private set; }
        public bool DangVeCoChe { get; private set; }
        public int SoBaiMau { get; private set; }

        static readonly (string ma, string ten)[] LoaiCoChe =
        {
            ("", "Tự động nhận diện"), ("SN2", "Thế SN2"), ("SN1", "Thế SN1"), ("E2", "Tách E2"), ("E1", "Tách E1"),
            ("AE", "Cộng electrophin (Markovnikov)"), ("SEAR", "Thế electrophin vòng benzen"), ("AN", "Cộng nucleophin vào C=O"),
            ("ESTE_HOA", "Este hoá (H⁺)"), ("THUY_PHAN_ESTE", "Thuỷ phân este (kiềm)"), ("KHAC", "Khác – để AI viết"),
        };

        TrangCuon DungTrangCoChe()
        {
            var t = TrangCuon.Tao(vung, "Trang Cơ chế", true);
            trang[2] = t;
            t.ThanhPhu("Cơ chế", () => MoTrang(Trang.Chu), new[] { "Nhập đề", "Kiểu cơ chế", "Kết quả" });
            t.DauTrang("Công cụ · Cơ chế phản ứng", "Cơ chế phản ứng",
                       "Nhập một phản ứng – máy vẽ cơ chế với <b>mũi tên cong</b> (đẩy electron), RDKit kiểm từng bước, " +
                       "rồi xem electron chạy trong <b>3D</b> và chuyển sang <b>tờ giấy</b> như đáp án.");
            t.GhiBuoc(0, t.DauMuc("01", "Nhập đề bài", "Gõ phản ứng, chọn ảnh chụp đề, hoặc mở ngay một trong các bài mẫu đã kiểm chứng."));
            var hangTab = t.Khoi("Tab nhập", 116, 18);
            tabDe = TrangCuon.NhomTheTab(hangTab, new[] { "Văn bản", "Hình ảnh", "Bài mẫu" },
                new[] { "Phản ứng, đề bài hoặc SMILES", "Chọn ảnh chụp đề (≤ 8 MB)", "Mở ngay, không cần mạng" },
                new[] { "T", "▣", "≡" }, ChonTabDe);
            t.LanLuot.Add(hangTab);

            var khoiNhap = t.Khoi("Khối nhập đề", 300, 40);
            var than = TrangCuon.The(khoiNhap, "Thẻ nhập đề", 0, 300);
            t.LanLuot.Add(khoiNhap);
            vanBanDe = Full(than, "Nhập văn bản");
            TrangCuon.NhanNho(vanBanDe, "Nhãn", "Phản ứng", new Vector2(28, -24), 400);
            oDe = TrangCuon.ONhieuDong(vanBanDe, "Ô đề", "Ví dụ: CH3CH2Br + OH⁻ → CH3CH2OH + Br⁻  ·  propen + HBr  ·  este hoá axit axetic với etanol  ·  SMILES: CCBr.[OH-]",
                                       new Vector2(28, -56), new Vector2(100, 150), 18);
            var ort = (RectTransform)oDe.transform; ort.anchorMax = new Vector2(1, 1); ort.offsetMax = new Vector2(-28, ort.offsetMax.y);
            var chips = new List<(string, string, Action)>();
            foreach (var g in new[] { "CH3CH2Br + OH⁻ → CH3CH2OH + Br⁻", "propen + HBr", "2-brompropan + KOH/etanol, đun nóng", "CH3COOH + C2H5OH (H2SO4 đặc)", "benzen + HNO3/H2SO4 đặc", "CH3CHO + HCN" })
            {
                string gg = g;
                chips.Add(("Gợi ý đề", g, () => oDe.text = gg));
            }
            TrangCuon.XepChip(vanBanDe, chips, 28, -222, 1560, 14);
            anhDe = Full(than, "Nhập hình ảnh");
            TrangCuon.NhanNho(anhDe, "Nhãn", "Ảnh đề", new Vector2(28, -24), 400);
            var khung = Fill(anhDe, "Khung ảnh", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -56), new Vector2(300, 210), Well, 12f);
            Ring(khung.transform, "Viền", HairStrong, 12f);
            GlassKit.Label(khung.transform, "Gợi ý", "Chưa có ảnh", 15, InkMute, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300, 210));
            xemAnhDe = new GameObject("Ảnh", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            xemAnhDe.transform.SetParent(khung.transform, false);
            var xr = xemAnhDe.rectTransform; xr.anchorMin = Vector2.zero; xr.anchorMax = Vector2.one; xr.offsetMin = new Vector2(6, 6); xr.offsetMax = new Vector2(-6, -6);
            xemAnhDe.gameObject.SetActive(false);
            var chon = MakeButton(anhDe, "Nút Chọn ảnh đề", ChonTepHeThong.HoTro ? "Chọn ảnh đề…" : "Ảnh đề (sắp có trên iPad)", new Vector2(360, -56), new Vector2(220, 48), ChonAnhDe, 1);
            chon.interactable = ChonTepHeThong.HoTro;
            MakeButton(anhDe, "Nút Bỏ ảnh đề", "Bỏ ảnh", new Vector2(592, -56), new Vector2(110, 48), () => DatAnhDe(null, ""), 1);
            tenAnhDe = GlassKit.Label(anhDe, "Tên ảnh", "", 15, InkDim, false, TextAnchor.UpperLeft, new Vector2(360, -120), new Vector2(1000, 90));
            tenAnhDe.horizontalOverflow = HorizontalWrapMode.Wrap; tenAnhDe.supportRichText = true;
            DatAnhDe(null, "");
            mauDe = Full(than, "Nhập bài mẫu");
            TrangCuon.NhanNho(mauDe, "Nhãn", "Bài mẫu đã kiểm chứng – bấm để mở 3D ⇄ Giấy", new Vector2(28, -24), 800);
            luoiMau = GlassKit.Rect(mauDe, "Lưới bài mẫu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -56), new Vector2(100, 230));
            luoiMau.anchorMax = new Vector2(1, 1); luoiMau.offsetMax = new Vector2(-28, luoiMau.offsetMax.y);
            ChonTabDe(0);

            // 02 kiểu cơ chế
            t.GhiBuoc(1, t.DauMuc("02", "Kiểu cơ chế", "Để Tự động nếu chưa chắc; chọn đúng kiểu giúp AI viết nhanh và đúng hơn."));
            var khoiTc = t.Khoi("Khối kiểu cơ chế", 262, 30);
            var tcThan = TrangCuon.The(khoiTc, "Thẻ kiểu cơ chế", 0, 262);
            TrangCuon.NhanNho(tcThan, "Nhãn", "Loại cơ chế", new Vector2(28, -24), 300);
            VeChipLoai(tcThan);
            // mức tư duy: nghĩ càng kỹ càng lâu, tối đa 3 phút cho một cơ chế (mức Tối đa: đề HSG quốc gia)
            TrangCuon.NhanNho(tcThan, "Nhãn tư duy", "Mức tư duy", new Vector2(28, -170), 300);
            var gtTd = GlassKit.Label(tcThan, "Giải thích tư duy", "Nhanh ≤ 1 phút · Cân bằng ≤ 2 phút · Kỹ ≤ 2,5 phút · Tối đa (HSG quốc gia) ≤ 3 phút", 13, InkMute, false, TextAnchor.MiddleLeft, new Vector2(700, -196), new Vector2(800, 40));
            gtTd.horizontalOverflow = HorizontalWrapMode.Overflow;
            tabTuDuy = Tabs(tcThan, "Mức tư duy", new[] { "Tư duy Nhanh", "Tư duy Cân bằng", "Tư duy Kỹ", "Tư duy Tối đa" }, new[] { "Nhanh", "Cân bằng", "Kỹ", "Tối đa · HSG QG" },
                            new Vector2(28, -196), new Vector2(150, 40), false, 4f, 1, i => mucTuDuy = i);
            t.LanLuot.Add(khoiTc);
            var khoiNut = t.Khoi("Khối nút vẽ", 60, 60);
            nutVe = MakeButton(khoiNut, "Nút Vẽ cơ chế", "Vẽ cơ chế  →", Vector2.zero, new Vector2(210, 56), VeCoChe, 0, RadiusButton);
            trangThaiDe = GlassKit.Label(khoiNut, "Trạng thái", "AI viết cơ chế → RDKit kiểm từng mũi tên (AI tự sửa nếu sai) → dựng 3D và sơ đồ giấy. Thường 20–120 giây.", 15, InkMute, false, TextAnchor.MiddleLeft, new Vector2(232, 0), new Vector2(1300, 56));
            trangThaiDe.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.LanLuot.Add(khoiNut);

            t.GhiBuoc(2, t.DauMuc("03", "Kết quả", "Mỗi lần vẽ là một khối: đề ở trên, sơ đồ cơ chế trên giấy bên dưới. Mở lớn để xem 3D ⇄ Giấy."));
            t.ThanhSoan("Gõ phản ứng rồi Enter, ví dụ: CH3CH2Br + OH⁻  ·  propen + HBr", "Vẽ cơ chế", c =>
            {
                oDe.text = c; tabDe.Chon(0, false); ChonTabDe(0); VeCoChe();
            });
            dongCoChe = DongThoiGian.Tao(t, "Chưa có cơ chế", "Nhập phản ứng ở mục <b>01</b> rồi bấm <b>Vẽ cơ chế</b>, hoặc mở một <b>bài mẫu</b>.");
            return t;
        }

        void VeChipLoai(RectTransform cha)
        {
            for (int i = cha.childCount - 1; i >= 0; i--) if (cha.GetChild(i).name.StartsWith("Loại ")) Destroy(cha.GetChild(i).gameObject);
            float x = 28, y = -56;
            foreach (var (ma, ten) in LoaiCoChe)
            {
                float w = 34 + ten.Length * 8.6f;
                if (x + w > 1560) { x = 28; y -= 50; }
                string m = ma;
                bool d = ma == loaiCoChe;
                var b = MakeButton(cha, "Loại " + (ma == "" ? "TU_DONG" : ma), ten, new Vector2(x, y), new Vector2(w, 40), () => { loaiCoChe = m; VeChipLoai(cha); }, d ? 0 : 1, TrangCuon.BoNut);
                var chu = b.transform.Find("Chữ").GetComponent<Text>();
                chu.fontSize = 15;
                if (!d) { chu.color = InkDim; chu.fontStyle = FontStyle.Normal; }
                x += w + 8;
            }
        }

        void ChonTabDe(int i)
        {
            vanBanDe.gameObject.SetActive(i == 0);
            anhDe.gameObject.SetActive(i == 1);
            mauDe.gameObject.SetActive(i == 2);
            if (i == 2) NapBaiMau();
            MenuAnim.Enter(i == 0 ? vanBanDe : i == 1 ? anhDe : mauDe, 0f, 6f, 0.25f);
        }

        /// <summary>Lưới bài mẫu: đọc tên từ các gói .verapack (qua CoCheMoDun, chưa mở lớp phủ).</summary>
        void NapBaiMau()
        {
            if (daNapMau || luoiMau == null) return;
            daNapMau = true;
            var md = CoCheMoDun.DamBaoCo();
            int n = md.DanhSachMa.Count;
            SoBaiMau = n;
            for (int i = 0; i < n; i++)
            {
                string ma = md.DanhSachMa[i];
                string ten = ma;
                try { ten = md.Nap(ma).Ten; } catch { ten = ma + " (gói lỗi)"; }
                int cot = i % 4, hang = i / 4;
                var the = Card(luoiMau, "Bài mẫu " + ma, cot / 4f, (cot + 1) / 4f, -hang * 108, 98, 14, true, 16f);
                MakeCardClickable(the, () => MoBaiMau(ma), 2f);
                var th = Body(the);
                TrangCuon.NhanNho(th, "Số", $"Bài {i + 1:00}", new Vector2(18, -16), 200, Accent);
                var tt = TrangCuon.ChuRong(th, "Tên", ten, 15, Ink, true, 18, -40, 50, 18);
                tt.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (n == 0) GlassKit.Label(luoiMau, "Trống", "Chưa có gói cơ chế trong StreamingAssets/CoChe.", 15, Warn, false, TextAnchor.UpperLeft, Vector2.zero, new Vector2(900, 30));
        }

        public void MoBaiMau(string ma)
        {
            var md = CoCheMoDun.DamBaoCo();
            BaiCoChe b;
            try { b = md.Nap(ma); }
            catch (Exception e) { DatTrangThaiDe("Không mở được bài mẫu: " + e.Message, true); return; }
            var k = dongCoChe.Them("Bài mẫu: " + b.Ten, null);
            DatDapAnCoChe(k, b, "Mẫu đã kiểm chứng", true);
        }

        void ChonAnhDe()
        {
            StartCoroutine(ChonTepHeThong.ChonAnh("Chọn ảnh đề Hoá", (duong, loi) =>
            {
                if (!string.IsNullOrEmpty(loi)) { DatTrangThaiDe(loi, true); return; }
                if (string.IsNullOrEmpty(duong)) return;
                var (du, loiDoc) = ChonTepHeThong.DocTep(duong, CoCheMoDun.AnhToiDa);
                if (du == null) { DatTrangThaiDe(loiDoc, true); return; }
                DatAnhDe(du, System.IO.Path.GetFileName(duong));
            }));
        }

        public void DatAnhDe(byte[] du, string ten)
        {
            if (du != null && du.Length > 7 * 1024 * 1024)
            {
                var nen = new Texture2D(2, 2);
                if (nen.LoadImage(du)) du = nen.EncodeToJPG(85);
                Destroy(nen);
            }
            anhDeDu = du;
            if (xemAnhDe.texture != null) Destroy(xemAnhDe.texture);
            xemAnhDe.texture = null;
            xemAnhDe.gameObject.SetActive(false);
            if (du != null)
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(du))
                {
                    xemAnhDe.texture = tex;
                    xemAnhDe.gameObject.SetActive(true);
                    var f = xemAnhDe.gameObject.GetComponent<AspectRatioFitter>() ?? xemAnhDe.gameObject.AddComponent<AspectRatioFitter>();
                    f.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    f.aspectRatio = tex.width / (float)Mathf.Max(1, tex.height);
                }
                else Destroy(tex);
            }
            tenAnhDe.text = du == null
                ? "AI đọc ảnh đề (Claude, dự phòng Gemini), viết cơ chế rồi RDKit kiểm.\nCó thể ghi thêm ở tab Văn bản, ví dụ \"chỉ câu 2\"."
                : $"<b>{ten}</b> ({du.Length / 1024f:0} KB) – sẽ gửi kèm khi bấm Vẽ cơ chế.";
        }

        void DatTrangThaiDe(string chu, bool loi)
        {
            trangThaiDe.text = chu;
            trangThaiDe.color = loi ? Error : InkMute;
            if (loi) MenuAnim.Shake(trangThaiDe.rectTransform);
        }

        public void VeCoChe()
        {
            if (DangVeCoChe) return;
            string de = (oDe.text ?? "").Trim();
            byte[] anh = tabDe.Dang == 1 ? anhDeDu : null;
            if (tabDe.Dang == 2) { DatTrangThaiDe("Ở tab Bài mẫu, bấm vào một bài để mở ngay.", false); return; }
            if (de.Length == 0 && anh == null) { DatTrangThaiDe(tabDe.Dang == 1 ? "Hãy chọn ảnh đề." : "Hãy gõ phản ứng hoặc chọn một gợi ý.", true); return; }
            StartCoroutine(GoiVeCoChe(de, anh));
        }

        IEnumerator GoiVeCoChe(string de, byte[] anh)
        {
            DangVeCoChe = true;
            nutVe.interactable = false;
            var k = dongCoChe.Them(de.Length > 0 ? de : "Ảnh đề cơ chế", anh != null ? xemAnhDe.texture : null);
            float t0 = Time.realtimeSinceStartup;
            bool xong = false, ok = false;
            string kq = "";
            StartCoroutine(MayChuCoChe.GiaiDe(de, anh, loaiCoChe, ChoTuDuy[mucTuDuy] + 30f, (o, x) => { ok = o; kq = x; xong = true; }, mucTuDuy));
            string[] buocLam = anh != null ? new[] { "Đọc ảnh đề", "AI viết cơ chế", "RDKit kiểm từng mũi tên", "Dựng 3D và giấy" } : new[] { "AI viết cơ chế", "RDKit kiểm từng mũi tên", "Dựng 3D và giấy" };
            while (!xong)
            {
                float s = Time.realtimeSinceStartup - t0;
                k.DatTienTrinh(TienTrinh(buocLam, Mathf.Min(buocLam.Length - 1, (int)(s / 12f)), s));
                DatTrangThaiDe($"Đang vẽ cơ chế… {s:0} s", false);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            DangVeCoChe = false;
            nutVe.interactable = true;
            if (!ok) { k.DatLoi(kq); DatTrangThaiDe("Chưa vẽ được – xem khối đáp án.", true); yield break; }
            try
            {
                var b = BaiCoChe.TuJson(kq);
                if (b.KichBan == null || b.KichBan.Buoc.Count == 0) throw new Exception("kết quả không có bước nào");
                var tt = D(Lay(D(Doc(kq)), "thong_tin"));
                int sua = I(Lay(tt, "so_lan_sua"));
                CoCheMoDun.DamBaoCo().GhiDeDaGiai(b);
                DatDapAnCoChe(k, b, "AI viết · RDKit đã kiểm" + (sua > 0 ? $" (sửa {sua} lần)" : ""), false);
                DatTrangThaiDe($"Xong: {b.KichBan.Buoc.Count} bước · {Time.realtimeSinceStartup - t0:0.0} s", false);
            }
            catch (Exception e) { k.DatLoi("Kết quả lỗi định dạng: " + e.Message); }
        }

        void DatDapAnCoChe(KhoiDapAn k, BaiCoChe b, string nguon, bool mau)
        {
            BaiKetQua = b;
            var sd = b.SoDo;
            float tiLe = sd != null && sd.X1 - sd.X0 > 1 ? (sd.Y1 - sd.Y0) / (sd.X1 - sd.X0) : 0.4f;
            var ten = new List<string>();
            foreach (var bu in b.KichBan.Buoc) ten.Add(bu.Ten);
            string tomTat = (string.IsNullOrEmpty(b.KichBan.Loai) ? "" : b.KichBan.Loai + " · ") + $"{b.KichBan.Buoc.Count} bước: " + string.Join(" → ", ten);
            var buocJson = new List<object>();
            foreach (var bu in b.KichBan.Buoc) buocJson.Add(new Dictionary<string, object> { { "tu", bu.Ten }, { "den", "" }, { "phan_ung", bu.GhiChu }, { "dieu_kien", "" } });
            k.DatDapAn((rt, w) => { float hp = Mathf.Clamp(w * tiLe, 200, 460); HinhDapAn.Giay(rt, b, 0, -1); return hp; },
                b.Ten, nguon, mau, "",
                new List<(string, string, int, Action)> {
                    ("Nút Xem 3D", "▶  Xem 3D", 0, () => k.BatKhu3D(Mathf.Clamp(dongCoChe.RongNoiDung * 0.3f, 300, 440) + 54, c => DungKhu3D(c, b), () => DongXem3D(b))),
                    ("Nút Hỏi tiếp", "Hỏi tiếp", 1, () => k.BatHoiTiep()),
                    ("Nút Xuất PNG", "Xuất ảnh", 2, () => StartCoroutine(XuatPng(k.Hinh, "co_che_" + b.Ma))),
                    ("Nút Báo sai cơ chế", "Báo sai", 2, () => baoSai.Mo("co_che", b.Ma, b.Ten, -1, "")) },
                null, (cau, tra) => StartCoroutine(HoiDap(cau, b.Ten, b.De ?? "", buocJson, tra)), null,
                () => bangLon.Mo("Cơ chế  ·  " + b.Ten, than => HinhDapAn.Giay(than, b, 0, -1), 0.9f));
            k.dong3DNhan = mo => { var n = k.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == "Nút Xem 3D"); if (n != null) n.transform.Find("Chữ").GetComponent<Text>().text = mo ? "▲  Ẩn 3D" : "▶  Xem 3D"; };
        }

        // ---- 3D ngay dưới tờ giấy trong khối đáp án (chỉ khối mới nhất giữ trình phát chạy; khối cũ có nút "Xem lại 3D")
        public Xem3DNho Xem3DMoi { get; private set; }
        RectTransform khung3DMoi;
        BaiCoChe baiXem3D;

        /// <summary>Khu 3D dưới hàng nút của khối Cơ chế: khung bo góc 14 (Mask theo hình bo), thanh điều khiển gọn, Toàn màn hình.</summary>
        void DungKhu3D(RectTransform c, BaiCoChe b)
        {
            float h3 = c.rect.height - 54;
            var khung = MenuKit.Stretch(c, "Khung 3D đáp án", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -h3), Vector2.zero);
            var nen = khung.gameObject.AddComponent<Image>();
            nen.sprite = Round(14f); nen.type = Image.Type.Sliced; nen.color = Color.white;
            var mk = khung.gameObject.AddComponent<Mask>(); mk.showMaskGraphic = false;
            var anh = MenuKit.Full(khung, "Ảnh 3D");
            var dk = MenuKit.Stretch(c, "Điều khiển 3D đáp án", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -h3 - 54), new Vector2(0, -h3 - 10));
            MakeButton(dk, "Nút Bước trước 3D", "‹", Vector2.zero, new Vector2(44, 44), () => { if (baiXem3D == b) Xem3DMoi?.TP.BuocTruoc(); }, 1, TrangCuon.BoNut);
            MakeButton(dk, "Nút Phát 3D", "Phát / Dừng", new Vector2(50, 0), new Vector2(130, 44), () => { if (baiXem3D != b) TaoXem3D(anh, b); else Xem3DMoi?.TP.PhatDung(); }, 0, TrangCuon.BoNut);
            MakeButton(dk, "Nút Bước sau 3D", "›", new Vector2(186, 0), new Vector2(44, 44), () => { if (baiXem3D == b) Xem3DMoi?.TP.BuocSau(); }, 1, TrangCuon.BoNut);
            GlassKit.Label(dk, "Nhãn tốc độ", "Tốc độ", 14, InkDim, false, TextAnchor.MiddleLeft, new Vector2(252, 0), new Vector2(60, 44));
            var chuToc = GlassKit.Label(dk, "Giá trị tốc độ", "1.00×", 14, Ink, false, TextAnchor.MiddleLeft, new Vector2(552, 0), new Vector2(60, 44));
            CoCheMoDun.TaoThanhTruot(dk, new Vector2(312, -12), new Vector2(228, 20), 0.25f, 3f, 1f, v => { if (baiXem3D == b && Xem3DMoi != null) Xem3DMoi.TP.TocDo = v; chuToc.text = v.ToString("0.00") + "×"; });
            Button nutAnH = null, nutE = null;
            nutAnH = MakeButton(dk, "Nút Ẩn H đáp án", "Ẩn H", new Vector2(624, 0), new Vector2(76, 44), () =>
            {
                if (baiXem3D != b || Xem3DMoi == null) return;
                Xem3DMoi.TP.AnH = !Xem3DMoi.TP.AnH; Face(nutAnH).color = Xem3DMoi.TP.AnH ? AccentSoft : Paper;
            }, 1, TrangCuon.BoNut);
            nutE = MakeButton(dk, "Nút Hiện e đáp án", "Hiện e", new Vector2(706, 0), new Vector2(86, 44), () =>
            {
                if (baiXem3D != b || Xem3DMoi == null) return;
                Xem3DMoi.TP.HienE = !Xem3DMoi.TP.HienE; Face(nutE).color = Xem3DMoi.TP.HienE ? AccentSoft : Paper;
            }, 1, TrangCuon.BoNut);
            var to = MakeButton(dk, "Nút Toàn màn hình 3D", "⤢  Toàn màn hình", Vector2.zero, new Vector2(170, 44), () =>
            {
                int bu = Xem3DMoi != null && baiXem3D == b ? Xem3DMoi.TP.ChiSoBuoc : 0;
                CoCheMoDun.MoVoiBai(b, -1, bu);
                CoCheMoDun.Instance?.DatToanMan(true);
                if (CoCheMoDun.Instance != null)
                {
                    CoCheMoDun.Instance.DongKhiThuNho = true;              // Thu nhỏ → về lại trang đáp án này
                    CoCheMoDun.Instance.SauKhiThuNho = buoc => { if (Xem3DMoi != null && baiXem3D == b) Xem3DMoi.TP.DenBuoc(buoc); };
                }
            }, 1, TrangCuon.BoNut, new Vector2(1, 1));
            ((RectTransform)to.transform).pivot = new Vector2(1, 1);
            TaoXem3D(anh, b);
        }

        void DongXem3D(BaiCoChe b)
        {
            if (Xem3DMoi != null && baiXem3D == b) { Destroy(Xem3DMoi.gameObject); Xem3DMoi = null; baiXem3D = null; khung3DMoi = null; }
        }

        void TaoXem3D(RectTransform k3, BaiCoChe b)
        {
            if (Xem3DMoi != null)
            {
                var cu = khung3DMoi; var baiCu = baiXem3D;
                Destroy(Xem3DMoi.gameObject);
                if (cu != null && cu != k3)
                {
                    var nut = MakeButton(cu, "Nút Xem lại 3D", "▶  Xem lại 3D", Vector2.zero, new Vector2(180, 48), null, 0, TrangCuon.BoNut, new Vector2(0.5f, 0.5f));
                    ((RectTransform)nut.transform).pivot = new Vector2(0.5f, 0.5f);
                    nut.onClick.AddListener(() => { Destroy(nut.gameObject); TaoXem3D(cu, baiCu); });
                }
            }
            foreach (Transform c in k3) if (c.name == "Nút Xem lại 3D") Destroy(c.gameObject);
            Xem3DMoi = Xem3DNho.Tao(k3, b.KichBan, GetComponentInParent<Canvas>().rootCanvas.worldCamera);
            khung3DMoi = k3; baiXem3D = b;
            Xem3DMoi.TP.DenBuoc(0, false);                       // 20/11: không tự chạy – bấm Phát mới phát
        }

        static string LoiGiaiCoChe(BaiCoChe b)
        {
            var sb = new StringBuilder();
            var gd = b.SoDo != null ? b.SoDo.GiaiDoanDs : null;
            var daGhi = new HashSet<int>();
            if (gd != null)
                foreach (var g in gd)
                {
                    if (g.NhanhSai >= 0 || g.Buoc.Count == 0) continue;
                    if (!string.IsNullOrEmpty(g.TieuDe)) sb.Append("<b>").Append(g.TieuDe.TrimEnd(':', ' ')).Append("</b>\n");
                    foreach (var i in g.Buoc) if (i >= 0 && i < b.KichBan.Buoc.Count && daGhi.Add(i)) DongBuoc(sb, b, i);
                }
            for (int i = 0; i < b.KichBan.Buoc.Count; i++) if (daGhi.Add(i)) DongBuoc(sb, b, i);
            return sb.ToString().TrimEnd('\n');
        }

        static void DongBuoc(StringBuilder sb, BaiCoChe b, int i)
        {
            var bu = b.KichBan.Buoc[i];
            sb.Append($"<b>{i + 1}. {bu.Ten}</b>" + (bu.BuocCham ? " (bước chậm)" : "") + $" – {bu.GhiChu}\n");
        }

        public Xem3DNho Bang3D { get; private set; }

        /// <summary>
        /// Bảng lớn của cơ chế: trái là tờ giấy hoặc 3D chạy ngay trong bảng (Giấy | 3D), phải là điều khiển 3D (‹ Phát ›, tốc độ
        /// 0,25×–3×, toàn màn hình) và các bước (bấm → 3D tới bước đó).
        /// </summary>
        void MoLonCoChe(BaiCoChe b)
        {
            bangLon.Mo("Cơ chế  ·  " + b.Ten, than =>
            {
                float W = Mathf.Max(800, than.rect.width);
                var trai = MenuKit.Stretch(than, "Vùng trái", new Vector2(0, 0), new Vector2(0.7f, 1), Vector2.zero, new Vector2(-12, 0));
                var giay = MenuKit.Full(trai, "Vùng giấy");
                HinhDapAn.Giay(giay, b, 0, -1);
                var khung3 = MenuKit.Full(trai, "Vùng 3D");
                khung3.gameObject.AddComponent<RectMask2D>();
                khung3.gameObject.SetActive(false);
                var ben = MenuKit.Stretch(than, "Cột bước", new Vector2(0.7f, 0), new Vector2(1, 1), new Vector2(12, 0), Vector2.zero);
                float cw = W * 0.3f - 12;
                RectTransform dieuKhien = null;
                Button nutGiay = null, nut3D = null;
                void Chon(bool la3D)
                {
                    giay.gameObject.SetActive(!la3D);
                    khung3.gameObject.SetActive(la3D);
                    if (la3D && Bang3D == null) Bang3D = Xem3DNho.Tao(khung3, b.KichBan, GetComponentInParent<Canvas>().rootCanvas.worldCamera);
                    Bang3D?.Hien(la3D);
                    dieuKhien.gameObject.SetActive(la3D);
                    Face(nutGiay).color = la3D ? Paper : AccentSoft;
                    Face(nut3D).color = la3D ? AccentSoft : Paper;
                }
                nutGiay = MakeButton(ben, "Nút Bảng giấy", "Giấy", new Vector2(0, 0), new Vector2(cw * 0.5f - 4, 44), () => Chon(false), 1, TrangCuon.BoNut);
                nut3D = MakeButton(ben, "Nút Bảng 3D", "▶  3D", new Vector2(cw * 0.5f + 4, 0), new Vector2(cw * 0.5f - 4, 44), () => Chon(true), 1, TrangCuon.BoNut);
                dieuKhien = GlassKit.Rect(ben, "Điều khiển 3D", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -56), new Vector2(cw, 104));
                MakeButton(dieuKhien, "Nút Bước trước bảng", "‹", new Vector2(0, 0), new Vector2(44, 44), () => Bang3D?.TP.BuocTruoc(), 1, TrangCuon.BoNut);
                var nutPhat = MakeButton(dieuKhien, "Nút Phát bảng", "Phát / Dừng", new Vector2(50, 0), new Vector2(cw - 100 - 12, 44), () => Bang3D?.TP.PhatDung(), 0, TrangCuon.BoNut);
                MakeButton(dieuKhien, "Nút Bước sau bảng", "›", new Vector2(cw - 44, 0), new Vector2(44, 44), () => Bang3D?.TP.BuocSau(), 1, TrangCuon.BoNut);
                GlassKit.Label(dieuKhien, "Nhãn tốc độ", "Tốc độ", 14, InkDim, false, TextAnchor.MiddleLeft, new Vector2(0, -56), new Vector2(60, 40));
                var chuToc = GlassKit.Label(dieuKhien, "Giá trị tốc độ", "1.00×", 14, Ink, false, TextAnchor.MiddleRight, new Vector2(cw - 56, -56), new Vector2(56, 40));
                CoCheMoDun.TaoThanhTruot(dieuKhien, new Vector2(64, -66), new Vector2(cw - 132, 20), 0.25f, 3f, 1f, v => { if (Bang3D != null) Bang3D.TP.TocDo = v; chuToc.text = v.ToString("0.00") + "×"; });
                var nutTo = MakeButton(ben, "Nút Toàn màn hình bảng", "⤢  Toàn màn hình", new Vector2(0, -170), new Vector2(cw, 44), () =>
                {
                    int bu = Bang3D != null ? Bang3D.TP.ChiSoBuoc : 0;
                    bangLon.Dong();
                    CoCheMoDun.MoVoiBai(b, -1, bu);
                    CoCheMoDun.Instance?.DatToanMan(true);
                }, 1, TrangCuon.BoNut);
                float y = -226;
                for (int i = 0; i < b.KichBan.Buoc.Count; i++)
                {
                    var bu = b.KichBan.Buoc[i];
                    int ii = i;
                    var hang = MakeButton(ben, "Bước cơ chế lớn " + (i + 1), "", new Vector2(0, y), new Vector2(cw, 66), () => { Chon(true); Bang3D.TP.DenBuoc(ii, true); }, 1, TrangCuon.BoNut);
                    var chu = hang.transform.Find("Chữ").GetComponent<Text>();
                    chu.alignment = TextAnchor.MiddleLeft; chu.fontSize = 14; chu.fontStyle = FontStyle.Normal; chu.supportRichText = true; chu.horizontalOverflow = HorizontalWrapMode.Wrap;
                    chu.rectTransform.anchorMin = Vector2.zero; chu.rectTransform.anchorMax = Vector2.one; chu.rectTransform.offsetMin = new Vector2(14, 4); chu.rectTransform.offsetMax = new Vector2(-14, -4);
                    string gc = bu.GhiChu ?? ""; if (gc.Length > 70) gc = gc.Substring(0, 68) + "…";
                    chu.text = $"<b>{i + 1}. {bu.Ten}</b>\n<color=#{ColorUtility.ToHtmlStringRGB(InkDim)}>{gc}</color>";
                    y -= 72;
                }
                Chon(false);
            });
            bangLon.KhiDong = () => { if (Bang3D != null) { Destroy(Bang3D.gameObject); Bang3D = null; } };
        }

        // ================================================================== MÔ HÌNH 3D
        InputField oChat;

        TrangCuon DungTrang3D()
        {
            var t = TrangCuon.Tao(vung, "Trang Mô hình 3D", true);
            t.ThanhPhu("Mô hình 3D", () => MoTrang(Trang.Chu), new[] { "Nhập chất" });
            t.DauTrang("Công cụ · Mô hình 3D", "Mô hình 3D",
                       "Nhập tên (tiếng Việt hoặc tiếng Anh), công thức thu gọn hoặc SMILES của <b>một chất</b> – máy dựng cấu dạng 3D để xoay 360°, phóng to, bật tắt H.");
            t.GhiBuoc(0, t.DauMuc("01", "Chất cần dựng 3D", "Gõ chất rồi bấm Dựng, hoặc bấm một chip mẫu."));
            var k = t.Khoi("Khối nhập chất", 190, 36);
            var th = TrangCuon.The(k, "Thẻ nhập chất", 0, 190);
            TrangCuon.NhanNho(th, "Nhãn", "Chất cần dựng 3D", new Vector2(28, -24), 400);
            oChat = TextField(th, "Ô chất", "Ví dụ: ethanol · axit axetic · CH3COOC2H5 · C6H5COOH · CC(=O)OC", new Vector2(28, -56), new Vector2(100, 54), 18);
            var ort = (RectTransform)oChat.transform; ort.anchorMax = new Vector2(1, 1); ort.offsetMax = new Vector2(-28, ort.offsetMax.y);
            oChat.onSubmit.AddListener(_ => Dung3D());
            var chips = new List<(string, string, Action)>();
            foreach (var m in new[] { "ethanol", "axit axetic", "benzen", "glucozơ", "CH3COOC2H5", "aspirin", "caffeine", "CC(C)(C)C#N" })
            {
                string mm = m;
                chips.Add(("Chất mẫu " + m, m, () => { oChat.text = mm; Dung3D(); }));
            }
            TrangCuon.XepChip(th, chips, 28, -126, 1560, 14);
            t.LanLuot.Add(k);
            var kn = t.Khoi("Khối nút dựng", 60, 40);
            MakeButton(kn, "Nút Dựng mô hình 3D", "Dựng mô hình 3D  →", Vector2.zero, new Vector2(250, 56), Dung3D, 0, RadiusButton);
            GlassKit.Label(kn, "Gợi ý", "Trong khung 3D: kéo để xoay · cuộn để phóng to · Đặt lại góc · Ẩn H.", 15, InkMute, false, TextAnchor.MiddleLeft, new Vector2(272, 0), new Vector2(1100, 56));
            t.LanLuot.Add(kn);
            t.GhiBuoc(0, t.Y);
            t.DauMuc("02", "Kết quả", "Mỗi chất một khối: ảnh 3D tĩnh, công thức, khối lượng mol. Mở lớn để xoay 360°, phóng to, ẩn H.");
            t.ThanhSoan("Gõ tên hoặc công thức rồi Enter, ví dụ: ethanol · CH3COOH · CCO", "Dựng 3D", c => { oChat.text = c; Dung3D(); });
            dong3D = DongThoiGian.Tao(t, "Chưa dựng chất nào", "Gõ tên hoặc công thức rồi bấm <b>Dựng mô hình 3D</b>, hoặc bấm một chip mẫu.");
            return t;
        }

        public bool DangDung3D { get; private set; }

        void Dung3D()
        {
            string q = (oChat.text ?? "").Trim();
            if (q.Length == 0 || DangDung3D) return;
            StartCoroutine(GoiDung3D(q));
        }

        IEnumerator GoiDung3D(string q)
        {
            DangDung3D = true;
            var k = dong3D.Them(q, null);
            float t0 = Time.realtimeSinceStartup;
            bool xong = false, ok = false;
            string kq = "";
            StartCoroutine(MayChuCoChe.Goi("POST", "/co-che/mo-hinh-3d", "{\"van_ban\":" + MayChuCoChe.ChuoiJson(q) + "}", 40f, (o, x) => { ok = o; kq = x; xong = true; }));
            string[] buocLam = { "Tìm chất", "Dựng cấu dạng RDKit", "Vẽ" };
            while (!xong)
            {
                float s = Time.realtimeSinceStartup - t0;
                k.DatTienTrinh(TienTrinh(buocLam, Mathf.Min(2, (int)(s / 1.2f)), s));
                yield return new WaitForSecondsRealtime(0.2f);
            }
            DangDung3D = false;
            if (!ok) { k.DatLoi(kq); yield break; }
            try
            {
                var d = D(Doc(kq));
                string nguon = S(d, "nguon") switch { "bang_sgk" => "Bảng SGK (đã kiểm chứng)", "pubchem" => "PubChem", "smiles" => "SMILES bạn nhập", var x => x };
                k.DatDapAn((rt, w) => HinhDapAn.Ba(rt, d, w, 380), $"{S(d, "ten")}  ·  {S(d, "cong_thuc")}", nguon, S(d, "nguon") == "bang_sgk",
                    $"M = {F(Lay(d, "khoi_luong")):0.00} g/mol  ·  {L(Lay(d, "nguyen_tu")).Count} nguyên tử (kể cả H)  ·  SMILES {S(d, "smiles")}",
                    new List<(string, string, int, Action)> {
                        ("Nút Mở lớn", "Xoay 3D toàn màn hình", 0, () => { MoHinh3DMoDun.Mo(); MoHinh3DMoDun.Instance?.XemChat(q); }),
                        ("Nút Xuất PNG", "Xuất ảnh", 2, () => StartCoroutine(XuatPng(k.Hinh, "mo_hinh_3d"))),
                        ("Nút Báo sai 3D", "Báo sai", 2, () => baoSai.Mo("mo_hinh_3d", "", q, -1, "")) },
                    null, null, null,
                    () => bangLon.Mo(S(d, "ten") + "  ·  " + S(d, "cong_thuc"), than => HinhDapAn.Ba(MenuKit.Full(than, "Ảnh 3D lớn"), d, than.rect.width, than.rect.height), 0.9f));
            }
            catch (Exception e) { k.DatLoi("Kết quả lỗi định dạng: " + e.Message); }
        }

        // ================================================================== HÓA LÝ
        TrangCuon DungTrangHoaLy()
        {
            var t = TrangCuon.Tao(vung, "Trang Hóa lý", false);
            t.DauTrang("Hóa lý · Mô phỏng tương tác", "Hóa lý", "Bốn mô phỏng của CHEMISTRY viết lại trong Unity: kéo thanh trượt, xem hạt chuyển động và đồ thị thay đổi tức thì.");
            string[,] ds = { { "khi_ly_tuong", "Khí lý tưởng", "pV = nRT · hạt chuyển động, áp suất từ va chạm", "pV" },
                             { "toc_do", "Tốc độ phản ứng", "Nồng độ, nhiệt độ, xúc tác · phân bố năng lượng Ea", "v" },
                             { "can_bang", "Cân bằng Le Chatelier", "Thêm chất, đổi nhiệt độ, áp suất · cân bằng dịch chuyển", "⇌" },
                             { "dong_co_nhiet", "Động cơ nhiệt", "Chu trình trên đồ thị p–V · hiệu suất", "η" } };
            for (int i = 0; i < 4; i++)
            {
                string ma = ds[i, 0];
                var k = t.Khoi("Khối " + ds[i, 1], 120, 16);
                var the = Card(k, "Hóa lý " + ds[i, 1], 0, 1, 0, 120, 0, true, TrangCuon.BoThe);
                MakeCardClickable(the, () => MoHoaLy?.Invoke(ma), 2f);
                var th = Body(the);
                var o = Fill(th, "Biểu tượng", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(60, 60), AccentSoft, 16f);
                GlassKit.Label(o.transform, "Ký hiệu", ds[i, 3], 22, Accent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(60, 60));
                TrangCuon.NhanNho(th, "Số", $"Mô phỏng {i + 1:00}", new Vector2(116, -24), 300, Accent);
                GlassKit.Label(th, "Tên", ds[i, 1], 24, Ink, true, TextAnchor.UpperLeft, new Vector2(116, -44), new Vector2(700, 34));
                GlassKit.Label(th, "Mô tả", ds[i, 2], 16, InkDim, false, TextAnchor.UpperLeft, new Vector2(116, -80), new Vector2(900, 24));
                var nut = MakeButton(th, "Nút Mở " + ds[i, 1], "Mở mô phỏng  →", new Vector2(-30, 0), new Vector2(190, 48), () => MoHoaLy?.Invoke(ma), 0, TrangCuon.BoNut, new Vector2(1, 0.5f));
                ((RectTransform)nut.transform).pivot = new Vector2(1, 0.5f);
                t.LanLuot.Add(k);
            }
            t.ChanTrang();
            t.ChotChieuCao();
            return t;
        }
    }

    /// <summary>
    /// Hộp "Báo sai" dùng chung (trang Chuỗi, trang Cơ chế, trình phát 3D ⇄ Giấy): ô mô tả nhiều dòng → POST /co-che/bao-sai.
    /// </summary>
    public class HopBaoSai : MonoBehaviour
    {
        RectTransform goc, khung;
        InputField o;
        Text tieuDe, trangThai;
        Button nutGui;
        string nguon, maBai, ten, cheDo;
        int buoc;
        public string KetQuaCuoi { get; private set; } = "";
        public bool DangMo => goc != null && goc.gameObject.activeSelf;

        public static HopBaoSai Tao(RectTransform cha)
        {
            var g = Full(cha, "Hộp Báo sai");
            var h = g.gameObject.AddComponent<HopBaoSai>();
            h.goc = g;
            var mo = g.gameObject.AddComponent<Image>();
            mo.color = GlassKit.Scrim(UiTheme.IsLight ? 0.32f : 0.55f);
            mo.raycastTarget = true;
            h.khung = GlassKit.Rect(g, "Khung", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 470));
            var the = Card(h.khung, "Thẻ báo sai", 0, 1, 0, 470, 0, false, TrangCuon.BoTheLon);
            var th = Body(the);
            th.GetComponent<Image>().raycastTarget = true;
            Eyebrow(th, "Nhãn", "Báo sai", new Vector2(34, -30), 300, Error);
            h.tieuDe = GlassKit.Label(th, "Tiêu đề", "", 24, Ink, true, TextAnchor.UpperLeft, new Vector2(34, -52), new Vector2(690, 64));
            h.tieuDe.horizontalOverflow = HorizontalWrapMode.Wrap;
            h.o = TrangCuon.ONhieuDong(th, "Ô mô tả lỗi", "Chỗ nào sai? Ví dụ: bước 2 mũi tên phải đi từ cặp electron của O, sản phẩm chính là…", new Vector2(34, -124), new Vector2(692, 190), 17);
            h.trangThai = GlassKit.Label(th, "Trạng thái", "Báo cáo lưu trên máy chủ (data/bao_sai) để giáo viên xem lại.", 14, InkMute, false, TextAnchor.UpperLeft, new Vector2(34, -326), new Vector2(692, 44));
            h.trangThai.horizontalOverflow = HorizontalWrapMode.Wrap;
            h.nutGui = MakeButton(th, "Nút Gửi báo sai", "Gửi báo sai", new Vector2(34, -384), new Vector2(170, 50), h.Gui, 0);
            MakeButton(th, "Nút Huỷ báo sai", "Huỷ", new Vector2(216, -384), new Vector2(110, 50), h.Dong, 1);
            g.gameObject.SetActive(false);
            return h;
        }

        public void Mo(string nguon, string maBai, string ten, int buoc, string cheDo)
        {
            this.nguon = nguon; this.maBai = maBai; this.ten = ten; this.buoc = buoc; this.cheDo = cheDo;
            tieuDe.text = string.IsNullOrEmpty(ten) ? "Báo lời giải sai" : ten;
            o.text = "";
            trangThai.color = InkMute;
            trangThai.text = "Báo cáo lưu trên máy chủ (data/bao_sai) để giáo viên xem lại.";
            nutGui.interactable = true;
            goc.gameObject.SetActive(true);
            goc.SetAsLastSibling();
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, 0f, 1f, 0.2f);
            MenuAnim.Pop(khung);
            o.ActivateInputField();
        }

        public void Dong()
        {
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = false;
            MenuAnim.Fade(g, g.alpha, 0f, 0.16f, 0f, () => goc.gameObject.SetActive(false));
        }

        public void Gui() => Gui(o.text);

        public void Gui(string moTa)
        {
            moTa = (moTa ?? "").Trim();
            if (moTa.Length < 4) { trangThai.color = Error; trangThai.text = "Hãy viết vài chữ mô tả chỗ sai."; return; }
            var sb = new StringBuilder("{\"nguon\":").Append(MayChuCoChe.ChuoiJson(nguon)).Append(",\"ma_bai\":").Append(MayChuCoChe.ChuoiJson(maBai ?? ""))
                .Append(",\"ten\":").Append(MayChuCoChe.ChuoiJson(ten ?? "")).Append(",\"mo_ta\":").Append(MayChuCoChe.ChuoiJson(moTa))
                .Append(",\"che_do\":").Append(MayChuCoChe.ChuoiJson(cheDo ?? ""));
            if (buoc >= 0) sb.Append(",\"buoc\":").Append(buoc + 1);
            sb.Append('}');
            nutGui.interactable = false;
            trangThai.color = InkMute;
            trangThai.text = "Đang gửi…";
            StartCoroutine(MayChuCoChe.Goi("POST", "/co-che/bao-sai", sb.ToString(), 15f, (ok, kq) =>
            {
                nutGui.interactable = true;
                if (!ok) { trangThai.color = Error; trangThai.text = "Chưa gửi được: " + kq; return; }
                KetQuaCuoi = kq;
                trangThai.color = Bio;
                trangThai.text = "Đã gửi. Cảm ơn bạn – giáo viên sẽ xem lại bài này.";
                StartCoroutine(DongSau(1.2f));
            }));
        }

        IEnumerator DongSau(float s) { yield return new WaitForSecondsRealtime(s); Dong(); }
    }
}
