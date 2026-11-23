using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Khkt.CoChe;
using Khkt.Demo;
using Khkt.Menu.Dashboard;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.CoChe.JsonNho;
using static Khkt.Menu.Dashboard.MenuKit;

namespace Khkt.Menu
{
    /// <summary>
    /// Thẻ kết quả "Mô hình 3D" nội tuyến (2 cột như giao diện web CHEMISTRY):
    ///   • Trái (71,5%): thanh điều khiển (Bóng-que | Que | Đặc | Dây, Bề mặt VDW/SAS/SES, Bảng màu Jmol/Carbon tối/Rasmol,
    ///     công tắc Nhãn & Tự xoay, Đặt lại góc, Toàn màn hình), thanh đo (Đo khoảng cách, Đo góc liên kết, Xoá đo, trạng thái),
    ///     khung 3D tương tác nền sáng studio với tooltip rê chuột, đường/nhãn đo theo 3D và nút "Toàn màn hình" nổi ở góc dưới phải.
    ///   • Phải (28,5%): THÔNG TIN CHẤT (tên, tên tiếng Anh, công thức khung 2D, CTPT, M, IUPAC, SMILES, PubChem, Nguồn 3D, Nhận diện)
    ///     và 4 nút xuất tệp (Tải PNG, Tải PNG 4K, Tải .sdf (3D), Tải .xyz).
    /// Mặc định dựng xong hiển thị nội tuyến ngay trong trang; chỉ khi bấm nút Toàn màn hình mới bật lớp phủ MoHinh3DMoDun.
    /// </summary>
    public class KhungMoHinh3D : MonoBehaviour
    {
        public const float ChieuCaoThe = 780f;

        static readonly string[] TenKieu = { "Bóng-que", "Que", "Đặc", "Dây" };
        static readonly string[] TenBeMat = { "Tắt bề mặt", "Bề mặt VDW", "Dung môi (SAS)", "Phân tử (SES)" };
        static readonly string[] TenMau = { "Màu Jmol", "Carbon tối", "Rasmol" };

        static readonly Dictionary<string, string> TenNguyenTo = new Dictionary<string, string>
        {
            {"H", "Hydro"}, {"C", "Carbon"}, {"N", "Nitơ"}, {"O", "Oxi"}, {"F", "Flo"},
            {"P", "Photpho"}, {"S", "Lưu huỳnh"}, {"Cl", "Clo"}, {"Br", "Brom"}, {"I", "Iot"},
            {"Na", "Natri"}, {"K", "Kali"}, {"Ca", "Canxi"}, {"Mg", "Magie"}, {"Fe", "Sắt"}, {"Cu", "Đồng"},
        };

        struct DiemDo
        {
            public string id, elem;
            public int soHieu;
            public Vector3 pos;
        }

        class PhepDo
        {
            public bool laGoc;
            public DiemDo[] diem;
            public string ketQua;
        }

        public RectTransform Goc { get; private set; }
        public Xem3DNho Xem { get; private set; }
        public Dictionary<string, object> DuLieu { get; private set; }
        public bool DangHien => Goc != null && Goc.gameObject.activeSelf;
        public string TenChatHienTai => tenChat != null ? tenChat.text : "";
        public string CheDoDo => cheDoDo;
        public int SoPhepDo => cacPhepDo.Count;
        public string DuongTepCuoi { get; private set; } = "";
        public Action<string, bool> BaoTrangThai;
        public Action<string> BaoSai;
        public Action<Dictionary<string, object>> CapNhatTuToanMan;

        RectTransform thanThe, khungViewport, anh3DRt, lopDo, badgeHover, menuBeMat, menuMau, lopChanMenu, nutFullDuoiRt;
        Text badgeHoverTen, badgeHoverChiTiet, thongBaoDo;
        Button[] nutKieu = new Button[4];
        Image[] nenNutKieu = new Image[4];
        Image[] vienNutKieu = new Image[4];
        Text[] chuNutKieu = new Text[4];
        Button nutBeMat, nutMau, nutDoKc, nutDoGoc, nutXoaDo, nutPubChem;
        Toggle ctNhan, ctXoay;

        // Cột phải: Thông tin chất
        Text tenChat, tenEn, valCtpt, valM, valIupac, valSmiles, valPubChem, valNguon3D, valNhanDien;
        RawImage anh2D;
        Texture2D tex2D;
        RectTransform khoiAnh2D, khoiMeta;
        string urlPubChem = "";

        // Trạng thái xem & đo
        int kieuVe, bangMau;
        BeMatPhanTu.Loai beMat = BeMatPhanTu.Loai.Tat;
        bool hienNhan, tuXoay;
        string cheDoDo = "";
        readonly List<DiemDo> dangChon = new List<DiemDo>();
        readonly List<PhepDo> cacPhepDo = new List<PhepDo>();

        // Pool đồ hoạ cho đường đo, nhãn kết quả đo, và đánh dấu [1]/[2]
        readonly List<Image> poolDuong = new List<Image>();
        readonly List<RectTransform> poolNhanDo = new List<RectTransform>();
        readonly List<RectTransform> poolChon = new List<RectTransform>();

        static Color MauNen3D => UiTheme.Pick(Hex(0xfbf2f5), Hex(0x121215), Hex(0x0a1526));
        static Color MauVienNhan => UiTheme.IsLight ? Hex(0x0f2b48) : Accent;

        // ------------------------------------------------------------------ tạo thẻ 2 cột
        public static KhungMoHinh3D Tao(RectTransform khoi)
        {
            var card = Card(khoi, "Thẻ kết quả 3D", 0, 1, 0, ChieuCaoThe, 0, false, TrangCuon.BoTheLon);
            var k = khoi.gameObject.AddComponent<KhungMoHinh3D>();
            k.Goc = khoi;
            var than = Body(card);
            than.GetComponent<Image>().raycastTarget = true;
            k.thanThe = than;

            // viền nhấn dọc bên trái (.the-nhan của CHEMISTRY)
            var vienTrai = Stretch(than, "Viền nhấn trái", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 14), new Vector2(4, -14));
            var vti = vienTrai.gameObject.AddComponent<Image>();
            vti.color = MauVienNhan; vti.raycastTarget = false;

            // vạch chia 2 cột (trái 71,5% · phải 28,5%)
            const float Chia = 0.715f;
            var vach = Stretch(than, "Vạch chia cột", new Vector2(Chia, 0), new Vector2(Chia, 1), Vector2.zero, new Vector2(1, 0));
            var vi = vach.gameObject.AddComponent<Image>();
            vi.color = Hair; vi.raycastTarget = false;

            var cotTrai = Stretch(than, "Cột 3D", new Vector2(0, 0), new Vector2(Chia, 1), new Vector2(24, 16), new Vector2(-20, -20));
            var cotPhai = Stretch(than, "Cột thông tin chất", new Vector2(Chia, 0), new Vector2(1, 1), new Vector2(24, 16), new Vector2(-24, -20));

            k.DungCotTrai(cotTrai);
            k.DungCotPhai(cotPhai);
            k.DungMenuTha();

            khoi.gameObject.SetActive(false);
            return k;
        }

        void DungCotTrai(RectTransform cha)
        {
            // ---- Hàng 1: Thanh điều khiển chính (cao 42)
            var thanh1 = Stretch(cha, "Thanh điều khiển 3D", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -42), Vector2.zero);

            // Nhóm phân đoạn 4 kiểu: Bóng-que | Que | Đặc | Dây
            float[] rongK = { 104f, 60f, 60f, 60f };
            float tongK = 8f;
            foreach (var w in rongK) tongK += w + 2f;
            tongK -= 2f;
            var khay = GlassKit.Rect(thanh1, "Khay kiểu 3D", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(tongK, 42));
            Paint(Full(khay, "Nền"), UiTheme.IsLight ? PaperAlt : Well, 12f);
            Ring(khay, "Viền", Hair, 12f);
            float xk = 4f;
            for (int i = 0; i < 4; i++)
            {
                int ii = i;
                var brt = GlassKit.Rect(khay, "Kiểu " + TenKieu[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(xk, -4), new Vector2(rongK[i], 34));
                var nen = Paint(Full(brt, "Mặt"), Color.clear, 9f);
                nen.raycastTarget = true;
                var vien = Ring(brt, "Viền", Color.clear, 9f);
                var b = brt.gameObject.AddComponent<Button>();
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.transition = Selectable.Transition.None;
                b.targetGraphic = nen;
                b.onClick.AddListener(() => DatKieu(ii));
                var t = GlassKit.Label(brt, "Chữ", TenKieu[i], 14, InkDim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(rongK[i], 34));
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                var hv = brt.gameObject.AddComponent<MenuHover>();
                hv.hoverScale = 1.022f;
                hv.pressScale = 0.935f;
                nutKieu[i] = b; nenNutKieu[i] = nen; vienNutKieu[i] = vien; chuNutKieu[i] = t;
                xk += rongK[i] + 2f;
            }

            // Hai nút chọn dạng dropdown: Bề mặt & Bảng màu
            float x = tongK + 12f;
            nutBeMat = NutChon(thanh1, "Nút Bề mặt 3D", TenBeMat[0] + "  ▾", new Vector2(x, 0), new Vector2(166, 42), () => MoMenu(menuBeMat, nutBeMat));
            x += 176f;
            nutMau = NutChon(thanh1, "Nút Màu 3D", TenMau[0] + "  ▾", new Vector2(x, 0), new Vector2(140, 42), () => MoMenu(menuMau, nutMau));

            // Nhóm bên phải: Nhãn · Tự xoay · Đặt lại góc · Toàn màn hình (biểu tượng trên)
            var nutToTren = NutBieuTuong(thanh1, "Nút Toàn màn hình thanh trên", IconFullscreen(), new Vector2(0, -1), 40f, MoToanManHinh, new Vector2(1, 1));
            ((RectTransform)nutToTren.transform).pivot = new Vector2(1, 1);

            var nutGoc = NutBieuTuong(thanh1, "Nút Đặt lại góc 3D", IconReset(), new Vector2(-48, -1), 40f, DatLaiGoc, new Vector2(1, 1));
            ((RectTransform)nutGoc.transform).pivot = new Vector2(1, 1);

            ctXoay = CongTacTrai(thanh1, "Công tắc Tự xoay 3D", "Tự xoay", new Vector2(-102, -3), 116f, false, v => { tuXoay = v; if (Xem != null) Xem.TP.TuXoay = v; });
            ctNhan = CongTacTrai(thanh1, "Công tắc Nhãn 3D", "Nhãn", new Vector2(-226, -3), 94f, false, v => { hienNhan = v; if (Xem != null) Xem.HienNhan = v; });

            // ---- Hàng 2: Thanh công cụ đo khoảng cách / góc liên kết (cao 44)
            var thanhDo = Stretch(cha, "Thanh đo 3D", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(0, -52));
            Paint(Full(thanhDo, "Nền"), UiTheme.IsLight ? Hex(0xf7f7f9) : Well, 12f);
            Ring(thanhDo, "Viền", Hair, 12f);

            nutDoKc = NutCongCuDo(thanhDo, "Nút Đo khoảng cách 3D", IconRuler(), "Đo khoảng cách", new Vector2(8, -5), 162f, () => BatCheDoDo("khoang-cach"));
            nutDoGoc = NutCongCuDo(thanhDo, "Nút Đo góc liên kết 3D", IconAngle(), "Đo góc liên kết", new Vector2(178, -5), 166f, () => BatCheDoDo("goc"));
            nutXoaDo = NutCongCuDo(thanhDo, "Nút Xoá đo 3D", null, "✕  Xoá đo", new Vector2(352, -5), 98f, XoaTatCaDo);
            var chuXoa = nutXoaDo.transform.Find("Chữ")?.GetComponent<Text>();
            if (chuXoa != null) chuXoa.color = Error;
            nutXoaDo.gameObject.SetActive(false);

            thongBaoDo = GlassKit.Label(thanhDo, "Thông báo đo 3D", "Rê chuột xem nguyên tử  ·  Bấm nút để đo liên kết", 14, InkMute, false, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
            var tbrt = thongBaoDo.rectTransform;
            tbrt.anchorMin = new Vector2(0, 0); tbrt.anchorMax = new Vector2(1, 1); tbrt.pivot = new Vector2(0, 0.5f);
            tbrt.offsetMin = new Vector2(356, 0); tbrt.offsetMax = new Vector2(-12, 0);
            thongBaoDo.supportRichText = true;
            thongBaoDo.horizontalOverflow = HorizontalWrapMode.Wrap;

            // ---- Hàng 3: Khung 3D tương tác (chiếm toàn bộ phần giữa)
            khungViewport = Stretch(cha, "Khung 3D tương tác", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 28), new Vector2(0, -108));
            Paint(Full(khungViewport, "Nền khung 3D"), MauNen3D, 14f);

            anh3DRt = Full(khungViewport, "Ảnh 3D");
            anh3DRt.gameObject.AddComponent<RectMask2D>();
            var tuongTac = anh3DRt.gameObject.AddComponent<TuongTacKhung3D>();
            tuongTac.KhiBam = XuLyBam3D;

            // Lớp phủ bo góc khử răng cưa (che 4 góc RawImage bằng màu nền thẻ Paper) + viền tóc
            var cap = Full(khungViewport, "Góc bo 3D").gameObject.AddComponent<Image>();
            cap.sprite = CornerCap(14f); cap.type = Image.Type.Sliced; cap.fillCenter = false;
            cap.pixelsPerUnitMultiplier = 2f; cap.color = Paper; cap.raycastTarget = false;
            Ring(khungViewport, "Viền khung 3D", Hair, 14f);

            // Lớp vẽ đường đo / nhãn đo / đánh dấu nguyên tử đang chọn (cắt theo khung)
            lopDo = Full(khungViewport, "Lớp đo 3D");
            lopDo.gameObject.AddComponent<RectMask2D>();

            // Badge rê chuột xem thông số nguyên tử
            badgeHover = GlassKit.Rect(khungViewport, "Badge hover 3D", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 44));
            Shadow(badgeHover, "Bóng", 8f, 10f, -2f, UiTheme.IsLight ? 0.12f : 0.45f);
            Paint(Full(badgeHover, "Mặt"), WithAlpha(Paper, 0.94f), 10f);
            Ring(badgeHover, "Viền", HairStrong, 10f);
            badgeHoverTen = GlassKit.Label(badgeHover, "Tên", "", 13, Accent, true, TextAnchor.UpperLeft, new Vector2(12, -5), new Vector2(236, 18));
            badgeHoverTen.horizontalOverflow = HorizontalWrapMode.Overflow; badgeHoverTen.raycastTarget = false;
            badgeHoverChiTiet = GlassKit.Label(badgeHover, "Chi tiết", "", 11, InkMute, false, TextAnchor.LowerLeft, new Vector2(12, -23), new Vector2(236, 16));
            badgeHoverChiTiet.horizontalOverflow = HorizontalWrapMode.Overflow; badgeHoverChiTiet.raycastTarget = false;
            badgeHover.gameObject.SetActive(false);

            // Nút TOÀN MÀN HÌNH nổi ở góc dưới phải khung 3D (theo yêu cầu người dùng)
            var nutFull = TaoNutToanManGocDuoi(khungViewport);
            nutFullDuoiRt = (RectTransform)nutFull.transform;

            // ---- Hàng 4: Dòng gợi ý điều khiển ở đáy cột trái
            var goiY = GlassKit.Label(cha, "Gợi ý điều khiển 3D",
                "Kéo để xoay  ·  cuộn chuột hoặc vuốt hai ngón để phóng to  ·  chuột phải (hoặc Ctrl+kéo) để dịch chuyển.",
                13, InkMute, false, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
            var gyrt = goiY.rectTransform;
            gyrt.anchorMin = new Vector2(0, 0); gyrt.anchorMax = new Vector2(1, 0); gyrt.pivot = new Vector2(0, 0);
            gyrt.offsetMin = new Vector2(2, 0); gyrt.offsetMax = new Vector2(0, 22);
            goiY.horizontalOverflow = HorizontalWrapMode.Overflow;

            CapNhatNutKieu();
            CapNhatNutDo();
        }

        Button TaoNutToanManGocDuoi(RectTransform cha)
        {
            var rt = GlassKit.Rect(cha, "Nút Toàn màn hình 3D", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(174, 42));
            var sh = Shadow(rt, "Bóng", 10f, 12f, -3f, UiTheme.IsLight ? 0.14f : 0.45f);
            var face = Paint(Full(rt, "Mặt"), WithAlpha(Paper, 0.94f), 12f);
            face.raycastTarget = true;
            var ring = Ring(rt, "Viền", HairStrong, 12f);
            var glow = Paint(Full(rt, "Sáng"), new Color(1, 1, 1, 0f), 12f);

            var icoRt = GlassKit.Rect(rt, "Biểu tượng", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(18, 18));
            var ico = icoRt.gameObject.AddComponent<Image>();
            ico.sprite = IconFullscreen(); ico.color = Ink; ico.raycastTarget = false;

            var lbl = GlassKit.Label(rt, "Chữ", "Toàn màn hình", 14, Ink, true, TextAnchor.MiddleLeft, new Vector2(40, 0), new Vector2(126, 42));
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow; lbl.raycastTarget = false;

            var b = rt.gameObject.AddComponent<Button>();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.transition = Selectable.Transition.None;
            b.targetGraphic = face;
            b.onClick.AddListener(MoToanManHinh);

            var hv = rt.gameObject.AddComponent<MenuHover>();
            hv.hoverScale = 1.025f;
            hv.pressScale = 0.94f;
            hv.lift = 1.5f;
            hv.shadow = sh; hv.shadowRest = sh.color.a; hv.shadowHover = Mathf.Min(1f, sh.color.a * 1.6f);
            hv.ring = ring; hv.ringRest = HairStrong; hv.ringHover = WithAlpha(Accent, 0.7f);
            hv.tint = glow; hv.tintRest = new Color(Accent.r, Accent.g, Accent.b, 0f); hv.tintHover = new Color(Accent.r, Accent.g, Accent.b, 0.08f);
            hv.liftTargets = new[] { face.rectTransform, ring.rectTransform, glow.rectTransform, icoRt, lbl.rectTransform };
            return b;
        }

        void DungCotPhai(RectTransform cha)
        {
            TrangCuon.NhanNho(cha, "Nhãn thông tin chất", "Thông tin chất", new Vector2(0, 0), 300);

            tenChat = GlassKit.Label(cha, "Tên chất 3D", "", 28, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -26), new Vector2(400, 38));
            var tcrt = tenChat.rectTransform;
            tcrt.anchorMax = new Vector2(1, 1); tcrt.offsetMin = new Vector2(0, -64); tcrt.offsetMax = new Vector2(0, -26);
            tenChat.horizontalOverflow = HorizontalWrapMode.Wrap;

            tenEn = GlassKit.Label(cha, "Tên tiếng Anh 3D", "", 14, InkMute, false, TextAnchor.UpperLeft, new Vector2(0, -64), new Vector2(400, 22));
            var tert = tenEn.rectTransform;
            tert.anchorMax = new Vector2(1, 1); tert.offsetMin = new Vector2(0, -86); tert.offsetMax = new Vector2(0, -64);
            tenEn.gameObject.SetActive(false);

            // Công thức khung 2D (thoáng không viền hộp ở nền sáng, như ảnh mẫu)
            khoiAnh2D = GlassKit.Rect(cha, "Khung ảnh 2D", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -76), new Vector2(190, 122));
            if (!UiTheme.IsLight)
            {
                Paint(Full(khoiAnh2D, "Nền ảnh 2D"), Hex(0xf8f8fa), 12f);
                Ring(khoiAnh2D, "Viền", Hair, 12f);
            }
            anh2D = new GameObject("Ảnh 2D chất", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            anh2D.transform.SetParent(khoiAnh2D, false);
            var a2rt = anh2D.rectTransform;
            a2rt.anchorMin = Vector2.zero; a2rt.anchorMax = Vector2.one;
            a2rt.offsetMin = UiTheme.IsLight ? Vector2.zero : new Vector2(8, 8);
            a2rt.offsetMax = UiTheme.IsLight ? Vector2.zero : new Vector2(-8, -8);
            anh2D.raycastTarget = false;

            // Bảng thông số (CTPT, M, IUPAC, SMILES, PubChem, Nguồn 3D, Nhận diện)
            khoiMeta = Stretch(cha, "Bảng thông số 3D", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -584), new Vector2(0, -220));
            float y = 0;
            valCtpt = DongMeta(khoiMeta, "CTPT", ref y, 32f, true);
            valM = DongMeta(khoiMeta, "M", ref y, 32f, false);
            valIupac = DongMeta(khoiMeta, "IUPAC", ref y, 42f, false);
            valSmiles = DongMeta(khoiMeta, "SMILES", ref y, 42f, false);
            valPubChem = DongMetaLink(khoiMeta, "PUBCHEM", ref y, 32f, out nutPubChem);
            valNguon3D = DongMeta(khoiMeta, "NGUỒN 3D", ref y, 52f, false);
            valNhanDien = DongMeta(khoiMeta, "NHẬN DIỆN", ref y, 40f, false);

            // Hàng 4 nút tải (2×2 ở đáy cột phải)
            var khungTai = Stretch(cha, "Khung nút tải 3D", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4), new Vector2(0, 108));
            NutTai(khungTai, "Nút Tải PNG 3D", "Tải PNG", new Vector2(0, -10), 126f, false, () => XuatPng(false));
            NutTai(khungTai, "Nút Tải PNG 4K 3D", "Tải PNG 4K", new Vector2(134, -10), 146f, true, () => XuatPng(true));
            NutTai(khungTai, "Nút Tải SDF 3D", "Tải .sdf (3D)", new Vector2(0, -58), 148f, false, XuatSdf);
            NutTai(khungTai, "Nút Tải XYZ 3D", "Tải .xyz", new Vector2(156, -58), 116f, false, XuatXyz);

            var nutBao = MakeButton(khungTai, "Nút Báo sai 3D", "Báo sai", new Vector2(0, -60), new Vector2(74, 34), () => BaoSai?.Invoke(TenChatHienTai), 2, 10f, new Vector2(1, 1));
            ((RectTransform)nutBao.transform).pivot = new Vector2(1, 1);
            var bt = nutBao.transform.Find("Chữ").GetComponent<Text>();
            bt.fontSize = 13; bt.color = InkMute; bt.fontStyle = FontStyle.Normal;
        }

        static Text DongMeta(RectTransform cha, string nhan, ref float y, float h, bool dam)
        {
            var k = GlassKit.Label(cha, "Nhãn " + nhan, nhan, 12, InkMute, true, TextAnchor.UpperLeft, new Vector2(0, y - 3), new Vector2(96, 20));
            k.horizontalOverflow = HorizontalWrapMode.Overflow;
            var v = GlassKit.Label(cha, "Giá trị " + nhan, "—", 15, Ink, dam, TextAnchor.UpperLeft, new Vector2(104, y), new Vector2(100, h));
            var vrt = v.rectTransform;
            vrt.anchorMax = new Vector2(1, 1); vrt.offsetMin = new Vector2(104, y - h); vrt.offsetMax = new Vector2(0, y);
            v.horizontalOverflow = HorizontalWrapMode.Wrap;
            v.verticalOverflow = VerticalWrapMode.Truncate;
            v.lineSpacing = 1.15f;
            y -= h + 4f;
            return v;
        }

        Text DongMetaLink(RectTransform cha, string nhan, ref float y, float h, out Button nut)
        {
            var k = GlassKit.Label(cha, "Nhãn " + nhan, nhan, 12, InkMute, true, TextAnchor.UpperLeft, new Vector2(0, y - 3), new Vector2(96, 20));
            k.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = Stretch(cha, "Nút PubChem 3D", new Vector2(0, 1), new Vector2(1, 1), new Vector2(104, y - h), new Vector2(0, y));
            var hit = rt.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0.001f);
            nut = rt.gameObject.AddComponent<Button>();
            nut.navigation = new Navigation { mode = Navigation.Mode.None };
            nut.transition = Selectable.Transition.None;
            nut.targetGraphic = hit;
            nut.onClick.AddListener(() => { if (!string.IsNullOrEmpty(urlPubChem)) Application.OpenURL(urlPubChem); });
            var hv = rt.gameObject.AddComponent<MenuHover>();
            hv.hoverScale = 1.015f;
            hv.pressScale = 0.95f;
            var v = GlassKit.Label(rt, "Giá trị " + nhan, "—", 15, Accent, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(260, h));
            var vrt = v.rectTransform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            y -= h + 4f;
            return v;
        }

        void NutTai(Transform cha, string ten, string chu, Vector2 pos, float w, bool dam, Action bam)
        {
            var b = MakeButton(cha, ten, chu, pos, new Vector2(w, 38), bam, 1, 19f);
            var t = b.transform.Find("Chữ").GetComponent<Text>();
            t.fontSize = 14; t.fontStyle = dam ? FontStyle.Bold : FontStyle.Normal;
            t.color = Ink;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void DungMenuTha()
        {
            lopChanMenu = Full(thanThe, "Lớp đóng menu 3D");
            var ci = lopChanMenu.gameObject.AddComponent<Image>(); ci.color = new Color(0, 0, 0, 0.001f);
            var cb = lopChanMenu.gameObject.AddComponent<Button>(); cb.transition = Selectable.Transition.None; cb.onClick.AddListener(DongMenu);
            lopChanMenu.gameObject.SetActive(false);

            menuBeMat = TaoMenuTha("Menu Bề mặt 3D", TenBeMat, 196f, i =>
            {
                beMat = (BeMatPhanTu.Loai)i;
                if (Xem != null) Xem.TP.BeMat = beMat;
                CapNhatNutChon();
                DongMenu();
            });
            menuMau = TaoMenuTha("Menu Màu 3D", TenMau, 168f, i =>
            {
                bangMau = i;
                if (Xem != null) Xem.TP.BangMau = bangMau;
                CapNhatNutChon();
                DongMenu();
            });
        }

        RectTransform TaoMenuTha(string ten, string[] muc, float w, Action<int> chon)
        {
            float h = muc.Length * 38f + 12f;
            var rt = GlassKit.Rect(thanThe, ten, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(w, h));
            Shadow(rt, "Bóng", 16f, 12f, -4f, UiTheme.IsLight ? 0.14f : 0.55f);
            Paint(Full(rt, "Mặt"), Paper, 12f).raycastTarget = true;
            Ring(rt, "Viền", HairStrong, 12f);
            for (int i = 0; i < muc.Length; i++)
            {
                int ii = i;
                var b = MakeButton(rt, ten + " mục " + i, muc[i], new Vector2(6, -6 - i * 38f), new Vector2(w - 12f, 36f), () => chon(ii), 2, 8f);
                var t = b.transform.Find("Chữ").GetComponent<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.fontSize = 14; t.color = Ink;
                t.rectTransform.offsetMin = new Vector2(12, 0); t.rectTransform.offsetMax = new Vector2(-12, 0);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            rt.gameObject.SetActive(false);
            return rt;
        }

        void MoMenu(RectTransform menu, Button nutGoc)
        {
            bool dangMo = menu.gameObject.activeSelf;
            DongMenu();
            if (dangMo) return;
            // đặt vị trí ngay dưới nút nguồn
            var corners = new Vector3[4];
            ((RectTransform)nutGoc.transform).GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(thanThe,
                RectTransformUtility.WorldToScreenPoint(GetComponentInParent<Canvas>()?.rootCanvas?.worldCamera, corners[0]),
                GetComponentInParent<Canvas>()?.rootCanvas?.worldCamera, out var lp);
            var r = thanThe.rect;
            menu.anchoredPosition = new Vector2(lp.x - r.xMin, lp.y - r.yMax - 6f);
            CapNhatDauChonMenu(menu, menu == menuBeMat ? (int)beMat : bangMau, menu == menuBeMat ? TenBeMat : TenMau);
            lopChanMenu.gameObject.SetActive(true);
            lopChanMenu.SetAsLastSibling();
            menu.gameObject.SetActive(true);
            menu.SetAsLastSibling();
            MenuAnim.Pop(menu, 0.91f, 0.18f);
        }

        static void CapNhatDauChonMenu(RectTransform menu, int dang, string[] ten)
        {
            for (int i = 0; i < ten.Length; i++)
            {
                var tr = menu.Find(menu.name + " mục " + i);
                if (tr == null) continue;
                var t = tr.Find("Chữ")?.GetComponent<Text>();
                if (t != null)
                {
                    t.text = (i == dang ? "✓  " : "     ") + ten[i];
                    t.color = i == dang ? Accent : Ink;
                    t.fontStyle = i == dang ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        void DongMenu()
        {
            if (lopChanMenu != null) lopChanMenu.gameObject.SetActive(false);
            if (menuBeMat != null) menuBeMat.gameObject.SetActive(false);
            if (menuMau != null) menuMau.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ nạp chất mới
        public void Hien(Dictionary<string, object> d)
        {
            if (d == null) return;
            DuLieu = d;
            DongMenu();
            XoaTatCaDo(false);
            cheDoDo = "";
            CapNhatNutDo();
            if (badgeHover != null) badgeHover.gameObject.SetActive(false);

            Goc.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();

            var kb = MoHinh3DMoDun.TaoKichBan(d);
            if (Xem == null)
            {
                var cv = GetComponentInParent<Canvas>();
                var camUi = cv != null ? cv.rootCanvas.worldCamera : null;
                Xem = Xem3DNho.Tao(anh3DRt, kb, camUi);
                Xem.KieuMoHinh(MauNen3D);
                Xem.ChanChuot = p =>
                    (menuBeMat != null && menuBeMat.gameObject.activeSelf)
                    || (menuMau != null && menuMau.gameObject.activeSelf)
                    || (nutFullDuoiRt != null && RectTransformUtility.RectangleContainsScreenPoint(nutFullDuoiRt, p, camUi));
                Xem.transform.SetParent(khungViewport, false);
            }
            else
            {
                Xem.DatNen(MauNen3D);
                Xem.NapLai(kb);
            }
            ApTrangThaiXem();
            DienThongTin(d);
        }

        void ApTrangThaiXem()
        {
            if (Xem == null) return;
            Xem.TP.KieuVe = kieuVe;
            Xem.TP.BeMat = beMat;
            Xem.TP.BangMau = bangMau;
            Xem.TP.TuXoay = tuXoay;
            Xem.HienNhan = hienNhan;
            CapNhatNutKieu();
            CapNhatNutChon();
            if (ctNhan != null) ctNhan.SetIsOnWithoutNotify(hienNhan);
            if (ctXoay != null) ctXoay.SetIsOnWithoutNotify(tuXoay);
        }

        void DienThongTin(Dictionary<string, object> d)
        {
            string ten = S(d, "ten");
            if (ten.Length > 1 && char.IsLower(ten[0])) ten = char.ToUpper(ten[0]) + ten.Substring(1);
            tenChat.text = string.IsNullOrEmpty(ten) ? "(không tên)" : ten;

            string en = S(d, "ten_tieng_anh");
            bool hienEn = !string.IsNullOrEmpty(en) && !string.Equals(en, ten, StringComparison.OrdinalIgnoreCase);
            tenEn.gameObject.SetActive(hienEn);
            if (hienEn) tenEn.text = en;
            khoiAnh2D.anchoredPosition = new Vector2(0, hienEn ? -94f : -76f);
            khoiMeta.offsetMax = new Vector2(0, hienEn ? -236f : -220f);

            // Ảnh 2D (ưu tiên anh_2d PNG base64 từ backend RDKit; không có thì tự vẽ công thức khung 2D nét)
            NapAnh2D(d);

            string ct = S(d, "cong_thuc");
            valCtpt.text = string.IsNullOrEmpty(ct) ? "—" : CongThucChiSo(ct);
            float m = F(Lay(d, "khoi_luong"));
            valM.text = m > 0 ? m.ToString("0.00", CultureInfo.InvariantCulture) + " g/mol" : "—";
            string iupac = S(d, "iupac");
            valIupac.text = string.IsNullOrEmpty(iupac) ? (string.IsNullOrEmpty(ten) ? "—" : ten.ToLowerInvariant()) : iupac;
            string smi = S(d, "smiles");
            valSmiles.text = string.IsNullOrEmpty(smi) ? "—" : smi;

            int cid = I(Lay(d, "cid"));
            urlPubChem = S(d, "url");
            if (cid > 0)
            {
                if (string.IsNullOrEmpty(urlPubChem)) urlPubChem = "https://pubchem.ncbi.nlm.nih.gov/compound/" + cid;
                valPubChem.text = $"CID {cid}  ↗";
                valPubChem.color = Accent;
                nutPubChem.interactable = true;
            }
            else
            {
                urlPubChem = "";
                valPubChem.text = "—";
                valPubChem.color = InkMute;
                nutPubChem.interactable = false;
            }

            int soNt = I(Lay(d, "so_nguyen_tu"));
            if (soNt <= 0) soNt = L(Lay(d, "nguyen_tu")).Count;
            string ng3d = S(d, "nguon_3d_mo_ta");
            if (string.IsNullOrEmpty(ng3d))
                ng3d = S(d, "nguon_3d") == "pubchem_3d"
                    ? (cid > 0 ? $"PubChem 3D conformer (CID {cid})" : "PubChem 3D conformer")
                    : "RDKit (ETKDG v3 + MMFF94)";
            valNguon3D.text = $"{ng3d}  ·  {soNt} nguyên tử";

            string cach = S(d, "cach_nhan_dien");
            if (string.IsNullOrEmpty(cach))
                cach = S(d, "nguon") switch { "bang_sgk" => "Bảng SGK (đã kiểm chứng)", "pubchem" => "PubChem (theo tên)", "smiles" => "SMILES", "ai_anh" => "AI nhận diện từ ảnh", var s => string.IsNullOrEmpty(s) ? "—" : s };
            valNhanDien.text = cach;
        }

        void NapAnh2D(Dictionary<string, object> d)
        {
            if (tex2D != null) { Destroy(tex2D); tex2D = null; }
            string b64 = S(d, "anh_2d");
            if (!string.IsNullOrEmpty(b64))
            {
                int comma = b64.IndexOf(',');
                if (comma >= 0) b64 = b64.Substring(comma + 1);
                try
                {
                    var bytes = Convert.FromBase64String(b64);
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (t.LoadImage(bytes)) tex2D = t;
                    else Destroy(t);
                }
                catch { }
            }
            if (tex2D == null) tex2D = VeCauTruc2D(d);
            anh2D.texture = tex2D;
            anh2D.gameObject.SetActive(tex2D != null);
            if (tex2D != null)
            {
                var fit = anh2D.gameObject.GetComponent<AspectRatioFitter>() ?? anh2D.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = tex2D.width / (float)Mathf.Max(1, tex2D.height);
            }
        }

        // ------------------------------------------------------------------ điều khiển kiểu, bề mặt, màu, góc, toàn màn hình
        public void DatKieu(int k)
        {
            kieuVe = Mathf.Clamp(k, 0, 3);
            if (Xem != null) Xem.TP.KieuVe = kieuVe;
            CapNhatNutKieu();
        }

        public void DatBeMat(BeMatPhanTu.Loai loai)
        {
            beMat = loai;
            if (Xem != null) Xem.TP.BeMat = beMat;
            CapNhatNutChon();
        }

        public void DatBangMau(int m)
        {
            bangMau = Mathf.Clamp(m, 0, 2);
            if (Xem != null) Xem.TP.BangMau = bangMau;
            CapNhatNutChon();
        }

        public void DatLaiGoc() => Xem?.DatLaiGoc();

        /// <summary>Bật lớp phủ Toàn màn hình như app VERA hiện tại, giữ nguyên chất và các tuỳ chọn hiển thị.</summary>
        public void MoToanManHinh()
        {
            if (DuLieu == null) return;
            DongMenu();
            MoHinh3DMoDun.MoVoiDuLieu(
                DuLieu, kieuVe, beMat, bangMau, hienNhan, tuXoay,
                Xem != null ? (Quaternion?)Xem.TP.Xoay : null,
                Xem != null ? Xem.TP.KhoangCach : 0f,
                (dMoi, kv, bm, m, nh, tx) =>
                {
                    kieuVe = kv; beMat = bm; bangMau = m; hienNhan = nh; tuXoay = tx;
                    if (dMoi != null && !ReferenceEquals(dMoi, DuLieu))
                    {
                        Hien(dMoi);
                        CapNhatTuToanMan?.Invoke(dMoi);
                    }
                    else ApTrangThaiXem();
                });
        }

        void CapNhatNutKieu()
        {
            for (int i = 0; i < 4; i++)
            {
                if (nenNutKieu[i] == null) continue;
                bool chon = kieuVe == i;
                nenNutKieu[i].color = chon ? Paper : new Color(0, 0, 0, 0);
                if (vienNutKieu[i] != null) vienNutKieu[i].color = chon ? Hair : new Color(0, 0, 0, 0);
                chuNutKieu[i].color = chon ? Ink : InkDim;
                chuNutKieu[i].font = chon ? GlassKit.Semibold : GlassKit.Regular;
            }
        }

        void CapNhatNutChon()
        {
            if (nutBeMat != null) nutBeMat.transform.Find("Chữ").GetComponent<Text>().text = TenBeMat[(int)beMat] + "  ▾";
            if (nutMau != null) nutMau.transform.Find("Chữ").GetComponent<Text>().text = TenMau[bangMau] + "  ▾";
        }

        // ------------------------------------------------------------------ đo khoảng cách & đo góc liên kết
        public void BatCheDoDo(string loai)
        {
            if (cheDoDo == loai)
            {
                cheDoDo = "";
                dangChon.Clear();
                thongBaoDo.text = "Rê chuột xem nguyên tử  ·  Bấm nút để đo liên kết";
            }
            else
            {
                cheDoDo = loai;
                dangChon.Clear();
                thongBaoDo.text = loai == "khoang-cach"
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(Accent)}><b>Đo khoảng cách:</b></color> hãy nhấp chọn nguyên tử thứ nhất…"
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(Accent)}><b>Đo góc:</b></color> hãy nhấp 3 nguyên tử (nguyên tử 2 là đỉnh góc)…";
            }
            CapNhatNutDo();
        }

        void CapNhatNutDo()
        {
            ToMauNutDo(nutDoKc, cheDoDo == "khoang-cach");
            ToMauNutDo(nutDoGoc, cheDoDo == "goc");
            bool co = cacPhepDo.Count > 0;
            if (nutXoaDo != null) nutXoaDo.gameObject.SetActive(co);
            if (thongBaoDo != null) thongBaoDo.rectTransform.offsetMin = new Vector2(co ? 458f : 356f, 0);
        }

        static void ToMauNutDo(Button b, bool dang)
        {
            if (b == null) return;
            var mat = b.transform.Find("Mặt")?.GetComponent<Image>();
            var chu = b.transform.Find("Chữ")?.GetComponent<Text>();
            var ico = b.transform.Find("Biểu tượng")?.GetComponent<Image>();
            if (mat != null) mat.color = dang ? Accent : Paper;
            if (chu != null) { chu.color = dang ? OnAccent : Ink; chu.fontStyle = dang ? FontStyle.Bold : FontStyle.Normal; }
            if (ico != null) ico.color = dang ? OnAccent : InkDim;
        }

        void XuLyBam3D(Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(cheDoDo) || Xem == null) return;
            if (!Xem.TimNguyenTuTai(screenPos, out var id, out var elem, out var idx, out var pos, out _)) return;
            for (int i = 0; i < dangChon.Count; i++) if (dangChon[i].id == id) return;
            dangChon.Add(new DiemDo { id = id, elem = elem, soHieu = idx + 1, pos = pos });

            string maNhan = ColorUtility.ToHtmlStringRGB(Accent);
            if (cheDoDo == "khoang-cach")
            {
                if (dangChon.Count == 1)
                    thongBaoDo.text = $"<color=#{maNhan}><b>Đo khoảng cách:</b></color> đã chọn <b>{elem} #{idx + 1}</b>. Nhấp nguyên tử thứ hai…";
                else if (dangChon.Count >= 2)
                {
                    var a1 = dangChon[0]; var a2 = dangChon[1];
                    float d = Vector3.Distance(a1.pos, a2.pos);
                    string kq = d.ToString("0.000", CultureInfo.InvariantCulture) + " Å";
                    cacPhepDo.Add(new PhepDo { laGoc = false, diem = new[] { a1, a2 }, ketQua = kq });
                    thongBaoDo.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Bio)}><b>Khoảng cách {a1.elem}–{a2.elem}: {kq}</b></color>";
                    dangChon.Clear();
                    CapNhatNutDo();
                }
            }
            else if (cheDoDo == "goc")
            {
                if (dangChon.Count == 1)
                    thongBaoDo.text = $"<color=#{maNhan}><b>Đo góc:</b></color> đã chọn <b>{elem} #{idx + 1}</b> (cạnh 1). Nhấp đỉnh góc…";
                else if (dangChon.Count == 2)
                    thongBaoDo.text = $"<color=#{maNhan}><b>Đo góc:</b></color> đã chọn đỉnh <b>{elem} #{idx + 1}</b>. Nhấp nguyên tử cạnh 2…";
                else if (dangChon.Count >= 3)
                {
                    var a1 = dangChon[0]; var a2 = dangChon[1]; var a3 = dangChon[2];
                    float goc = Vector3.Angle(a1.pos - a2.pos, a3.pos - a2.pos);
                    string kq = goc.ToString("0.1", CultureInfo.InvariantCulture) + "°";
                    cacPhepDo.Add(new PhepDo { laGoc = true, diem = new[] { a1, a2, a3 }, ketQua = kq });
                    thongBaoDo.text = $"<color=#{maNhan}><b>Góc {a1.elem}–{a2.elem}–{a3.elem}: {kq}</b></color>";
                    dangChon.Clear();
                    CapNhatNutDo();
                }
            }
        }

        public void XoaTatCaDo() => XoaTatCaDo(true);

        void XoaTatCaDo(bool bao)
        {
            cacPhepDo.Clear();
            dangChon.Clear();
            CapNhatNutDo();
            if (bao && thongBaoDo != null)
                thongBaoDo.text = "Đã xoá các phép đo. Rê chuột hoặc bấm nút để đo tiếp.";
            AnLopDo();
        }

        void AnLopDo()
        {
            for (int i = 0; i < poolDuong.Count; i++) if (poolDuong[i].gameObject.activeSelf) poolDuong[i].gameObject.SetActive(false);
            for (int i = 0; i < poolNhanDo.Count; i++) if (poolNhanDo[i].gameObject.activeSelf) poolNhanDo[i].gameObject.SetActive(false);
            for (int i = 0; i < poolChon.Count; i++) if (poolChon[i].gameObject.activeSelf) poolChon[i].gameObject.SetActive(false);
        }

        void Update()
        {
            if (!DangHien || Xem == null || MoHinh3DMoDun.DangMo || CoCheMoDun.DangMo) return;
            if ((menuBeMat != null && menuBeMat.gameObject.activeSelf) || (menuMau != null && menuMau.gameObject.activeSelf))
            {
                if (badgeHover.gameObject.activeSelf) badgeHover.gameObject.SetActive(false);
                return;
            }
            var m = Mouse.current;
            if (m != null && !m.leftButton.isPressed && !m.rightButton.isPressed)
            {
                var p = m.position.ReadValue();
                if (Xem.TimNguyenTuTai(p, out _, out var elem, out var idx, out var molPos, out var lp))
                {
                    string tenTv = TenNguyenTo.TryGetValue(elem, out var tv) ? tv : elem;
                    badgeHoverTen.text = $"{elem} ({tenTv}) #{idx + 1}";
                    badgeHoverChiTiet.text = string.Format(CultureInfo.InvariantCulture, "X: {0:0.00}   Y: {1:0.00}   Z: {2:0.00}", molPos.x, molPos.y, -molPos.z);
                    var r = khungViewport.rect;
                    float bx = Mathf.Clamp(lp.x, -r.width * 0.5f + 138f, r.width * 0.5f - 138f);
                    float by = Mathf.Clamp(lp.y + 42f, -r.height * 0.5f + 30f, r.height * 0.5f - 30f);
                    badgeHover.anchoredPosition = new Vector2(bx, by);
                    if (!badgeHover.gameObject.activeSelf) badgeHover.gameObject.SetActive(true);
                }
                else if (badgeHover.gameObject.activeSelf) badgeHover.gameObject.SetActive(false);
            }
            else if (badgeHover.gameObject.activeSelf) badgeHover.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!DangHien || Xem == null || (cacPhepDo.Count == 0 && dangChon.Count == 0))
            {
                AnLopDo();
                return;
            }
            int dLine = 0, dNhan = 0, dChon = 0;
            Color xanhLa = Hex(0x10b981), xanhLam = Hex(0x3b82f6);
            foreach (var pd in cacPhepDo)
            {
                if (!pd.laGoc && pd.diem.Length == 2)
                {
                    if (Xem.ChieuLenKhung(pd.diem[0].pos, out var p1) && Xem.ChieuLenKhung(pd.diem[1].pos, out var p2))
                    {
                        VeDuong(ref dLine, p1, p2, xanhLa, 3f);
                        VeNhanDo(ref dNhan, (p1 + p2) * 0.5f, pd.ketQua, Hex(0x064e3b), Hex(0xd1fae5), xanhLa);
                    }
                }
                else if (pd.laGoc && pd.diem.Length == 3)
                {
                    if (Xem.ChieuLenKhung(pd.diem[0].pos, out var p1) && Xem.ChieuLenKhung(pd.diem[1].pos, out var p2) && Xem.ChieuLenKhung(pd.diem[2].pos, out var p3))
                    {
                        VeDuong(ref dLine, p1, p2, xanhLam, 2.5f);
                        VeDuong(ref dLine, p3, p2, xanhLam, 2.5f);
                        VeNhanDo(ref dNhan, p2 + new Vector2(0, 26f), pd.ketQua, Hex(0x1e3a8a), Hex(0xdbeafe), xanhLam);
                    }
                }
            }
            for (int i = 0; i < dangChon.Count; i++)
                if (Xem.ChieuLenKhung(dangChon[i].pos, out var p))
                    VeDauChon(ref dChon, p, $"[{i + 1}]");

            for (int i = dLine; i < poolDuong.Count; i++) if (poolDuong[i].gameObject.activeSelf) poolDuong[i].gameObject.SetActive(false);
            for (int i = dNhan; i < poolNhanDo.Count; i++) if (poolNhanDo[i].gameObject.activeSelf) poolNhanDo[i].gameObject.SetActive(false);
            for (int i = dChon; i < poolChon.Count; i++) if (poolChon[i].gameObject.activeSelf) poolChon[i].gameObject.SetActive(false);
        }

        void VeDuong(ref int idx, Vector2 a, Vector2 b, Color mau, float day)
        {
            while (poolDuong.Count <= idx)
            {
                var rt = GlassKit.Rect(lopDo, "Đường đo", new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, day));
                var im = rt.gameObject.AddComponent<Image>(); im.raycastTarget = false;
                poolDuong.Add(im);
            }
            var img = poolDuong[idx++];
            var v = b - a;
            img.color = mau;
            img.rectTransform.anchoredPosition = a;
            img.rectTransform.sizeDelta = new Vector2(v.magnitude, day);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        }

        void VeNhanDo(ref int idx, Vector2 p, string chu, Color mauChu, Color nen, Color vien)
        {
            while (poolNhanDo.Count <= idx)
            {
                var rt = GlassKit.Rect(lopDo, "Nhãn đo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(82, 26));
                Paint(Full(rt, "Mặt"), nen, 8f);
                Ring(rt, "Viền", vien, 8f);
                var t = GlassKit.Label(rt, "Chữ", "", 13, mauChu, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(82, 26));
                t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
                poolNhanDo.Add(rt);
            }
            var box = poolNhanDo[idx++];
            var lbl = box.Find("Chữ").GetComponent<Text>();
            if (lbl.text != chu)
            {
                lbl.text = chu;
                float w = Mathf.Max(68f, chu.Length * 9f + 22f);
                box.sizeDelta = new Vector2(w, 26f);
                lbl.rectTransform.sizeDelta = box.sizeDelta;
            }
            lbl.color = mauChu;
            box.Find("Mặt").GetComponent<Image>().color = nen;
            box.Find("Viền").GetComponent<Image>().color = vien;
            box.anchoredPosition = p;
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
        }

        void VeDauChon(ref int idx, Vector2 p, string chu)
        {
            while (poolChon.Count <= idx)
            {
                var rt = GlassKit.Rect(lopDo, "Điểm đo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 24));
                Paint(Full(rt, "Mặt"), Hex(0xf59e0b), 8f);
                var t = GlassKit.Label(rt, "Chữ", "", 12, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(30, 24));
                t.raycastTarget = false;
                poolChon.Add(rt);
            }
            var box = poolChon[idx++];
            var lbl = box.Find("Chữ").GetComponent<Text>();
            if (lbl.text != chu) lbl.text = chu;
            box.anchoredPosition = p + new Vector2(0, 18f);
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ xuất tệp PNG / PNG 4K / .sdf / .xyz
        string ThuMucXuat()
        {
            string goc = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
            string thu = string.IsNullOrEmpty(goc) ? Path.Combine(Application.persistentDataPath, "Xuat3D") : Path.Combine(goc, "VERA Lab");
            Directory.CreateDirectory(thu);
            return thu;
        }

        string TenTepAnToan()
        {
            string s = S(DuLieu, "ten_tieng_anh");
            if (string.IsNullOrEmpty(s)) s = S(DuLieu, "ten");
            if (string.IsNullOrEmpty(s)) s = "chat";
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
            string kq = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(kq) ? "chat" : kq.Substring(0, Mathf.Min(36, kq.Length));
        }

        public void XuatPng(bool bonK)
        {
            if (Xem == null || DuLieu == null) return;
            try
            {
                string duong = Path.Combine(ThuMucXuat(), $"{TenTepAnToan()}_3d{(bonK ? "_4K" : "")}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                Xem.LuuAnhCo(duong, bonK ? 2560 : 1280, bonK ? 1600 : 800);
                DuongTepCuoi = duong;
                BaoTrangThai?.Invoke($"Đã lưu {(bonK ? "ảnh 4K" : "ảnh PNG")}: {duong}", false);
            }
            catch (Exception e) { BaoTrangThai?.Invoke("Không lưu được ảnh: " + e.Message, true); }
        }

        public void XuatSdf()
        {
            if (DuLieu == null) return;
            try
            {
                string sdf = S(DuLieu, "sdf");
                if (string.IsNullOrEmpty(sdf)) sdf = TaoSdfTuDuLieu(DuLieu);
                string duong = Path.Combine(ThuMucXuat(), $"{TenTepAnToan()}_3d.sdf");
                File.WriteAllText(duong, sdf, Encoding.UTF8);
                DuongTepCuoi = duong;
                BaoTrangThai?.Invoke("Đã lưu tệp .sdf (3D): " + duong, false);
            }
            catch (Exception e) { BaoTrangThai?.Invoke("Không lưu được .sdf: " + e.Message, true); }
        }

        public void XuatXyz()
        {
            if (DuLieu == null) return;
            try
            {
                string xyz = S(DuLieu, "xyz");
                if (string.IsNullOrEmpty(xyz)) xyz = TaoXyzTuDuLieu(DuLieu);
                string duong = Path.Combine(ThuMucXuat(), $"{TenTepAnToan()}.xyz");
                File.WriteAllText(duong, xyz, Encoding.UTF8);
                DuongTepCuoi = duong;
                BaoTrangThai?.Invoke("Đã lưu tệp .xyz: " + duong, false);
            }
            catch (Exception e) { BaoTrangThai?.Invoke("Không lưu được .xyz: " + e.Message, true); }
        }

        static string TaoSdfTuDuLieu(Dictionary<string, object> d)
        {
            var nt = L(Lay(d, "nguyen_tu"));
            var lk = L(Lay(d, "lien_ket"));
            var sb = new StringBuilder();
            sb.AppendLine(S(d, "ten", "Molecule"));
            sb.AppendLine("  VERALab 3D");
            sb.AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0,3}{1,3}  0  0  0  0  0  0  0  0999 V2000\n", nt.Count, lk.Count);
            foreach (var o in nt)
            {
                var a = D(o);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0,10:0.0000}{1,10:0.0000}{2,10:0.0000} {3,-3} 0  0  0  0  0  0  0  0  0  0  0  0\n",
                    F(Lay(a, "x")), F(Lay(a, "y")), F(Lay(a, "z")), S(a, "e", "C"));
            }
            foreach (var o in lk)
            {
                var b = D(o);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0,3}{1,3}{2,3}  0  0  0  0\n",
                    I(Lay(b, "a")) + 1, I(Lay(b, "b")) + 1, Mathf.Max(1, I(Lay(b, "bac"))));
            }
            sb.AppendLine("M  END");
            sb.AppendLine("$$$$");
            return sb.ToString();
        }

        static string TaoXyzTuDuLieu(Dictionary<string, object> d)
        {
            var nt = L(Lay(d, "nguyen_tu"));
            var sb = new StringBuilder();
            sb.AppendLine(nt.Count.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(S(d, "ten", "Molecule"));
            foreach (var o in nt)
            {
                var a = D(o);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0,-3} {1,10:0.0000} {2,10:0.0000} {3,10:0.0000}\n",
                    S(a, "e", "C"), F(Lay(a, "x")), F(Lay(a, "y")), F(Lay(a, "z")));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ vẽ công thức khung 2D dự phòng khi không có anh_2d
        static Texture2D VeCauTruc2D(Dictionary<string, object> d)
        {
            var nt = L(Lay(d, "nguyen_tu"));
            var lk = L(Lay(d, "lien_ket"));
            if (nt.Count == 0) return null;

            // giữ nguyên tử nặng (như công thức khung SGK); nếu chỉ có H thì giữ hết
            var map = new Dictionary<int, int>();
            var pos3 = new List<Vector3>();
            var elem = new List<string>();
            for (int i = 0; i < nt.Count; i++)
            {
                var a = D(nt[i]);
                string e = S(a, "e", "C");
                if (e == "H") continue;
                map[i] = pos3.Count;
                pos3.Add(new Vector3(F(Lay(a, "x")), F(Lay(a, "y")), F(Lay(a, "z"))));
                elem.Add(e);
            }
            if (pos3.Count == 0)
            {
                for (int i = 0; i < nt.Count; i++)
                {
                    var a = D(nt[i]);
                    map[i] = pos3.Count;
                    pos3.Add(new Vector3(F(Lay(a, "x")), F(Lay(a, "y")), F(Lay(a, "z"))));
                    elem.Add(S(a, "e", "C"));
                }
            }
            Vector3 tam = Vector3.zero;
            foreach (var p in pos3) tam += p;
            tam /= pos3.Count;
            for (int i = 0; i < pos3.Count; i++) pos3[i] -= tam;

            // chiếu lên mặt phẳng trải rộng nhất (PCA 2 trục chính)
            float xx = 0, yy = 0, zz = 0, xy = 0, xz = 0, yz = 0;
            foreach (var p in pos3) { xx += p.x * p.x; yy += p.y * p.y; zz += p.z * p.z; xy += p.x * p.y; xz += p.x * p.z; yz += p.y * p.z; }
            float tr = xx + yy + zz;
            var n = new Vector3(0.23f, 0.37f, 0.9f).normalized;
            for (int it = 0; it < 32; it++)
            {
                var w = new Vector3((tr - xx) * n.x - xy * n.y - xz * n.z, -xy * n.x + (tr - yy) * n.y - yz * n.z, -xz * n.x - yz * n.y + (tr - zz) * n.z);
                if (w.sqrMagnitude < 1e-8f) break;
                n = w.normalized;
            }
            var q = Quaternion.FromToRotation(n, Vector3.forward);
            var p2 = new Vector2[pos3.Count];
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < pos3.Count; i++)
            {
                var r = q * pos3[i];
                p2[i] = new Vector2(r.x, r.y);
                mn = Vector2.Min(mn, p2[i]); mx = Vector2.Max(mx, p2[i]);
            }

            const int W = 320, H = 200;
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            float spanX = Mathf.Max(0.8f, mx.x - mn.x), spanY = Mathf.Max(0.8f, mx.y - mn.y);
            float scale = Mathf.Min((W - 64f) / spanX, (H - 48f) / spanY);
            Vector2 mid = (mn + mx) * 0.5f, cen = new Vector2(W * 0.5f, H * 0.5f);
            Vector2 ToPx(int i) => cen + (p2[i] - mid) * scale;

            Color32 ink = new Color32(0x18, 0x18, 0x1b, 255);
            foreach (var o in lk)
            {
                var b = D(o);
                int ia = I(Lay(b, "a")), ib = I(Lay(b, "b"));
                if (!map.TryGetValue(ia, out int ua) || !map.TryGetValue(ib, out int ub)) continue;
                int bac = Mathf.Clamp(Mathf.Max(1, I(Lay(b, "bac"))), 1, 3);
                Vector2 a = ToPx(ua), c = ToPx(ub), dir = (c - a).normalized, perp = new Vector2(-dir.y, dir.x);
                if (bac == 1) VeDoanAA(px, W, H, a, c, 2.2f, ink);
                else if (bac == 2)
                {
                    VeDoanAA(px, W, H, a + perp * 3.5f, c + perp * 3.5f, 2.0f, ink);
                    VeDoanAA(px, W, H, a - perp * 3.5f, c - perp * 3.5f, 2.0f, ink);
                }
                else
                {
                    VeDoanAA(px, W, H, a, c, 1.9f, ink);
                    VeDoanAA(px, W, H, a + perp * 5.2f, c + perp * 5.2f, 1.8f, ink);
                    VeDoanAA(px, W, H, a - perp * 5.2f, c - perp * 5.2f, 1.8f, ink);
                }
            }
            // chấm màu nhỏ cho dị tố (O đỏ, N xanh, Cl lục, S vàng) để công thức dễ nhận diện
            for (int i = 0; i < elem.Count; i++)
            {
                if (elem[i] == "C") continue;
                Color32 mc = elem[i] == "O" ? new Color32(0xdc, 0x26, 0x26, 255)
                           : elem[i] == "N" ? new Color32(0x25, 0x63, 0xeb, 255)
                           : elem[i] == "Cl" || elem[i] == "F" ? new Color32(0x05, 0x96, 0x69, 255)
                           : new Color32(0xd9, 0x77, 0x06, 255);
                VeChamAA(px, W, H, ToPx(i), 7f, mc);
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ nút & công tắc phụ trợ + biểu tượng SDF sắc nét
        static Button NutChon(Transform cha, string ten, string chu, Vector2 pos, Vector2 co, Action bam)
        {
            var b = MakeButton(cha, ten, chu, pos, co, bam, 1, 12f);
            var t = b.transform.Find("Chữ").GetComponent<Text>();
            t.fontSize = 14; t.fontStyle = FontStyle.Normal; t.color = Ink;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }

        static Button NutBieuTuong(Transform cha, string ten, Sprite ico, Vector2 pos, float co, Action bam, Vector2? anchor = null)
        {
            var b = MakeButton(cha, ten, "", pos, new Vector2(co, co), bam, 1, 12f, anchor);
            var irt = GlassKit.Rect(b.transform, "Biểu tượng", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            var im = irt.gameObject.AddComponent<Image>();
            im.sprite = ico; im.color = Ink; im.raycastTarget = false;
            return b;
        }

        static Button NutCongCuDo(Transform cha, string ten, Sprite ico, string chu, Vector2 pos, float w, Action bam)
        {
            var b = MakeButton(cha, ten, chu, pos, new Vector2(w, 34), bam, 1, 9f);
            var t = b.transform.Find("Chữ").GetComponent<Text>();
            t.fontSize = 13; t.fontStyle = FontStyle.Normal; t.color = Ink;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (ico != null)
            {
                var irt = GlassKit.Rect(b.transform, "Biểu tượng", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(11, 0), new Vector2(15, 15));
                var im = irt.gameObject.AddComponent<Image>();
                im.sprite = ico; im.color = InkDim; im.raycastTarget = false;
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.anchoredPosition = new Vector2(32, 0);
                t.rectTransform.sizeDelta = new Vector2(w - 38, 34);
            }
            return b;
        }

        /// <summary>Công tắc gọn có rãnh bên trái, chữ bên phải (như ảnh mẫu: rãnh sáng viền rõ, núm đậm khi tắt, xanh khi bật).</summary>
        static Toggle CongTacTrai(Transform cha, string ten, string nhan, Vector2 pos, float w, bool batDau, Action<bool> doi)
        {
            var rt = GlassKit.Rect(cha, ten, new Vector2(1, 1), new Vector2(1, 1), pos, new Vector2(w, 36));
            var hit = rt.gameObject.AddComponent<Image>(); hit.sprite = GlassKit.Plain; hit.color = new Color(1, 1, 1, 0.001f);
            var track = Fill(rt, "Rãnh công tắc", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(38, 22), UiTheme.IsLight ? Hex(0xf4f4f5) : Well, 11f);
            var trackRing = Ring(track.transform, "Viền", UiTheme.IsLight ? Hex(0xd4d4d8) : HairStrong, 11f);
            var knob = GlassKit.Rect(track.transform, "Núm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8, 0), new Vector2(15, 15));
            var knobFace = Paint(Full(knob, "Mặt"), UiTheme.IsLight ? Hex(0x52525b) : Hex(0xa1a1aa), 8f);
            var lbl = GlassKit.Label(rt, "Nhãn", nhan, 14, Ink, false, TextAnchor.MiddleLeft, new Vector2(46, 0), new Vector2(w - 46, 36));
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow; lbl.raycastTarget = false;
            var tog = rt.gameObject.AddComponent<Toggle>();
            tog.navigation = new Navigation { mode = Navigation.Mode.None };
            tog.transition = Selectable.Transition.None;
            tog.targetGraphic = hit;
            tog.SetIsOnWithoutNotify(batDau);
            if (doi != null) tog.onValueChanged.AddListener(v => doi(v));
            var sw = rt.gameObject.AddComponent<CongTacNho3D>();
            sw.toggle = tog; sw.track = track; sw.trackRing = trackRing; sw.knob = knob; sw.knobFace = knobFace;
            var hv = track.gameObject.AddComponent<MenuHover>();
            hv.hoverScale = 1.04f;
            hv.pressScale = 0.92f;
            return tog;
        }

        public static string CongThucChiSo(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= '0' && c <= '9' ? (char)('₀' + (c - '0')) : c);
            return sb.ToString();
        }

        // ---- Biểu tượng vẽ SDF 36×36 (sắc nét cả macOS lẫn Windows, không phụ thuộc phông ký hiệu)
        static Sprite sIconReset, sIconFull, sIconRuler, sIconAngle;
        static Sprite IconReset() => sIconReset != null ? sIconReset : sIconReset = TaoIcon(px =>
        {
            const int S = 36;
            for (int a = 35; a <= 315; a += 6)
            {
                float r1 = a * Mathf.Deg2Rad, r2 = (a + 6) * Mathf.Deg2Rad;
                VeDoanAlpha(px, S, new Vector2(18 + 10.5f * Mathf.Cos(r1), 18 + 10.5f * Mathf.Sin(r1)),
                    new Vector2(18 + 10.5f * Mathf.Cos(r2), 18 + 10.5f * Mathf.Sin(r2)), 2.4f);
            }
            VeDoanAlpha(px, S, new Vector2(10.5f, 25.5f), new Vector2(7.5f, 18.5f), 2.4f);
            VeDoanAlpha(px, S, new Vector2(10.5f, 25.5f), new Vector2(17.5f, 24.5f), 2.4f);
        });

        static Sprite IconFullscreen() => sIconFull != null ? sIconFull : sIconFull = TaoIcon(px =>
        {
            const int S = 36;
            // 4 góc mở rộng như biểu tượng SVG của CHEMISTRY
            VeDoanAlpha(px, S, new Vector2(7, 29), new Vector2(14, 29), 2.5f); VeDoanAlpha(px, S, new Vector2(7, 29), new Vector2(7, 22), 2.5f);
            VeDoanAlpha(px, S, new Vector2(29, 29), new Vector2(22, 29), 2.5f); VeDoanAlpha(px, S, new Vector2(29, 29), new Vector2(29, 22), 2.5f);
            VeDoanAlpha(px, S, new Vector2(7, 7), new Vector2(14, 7), 2.5f); VeDoanAlpha(px, S, new Vector2(7, 7), new Vector2(7, 14), 2.5f);
            VeDoanAlpha(px, S, new Vector2(29, 7), new Vector2(22, 7), 2.5f); VeDoanAlpha(px, S, new Vector2(29, 7), new Vector2(29, 14), 2.5f);
        });

        static Sprite IconRuler() => sIconRuler != null ? sIconRuler : sIconRuler = TaoIcon(px =>
        {
            const int S = 36;
            VeDoanAlpha(px, S, new Vector2(8, 8), new Vector2(28, 28), 2.5f);
            VeDoanAlpha(px, S, new Vector2(8, 8), new Vector2(14, 8), 2.2f); VeDoanAlpha(px, S, new Vector2(8, 8), new Vector2(8, 14), 2.2f);
            VeDoanAlpha(px, S, new Vector2(28, 28), new Vector2(22, 28), 2.2f); VeDoanAlpha(px, S, new Vector2(28, 28), new Vector2(28, 22), 2.2f);
        });

        static Sprite IconAngle() => sIconAngle != null ? sIconAngle : sIconAngle = TaoIcon(px =>
        {
            const int S = 36;
            VeDoanAlpha(px, S, new Vector2(8, 9), new Vector2(29, 9), 2.5f);
            VeDoanAlpha(px, S, new Vector2(8, 9), new Vector2(23, 28), 2.5f);
            for (int a = 5; a <= 48; a += 8)
            {
                float r1 = a * Mathf.Deg2Rad, r2 = (a + 8) * Mathf.Deg2Rad;
                VeDoanAlpha(px, S, new Vector2(8 + 11f * Mathf.Cos(r1), 9 + 11f * Mathf.Sin(r1)),
                    new Vector2(8 + 11f * Mathf.Cos(r2), 9 + 11f * Mathf.Sin(r2)), 2.0f);
            }
        });

        static Sprite TaoIcon(Action<Color32[]> ve)
        {
            const int S = 36;
            var px = new Color32[S * S];
            ve(px);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px); tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }

        static void VeDoanAlpha(Color32[] px, int S, Vector2 a, Vector2 b, float day)
        {
            float r = day * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 2)), x1 = Mathf.Min(S - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 2)), y1 = Mathf.Min(S - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 2));
            var ab = b - a; float l2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                    float d = (p - (a + ab * t)).magnitude - r;
                    byte al = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                    int k = y * S + x;
                    if (al > px[k].a) px[k] = new Color32(255, 255, 255, al);
                }
        }

        static void VeDoanAA(Color32[] px, int W, int H, Vector2 a, Vector2 b, float day, Color32 mau)
        {
            float r = day * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 2)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 2)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 2));
            var ab = b - a; float l2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                    float al = Mathf.Clamp01(0.5f - ((p - (a + ab * t)).magnitude - r));
                    if (al <= 0f) continue;
                    int k = y * W + x;
                    var cu = px[k];
                    px[k] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(cu.r, mau.r, al)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(cu.g, mau.g, al)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(cu.b, mau.b, al)), 255);
                }
        }

        static void VeChamAA(Color32[] px, int W, int H, Vector2 c, float r, Color32 mau)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r - 2)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(c.x + r + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r - 2)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(c.y + r + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float al = Mathf.Clamp01(0.5f - (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) - r));
                    if (al <= 0f) continue;
                    int k = y * W + x;
                    var cu = px[k];
                    px[k] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(cu.r, mau.r, al)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(cu.g, mau.g, al)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(cu.b, mau.b, al)), 255);
                }
        }

        void OnDestroy()
        {
            if (tex2D != null) Destroy(tex2D);
        }
    }

    /// <summary>Hiệu ứng công tắc nhỏ (tắt: rãnh sáng + núm xám đậm như ảnh mẫu; bật: rãnh màu nhấn + núm trắng).</summary>
    public class CongTacNho3D : MonoBehaviour
    {
        public Toggle toggle;
        public Image track, trackRing, knobFace;
        public RectTransform knob;
        float k = -1f;

        void Update()
        {
            if (toggle == null) return;
            float target = toggle.isOn ? 1f : 0f;
            if (k >= 0f && Mathf.Abs(k - target) < 0.002f) return;
            k = k < 0f ? target : Mathf.MoveTowards(k, target, Time.unscaledDeltaTime * 7f);
            float c = Mathf.SmoothStep(0f, 1f, k);
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-8f, 8f, c), 0f);
            track.color = Color.Lerp(UiTheme.IsLight ? Hex(0xf4f4f5) : Well, Accent, c);
            trackRing.color = Color.Lerp(UiTheme.IsLight ? Hex(0xd4d4d8) : HairStrong, WithAlpha(Accent, 0f), c);
            knobFace.color = Color.Lerp(UiTheme.IsLight ? Hex(0x52525b) : Hex(0xa1a1aa), Color.white, c);
        }
    }

    /// <summary>Phân biệt bấm chọn nguyên tử (di chuyển &lt; 6 px) với kéo chuột để xoay trong khung 3D.</summary>
    public class TuongTacKhung3D : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<Vector2> KhiBam;
        Vector2 diemXuong;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) diemXuong = e.position;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Vector2.Distance(e.position, diemXuong) < 6f)
                KhiBam?.Invoke(e.position);
        }
    }
}
