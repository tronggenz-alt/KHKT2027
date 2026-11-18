using System.Collections;
using System.Collections.Generic;
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
    /// Mục "Mô hình 3D" của màn Giải bài tập (viết lại trong Unity từ mục Mô hình 3D của app CHEMISTRY).
    /// Gõ tên / công thức / SMILES → máy chủ cơ chế (POST /co-che/mo-hinh-3d, RDKit + PubChem) trả nguyên tử, liên kết 3D
    /// → vẽ bằng TrinhPhat3D (GPU instancing) trên nền tối như bản demo cơ chế. Kéo để xoay 360°, cuộn để phóng to, Ẩn H.
    /// Hợp đồng chung: Mo(), Dong(), DangMo.
    /// </summary>
    public class MoHinh3DMoDun : MonoBehaviour
    {
        public static MoHinh3DMoDun Instance { get; private set; }
        public static bool DangMo => Instance != null && Instance.gameObject.activeSelf;

        public Camera Cam { get; private set; }
        public TrinhPhat3D TrinhPhat { get; private set; }
        public string TenHienTai { get; private set; } = "";
        public int SoNguyenTu { get; private set; }
        public string LoiCuoi { get; private set; } = "";
        public bool DangTai { get; private set; }

        RectTransform goc, vungNhin, khungNhan;
        InputField oNhap;
        Text tenChat, thongTin, trangThai;
        Button nutAnH;
        readonly List<Camera> camTat = new List<Camera>();
        readonly List<Text> nhan = new List<Text>();
        readonly Vector3[] gocVung = new Vector3[4];
        bool daTangUiCapture;

        static readonly string[] Mau = { "ethanol", "CH3COOH", "benzen", "glucose", "aspirin", "caffeine", "axeton", "anilin" };

        public static void Mo()
        {
            if (CoCheMoDun.DangMo) CoCheMoDun.Dong();
            if (Instance == null)
            {
                var go = new GameObject("Mô hình 3D (Giải bài tập)");
                DontDestroyOnLoad(go);
                go.SetActive(false);
                Instance = go.AddComponent<MoHinh3DMoDun>();
                Instance.Dung();
            }
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

        void Dung()
        {
            var camGo = new GameObject("Camera mô hình 3D");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = CoCheMoDun.NenToi;
            Cam.cullingMask = 1 << TrinhPhat3D.LopVe;
            Cam.fieldOfView = 32f;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 300f;
            Cam.depth = 96;
            Khkt.Rendering.CameraKhongHauKy.Gan(Cam);

            var cgo = new GameObject("Canvas mô hình 3D", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            cgo.layer = TrinhPhat3D.LopVe;
            var cv = cgo.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceCamera;
            cv.worldCamera = Cam;
            cv.planeDistance = 1f;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            UiBridge.EnsureInteractive(cv);
            goc = (RectTransform)cgo.transform;

            TrinhPhat = gameObject.AddComponent<TrinhPhat3D>();
            TrinhPhat.Cam = Cam;

            vungNhin = Gian(goc, "Vùng nhìn", new Vector2(0, 40), new Vector2(-440, -190));
            khungNhan = Gian(goc, "Nhãn nguyên tử", Vector2.zero, Vector2.zero);

            var sang = new Color32(0xe8, 0xec, 0xf5, 0xff);
            var phu = new Color32(0x9a, 0xa5, 0xbd, 0xff);
            GlassKit.Label(goc, "Tiêu đề", "Mô hình 3D", 27, sang, true, TextAnchor.UpperLeft, new Vector2(28, -22), new Vector2(800, 38));
            GlassKit.Label(goc, "Mô tả", "Gõ tên chất (tiếng Việt hoặc tiếng Anh), công thức thu gọn hoặc SMILES · kéo để xoay 360° · cuộn để phóng to",
                           15, phu, false, TextAnchor.UpperLeft, new Vector2(28, -64), new Vector2(1300, 22));
            oNhap = GlassKit.MakeInput(goc, "Ô nhập chất", "Ví dụ: ethanol, CH3COOH, benzen, CCO", new Vector2(28, -100), new Vector2(620, 50), 17);
            oNhap.onSubmit.AddListener(XemChat);
            GlassKit.MakeButton(goc, "Nút Xem 3D", "Xem 3D", new Vector2(660, -100), new Vector2(130, 50), () => XemChat(oNhap.text), true, 16);
            float x = 806;
            foreach (var m in Mau)
            {
                string mm = m;
                var b = GlassKit.MakeChip(goc, "Mẫu " + m, m, new Vector2(x, -104), new Vector2(Mathf.Max(80, 16 + m.Length * 10), 42), () => { oNhap.text = mm; XemChat(mm); }, false, 14);
                x += ((RectTransform)b.transform).sizeDelta.x + 8;
            }
            GlassKit.MakeButton(goc, "Đóng", "Đóng", new Vector2(-110, -22), new Vector2(100, 44), Dong, false, 15, new Vector2(1, 1));

            // thẻ thông tin bên phải
            var the = GlassKit.Fill(goc, "Thông tin chất", new Vector2(-420, -190), new Vector2(392, 520), GlassKit.Surface, new Vector2(1, 1), true).rectTransform;
            tenChat = GlassKit.Label(the, "Tên chất", "Chưa có chất", 22, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(22, -20), new Vector2(350, 60));
            tenChat.horizontalOverflow = HorizontalWrapMode.Wrap;
            thongTin = GlassKit.Label(the, "Thông số", "", 16, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(22, -90), new Vector2(350, 260));
            thongTin.horizontalOverflow = HorizontalWrapMode.Wrap;
            nutAnH = GlassKit.MakeChip(the, "Ẩn H", "Ẩn H", new Vector2(22, -380), new Vector2(110, 44), () => { TrinhPhat.AnH = !TrinhPhat.AnH; GlassKit.SetChip(nutAnH, TrinhPhat.AnH); });
            GlassKit.MakeButton(the, "Góc nhìn", "Đặt lại góc", new Vector2(142, -380), new Vector2(150, 44), () => { if (TrinhPhat.KichBan != null) TrinhPhat.Nap(TrinhPhat.KichBan); });
            trangThai = GlassKit.Label(the, "Trạng thái", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(22, -440), new Vector2(350, 70));
            trangThai.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static RectTransform Gian(Transform cha, string ten, Vector2 offMin, Vector2 offMax)
        {
            var rt = new GameObject(ten, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(cha, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        void KhiMo()
        {
            camTat.Clear();
            foreach (var c in Camera.allCameras)
                if (c != Cam && c.enabled) { c.enabled = false; camTat.Add(c); }
            if (!daTangUiCapture) { DesktopViewer.UiCapture++; daTangUiCapture = true; }
            if (TrinhPhat.KichBan == null) XemChat("ethanol");
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(oNhap.gameObject);
        }

        void KhiDong()
        {
            foreach (var c in camTat) if (c != null) c.enabled = true;
            camTat.Clear();
            if (daTangUiCapture) { DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1); daTangUiCapture = false; }
        }

        public void XemChat(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || DangTai) return;
            StartCoroutine(Tai(q.Trim()));
        }

        IEnumerator Tai(string q)
        {
            DangTai = true;
            trangThai.text = "Đang dựng 3D cho \"" + q + "\"…";
            LoiCuoi = "";
            yield return MayChuCoChe.Goi("POST", "/co-che/mo-hinh-3d", "{\"van_ban\":" + MayChuCoChe.ChuoiJson(q) + "}", 30f, (ok, kq) =>
            {
                if (!ok)
                {
                    LoiCuoi = DocLoi(kq);
                    trangThai.text = LoiCuoi;
                    return;
                }
                try { Hien(D(JsonNho.Doc(kq))); }
                catch (System.Exception e) { LoiCuoi = "Kết quả lỗi định dạng: " + e.Message; trangThai.text = LoiCuoi; }
            });
            DangTai = false;
        }

        static string DocLoi(string kq)
        {
            try
            {
                var d = D(JsonNho.Doc(kq));
                var ct = S(d, "detail");
                if (!string.IsNullOrEmpty(ct)) return ct;
            }
            catch { }
            return "Không kết nối được máy chủ cơ chế (" + MayChuCoChe.Url + "). Bật: python -m server.ai.co_che.may_chu · " + kq;
        }

        /// <summary>Đổi kết quả máy chủ thành kịch bản 1 trạng thái, 1 bước (không mũi tên) để TrinhPhat3D vẽ tĩnh.</summary>
        void Hien(Dictionary<string, object> d)
        {
            var kb = new KichBan3D { Ten = S(d, "ten") };
            var tt = new TrangThai3D { Ten = kb.Ten };
            var ds = L(Lay(d, "nguyen_tu"));
            for (int i = 0; i < ds.Count; i++)
            {
                var a = D(ds[i]);
                string id = "a" + i;
                kb.NguyenTu[id] = S(a, "e", "C");
                tt.ToaDo[id] = new Vector3(F(Lay(a, "x")), F(Lay(a, "y")), -F(Lay(a, "z")));   // hệ tay trái của Unity
                int dt = I(Lay(a, "dt"));
                if (dt != 0) tt.DienTich[id] = dt;
            }
            foreach (var o in L(Lay(d, "lien_ket")))
            {
                var l = D(o);
                tt.LienKet.Add(new LienKet3D { A = "a" + I(Lay(l, "a")), B = "a" + I(Lay(l, "b")), Bac = Mathf.Max(1, I(Lay(l, "bac"))) });
            }
            kb.TrangThai.Add(tt);
            kb.Buoc.Add(new Buoc3D { Tu = 0, Den = 0, Ten = kb.Ten });
            TrinhPhat.Nap(kb);
            TrinhPhat.DatT(1f);
            TenHienTai = kb.Ten;
            SoNguyenTu = ds.Count;
            tenChat.text = string.IsNullOrEmpty(kb.Ten) ? "(không tên)" : kb.Ten;
            string nguon = S(d, "nguon") switch { "bang_sgk" => "bảng chất SGK (đã kiểm chứng)", "pubchem" => "PubChem", "smiles" => "SMILES bạn nhập", var s => s };
            thongTin.text = $"<b>Công thức phân tử:</b> {CongThuc(S(d, "cong_thuc"))}\n<b>Khối lượng mol:</b> {F(Lay(d, "khoi_luong")):0.00} g/mol\n" +
                            $"<b>Số nguyên tử:</b> {ds.Count} (kể cả H)\n<b>SMILES:</b> {S(d, "smiles")}\n<b>Nguồn:</b> {nguon}\n\n" +
                            "<color=#9aa5bd>Cấu dạng tính bằng RDKit (ETKDG + MMFF).</color>";
            trangThai.text = "";
        }

        /// <summary>C2H6O → C₂H₆O</summary>
        static string CongThuc(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s) sb.Append(c >= '0' && c <= '9' ? (char)('₀' + (c - '0')) : c);
            return sb.ToString();
        }

        void Update()
        {
            if (vungNhin == null) return;
            vungNhin.GetWorldCorners(gocVung);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(Cam, gocVung[0]), b = RectTransformUtility.WorldToScreenPoint(Cam, gocVung[2]);
            float w = Mathf.Max(1, Cam.pixelWidth), h = Mathf.Max(1, Cam.pixelHeight);
            TrinhPhat.VungNhin = Rect.MinMaxRect(a.x / w, a.y / h, b.x / w, b.y / h);
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Dong();
        }

        void LateUpdate()
        {
            int dung = 0;
            if (TrinhPhat.KichBan != null)
                foreach (var kv in TrinhPhat.ViTriNguyenTu)
                {
                    string e = TrinhPhat.NguyenTo(kv.Key);
                    if (e == "C" || e == "H") continue;
                    var sp = Cam.WorldToScreenPoint(TrinhPhat.TheGioi(kv.Value));
                    if (sp.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(khungNhan, sp, Cam, out var lp)) continue;
                    while (nhan.Count <= dung)
                    {
                        var t = GlassKit.Label(khungNhan, "Nhãn", "", 15, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(50, 24), new Vector2(0.5f, 0.5f));
                        t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        t.raycastTarget = false;
                        var sh = t.gameObject.AddComponent<Shadow>();
                        sh.effectColor = new Color(0, 0, 0, 0.75f);
                        nhan.Add(t);
                    }
                    var n = nhan[dung++];
                    if (n.text != e) n.text = e;
                    n.rectTransform.anchoredPosition = lp;
                    if (!n.gameObject.activeSelf) n.gameObject.SetActive(true);
                }
            for (int i = dung; i < nhan.Count; i++) if (nhan[i].gameObject.activeSelf) nhan[i].gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            KhiDong();
        }
    }
}
