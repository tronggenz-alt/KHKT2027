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
    /// danh sách 8 bài (gói .verapack trong StreamingAssets/CoChe), trình phát 3D, sơ đồ giấy, chuyển 3D ⇄ Giấy (0,8 s: hình 3D
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

        RectTransform goc, vungNhin, giaiThich, bangChuoi, caiDat;
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

        /// <summary>Mở thẳng bảng giải chuỗi (công cụ "Chuỗi phản ứng" của màn Giải bài tập).</summary>
        public static void MoGiaiChuoi()
        {
            Mo();
            Instance?.BatBangChuoi(true);
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
            NutGiay(to, "Nút Giải thích từng bước", "Giải thích từng bước ▾", new Vector2(36, 24), new Vector2(250, 46), () => BatGiaiThich(!GiaiThichMo));
            lopToGiay = to.gameObject.AddComponent<CanvasGroup>();

            // ---- đầu trang (góc trái): tên bài, đề, gợi ý thao tác, nút chọn bài
            var dau = GlassKit.Rect(goc, "Đầu trang", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(1180, 150));
            dauTrang = dau.gameObject.AddComponent<CanvasGroup>();
            tieuDe = GlassKit.Label(dau, "Tên bài", "Chọn một bài", 27, ChuSang, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(1180, 38));
            deBai = GlassKit.Label(dau, "Đề", "", 16, ChuPhu, false, TextAnchor.UpperLeft, new Vector2(0, -42), new Vector2(1100, 44));
            deBai.horizontalOverflow = HorizontalWrapMode.Wrap;
            GlassKit.Label(dau, "Gợi ý", "Kéo để xoay 360° · cuộn để phóng to · ← → chuyển bước · Space phát/dừng", 13, ChuPhu, false, TextAnchor.UpperLeft, new Vector2(0, -92), new Vector2(1100, 20));
            NutKinh(dau, "Nút Chọn bài", "Bài khác ▾", new Vector2(0, -118), new Vector2(150, 40), () => dsBai.gameObject.SetActive(!dsBai.gameObject.activeSelf), 14);

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
            nutDong = NutKinh(goc, "Đóng", "Đóng", new Vector2(-24, -26), new Vector2(110, 44), Dong, 15, new Vector2(1, 1));

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

            // ---- ô giải thích (mặc định đóng), nổi bên phải
            giaiThich = GlassKit.Fill(goc, "Giải thích", new Vector2(-500, -100), new Vector2(476, 700), GlassKit.Surface, new Vector2(1, 1), true).rectTransform;
            giaiThich.GetComponent<Image>().raycastTarget = true;
            GlassKit.Label(giaiThich, "Tiêu đề", "Giải thích", 20, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(20, -16), new Vector2(436, 28));
            noiDungGiaiThich = GlassKit.Label(giaiThich, "Nội dung", "", 15, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(20, -52), new Vector2(436, 430));
            noiDungGiaiThich.horizontalOverflow = HorizontalWrapMode.Wrap;
            oCauHoi = GlassKit.MakeInput(giaiThich, "Câu hỏi", "Hỏi về bài này…", new Vector2(20, -496), new Vector2(436, 48), 15);
            GlassKit.MakeButton(giaiThich, "Gửi", "Hỏi AI", new Vector2(20, -552), new Vector2(140, 44), HoiAI, true);
            GlassKit.MakeButton(giaiThich, "Đóng giải thích", "Đóng", new Vector2(170, -552), new Vector2(100, 44), () => BatGiaiThich(false));
            trangThaiAI = GlassKit.Label(giaiThich, "Trạng thái AI", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -604), new Vector2(436, 90));
            trangThaiAI.horizontalOverflow = HorizontalWrapMode.Wrap;
            nhanMayChu = GlassKit.Label(giaiThich, "Máy chủ", "", 12, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -676), new Vector2(436, 20));
            giaiThich.gameObject.SetActive(false);

            // ---- danh sách bài (bảng nổi, mở bằng "Bài khác ▾")
            dsBai = GlassKit.Rect(goc, "Danh sách bài", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -214), new Vector2(460, 680));
            var nenDs = dsBai.gameObject.AddComponent<Image>();
            nenDs.sprite = GlassKit.Solid; nenDs.type = Image.Type.Sliced; nenDs.pixelsPerUnitMultiplier = 2f; nenDs.color = new Color32(16, 22, 40, 245);
            nenDs.raycastTarget = true;

            DungBangChuoi();
            DungCaiDat();
            DungDanhSach(dsBai);
            NutKinh(dsBai, "Giải chuỗi", "Giải chuỗi phản ứng", new Vector2(16, 64), new Vector2(428, 44), () => { dsBai.gameObject.SetActive(false); BatBangChuoi(true); }, 15, new Vector2(0, 0));
            NutKinh(dsBai, "Cài đặt", "Cài đặt máy chủ", new Vector2(16, 14), new Vector2(428, 44), () => caiDat.gameObject.SetActive(true), 15, new Vector2(0, 0));
            dsBai.gameObject.SetActive(false);
            DatMauGat(false);
        }

        void DungDanhSach(RectTransform trai)
        {
            DanhSachMa.Clear();
            try { DanhSachMa.AddRange(GoiCoChe.DanhSachTrongMay()); }
            catch (System.Exception e) { Debug.LogWarning("[CoChe] Không đọc được thư mục gói: " + e.Message); }
            GlassKit.Label(trai, "Tiêu đề danh sách", "8 bài mẫu", 15, ChuPhu, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(420, 24));
            float y = -46;
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
                               14, GlassKit.Warn, false, TextAnchor.UpperLeft, new Vector2(18, -46), new Vector2(420, 90));
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
            var t = GlassKit.Label(rt, "Chữ", chu, coChu, ChuSang, true, TextAnchor.MiddleCenter, Vector2.zero, co);
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
                    bool coChu = e != "C" && e != "H";
                    if (!coChu && dt == 0) continue;
                    if (TrinhPhat.CoNguyenTu(kv.Key) < 0.5f) continue;
                    var sp = Cam.WorldToScreenPoint(TrinhPhat.TheGioi(kv.Value));
                    if (sp.z <= 0f) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(khungNhan, sp, Cam, out var lp)) continue;
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
                if (c != Cam && c.enabled) { c.enabled = false; camTat.Add(c); }   // trả GPU cho lớp phủ; bật lại khi đóng
            if (!daTangUiCapture) { DesktopViewer.UiCapture++; daTangUiCapture = true; }
            Cam.backgroundColor = CheDoGiay ? NenGiay : NenToi;
            if (BaiHienTai == null && DanhSachMa.Count > 0) MoBai(DanhSachMa[0]);
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
            BaiHienTai = b;
            tieuDe.text = b.Ten;
            deBai.text = b.De;
            tieuDeTo.text = "Cơ chế " + b.Ten + ":";
            TrinhPhat.Nap(b.KichBan);
            Giay.Dat(b.SoDo);
            nutNhanh.gameObject.SetActive(b.KichBan.NhanhSai.Count > 0);
            for (int i = 0; i < nutBai.Count; i++)
                nutBai[i].GetComponent<Image>().color = DanhSachMa[i] == ma ? new Color(0.23f, 0.51f, 0.96f, 0.35f) : new Color(1f, 1f, 1f, 0.04f);
            TraLoiAICuoi = "";
            trangThaiAI.text = "";
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

        IEnumerator Chuyen(bool sangGiay)
        {
            DangChuyen = true;
            DatMauGat(sangGiay);
            dsBai.gameObject.SetActive(false);
            const float nua = 0.4f;
            for (float t = 0; t < 2 * nua; t += Time.unscaledDeltaTime)
            {
                float u = t / nua;
                float g;
                if (sangGiay)
                {
                    TrinhPhat.Phang = Mathf.Lerp(1f, 0.02f, Muot(u));                 // 3D trải phẳng
                    g = Muot(u - 1f);                                                   // rồi tờ giấy hiện lên
                }
                else
                {
                    g = 1f - Muot(u);
                    TrinhPhat.Phang = Mathf.Lerp(0.02f, 1f, Muot(u - 1f));
                }
                lopGiay.alpha = g;
                lopToGiay.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, g);
                dauTrang.alpha = khungBuoc.alpha = 1f - g;
                ((RectTransform)khungBuoc.transform).anchoredPosition = new Vector2(0, 20 - 24 * g);
                Cam.backgroundColor = Color.Lerp(NenToi, NenGiay, g);
                yield return null;
            }
            TrinhPhat.Phang = sangGiay ? 0.02f : 1f;
            lopGiay.alpha = sangGiay ? 1f : 0f;
            lopToGiay.transform.localScale = Vector3.one;
            lopGiay.blocksRaycasts = sangGiay;
            dauTrang.alpha = khungBuoc.alpha = sangGiay ? 0f : 1f;
            dauTrang.blocksRaycasts = khungBuoc.blocksRaycasts = !sangGiay;
            ((RectTransform)khungBuoc.transform).anchoredPosition = new Vector2(0, sangGiay ? -4 : 20);
            Cam.backgroundColor = sangGiay ? NenGiay : NenToi;
            TrinhPhat.NhanDieuKhien = !sangGiay;
            CheDoGiay = sangGiay;
            DangChuyen = false;
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
            if (vungNhin == null) return;
            var g = gocVung;                           // dùng lại mảng, không cấp phát mỗi khung
            vungNhin.GetWorldCorners(g);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(Cam, g[0]), b = RectTransformUtility.WorldToScreenPoint(Cam, g[2]);
            float w = Mathf.Max(1, Cam.pixelWidth), h = Mathf.Max(1, Cam.pixelHeight);
            var vung = Rect.MinMaxRect(a.x / w, a.y / h, b.x / w, b.y / h);
            if (GiaiThichMo) vung.xMax = Mathf.Max(vung.xMin + 0.2f, vung.xMax - 500f / 1920f);
            TrinhPhat.VungNhin = vung;
            var ten = nutPhat.GetComponentInChildren<Text>();
            string can = TrinhPhat.DangPhat ? "Dừng" : "Phát";
            if (ten.text != can) ten.text = can;
            CapNhatTruc();
        }

        void LateUpdate() => CapNhatNhan();     // sau khi TrinhPhat3D đã tính vị trí nguyên tử của khung này

        void CapNhatChuThich()
        {
            if (chuThich == null || BaiHienTai == null) return;
            var tp = TrinhPhat;
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
            if (mo) CapNhatGiaiThich();
        }

        void CapNhatGiaiThich()
        {
            if (noiDungGiaiThich == null || BaiHienTai == null) return;
            var sb = new StringBuilder();
            sb.Append(BaiHienTai.KichBan.GiaiThich).Append("\n\n");
            foreach (var (ten, gt) in BaiHienTai.CacBuoc) sb.Append("<b>").Append(ten).Append(":</b> ").Append(gt).Append('\n');
            if (!string.IsNullOrEmpty(TraLoiAICuoi)) sb.Append("\n<b>AI trả lời:</b> ").Append(TraLoiAICuoi);
            noiDungGiaiThich.text = sb.ToString();
        }

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
            string cau = oCauHoi != null && !string.IsNullOrWhiteSpace(oCauHoi.text) ? oCauHoi.text : "Giải thích ngắn gọn cơ chế này cho học sinh.";
            StartCoroutine(GoiAI(cau));
        }

        IEnumerator GoiAI(string cau)
        {
            trangThaiAI.text = "Đang hỏi AI tại máy… (mô hình nhỏ cần 5–15 giây)";
            string than = "{\"cau_hoi\":" + MayChuCoChe.ChuoiJson(cau) + ",\"ma_bai\":" + MayChuCoChe.ChuoiJson(BaiHienTai.Ma) + "}";
            yield return MayChuCoChe.Goi("POST", "/co-che/giai-thich", than, 150f, (ok, kq) =>
            {
                if (!ok) { trangThaiAI.text = "Không hỏi được AI: " + kq; return; }
                try
                {
                    var d = D(JsonNho.Doc(kq));
                    TraLoiAICuoi = S(d, "tra_loi");
                    var nguon = new List<string>();
                    foreach (var o in L(Lay(d, "nguon"))) nguon.Add($"[{I(Lay(D(o), "so"))}] {S(D(o), "tieu_de")}");
                    trangThaiAI.text = $"Nguồn: {string.Join("; ", nguon)}\n({S(d, "nha_cung_cap")} {S(d, "mo_hinh")}, {F(Lay(d, "giay")):0.0} s, {S(d, "kiem_chung")})";
                    CapNhatGiaiThich();
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
