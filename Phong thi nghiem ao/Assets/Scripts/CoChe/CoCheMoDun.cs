using System.Collections;
using System.Collections.Generic;
using System.Text;
using Khkt.Demo;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.CoChe.JsonNho;

namespace Khkt.CoChe
{
    /// <summary>
    /// Mô-đun "Cơ chế 3D ⇄ Giấy" (hợp đồng chung: Mo(), Dong(), DangMo). Lớp phủ toàn màn hình có camera riêng:
    /// danh sách 8 bài (gói .verapack trong StreamingAssets/CoChe) + bảng "Đề mới" (gõ đề hoặc chọn ảnh đề → POST /co-che/giai-de →
    /// AI viết cơ chế, RDKit kiểm, máy chủ dựng 3D + giấy → mở như một bài), trình phát 3D, sơ đồ giấy, chuyển 3D ⇄ Giấy (0,8 s: hình 3D
    /// trải phẳng rồi tờ giấy hiện lên), ô "Giải thích" (mặc định đóng), nút "Hỏi AI" (/co-che/giai-thich), bảng giải chuỗi
    /// (/co-che/giai-chuoi) và ô cài đặt URL máy chủ (PlayerPrefs). Phím tắt trong lab: M (chưa dùng ở lab; F7–F12, Esc, B, T, H… đã dùng).
    /// Không sửa HandController hay cảnh: tự gắn bằng [RuntimeInitializeOnLoadMethod].
    /// </summary>
    public class CoCheMoDun : MonoBehaviour
    {
        internal const string CheC = "VERA-Lab-18/11";
        public static CoCheMoDun Instance { get; private set; }
        public static bool DangMo => Instance != null && Instance.gameObject.activeSelf;

        public static readonly string[] ChuoiMau =
        {
            "C2H4 -> C2H5OH -> CH3CHO -> CH3COOH -> CH3COOC2H5",
            "CH4 -> CH3Cl -> CH3OH -> HCHO",
            "C3H6 -> CH3CHBrCH3 -> CH3CH(OH)CH3 -> CH3COCH3",
        };

        public Camera Cam { get; private set; }
        public Canvas Canvas { get; private set; }
        public TrinhPhat3D TrinhPhat { get; private set; }
        public SoDoGiay Giay { get; private set; }
        public BaiCoChe BaiHienTai { get; private set; }
        public bool CheDoGiay { get; private set; }
        public bool DangChuyen { get; private set; }
        public bool CoMayChu { get; private set; }
        public string LyDoMayChu { get; private set; } = "chưa kiểm tra";
        public bool GiaiThichMo => giaiThich != null && giaiThich.gameObject.activeSelf;
        public bool BangChuoiMo => bangChuoi != null && bangChuoi.gameObject.activeSelf;
        public readonly List<string> DanhSachMa = new List<string>();
        public readonly Dictionary<string, BaiCoChe> DaNap = new Dictionary<string, BaiCoChe>();
        public string KetQuaChuoiCuoi { get; private set; } = "";
        public int SoTheChuoi { get; private set; }
        public string TraLoiAICuoi { get; private set; } = "";
        public bool DeMoiMo => deMoi != null && deMoi.gameObject.activeSelf;
        public bool DangGiaiDe { get; private set; }
        public string TrangThaiDeCuoi { get; private set; } = "";
        public string TenAnhDe { get; private set; } = "";
        public byte[] AnhDe { get; private set; }
        /// <summary>Các bài giải từ đề mới trong phiên này (mã → bài), mở lại bằng chip "Đề đã giải".</summary>
        public readonly List<BaiCoChe> DeDaGiai = new List<BaiCoChe>();
        public const float ChoGiaiDe = 330f;                    // máy chủ tự dừng ở 300 s
        public const int AnhToiDa = 8 * 1024 * 1024;

        RectTransform goc, vungNhin, giaiThich, bangChuoi, caiDat, deMoi, dsDeDaGiai;
        InputField oDe;
        Text trangThaiDe, nhanAnhDe;
        Button nutPhanTich, nutBoAnh;
        CanvasGroup lopGiay;
        Text tieuDe, deBai, chuThich, noiDungGiaiThich, trangThaiAI, trangThaiChuoi, nhanMayChu;
        Button nut3D, nutGiay, nutAnH, nutPhat, nutNhanh, nutHoiAI;
        InputField oCauHoi, oChuoi, oUrl;
        RectTransform dsTheChuoi;
        readonly List<Button> nutBai = new List<Button>();
        readonly List<Camera> camTat = new List<Camera>();
        bool daTangUiCapture;

        // ------------------------------------------------------------------ hợp đồng chung
        public static void Mo()
        {
            if (MoHinh3DMoDun.DangMo) MoHinh3DMoDun.Dong();
            if (Instance == null) Tao();
            if (Instance.gameObject.activeSelf) return;
            Instance.gameObject.SetActive(true);
            Instance.KhiMo();
        }

        public static void Dong()
        {
            if (Instance == null || !Instance.gameObject.activeSelf) return;
            Instance.KhiDong();
            Instance.gameObject.SetActive(false);
        }

        public static void BatTat() { if (DangMo) Dong(); else Mo(); }

        /// <summary>Mở thẳng bảng "Đề mới" (gõ đề / chọn ảnh đề) – dùng được từ màn Giải bài tập.</summary>
        public static void MoDeMoi()
        {
            Mo();
            Instance?.BatDeMoi(true);
        }

        /// <summary>Mở thẳng bảng giải chuỗi (công cụ "Chuỗi phản ứng" của màn Giải bài tập).</summary>
        public static void MoGiaiChuoi()
        {
            Mo();
            Instance?.BatBangChuoi(true);
        }

        /// <summary>Tạo mô-đun (chưa mở lớp phủ) để đọc danh sách / nạp bài mẫu từ trang Giải bài tập.</summary>
        public static CoCheMoDun DamBaoCo()
        {
            if (Instance == null) Tao();
            return Instance;
        }

        /// <summary>Mở lớp phủ với một bài; tab bảng bên: 0 Các bước, 1 Giải thích, 2 Hỏi đáp (-1 giữ nguyên); buoc ≥ 0 thì nhảy tới bước đó.</summary>
        public static void MoVoiBai(BaiCoChe b, int tab = -1, int buoc = -1)
        {
            if (b == null) return;
            DamBaoCo();
            Mo();
            Instance.DaNap[b.Ma] = b;
            if (Instance.BaiHienTai != b) Instance.MoBai(b);
            if (buoc >= 0 && buoc < Instance.TrinhPhat.SoBuoc) { Instance.TrinhPhat.DenBuoc(buoc); if (Instance.CheDoGiay) Instance.DatCheDo(false); }
            else if (!Instance.CheDoGiay) Instance.DatCheDo(true);          // bài mới: hiện bản giấy trước, bấm "Xem 3D" mới bật khung 3D
            if (tab >= 0) Instance.MoBangBen(tab);
        }

        public static void MoMau(string ma)
        {
            DamBaoCo();
            BaiCoChe b;
            try { b = Instance.Nap(ma); } catch (System.Exception e) { Debug.LogWarning("[CoChe] " + e.Message); return; }
            MoVoiBai(b);
        }

        public void GhiDeDaGiai(BaiCoChe b)
        {
            DaNap[b.Ma] = b;
            DeDaGiai.RemoveAll(x => x.Ma == b.Ma);
            DeDaGiai.Add(b);
            VeDeDaGiai();
        }

        static void Tao()
        {
            var go = new GameObject("Cơ chế 3D ⇄ Giấy");
            DontDestroyOnLoad(go);
            go.SetActive(false);
            Instance = go.AddComponent<CoCheMoDun>();
            Instance.Dung();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CaiPhimTat()
        {
            var go = new GameObject("Phím tắt cơ chế (M)");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<PhimTatCoChe>();
        }

        // ------------------------------------------------------------------ dựng giao diện (18/11: theo bản demo đã duyệt)
        // Bố cục giống nguyen_mau/co_che_3d/sn2_sn1.html: phân tử chiếm cả màn nền xanh đậm, đầu trang góc trái (tên bài, đề),
        // nút gạt 3D | Giấy góc phải, khung bước bằng kính ở giữa đáy (nhãn, tên bước, mô tả, thanh tiến trình chia đoạn, nút phát).
        // Chế độ Giấy: cả trang màu giấy, tờ đáp án ở giữa (phông có chân), nút "Giải thích từng bước".
        public static readonly Color NenToi = new Color32(0x0b, 0x10, 0x20, 0xff);
        public static readonly Color NenGiay = new Color32(0xf6, 0xf1, 0xe6, 0xff);
        static readonly Color Kinh = new Color32(18, 24, 44, 205);
        static readonly Color VienKinh = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color ChuSang = new Color32(0xe8, 0xec, 0xf5, 0xff);
        static readonly Color ChuPhu = new Color32(0x9a, 0xa5, 0xbd, 0xff);
        static readonly Color XanhNhan = new Color32(0x3b, 0x82, 0xf6, 0xff);
        static readonly Color Muc = new Color32(0x1b, 0x26, 0x21, 0xff);

        CanvasGroup dauTrang, khungBuoc, lopToGiay;
        RectTransform truc, dsBai, khungNhan, vachGat;
        Text nhanBuoc, tenBuoc, moTaBuoc, chuToc, tieuDeTo, nhanGat3D, nhanGatGiay;
        Image nenNhanBuoc, nenGat;
        Button nutDong;
        readonly List<Image> doanTruc = new List<Image>();
        readonly List<Text> nhanNguyenTu = new List<Text>();
        static readonly float[] CacToc = { 1f, 2f, 0.5f };
        int chiSoToc;

        void Dung()
        {
            var camGo = new GameObject("Camera cơ chế");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = NenToi;
            Cam.cullingMask = 1 << TrinhPhat3D.LopVe;
            Cam.fieldOfView = 32f;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 300f;
            Cam.depth = 95;
            Khkt.Rendering.CameraKhongHauKy.Gan(Cam);   // giấy trắng phải trắng thật: không hậu kỳ

            var cgo = new GameObject("Canvas cơ chế", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            cgo.layer = TrinhPhat3D.LopVe;
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = Cam;
            Canvas.planeDistance = 1f;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            UiBridge.EnsureInteractive(Canvas);
            goc = (RectTransform)cgo.transform;

            TrinhPhat = gameObject.AddComponent<TrinhPhat3D>();
            TrinhPhat.Cam = Cam;
            TrinhPhat.DoiBuoc += CapNhatChuThich;

            // vùng nhìn 3D: cả màn (phân tử chừa chỗ cho khung bước ở đáy)
            vungNhin = KeoGian(goc, "Vùng nhìn", new Vector2(0, 250), new Vector2(0, -110));
            khungNhan = KeoGian(goc, "Nhãn nguyên tử", Vector2.zero, Vector2.zero);

            // ---- chế độ Giấy: cả trang màu giấy, tờ đáp án ở giữa
            var trangGiay = KeoGian(goc, "Trang giấy", Vector2.zero, Vector2.zero);
            var nenTrang = trangGiay.gameObject.AddComponent<Image>();
            nenTrang.color = NenGiay;
            lopGiay = trangGiay.gameObject.AddComponent<CanvasGroup>();
            lopGiay.alpha = 0f;
            lopGiay.blocksRaycasts = false;
            var to = GlassKit.Rect(trangGiay, "Tờ giấy", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(1500, 800));
            var bong = KeoGian(to, "Bóng tờ", new Vector2(-1, -10), new Vector2(1, 1));      // viền + bóng mỏng dưới tờ
            var bi = bong.gameObject.AddComponent<Image>();
            bi.color = new Color(0.24f, 0.2f, 0.08f, 0.10f);
            var nenTo = KeoGian(to, "Mặt tờ", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            nenTo.color = SoDoGiay.MauGiay;
            tieuDeTo = GlassKit.Label(to, "Tiêu đề tờ", "", 24, Muc, true, TextAnchor.UpperLeft, new Vector2(36, -26), new Vector2(1300, 36));
            tieuDeTo.font = PhongCoChan(true) ?? tieuDeTo.font;
            tieuDeTo.fontStyle = FontStyle.BoldAndItalic;
            var gach = GlassKit.Rect(to, "Gạch tiêu đề", new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -64), new Vector2(360, 2));
            gach.gameObject.AddComponent<Image>().color = new Color32(0xc9, 0xa2, 0x27, 0xff);
            var sdGo = new GameObject("Sơ đồ", typeof(RectTransform));
            var sdRt = (RectTransform)sdGo.transform;
            sdRt.SetParent(to, false);
            sdRt.pivot = new Vector2(0.5f, 0.5f);                 // đặt pivot TRƯỚC offset (đổi pivot sau sẽ dời khung)
            sdRt.anchorMin = Vector2.zero; sdRt.anchorMax = Vector2.one; sdRt.offsetMin = new Vector2(30, 84); sdRt.offsetMax = new Vector2(-30, -78);
            Giay = sdGo.AddComponent<SoDoGiay>();
            Giay.raycastTarget = false;
            Giay.AnTieuDe = true;                                 // 20/11: không tiêu đề gạch lượn sóng trên hình (như đáp án thi)
            NutGiay(to, "Nút Giải thích từng bước", "Giải thích từng bước ▾", new Vector2(36, 24), new Vector2(250, 46), () => BatGiaiThich(!GiaiThichMo));
            lopToGiay = to.gameObject.AddComponent<CanvasGroup>();

            // ---- đầu trang (góc trái): tên bài, đề, gợi ý thao tác, nút chọn bài
            var dau = GlassKit.Rect(goc, "Đầu trang", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(1180, 150));
            dauTrang = dau.gameObject.AddComponent<CanvasGroup>();
            tieuDe = GlassKit.Label(dau, "Tên bài", "Chọn một bài", 27, ChuSang, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(1180, 38));
            deBai = GlassKit.Label(dau, "Đề", "", 16, ChuPhu, false, TextAnchor.UpperLeft, new Vector2(0, -42), new Vector2(1100, 44));
            deBai.horizontalOverflow = HorizontalWrapMode.Wrap;
            GlassKit.Label(dau, "Gợi ý", "Kéo để xoay 360° · cuộn để phóng to · ← → chuyển bước · Space phát/dừng", 15, ChuPhu, false, TextAnchor.UpperLeft, new Vector2(0, -92), new Vector2(1100, 20));
            NutKinh(dau, "Nút Chọn bài", "Bài khác ▾", new Vector2(0, -118), new Vector2(150, 40), () => dsBai.gameObject.SetActive(!dsBai.gameObject.activeSelf), 15);
            NutKinh(dau, "Nút Đề mới", "＋ Đề mới (gõ / ảnh)", new Vector2(160, -118), new Vector2(210, 40), () => { dsBai.gameObject.SetActive(false); BatDeMoi(true); }, 15);

            // ---- nút gạt 3D | Giấy (góc phải) + Đóng
            var gat = GlassKit.Rect(goc, "Nút gạt 3D Giấy", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -22), new Vector2(190, 52));
            nenGat = gat.gameObject.AddComponent<Image>();
            nenGat.sprite = GlassKit.Solid; nenGat.type = Image.Type.Sliced; nenGat.pixelsPerUnitMultiplier = 1f;
            nenGat.color = Kinh;
            vachGat = GlassKit.Rect(gat, "Vạch gạt", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(90, 42));
            var vg = vachGat.gameObject.AddComponent<Image>();
            vg.sprite = GlassKit.Solid; vg.type = Image.Type.Sliced; vg.pixelsPerUnitMultiplier = 1f; vg.color = XanhNhan;
            nut3D = NutTrong(gat, "3D", new Vector2(5, 0), new Vector2(90, 42), () => DatCheDo(false), out nhanGat3D);
            nutGiay = NutTrong(gat, "Giấy", new Vector2(95, 0), new Vector2(90, 42), () => DatCheDo(true), out nhanGatGiay);
            nutDong = NutKinh(goc, "Đóng", "← Quay lại", new Vector2(-24, -26), new Vector2(130, 44), Dong, 15, new Vector2(1, 1));
            // hàng nút dưới nút gạt: xuất ảnh sơ đồ giấy, báo sai, bảng bên (các bước / giải thích / hỏi đáp)
            nutXuat = NutKinh(goc, "Nút Xuất ảnh", "Xuất ảnh PNG", new Vector2(-24, -84), new Vector2(150, 40), () => StartCoroutine(XuatAnhGiay()), 15, new Vector2(1, 1));
            nutBaoSai = NutKinh(goc, "Nút Báo sai", "Báo sai", new Vector2(-182, -84), new Vector2(104, 40), () => { string cd = CheDoGiay ? "giay" : "3d"; if (!CheDoGiay) DatCheDo(true); hopBaoSai.Mo("co_che", BaiHienTai?.Ma, BaiHienTai?.Ten, TrinhPhat.ChiSoBuoc, cd); }, 15, new Vector2(1, 1));
            nutBangBen = NutKinh(goc, "Nút Bảng bên", "Các bước ▸", new Vector2(-294, -84), new Vector2(130, 40), () => BatGiaiThich(!GiaiThichMo), 15, new Vector2(1, 1));

            // ---- khung bước (giữa đáy)
            var kb = GlassKit.Rect(goc, "Khung bước", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(1180, 220));
            khungBuoc = kb.gameObject.AddComponent<CanvasGroup>();
            var nenKb = kb.gameObject.AddComponent<Image>();
            nenKb.sprite = GlassKit.Solid; nenKb.type = Image.Type.Sliced; nenKb.pixelsPerUnitMultiplier = 2f; nenKb.color = Kinh;
            nenKb.raycastTarget = true;
            var tag = GlassKit.Rect(kb, "Nhãn bước", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -18), new Vector2(150, 26));
            nenNhanBuoc = tag.gameObject.AddComponent<Image>();
            nenNhanBuoc.sprite = GlassKit.Solid; nenNhanBuoc.type = Image.Type.Sliced; nenNhanBuoc.pixelsPerUnitMultiplier = 1f;
            nhanBuoc = GlassKit.Label(tag, "Chữ", "", 12, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 26));
            tenBuoc = GlassKit.Label(kb, "Tên bước", "", 20, ChuSang, true, TextAnchor.UpperLeft, new Vector2(184, -16), new Vector2(970, 30));
            moTaBuoc = GlassKit.Label(kb, "Mô tả bước", "", 16, ChuSang, false, TextAnchor.UpperLeft, new Vector2(22, -54), new Vector2(1136, 64));
            moTaBuoc.horizontalOverflow = HorizontalWrapMode.Wrap;
            chuThich = moTaBuoc;
            truc = GlassKit.Rect(kb, "Trục bước", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -128), new Vector2(1136, 8));
            float x = 22, y = -150;
            NutKinh(kb, "Trước", "‹ Trước", new Vector2(x, y), new Vector2(92, 44), () => TrinhPhat.BuocTruoc(), 15); x += 100;
            nutPhat = NutKinh(kb, "Phát", "Phát", new Vector2(x, y), new Vector2(92, 44), () => TrinhPhat.PhatDung(), 15); x += 100;
            NutKinh(kb, "Sau", "Sau ›", new Vector2(x, y), new Vector2(84, 44), () => TrinhPhat.BuocSau(), 15); x += 92;
            var nutToc = NutKinh(kb, "Tốc độ", "1×", new Vector2(x, y), new Vector2(64, 44), DoiToc, 15); x += 72;
            chuToc = nutToc.GetComponentInChildren<Text>();
            nutAnH = NutKinh(kb, "Ẩn H", "Ẩn H", new Vector2(x, y), new Vector2(84, 44), BatAnH, 15); x += 92;
            nutNhanh = NutKinh(kb, "Nhánh sai", "Nhánh sai", new Vector2(x, y), new Vector2(118, 44), () => { DatCheDo(false); TrinhPhat.PhatNhanhSai(); }, 15); x += 126;
            NutKinh(kb, "Giải thích", "Giải thích", new Vector2(x, y), new Vector2(116, 44), () => BatGiaiThich(!GiaiThichMo), 15); x += 124;
            nutHoiAI = NutKinh(kb, "Hỏi AI", "Hỏi AI", new Vector2(x, y), new Vector2(118, 44), () => { BatGiaiThich(true); HoiAI(); }, 15);
            GlassKit.Label(kb, "Chú giải", "<color=#ffd166>●</color> cặp electron   xám đậm C · trắng H · đỏ O · nâu Br", 13, ChuPhu, false, TextAnchor.MiddleRight,
                           new Vector2(-22, -150), new Vector2(330, 44), new Vector2(1, 1)).rectTransform.pivot = new Vector2(1, 1);

            DungBangBen();
            thongBao = GlassKit.Label(goc, "Thông báo", "", 16, ChuSang, false, TextAnchor.MiddleRight, new Vector2(-24, -132), new Vector2(900, 30), new Vector2(1, 1));
            thongBao.rectTransform.pivot = new Vector2(1, 1);
            thongBao.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ---- danh sách bài (bảng nổi, mở bằng "Bài khác ▾")
            dsBai = GlassKit.Rect(goc, "Danh sách bài", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -214), new Vector2(460, 740));
            var nenDs = dsBai.gameObject.AddComponent<Image>();
            nenDs.sprite = GlassKit.Solid; nenDs.type = Image.Type.Sliced; nenDs.pixelsPerUnitMultiplier = 2f; nenDs.color = new Color32(16, 22, 40, 245);
            nenDs.raycastTarget = true;

            DungBangChuoi();
            DungDeMoi();
            DungCaiDat();
            DungDanhSach(dsBai);
            hopBaoSai = Khkt.Menu.HopBaoSai.Tao(goc);
            NutKinh(dsBai, "Giải chuỗi", "Giải chuỗi phản ứng", new Vector2(16, 64), new Vector2(428, 44), () => { dsBai.gameObject.SetActive(false); BatBangChuoi(true); }, 15, new Vector2(0, 0));
            NutKinh(dsBai, "Cài đặt", "Cài đặt máy chủ", new Vector2(16, 14), new Vector2(428, 44), () => caiDat.gameObject.SetActive(true), 15, new Vector2(0, 0));
            dsBai.gameObject.SetActive(false);
            DatMauGat(false);
            toGiay = to;
            nutXem3D = NutGiay(to, "Nút Xem 3D", "▶  Xem 3D", new Vector2(-36, 24), new Vector2(170, 46), () => DatCheDo(false));
            var nx = (RectTransform)nutXem3D.transform;
            nx.anchorMin = nx.anchorMax = new Vector2(1, 0); nx.pivot = new Vector2(1, 0); nx.anchoredPosition = new Vector2(-36, 24);
            nutXem3D.GetComponent<Image>().color = new Color32(0x1b, 0x26, 0x21, 0xff);
            nutXem3D.GetComponentInChildren<Text>().color = new Color32(0xf6, 0xf1, 0xe6, 0xff);
            DungKhung3D();
            // 19/11: mở là thấy tờ giấy; đầu trang 3D cũ, khung bước cũ, nút gạt 3D|Giấy thôi dùng (3D nằm trong khung riêng)
            dauTrang.gameObject.SetActive(false);
            khungBuoc.gameObject.SetActive(false);
            nenGat.gameObject.SetActive(false);
            vungNhin.gameObject.SetActive(false);
            lopGiay.alpha = 1f; lopGiay.blocksRaycasts = true;
            CheDoGiay = true;
            Cam.backgroundColor = NenGiay;
            TrinhPhat.NhanDieuKhien = false;
            nutBaiKhac = NutKinh(goc, "Nút Chọn bài phải", "Bài khác ▾", new Vector2(-170, -26), new Vector2(130, 44), () => dsBai.gameObject.SetActive(!dsBai.gameObject.activeSelf), 15, new Vector2(1, 1));
            DatMauGat(true);
        }

        void DungDanhSach(RectTransform trai)
        {
            DanhSachMa.Clear();
            try { DanhSachMa.AddRange(GoiCoChe.DanhSachTrongMay()); }
            catch (System.Exception e) { Debug.LogWarning("[CoChe] Không đọc được thư mục gói: " + e.Message); }
            var nutDe = NutKinh(trai, "Đề mới", "＋ Giải đề mới (gõ đề hoặc ảnh đề)", new Vector2(16, -14), new Vector2(428, 46),
                                () => { dsBai.gameObject.SetActive(false); BatDeMoi(true); }, 15);
            nutDe.GetComponent<Image>().color = new Color(0.23f, 0.51f, 0.96f, 0.30f);
            GlassKit.Label(trai, "Tiêu đề danh sách", "Bài mẫu", 15, ChuPhu, true, TextAnchor.UpperLeft, new Vector2(18, -72), new Vector2(420, 24));
            float y = -102;
            foreach (var ma in DanhSachMa)
            {
                string ten = ma;
                try { var b = Nap(ma); ten = b.Ten; }
                catch (System.Exception e) { ten = ma + " (gói lỗi)"; Debug.LogWarning($"[CoChe] Gói {ma}: {e.Message}"); }
                string m = ma;
                var nut = NutKinh(trai, "Bài " + ma, ten, new Vector2(16, y), new Vector2(428, 58), () => { dsBai.gameObject.SetActive(false); MoBai(m); }, 14);
                var t = nut.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.anchoredPosition = new Vector2(14, 0); t.rectTransform.sizeDelta = new Vector2(400, 58);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                nutBai.Add(nut);
                y -= 64;
            }
            if (DanhSachMa.Count == 0)
                GlassKit.Label(trai, "Trống", "Chưa có gói cơ chế trong StreamingAssets/CoChe.\nChạy: python -m server.ai.co_che.dong_goi --unity",
                               14, GlassKit.Warn, false, TextAnchor.UpperLeft, new Vector2(18, -102), new Vector2(420, 90));
        }

        // ---- tiện ích dựng nút kiểu kính tối (giống bản demo, không theo giao diện sáng/tối của app)
        static RectTransform KeoGian(Transform cha, string ten, Vector2 offMin, Vector2 offMax)
        {
            var rt = new GameObject(ten, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(cha, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        static Button NutKinh(Transform cha, string ten, string chu, Vector2 pos, Vector2 co, System.Action bam, int coChu, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var rt = GlassKit.Rect(cha, ten, a, a, pos, co);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = GlassKit.Solid; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f;
            img.color = new Color(1f, 1f, 1f, 0.04f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cl = b.colors;
            cl.normalColor = Color.white; cl.highlightedColor = new Color(1f, 1f, 1f, 2.2f); cl.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); cl.selectedColor = Color.white;
            b.colors = cl;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => bam());
            var t = GlassKit.Label(rt, "Chữ", chu, coChu, ChuSang, false, TextAnchor.MiddleCenter, Vector2.zero, co);
            t.raycastTarget = false;
            return b;
        }

        static Button NutTrong(Transform cha, string chu, Vector2 pos, Vector2 co, System.Action bam, out Text nhan)
        {
            var rt = GlassKit.Rect(cha, chu, new Vector2(0, 0.5f), new Vector2(0, 0.5f), pos, co);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.001f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => bam());
            nhan = GlassKit.Label(rt, "Chữ", chu, 16, ChuPhu, true, TextAnchor.MiddleCenter, Vector2.zero, co);
            nhan.raycastTarget = false;
            return b;
        }

        static Button NutGiay(Transform cha, string ten, string chu, Vector2 pos, Vector2 co, System.Action bam)
        {
            var rt = GlassKit.Rect(cha, ten, Vector2.zero, Vector2.zero, pos, co);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = GlassKit.Solid; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f;
            img.color = new Color32(0xef, 0xe9, 0xdb, 0xff);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => bam());
            var t = GlassKit.Label(rt, "Chữ", chu, 15, Muc, true, TextAnchor.MiddleCenter, Vector2.zero, co);
            t.raycastTarget = false;
            return b;
        }

        static Font phongCoChan, phongCoChanDam;
        /// <summary>Phông có chân như tờ đáp án (Times New Roman có sẵn trên Mac và Windows); không có thì dùng phông của app.</summary>
        public static Font PhongCoChan(bool dam)
        {
            if (dam ? phongCoChanDam != null : phongCoChan != null) return dam ? phongCoChanDam : phongCoChan;
            Font f = null;
            try { f = Font.CreateDynamicFontFromOSFont(new[] { "Times New Roman", "Times", "Georgia", "Noto Serif" }, 32); } catch { }
            if (dam) phongCoChanDam = f; else phongCoChan = f;
            return f;
        }

        void DatMauGat(bool giay)
        {
            vachGat.anchoredPosition = new Vector2(giay ? 95 : 5, 0);
            nenGat.color = giay ? new Color32(0xff, 0xfd, 0xf7, 0xeb) : Kinh;
            vachGat.GetComponent<Image>().color = giay ? Muc : XanhNhan;
            nhanGat3D.color = giay ? new Color32(0x5d, 0x68, 0x62, 0xff) : Color.white;
            nhanGatGiay.color = giay ? new Color32(0xf6, 0xf1, 0xe6, 0xff) : ChuPhu;
            foreach (var n in new[] { nutXuat, nutBaoSai, nutBangBen, nutBaiKhac })
                if (n != null)
                {
                    n.GetComponent<Image>().color = giay ? new Color32(0xef, 0xe9, 0xdb, 0xff) : new Color(1f, 1f, 1f, 0.04f);
                    n.GetComponentInChildren<Text>().color = giay ? Muc : ChuSang;
                }
            if (thongBao != null) thongBao.color = giay ? Muc : ChuSang;
            if (nutDong != null)                                   // nút Đóng đọc được trên cả nền tối và nền giấy
            {
                nutDong.GetComponent<Image>().color = giay ? new Color32(0xef, 0xe9, 0xdb, 0xff) : new Color(1f, 1f, 1f, 0.04f);
                nutDong.GetComponentInChildren<Text>().color = giay ? Muc : ChuSang;
            }
        }

        void DoiToc()
        {
            chiSoToc = (chiSoToc + 1) % CacToc.Length;
            TrinhPhat.TocDo = CacToc[chiSoToc];
            chuToc.text = CacToc[chiSoToc] == 0.5f ? "½×" : CacToc[chiSoToc].ToString("0") + "×";
        }

        /// <summary>Thanh tiến trình chia đoạn: mỗi bước một đoạn, đoạn đã qua tô đầy, đoạn đang phát tô theo tiến độ.</summary>
        void DungTruc()
        {
            for (int i = truc.childCount - 1; i >= 0; i--) Destroy(truc.GetChild(i).gameObject);
            doanTruc.Clear();
            int n = Mathf.Max(1, TrinhPhat.SoBuoc);
            float rong = (truc.sizeDelta.x - (n - 1) * 4f) / n;
            for (int i = 0; i < n; i++)
            {
                var d = GlassKit.Rect(truc, "Đoạn " + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * (rong + 4f), 0), new Vector2(rong, 6));
                var nen = d.gameObject.AddComponent<Image>();
                nen.color = new Color(ChuSang.r, ChuSang.g, ChuSang.b, 0.16f);
                nen.raycastTarget = true;
                int idx = i;
                var b = d.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => TrinhPhat.DenBuoc(idx));
                var day = GlassKit.Rect(d, "Đầy", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 6));
                var di = day.gameObject.AddComponent<Image>();
                di.color = XanhNhan;
                di.raycastTarget = false;
                doanTruc.Add(di);
            }
        }

        void CapNhatTruc()
        {
            if (doanTruc.Count == 0) return;
            float rong = ((RectTransform)doanTruc[0].transform.parent).sizeDelta.x;
            for (int i = 0; i < doanTruc.Count; i++)
            {
                float f = i < TrinhPhat.ChiSoBuoc ? 1f : i == TrinhPhat.ChiSoBuoc ? TrinhPhat.T : 0f;
                var rt = doanTruc[i].rectTransform;
                float w = rong * f;
                if (Mathf.Abs(rt.sizeDelta.x - w) > 0.5f) rt.sizeDelta = new Vector2(w, 6);
                var c = TrinhPhat.DangNhanhSai ? (Color)new Color32(0xff, 0x4d, 0x4d, 0xff) : XanhNhan;
                if (doanTruc[i].color != c) doanTruc[i].color = c;
            }
        }

        /// <summary>Nhãn tên nguyên tố (trừ C, H) và điện tích ⊕ ⊖ bám theo nguyên tử trên màn, như bản demo.</summary>
        void CapNhatNhan()
        {
            int dung = 0;
            if (!CheDoGiay && !DangChuyen && BaiHienTai != null)
            {
                foreach (var kv in TrinhPhat.ViTriNguyenTu)
                {
                    string e = TrinhPhat.NguyenTo(kv.Key);
                    int dt = TrinhPhat.DienTichHienTai(kv.Key);
                    bool coChu = true;                                    // 20/11: ghi ký hiệu mọi nguyên tử (cả C, H) cho dễ nhận biết
                    if (TrinhPhat.AnH && e == "H") continue;
                    if (TrinhPhat.CoNguyenTu(kv.Key) < 0.5f) continue;
                    var sp = TrinhPhat.Cam.WorldToScreenPoint(TrinhPhat.TheGioi(kv.Value));
                    if (sp.z <= 0f) continue;
                    var kr = khungNhan.rect;
                    var lp = new Vector2((sp.x / Mathf.Max(1, TrinhPhat.Cam.pixelWidth) - 0.5f) * kr.width, (sp.y / Mathf.Max(1, TrinhPhat.Cam.pixelHeight) - 0.5f) * kr.height);
                    if (Mathf.Abs(lp.x) > kr.width * 0.5f || Mathf.Abs(lp.y) > kr.height * 0.5f) continue;
                    var t = LayNhan(dung++);
                    string chu = coChu ? e : "";
                    if (dt != 0) chu += dt > 0 ? (dt > 1 ? dt + "⊕" : "⊕") : (dt < -1 ? (-dt) + "⊖" : "⊖");
                    if (t.text != chu) t.text = chu;
                    t.rectTransform.anchoredPosition = lp + new Vector2(0, 2);
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                }
            }
            for (int i = dung; i < nhanNguyenTu.Count; i++)
                if (nhanNguyenTu[i].gameObject.activeSelf) nhanNguyenTu[i].gameObject.SetActive(false);
        }

        Text LayNhan(int i)
        {
            while (nhanNguyenTu.Count <= i)
            {
                var t = GlassKit.Label(khungNhan, "Nhãn", "", 15, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(60, 24), new Vector2(0.5f, 0.5f));
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.raycastTarget = false;
                var bong = t.gameObject.AddComponent<Shadow>();
                bong.effectColor = new Color(0, 0, 0, 0.75f);
                bong.effectDistance = new Vector2(1, -1);
                nhanNguyenTu.Add(t);
            }
            return nhanNguyenTu[i];
        }

        public BaiCoChe Nap(string ma)
        {
            if (DaNap.TryGetValue(ma, out var b)) return b;
            b = GoiCoChe.NapTrongMay(ma);
            DaNap[ma] = b;
            return b;
        }

        // ------------------------------------------------------------------ mở / đóng
        void KhiMo()
        {
            camTat.Clear();
            foreach (var c in Camera.allCameras)
                if (c != Cam && !c.transform.IsChildOf(transform) && c.enabled) { c.enabled = false; camTat.Add(c); }   // trả GPU cho lớp phủ; bật lại khi đóng
            if (!daTangUiCapture) { DesktopViewer.UiCapture++; daTangUiCapture = true; }
            Cam.backgroundColor = NenGiay;
            if (BaiHienTai == null && DanhSachMa.Count > 0) MoBai(DanhSachMa[0]);
            else if (BaiHienTai != null && string.IsNullOrEmpty(GiaiThichChiTietCuoi)) taiGiaiThich = StartCoroutine(TaiGiaiThichChiTiet(BaiHienTai));
            StartCoroutine(KiemMayChu());
        }

        void KhiDong()
        {
            foreach (var c in camTat) if (c != null) c.enabled = true;
            camTat.Clear();
            if (daTangUiCapture) { DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1); daTangUiCapture = false; }
            if (TrinhPhat != null) TrinhPhat.DangPhat = false;
        }

        public void MoBai(string ma)
        {
            BaiCoChe b;
            try { b = Nap(ma); }
            catch (System.Exception e) { tieuDe.text = "Không mở được gói " + ma; deBai.text = e.Message; return; }
            MoBai(b);
        }

        /// <summary>Mở một bài đã có trong bộ nhớ (bài mẫu đã giải gói, hoặc bài giải từ đề mới).</summary>
        public void MoBai(BaiCoChe b)
        {
            if (b == null) return;
            string ma = b.Ma;
            BaiHienTai = b;
            tieuDe.text = b.Ten;
            deBai.text = b.De;
            tieuDeTo.text = "Cơ chế " + b.Ten + ":";
            TrinhPhat.Nap(b.KichBan);
            Giay.Dat(b.SoDo);
            Giay.DanhDauBuoc(TrinhPhat.ChiSoBuoc);
            nutNhanh.gameObject.SetActive(b.KichBan.NhanhSai.Count > 0);
            deBai.text = b.De.Length > 220 ? b.De.Substring(0, 217) + "…" : b.De;
            for (int i = 0; i < nutBai.Count; i++)
                nutBai[i].GetComponent<Image>().color = DanhSachMa[i] == ma ? new Color(0.23f, 0.51f, 0.96f, 0.35f) : new Color(1f, 1f, 1f, 0.04f);
            TraLoiAICuoi = "";
            trangThaiAI.text = "";
            hoiDap.Clear();
            VeDongBuoc();
            if (taiGiaiThich != null) StopCoroutine(taiGiaiThich);
            if (gameObject.activeInHierarchy) taiGiaiThich = StartCoroutine(TaiGiaiThichChiTiet(b));
            DungTruc();
            CapNhatGiaiThich();
            CapNhatChuThich();
        }

        // ------------------------------------------------------------------ 3D ⇄ giấy
        public void DatCheDo(bool giay)
        {
            if (giay == CheDoGiay || DangChuyen) return;
            StartCoroutine(Chuyen(giay));
        }

        /// <summary>
        /// 19/11: tờ giấy luôn hiện; "Xem 3D" bật khung 3D nhỏ phía dưới (tờ giấy thu lên trên), "Quay lại bảng giấy" tắt khung.
        /// sangGiay = true: đóng khung 3D (chỉ còn giấy). Khung phóng to được ra toàn màn hình (DatToanMan).
        /// </summary>
        IEnumerator Chuyen(bool sangGiay)
        {
            DangChuyen = true;
            dsBai.gameObject.SetActive(false);
            if (!sangGiay)
            {
                khung3D.gameObject.SetActive(true);
                Cam3D.enabled = true;
                khung3D.SetAsLastSibling();
                if (giaiThich != null) giaiThich.SetAsLastSibling();
                dsBai.SetAsLastSibling();
                DatKhung(ToanMan ? 1f : 0f);
            }
            const float lau = 0.38f;
            for (float t = 0; t < lau; t += Time.unscaledDeltaTime)
            {
                float g = Muot(t / lau);
                if (sangGiay) g = 1f - g;
                DatHienKhung(g);
                yield return null;
            }
            DatHienKhung(sangGiay ? 0f : 1f);
            if (sangGiay)
            {
                khung3D.gameObject.SetActive(false);
                Cam3D.enabled = false;
                ToanMan = false;
            }
            TrinhPhat.Phang = 1f;
            TrinhPhat.NhanDieuKhien = !sangGiay;
            CheDoGiay = sangGiay;
            DangChuyen = false;
        }

        /// <summary>g = 0: chỉ giấy; g = 1: khung 3D hiện đủ, tờ giấy thu nhỏ lên phía trên.</summary>
        void DatHienKhung(float g)
        {
            nhomKhung.alpha = g;
            nhomKhung.blocksRaycasts = g > 0.5f;
            khung3D.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, g);
            toGiay.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, g);
            toGiay.anchoredPosition = Vector2.Lerp(new Vector2(0, -18), new Vector2(0, 178), g);
            if (nutXem3D != null) nutXem3D.gameObject.SetActive(g < 0.5f);
        }

        // ------------------------------------------------------------------ khung 3D nhỏ ⇄ toàn màn hình
        public Camera Cam3D { get; private set; }
        public bool ToanMan { get; private set; }
        public bool Khung3DMo => khung3D != null && khung3D.gameObject.activeSelf && !CheDoGiay;
        /// <summary>Ô xem 3D trên màn (tỉ lệ 0..1 của đích kết xuất).</summary>
        public Rect VungCam3D => vungManHinh;
        RectTransform khung3D, vungXem, dauKhung, thanhKhung, toGiay;
        CanvasGroup nhomKhung;
        Text chuDauKhung, chuTocKhung;
        Slider thanhToc;
        Button nutPhongTo, nutXem3D, nutBaiKhac;
        float fKhung;
        Coroutine chayKhung;
        const float KhungRong = 1040f, KhungCao = 450f;

        void DungKhung3D()
        {
            // camera 3D riêng vẽ vào RenderTexture; khung là RawImage trên canvas giấy (một camera gốc URP, nhãn nguyên tử đè đúng chỗ)
            var c3 = new GameObject("Camera 3D khung");
            c3.transform.SetParent(transform, false);
            Cam3D = c3.AddComponent<Camera>();
            Cam3D.clearFlags = CameraClearFlags.SolidColor;
            Cam3D.backgroundColor = NenToi;
            Cam3D.cullingMask = 1 << TrinhPhat3D.LopVe;
            Cam3D.fieldOfView = 32f; Cam3D.nearClipPlane = 0.05f; Cam3D.farClipPlane = 300f;
            Cam3D.depth = Cam.depth - 1;
            Khkt.Rendering.CameraKhongHauKy.Gan(Cam3D);
            Cam3D.enabled = false;
            TrinhPhat.Cam = Cam3D;
            TrinhPhat.VungNhin = new Rect(0, 0, 1, 1);
            TrinhPhat.ViTriChuot = ChuotTrongVung;
            var gocKhung = goc;

            khung3D = GlassKit.Rect(gocKhung, "Khung 3D", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(KhungRong, KhungCao));
            nhomKhung = khung3D.gameObject.AddComponent<CanvasGroup>();
            var nen = khung3D.gameObject.AddComponent<Image>();
            nen.sprite = GlassKit.Solid; nen.type = Image.Type.Sliced; nen.pixelsPerUnitMultiplier = 2f;
            nen.color = new Color32(0x0b, 0x10, 0x20, 0xff);
            nen.raycastTarget = true;                               // chặn bấm xuyên xuống tờ giấy
            var bong = KeoGian(khung3D, "Bóng", new Vector2(-2, -8), new Vector2(2, 2));
            bong.SetAsFirstSibling();
            var bi = bong.gameObject.AddComponent<Image>();
            bi.sprite = GlassKit.Solid; bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 2f;
            bi.color = new Color(0.1f, 0.08f, 0.02f, 0.22f); bi.raycastTarget = false;

            // đầu khung: bước đang xem (chữ ngắn) + Phóng to / Thu nhỏ + Quay lại bảng giấy
            dauKhung = KeoGian(khung3D, "Đầu khung", Vector2.zero, Vector2.zero);
            dauKhung.anchorMin = new Vector2(0, 1); dauKhung.offsetMin = new Vector2(0, -50); dauKhung.offsetMax = Vector2.zero;
            var nd = dauKhung.gameObject.AddComponent<Image>();
            nd.color = new Color32(18, 24, 44, 255); nd.raycastTarget = true;
            chuDauKhung = GlassKit.Label(dauKhung, "Bước", "", 16, ChuSang, false, TextAnchor.MiddleLeft, new Vector2(18, 0), new Vector2(560, 50));
            chuDauKhung.supportRichText = true;
            var quay = NutKinh(dauKhung, "Nút Quay lại bảng giấy", "Quay lại bảng giấy", new Vector2(-10, -6), new Vector2(190, 38), () => DatCheDo(true), 15, new Vector2(1, 1));
            ((RectTransform)quay.transform).pivot = new Vector2(1, 1);
            nutPhongTo = NutKinh(dauKhung, "Nút Phóng to", "⤢  Phóng to", new Vector2(-208, -6), new Vector2(150, 38), () => DatToanMan(!ToanMan), 15, new Vector2(1, 1));
            ((RectTransform)nutPhongTo.transform).pivot = new Vector2(1, 1);

            vungXem = KeoGian(khung3D, "Vùng xem 3D", new Vector2(0, 72), new Vector2(0, -50));
            vungXem.gameObject.AddComponent<RectMask2D>();
            anh3D = vungXem.gameObject.AddComponent<RawImage>();
            anh3D.raycastTarget = false;
            anh3D.color = Color.white;
            khungNhan.SetParent(vungXem, false);
            khungNhan.SetAsLastSibling();
            khungNhan.anchorMin = Vector2.zero; khungNhan.anchorMax = Vector2.one; khungNhan.offsetMin = khungNhan.offsetMax = Vector2.zero;

            // thanh điều khiển: ‹ Phát › · tiến trình theo bước · tốc độ (kéo liên tục 0,25× – 3×) · Ẩn H
            thanhKhung = KeoGian(khung3D, "Thanh điều khiển 3D", Vector2.zero, Vector2.zero);
            thanhKhung.anchorMax = new Vector2(1, 0); thanhKhung.offsetMin = Vector2.zero; thanhKhung.offsetMax = new Vector2(0, 72);
            var nt = thanhKhung.gameObject.AddComponent<Image>();
            nt.color = new Color32(18, 24, 44, 255); nt.raycastTarget = true;
            float x = 14, y = -14;
            NutKinh(thanhKhung, "Nút Bước trước 3D", "‹", new Vector2(x, y), new Vector2(44, 44), () => TrinhPhat.BuocTruoc(), 20); x += 50;
            nutPhat = NutKinh(thanhKhung, "Nút Phát 3D", "Phát", new Vector2(x, y), new Vector2(84, 44), () => TrinhPhat.PhatDung(), 15); x += 90;
            NutKinh(thanhKhung, "Nút Bước sau 3D", "›", new Vector2(x, y), new Vector2(44, 44), () => TrinhPhat.BuocSau(), 20); x += 62;
            truc = GlassKit.Rect(thanhKhung, "Trục bước 3D", new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -33), new Vector2(100, 8));
            truc.anchorMax = new Vector2(1, 1); truc.offsetMax = new Vector2(-(14 + 96 + 12 + 286 + 8 + 96 + 16), truc.offsetMax.y);
            var tocRt = GlassKit.Rect(thanhKhung, "Tốc độ 3D", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-(14 + 96 + 12), -14), new Vector2(230 + 56, 44));
            GlassKit.Label(tocRt, "Nhãn", "Tốc độ", 14, ChuPhu, false, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(60, 44));
            thanhToc = TaoThanhTruot(tocRt, new Vector2(60, -12), new Vector2(160, 20), 0.25f, 3f, 1f, v => { TrinhPhat.TocDo = v; chuTocKhung.text = v.ToString("0.00") + "×"; });
            chuTocKhung = GlassKit.Label(tocRt, "Giá trị", "1.00×", 15, ChuSang, false, TextAnchor.MiddleRight, new Vector2(226, 0), new Vector2(58, 44));
            nutAnH = NutKinh(thanhKhung, "Nút Ẩn H 3D", "Ẩn H", new Vector2(-14, -14), new Vector2(96, 44), BatAnH, 15, new Vector2(1, 1));
            var nutE = NutKinh(thanhKhung, "Nút Hiện e 3D", "Hiện e", new Vector2(-(14 + 96 + 12 + 286 + 8), -14), new Vector2(96, 44), null, 15, new Vector2(1, 1));
            ((RectTransform)nutE.transform).pivot = new Vector2(1, 1);
            nutE.onClick.AddListener(() => { TrinhPhat.HienE = !TrinhPhat.HienE; nutE.GetComponent<Image>().color = TrinhPhat.HienE ? new Color(0.23f, 0.51f, 0.96f, 0.35f) : new Color(1f, 1f, 1f, 0.04f); });
            ((RectTransform)nutAnH.transform).pivot = new Vector2(1, 1);
            ((RectTransform)tocRt).pivot = new Vector2(1, 1);
            khung3D.gameObject.SetActive(false);
            DatKhung(0f);
        }

        RawImage anh3D;
        RenderTexture rt3D;
        Rect vungManHinh;

        /// <summary>Điểm chuột (pixel màn hình) → toạ độ 0..1 trong ô xem 3D; null nếu ngoài ô hoặc đang có bảng nổi che.</summary>
        Vector2? ChuotTrongVung(Vector2 p)
        {
            if (!Khung3DMo || DangChuyen) return null;
            if (GiaiThichMo && RectTransformUtility.RectangleContainsScreenPoint(giaiThich, p, Cam)) return null;
            if (dsBai.gameObject.activeSelf || (hopBaoSai != null && hopBaoSai.DangMo)) return null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(vungXem, p, Cam, out var lp)) return null;
            var r = vungXem.rect;
            var n = new Vector2((lp.x - r.xMin) / Mathf.Max(1, r.width), (lp.y - r.yMin) / Mathf.Max(1, r.height));
            return n.x >= 0 && n.x <= 1 && n.y >= 0 && n.y <= 1 ? n : (Vector2?)null;
        }

        /// <summary>Thanh trượt uGUI tự dựng (nền, phần đã kéo, núm tròn).</summary>
        public static Slider TaoThanhTruot(Transform cha, Vector2 pos, Vector2 co, float min, float max, float gt, System.Action<float> doi)
        {
            // kiểu thanh trượt máy đo: rãnh mảnh 4 điểm, phần đã kéo màu nhấn, núm TRÒN ĐỀU 18 điểm có viền (không kéo giãn thành ellipse)
            var rt = GlassKit.Rect(cha, "Thanh trượt", new Vector2(0, 1), new Vector2(0, 1), pos, co);
            var bat = rt.gameObject.AddComponent<Image>(); bat.color = new Color(1, 1, 1, 0.001f);       // vùng bấm cao cả thanh
            var nen = GlassKit.Rect(rt, "Rãnh", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(co.x, 4));
            nen.anchorMax = new Vector2(1, 0.5f); nen.sizeDelta = new Vector2(0, 4);
            var ni = nen.gameObject.AddComponent<Image>(); ni.sprite = Khkt.Menu.Dashboard.MenuKit.Round(2f); ni.type = Image.Type.Sliced; ni.color = new Color(0.6f, 0.65f, 0.74f, 0.45f); ni.raycastTarget = false;
            var vungDay = GlassKit.Rect(rt, "Vùng đầy", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(co.x, 4));
            vungDay.anchorMax = new Vector2(1, 0.5f); vungDay.sizeDelta = new Vector2(0, 4);
            var day = KeoGian(vungDay, "Đầy", Vector2.zero, Vector2.zero);
            var di = day.gameObject.AddComponent<Image>(); di.sprite = Khkt.Menu.Dashboard.MenuKit.Round(2f); di.type = Image.Type.Sliced; di.color = XanhNhan; di.raycastTarget = false;
            // Slider đặt neo dọc của núm = 0..1 theo vùng chứa → vùng núm phải CAO 0 (giữa thanh) thì núm mới giữ đúng 20×20, tròn đều
            var vungNum = KeoGian(rt, "Vùng núm", new Vector2(10, co.y * 0.5f), new Vector2(-10, -co.y * 0.5f));
            var num = GlassKit.Rect(vungNum, "Núm", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var bong = GlassKit.Rect(num, "Bóng núm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(22, 22));
            var bi = bong.gameObject.AddComponent<Image>(); bi.sprite = Khkt.Menu.Dashboard.MenuKit.Round(11f); bi.type = Image.Type.Sliced; bi.color = new Color(0, 0, 0, 0.18f); bi.raycastTarget = false;
            var vien = GlassKit.Rect(num, "Viền núm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20)).gameObject.AddComponent<Image>();
            vien.sprite = Khkt.Menu.Dashboard.MenuKit.Round(10f); vien.type = Image.Type.Sliced; vien.color = XanhNhan;
            var mat = GlassKit.Rect(num, "Mặt núm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
            var mi = mat.gameObject.AddComponent<Image>(); mi.sprite = Khkt.Menu.Dashboard.MenuKit.Round(7f); mi.type = Image.Type.Sliced; mi.color = Color.white; mi.raycastTarget = false;
            var sl = rt.gameObject.AddComponent<Slider>();
            sl.fillRect = day; sl.handleRect = num; sl.targetGraphic = vien;
            sl.direction = Slider.Direction.LeftToRight;
            sl.minValue = min; sl.maxValue = max; sl.value = gt;
            sl.navigation = new Navigation { mode = Navigation.Mode.None };
            sl.onValueChanged.AddListener(v => doi(v));
            return sl;
        }

        /// <summary>Đặt tốc độ phát 3D (0,25× – 3×), đồng bộ thanh trượt.</summary>
        public void DatTocDo(float v)
        {
            v = Mathf.Clamp(v, 0.25f, 3f);
            if (thanhToc != null) thanhToc.SetValueWithoutNotify(v);
            TrinhPhat.TocDo = v;
            if (chuTocKhung != null) chuTocKhung.text = v.ToString("0.00") + "×";
        }

        /// <summary>Phóng khung 3D ra toàn màn hình hoặc thu về khung nhỏ (0,3 s). Toàn màn hình chỉ có 3D + thanh điều khiển.</summary>
        public void DatToanMan(bool toanMan)
        {
            if (CheDoGiay) DatCheDo(false);
            ToanMan = toanMan;
            nutPhongTo.GetComponentInChildren<Text>().text = toanMan ? "⤡  Thu nhỏ" : "⤢  Phóng to";
            if (chayKhung != null) StopCoroutine(chayKhung);
            if (!gameObject.activeInHierarchy) { DatKhung(toanMan ? 1f : 0f); return; }
            chayKhung = StartCoroutine(ChayKhung(toanMan ? 1f : 0f));
        }

        IEnumerator ChayKhung(float den)
        {
            float tu = fKhung;
            const float lau = 0.3f;
            for (float t = 0; t < lau; t += Time.unscaledDeltaTime) { DatKhung(Mathf.Lerp(tu, den, Muot(t / lau))); yield return null; }
            DatKhung(den);
            chayKhung = null;
        }

        /// <summary>f = 0: khung nhỏ giữa đáy màn; f = 1: phủ toàn màn hình.</summary>
        void DatKhung(float f)
        {
            fKhung = f;
            var cha = (RectTransform)khung3D.parent;
            float W = cha.rect.width > 1 ? cha.rect.width : 1920f, H = cha.rect.height > 1 ? cha.rect.height : 1080f;
            var nho = Rect.MinMaxRect(W * 0.5f - KhungRong * 0.5f, 18f, W * 0.5f + KhungRong * 0.5f, 18f + KhungCao);
            var to = Rect.MinMaxRect(0, 0, W, H);
            float x0 = Mathf.Lerp(nho.xMin, to.xMin, f), y0 = Mathf.Lerp(nho.yMin, to.yMin, f), x1 = Mathf.Lerp(nho.xMax, to.xMax, f), y1 = Mathf.Lerp(nho.yMax, to.yMax, f);
            khung3D.anchorMin = khung3D.anchorMax = Vector2.zero;
            khung3D.pivot = new Vector2(0.5f, 0.5f);
            khung3D.sizeDelta = new Vector2(x1 - x0, y1 - y0);
            khung3D.anchoredPosition = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        }

        /// <summary>
        /// RenderTexture cỡ bằng đích của camera giấy (màn hình hoặc ảnh kiểm thử); camera 3D chỉ vẽ góc dưới trái bằng đúng
        /// số điểm ảnh của ô xem, RawImage lấy đúng phần đó (uvRect) → nét 1:1, đổi khung nhỏ ⇄ toàn màn không cấp phát lại.
        /// </summary>
        void CapNhatVungCam3D()
        {
            if (Cam3D == null || !Cam3D.enabled || vungXem == null) return;
            int W = Mathf.Max(64, Cam.pixelWidth), H = Mathf.Max(64, Cam.pixelHeight);
            if (rt3D == null || rt3D.width != W || rt3D.height != H)
            {
                if (rt3D != null) { Cam3D.targetTexture = null; rt3D.Release(); Destroy(rt3D); }
                rt3D = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "CoChe3D" };
                rt3D.Create();
                Cam3D.targetTexture = rt3D;
                anh3D.texture = rt3D;
            }
            var g = gocVung;
            vungXem.GetWorldCorners(g);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(Cam, g[0]), b = RectTransformUtility.WorldToScreenPoint(Cam, g[2]);
            vungManHinh = Rect.MinMaxRect(a.x / W, a.y / H, b.x / W, b.y / H);
            float fw = Mathf.Clamp(vungManHinh.width, 0.02f, 1f), fh = Mathf.Clamp(vungManHinh.height, 0.02f, 1f);
            var r = new Rect(0, 0, fw, fh);
            if (Cam3D.rect != r) Cam3D.rect = r;
            anh3D.uvRect = r;
        }

        /// <summary>Kiểm thử/chụp ảnh: đặt đích kết xuất cho cả 3 camera (giấy, 3D, giao diện khung).</summary>
        public void DatDich(RenderTexture rt)
        {
            Cam.targetTexture = rt;
        }

        /// <summary>Kiểm thử/chụp ảnh: vẽ lần lượt giấy → 3D → khung (như màn hình thật).</summary>
        public void VeTatCa()
        {
            CapNhatVungCam3D();
            if (Cam3D != null && Cam3D.enabled) Cam3D.Render();
            CapNhatNhan();
            Canvas.ForceUpdateCanvases();
            Cam.Render();
        }

        static float Muot(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void BatAnH()
        {
            TrinhPhat.AnH = !TrinhPhat.AnH;
            nutAnH.GetComponent<Image>().color = TrinhPhat.AnH ? new Color(0.23f, 0.51f, 0.96f, 0.35f) : new Color(1f, 1f, 1f, 0.04f);
        }

        readonly Vector3[] gocVung = new Vector3[4];

        void Update()
        {
            if (nutPhat == null) return;
            // phím tắt (khi không gõ chữ): V = Xem 3D ⇄ bảng giấy, F = phóng to ⇄ thu nhỏ khung 3D
            var kb = Keyboard.current;
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool dangGo = sel != null && sel.GetComponent<InputField>() is InputField f && f.isFocused && f.gameObject.activeInHierarchy;
            if (kb != null && !dangGo && !DangChuyen && (hopBaoSai == null || !hopBaoSai.DangMo))
            {
                if (kb.vKey.wasPressedThisFrame) DatCheDo(!CheDoGiay);
                else if (kb.fKey.wasPressedThisFrame) DatToanMan(!ToanMan);
            }
            var ten = nutPhat.GetComponentInChildren<Text>();
            string can = TrinhPhat.DangPhat ? "Dừng" : "Phát";
            if (ten.text != can) ten.text = can;
            CapNhatTruc();
            if (chuDauKhung != null && BaiHienTai != null)
            {
                var tp = TrinhPhat;
                string chu = tp.DangNhanhSai && tp.NhanhHienTai != null ? "<color=#ff6b6b>Nhánh sai</color>  " + tp.NhanhHienTai.Ten
                           : tp.BuocHienTai != null ? $"<b>Bước {tp.ChiSoBuoc + 1}/{tp.SoBuoc}</b>  {tp.BuocHienTai.Ten}" : "";
                if (chuDauKhung.text != chu) chuDauKhung.text = chu;
            }
        }

        void LateUpdate() { CapNhatVungCam3D(); CapNhatNhan(); }     // sau khi TrinhPhat3D đã tính vị trí nguyên tử của khung này

        void CapNhatChuThich()
        {
            if (chuThich == null || BaiHienTai == null) return;
            var tp = TrinhPhat;
            Giay.DanhDauBuoc(tp.ChiSoBuoc);                       // tô khối giai đoạn đang xem trên tờ giấy (như .buoc.dang-xem của demo)
            ToDongBuoc();
            if (tp.DangNhanhSai && tp.NhanhHienTai != null)
            {
                DatNhan("KHÔNG KHẢ THI", new Color32(0xdc, 0x26, 0x26, 0xff));
                tenBuoc.text = "Nhánh sai: " + tp.NhanhHienTai.Ten;
                moTaBuoc.text = tp.NhanhHienTai.LyDo;
                return;
            }
            var b = tp.BuocHienTai;
            if (b == null) { tenBuoc.text = moTaBuoc.text = ""; DatNhan("", Color.clear); return; }
            if (b.BuocCham) DatNhan("BƯỚC CHẬM", new Color32(0xc2, 0x7c, 0x0e, 0xff));
            else if (b.ThuanNghich) DatNhan("THUẬN NGHỊCH", new Color32(0x0e, 0x7c, 0x86, 0xff));
            else DatNhan($"BƯỚC {tp.ChiSoBuoc + 1}/{tp.SoBuoc}", new Color(0.23f, 0.51f, 0.96f, 0.55f));
            tenBuoc.text = $"{tp.ChiSoBuoc + 1}. {b.Ten}";
            moTaBuoc.text = b.GhiChu;
        }

        void DatNhan(string chu, Color mau)
        {
            nhanBuoc.text = chu;
            nenNhanBuoc.color = mau;
            nenNhanBuoc.gameObject.SetActive(!string.IsNullOrEmpty(chu));
        }

        // ------------------------------------------------------------------ giải thích + hỏi AI
        public void BatGiaiThich(bool mo)
        {
            giaiThich.gameObject.SetActive(mo);
            nutBangBen.GetComponentInChildren<Text>().text = mo ? "Đóng bảng ◂" : "Các bước ▸";
            if (mo) { giaiThich.SetAsLastSibling(); ChonTabBen(Mathf.Max(0, tabBen.current)); }
        }

        /// <summary>Mở bảng bên ở tab i (0 Các bước, 1 Giải thích chi tiết, 2 Hỏi đáp).</summary>
        public void MoBangBen(int i)
        {
            BatGiaiThich(true);
            tabBen.Select(i, true);
        }

        public int TabBen => tabBen != null ? tabBen.current : -1;
        public int SoDongBuoc => dongBuoc.Count;
        public string GiaiThichChiTietCuoi { get; private set; } = "";
        public string DuongAnhXuatCuoi { get; private set; } = "";

        void CapNhatGiaiThich()
        {
            if (noiDungGiaiThich == null || BaiHienTai == null) return;
            noiDungGiaiThich.text = string.IsNullOrEmpty(GiaiThichChiTietCuoi) ? GiaiThichMau() : GiaiThichChiTietCuoi;
            CoGianChu(noiDungGiaiThich);
            VeHoiDap();
        }

        string GiaiThichMau()
        {
            var sb = new StringBuilder();
            sb.Append(BaiHienTai.KichBan.GiaiThich).Append("\n\n");
            foreach (var (ten, gt) in BaiHienTai.CacBuoc) sb.Append("<b>").Append(ten).Append(":</b> ").Append(gt).Append("\n\n");
            if (BaiHienTai.LoiHayGap.Count > 0) sb.Append("<b>Lỗi hay gặp</b>\n• ").Append(string.Join("\n• ", BaiHienTai.LoiHayGap));
            return sb.ToString();
        }

        static void CoGianChu(Text t)
        {
            var rt = t.rectTransform;
            float h = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(rt.rect.width, 0))) / Mathf.Max(0.01f, t.pixelsPerUnit);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(60, h + 24));
            if (rt.parent is RectTransform nd) nd.sizeDelta = new Vector2(nd.sizeDelta.x, rt.sizeDelta.y + 10);
        }

        // ------------------------------------------------------------------ bảng bên: Các bước / Giải thích / Hỏi đáp
        RectTransform benBuoc, benGiaiThich, benHoiDap, dsDongBuoc;
        Khkt.Menu.Dashboard.MenuTabs tabBen;
        Text chuHoiDap, nguonGiaiThich, thongBao;
        Button nutXuat, nutBaoSai, nutBangBen, nutGuiHoi;
        Khkt.Menu.HopBaoSai hopBaoSai;
        readonly List<Button> dongBuoc = new List<Button>();
        readonly List<(bool ai, string chu)> hoiDap = new List<(bool, string)>();
        Coroutine taiGiaiThich;

        void DungBangBen()
        {
            giaiThich = GlassKit.Rect(goc, "Giải thích", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -140), new Vector2(480, 670));
            var nen = giaiThich.gameObject.AddComponent<Image>();
            nen.sprite = GlassKit.Solid; nen.type = Image.Type.Sliced; nen.pixelsPerUnitMultiplier = 2f;
            nen.color = new Color32(16, 22, 40, 240);
            nen.raycastTarget = true;
            var vien = GlassKit.Rect(giaiThich, "Viền", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            vien.anchorMin = Vector2.zero; vien.anchorMax = Vector2.one; vien.offsetMin = vien.offsetMax = Vector2.zero;
            tabBen = Khkt.Menu.Dashboard.MenuKit.Tabs(giaiThich, "Tab bảng bên", new[] { "Tab Các bước", "Tab Giải thích", "Tab Hỏi đáp" },
                new[] { "Các bước", "Giải thích", "Hỏi đáp" }, new Vector2(20, -18), new Vector2(144, 40), false, 4f, -1, ChonTabBen);
            foreach (var l in tabBen.labels) l.fontStyle = FontStyle.Normal;

            benBuoc = KhungCuon(giaiThich, "Nội dung Các bước", out dsDongBuoc);
            benGiaiThich = KhungCuon(giaiThich, "Nội dung Giải thích", out var ndGt);
            noiDungGiaiThich = GlassKit.Label(ndGt, "Nội dung", "", 16, ChuSang, false, TextAnchor.UpperLeft, new Vector2(4, 0), new Vector2(424, 100));
            noiDungGiaiThich.horizontalOverflow = HorizontalWrapMode.Wrap;
            noiDungGiaiThich.supportRichText = true;
            nguonGiaiThich = GlassKit.Label(giaiThich, "Nguồn giải thích", "", 13, ChuPhu, false, TextAnchor.UpperLeft, new Vector2(20, -642), new Vector2(440, 20));

            benHoiDap = KhungCuon(giaiThich, "Nội dung Hỏi đáp", out var ndHd, 132);
            chuHoiDap = GlassKit.Label(ndHd, "Hội thoại", "", 16, ChuSang, false, TextAnchor.UpperLeft, new Vector2(4, 0), new Vector2(424, 100));
            chuHoiDap.horizontalOverflow = HorizontalWrapMode.Wrap;
            chuHoiDap.supportRichText = true;
            oCauHoi = GlassKit.MakeInput(benHoiDap, "Câu hỏi", "Hỏi về bài này… (Enter để gửi)", new Vector2(0, -470), new Vector2(440, 48), 15);
            oCauHoi.onSubmit.AddListener(_ => HoiAI());
            nutGuiHoi = NutKinh(benHoiDap, "Gửi câu hỏi", "Gửi", new Vector2(0, -526), new Vector2(100, 40), HoiAI, 15);
            NutKinh(benHoiDap, "Xoá hội thoại", "Xoá hội thoại", new Vector2(108, -526), new Vector2(140, 40), () => { hoiDap.Clear(); VeHoiDap(); }, 15);
            trangThaiAI = GlassKit.Label(benHoiDap, "Trạng thái AI", "", 13, ChuPhu, false, TextAnchor.UpperLeft, new Vector2(256, -526), new Vector2(190, 44));
            trangThaiAI.horizontalOverflow = HorizontalWrapMode.Wrap;
            nhanMayChu = GlassKit.Label(giaiThich, "Máy chủ", "", 12, ChuPhu, false, TextAnchor.UpperLeft, new Vector2(20, -642), new Vector2(440, 20));
            giaiThich.gameObject.SetActive(false);
        }

        /// <summary>Vùng cuộn trong bảng bên (dưới thanh tab). chuaDuoi: chừa chỗ dưới cho ô nhập.</summary>
        static RectTransform KhungCuon(RectTransform cha, string ten, out RectTransform noiDung, float chuaDuoi = 36)
        {
            var g = GlassKit.Rect(cha, ten, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -76), new Vector2(440, 560));
            var vp = GlassKit.Rect(g, "Khung nhìn", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(440, 560 - chuaDuoi));
            vp.gameObject.AddComponent<RectMask2D>();
            var bat = vp.gameObject.AddComponent<Image>();
            bat.color = new Color(1, 1, 1, 0.001f);
            noiDung = GlassKit.Rect(vp, "Nội dung", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(440, 100));
            var sr = g.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = noiDung; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 36f;
            g.gameObject.SetActive(false);
            return g;
        }

        void ChonTabBen(int i)
        {
            benBuoc.gameObject.SetActive(i == 0);
            benGiaiThich.gameObject.SetActive(i == 1);
            benHoiDap.gameObject.SetActive(i == 2);
            nguonGiaiThich.gameObject.SetActive(i == 1);
            nhanMayChu.gameObject.SetActive(i == 0);
            if (TrinhPhat != null) TrinhPhat.NhanDieuKhien = i != 2 && !CheDoGiay;
            if (i == 1) CapNhatGiaiThich();
            if (i == 2) { VeHoiDap(); oCauHoi.ActivateInputField(); }
        }

        void VeDongBuoc()
        {
            foreach (var b in dongBuoc) if (b != null) Destroy(b.gameObject);
            dongBuoc.Clear();
            if (BaiHienTai == null) return;
            var ds = BaiHienTai.KichBan.Buoc;
            float y = 0;
            for (int i = 0; i < ds.Count; i++)
            {
                int ii = i;
                var b = NutKinh(dsDongBuoc, "Dòng bước " + (i + 1), "", new Vector2(0, y), new Vector2(436, 76), () => { DatCheDo(false); TrinhPhat.DenBuoc(ii); }, 15);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft; t.supportRichText = true; t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.rectTransform.sizeDelta = new Vector2(408, 70); t.rectTransform.anchoredPosition = new Vector2(14, -3);
                string gc = ds[i].GhiChu ?? "";
                if (gc.Length > 92) gc = gc.Substring(0, 90) + "…";
                t.text = $"<b>{i + 1}. {ds[i].Ten}</b>" + (ds[i].BuocCham ? "  <color=#f5b84b>· chậm</color>" : "") + $"\n<color=#9aa5bd>{gc}</color>";
                dongBuoc.Add(b);
                y -= 82;
            }
            dsDongBuoc.sizeDelta = new Vector2(440, -y);
            ToDongBuoc();
        }

        void ToDongBuoc()
        {
            for (int i = 0; i < dongBuoc.Count; i++)
                dongBuoc[i].GetComponent<Image>().color = i == TrinhPhat.ChiSoBuoc && !TrinhPhat.DangNhanhSai ? new Color(0.23f, 0.51f, 0.96f, 0.32f) : new Color(1f, 1f, 1f, 0.04f);
        }

        void VeHoiDap()
        {
            if (chuHoiDap == null) return;
            var sb = new StringBuilder();
            if (hoiDap.Count == 0) sb.Append("<color=#9aa5bd>Hỏi bất cứ điều gì về bài đang xem, ví dụ:\n• Vì sao bước 1 là bước chậm?\n• Đổi sang dung môi khác thì sao?\n• Sản phẩm phụ là gì?</color>");
            foreach (var (ai, chu) in hoiDap)
                sb.Append(ai ? "<color=#7fb2ff><b>VERA</b></color>\n" : "<color=#ffd166><b>Bạn</b></color>\n").Append(chu).Append("\n\n");
            chuHoiDap.text = sb.ToString();
            CoGianChu(chuHoiDap);
        }

        IEnumerator TaiGiaiThichChiTiet(BaiCoChe b)
        {
            GiaiThichChiTietCuoi = "";
            nguonGiaiThich.text = "Đang tạo giải thích chi tiết trên máy chủ…";
            while (LyDoMayChu == "chưa kiểm tra") yield return null;
            if (!CoMayChu) { nguonGiaiThich.text = "Giải thích soạn sẵn của bài (chưa kết nối máy chủ)."; yield break; }
            var sb = new StringBuilder("{\"ten\":").Append(MayChuCoChe.ChuoiJson(b.Ten)).Append(",\"de\":").Append(MayChuCoChe.ChuoiJson(b.De ?? ""))
                .Append(",\"loai\":").Append(MayChuCoChe.ChuoiJson(b.KichBan.Loai ?? "")).Append(",\"buoc\":[");
            for (int i = 0; i < b.KichBan.Buoc.Count; i++)
                sb.Append(i > 0 ? "," : "").Append("{\"ten\":").Append(MayChuCoChe.ChuoiJson(b.KichBan.Buoc[i].Ten ?? "")).Append(",\"ghi_chu\":").Append(MayChuCoChe.ChuoiJson(b.KichBan.Buoc[i].GhiChu ?? "")).Append('}');
            sb.Append("]}");
            yield return MayChuCoChe.Goi("POST", "/co-che/giai-thich-chi-tiet", sb.ToString(), 160f, (ok, kq) =>
            {
                if (BaiHienTai != b) return;
                if (!ok) { nguonGiaiThich.text = "Giải thích soạn sẵn (máy chủ: " + kq + ")"; return; }
                try
                {
                    var d = D(JsonNho.Doc(kq));
                    var o = new StringBuilder();
                    o.Append("<b>Tổng quan</b>\n").Append(S(d, "tong_quan")).Append("\n\n");
                    foreach (var x in L(Lay(d, "buoc"))) { var bb = D(x); o.Append($"<b>Bước {I(Lay(bb, "so"))} · {S(bb, "tieu_de")}</b>\n{S(bb, "noi_dung")}\n\n"); }
                    var luuY = L(Lay(d, "luu_y"));
                    if (luuY.Count > 0) { o.Append("<b>Lưu ý khi làm bài</b>"); foreach (var x in luuY) o.Append("\n• ").Append(x as string); }
                    GiaiThichChiTietCuoi = o.ToString();
                    nguonGiaiThich.text = S(d, "nguon") == "ai" ? $"AI viết theo các bước đã kiểm chứng ({S(d, "mo_hinh")}, {F(Lay(d, "giay")):0.0} s)" : "Giải thích theo ghi chú đã kiểm chứng (chưa có AI: " + S(d, "ly_do") + ")";
                    if (GiaiThichMo) CapNhatGiaiThich();
                }
                catch (System.Exception e) { nguonGiaiThich.text = "Giải thích lỗi định dạng: " + e.Message; }
            });
        }

        // ------------------------------------------------------------------ xuất ảnh sơ đồ giấy (PNG)
        /// <summary>Chụp tờ giấy (chế độ Giấy) thành PNG gấp đôi độ phân giải màn hình, lưu vào thư mục Ảnh/VERA Lab.</summary>
        public IEnumerator XuatAnhGiay()
        {
            if (BaiHienTai == null) yield break;
            if (!CheDoGiay)
            {
                DatCheDo(true);
                float t0 = Time.realtimeSinceStartup;
                while ((DangChuyen || !CheDoGiay) && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            }
            var to = (RectTransform)lopToGiay.transform;
            bool moBen = GiaiThichMo, moDs = dsBai.gameObject.activeSelf;
            giaiThich.gameObject.SetActive(false); dsBai.gameObject.SetActive(false);
            var nutTo = to.Find("Nút Giải thích từng bước");
            if (nutTo != null) nutTo.gameObject.SetActive(false);
            yield return null;                         // (WaitForEndOfFrame không chạy ở batchmode) – một khung để canvas dựng lại
            var c = new Vector3[4];
            to.GetWorldCorners(c);
            var cam = Cam.targetTexture;
            int W = cam != null ? cam.width : Screen.width, H = cam != null ? cam.height : Screen.height;
            var rt = RenderTexture.GetTemporary(W * 2, H * 2, 24, RenderTextureFormat.ARGB32);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(Cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(Cam, c[2]);
            float kx = 2f * W / Mathf.Max(1, Cam.pixelWidth), ky = 2f * H / Mathf.Max(1, Cam.pixelHeight);
            Cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            Cam.Render();
            Cam.targetTexture = cam;
            var vung = Rect.MinMaxRect(Mathf.Clamp(a.x * kx, 0, W * 2), Mathf.Clamp(a.y * ky, 0, H * 2), Mathf.Clamp(b.x * kx, 0, W * 2), Mathf.Clamp(b.y * ky, 0, H * 2));
            var cu = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Mathf.Max(1, (int)vung.width), Mathf.Max(1, (int)vung.height), TextureFormat.RGB24, false);
            tex.ReadPixels(vung, 0, 0);
            tex.Apply();
            RenderTexture.active = cu;
            RenderTexture.ReleaseTemporary(rt);
            if (nutTo != null) nutTo.gameObject.SetActive(true);
            giaiThich.gameObject.SetActive(moBen); dsBai.gameObject.SetActive(moDs);
            string loi = null;
            try
            {
                string thuMuc = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "VERA Lab");
                if (string.IsNullOrEmpty(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures))) thuMuc = System.IO.Path.Combine(Application.persistentDataPath, "Anh");
                System.IO.Directory.CreateDirectory(thuMuc);
                string ten = "co_che_" + System.Text.RegularExpressions.Regex.Replace(BaiHienTai.Ma ?? "bai", "[^a-zA-Z0-9_]+", "_") + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                DuongAnhXuatCuoi = System.IO.Path.Combine(thuMuc, ten);
                System.IO.File.WriteAllBytes(DuongAnhXuatCuoi, tex.EncodeToPNG());
            }
            catch (System.Exception e) { loi = e.Message; DuongAnhXuatCuoi = ""; }
            Destroy(tex);
            BaoNhanh(loi == null ? "Đã lưu ảnh: " + DuongAnhXuatCuoi : "Không lưu được ảnh: " + loi, loi != null);
        }

        void BaoNhanh(string chu, bool loi)
        {
            thongBao.text = chu;
            thongBao.color = loi ? new Color32(0xf8, 0x71, 0x71, 0xff) : (CheDoGiay ? Muc : ChuSang);
            StopCoroutine(nameof(XoaThongBao));
            StartCoroutine(nameof(XoaThongBao));
        }

        IEnumerator XoaThongBao() { yield return new WaitForSecondsRealtime(6f); thongBao.text = ""; }

        IEnumerator KiemMayChu()
        {
            if (nhanMayChu != null) nhanMayChu.text = "Máy chủ: đang kiểm tra " + MayChuCoChe.Url;
            yield return MayChuCoChe.Goi("GET", "/co-che/suc-khoe", null, 4f, (ok, kq) =>
            {
                CoMayChu = ok;
                LyDoMayChu = ok ? "" : kq;
            });
            nutHoiAI.interactable = CoMayChu;
            nutHoiAI.GetComponentInChildren<Text>().text = CoMayChu ? "Hỏi AI" : "Hỏi AI (tắt)";
            if (nhanMayChu != null) nhanMayChu.text = CoMayChu ? "Máy chủ: đã kết nối " + MayChuCoChe.Url : "Máy chủ: " + LyDoMayChu;
            if (!CoMayChu && trangThaiAI != null) trangThaiAI.text = "Nút Hỏi AI tạm tắt: " + LyDoMayChu + ". Vào \"Cài đặt máy chủ\" để đổi địa chỉ.";
        }

        public void HoiAI()
        {
            if (BaiHienTai == null) return;
            if (!CoMayChu) { trangThaiAI.text = "Chưa kết nối máy chủ cơ chế: " + LyDoMayChu; return; }
            string cau = oCauHoi != null && !string.IsNullOrWhiteSpace(oCauHoi.text) ? oCauHoi.text.Trim() : "Giải thích ngắn gọn cơ chế này cho học sinh.";
            if (oCauHoi != null) oCauHoi.text = "";
            if (!GiaiThichMo || tabBen.current != 2) MoBangBen(2);
            StartCoroutine(GoiAI(cau));
        }

        IEnumerator GoiAI(string cau)
        {
            var b = BaiHienTai;
            var lichSu = new StringBuilder("[");
            for (int i = 0; i < hoiDap.Count; i++)
                lichSu.Append(i > 0 ? "," : "").Append("{\"vai\":\"").Append(hoiDap[i].ai ? "ai" : "hoc_sinh").Append("\",\"noi_dung\":").Append(MayChuCoChe.ChuoiJson(hoiDap[i].chu)).Append('}');
            lichSu.Append(']');
            hoiDap.Add((false, cau));
            VeHoiDap();
            trangThaiAI.text = "Đang hỏi AI…";
            nutGuiHoi.interactable = false;
            var sb = new StringBuilder("{\"cau_hoi\":").Append(MayChuCoChe.ChuoiJson(cau)).Append(",\"ma_bai\":").Append(MayChuCoChe.ChuoiJson(b.Ma ?? ""))
                .Append(",\"ten\":").Append(MayChuCoChe.ChuoiJson(b.Ten ?? "")).Append(",\"de\":").Append(MayChuCoChe.ChuoiJson(b.De ?? "")).Append(",\"buoc\":[");
            for (int i = 0; i < b.KichBan.Buoc.Count; i++)
                sb.Append(i > 0 ? "," : "").Append("{\"ten\":").Append(MayChuCoChe.ChuoiJson(b.KichBan.Buoc[i].Ten ?? "")).Append(",\"ghi_chu\":").Append(MayChuCoChe.ChuoiJson(b.KichBan.Buoc[i].GhiChu ?? "")).Append('}');
            sb.Append("],\"lich_su\":").Append(lichSu).Append('}');
            yield return MayChuCoChe.Goi("POST", "/co-che/hoi-dap", sb.ToString(), 160f, (ok, kq) =>
            {
                nutGuiHoi.interactable = true;
                if (!ok) { trangThaiAI.text = "Không hỏi được: " + kq; return; }
                try
                {
                    var d = D(JsonNho.Doc(kq));
                    TraLoiAICuoi = S(d, "tra_loi");
                    hoiDap.Add((true, TraLoiAICuoi));
                    string ng = S(d, "nguon");
                    trangThaiAI.text = ng == "ai" ? $"{S(d, "mo_hinh")} · {F(Lay(d, "giay")):0.0} s" : ng == "tai_may" ? "AI tại máy (RAG)" : "Theo bài mẫu (chưa có AI)";
                    VeHoiDap();
                }
                catch (System.Exception e) { trangThaiAI.text = "Trả lời lỗi định dạng: " + e.Message; }
            });
        }

        // ------------------------------------------------------------------ bảng giải chuỗi
        void DungBangChuoi()
        {
            var nen = GlassKit.Fill(goc, "Bảng giải chuỗi", Vector2.zero, new Vector2(1180, 820), GlassKit.Surface, new Vector2(0.5f, 0.5f), true).rectTransform;
            nen.pivot = new Vector2(0.5f, 0.5f);
            nen.anchoredPosition = Vector2.zero;
            nen.GetComponent<Image>().raycastTarget = true;
            bangChuoi = nen;
            GlassKit.Header(nen, "CƠ CHẾ · CHUỖI", "Giải chuỗi phản ứng", "Nối các chất bằng ->  ·  bước có cơ chế mẫu thì bấm để xem", 1120, 30, -22);
            oChuoi = GlassKit.MakeInput(nen, "Chuỗi", "Ví dụ: C2H4 -> C2H5OH -> CH3CHO -> CH3COOH", new Vector2(30, -124), new Vector2(860, 48), 16);
            GlassKit.MakeButton(nen, "Giải", "Giải", new Vector2(900, -124), new Vector2(110, 48), () => GiaiChuoi(oChuoi.text), true);
            GlassKit.MakeButton(nen, "Đóng", "Đóng", new Vector2(1020, -124), new Vector2(130, 48), () => BatBangChuoi(false));
            float x = 30;
            foreach (var c in ChuoiMau)
            {
                string cc = c;
                GlassKit.MakeChip(nen, "Mẫu", c, new Vector2(x, -186), new Vector2(370, 40), () => { oChuoi.text = cc; GiaiChuoi(cc); }, false, 12);
                x += 378;
            }
            trangThaiChuoi = GlassKit.Label(nen, "Trạng thái", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(30, -238), new Vector2(1120, 24));
            dsTheChuoi = GlassKit.Rect(nen, "Các bước", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -270), new Vector2(1120, 520));
            nen.gameObject.SetActive(false);
        }

        public void BatBangChuoi(bool mo)
        {
            bangChuoi.gameObject.SetActive(mo);
            if (mo) bangChuoi.SetAsLastSibling();
        }

        public void GiaiChuoi(string chuoi)
        {
            if (string.IsNullOrWhiteSpace(chuoi)) return;
            StartCoroutine(GoiChuoi(chuoi));
        }

        IEnumerator GoiChuoi(string chuoi)
        {
            trangThaiChuoi.text = "Đang giải…";
            for (int i = dsTheChuoi.childCount - 1; i >= 0; i--) Destroy(dsTheChuoi.GetChild(i).gameObject);
            SoTheChuoi = 0;
            yield return MayChuCoChe.Goi("POST", "/co-che/giai-chuoi", "{\"chuoi\":" + MayChuCoChe.ChuoiJson(chuoi) + "}", 120f, (ok, kq) =>
            {
                if (!ok) { trangThaiChuoi.text = "Không giải được: " + kq; return; }
                KetQuaChuoiCuoi = kq;
                try { HienTheChuoi(D(JsonNho.Doc(kq))); }
                catch (System.Exception e) { trangThaiChuoi.text = "Kết quả lỗi định dạng: " + e.Message; }
            });
        }

        void HienTheChuoi(Dictionary<string, object> kq)
        {
            var chat = new Dictionary<string, string>();
            foreach (var o in L(Lay(kq, "chat"))) { var c = D(o); chat[S(c, "ki_hieu")] = S(c, "ten"); }
            float y = 0;
            foreach (var o in L(Lay(kq, "buoc")))
            {
                var b = D(o);
                var the = GlassKit.Fill(dsTheChuoi, "Thẻ", new Vector2(0, y), new Vector2(1120, 76), GlassKit.Well, null, true).rectTransform;
                string tu = S(b, "tu"), den = S(b, "den");
                bool kc = S(b, "kiem_chung") == "da_kiem_chung";
                GlassKit.Label(the, "Bước", $"<b>{tu} → {den}</b>   {S(b, "phan_ung")}" + (chat.TryGetValue(den, out var tn) && tn != "" ? $"  ({tn})" : ""),
                               16, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(16, -10), new Vector2(860, 26));
                GlassKit.Label(the, "Điều kiện", $"{S(b, "dieu_kien")}   ·   {(kc ? "đã kiểm chứng" : "chưa kiểm chứng")}" + (S(b, "loai") != "" ? "   ·   " + S(b, "loai") : ""),
                               14, kc ? GlassKit.Dim : GlassKit.Warn, false, TextAnchor.UpperLeft, new Vector2(16, -42), new Vector2(860, 24));
                string ma = Lay(b, "ma_co_che") as string;
                if (!string.IsNullOrEmpty(ma) && DanhSachMa.Contains(ma))
                {
                    string khop = S(b, "khop_mau") == "dung_mau" ? "Xem cơ chế" : "Cơ chế tương tự";
                    GlassKit.MakeButton(the, "Xem", khop, new Vector2(900, -16), new Vector2(200, 44), () => { BatBangChuoi(false); MoBai(ma); DatCheDo(false); }, true, 15);
                }
                y -= 84;
                SoTheChuoi++;
            }
            trangThaiChuoi.text = (B(kq, "da_kiem_chung") ? "Toàn bộ chuỗi đã kiểm chứng." : "Có phần chưa kiểm chứng (AI đưa ra) – cần giáo viên xem lại.") + $"  {SoTheChuoi} bước.";
        }

        // ------------------------------------------------------------------ đề mới: gõ đề / chọn ảnh đề → máy chủ giải
        public static readonly string[] DeMau =
        {
            "Viết cơ chế phản ứng cộng HBr vào propen, giải thích sản phẩm chính.",
            "Viết cơ chế thủy phân 2-brom-2-metylpropan trong nước (đun nóng).",
            "CC(C)Br + [OH-] -> CC(C)O + [Br-]",
        };

        void DungDeMoi()
        {
            var nen = GlassKit.Fill(goc, "Bảng đề mới", Vector2.zero, new Vector2(1180, 660), GlassKit.Surface, new Vector2(0.5f, 0.5f), true).rectTransform;
            nen.pivot = new Vector2(0.5f, 0.5f);
            nen.anchoredPosition = new Vector2(0, 20);
            nen.GetComponent<Image>().raycastTarget = true;
            deMoi = nen;
            GlassKit.Header(nen, "CƠ CHẾ · ĐỀ MỚI", "Giải đề bài mới",
                            "Gõ đề (chữ hoặc SMILES) hoặc chọn ảnh đề → AI viết cơ chế → RDKit kiểm từng mũi tên → xem 3D ⇄ Giấy", 960, 30, -22);
            GlassKit.MakeButton(nen, "Đóng đề", "Đóng", new Vector2(1030, -26), new Vector2(120, 44), () => BatDeMoi(false));

            oDe = GlassKit.MakeInput(nen, "Đề bài", "Gõ đề bài, ví dụ: Viết cơ chế phản ứng của CH3CH2Br với NaOH loãng…",
                                     new Vector2(30, -124), new Vector2(1120, 160), 17);
            oDe.lineType = InputField.LineType.MultiLineNewline;
            oDe.characterLimit = 4000;
            foreach (var t in new[] { oDe.textComponent, (Text)oDe.placeholder })
            {
                t.alignment = TextAnchor.UpperLeft;
                t.rectTransform.anchoredPosition = new Vector2(14, -10);
                t.rectTransform.sizeDelta = new Vector2(1120 - 28, 160 - 20);
            }

            float y = -300;
            var nutAnh = GlassKit.MakeButton(nen, "Chọn ảnh đề", "Chọn ảnh đề…", new Vector2(30, y), new Vector2(200, 48), ChonAnhDe);
            if (!ChonTepHeThong.HoTro)
            {
                nutAnh.interactable = false;
                nutAnh.GetComponentInChildren<Text>().text = "Ảnh đề (sắp có)";
            }
            nhanAnhDe = GlassKit.Label(nen, "Ảnh đã chọn", "Chưa chọn ảnh (không bắt buộc)", 15, GlassKit.Dim, false, TextAnchor.MiddleLeft,
                                       new Vector2(244, y), new Vector2(560, 48));
            nutBoAnh = GlassKit.MakeButton(nen, "Bỏ ảnh", "Bỏ ảnh", new Vector2(810, y), new Vector2(110, 48), () => DatAnhDe(null, ""));
            nutBoAnh.gameObject.SetActive(false);
            nutPhanTich = GlassKit.MakeButton(nen, "Phân tích", "Phân tích", new Vector2(930, y), new Vector2(220, 48), () => PhanTichDe(), true, 17);

            y = -364;
            GlassKit.Label(nen, "Đề mẫu", "Đề mẫu:", 14, GlassKit.Dim, true, TextAnchor.MiddleLeft, new Vector2(30, y), new Vector2(80, 40));
            float x = 110;
            foreach (var d in DeMau)
            {
                string dd = d;
                var chip = GlassKit.MakeChip(nen, "Đề mẫu", d, new Vector2(x, y), new Vector2(340, 40), () => oDe.text = dd, false, 12);
                chip.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
                x += 348;
            }
            trangThaiDe = GlassKit.Label(nen, "Trạng thái đề", "", 15, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(30, -420), new Vector2(1120, 140));
            trangThaiDe.horizontalOverflow = HorizontalWrapMode.Wrap;
            GlassKit.Label(nen, "Đã giải", "Đề đã giải trong phiên này:", 14, GlassKit.Dim, true, TextAnchor.UpperLeft, new Vector2(30, -572), new Vector2(400, 22));
            dsDeDaGiai = GlassKit.Rect(nen, "Các đề đã giải", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -598), new Vector2(1120, 44));
            nen.gameObject.SetActive(false);
        }

        public void BatDeMoi(bool mo)
        {
            if (deMoi == null) return;
            deMoi.gameObject.SetActive(mo);
            if (TrinhPhat != null) TrinhPhat.NhanDieuKhien = !mo && !CheDoGiay;   // gõ Space/← → trong ô đề không điều khiển phim 3D
            if (mo)
            {
                deMoi.SetAsLastSibling();
                if (oDe != null && !DangGiaiDe) oDe.ActivateInputField();
            }
        }

        /// <summary>Gắn (hoặc bỏ, khi du == null) ảnh đề sẽ gửi kèm. Ảnh PNG/JPG lớn hơn 7 MB được nén lại JPG trước khi gửi.</summary>
        public void DatAnhDe(byte[] du, string ten)
        {
            if (du != null && du.Length > 7 * 1024 * 1024)
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(du)) du = tex.EncodeToJPG(85);
                Destroy(tex);
            }
            AnhDe = du;
            TenAnhDe = du == null ? "" : ten;
            nhanAnhDe.text = du == null ? "Chưa chọn ảnh (không bắt buộc)" : $"Ảnh: {ten} ({du.Length / 1024f:0} KB)";
            nhanAnhDe.color = du == null ? GlassKit.Dim : GlassKit.Ink;
            nutBoAnh.gameObject.SetActive(du != null);
        }

        /// <summary>Nút "Chọn ảnh đề…": hộp chọn tệp của hệ điều hành (ChonTepHeThong).</summary>
        public void ChonAnhDe()
        {
            if (DangGiaiDe) return;
            StartCoroutine(ChonTepHeThong.ChonAnh("Chọn ảnh đề Hoá", (duong, loi) =>
            {
                if (!string.IsNullOrEmpty(loi)) { DatTrangThaiDe(loi, true); return; }
                if (string.IsNullOrEmpty(duong)) return;                          // bấm Hủy
                var (du, loiDoc) = ChonTepHeThong.DocTep(duong, AnhToiDa);
                if (du == null) { DatTrangThaiDe(loiDoc, true); return; }
                DatAnhDe(du, System.IO.Path.GetFileName(duong));
                DatTrangThaiDe("Đã chọn ảnh. Có thể ghi thêm vào ô đề (vd \"chỉ câu 2\") rồi bấm Phân tích.", false);
            }));
        }

        void DatTrangThaiDe(string chu, bool loi)
        {
            TrangThaiDeCuoi = chu;
            trangThaiDe.text = chu;
            trangThaiDe.color = loi ? GlassKit.Warn : GlassKit.Dim;
        }

        /// <summary>Gửi đề (chữ trong ô và/hoặc ảnh đã chọn) lên máy chủ; xong thì mở bài như 8 bài mẫu.</summary>
        public void PhanTichDe(string vanBan = null)
        {
            if (DangGiaiDe) return;
            string de = (vanBan ?? oDe.text ?? "").Trim();
            if (de.Length == 0 && AnhDe == null) { DatTrangThaiDe("Hãy gõ đề hoặc chọn ảnh đề.", true); return; }
            StartCoroutine(GoiGiaiDe(de, AnhDe));
        }

        IEnumerator GoiGiaiDe(string de, byte[] anh)
        {
            DangGiaiDe = true;
            nutPhanTich.interactable = false;
            nutPhanTich.GetComponentInChildren<Text>().text = "Đang phân tích…";
            float t0 = Time.realtimeSinceStartup;
            string viec = anh != null ? "đọc ảnh đề → AI viết cơ chế → RDKit kiểm (AI tự sửa nếu sai) → dựng 3D và sơ đồ giấy"
                                      : "AI viết cơ chế → RDKit kiểm (AI tự sửa nếu sai) → dựng 3D và sơ đồ giấy";
            bool xong = false, ok = false;
            string kq = "";
            StartCoroutine(MayChuCoChe.GiaiDe(de, anh, "", ChoGiaiDe, (o, k) => { ok = o; kq = k; xong = true; }));
            while (!xong)
            {
                DatTrangThaiDe($"Đang phân tích… {Time.realtimeSinceStartup - t0:0} s\n({viec}; thường 20–120 giây)", false);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            DangGiaiDe = false;
            nutPhanTich.interactable = true;
            nutPhanTich.GetComponentInChildren<Text>().text = "Phân tích";
            if (!ok) { DatTrangThaiDe("Chưa giải được: " + kq, true); yield break; }
            BaiCoChe b;
            string thongTin = "";
            try
            {
                b = BaiCoChe.TuJson(kq);
                if (b.KichBan == null || b.KichBan.Buoc.Count == 0) throw new System.Exception("kết quả không có bước nào");
                var tt = D(Lay(D(JsonNho.Doc(kq)), "thong_tin"));
                int sua = I(Lay(tt, "so_lan_sua"));
                thongTin = $"{S(tt, "mo_hinh")}, {F(Lay(tt, "giay")):0} s" + (sua > 0 ? $", AI đã tự sửa {sua} lần" : "") + (B(tt, "tu_bo_nho_dem") ? ", lấy lại kết quả cũ" : "");
            }
            catch (System.Exception e) { DatTrangThaiDe("Kết quả lỗi định dạng: " + e.Message, true); yield break; }
            DaNap[b.Ma] = b;
            DeDaGiai.RemoveAll(x => x.Ma == b.Ma);
            DeDaGiai.Add(b);
            VeDeDaGiai();
            DatTrangThaiDe($"Xong: {b.Ten} – {b.KichBan.Buoc.Count} bước, đã qua kiểm tra RDKit ({thongTin}).", false);
            BatDeMoi(false);
            MoBai(b);
            DatCheDo(false);
        }

        void VeDeDaGiai()
        {
            for (int i = dsDeDaGiai.childCount - 1; i >= 0; i--) Destroy(dsDeDaGiai.GetChild(i).gameObject);
            float x = 0;
            for (int i = Mathf.Max(0, DeDaGiai.Count - 4); i < DeDaGiai.Count; i++)
            {
                var b = DeDaGiai[i];
                string ten = b.Ten.Length > 34 ? b.Ten.Substring(0, 33) + "…" : b.Ten;
                var chip = GlassKit.MakeChip(dsDeDaGiai, "Đề đã giải", ten, new Vector2(x, 0), new Vector2(272, 40), () => { BatDeMoi(false); MoBai(b); DatCheDo(false); }, false, 12);
                chip.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
                x += 280;
            }
        }

        // ------------------------------------------------------------------ cài đặt máy chủ
        void DungCaiDat()
        {
            var nen = GlassKit.Fill(goc, "Cài đặt máy chủ", Vector2.zero, new Vector2(720, 280), GlassKit.Surface, new Vector2(0.5f, 0.5f), true).rectTransform;
            nen.pivot = new Vector2(0.5f, 0.5f);
            nen.anchoredPosition = Vector2.zero;
            nen.GetComponent<Image>().raycastTarget = true;
            caiDat = nen;
            GlassKit.Header(nen, "CƠ CHẾ · MÁY CHỦ", "Máy chủ cơ chế", "Mặc định " + MayChuCoChe.UrlMacDinh + " (python -m server.ai.co_che.may_chu)", 660, 30, -22);
            oUrl = GlassKit.MakeInput(nen, "URL", MayChuCoChe.UrlMacDinh, new Vector2(30, -124), new Vector2(660, 48), 16);
            oUrl.text = MayChuCoChe.Url;
            GlassKit.MakeButton(nen, "Lưu", "Lưu và kiểm tra", new Vector2(30, -190), new Vector2(220, 48), () => { MayChuCoChe.Url = oUrl.text; StartCoroutine(KiemMayChu()); caiDat.gameObject.SetActive(false); }, true);
            GlassKit.MakeButton(nen, "Đóng", "Đóng", new Vector2(260, -190), new Vector2(120, 48), () => caiDat.gameObject.SetActive(false));
            nen.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            KhiDong();
        }
    }

    /// <summary>Phím M bật/tắt mô-đun cơ chế (bỏ qua khi đang gõ vào ô nhập chữ).</summary>
    public class PhimTatCoChe : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.mKey.wasPressedThisFrame) return;
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && sel.TryGetComponent<InputField>(out var f) && f.isFocused) return;
            CoCheMoDun.BatTat();
        }
    }
}
