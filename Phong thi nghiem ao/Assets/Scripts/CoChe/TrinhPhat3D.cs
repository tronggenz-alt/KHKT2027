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
        /// <summary>Nguyên tử/liên kết tự phát sáng nhẹ (emission) – dùng khi cảnh có thể không có đèn (bảng lớn trên menu).</summary>
        public float TuSang;
        public float TocXoay = 24f;
        /// <summary>Tự lắc góc nhìn chậm quanh góc tốt nhất (mặc định bật cho cơ chế).</summary>
        public bool LacGoc = true;
        /// <summary>Hiện electron không liên kết như cấu trúc Lewis (cặp = 2 chấm, electron lẻ = 1 chấm) quanh mỗi nguyên tử.</summary>
        public bool HienE;
        public int SoChamLewis { get; private set; }
        readonly List<Matrix4x4> lewis = new List<Matrix4x4>();
        readonly Dictionary<string, int> tongBac = new Dictionary<string, int>();
        readonly Dictionary<string, List<Vector3>> huongLk = new Dictionary<string, List<Vector3>>();
        readonly List<Vector3> daChon = new List<Vector3>(8);
        static readonly Dictionary<string, int> HoaTri = new Dictionary<string, int>
        { {"H", 1}, {"C", 4}, {"N", 5}, {"O", 6}, {"F", 7}, {"Cl", 7}, {"Br", 7}, {"I", 7}, {"S", 6}, {"P", 5} };
        static readonly Vector3[] HuongUng = TaoHuongThu(26);
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
            KhoangCach = khoangDich;
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
            bangLkDem.Clear(); hGanC.Clear();
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

        readonly HashSet<int> daPhatNhanh = new HashSet<int>();

        // ---- Ẩn H chỉ áp cho H gắn vào C (mạch chính); H của tác nhân (H₂O, NH₃, OH⁻, HBr…) vẫn hiện
        TrangThai3D trangDang;
        readonly Dictionary<TrangThai3D, HashSet<string>> hGanC = new Dictionary<TrangThai3D, HashSet<string>>();
        public bool LaHAn(string id)
        {
            if (!AnH || trangDang == null || KichBan == null) return false;
            var mtDang = DangNhanhSai && NhanhHienTai != null ? NhanhHienTai.MuiTen : BuocHienTai?.MuiTen;
            if (mtDang != null)                                     // H tham gia mũi tên của bước đang xem: luôn hiện
                foreach (var m in mtDang)
                {
                    if (m.Tu != null) foreach (var x in m.Tu) if (id == "m" + x) return false;
                    if (m.Den != null) foreach (var x in m.Den) if (id == "m" + x) return false;
                }
            if (!hGanC.TryGetValue(trangDang, out var ds))
            {
                ds = new HashSet<string>();
                foreach (var l in trangDang.LienKet)
                {
                    string ea = Nguyen(l.A), eb = Nguyen(l.B);
                    if (ea == "H" && eb == "C") ds.Add(l.A);
                    else if (eb == "H" && ea == "C") ds.Add(l.B);
                }
                hGanC[trangDang] = ds;
            }
            return ds.Contains(id);
        }
        public int SoNhanhDaPhat => daPhatNhanh.Count;

        public void DenBuoc(int i, bool phat = false)
        {
            daPhatNhanh.Clear();                                   // xem lại bước thì phát lại cả hướng sai
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
                // 20/11: tự lắc chậm ±18° quanh góc nhìn tốt nhất của bước – thấy chiều sâu, dễ hình dung chuyển động
                var dich = LacGoc ? Quaternion.AngleAxis(18f * Mathf.Sin(Time.unscaledTime * 0.45f), Vector3.up) * xoayDich : xoayDich;
                if (!TuXoay) Xoay = Quaternion.Slerp(Xoay, dich, k);
                tamNay = Vector3.Lerp(tamNay, tamDich, k);
                KhoangCach = Mathf.Lerp(KhoangCach, khoangDich, k);
            }
            float dt = Time.unscaledDeltaTime * TocDo;
            // 20/11: tới bước có hướng sai trong lúc PHÁT thì tự phát hướng sai trước (electron bị quả cầu chặn), xong mới đi hướng đúng
            if (DangPhat && !DangNhanhSai && T <= 0f && KichBan.NhanhSai.Count > 0 && BuocHienTai != null)
                for (int i = 0; i < KichBan.NhanhSai.Count; i++)
                    if (KichBan.NhanhSai[i].TuTrangThai == BuocHienTai.Tu && daPhatNhanh.Add(i))
                    {
                        NhanhHienTai = KichBan.NhanhSai[i];
                        DangNhanhSai = true;
                        tNhanh = 0f;
                        DoiBuoc?.Invoke();
                        break;
                    }
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
            {   // zoom theo độ dài cú cuộn: trackpad (nhiều nhịp nhỏ) mượt, con lăn chuột (nhịp lớn) vẫn vừa phải
                KhoangCach = Mathf.Clamp(KhoangCach * Mathf.Exp(-Mathf.Clamp(cuon, -30f, 30f) * 0.012f), 3f, 60f);
                TuDongGoc = false;
            }
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
                // căn theo khung chữ nhật của các chất SAU KHI xoay về góc nhìn đã chọn: ngang chia tỉ lệ khung, dọc theo FOV
                float nuaX = 1.2f, nuaY = 1.2f;
                foreach (var p in ds)
                {
                    var q = xoayDich * (p - tamDich);
                    nuaX = Mathf.Max(nuaX, Mathf.Abs(q.x) + 0.6f);
                    nuaY = Mathf.Max(nuaY, Mathf.Abs(q.y) + 0.6f);
                }
                float tanf = Mathf.Tan((Cam != null ? Cam.fieldOfView : 32f) * 0.5f * Mathf.Deg2Rad);
                float tl = Cam != null ? Mathf.Max(0.3f, Cam.aspect) : 1.6f;
                khoangDich = Mathf.Clamp(Mathf.Max(nuaY / tanf, nuaX / (tanf * tl)) * 1.3f + 2.5f, 6f, 60f);   // chừa lề: không quá sát
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
            // 20/11: màu tươi, sáng để nổi trên nền đen (C xám sáng thay vì xám đậm chìm vào nền)
            {"H", new Color(0.97f, 0.97f, 0.99f)}, {"C", new Color(0.58f, 0.62f, 0.68f)}, {"N", new Color(0.30f, 0.50f, 1.00f)},
            {"O", new Color(1.00f, 0.27f, 0.25f)}, {"F", new Color(0.56f, 0.95f, 0.31f)}, {"Cl", new Color(0.20f, 0.90f, 0.45f)},
            {"Br", new Color(0.86f, 0.30f, 0.22f)}, {"I", new Color(0.58f, 0.0f, 0.58f)}, {"S", new Color(0.95f, 0.85f, 0.20f)},
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
            {"C", new Color32(0x9a, 0xa3, 0xad, 0xff)}, {"H", new Color32(0xf4, 0xf4, 0xf5, 0xff)}, {"O", new Color32(0xe6, 0x39, 0x46, 0xff)},
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
                matQuang.SetColor("_BaseColor", new Color(1f, 0.82f, 0.4f, 0.10f));
            }
        }

        Material MatNguyenTu(string e)
        {
            if (matNt.TryGetValue(e, out var m)) return m;
            m = new Material(matGoc) { enableInstancing = true, name = "CoChe_" + e };
            m.SetColor("_BaseColor", KieuChemistry ? MauCH(e) : Mau(e));
            m.SetColor("_Color", KieuChemistry ? MauCH(e) : Mau(e));
            m.SetFloat("_Smoothness", 0.62f);
            float sang = TuSang > 0f ? TuSang : 0.12f;
            if (sang > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", (KieuChemistry ? MauCH(e) : Mau(e)) * sang);
            }
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

            trangDang = a;
            SoNguyenTuDangVe = 0;
            foreach (var kv in viTri)
            {
                float s = coNt[kv.Key];
                if (s <= 0.01f) continue;
                string e = Nguyen(kv.Key);
                if (LaHAn(kv.Key)) continue;                       // Ẩn H: chỉ H gắn vào C (H của H₂O, NH₃, OH⁻… vẫn hiện)
                float d = (KieuChemistry ? VdW(e) * 0.27f : BK(e)) * 2f * s;
                if (!nhom.TryGetValue(e, out var l)) nhom[e] = l = new List<Matrix4x4>();
                l.Add(Matrix4x4.TRS(W(kv.Value), Quaternion.identity, new Vector3(d, d, d)));
                SoNguyenTuDangVe++;
            }

            // liên kết: có ở cả hai → đổi bậc giữa chừng; chỉ ở sau → mọc dần; chỉ ở trước → co lại
            var bacA = BangLkNho(a);
            var bacB = dichNhanh != null ? bacA : BangLkNho(b);
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
            VeMuiTen3D(muiTen);
            if (tE > 0f && tE < 1.0f && muiTen != null)
            {
                int vet = Mathf.Clamp(toiDa / Mathf.Max(1, muiTen.Count * 2), 2, 4);
                float quay = Time.unscaledTime * 2.6f;     // 20/11: cặp e xoay chậm quanh đường bay – nhìn rõ 2 hạt, không thành một cục
                foreach (var mt in muiTen)
                {
                    if (!DiemMuiTen(mt, out var s0, out var c0, out var e0)) continue;
                    float dung = DangNhanhSai && NhanhHienTai != null ? DiemChan(s0, c0, e0) : 1f;
                    float u = Mathf.Min(0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(tE)), dung);   // dạng sin: tăng–giảm tốc êm, đều hơn
                    if (u <= 0f) continue;
                    Vector3 huong = (Bezier(s0, c0, e0, Mathf.Min(1f, u + 0.02f)) - Bezier(s0, c0, e0, Mathf.Max(0f, u - 0.02f))).normalized;
                    if (huong.sqrMagnitude < 1e-6f) huong = (e0 - s0).normalized;
                    Vector3 vg1 = Vector3.Cross(huong, Mathf.Abs(Vector3.Dot(huong, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 vg2 = Vector3.Cross(huong, vg1);
                    float hien = Mathf.SmoothStep(0f, 1f, u / 0.12f) * Mathf.Lerp(1f, 0.82f, Mathf.SmoothStep(0f, 1f, (u - 0.85f) / 0.15f));
                    for (int k = 0; k < mt.SoE && hat.Count < toiDa; k++)
                    {
                        // 20/11: đơn giản – mỗi electron một chấm vàng nhỏ, cặp e là 2 chấm cố định hai bên đường bay, chỉ bay từ A sang B
                        Vector3 nhin = Quaternion.Inverse(Xoay) * Vector3.forward;                 // hướng nhìn trong toạ độ mô hình
                        Vector3 ben = Vector3.Cross(huong, nhin); ben = ben.sqrMagnitude > 1e-4f ? ben.normalized : vg1;   // tách trên mặt màn hình
                        Vector3 lech = mt.SoE == 2 ? ben * (k == 0 ? 0.13f : -0.13f) : Vector3.zero;
                        hat.Add(Matrix4x4.TRS(W(Bezier(s0, c0, e0, u) + lech), Quaternion.identity, Vector3.one * 0.13f * hien));
                    }
                }
            }
            SoHatDangVe = hat.Count;

            // vẽ
            foreach (var kv in nhom) VeNhom(MatNguyenTu(kv.Key), cau, kv.Value);
            VeNhom(matLk != null ? matLk : MatNguyenTu("C"), tru, lk);
            VeNhom(matE, cauE, hat);
            VeLewis(m < 0.5f ? bacA : bacB, m < 0.5f ? a : b);
            if (lewis.Count > 0) VeNhom(matE, cauE, lewis);
            if (matQuang != null) VeNhom(matQuang, cauE, quang);
            if (DangNhanhSai && NhanhHienTai != null && NhanhHienTai.BanKinhChan > 0f)
            {
                // 20/11, theo bản demo đã duyệt (nguyen_mau/co_che_3d/sn2_sn1.html): QUẢ CẦU chắn đỏ trong suốt bán kính 2,3 Å bao quanh
                // nguyên tử bị tấn công (cả các nhóm che chắn) + lưới khung đa diện; đậm dần khi electron chạm tới.
                float vaCham = Mathf.Clamp01((tNhanh - 0.6f) / 0.2f);
                float nhip = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
                Vector3 tamM = NhanhHienTai.TamChan - NhanhHienTai.PhapTuyen * 1.55f;      // vị trí nguyên tử đích
                const float R = 2.3f;
                matChan.SetColor("_BaseColor", new Color(1f, 0.23f, 0.23f, (0.06f + 0.12f * vaCham) * nhip));
                Graphics.RenderMesh(Rp(matChan), cau, 0, Matrix4x4.TRS(W(tamM), Quaternion.identity, Vector3.one * R * 2f));
                if (matLuoi == null) matLuoi = new Material(matChan) { name = "CoChe_LuoiChan", enableInstancing = true };
                matLuoi.SetColor("_BaseColor", new Color(1f, 0.42f, 0.42f, (0.3f + 0.45f * vaCham) * nhip));
                luoiChan.Clear();
                foreach (var (ca, cb) in CanhCau)
                {
                    Vector3 wa = W(tamM + ca * R * 1.005f), wb = W(tamM + cb * R * 1.005f), d = wb - wa;
                    float dai = d.magnitude;
                    if (dai < 1e-4f) continue;
                    luoiChan.Add(Matrix4x4.TRS(wa, Quaternion.FromToRotation(Vector3.up, d / dai), new Vector3(0.014f, dai, 0.014f)));
                }
                VeNhom(matLuoi, tru, luoiChan);
            }
        }

        Material matLuoi;
        readonly List<Matrix4x4> luoiChan = new List<Matrix4x4>(130);
        static readonly (Vector3, Vector3)[] CanhCau = TaoCanhCau();

        /// <summary>Cạnh của hình cầu 20 mặt chia 1 lần (42 đỉnh, 120 cạnh) – lưới khung của quả cầu chắn.</summary>
        static (Vector3, Vector3)[] TaoCanhCau()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3> { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                                        new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var giua = new Dictionary<long, int>();
            int G(int a, int b) { long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (giua.TryGetValue(k, out int i)) return i; v.Add(((v[a] + v[b]) * 0.5f).normalized); giua[k] = v.Count - 1; return v.Count - 1; }
            var canh = new HashSet<long>();
            var ds = new List<(Vector3, Vector3)>();
            void C(int a, int b) { long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (canh.Add(k)) ds.Add((v[a], v[b])); }
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2], ab = G(a, b), bc = G(b, c), ca = G(c, a);
                C(a, ab); C(ab, b); C(b, bc); C(bc, c); C(c, ca); C(ca, a); C(ab, bc); C(bc, ca); C(ca, ab);
            }
            return ds.ToArray();
        }

        string Nguyen(string id)
        {
            if (KichBan.NguyenTu.TryGetValue(id, out var e)) return e;
            if (NhanhHienTai != null && NhanhHienTai.NguyenTuThem.TryGetValue(id, out e)) return e;
            return id.StartsWith("H") ? "H" : "C";
        }

        // tối ưu 20/11: bảng liên kết của mỗi trạng thái chỉ dựng 1 lần (trước đây dựng lại 2 lần mỗi khung hình → rác bộ nhớ)
        readonly Dictionary<TrangThai3D, Dictionary<string, int>> bangLkDem = new Dictionary<TrangThai3D, Dictionary<string, int>>();
        Dictionary<string, int> BangLkNho(TrangThai3D s)
        {
            if (!bangLkDem.TryGetValue(s, out var d)) bangLkDem[s] = d = BangLk(s);
            return d;
        }

        static readonly Dictionary<(string, int), string> nhanDem = new Dictionary<(string, int), string>();
        /// <summary>Chữ nhãn nguyên tử (ký hiệu + điện tích ⊕/⊖), lưu đệm để không tạo chuỗi mới mỗi khung hình.</summary>
        public static string NhanNT(string e, int dt)
        {
            if (nhanDem.TryGetValue((e, dt), out var t)) return t;
            t = e + (dt == 0 ? "" : dt > 0 ? (dt > 1 ? dt + "⊕" : "⊕") : (dt < -1 ? (-dt) + "⊖" : "⊖"));
            nhanDem[(e, dt)] = t;
            return t;
        }

        static Dictionary<string, int> BangLk(TrangThai3D s)
        {
            var d = new Dictionary<string, int>();
            foreach (var l in s.LienKet) d[string.CompareOrdinal(l.A, l.B) < 0 ? l.A + "|" + l.B : l.B + "|" + l.A] = l.Bac;
            return d;
        }

        /// <summary>Cặp electron không liên kết: số e = hoá trị − tổng bậc liên kết − điện tích; đặt theo hướng xa các liên kết nhất.</summary>
        void VeLewis(Dictionary<string, int> bac, TrangThai3D st)
        {
            lewis.Clear();
            SoChamLewis = 0;
            if (!HienE || st == null) return;
            tongBac.Clear();
            foreach (var l in huongLk.Values) l.Clear();
            foreach (var kv in bac)
            {
                int k = kv.Key.IndexOf('|');
                string ia = kv.Key.Substring(0, k), ib = kv.Key.Substring(k + 1);
                tongBac[ia] = (tongBac.TryGetValue(ia, out var x) ? x : 0) + kv.Value;
                tongBac[ib] = (tongBac.TryGetValue(ib, out var y) ? y : 0) + kv.Value;
                if (viTri.TryGetValue(ia, out var pa) && viTri.TryGetValue(ib, out var pb) && (pb - pa).sqrMagnitude > 1e-6f)
                {
                    if (!huongLk.TryGetValue(ia, out var la)) huongLk[ia] = la = new List<Vector3>(4);
                    if (!huongLk.TryGetValue(ib, out var lb)) huongLk[ib] = lb = new List<Vector3>(4);
                    la.Add((pb - pa).normalized); lb.Add((pa - pb).normalized);
                }
            }
            Vector3 nhin = Quaternion.Inverse(Xoay) * Vector3.forward;
            foreach (var kv in viTri)
            {
                if (coNt.TryGetValue(kv.Key, out var c) && c < 0.5f) continue;
                string e = Nguyen(kv.Key);
                if (LaHAn(kv.Key)) continue;
                if (!HoaTri.TryGetValue(e, out int ht)) continue;
                int n = ht - (tongBac.TryGetValue(kv.Key, out var tb) ? tb : 0) - (st.DienTich.TryGetValue(kv.Key, out var dt) ? dt : 0);
                if (n <= 0) continue;
                int cap = n / 2, le = n % 2;
                daChon.Clear();
                if (huongLk.TryGetValue(kv.Key, out var hl)) daChon.AddRange(hl);
                int coSan = daChon.Count;
                float r = (KieuChemistry ? VdW(e) * 0.27f : BK(e)) + 0.17f;
                for (int i = 0; i < cap + le; i++)
                {
                    Vector3 tot = Vector3.up; float diem = -9f;
                    foreach (var h in HuongUng)
                    {
                        float gan = 1f;                                   // cos góc với hướng đã dùng gần nhất (nhỏ = xa)
                        foreach (var d in daChon) gan = Mathf.Min(gan, -Vector3.Dot(h, d));
                        float g = daChon.Count == 0 ? -Mathf.Abs(Vector3.Dot(h, nhin)) : gan - 0.3f * Mathf.Abs(Vector3.Dot(h, nhin));
                        if (g > diem) { diem = g; tot = h; }
                    }
                    daChon.Add(tot);
                    Vector3 tam = kv.Value + tot * r;
                    Vector3 ben = Vector3.Cross(tot, nhin); ben = ben.sqrMagnitude > 1e-4f ? ben.normalized * 0.065f : Vector3.zero;
                    bool don = i >= cap;                                  // electron lẻ (gốc tự do)
                    if (don) lewis.Add(Matrix4x4.TRS(W(tam), Quaternion.identity, Vector3.one * 0.085f));
                    else { lewis.Add(Matrix4x4.TRS(W(tam + ben), Quaternion.identity, Vector3.one * 0.08f)); lewis.Add(Matrix4x4.TRS(W(tam - ben), Quaternion.identity, Vector3.one * 0.08f)); }
                }
                SoChamLewis = lewis.Count;
                _ = coSan;
            }
        }

        void VeLienKet(string khoa, int bac, float g)
        {
            if (g <= 0.01f) return;
            int k = khoa.IndexOf('|');
            string ia = khoa.Substring(0, k), ib = khoa.Substring(k + 1);
            if (!viTri.TryGetValue(ia, out var pa) || !viTri.TryGetValue(ib, out var pb)) return;
            if (LaHAn(ia) || LaHAn(ib)) return;
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

        // ------------------------------------------------------------------ mũi tên cong 3D (20/11)
        // Như mũi tên trên giấy: đường cong trắng, MẢNH đi đúng đường bay của electron (từ liên kết / cặp e tới nguyên tử / liên kết),
        // đầu nón nhỏ ở đích. Hiện từ đầu bước, mờ dần khi liên kết đã chuyển xong; hướng sai thì dừng ở quả cầu chắn.
        Mesh non;
        Material matMuiTen;
        readonly List<Matrix4x4> thanMt = new List<Matrix4x4>(256), dauMt = new List<Matrix4x4>(8);
        public int SoDoanMuiTen => thanMt.Count;
        Mesh ongMt;
        readonly List<Vector3> dinhOng = new List<Vector3>(512);
        readonly List<int> tamGiacOng = new List<int>(3072);
        public bool HienMuiTen3D = true;

        void VeMuiTen3D(List<MuiTen> ds)
        {
            thanMt.Clear(); dauMt.Clear(); dinhOng.Clear(); tamGiacOng.Clear();
            if (!HienMuiTen3D || ds == null || ds.Count == 0 || matChan == null) return;
            float a = DangNhanhSai ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (T - 0.72f) / 0.25f);
            if (a <= 0.01f) return;
            if (non == null) non = TaoNon(16);
            if (matMuiTen == null) matMuiTen = new Material(matChan) { name = "CoChe_MuiTen3D", enableInstancing = true };
            matMuiTen.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.92f * a));
            const int So = 26;
            foreach (var mt in ds)
            {
                if (!DiemMuiTen(mt, out var s0, out var c0, out var e0)) continue;
                float uTip = DangNhanhSai && NhanhHienTai != null ? Mathf.Min(1f, DiemChan(s0, c0, e0)) : 1f;
                float uDau = 0f;
                if (uTip <= 0.1f || uTip - uDau < 0.1f) continue;
                Vector3 tip = W(Bezier(s0, c0, e0, uTip)), truocTip = W(Bezier(s0, c0, e0, uTip - 0.05f));
                Vector3 huong = (tip - truocTip).sqrMagnitude > 1e-6f ? (tip - truocTip).normalized : Vector3.up;
                const float DaiDau = 0.24f;
                Vector3 goc = tip - huong * DaiDau;
                // thân: một ỐNG LIỀN (6 cạnh) theo đường cong – cả các mũi tên gộp 1 lưới, 1 lần vẽ (trước: hàng chục trụ rời → giảm FPS)
                int dau0 = dinhOng.Count;
                Vector3 truocP = W(Bezier(s0, c0, e0, uDau)), phap = Vector3.zero;
                for (int i = 0; i <= So; i++)
                {
                    float u = Mathf.Lerp(uDau, uTip, i / (float)So);
                    Vector3 p = W(Bezier(s0, c0, e0, u));
                    if (i > So / 2 && Vector3.Dot(p - goc, huong) > 0f) p = goc;   // chỉ chặn ở nửa cuối (cung vồng mạnh không bị cụt)
                    Vector3 t = i == 0 ? (W(Bezier(s0, c0, e0, u + 0.01f)) - p) : (p - truocP);
                    if (t.sqrMagnitude < 1e-8f) t = huong;
                    t.Normalize();
                    if (i == 0 || phap == Vector3.zero) phap = Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    phap = Vector3.Cross(Vector3.Cross(t, phap), t).normalized;   // khung trượt theo đường cong, không xoắn
                    Vector3 phap2 = Vector3.Cross(t, phap);
                    for (int c = 0; c < 6; c++)
                    {
                        float g = c * Mathf.PI / 3f;
                        dinhOng.Add(p + (phap * Mathf.Cos(g) + phap2 * Mathf.Sin(g)) * 0.018f);
                    }
                    if (i > 0)
                        for (int c = 0; c < 6; c++)
                        {
                            int a0 = dau0 + (i - 1) * 6 + c, a1 = dau0 + (i - 1) * 6 + (c + 1) % 6, b0 = a0 + 6, b1 = a1 + 6;
                            tamGiacOng.Add(a0); tamGiacOng.Add(b0); tamGiacOng.Add(a1);
                            tamGiacOng.Add(a1); tamGiacOng.Add(b0); tamGiacOng.Add(b1);
                        }
                    truocP = p;
                }
                float rDau = mt.SoE == 1 ? 0.055f : 0.075f;                 // mũi tên 1 e (nửa đầu) nhỏ hơn
                dauMt.Add(Matrix4x4.TRS(goc, Quaternion.FromToRotation(Vector3.up, huong), new Vector3(rDau, DaiDau, rDau)));
            }
            if (dinhOng.Count > 0)
            {
                if (ongMt == null) { ongMt = new Mesh { name = "OngMuiTen" }; ongMt.MarkDynamic(); }
                ongMt.Clear();
                ongMt.SetVertices(dinhOng);
                ongMt.SetTriangles(tamGiacOng, 0);
                ongMt.RecalculateBounds();
                Graphics.RenderMesh(Rp(matMuiTen), ongMt, 0, Matrix4x4.identity);
                thanMt.Add(Matrix4x4.identity);                       // đếm cho kiểm thử: có thân mũi tên
            }
            VeNhom(matMuiTen, non, dauMt);
        }

        /// <summary>Bán kính quả cầu nguyên tử đang vẽ (toạ độ mô hình).</summary>
        float BanKinhVe(string id)
        {
            string e = Nguyen(id);
            return KieuChemistry ? VdW(e) * 0.27f : BK(e);
        }

        /// <summary>Hình nón đáy tròn bán kính 1 ở y = 0, đỉnh y = 1 (khớp quy ước trụ: tỉ lệ x = bán kính, y = chiều dài).</summary>
        static Mesh TaoNon(int canh)
        {
            var v = new List<Vector3>(); var tri = new List<int>();
            v.Add(new Vector3(0, 1, 0)); v.Add(Vector3.zero);
            for (int i = 0; i < canh; i++) { float g = i * Mathf.PI * 2f / canh; v.Add(new Vector3(Mathf.Cos(g), 0, Mathf.Sin(g))); }
            for (int i = 0; i < canh; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % canh;
                tri.Add(0); tri.Add(b); tri.Add(a);                         // mặt bên
                tri.Add(1); tri.Add(a); tri.Add(b);                         // đáy
            }
            var m = new Mesh { name = "Non" };
            m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u) => (1 - u) * (1 - u) * a + 2 * u * (1 - u) * c + u * u * b;

        Vector3 Tim(int map) => viTri.TryGetValue("m" + map, out var p) ? p : Vector3.zero;

        /// <summary>
        /// Toạ độ mũi tên cong (20/11, làm lại): cong VUÔNG GÓC với hướng nhìn (luôn thấy rõ đường cong), ra phía ngoài phân tử;
        /// đầu mũi tên nằm đúng trên MẶT CẦU nguyên tử đích ở phía đường cong tới; từ cặp e thì rời mặt cầu nguyên tử nguồn,
        /// từ liên kết thì nhấc lên khỏi liên kết một chút; liên kết → nguyên tử: vồng lên rồi úp xuống nguyên tử.
        /// </summary>
        bool DiemMuiTen(MuiTen mt, out Vector3 s, out Vector3 c, out Vector3 e)
        {
            s = c = e = Vector3.zero;
            if (mt.Tu == null || mt.Den == null || mt.Tu.Length == 0 || mt.Den.Length == 0) return false;
            foreach (var x in mt.Tu) if (!viTri.ContainsKey("m" + x)) return false;
            foreach (var x in mt.Den) if (!viTri.ContainsKey("m" + x)) return false;
            Vector3 tam = Vector3.zero;
            foreach (var p in viTri.Values) tam += p;
            tam /= Mathf.Max(1, viTri.Count);
            bool sLk = mt.TuLoai == "lien_ket" && mt.Tu.Length >= 2, eLk = mt.DenLoai == "lien_ket" && mt.Den.Length >= 2;
            Vector3 sGoc = sLk ? (Tim(mt.Tu[0]) + Tim(mt.Tu[1])) * 0.5f : Tim(mt.Tu[0]);
            int denNt = mt.Den[0];
            if (eLk && Vector3.Distance(Tim(mt.Den[0]), Tim(mt.Den[1])) > 1.9f)
            {   // đích là liên kết ĐANG HÌNH THÀNH (2 nguyên tử còn xa): mũi tên chỉ vào nguyên tử nhận (không thuộc phía nguồn)
                bool o0 = System.Array.IndexOf(mt.Tu, mt.Den[0]) >= 0, o1 = System.Array.IndexOf(mt.Tu, mt.Den[1]) >= 0;
                denNt = o0 && !o1 ? mt.Den[1] : o1 && !o0 ? mt.Den[0]
                      : (Vector3.Distance(sGoc, Tim(mt.Den[0])) > Vector3.Distance(sGoc, Tim(mt.Den[1])) ? mt.Den[0] : mt.Den[1]);
                eLk = false;
            }
            Vector3 eGoc = eLk ? (Tim(mt.Den[0]) + Tim(mt.Den[1])) * 0.5f : Tim(denNt);
            Vector3 d = eGoc - sGoc;
            float L = d.magnitude;
            Vector3 dn = L > 1e-3f ? d / L : Vector3.right;
            Vector3 nhin = Quaternion.Inverse(Xoay) * Vector3.forward;              // hướng nhìn trong toạ độ mô hình
            Vector3 phap = Vector3.Cross(dn, nhin);
            if (phap.sqrMagnitude < 1e-4f) phap = Vector3.Cross(dn, Vector3.up);
            if (phap.sqrMagnitude < 1e-4f) phap = Vector3.Cross(dn, Vector3.right);
            phap.Normalize();
            if (Vector3.Dot(phap, (sGoc + eGoc) * 0.5f - tam) < 0f) phap = -phap;   // cong ra phía ngoài phân tử
            // độ vồng: dây dài vồng vừa (22% L, 0,35–1,2 Å); liên kết → nguyên tử / liên kết → liên kết vồng thấp sát liên kết
            float h = sLk ? 0.35f : Mathf.Clamp(L * 0.22f, 0.35f, 1.2f);
            c = (sGoc + eGoc) * 0.5f + phap * h * 2f;                               // đỉnh cung Bézier bậc 2 ≈ h
            // đầu/đuôi cùng ở phía vồng: đi ra khỏi nguồn theo hướng (phía vồng + hướng tới đích), tới đích theo (phía vồng − hướng đi)
            if (sLk) s = sGoc + phap * 0.25f;
            else s = sGoc + (phap * 0.75f + dn * 0.65f).normalized * (BanKinhVe("m" + mt.Tu[0]) + 0.08f);
            if (eLk) e = eGoc + phap * 0.25f;
            else e = eGoc + (phap * 0.75f - dn * 0.65f).normalized * (BanKinhVe("m" + denNt) + 0.06f);
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
