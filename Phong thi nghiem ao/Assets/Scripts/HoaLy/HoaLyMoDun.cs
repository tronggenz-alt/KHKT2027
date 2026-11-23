using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Hàng "Hóa lý" của màn Giải bài tập: 4 mô phỏng viết lại từ CHEMISTRY (frontend/hoa-hoc/hoa-ly).
    /// MainMenu gọi Mo(ma) qua reflection theo tên lớp "Khkt.HoaLy.HoaLyMoDun". Mã: khi_ly_tuong, toc_do, can_bang, dong_co_nhiet.
    /// </summary>
    public static class HoaLyMoDun
    {
        public static readonly string[] DanhSachMa = { "khi_ly_tuong", "toc_do", "can_bang", "dong_co_nhiet" };

        public static HoaLyManHinh ManHinh => HoaLyManHinh.Instance;
        public static bool DangMo => HoaLyManHinh.Instance != null && HoaLyManHinh.Instance.gameObject.activeSelf;

        public static void Mo(string ma)
        {
            if (Khkt.CoChe.CoCheMoDun.DangMo) Khkt.CoChe.CoCheMoDun.Dong();
            if (Khkt.CoChe.MoHinh3DMoDun.DangMo) Khkt.CoChe.MoHinh3DMoDun.Dong();
            var mh = HoaLyManHinh.LayHoacTao();
            if (!mh.gameObject.activeSelf)
            {
                mh.gameObject.SetActive(true);
                mh.KhiMo();
            }
            mh.ChonTrang(ma);
        }

        public static void Dong()
        {
            var mh = HoaLyManHinh.Instance;
            if (mh == null || !mh.gameObject.activeSelf) return;
            mh.KhiDong();
            mh.gameObject.SetActive(false);
        }
    }

    /// <summary>Lớp phủ toàn màn của 4 mô phỏng Hóa lý: camera riêng, canvas 1920×1080, thanh tab, cột điều khiển / câu hỏi.</summary>
    public class HoaLyManHinh : MonoBehaviour
    {
        public static HoaLyManHinh Instance { get; private set; }

        public Camera Cam { get; private set; }
        public Canvas Canvas { get; private set; }
        public TrangHoaLy[] Trang { get; private set; }
        public int ChiSoTrang { get; private set; } = -1;
        public TrangHoaLy TrangHienTai => ChiSoTrang >= 0 ? Trang[ChiSoTrang] : null;
        public BangCauHoi CauHoi { get; private set; }
        public bool DangXemCauHoi { get; private set; }

        RectTransform goc, noiDung, theDieuKhien, vungCauHoi, gachTab;
        CanvasGroup nhomGoc;
        Text tieuDe, moTa;
        Button[] tab;
        Button nutDieuKhien, nutCauHoi;
        RectTransform[] traiTrang, phaiTrang;
        CanvasGroup[] nhomTrai, nhomPhai;
        CanvasGroup nhomCauHoi;
        readonly List<Camera> camTat = new List<Camera>();
        bool daTangUiCapture;
        float tMo = 1f, tChuyen = 1f, gachX, gachW;

        public const int LopUi = 5;

        public static HoaLyManHinh LayHoacTao()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Hóa lý (Giải bài tập)");
            DontDestroyOnLoad(go);
            go.SetActive(false);
            Instance = go.AddComponent<HoaLyManHinh>();
            Instance.Dung();
            return Instance;
        }

        void Dung()
        {
            var camGo = new GameObject("Camera Hóa lý");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = GlassKit.PageBg;
            Cam.cullingMask = 1 << LopUi;
            Cam.orthographic = true;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 50f;
            Cam.depth = 97;
            Khkt.Rendering.CameraKhongHauKy.Gan(Cam);

            var cgo = new GameObject("Canvas Hóa lý", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            cgo.layer = LopUi;
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
            nhomGoc = cgo.GetComponent<CanvasGroup>();

            // nội dung dịch nhẹ khi mở
            noiDung = HoaLyUi.Day(goc, "Nội dung");

            // ---- thanh trên ----
            HoaLyUi.Chu(noiDung, "Mã", "HÓA LÝ · MÔ PHỎNG", 13, GlassKit.Accent2, true, TextAnchor.UpperLeft, new Vector2(30, -22), new Vector2(400, 18));
            tieuDe = HoaLyUi.Chu(noiDung, "Tiêu đề", "", 28, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(30, -42), new Vector2(520, 40));
            moTa = HoaLyUi.Chu(noiDung, "Mô tả", "", 15, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(30, -88), new Vector2(1240, 22));

            Trang = new TrangHoaLy[] { new TrangKhiLyTuong(), new TrangTocDo(), new TrangCanBang(), new TrangDongCo() };
            string[] tenTab = { "Khí lý tưởng", "Tốc độ phản ứng", "Cân bằng", "Động cơ nhiệt" };
            tab = new Button[Trang.Length];
            const float tw = 196, tg = 8;
            float x0 = -(Trang.Length * tw + (Trang.Length - 1) * tg) / 2;
            for (int i = 0; i < Trang.Length; i++)
            {
                int ii = i;
                tab[i] = GlassKit.MakeButton(noiDung, "Tab " + Trang[i].Ma, tenTab[i], new Vector2(x0 + i * (tw + tg) + 70, -24), new Vector2(tw, 46),
                    () => ChonTrang(ii), false, 15, new Vector2(0.5f, 1));
            }
            gachTab = GlassKit.Fill(noiDung, "Gạch tab", Vector2.zero, new Vector2(tw, 3), GlassKit.Accent, new Vector2(0.5f, 1)).rectTransform;
            GlassKit.MakeButton(noiDung, "Đóng", "Đóng  ✕", new Vector2(-150, -24), new Vector2(120, 46), HoaLyMoDun.Dong, false, 15, new Vector2(1, 1));

            // ---- vùng trái (hình + đồ thị) và cột phải ----
            var vungTrai = HoaLyUi.Gian(noiDung, "Vùng mô phỏng", Vector2.zero, Vector2.one, new Vector2(28, 28), new Vector2(-648, -122));
            theDieuKhien = HoaLyUi.The(noiDung, "Cột điều khiển", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-628, 28), new Vector2(-28, -122));
            nutDieuKhien = GlassKit.MakeChip(theDieuKhien, "Mục điều khiển", "Điều khiển", new Vector2(20, -16), new Vector2(276, 42), () => DatCheDoCauHoi(false), true, 15);
            nutCauHoi = GlassKit.MakeChip(theDieuKhien, "Mục câu hỏi", "Câu hỏi nhanh", new Vector2(304, -16), new Vector2(276, 42), () => DatCheDoCauHoi(true), false, 15);
            GlassKit.Rule(theDieuKhien, new Vector2(20, -70), 560);

            traiTrang = new RectTransform[Trang.Length];
            phaiTrang = new RectTransform[Trang.Length];
            nhomTrai = new CanvasGroup[Trang.Length];
            nhomPhai = new CanvasGroup[Trang.Length];
            for (int i = 0; i < Trang.Length; i++)
            {
                traiTrang[i] = HoaLyUi.Day(vungTrai, "Trang " + Trang[i].Ma);
                phaiTrang[i] = HoaLyUi.Gian(theDieuKhien, "Điều khiển " + Trang[i].Ma, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -78));
                nhomTrai[i] = traiTrang[i].gameObject.AddComponent<CanvasGroup>();
                nhomPhai[i] = phaiTrang[i].gameObject.AddComponent<CanvasGroup>();
                Trang[i].Gan(traiTrang[i], phaiTrang[i]);
                traiTrang[i].gameObject.SetActive(false);
                phaiTrang[i].gameObject.SetActive(false);
            }
            vungCauHoi = HoaLyUi.Gian(theDieuKhien, "Câu hỏi nhanh", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -78));
            nhomCauHoi = vungCauHoi.gameObject.AddComponent<CanvasGroup>();
            CauHoi = new BangCauHoi(vungCauHoi);
            vungCauHoi.gameObject.SetActive(false);
        }

        public void KhiMo()
        {
            camTat.Clear();
            foreach (var c in Camera.allCameras)
                if (c != Cam && c.enabled) { c.enabled = false; camTat.Add(c); }
            if (!daTangUiCapture) { DesktopViewer.UiCapture++; daTangUiCapture = true; }
            Cam.backgroundColor = GlassKit.PageBg;
            tMo = 0f;
        }

        public void KhiDong()
        {
            foreach (var c in camTat) if (c != null) c.enabled = true;
            camTat.Clear();
            if (daTangUiCapture) { DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1); daTangUiCapture = false; }
        }

        public void ChonTrang(string ma)
        {
            int i = System.Array.IndexOf(HoaLyMoDun.DanhSachMa, ma);
            ChonTrang(i < 0 ? 0 : i);
        }

        public void ChonTrang(int i)
        {
            i = Mathf.Clamp(i, 0, Trang.Length - 1);
            if (i == ChiSoTrang) return;
            if (ChiSoTrang >= 0)
            {
                traiTrang[ChiSoTrang].gameObject.SetActive(false);
                phaiTrang[ChiSoTrang].gameObject.SetActive(false);
            }
            ChiSoTrang = i;
            for (int k = 0; k < tab.Length; k++) GlassKit.SetChip(tab[k], k == i);
            var t = Trang[i];
            tieuDe.text = t.Ten;
            moTa.text = t.MoTa;
            traiTrang[i].gameObject.SetActive(true);
            phaiTrang[i].gameObject.SetActive(!DangXemCauHoi);
            CauHoi.Nap(t.TepCauHoi);
            t.KhiHien();
            tChuyen = 0f;
        }

        public void DatCheDoCauHoi(bool cauHoi)
        {
            DangXemCauHoi = cauHoi;
            GlassKit.SetChip(nutDieuKhien, !cauHoi);
            GlassKit.SetChip(nutCauHoi, cauHoi);
            vungCauHoi.gameObject.SetActive(cauHoi);
            if (ChiSoTrang >= 0) phaiTrang[ChiSoTrang].gameObject.SetActive(!cauHoi);
            tChuyen = Mathf.Min(tChuyen, 0.35f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // mở: mờ dần + nở nhẹ từ tâm
            if (tMo < 1f)
            {
                tMo = Mathf.Min(1f, tMo + dt / 0.26f);
                float e = HoaLyUi.EaseOut(tMo);
                nhomGoc.alpha = e;
                float s = Mathf.Lerp(0.955f, 1f, e);
                noiDung.localScale = new Vector3(s, s, 1f);
                noiDung.anchoredPosition = Vector2.zero;
            }
            // chuyển tab: trang mới phóng mở mượt từ 0,965 lên 1 (không trượt ngang qua lại)
            if (tChuyen < 1f && ChiSoTrang >= 0)
            {
                tChuyen = Mathf.Min(1f, tChuyen + dt / 0.24f);
                float e = HoaLyUi.EaseOut(tChuyen);
                nhomTrai[ChiSoTrang].alpha = e;
                nhomPhai[ChiSoTrang].alpha = e;
                nhomCauHoi.alpha = e;
                float s = Mathf.Lerp(0.968f, 1f, e);
                traiTrang[ChiSoTrang].localScale = new Vector3(s, s, 1f);
                traiTrang[ChiSoTrang].anchoredPosition = Vector2.zero;
            }
            // gạch dưới tab chạy theo tab đang chọn
            if (ChiSoTrang >= 0)
            {
                var r = (RectTransform)tab[ChiSoTrang].transform;
                float dichX = r.anchoredPosition.x, dichW = r.sizeDelta.x;
                gachX = gachW == 0 ? dichX : HoaLyUi.Muot(gachX, dichX, 0.07f, dt);
                gachW = dichW;
                gachTab.anchoredPosition = new Vector2(gachX, -74);
                gachTab.sizeDelta = new Vector2(gachW, 3);
                TrangHienTai.CapNhat(dt);
            }
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) HoaLyMoDun.Dong();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            KhiDong();
        }
    }
}
