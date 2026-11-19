using System.Collections.Generic;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Khkt.CoChe
{
    /// <summary>
    /// Trình phát cơ chế 3D (theo bản thử nguyen_mau/co_che_3d/hsg_propellane.html và HOP_DONG_APP.md mục 1):
    ///   • nguyên tử = quả cầu màu CPK, bán kính theo nguyên tố, độ mịn HardwareProfile.SoManhCau; liên kết = trụ bậc 1/2/3;
    ///     vẽ bằng GPU instancing (Graphics.RenderMeshInstanced) trên camera riêng của lớp phủ cơ chế;
    ///   • nội suy giữa hai trạng thái: electron đi trước, nguyên tử bắt đầu dịch từ ~35% thời gian bước; liên kết mới mọc dần,
    ///     liên kết mất mờ dần (co lại); mỗi mũi tên là 1–2 hạt electron bay theo đường cong (tổng ≤ HardwareProfile.SoHatToiDa);
    ///   • nhánh sai: chất tấn công tiến tới, electron dừng ở màn chắn – màn chắn CHỈ hiện khi đang phát nhánh sai;
    ///   • nút Ẩn H; kéo chuột xoay 360°, cuộn để phóng to, ← → chuyển bước, Space phát/dừng.
    /// Phân tử đặt ở xa khu lab (Tam), camera riêng chỉ vẽ lớp LopVe nên không lẫn với cảnh.
    /// </summary>
    public class TrinhPhat3D : MonoBehaviour
    {
        internal const string CheB = "electron-đi-trước-35%";
        public const int LopVe = 5;
        public static readonly Vector3 Tam = new Vector3(0f, -500f, 0f);

        public Camera Cam;
        public KichBan3D KichBan { get; private set; }
        public int ChiSoBuoc { get; private set; }
        public float T { get; private set; }
        public bool DangPhat;
        public bool AnH;
        public bool NhanDieuKhien = true;
        public float Phang = 1f;
        public float ThoiGianBuoc = 4.2f;        // 19/11: chậm hơn để thấy rõ electron bay (bay ≈ 55% đầu bước ≈ 2,3 s ở 1×)
        public float TocDo = 1f;                 // thanh trượt 0,25× – 3× ở khung 3D
        /// <summary>Tự chọn góc nhìn mỗi bước (thấy rõ đường electron và nguyên tử tham gia, không bị che). Kéo chuột thì tắt tới bước sau.</summary>
        public bool TuDongGoc = true;
        /// <summary>Kiểu 3Dmol của CHEMISTRY (PubChem): cầu nhỏ (0,27 × bán kính van der Waals), que dày 0,14 Å, bảng màu CHEMISTRY.</summary>
        public bool KieuChemistry;
        /// <summary>Tự xoay chậm quanh trục đứng (như spin("y") của 3Dmol); kéo chuột thì dừng tới khi thả.</summary>
        public bool TuXoay;
        public float TocXoay = 24f;
        /// <summary>Khi hình 3D hiện trong một khung (RenderTexture): đổi điểm chuột (pixel màn hình) → toạ độ 0..1 trong khung, null nếu ngoài khung.</summary>
        public System.Func<Vector2, Vector2?> ViTriChuot;
        Quaternion xoayDich = Quaternion.Euler(12f, -18f, 0f);
        int dichBuoc = -2;
        Vector3 tamDich, tamNay;                 // tâm khung nhìn (toạ độ mô hình) của bước, trượt mượt
        float khoangDich = 13f;
        bool dichNhanh;
        public Quaternion XoayDich => xoayDich;
        /// <summary>Đặt ngay góc, tâm, khoảng cách tự chọn của bước hiện tại (chụp ảnh, kiểm thử).</summary>
        public void DatNgayGocDich()
        {
            CapNhatDich();
            Xoay = xoayDich; tamNay = tamDich;
            KhoangCach = khoangDich * (Cam != null ? Mathf.Clamp(1.6f / Mathf.Max(0.3f, Cam.aspect), 1f, 1.8f) : 1f);
        }
        public float KhoangCach = 13f;
        public Quaternion Xoay = Quaternion.Euler(12f, -18f, 0f);
        /// <summary>Vùng màn hình (tỉ lệ 0..1) dành cho hình 3D: tâm phân tử đặt vào giữa vùng này, kéo chuột trong vùng để xoay.</summary>
        public Rect VungNhin = new Rect(0.25f, 0.12f, 0.75f, 0.76f);
        public bool DangNhanhSai { get; private set; }
        public NhanhSai3D NhanhHienTai { get; private set; }
        public int SoHatDangVe { get; private set; }
        public int SoNguyenTuDangVe { get; private set; }
        public event System.Action DoiBuoc;

        float tNhanh;
        Mesh cau, tru, dia, cauE;
        int manhCau = -1;
        Material matGoc, matE, matChan, matLk, matQuang;
        readonly List<Matrix4x4> quang = new List<Matrix4x4>();
        readonly Dictionary<string, Material> matNt = new Dictionary<string, Material>();
        readonly Dictionary<string, List<Matrix4x4>> nhom = new Dictionary<string, List<Matrix4x4>>();
        readonly List<Matrix4x4> lk = new List<Matrix4x4>(), hat = new List<Matrix4x4>();
        readonly Dictionary<string, Vector3> viTri = new Dictionary<string, Vector3>();
        readonly Dictionary<string, float> coNt = new Dictionary<string, float>();
        bool keo;
        Vector2 chuotTruoc;

        public int SoBuoc => KichBan == null ? 0 : KichBan.Buoc.Count;
        public Buoc3D BuocHienTai => KichBan == null || SoBuoc == 0 ? null : KichBan.Buoc[Mathf.Clamp(ChiSoBuoc, 0, SoBuoc - 1)];

        public void Nap(KichBan3D kb)
        {
            KichBan = kb;
            ChiSoBuoc = 0;
            T = 0f;
            DangPhat = false;
            DangNhanhSai = false;
            NhanhHienTai = null;
            Xoay = Quaternion.Euler(12f, -18f, 0f);
            dichBuoc = -2;
            CapNhatDich();
            Xoay = xoayDich;                                        // mở bài: đặt ngay góc tốt, không quay vòng
            tamNay = tamDich;
            KhoangCach = khoangDich;
            DoiBuoc?.Invoke();
        }

        public void DenBuoc(int i, bool phat = false)
        {
            if (KichBan == null) return;
            DangNhanhSai = false;
            ChiSoBuoc = Mathf.Clamp(i, 0, SoBuoc - 1);
            T = 0f;
            DangPhat = phat;
            DoiBuoc?.Invoke();
        }

        public void BuocSau()
        {
            if (KichBan == null) return;
            if (T < 1f && !DangPhat) { DangPhat = true; return; }
            if (ChiSoBuoc < SoBuoc - 1) DenBuoc(ChiSoBuoc + 1, true);
        }

        public void BuocTruoc()
        {
            if (KichBan == null) return;
            if (T > 0.05f) DenBuoc(ChiSoBuoc);
            else DenBuoc(ChiSoBuoc - 1);
        }

        public void PhatDung()
        {
            if (KichBan == null) return;
            if (!DangPhat && T >= 1f && ChiSoBuoc >= SoBuoc - 1) { DenBuoc(0, true); return; }
            DangPhat = !DangPhat;
        }

        /// <summary>Đặt thời điểm trong bước (kiểm thử, chụp ảnh).</summary>
        public void DatT(float t) { T = Mathf.Clamp01(t); }

        public void PhatNhanhSai(int i = 0)
        {
            if (KichBan == null || KichBan.NhanhSai.Count == 0) return;
            NhanhHienTai = KichBan.NhanhSai[Mathf.Clamp(i, 0, KichBan.NhanhSai.Count - 1)];
            DangNhanhSai = true;
            DangPhat = false;
            tNhanh = 0f;
            DoiBuoc?.Invoke();
        }

        public void DatTNhanh(float t) => tNhanh = t;

        // ------------------------------------------------------------------ vòng cập nhật
        void Update()
        {
            if (KichBan == null || Cam == null) return;
            DieuKhien();
            CapNhatDich();
            if (TuXoay && !keo) Xoay = Quaternion.AngleAxis(TocXoay * Time.unscaledDeltaTime, Vector3.up) * Xoay;
            if (TuDongGoc && !keo)
            {
                float k = 1f - Mathf.Exp(-2.2f * Time.unscaledDeltaTime);
                if (!TuXoay) Xoay = Quaternion.Slerp(Xoay, xoayDich, k);
                tamNay = Vector3.Lerp(tamNay, tamDich, k);
                KhoangCach = Mathf.Lerp(KhoangCach, khoangDich * Mathf.Clamp(1.6f / Mathf.Max(0.3f, Cam.aspect), 1f, 1.8f), k);
            }
            float dt = Time.unscaledDeltaTime * TocDo;
            if (DangNhanhSai)
            {
                tNhanh += dt / 3.4f;
                if (tNhanh > 1.3f) { DangNhanhSai = false; DoiBuoc?.Invoke(); }
            }
            else if (DangPhat && SoBuoc > 0)
            {
                T += dt / ThoiGianBuoc;
                if (T >= 1f)
                {
                    if (ChiSoBuoc < SoBuoc - 1) { ChiSoBuoc++; T = 0f; DoiBuoc?.Invoke(); }
                    else { T = 1f; DangPhat = false; DoiBuoc?.Invoke(); }
                }
            }
            DatCamera();
            Ve();
        }

        void DieuKhien()
        {
            if (!NhanDieuKhien) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) BuocSau();
                if (kb.leftArrowKey.wasPressedThisFrame) BuocTruoc();
                if (kb.spaceKey.wasPressedThisFrame) PhatDung();
            }
            var m = Mouse.current;
            if (m == null) return;
            Vector2 p = m.position.ReadValue();
            bool trongVung;
            if (ViTriChuot != null) trongVung = ViTriChuot(p).HasValue;
            else
            {
                var vp = new Vector2(p.x / Mathf.Max(1, Screen.width), p.y / Mathf.Max(1, Screen.height));
                trongVung = VungNhin.Contains(vp) && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            }
            if (m.leftButton.wasPressedThisFrame && trongVung) { keo = true; chuotTruoc = p; TuDongGoc = false; }
            if (!m.leftButton.isPressed) keo = false;
            if (keo)
            {
                Vector2 d = p - chuotTruoc;
                chuotTruoc = p;
                Xoay = Quaternion.AngleAxis(-d.x * 0.35f, Vector3.up) * Quaternion.AngleAxis(d.y * 0.35f, Vector3.right) * Xoay;
            }
            float cuon = m.scroll.ReadValue().y;
            if (trongVung && Mathf.Abs(cuon) > 0.01f)
            { KhoangCach = Mathf.Clamp(KhoangCach * (cuon > 0 ? 0.9f : 1.1f), 4f, 60f); TuDongGoc = false; }
        }

        // ------------------------------------------------------------------ góc nhìn tự động (19/11)
        void CapNhatDich()
        {
            if (KichBan == null) return;
            int k = DangNhanhSai ? -1 : ChiSoBuoc;
            if (k == dichBuoc && DangNhanhSai == dichNhanh) return;
            dichBuoc = k;
            dichNhanh = DangNhanhSai;
            TuDongGoc = true;
            xoayDich = TinhGocNhin();
            // khung nhìn ôm các chất có mặt ở bước này (trạng thái đầu và cuối của bước): tâm + khoảng cách vừa đủ
            var ds = new List<Vector3>();
            if (DangNhanhSai && NhanhHienTai != null) ds.AddRange(KichBan.TrangThai[Mathf.Clamp(NhanhHienTai.TuTrangThai, 0, KichBan.TrangThai.Count - 1)].ToaDo.Values);
            else if (BuocHienTai != null)
            {
                ds.AddRange(KichBan.TrangThai[Mathf.Clamp(BuocHienTai.Tu, 0, KichBan.TrangThai.Count - 1)].ToaDo.Values);
                ds.AddRange(KichBan.TrangThai[Mathf.Clamp(BuocHienTai.Den, 0, KichBan.TrangThai.Count - 1)].ToaDo.Values);
            }
            if (ds.Count > 0)
            {
                Vector3 mn = ds[0], mx = ds[0];
                foreach (var p in ds) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
                tamDich = (mn + mx) * 0.5f;
                float r = 1.2f;
                foreach (var p in ds) r = Mathf.Max(r, (p - tamDich).magnitude + 0.5f);
                float tanf = Mathf.Tan((Cam != null ? Cam.fieldOfView : 32f) * 0.5f * Mathf.Deg2Rad);
                khoangDich = Mathf.Clamp(r / tanf * 1.08f, 5f, 60f);
            }
        }

        static readonly Vector3[] HuongThu = TaoHuongThu(160);

        static Vector3[] TaoHuongThu(int n)
        {
            var ds = new Vector3[n];
            float vang = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < n; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / n, r = Mathf.Sqrt(1f - y * y), a = vang * i;
                ds[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            return ds;
        }

        /// <summary>
        /// Chấm điểm 160 hướng nhìn: (1) mũi tên electron nằm ngang mặt nhìn (thấy trọn đường bay), (2) nguyên tử tham gia không bị
        /// nguyên tử khác che và không chồng lên nhau, (3) gần hướng đang nhìn và hơi nhìn từ trên xuống (chuyển góc ít, dễ theo dõi).
        /// Trả về phép xoay đưa hướng tốt nhất về phía camera.
        /// </summary>
        public Quaternion TinhGocNhin()
        {
            if (KichBan == null || KichBan.TrangThai.Count == 0) return Xoay;
            TrangThai3D s;
            List<MuiTen> mts;
            if (DangNhanhSai && NhanhHienTai != null) { s = KichBan.TrangThai[Mathf.Clamp(NhanhHienTai.TuTrangThai, 0, KichBan.TrangThai.Count - 1)]; mts = NhanhHienTai.MuiTen; }
            else { var b = BuocHienTai; if (b == null) return Xoay; s = KichBan.TrangThai[Mathf.Clamp(b.Tu, 0, KichBan.TrangThai.Count - 1)]; mts = b.MuiTen; }
            var pos = s.ToaDo;
            var tatCa = new List<Vector3>(pos.Values);
            if (tatCa.Count == 0) return Xoay;
            var thamGia = new List<Vector3>();
            var muiTen = new List<(Vector3 a, Vector3 e)>();
            Vector3 TB(int[] ds)
            {
                Vector3 t = Vector3.zero; int n = 0;
                if (ds != null) foreach (var x in ds) if (pos.TryGetValue("m" + x, out var q)) { t += q; n++; if (!thamGia.Contains(q)) thamGia.Add(q); }
                return n > 0 ? t / n : Vector3.zero;
            }
            foreach (var mt in mts) { var a = TB(mt.Tu); var e = TB(mt.Den); if ((e - a).sqrMagnitude > 1e-4f) muiTen.Add((a, e)); }
            if (thamGia.Count == 0) thamGia.AddRange(tatCa);
            Vector3 vNay = Quaternion.Inverse(Xoay) * Vector3.back;
            float tot = float.NegativeInfinity;
            Vector3 vTot = vNay;
            foreach (var v in HuongThu)
            {
                float d = 0f;
                foreach (var (a, e) in muiTen)
                {
                    var u = e - a;
                    d += 1.4f * (u - Vector3.Dot(u, v) * v).magnitude / (u.magnitude + 1e-3f);
                }
                foreach (var p in thamGia)
                {
                    foreach (var q in tatCa)
                    {
                        var w = q - p;
                        float doc = Vector3.Dot(w, v);
                        if (doc < 0.05f) continue;                       // q ở sau p (xa camera hơn) thì không che
                        float ngang = (w - doc * v).magnitude;
                        if (ngang < 0.7f) d -= 1.3f * (1f - ngang / 0.7f);
                    }
                }
                for (int i = 0; i < thamGia.Count; i++)
                    for (int j = i + 1; j < thamGia.Count; j++)
                    {
                        var w = thamGia[j] - thamGia[i];
                        float ngang = (w - Vector3.Dot(w, v) * v).magnitude;
                        if (ngang < 0.9f) d -= 0.8f * (1f - ngang / 0.9f);
                    }
                d += 0.35f * Vector3.Dot(v, vNay) + 0.15f * v.y;
                if (d > tot) { tot = d; vTot = v; }
            }
            var len = Mathf.Abs(Vector3.Dot(vTot, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            return Quaternion.Inverse(Quaternion.LookRotation(-vTot, len));
        }

        void DatCamera()
        {
            float tanf = Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float cx = VungNhin.center.x, cy = VungNhin.center.y;
            float dx = (cx - 0.5f) * 2f * KhoangCach * tanf * Cam.aspect;
            float dy = (cy - 0.5f) * 2f * KhoangCach * tanf;
            Vector3 tam = W(tamNay);
            Cam.transform.SetPositionAndRotation(tam + new Vector3(-dx, -dy, -KhoangCach), Quaternion.identity);
        }

        // ------------------------------------------------------------------ hình học
        static readonly Dictionary<string, Color> MauCPK = new Dictionary<string, Color>
        {
            {"H", new Color(0.95f, 0.95f, 0.95f)}, {"C", new Color(0.25f, 0.27f, 0.30f)}, {"N", new Color(0.19f, 0.31f, 0.97f)},
            {"O", new Color(0.93f, 0.16f, 0.12f)}, {"F", new Color(0.56f, 0.88f, 0.31f)}, {"Cl", new Color(0.12f, 0.80f, 0.20f)},
            {"Br", new Color(0.65f, 0.16f, 0.16f)}, {"I", new Color(0.58f, 0.0f, 0.58f)}, {"S", new Color(0.95f, 0.85f, 0.20f)},
            {"P", new Color(1f, 0.5f, 0f)}, {"Na", new Color(0.67f, 0.36f, 0.95f)}, {"K", new Color(0.56f, 0.25f, 0.83f)},
        };
        static readonly Dictionary<string, float> BanKinh = new Dictionary<string, float>
        {
            {"H", 0.25f}, {"C", 0.36f}, {"N", 0.35f}, {"O", 0.34f}, {"F", 0.33f}, {"Cl", 0.44f}, {"Br", 0.48f}, {"I", 0.54f}, {"S", 0.44f}, {"P", 0.44f},
        };

        public static Color Mau(string e) => MauCPK.TryGetValue(e, out var c) ? c : new Color(1f, 0.55f, 0.75f);
        public static float BK(string e) => BanKinh.TryGetValue(e, out var r) ? r : 0.42f;

        // bảng màu và bán kính van der Waals theo trình xem 3Dmol của CHEMISTRY (app.js layMau3D)
        static readonly Dictionary<string, Color> MauChem = new Dictionary<string, Color>
        {
            {"C", new Color32(0x2c, 0x30, 0x38, 0xff)}, {"H", new Color32(0xf4, 0xf4, 0xf5, 0xff)}, {"O", new Color32(0xe6, 0x39, 0x46, 0xff)},
            {"N", new Color32(0x3a, 0x86, 0xff, 0xff)}, {"S", new Color32(0xff, 0xb7, 0x03, 0xff)}, {"Cl", new Color32(0x06, 0xd6, 0xa0, 0xff)},
            {"Br", new Color32(0x9d, 0x02, 0x08, 0xff)}, {"F", new Color32(0x70, 0xe0, 0x00, 0xff)}, {"P", new Color32(0xfb, 0x85, 0x00, 0xff)},
            {"I", new Color32(0x72, 0x09, 0xb7, 0xff)},
        };
        static readonly Dictionary<string, float> BanKinhVdW = new Dictionary<string, float>
        {
            {"H", 1.2f}, {"C", 1.7f}, {"N", 1.55f}, {"O", 1.52f}, {"F", 1.47f}, {"Cl", 1.75f}, {"Br", 1.85f}, {"I", 1.98f}, {"S", 1.8f}, {"P", 1.8f},
        };
        public static Color MauCH(string e) => MauChem.TryGetValue(e, out var c) ? c : Mau(e);
        public static float VdW(string e) => BanKinhVdW.TryGetValue(e, out var r) ? r : 1.7f;

        void DamBaoTaiNguyen()
        {
            // nguyên tử luôn tròn mịn (ít nhất 48 mảnh): mỗi phân tử chỉ vài chục quả cầu, vẽ instancing nên không tốn
            int n = Mathf.Max(48, HardwareProfile.SoManhCau);
            if (cau == null || manhCau != n)
            {
                if (cau != null) Destroy(cau);
                if (tru != null) Destroy(tru);
                if (cauE != null) Destroy(cauE);
                cau = TaoCau(n, n / 2);
                cauE = TaoCau(24, 12);
                tru = TaoTru(Mathf.Max(8, n / 2));
                manhCau = n;
            }
            if (dia == null) dia = TaoDia(48);
            if (matGoc == null)
            {
                matGoc = Resources.Load<Material>("CoCheVe/NguyenTu");
                matE = Resources.Load<Material>("CoCheVe/Electron");
                matChan = Resources.Load<Material>("CoCheVe/ManChan");
                if (matGoc == null) matGoc = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
                if (matE == null) matE = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { enableInstancing = true };
                if (matChan == null) matChan = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                matE = new Material(matE) { enableInstancing = true };
                matE.SetColor("_BaseColor", new Color(1f, 0.86f, 0.2f));
                matChan = new Material(matChan);
                // liên kết xám sáng và quầng vàng quanh electron như bản demo đã duyệt
                matLk = new Material(matGoc) { enableInstancing = true, name = "CoChe_LienKet" };
                matLk.SetColor("_BaseColor", new Color32(0xa7, 0xaf, 0xbd, 0xff));
                matLk.SetColor("_Color", new Color32(0xa7, 0xaf, 0xbd, 0xff));
                matQuang = new Material(matChan) { enableInstancing = true, name = "CoChe_Quang" };
                matQuang.SetColor("_BaseColor", new Color(1f, 0.82f, 0.4f, 0.16f));
            }
        }

        Material MatNguyenTu(string e)
        {
            if (matNt.TryGetValue(e, out var m)) return m;
            m = new Material(matGoc) { enableInstancing = true, name = "CoChe_" + e };
            m.SetColor("_BaseColor", KieuChemistry ? MauCH(e) : Mau(e));
            m.SetColor("_Color", KieuChemistry ? MauCH(e) : Mau(e));
            m.SetFloat("_Smoothness", 0.62f);
            matNt[e] = m;
            return m;
        }

        static float Muot(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // ---- cho lớp nhãn trên màn (CoCheMoDun): vị trí, nguyên tố, điện tích của khung đang vẽ
        public IReadOnlyDictionary<string, Vector3> ViTriNguyenTu => viTri;
        public string NguyenTo(string id) => KichBan == null ? "C" : Nguyen(id);
        public float CoNguyenTu(string id) => coNt.TryGetValue(id, out var c) ? c : 0f;
        public Vector3 TheGioi(Vector3 p) => W(p);
        public int DienTichHienTai(string id)
        {
            if (KichBan == null || KichBan.TrangThai.Count == 0) return 0;
            TrangThai3D s;
            if (DangNhanhSai && NhanhHienTai != null) s = KichBan.TrangThai[Mathf.Clamp(NhanhHienTai.TuTrangThai, 0, KichBan.TrangThai.Count - 1)];
            else { var b = BuocHienTai; if (b == null) return 0; s = KichBan.TrangThai[T < 0.6f ? b.Tu : b.Den]; }
            return s.DienTich.TryGetValue(id, out var d) ? d : 0;
        }

        Vector3 W(Vector3 p)
        {
            var q = Xoay * p;
            q.z *= Mathf.Max(Phang, 0.02f);
            return Tam + q;
        }

        // ------------------------------------------------------------------ vẽ một khung hình
        void Ve()
        {
            DamBaoTaiNguyen();
            foreach (var l in nhom.Values) l.Clear();
            lk.Clear();
            hat.Clear();
            viTri.Clear();
            coNt.Clear();

            TrangThai3D a, b;
            float m;
            List<MuiTen> muiTen;
            float tE;
            Dictionary<string, Vector3> dichNhanh = null;
            if (DangNhanhSai && NhanhHienTai != null)
            {
                a = b = KichBan.TrangThai[Mathf.Clamp(NhanhHienTai.TuTrangThai, 0, KichBan.TrangThai.Count - 1)];
                m = Muot(tNhanh / 0.5f);
                dichNhanh = NhanhHienTai.ToaDo;
                muiTen = NhanhHienTai.MuiTen;
                tE = Mathf.Clamp01((tNhanh - 0.4f) / 0.5f);
            }
            else
            {
                var bu = BuocHienTai;
                if (bu == null) return;
                a = KichBan.TrangThai[bu.Tu];
                b = KichBan.TrangThai[bu.Den];
                m = Muot((T - 0.35f) / 0.65f);
                muiTen = bu.MuiTen;
                tE = T / 0.55f;
            }

            // vị trí, cỡ nguyên tử
            if (dichNhanh != null)
            {
                foreach (var kv in dichNhanh)
                {
                    bool coSan = a.ToaDo.TryGetValue(kv.Key, out var p0);
                    viTri[kv.Key] = coSan ? Vector3.Lerp(p0, kv.Value, m) : kv.Value;
                    coNt[kv.Key] = coSan ? 1f : m;
                }
            }
            else
            {
                foreach (var kv in a.ToaDo)
                {
                    if (b.ToaDo.TryGetValue(kv.Key, out var p1)) { viTri[kv.Key] = Vector3.Lerp(kv.Value, p1, m); coNt[kv.Key] = 1f; }
                    else { viTri[kv.Key] = kv.Value; coNt[kv.Key] = 1f - m; }
                }
                foreach (var kv in b.ToaDo)
                    if (!a.ToaDo.ContainsKey(kv.Key)) { viTri[kv.Key] = kv.Value; coNt[kv.Key] = m; }
            }

            SoNguyenTuDangVe = 0;
            foreach (var kv in viTri)
            {
                float s = coNt[kv.Key];
                if (s <= 0.01f) continue;
                string e = Nguyen(kv.Key);
                if (AnH && e == "H") s *= 0.35f;
                float d = (KieuChemistry ? VdW(e) * 0.27f : BK(e)) * 2f * s;
                if (!nhom.TryGetValue(e, out var l)) nhom[e] = l = new List<Matrix4x4>();
                l.Add(Matrix4x4.TRS(W(kv.Value), Quaternion.identity, new Vector3(d, d, d)));
                SoNguyenTuDangVe++;
            }

            // liên kết: có ở cả hai → đổi bậc giữa chừng; chỉ ở sau → mọc dần; chỉ ở trước → co lại
            var bacA = BangLk(a);
            var bacB = dichNhanh != null ? bacA : BangLk(b);
            foreach (var kv in bacA)
            {
                bool coB = bacB.TryGetValue(kv.Key, out int bb);
                VeLienKet(kv.Key, coB ? (m < 0.5f ? kv.Value : bb) : kv.Value, coB ? 1f : 1f - m);
            }
            foreach (var kv in bacB)
                if (!bacA.ContainsKey(kv.Key)) VeLienKet(kv.Key, kv.Value, m);

            // electron bay theo mũi tên (18/11, theo góp ý): cặp electron bay như một khối, hai hạt xoay quanh nhau quanh đường bay,
            // tăng tốc – hãm êm (smootherstep), lớn dần khi rời nguyên tử và thu lại khi tới đích; vệt sáng mờ dần phía sau (vẽ trong suốt).
            int toiDa = Mathf.Max(8, HardwareProfile.SoHatToiDa);
            quang.Clear();
            if (tE > 0f && tE < 1.0f && muiTen != null)
            {
                int vet = Mathf.Clamp(toiDa / Mathf.Max(1, muiTen.Count * 2), 2, 5);
                float quay = Time.unscaledTime * 7.5f;
                foreach (var mt in muiTen)
                {
                    if (!DiemMuiTen(mt, out var s0, out var c0, out var e0)) continue;
                    float dung = DangNhanhSai && NhanhHienTai != null ? DiemChan(s0, c0, e0) : 1f;
                    float u = Mathf.Min(MuotHon(Mathf.Clamp01(tE)), dung);
                    if (u <= 0f) continue;
                    Vector3 huong = (Bezier(s0, c0, e0, Mathf.Min(1f, u + 0.02f)) - Bezier(s0, c0, e0, Mathf.Max(0f, u - 0.02f))).normalized;
                    if (huong.sqrMagnitude < 1e-6f) huong = (e0 - s0).normalized;
                    Vector3 vg1 = Vector3.Cross(huong, Mathf.Abs(Vector3.Dot(huong, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 vg2 = Vector3.Cross(huong, vg1);
                    float hien = Mathf.SmoothStep(0f, 1f, u / 0.12f) * Mathf.Lerp(1f, 0.82f, Mathf.SmoothStep(0f, 1f, (u - 0.85f) / 0.15f));
                    for (int k = 0; k < mt.SoE && hat.Count < toiDa; k++)
                    {
                        float pha = quay + k * Mathf.PI;
                        Vector3 lech = mt.SoE == 2 ? (vg1 * Mathf.Cos(pha) + vg2 * Mathf.Sin(pha)) * 0.11f : Vector3.zero;
                        Vector3 p = Bezier(s0, c0, e0, u) + lech;
                        float co = 0.16f * hien;
                        hat.Add(Matrix4x4.TRS(W(p), Quaternion.identity, Vector3.one * co));
                        quang.Add(Matrix4x4.TRS(W(p), Quaternion.identity, Vector3.one * co * 3.0f));
                        for (int v = 1; v <= vet; v++)                       // vệt: cầu trong suốt nhỏ dần theo đường đã đi
                        {
                            float uu = u - v * 0.028f;
                            if (uu <= 0f) break;
                            float g = 1f - v / (float)(vet + 1);
                            Vector3 pv = Bezier(s0, c0, e0, uu) + lech * g;
                            quang.Add(Matrix4x4.TRS(W(pv), Quaternion.identity, Vector3.one * co * (0.6f + 1.6f * g)));
                        }
                    }
                }
            }
            SoHatDangVe = hat.Count;

            // vẽ
            foreach (var kv in nhom) VeNhom(MatNguyenTu(kv.Key), cau, kv.Value);
            VeNhom(matLk != null ? matLk : MatNguyenTu("C"), tru, lk);
            VeNhom(matE, cauE, hat);
            if (matQuang != null) VeNhom(matQuang, cauE, quang);
            if (DangNhanhSai && NhanhHienTai != null && NhanhHienTai.BanKinhChan > 0f)
            {
                var c = matChan.GetColor("_BaseColor");
                c.a = 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                matChan.SetColor("_BaseColor", c);
                var n = Xoay * NhanhHienTai.PhapTuyen;
                n.z *= Mathf.Max(Phang, 0.02f);
                var rp = Rp(matChan);
                float r = NhanhHienTai.BanKinhChan * 2f;
                Graphics.RenderMesh(rp, dia, 0, Matrix4x4.TRS(W(NhanhHienTai.TamChan), Quaternion.LookRotation(n.sqrMagnitude > 1e-6f ? n : Vector3.forward), new Vector3(r, r, r)));
            }
        }

        string Nguyen(string id)
        {
            if (KichBan.NguyenTu.TryGetValue(id, out var e)) return e;
            if (NhanhHienTai != null && NhanhHienTai.NguyenTuThem.TryGetValue(id, out e)) return e;
            return id.StartsWith("H") ? "H" : "C";
        }

        static Dictionary<string, int> BangLk(TrangThai3D s)
        {
            var d = new Dictionary<string, int>();
            foreach (var l in s.LienKet) d[string.CompareOrdinal(l.A, l.B) < 0 ? l.A + "|" + l.B : l.B + "|" + l.A] = l.Bac;
            return d;
        }

        void VeLienKet(string khoa, int bac, float g)
        {
            if (g <= 0.01f) return;
            int k = khoa.IndexOf('|');
            string ia = khoa.Substring(0, k), ib = khoa.Substring(k + 1);
            if (!viTri.TryGetValue(ia, out var pa) || !viTri.TryGetValue(ib, out var pb)) return;
            if (AnH && (Nguyen(ia) == "H" || Nguyen(ib) == "H")) return;
            Vector3 wa = W(pa), wb = W(pb), d = wb - wa;
            float dai = d.magnitude;
            if (dai < 1e-4f) return;
            var quay = Quaternion.FromToRotation(Vector3.up, d / dai);
            var ngang = Vector3.Cross(d / dai, Vector3.forward);
            if (ngang.sqrMagnitude < 1e-6f) ngang = Vector3.right;
            ngang.Normalize();
            float r = (KieuChemistry ? 0.14f : 0.075f) * g * (bac > 1 ? 0.62f : 1f);
            float[] lech = bac >= 3 ? new[] { -0.14f, 0f, 0.14f } : bac == 2 ? new[] { -0.09f, 0.09f } : new[] { 0f };
            foreach (var o in lech)
                lk.Add(Matrix4x4.TRS(wa + ngang * o, quay, new Vector3(r, dai, r)));
        }

        static float MuotHon(float x) => x * x * x * (x * (6f * x - 15f) + 10f);

        static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u) => (1 - u) * (1 - u) * a + 2 * u * (1 - u) * c + u * u * b;

        Vector3 Tim(int map) => viTri.TryGetValue("m" + map, out var p) ? p : Vector3.zero;

        bool DiemMuiTen(MuiTen mt, out Vector3 s, out Vector3 c, out Vector3 e)
        {
            s = c = e = Vector3.zero;
            if (mt.Tu == null || mt.Den == null || mt.Tu.Length == 0 || mt.Den.Length == 0) return false;
            foreach (var x in mt.Tu) if (!viTri.ContainsKey("m" + x)) return false;
            foreach (var x in mt.Den) if (!viTri.ContainsKey("m" + x)) return false;
            Vector3 tam = Vector3.zero;
            foreach (var p in viTri.Values) tam += p;
            tam /= Mathf.Max(1, viTri.Count);
            if (mt.TuLoai == "lien_ket" && mt.Tu.Length >= 2) s = (Tim(mt.Tu[0]) + Tim(mt.Tu[1])) * 0.5f;
            else
            {
                var p = Tim(mt.Tu[0]);
                var huong = (p - tam).sqrMagnitude > 1e-4f ? (p - tam).normalized : Vector3.up;
                s = p + huong * 0.45f;
            }
            if (mt.DenLoai == "lien_ket" && mt.Den.Length >= 2) e = (Tim(mt.Den[0]) + Tim(mt.Den[1])) * 0.5f;
            else
            {
                var p = Tim(mt.Den[0]);
                e = p + (s - p).normalized * 0.3f;
            }
            var giua = (s + e) * 0.5f;
            var phap = Vector3.Cross(e - s, Vector3.forward);
            if (phap.sqrMagnitude < 1e-6f) phap = Vector3.up;
            phap = phap.normalized;
            if (Vector3.Dot(phap, giua - tam) < 0f) phap = -phap;
            c = giua + phap * Mathf.Max(0.6f, (e - s).magnitude * 0.45f);
            return true;
        }

        /// <summary>Tham số đường cong nơi electron chạm màn chắn (dừng lại trước nó).</summary>
        float DiemChan(Vector3 s, Vector3 c, Vector3 e)
        {
            var n = NhanhHienTai.PhapTuyen;
            var t0 = NhanhHienTai.TamChan;
            float truoc = Vector3.Dot(s - t0, n);
            for (int i = 1; i <= 40; i++)
            {
                float u = i / 40f;
                if (Mathf.Sign(Vector3.Dot(Bezier(s, c, e, u) - t0, n)) != Mathf.Sign(truoc)) return Mathf.Max(0f, u - 0.04f);
            }
            return 0.85f;
        }

        RenderParams Rp(Material mat) => new RenderParams(mat)
        {
            camera = Cam,
            layer = LopVe,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(Tam, Vector3.one * 400f),
        };

        void VeNhom(Material mat, Mesh mesh, List<Matrix4x4> ds)
        {
            if (ds.Count == 0 || mat == null || mesh == null) return;
            var rp = Rp(mat);
            for (int i = 0; i < ds.Count; i += 1000)
                Graphics.RenderMeshInstanced(rp, mesh, 0, ds, Mathf.Min(1000, ds.Count - i), i);
        }

        // ------------------------------------------------------------------ lưới
        static Mesh TaoCau(int kinh, int vi)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= vi; i++)
            {
                float th = Mathf.PI * i / vi;
                for (int j = 0; j <= kinh; j++)
                {
                    float ph = 2f * Mathf.PI * j / kinh;
                    v.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f);
                }
            }
            for (int i = 0; i < vi; i++)
                for (int j = 0; j < kinh; j++)
                {
                    int a = i * (kinh + 1) + j, b = a + kinh + 1;
                    tri.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var m = new Mesh { name = "CoChe_Cau" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            var n = new List<Vector3>();
            foreach (var p in v) n.Add(p.normalized);
            m.SetNormals(n);
            return m;
        }

        static Mesh TaoTru(int canh)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();
            for (int j = 0; j <= canh; j++)
            {
                float ph = 2f * Mathf.PI * j / canh;
                var r = new Vector3(Mathf.Cos(ph), 0f, Mathf.Sin(ph));
                v.Add(r); v.Add(r + Vector3.up);
                n.Add(r); n.Add(r);
            }
            for (int j = 0; j < canh; j++)
            {
                int a = 2 * j;
                tri.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            var m = new Mesh { name = "CoChe_Tru" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(tri, 0);
            return m;
        }

        static Mesh TaoDia(int canh)
        {
            var v = new List<Vector3> { Vector3.zero };
            var tri = new List<int>();
            for (int j = 0; j <= canh; j++)
            {
                float ph = 2f * Mathf.PI * j / canh;
                v.Add(new Vector3(Mathf.Cos(ph), Mathf.Sin(ph), 0f) * 0.5f);
            }
            for (int j = 1; j <= canh; j++) tri.AddRange(new[] { 0, j, j + 1, 0, j + 1, j });
            var m = new Mesh { name = "CoChe_Dia" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            return m;
        }

        void OnDestroy()
        {
            if (cau != null) Destroy(cau);
            if (tru != null) Destroy(tru);
            if (dia != null) Destroy(dia);
            if (cauE != null) Destroy(cauE);
            foreach (var m in matNt.Values) Destroy(m);
            if (matE != null) Destroy(matE);         // matE, matChan là bản sao tạo bằng new Material(...)
            if (matChan != null) Destroy(matChan);
            if (matLk != null) Destroy(matLk);
            if (matQuang != null) Destroy(matQuang);
        }
    }
}
