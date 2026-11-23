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
    /// Màn đầu tiên khi mở app: nền có chiều sâu, logo góc trái, lời chào lớn, hai thẻ lớn ở giữa
    ///   • "Giải bài tập" → màn Giải bài tập (Chuỗi, 3D, Cơ chế: phần app CHEMISTRY đưa vào VERA)
    ///   • "Phòng Lab"    → luồng cũ: đăng nhập → bảng điều khiển → vào phòng thí nghiệm.
    /// Giao diện 18/11: thẻ lớn dải màu chéo (navy / ngọc lục bảo) như thẻ lớn CHEMISTRY, không ảnh, chữ to có bóng,
    /// rê chuột thì thẻ nhấc lên và mũi tên nhích; vào màn thì các phần hiện lần lượt.
    /// </summary>
    public class ManChonLuong : MonoBehaviour
    {
        public Action ChonPhongLab;
        public Action ChonGiaiBaiTap;
        public Action Thoat;

        RectTransform goc;
        readonly List<RectTransform> lanLuot = new List<RectTransform>();

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
            KhoiDau.HienMan(goc, lanLuot);
        }

        public void An()
        {
            KhoiDau.AnMan(goc);
        }

        /// <summary>Phím tắt màn đầu: G = Giải bài tập, L = Phòng Lab.</summary>
        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !DangHien) return;
            if (kb.gKey.wasPressedThisFrame) ChonGiaiBaiTap?.Invoke();
            else if (kb.lKey.wasPressedThisFrame) ChonPhongLab?.Invoke();
        }

        void Dung(Texture2D logo)
        {
            Backdrop(goc);
            KhoiDau.ThanhTren(goc, logo);
            var thoat = MakeButton(goc, "Nút Thoát ứng dụng", "Thoát", new Vector2(-40, -30), new Vector2(96, 44), () => Thoat?.Invoke(), 2, RadiusButton, new Vector2(1, 1));
            ((RectTransform)thoat.transform).pivot = new Vector2(1, 1);

            var giua = new Vector2(0.5f, 0.5f);
            var tag = Tag(goc, "Nhãn chào", "VERA Lab · Hóa học & Sinh học THPT", new Vector2(0, 300), AccentSoft, WithAlpha(Accent, 0.28f), Accent, giua);
            var chao = GlassKit.Label(goc, "Lời chào", "Hôm nay bạn muốn làm gì?", 60, Ink, true, TextAnchor.MiddleCenter, new Vector2(0, 232), new Vector2(1400, 84));
            chao.rectTransform.SetAnchors(0.5f, 0.5f);
            TextShadow(chao, UiTheme.IsLight ? 0.10f : 0.55f, 3f);
            var phu = GlassKit.Label(goc, "Lời chào phụ", "Chọn một lối vào. Bạn có thể quay lại màn này bất cứ lúc nào.", 20, InkDim, false, TextAnchor.MiddleCenter, new Vector2(0, 168), new Vector2(1400, 32));
            phu.rectTransform.SetAnchors(0.5f, 0.5f);

            var hang = GlassKit.Rect(goc, "Hai lối vào", giua, giua, new Vector2(0, -78), new Vector2(1200, 340));
            var a = TheLon(hang, 0, "Giải bài tập", "01 · Hóa hữu cơ", "Chuỗi phản ứng · Mô hình 3D · Cơ chế",
                           "Dán đề hoặc chọn bài mẫu, AI giải từng bước và vẽ như bài thi.",
                           Hex(0x18497a), Hex(0x0a1d35), Hex(0xbae6fd), () => ChonGiaiBaiTap?.Invoke());
            var b = TheLon(hang, 1, "Phòng Lab", "02 · Thí nghiệm ảo", "Hóa học tầng 1 · Sinh học tầng 2",
                           "Đăng nhập rồi tự tay làm thí nghiệm trong phòng lab ảo.",
                           Hex(0x0a6e51), Hex(0x032b20), Hex(0xa7f3d0), () => ChonPhongLab?.Invoke());
            lanLuot.Add(tag);
            lanLuot.Add(chao.rectTransform);
            lanLuot.Add(phu.rectTransform);
            lanLuot.Add(a);
            lanLuot.Add(b);
        }

        /// <summary>
        /// Thẻ lớn bấm được: dải màu chéo 145°, viền trắng mờ, ánh mép trên, tên 48 đậm có bóng,
        /// nút "Vào ngay →" màu trắng. Bóng đổ nhuốm màu thẻ, rê chuột thì thẻ nhấc 6 điểm và bóng rơi xa hơn.
        /// </summary>
        static RectTransform TheLon(RectTransform hang, int i, string ten, string nhan, string phu, string moTa, Color dau, Color cuoi, Color nhat, Action bam)
        {
            var the = Card(hang, "Nút lớn " + ten, i * 0.5f, (i + 1) * 0.5f, 0, 340, 40, true, RadiusLarge);
            var than = Body(the);
            var nen = than.GetComponent<Image>();
            nen.color = Color.white;
            Gradient(nen, dau, cuoi, 145f);
            if (UiTheme.IsLight) Sheen(the, RadiusLarge, 0.22f);
            var hv = the.GetComponent<MenuHover>();
            var bong = the.Find("Bóng").GetComponent<Image>();
            Color mauBong = UiTheme.IsLight ? cuoi : new Color(0, 0, 0, 1);
            float nghi = UiTheme.IsLight ? 0.30f : 0.55f, noi = UiTheme.IsLight ? 0.48f : 0.75f;
            bong.color = WithAlpha(mauBong, nghi);
            var vien = CardRing(the);
            vien.color = new Color(1, 1, 1, 0.12f);
            MakeCardClickable(the, bam, 6f, new Color(1, 1, 1, 0.40f));
            hv.shadowRest = nghi;
            hv.shadowHover = noi;
            hv.ringRest = vien.color;
            hv.pressScale = 0.985f;
            var ds = new List<RectTransform>(hv.liftTargets);
            var sheen = the.Find("Ánh mép") as RectTransform;
            if (sheen != null && !ds.Contains(sheen)) ds.Add(sheen);
            hv.liftTargets = ds.ToArray();

            const float Pad = 40f;
            Tag(than, "Nhãn", nhan, new Vector2(Pad, -40), new Color(1, 1, 1, 0.12f), new Color(1, 1, 1, 0.20f), nhat);
            var tieuDe = GlassKit.Label(than, "Tên", ten, 48, Color.white, true, TextAnchor.UpperLeft, new Vector2(Pad, -86), new Vector2(480, 64));
            TextShadow(tieuDe, 0.35f, 2f);
            GlassKit.Label(than, "Dòng phụ", phu, 18, nhat, true, TextAnchor.UpperLeft, new Vector2(Pad, -156), new Vector2(480, 28));
            var mt = GlassKit.Label(than, "Mô tả", moTa, 17, new Color(1, 1, 1, 0.74f), false, TextAnchor.UpperLeft, new Vector2(Pad, -192), new Vector2(440, 52));
            mt.horizontalOverflow = HorizontalWrapMode.Wrap;

            // nút giả "Vào ngay →": khối chứa (bóng trước, mặt trắng sau) để bóng nằm dưới mặt
            var moi = GlassKit.Rect(than, "Lời mời", new Vector2(0, 0), new Vector2(0, 0), new Vector2(Pad, 34), new Vector2(160, 48));
            Shadow(moi, "Bóng", 8f, RadiusPill, -3f, 0.28f);
            Paint(Full(moi, "Mặt"), Color.white, RadiusPill);
            var chu = GlassKit.Label(moi.transform, "Chữ", "Vào ngay", 17, cuoi, true, TextAnchor.MiddleLeft, new Vector2(24, 0), new Vector2(100, 48));
            chu.horizontalOverflow = HorizontalWrapMode.Overflow;
            var muiTen = GlassKit.Label(moi.transform, "Mũi tên", "→", 18, cuoi, true, TextAnchor.MiddleLeft, new Vector2(112, 0), new Vector2(30, 48));
            hv.nudge = muiTen.rectTransform;
            hv.nudgeX = 6f;
            return the;
        }
    }

    /// <summary>
    /// Cài đặt của màn Giải bài tập: chỉ còn 2 mục "Thông tin" và "API" (như yêu cầu 18/11).
    /// Quản lý (lịch sử bài giải, mã Claude, hoạt động người dùng) chuyển sang trang web quản lý chung của VERA.
    /// </summary>
    public class CaiDatGiaiBaiTap : MonoBehaviour
    {
        RectTransform goc, khung, mucThongTin, mucApi;
        MenuTabs tabs;
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
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, 0f, 1f, 0.2f);
            MenuAnim.Pop(khung);
        }

        void Dong()
        {
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = false;
            MenuAnim.Fade(g, g.alpha, 0f, 0.18f, 0f, () => goc.gameObject.SetActive(false));
        }

        void Dung()
        {
            var mo = goc.gameObject.AddComponent<Image>();
            mo.color = GlassKit.Scrim(UiTheme.IsLight ? 0.32f : 0.55f);
            mo.raycastTarget = true;
            khung = GlassKit.Rect(goc, "Khung", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 560));
            var the = Card(khung, "Thẻ cài đặt", 0f, 1f, 0, 560, 0, false, RadiusLarge);
            var than = Body(the);
            than.GetComponent<Image>().raycastTarget = true;
            Eyebrow(than, "Nhãn", "Giải bài tập", new Vector2(36, -30), 300, Accent);
            GlassKit.Label(than, "Tiêu đề", "Cài đặt", 30, Ink, true, TextAnchor.UpperLeft, new Vector2(36, -50), new Vector2(500, 40));
            var dong = MakeButton(than, "Nút Đóng cài đặt", "Đóng", new Vector2(-28, -28), new Vector2(100, 44), Dong, 1, RadiusButton, new Vector2(1, 1));
            ((RectTransform)dong.transform).pivot = new Vector2(1, 1);
            tabs = Tabs(than, "Nhóm mục", new[] { "Mục Thông tin", "Mục API" }, new[] { "Thông tin", "API" }, new Vector2(40, -112), new Vector2(140, 44), false, 4f, 0, ChonMuc);

            mucThongTin = GlassKit.Rect(than, "Nội dung Thông tin", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -184), new Vector2(788, 340));
            chuThongTin = GlassKit.Label(mucThongTin, "Chữ", "", 17, Ink, false, TextAnchor.UpperLeft, Vector2.zero, new Vector2(788, 330));
            chuThongTin.horizontalOverflow = HorizontalWrapMode.Wrap;

            mucApi = GlassKit.Rect(than, "Nội dung API", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -184), new Vector2(788, 340));
            GlassKit.Label(mucApi, "Nhãn máy chủ", "Máy chủ giải bài (giải chuỗi, mô hình 3D, cơ chế, hỏi AI)", 15, InkDim, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(788, 24));
            oMayChu = TextField(mucApi, "Ô máy chủ", Khkt.CoChe.MayChuCoChe.UrlMacDinh, new Vector2(0, -32), new Vector2(552, 48), 16);
            MakeButton(mucApi, "Nút Lưu và kiểm tra", "Lưu và kiểm tra", new Vector2(564, -32), new Vector2(224, 48), () =>
            {
                Khkt.CoChe.MayChuCoChe.Url = oMayChu.text;
                StartCoroutine(KiemMayChu());
            }, 0);
            chuApi = GlassKit.Label(mucApi, "Trạng thái", "", 16, Ink, false, TextAnchor.UpperLeft, new Vector2(0, -100), new Vector2(788, 230));
            chuApi.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void ChonMuc(int i)
        {
            if (tabs.current != i) tabs.Select(i);
            mucThongTin.gameObject.SetActive(i == 0);
            mucApi.gameObject.SetActive(i == 1);
            var dang = i == 0 ? mucThongTin : mucApi;
            MenuAnim.Enter(dang, 0f, 8f, 0.3f);
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
                chuApi.text = $"<color={GlassKit.HexBad}>Chưa kết nối được máy chủ.</color> " + ketQua + "\nBật trên máy: python -m server.ai.co_che.may_chu (cổng 8010).";
                yield break;
            }
            try
            {
                var d = Khkt.CoChe.JsonNho.D(Khkt.CoChe.JsonNho.Doc(ketQua));
                var rag = Khkt.CoChe.JsonNho.D(Khkt.CoChe.JsonNho.Lay(d, "rag"));
                chuApi.text = $"<color={GlassKit.HexGood}>Đã kết nối.</color>\n" +
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
        /// <summary>Thanh trên cùng: logo, tên app, vạch ngăn, nhãn phụ nhỏ (kiểu logo-chu + logo-phu của CHEMISTRY).</summary>
        public static void ThanhTren(RectTransform goc, Texture2D logo)
        {
            if (logo != null)
            {
                var l = GlassKit.Rect(goc, "Logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -30), new Vector2(44, 44));
                var img = l.gameObject.AddComponent<RawImage>();
                img.texture = logo;
                img.raycastTarget = false;
            }
            var ten = GlassKit.Label(goc, "Tên app", "VERA Lab", 24, Ink, true, TextAnchor.MiddleLeft, new Vector2(98, -30), new Vector2(300, 44));
            ten.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Ceil(GlassKit.Width(ten, ten.text));
            var vach = GlassKit.Fill(goc, "Vạch ngăn", new Vector2(98 + w + 14, -41), new Vector2(1, 22), Hair);
            vach.raycastTarget = false;
            var phu = GlassKit.Label(goc, "Nhãn app", "PHÒNG THÍ NGHIỆM ẢO", 12, InkMute, true, TextAnchor.MiddleLeft, new Vector2(98 + w + 28, -30), new Vector2(300, 44));
            phu.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>Vào màn: cả màn mờ dần, các phần trượt lên hiện lần lượt (cách nhau 50 ms) như .hien-dan của CHEMISTRY.</summary>
        public static void HienMan(RectTransform goc, List<RectTransform> lanLuot)
        {
            MenuAnim.Stop(goc, "nở");
            goc.localScale = Vector3.one;
            goc.gameObject.SetActive(true);
            goc.SetAsLastSibling();
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, g.alpha < 0.99f ? g.alpha : 0f, 1f, 0.25f);
            for (int i = 0; i < lanLuot.Count; i++)
            {
                bool lon = lanLuot[i].name.StartsWith("Nút lớn") || lanLuot[i].name.StartsWith("Công cụ");
                MenuAnim.Enter(lanLuot[i], 0.03f + Mathf.Min(i, 10) * 0.045f, lon ? 22f : 12f, lon ? 0.55f : 0.42f);
            }
        }

        /// <summary>Rời màn: mờ đi và chìm nhẹ (~200 ms) rồi tắt hẳn để không tốn công vẽ.</summary>
        public static void AnMan(RectTransform goc)
        {
            if (!goc.gameObject.activeSelf) return;
            var g = MenuAnim.Group(goc);
            g.blocksRaycasts = false;
            MenuAnim.Leave(goc, 0.2f, () => goc.gameObject.SetActive(false));
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
