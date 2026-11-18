using System;
using System.Collections;
using Khkt.Demo;
using Khkt.Menu.Dashboard;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.Menu.Dashboard.MenuKit;

namespace Khkt.Menu
{
    /// <summary>
    /// Màn đầu tiên khi mở app: nền tối giản, logo góc trái, hai nút lớn ở giữa
    ///   • "Giải bài tập" → màn Giải bài tập (Chuỗi, 3D, Cơ chế: phần app CHEMISTRY đưa vào VERA)
    ///   • "Phòng Lab"    → luồng cũ: đăng nhập → bảng điều khiển → vào phòng thí nghiệm.
    /// Dựng lúc chạy bằng MenuKit như các màn khác, theo giao diện sáng/tối đang chọn.
    /// </summary>
    public class ManChonLuong : MonoBehaviour
    {
        public Action ChonPhongLab;
        public Action ChonGiaiBaiTap;
        public Action Thoat;

        RectTransform goc;

        public static ManChonLuong Tao(RectTransform cha, Texture2D logo)
        {
            var rt = Full(cha, "Màn chọn luồng");
            var m = rt.gameObject.AddComponent<ManChonLuong>();
            m.goc = rt;
            m.Dung(logo);
            return m;
        }

        public bool DangHien => goc != null && goc.gameObject.activeSelf;

        public void Hien()
        {
            goc.gameObject.SetActive(true);
            goc.SetAsLastSibling();
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, 0f, 1f, 0.3f);
        }

        public void An()
        {
            if (!goc.gameObject.activeSelf) return;
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = false;
            MenuAnim.Fade(g, g.alpha, 0f, 0.22f, 0f, () => goc.gameObject.SetActive(false));
        }

        void Dung(Texture2D logo)
        {
            var nen = goc.gameObject.AddComponent<Image>();
            nen.color = PaperAlt;
            nen.raycastTarget = true;

            KhoiDau.Logo(goc, logo);
            var thoat = MakeButton(goc, "Nút Thoát ứng dụng", "Thoát", new Vector2(-36, -28), new Vector2(96, 44), () => Thoat?.Invoke(), 2, RadiusButton, new Vector2(1, 1));
            ((RectTransform)thoat.transform).pivot = new Vector2(1, 1);

            var chao = GlassKit.Label(goc, "Lời chào", "Hôm nay bạn muốn làm gì?", 52, Ink, true, TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(1400, 72));
            chao.rectTransform.SetAnchors(0.5f, 0.5f);
            DoBong(chao, 0.10f, 2f);
            GlassKit.Label(goc, "Lời chào phụ", "Chọn một lối vào. Bạn có thể quay lại màn này bất cứ lúc nào.", 20, InkDim, false, TextAnchor.MiddleCenter, new Vector2(0, 168), new Vector2(1400, 32)).rectTransform.SetAnchors(0.5f, 0.5f);

            var hang = GlassKit.Rect(goc, "Hai lối vào", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1120, 300));
            NutLon(hang, 0, "Giải bài tập", "Chuỗi phản ứng · Mô hình 3D · Cơ chế",
                   "Dán đề hoặc chọn bài mẫu, AI giải từng bước và vẽ như bài thi.", Accent, () => ChonGiaiBaiTap?.Invoke());
            NutLon(hang, 1, "Phòng Lab", "Hóa học tầng 1 · Sinh học tầng 2",
                   "Đăng nhập rồi tự tay làm thí nghiệm trong phòng lab ảo.", Bio, () => ChonPhongLab?.Invoke());
        }

        /// <summary>Đổ bóng mềm cho chữ (lệch xuống dưới, mờ nhẹ), đọc rõ trên nền sáng lẫn tối.</summary>
        static void DoBong(Text t, float a, float lech)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0.05f, 0.15f, UiTheme.IsLight ? a : a * 3f);
            sh.effectDistance = new Vector2(0, -lech);
        }

        /// <summary>
        /// Thẻ lớn bấm được, không ảnh (18/11): vạch màu, tên to đậm, dòng phụ màu nhấn, mô tả, lời mời "Vào ngay →".
        /// Bóng đổ đậm hơn thẻ thường và lệch xuống để thẻ nổi khỏi nền; rê chuột thì nổi cao hơn.
        /// </summary>
        static void NutLon(RectTransform hang, int i, string ten, string phu, string moTa, Color mau, Action bam)
        {
            var the = Card(hang, "Nút lớn " + ten, i * 0.5f, (i + 1) * 0.5f, 0, 300, 48);
            var than = Body(the);
            var nut = than.gameObject.AddComponent<Button>();
            nut.navigation = new Navigation { mode = Navigation.Mode.None };
            nut.targetGraphic = than.GetComponent<Image>();
            nut.onClick.AddListener(() => bam());
            var hv = the.GetComponent<MenuHover>();
            var bong = the.Find("Bóng") as RectTransform;
            if (bong != null)
            {
                bong.offsetMin = new Vector2(-34, -52);              // bóng rộng, lệch xuống dưới
                bong.offsetMax = new Vector2(34, 14);
                var bi = bong.GetComponent<Image>();
                float nghi = UiTheme.IsLight ? 0.16f : 0.45f, noi = UiTheme.IsLight ? 0.28f : 0.65f;
                bi.color = new Color(bi.color.r, bi.color.g, bi.color.b, nghi);
                if (hv != null) { hv.shadowRest = nghi; hv.shadowHover = noi; }
            }
            if (hv != null) { hv.hoverScale = 1.025f; hv.pressScale = 0.98f; }

            const float Pad = 40f;
            var vach = Fill(than, "Vạch màu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -40), new Vector2(56, 6), mau, 1f);
            vach.raycastTarget = false;
            var tieuDe = GlassKit.Label(than, "Tên", ten, 46, Ink, true, TextAnchor.UpperLeft, new Vector2(Pad, -64), new Vector2(460, 62));
            DoBong(tieuDe, 0.08f, 1.5f);
            GlassKit.Label(than, "Dòng phụ", phu, 18, mau, true, TextAnchor.UpperLeft, new Vector2(Pad, -134), new Vector2(460, 28));
            var mt = GlassKit.Label(than, "Mô tả", moTa, 17, InkDim, false, TextAnchor.UpperLeft, new Vector2(Pad, -172), new Vector2(460, 52));
            mt.horizontalOverflow = HorizontalWrapMode.Wrap;
            mt.lineSpacing = 1.15f;
            GlassKit.Label(than, "Lời mời", "Vào ngay  →", 18, mau, true, TextAnchor.LowerLeft, new Vector2(Pad, 32), new Vector2(300, 28), new Vector2(0, 0)).rectTransform.pivot = new Vector2(0, 0);
        }
    }

    /// <summary>
    /// Màn "Giải bài tập": phần app CHEMISTRY đưa vào VERA, giao diện theo bảng điều khiển VERA.
    /// Ba công cụ lớn: Chuỗi phản ứng, Mô hình 3D, Cơ chế (3D ⇄ giấy, tách từ bảng điều khiển sang đây).
    /// </summary>
    public class ManGiaiBaiTap : MonoBehaviour
    {
        public Action QuayLai;
        public Action MoChuoi, Mo3D, MoCoChe;
        public Action<string> MoHoaLy;
        public Action MoCaiDat;

        RectTransform goc;
        Text trangThai;

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
            goc.SetAsLastSibling();
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, 0f, 1f, 0.3f);
            trangThai.text = "";
        }

        public void An()
        {
            if (!goc.gameObject.activeSelf) return;
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = false;
            MenuAnim.Fade(g, g.alpha, 0f, 0.22f, 0f, () => goc.gameObject.SetActive(false));
        }

        public void Bao(string chu, bool loi = true)
        {
            trangThai.color = loi ? Error : Bio;
            trangThai.text = chu;
        }

        void Dung(Texture2D logo)
        {
            var nen = goc.gameObject.AddComponent<Image>();
            nen.color = PaperAlt;
            nen.raycastTarget = true;

            KhoiDau.Logo(goc, logo);
            var ve = MakeButton(goc, "Nút Về màn chính", "← Về màn chính", new Vector2(-36, -28), new Vector2(190, 46), () => QuayLai?.Invoke(), 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)ve.transform).pivot = new Vector2(1, 1);

            const float Pad = 96f;
            GlassKit.Label(goc, "Tiêu đề", "Giải bài tập", 40, Ink, true, TextAnchor.UpperLeft, new Vector2(Pad, -150), new Vector2(1200, 54));
            GlassKit.Label(goc, "Mô tả", "Hóa hữu cơ theo chương trình THPT và HSG: giải chuỗi, xem chất trong không gian, hiểu cơ chế như bài thi trên giấy.",
                           19, InkDim, false, TextAnchor.UpperLeft, new Vector2(Pad, -210), new Vector2(1500, 30));

            var hang = Stretch(goc, "Ba công cụ", new Vector2(0, 1), new Vector2(1, 1), new Vector2(Pad - 14, -720), new Vector2(-Pad + 14, -270));
            CongCu(hang, 0, "Chuỗi phản ứng", "Giải chuỗi", "Nhập chuỗi A → B → C hoặc chọn chuỗi mẫu. Máy tìm từng chất, nêu điều kiện và loại phản ứng.",
                   "GiaoDien/AnhThu/chuoi", Accent, "Giải chuỗi", () => MoChuoi?.Invoke());
            CongCu(hang, 1, "Mô hình 3D", "Tìm chất hữu cơ", "Gõ tên hoặc công thức, xem phân tử 3D xoay 360°, bật tắt H, đo cấu tạo.",
                   "GiaoDien/AnhThu/mo_hinh_3d", Bio, "Xem 3D", () => Mo3D?.Invoke());
            CongCu(hang, 2, "Cơ chế", "3D ⇄ giấy", "Electron chạy trong không gian 3D, chuyển sang tờ giấy trình bày như đáp án. 8 bài mẫu.",
                   "GiaoDien/AnhThu/co_che", Warn, "Mở cơ chế", () => MoCoChe?.Invoke());

            // Hóa lý: mục nhỏ ở dưới (4 mô phỏng của CHEMISTRY)
            GlassKit.Label(goc, "Tiêu đề Hóa lý", "Hóa lý", 22, Ink, true, TextAnchor.UpperLeft, new Vector2(Pad, -752), new Vector2(400, 32));
            var hl = Stretch(goc, "Hàng Hóa lý", new Vector2(0, 1), new Vector2(1, 1), new Vector2(Pad - 14, -900), new Vector2(-Pad + 14, -792));
            string[,] dsHl = { { "khi_ly_tuong", "Khí lý tưởng", "pV = nRT, hạt chuyển động", "pV" }, { "toc_do", "Tốc độ phản ứng", "Nồng độ, nhiệt độ, xúc tác", "v" },
                               { "can_bang", "Cân bằng Le Chatelier", "Dịch chuyển cân bằng", "⇌" }, { "dong_co_nhiet", "Động cơ nhiệt", "Chu trình, hiệu suất", "η" } };
            for (int i = 0; i < 4; i++)
            {
                string ma = dsHl[i, 0];
                var the = Card(hl, "Hóa lý " + dsHl[i, 1], i / 4f, (i + 1) / 4f, 0, 96, 28);
                var than = Body(the);
                var nut = than.gameObject.AddComponent<Button>();
                nut.navigation = new Navigation { mode = Navigation.Mode.None };
                nut.targetGraphic = than.GetComponent<Image>();
                nut.onClick.AddListener(() => MoHoaLy?.Invoke(ma));
                var oKyHieu = Fill(than, "Biểu tượng", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -24), new Vector2(48, 48), AccentSoft, 1f);
                oKyHieu.sprite = GlassKit.SolidCircle; oKyHieu.type = Image.Type.Simple;
                GlassKit.Label(oKyHieu.transform, "Ký hiệu", dsHl[i, 3], 17, Accent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(48, 48));
                GlassKit.Label(than, "Tên", dsHl[i, 1], 18, Ink, true, TextAnchor.UpperLeft, new Vector2(80, -20), new Vector2(300, 26));
                GlassKit.Label(than, "Mô tả", dsHl[i, 2], 14, InkDim, false, TextAnchor.UpperLeft, new Vector2(80, -50), new Vector2(300, 22));
            }

            var caiDat = MakeButton(goc, "Nút Cài đặt Giải bài tập", "Cài đặt", new Vector2(-244, -28), new Vector2(130, 46), () => MoCaiDat?.Invoke(), 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)caiDat.transform).pivot = new Vector2(1, 1);
            trangThai = GlassKit.Label(goc, "Thông báo", "", 17, Error, false, TextAnchor.UpperLeft, new Vector2(Pad, -920), new Vector2(1500, 28));
        }

        static void CongCu(RectTransform hang, int i, string ten, string phu, string moTa, string anh, Color mau, string cta, Action bam)
        {
            var the = Card(hang, "Công cụ " + ten, i / 3f, (i + 1) / 3f, 0, 450, 28);
            var than = Body(the);
            var nutThe = than.gameObject.AddComponent<Button>();
            nutThe.navigation = new Navigation { mode = Navigation.Mode.None };
            nutThe.targetGraphic = than.GetComponent<Image>();
            nutThe.onClick.AddListener(() => bam());

            var khung = Fill(than, "Khung ảnh", new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(0, 190), Color.white, RadiusCard);
            var kr = khung.rectTransform;
            kr.anchorMin = new Vector2(0, 1); kr.anchorMax = new Vector2(1, 1);
            kr.offsetMin = new Vector2(16, -206); kr.offsetMax = new Vector2(-16, -16);
            khung.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var img = Full(khung.transform, "Ảnh").gameObject.AddComponent<RawImage>();
            img.texture = Resources.Load<Texture2D>(anh);
            img.color = img.texture != null ? Color.white : WithAlpha(mau, 0.25f);
            img.raycastTarget = false;
            if (img.texture != null)
            {
                float want = 2.3f, have = (float)img.texture.width / img.texture.height;
                img.uvRect = want < have ? new Rect((1f - want / have) * 0.5f, 0, want / have, 1) : new Rect(0, (1f - have / want) * 0.5f, 1, have / want);
            }

            GlassKit.Label(than, "Dòng phụ", phu.ToUpperInvariant(), 14, mau, true, TextAnchor.UpperLeft, new Vector2(20, -224), new Vector2(460, 22));
            GlassKit.Label(than, "Tên", ten, 30, Ink, true, TextAnchor.UpperLeft, new Vector2(20, -248), new Vector2(460, 42));
            var mt = GlassKit.Label(than, "Mô tả", moTa, 16, InkDim, false, TextAnchor.UpperLeft, new Vector2(20, -296), new Vector2(0, 70));
            mt.rectTransform.anchorMax = new Vector2(1, 1);
            mt.rectTransform.offsetMax = new Vector2(-20, mt.rectTransform.offsetMax.y);
            mt.horizontalOverflow = HorizontalWrapMode.Wrap;
            MakeButton(than, "Nút " + ten, cta, new Vector2(20, 22), new Vector2(170, 48), bam, 0, RadiusButton, new Vector2(0, 0));
        }
    }

    /// <summary>
    /// Cài đặt của màn Giải bài tập: chỉ còn 2 mục "Thông tin" và "API" (như yêu cầu 18/11).
    /// Quản lý (lịch sử bài giải, mã Claude, hoạt động người dùng) chuyển sang trang web quản lý chung của VERA.
    /// </summary>
    public class CaiDatGiaiBaiTap : MonoBehaviour
    {
        RectTransform goc, mucThongTin, mucApi;
        Button tabThongTin, tabApi;
        Text chuThongTin, chuApi;
        InputField oMayChu;

        public static CaiDatGiaiBaiTap Tao(RectTransform cha)
        {
            var phu = Full(cha, "Cài đặt Giải bài tập");
            var m = phu.gameObject.AddComponent<CaiDatGiaiBaiTap>();
            m.goc = phu;
            m.Dung();
            phu.gameObject.SetActive(false);
            return m;
        }

        public void Hien()
        {
            goc.gameObject.SetActive(true);
            goc.SetAsLastSibling();
            ChonMuc(0);
            MenuAnim.Fade(MenuAnim.Group(goc), 0f, 1f, 0.2f);
        }

        void Dung()
        {
            var mo = goc.gameObject.AddComponent<Image>();
            mo.color = new Color(0f, 0f, 0f, UiTheme.IsLight ? 0.28f : 0.5f);
            mo.raycastTarget = true;
            var khung = GlassKit.Rect(goc, "Khung", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 560));
            var the = Card(khung, "Thẻ cài đặt", 0f, 1f, 0, 560, 0, false);
            var than = Body(the);
            than.GetComponent<Image>().raycastTarget = true;
            GlassKit.Label(than, "Tiêu đề", "Cài đặt", 30, Ink, true, TextAnchor.UpperLeft, new Vector2(32, -26), new Vector2(500, 40));
            var dong = MakeButton(than, "Nút Đóng cài đặt", "Đóng", new Vector2(-24, -24), new Vector2(100, 44), () => goc.gameObject.SetActive(false), 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)dong.transform).pivot = new Vector2(1, 1);
            tabThongTin = GlassKit.MakeChip(than, "Mục Thông tin", "Thông tin", new Vector2(32, -88), new Vector2(140, 44), () => ChonMuc(0), true, 16);
            tabApi = GlassKit.MakeChip(than, "Mục API", "API", new Vector2(182, -88), new Vector2(110, 44), () => ChonMuc(1), false, 16);

            mucThongTin = GlassKit.Rect(than, "Nội dung Thông tin", new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -150), new Vector2(796, 380));
            chuThongTin = GlassKit.Label(mucThongTin, "Chữ", "", 17, Ink, false, TextAnchor.UpperLeft, Vector2.zero, new Vector2(796, 300));
            chuThongTin.horizontalOverflow = HorizontalWrapMode.Wrap;

            mucApi = GlassKit.Rect(than, "Nội dung API", new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -150), new Vector2(796, 380));
            GlassKit.Label(mucApi, "Nhãn máy chủ", "Máy chủ giải bài (giải chuỗi, mô hình 3D, cơ chế, hỏi AI)", 16, InkDim, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(796, 24));
            oMayChu = GlassKit.MakeInput(mucApi, "Ô máy chủ", Khkt.CoChe.MayChuCoChe.UrlMacDinh, new Vector2(0, -32), new Vector2(560, 48), 16);
            GlassKit.MakeButton(mucApi, "Nút Lưu và kiểm tra", "Lưu và kiểm tra", new Vector2(572, -32), new Vector2(224, 48), () =>
            {
                Khkt.CoChe.MayChuCoChe.Url = oMayChu.text;
                StartCoroutine(KiemMayChu());
            }, true, 16);
            chuApi = GlassKit.Label(mucApi, "Trạng thái", "", 16, Ink, false, TextAnchor.UpperLeft, new Vector2(0, -100), new Vector2(796, 270));
            chuApi.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void ChonMuc(int i)
        {
            GlassKit.SetChip(tabThongTin, i == 0);
            GlassKit.SetChip(tabApi, i == 1);
            mucThongTin.gameObject.SetActive(i == 0);
            mucApi.gameObject.SetActive(i == 1);
            if (i == 0) CapNhatThongTin();
            else { oMayChu.text = Khkt.CoChe.MayChuCoChe.Url; StartCoroutine(KiemMayChu()); }
        }

        void CapNhatThongTin()
        {
            var u = GameSession.User;
            string tk = u != null ? $"<b>{u.ho_ten}</b> ({u.ten_dang_nhap})\n{ClassLine(u)}"
                      : GameSession.GuestActive ? "<b>Khách</b> · dùng thử, bài giải không lưu vào tài khoản"
                      : "Chưa đăng nhập. Giải bài tập dùng được không cần tài khoản.";
            chuThongTin.text =
                $"<b>Tài khoản</b>\n{tk}\n\n" +
                $"<b>Ứng dụng</b>\nVERA Lab {Application.version} · {SystemInfo.operatingSystem}\n" +
                $"Máy chủ tài khoản: {GameSession.ServerUrl}\n\n" +
                "<b>Quản lý</b>\nLịch sử bài giải, mã Claude và hoạt động người dùng nay nằm ở trang web quản lý chung của VERA.";
        }

        IEnumerator KiemMayChu()
        {
            chuApi.text = "Đang kiểm tra " + Khkt.CoChe.MayChuCoChe.Url + "…";
            string ketQua = "";
            bool ok = false;
            yield return Khkt.CoChe.MayChuCoChe.Goi("GET", "/co-che/suc-khoe", null, 4f, (dat, kq) => { ok = dat; ketQua = kq; });
            if (!ok)
            {
                chuApi.text = "<color=#d64545>Chưa kết nối được máy chủ.</color> " + ketQua + "\nBật trên máy: python -m server.ai.co_che.may_chu (cổng 8010).";
                yield break;
            }
            try
            {
                var d = Khkt.CoChe.JsonNho.D(Khkt.CoChe.JsonNho.Doc(ketQua));
                var rag = Khkt.CoChe.JsonNho.D(Khkt.CoChe.JsonNho.Lay(d, "rag"));
                chuApi.text = "<color=#2e9d5b>Đã kết nối.</color>\n" +
                              $"AI tại máy: {Khkt.CoChe.JsonNho.S(d, "mo_hinh")} (mức {Khkt.CoChe.JsonNho.S(d, "muc")}, RAM {Khkt.CoChe.JsonNho.S(d, "ram_gb")} GB)\n" +
                              $"Tài liệu tra cứu: {Khkt.CoChe.JsonNho.S(rag, "so_doan")} đoạn · Bài cơ chế: {Khkt.CoChe.JsonNho.S(d, "so_bai")}\n\n" +
                              "Khóa API trực tuyến (Claude, Gemini) do máy chủ đọc từ biến môi trường ANTHROPIC_API_KEY / LLM_API_KEY; " +
                              "AI tại máy không cần khóa.";
            }
            catch (Exception e) { chuApi.text = "Đã kết nối nhưng đọc trạng thái lỗi: " + e.Message; }
        }
    }

    static class KhoiDau
    {
        public static void Logo(RectTransform goc, Texture2D logo)
        {
            if (logo != null)
            {
                var l = GlassKit.Rect(goc, "Logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -28), new Vector2(48, 48));
                var img = l.gameObject.AddComponent<RawImage>();
                img.texture = logo;
                img.raycastTarget = false;
            }
            GlassKit.Label(goc, "Tên app", "VERA Lab", 26, Ink, true, TextAnchor.MiddleLeft, new Vector2(100, -28), new Vector2(300, 48));
        }

        public static void SetAnchors(this RectTransform rt, float x, float y)
        {
            var p = rt.anchoredPosition;
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = p;
        }
    }
}
