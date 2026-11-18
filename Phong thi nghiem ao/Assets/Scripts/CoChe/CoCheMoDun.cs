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

        // ------------------------------------------------------------------ dựng giao diện
        void Dung()
        {
            var camGo = new GameObject("Camera cơ chế");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = GlassKit.PageBg;
            Cam.cullingMask = 1 << TrinhPhat3D.LopVe;
            Cam.fieldOfView = 32f;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 300f;
            Cam.depth = 95;

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

            var tl = new Vector2(0, 1);
            // cột trái: danh sách bài
            var trai = GlassKit.Fill(goc, "Cột bài", new Vector2(0, 0), new Vector2(392, 1080), GlassKit.Surface, tl, true).rectTransform;
            trai.pivot = new Vector2(0, 1);
            trai.anchorMin = new Vector2(0, 0); trai.anchorMax = new Vector2(0, 1); trai.offsetMin = new Vector2(0, 0); trai.offsetMax = new Vector2(392, 0);
            trai.GetComponent<Image>().raycastTarget = true;
            GlassKit.Header(trai, "CƠ CHẾ · 3D ⇄ GIẤY", "Cơ chế phản ứng", "Phím M để mở/đóng", 336, 26, -22);

            // vùng phải
            var phai = GlassKit.Rect(goc, "Vùng bài", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            phai.anchorMin = Vector2.zero; phai.anchorMax = Vector2.one; phai.offsetMin = new Vector2(412, 0); phai.offsetMax = new Vector2(-20, 0);
            tieuDe = GlassKit.Label(phai, "Tên bài", "Chọn một bài ở cột trái", 24, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(0, -20), new Vector2(1180, 34));
            deBai = GlassKit.Label(phai, "Đề", "", 15, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(0, -58), new Vector2(1180, 44));
            float x = 0, y = -108;
            nut3D = GlassKit.MakeChip(phai, "3D", "3D", new Vector2(x, y), new Vector2(70, 44), () => DatCheDo(false), true); x += 78;
            nutGiay = GlassKit.MakeChip(phai, "Giấy", "Giấy", new Vector2(x, y), new Vector2(80, 44), () => DatCheDo(true)); x += 100;
            nutAnH = GlassKit.MakeChip(phai, "Ẩn H", "Ẩn H", new Vector2(x, y), new Vector2(84, 44), BatAnH); x += 104;
            GlassKit.MakeButton(phai, "Trước", "Bước trước", new Vector2(x, y), new Vector2(120, 44), () => TrinhPhat.BuocTruoc()); x += 128;
            nutPhat = GlassKit.MakeChip(phai, "Phát", "Phát", new Vector2(x, y), new Vector2(96, 44), () => TrinhPhat.PhatDung(), true); x += 104;
            GlassKit.MakeButton(phai, "Sau", "Bước sau", new Vector2(x, y), new Vector2(110, 44), () => TrinhPhat.BuocSau()); x += 130;
            nutNhanh = GlassKit.MakeButton(phai, "Nhánh sai", "Nhánh sai", new Vector2(x, y), new Vector2(120, 44), () => { DatCheDo(false); TrinhPhat.PhatNhanhSai(); }); x += 140;
            GlassKit.MakeButton(phai, "Giải thích", "Giải thích", new Vector2(x, y), new Vector2(120, 44), () => BatGiaiThich(!GiaiThichMo)); x += 128;
            nutHoiAI = GlassKit.MakeButton(phai, "Hỏi AI", "Hỏi AI", new Vector2(x, y), new Vector2(150, 44), () => { BatGiaiThich(true); HoiAI(); });
            var dong = GlassKit.MakeButton(phai, "Đóng", "Đóng", new Vector2(-110, -20), new Vector2(100, 44), Dong, false, 16, new Vector2(1, 1));

            vungNhin = GlassKit.Rect(phai, "Vùng nhìn", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            vungNhin.anchorMin = Vector2.zero; vungNhin.anchorMax = Vector2.one; vungNhin.offsetMin = new Vector2(0, 92); vungNhin.offsetMax = new Vector2(0, -166);

            // tờ giấy
            var giay = GlassKit.Fill(vungNhin, "Tờ giấy", Vector2.zero, Vector2.zero, SoDoGiay.MauGiay, tl, true).rectTransform;
            giay.GetComponent<Image>().sprite = null;              // nền trơn: trùng màu khe che của nét
            giay.pivot = new Vector2(0.5f, 0.5f);                 // đặt pivot TRƯỚC offset (đổi pivot sau sẽ dời khung)
            giay.anchorMin = Vector2.zero; giay.anchorMax = Vector2.one; giay.offsetMin = giay.offsetMax = Vector2.zero;
            lopGiay = giay.gameObject.AddComponent<CanvasGroup>();
            lopGiay.alpha = 0f;
            lopGiay.blocksRaycasts = false;
            var sdGo = new GameObject("Sơ đồ", typeof(RectTransform));
            var sdRt = (RectTransform)sdGo.transform;
            sdRt.SetParent(giay, false);
            sdRt.pivot = new Vector2(0.5f, 0.5f);
            sdRt.anchorMin = Vector2.zero; sdRt.anchorMax = Vector2.one; sdRt.offsetMin = new Vector2(24, 24); sdRt.offsetMax = new Vector2(-24, -24);
            Giay = sdGo.AddComponent<SoDoGiay>();
            Giay.raycastTarget = false;

            chuThich = GlassKit.Label(phai, "Chú thích bước", "", 17, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(0, 80), new Vector2(1200, 64), new Vector2(0, 0));

            // ô giải thích (mặc định đóng)
            giaiThich = GlassKit.Fill(phai, "Giải thích", new Vector2(-480, -166), new Vector2(480, 760), GlassKit.Surface, new Vector2(1, 1), true).rectTransform;
            giaiThich.GetComponent<Image>().raycastTarget = true;
            GlassKit.Label(giaiThich, "Tiêu đề", "Giải thích", 20, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(20, -16), new Vector2(440, 28));
            noiDungGiaiThich = GlassKit.Label(giaiThich, "Nội dung", "", 15, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(20, -52), new Vector2(440, 470));
            oCauHoi = GlassKit.MakeInput(giaiThich, "Câu hỏi", "Hỏi về bài này…", new Vector2(20, -536), new Vector2(440, 48), 15);
            GlassKit.MakeButton(giaiThich, "Gửi", "Hỏi AI", new Vector2(20, -592), new Vector2(140, 44), HoiAI, true);
            trangThaiAI = GlassKit.Label(giaiThich, "Trạng thái AI", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -644), new Vector2(440, 100));
            giaiThich.gameObject.SetActive(false);

            // cuối cột trái: giải chuỗi, máy chủ
            GlassKit.MakeButton(trai, "Giải chuỗi", "Giải chuỗi phản ứng", new Vector2(26, 156), new Vector2(336, 48), () => BatBangChuoi(true), false, 16, new Vector2(0, 0));
            nhanMayChu = GlassKit.Label(trai, "Máy chủ", "", 13, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(26, 98), new Vector2(336, 40), new Vector2(0, 0));
            GlassKit.MakeButton(trai, "Cài đặt", "Cài đặt máy chủ", new Vector2(26, 56), new Vector2(336, 44), () => caiDat.gameObject.SetActive(true), false, 15, new Vector2(0, 0));

            DungBangChuoi();
            DungCaiDat();
            DungDanhSach(trai);
        }

        void DungDanhSach(RectTransform trai)
        {
            DanhSachMa.Clear();
            try { DanhSachMa.AddRange(GoiCoChe.DanhSachTrongMay()); }
            catch (System.Exception e) { Debug.LogWarning("[CoChe] Không đọc được thư mục gói: " + e.Message); }
            float y = -130;
            foreach (var ma in DanhSachMa)
            {
                string ten = ma;
                try { var b = Nap(ma); ten = b.Ten; }
                catch (System.Exception e) { ten = ma + " (gói lỗi)"; Debug.LogWarning($"[CoChe] Gói {ma}: {e.Message}"); }
                string m = ma;
                var nut = GlassKit.MakeButton(trai, "Bài " + ma, ten, new Vector2(26, y), new Vector2(336, 70), () => MoBai(m), false, 14);
                nut.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                var t = nut.GetComponentInChildren<Text>().rectTransform;
                t.anchoredPosition = new Vector2(14, 0); t.sizeDelta = new Vector2(308, 70);
                nutBai.Add(nut);
                y -= 78;
            }
            if (DanhSachMa.Count == 0)
                GlassKit.Label(trai, "Trống", "Chưa có gói cơ chế trong StreamingAssets/CoChe.\nChạy: python -m server.ai.co_che.dong_goi --unity",
                               14, GlassKit.Warn, false, TextAnchor.UpperLeft, new Vector2(26, -130), new Vector2(336, 90));
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
            Cam.backgroundColor = GlassKit.PageBg;
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
            TrinhPhat.Nap(b.KichBan);
            Giay.Dat(b.SoDo);
            nutNhanh.gameObject.SetActive(b.KichBan.NhanhSai.Count > 0);
            for (int i = 0; i < nutBai.Count; i++) GlassKit.SetChip(nutBai[i], DanhSachMa[i] == ma);
            TraLoiAICuoi = "";
            trangThaiAI.text = "";
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
            GlassKit.SetChip(nut3D, !sangGiay);
            GlassKit.SetChip(nutGiay, sangGiay);
            const float nua = 0.4f;
            for (float t = 0; t < 2 * nua; t += Time.unscaledDeltaTime)
            {
                float u = t / nua;
                if (sangGiay)
                {
                    TrinhPhat.Phang = Mathf.Lerp(1f, 0.02f, Muot(u));                 // 3D trải phẳng
                    lopGiay.alpha = Muot(u - 1f);                                      // rồi tờ giấy hiện lên
                    lopGiay.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, Muot(u - 1f));
                }
                else
                {
                    lopGiay.alpha = 1f - Muot(u);
                    TrinhPhat.Phang = Mathf.Lerp(0.02f, 1f, Muot(u - 1f));
                }
                yield return null;
            }
            TrinhPhat.Phang = sangGiay ? 0.02f : 1f;
            lopGiay.alpha = sangGiay ? 1f : 0f;
            lopGiay.transform.localScale = Vector3.one;
            lopGiay.blocksRaycasts = sangGiay;
            TrinhPhat.NhanDieuKhien = !sangGiay;
            CheDoGiay = sangGiay;
            DangChuyen = false;
        }

        static float Muot(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void BatAnH()
        {
            TrinhPhat.AnH = !TrinhPhat.AnH;
            GlassKit.SetChip(nutAnH, TrinhPhat.AnH);
        }

        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData camData;

        void Update()
        {
            // camera lớp phủ vẽ giao diện + mô hình: không hậu kỳ (HardwareProfile bật hậu kỳ cho mọi camera Base,
            // tonemapping làm giấy trắng thành xám)
            if (camData == null && Cam != null) camData = Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null && camData.renderPostProcessing) camData.renderPostProcessing = false;
            if (vungNhin == null) return;
            var g = new Vector3[4];
            vungNhin.GetWorldCorners(g);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(Cam, g[0]), b = RectTransformUtility.WorldToScreenPoint(Cam, g[2]);
            float w = Mathf.Max(1, Cam.pixelWidth), h = Mathf.Max(1, Cam.pixelHeight);
            var vung = Rect.MinMaxRect(a.x / w, a.y / h, b.x / w, b.y / h);
            if (GiaiThichMo) vung.xMax = Mathf.Max(vung.xMin + 0.2f, vung.xMax - 500f / 1920f);
            TrinhPhat.VungNhin = vung;
            var ten = nutPhat.GetComponentInChildren<Text>();
            string can = TrinhPhat.DangPhat ? "Dừng" : "Phát";
            if (ten.text != can) ten.text = can;
        }

        void CapNhatChuThich()
        {
            if (chuThich == null || BaiHienTai == null) return;
            var tp = TrinhPhat;
            if (tp.DangNhanhSai && tp.NhanhHienTai != null)
            {
                chuThich.text = $"<b>Nhánh sai: {tp.NhanhHienTai.Ten}</b> — {tp.NhanhHienTai.LyDo}";
                chuThich.color = GlassKit.Bad;
                return;
            }
            chuThich.color = GlassKit.Ink;
            var b = tp.BuocHienTai;
            if (b == null) { chuThich.text = ""; return; }
            string nhan = b.BuocCham ? " · chậm" : "";
            if (b.ThuanNghich) nhan += " · thuận nghịch";
            chuThich.text = $"<b>{tp.ChiSoBuoc + 1}/{tp.SoBuoc} · {b.Ten}{nhan}</b>  {b.GhiChu}";
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
