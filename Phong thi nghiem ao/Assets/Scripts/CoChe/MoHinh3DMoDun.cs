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
            Cam.backgroundColor = new Color32(0x12, 0x12, 0x15, 0xff);          // nền tối như mục 3D của CHEMISTRY
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
            TrinhPhat.KieuChemistry = true; TrinhPhat.KieuJmol = true;
            // kéo chuột / trackpad để xoay khi con trỏ nằm trong vùng nhìn (dưới thanh nút, trên ô nhập)
            TrinhPhat.ViTriChuot = p => vungNhin != null && RectTransformUtility.RectangleContainsScreenPoint(vungNhin, p, Cam) ? p : (Vector2?)null;          // bóng–que: cầu 0,27×vdW, que 0,14 Å hai màu

            // 20/11: bố cục giống mục 04 "Mô hình 3D" của CHEMISTRY – nền tối toàn màn hình, nút nổi ở trên (như Cơ chế 3D toàn màn hình)
            vungNhin = Gian(goc, "Vùng nhìn", new Vector2(0, 90), new Vector2(0, -84));
            khungNhan = Gian(goc, "Nhãn nguyên tử", Vector2.zero, Vector2.zero);
            khungNhan.gameObject.SetActive(false);

            Color sang = new Color32(0xfa, 0xfa, 0xfa, 0xff), phu = new Color32(0xa1, 0xa1, 0xaa, 0xff);
            var thanh = GlassKit.Rect(goc, "Thanh nổi", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -84), new Vector2(0, 0));
            thanh.anchorMin = new Vector2(0, 1); thanh.anchorMax = new Vector2(1, 1); thanh.pivot = new Vector2(0.5f, 1f);
            thanh.offsetMin = new Vector2(0, -84); thanh.offsetMax = Vector2.zero;
            var nenThanh = thanh.gameObject.AddComponent<Image>(); nenThanh.color = new Color32(0x09, 0x09, 0x0b, 0xe6); nenThanh.raycastTarget = false;
            tenChat = GlassKit.Label(thanh, "Tên chất", "ethanol", 21, sang, true, TextAnchor.MiddleLeft, new Vector2(28, -20), new Vector2(260, 44));
            tenChat.horizontalOverflow = HorizontalWrapMode.Overflow;
            thongTin = GlassKit.Label(thanh, "Thông số", "", 17, phu, false, TextAnchor.MiddleLeft, new Vector2(262, -20), new Vector2(240, 44));
            thongTin.supportRichText = true;

            float x = 470;
            var nhomKieu = Pill(thanh, "Nhóm kiểu", new Vector2(x, -18), new Vector2(4 * 104 + 8, 48), NenNut);
            string[] kieu = { "Bóng-que", "Que", "Đặc", "Dây" };
            for (int k = 0; k < 4; k++)
            {
                int kk = k;
                nutKieu[k] = NutNoi(nhomKieu, "Kiểu " + kieu[k], kieu[k], new Vector2(4 + k * 104, -4), new Vector2(100, 40), () => DatKieu(kk), false);
            }
            x += 4 * 104 + 8 + 14;
            nutBeMat = NutNoi(thanh, "Bề mặt", "Tắt bề mặt  ▾", new Vector2(x, -18), new Vector2(176, 48), DoiBeMat, true); x += 186;
            nutMau = NutNoi(thanh, "Màu", "Màu Jmol  ▾", new Vector2(x, -18), new Vector2(150, 48), DoiMau, true); x += 160;
            nutNhan = NutNoi(thanh, "Nhãn", "☐  Nhãn", new Vector2(x, -18), new Vector2(104, 48), () => { khungNhan.gameObject.SetActive(!khungNhan.gameObject.activeSelf); CapNhatNut(); }, true); x += 112;
            nutXoay = NutNoi(thanh, "Tự xoay", "☐  Tự xoay", new Vector2(x, -18), new Vector2(124, 48), () => { TrinhPhat.TuXoay = !TrinhPhat.TuXoay; CapNhatNut(); }, true); x += 132;
            nutAnH = NutNoi(thanh, "Ẩn H", "☐  Ẩn H", new Vector2(x, -18), new Vector2(104, 48), () => { TrinhPhat.AnH = !TrinhPhat.AnH; CapNhatNut(); }, true);
            var dong = NutNoi(thanh, "Đóng", "Đóng", new Vector2(-24, -18), new Vector2(96, 48), Dong, true);
            var rd = (RectTransform)dong.transform; rd.anchorMin = rd.anchorMax = rd.pivot = new Vector2(1, 1);

            // ô nhập nổi ở đáy
            // ô tìm chất + nút Xem 3D nổi ở đáy, cùng kiểu viên nút tối của thanh trên
            var khungNhap = Pill(goc, "Ô nhập chất (khung)", Vector2.zero, new Vector2(720, 52), NenNut);
            khungNhap.anchorMin = khungNhap.anchorMax = khungNhap.pivot = new Vector2(0, 0); khungNhap.anchoredPosition = new Vector2(28, 22);
            var chuNhap = GlassKit.Label(khungNhap, "Chữ", "", 17, new Color32(0xfa, 0xfa, 0xfa, 0xff), false, TextAnchor.MiddleLeft, new Vector2(18, 0), new Vector2(684, 52));
            chuNhap.supportRichText = false; chuNhap.raycastTarget = false;
            var goiY = GlassKit.Label(khungNhap, "Gợi ý", "Tên chất, công thức hoặc SMILES rồi Enter (vd: ethanol, CH3COOH, CCO)", 17, new Color32(0x71, 0x71, 0x7a, 0xff), false, TextAnchor.MiddleLeft, new Vector2(18, 0), new Vector2(684, 52));
            goiY.raycastTarget = false;
            oNhap = khungNhap.gameObject.AddComponent<InputField>();
            oNhap.name = "Ô nhập chất";
            khungNhap.name = "Ô nhập chất";
            oNhap.textComponent = chuNhap; oNhap.placeholder = goiY;
            oNhap.targetGraphic = khungNhap.GetComponent<Image>();
            oNhap.caretColor = new Color32(0xfa, 0xfa, 0xfa, 0xff); oNhap.customCaretColor = true;
            oNhap.selectionColor = new Color(0.36f, 0.55f, 1f, 0.35f);
            oNhap.lineType = InputField.LineType.SingleLine;
            var mauO = oNhap.colors; mauO.normalColor = Color.white; mauO.highlightedColor = new Color(1.25f, 1.25f, 1.3f); mauO.selectedColor = new Color(1.4f, 1.4f, 1.5f); oNhap.colors = mauO;
            oNhap.onSubmit.AddListener(XemChat);
            var xem = NutNoi(goc, "Nút Xem 3D", "Xem 3D", Vector2.zero, new Vector2(130, 52), () => XemChat(oNhap.text), true);
            var rx = (RectTransform)xem.transform; rx.anchorMin = rx.anchorMax = rx.pivot = new Vector2(0, 0); rx.anchoredPosition = new Vector2(760, 22);
            xem.GetComponent<Image>().color = new Color32(0xfa, 0xfa, 0xfa, 0xff);                      // nút chính: nền sáng chữ tối như ô đang chọn
            var cx = xem.GetComponentInChildren<Text>(); cx.color = new Color32(0x09, 0x09, 0x0b, 0xff); cx.fontStyle = FontStyle.Bold;
            trangThai = GlassKit.Label(goc, "Trạng thái", "", 15, phu, false, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(900, 30));
            var rt = trangThai.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0); rt.anchoredPosition = new Vector2(30, 80);
            trangThai.horizontalOverflow = HorizontalWrapMode.Wrap;
            CapNhatNut();
        }

        static readonly Color NenNut = new Color32(0x12, 0x12, 0x15, 0xf2);
        readonly Button[] nutKieu = new Button[4];
        Button nutMau, nutNhan, nutXoay, nutBeMat;
        static readonly string[] TenBeMat = { "Tắt bề mặt", "Bề mặt VDW", "Dung môi (SAS)", "Phân tử (SES)" };
        void DoiBeMat() { TrinhPhat.BeMat = (BeMatPhanTu.Loai)(((int)TrinhPhat.BeMat + 1) % 4); CapNhatNut(); }
        static readonly string[] TenMau = { "Màu Jmol", "Carbon tối", "Rasmol" };

        static RectTransform Pill(Transform cha, string ten, Vector2 pos, Vector2 size, Color mau)
        {
            var r = GlassKit.Rect(cha, ten, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            r.pivot = new Vector2(0, 1); r.anchoredPosition = pos;
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Khkt.Menu.Dashboard.MenuKit.Round(6f); im.type = Image.Type.Sliced; im.color = mau;
            var ol = r.gameObject.AddComponent<Outline>(); ol.effectColor = new Color32(0x27, 0x27, 0x2a, 0xff); ol.effectDistance = new Vector2(1, -1);
            return r;
        }

        Button NutNoi(Transform cha, string ten, string chu, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction bam, bool vien)
        {
            var r = Pill(cha, ten, pos, size, vien ? NenNut : new Color(0, 0, 0, 0));
            if (!vien) Destroy(r.GetComponent<Outline>());
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            b.onClick.AddListener(bam);
            var t = GlassKit.Label(r, "Chữ", chu, 17, new Color32(0xfa, 0xfa, 0xfa, 0xff), false, TextAnchor.MiddleCenter, Vector2.zero, size);
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }

        void DatKieu(int k) { TrinhPhat.KieuVe = k; CapNhatNut(); }
        void DoiMau() { TrinhPhat.BangMau = (TrinhPhat.BangMau + 1) % 3; CapNhatNut(); }

        void CapNhatNut()
        {
            Color den = new Color32(0x09, 0x09, 0x0b, 0xff), sang = new Color32(0xfa, 0xfa, 0xfa, 0xff), phu = new Color32(0xa1, 0xa1, 0xaa, 0xff);
            for (int k = 0; k < 4; k++)
            {
                if (nutKieu[k] == null) continue;
                bool chon = TrinhPhat.KieuVe == k;
                nutKieu[k].GetComponent<Image>().color = chon ? sang : new Color(0, 0, 0, 0);
                nutKieu[k].GetComponentInChildren<Text>().color = chon ? den : phu;
            }
            if (nutBeMat != null) nutBeMat.GetComponentInChildren<Text>().text = TenBeMat[(int)TrinhPhat.BeMat] + "  ▾";
            if (nutMau != null) nutMau.GetComponentInChildren<Text>().text = TenMau[TrinhPhat.BangMau] + "  ▾";
            if (nutNhan != null) nutNhan.GetComponentInChildren<Text>().text = (khungNhan.gameObject.activeSelf ? "☑" : "☐") + "  Nhãn";
            if (nutXoay != null) nutXoay.GetComponentInChildren<Text>().text = (TrinhPhat.TuXoay ? "☑" : "☐") + "  Tự xoay";
            if (nutAnH != null) nutAnH.GetComponentInChildren<Text>().text = (TrinhPhat.AnH ? "☑" : "☐") + "  Ẩn H";
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
        /// <summary>Kịch bản 3D một trạng thái từ kết quả /co-che/mo-hinh-3d (dùng chung cho khung lớn và khung nhỏ ở trang kết quả).</summary>
        public static KichBan3D TaoKichBan(Dictionary<string, object> d)
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
            return kb;
        }

        void Hien(Dictionary<string, object> d)
        {
            var kb = TaoKichBan(d);
            var ds = L(Lay(d, "nguyen_tu"));
            TrinhPhat.Nap(kb);
            TrinhPhat.DatT(1f);
            TenHienTai = kb.Ten;
            SoNguyenTu = ds.Count;
            tenChat.text = string.IsNullOrEmpty(kb.Ten) ? "(không tên)" : kb.Ten;
            string nguon = S(d, "nguon") switch { "bang_sgk" => "bảng chất SGK (đã kiểm chứng)", "pubchem" => "PubChem", "smiles" => "SMILES bạn nhập", var s => s };
            thongTin.text = $"{CongThuc(S(d, "cong_thuc"))} · {F(Lay(d, "khoi_luong")):0.00} g/mol";
            float rongTen = Mathf.Min(260f, tenChat.preferredWidth);
            thongTin.rectTransform.anchoredPosition = new Vector2(28 + rongTen + 14, thongTin.rectTransform.anchoredPosition.y);
            _ = nguon;
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
                    if (e == "H") continue;
                    if (TrinhPhat.BiChe(kv.Key)) continue;
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
