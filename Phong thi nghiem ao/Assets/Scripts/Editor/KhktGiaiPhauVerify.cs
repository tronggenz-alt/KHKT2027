using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Khkt.Demo;
using Khkt.Demo.Bio;
using Khkt.Demo.Bio.GiaiPhau;
using Khkt.Demo.Bio.Portal;
using Khkt.Environment;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio.GiaiPhau;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Kiểm thử batch cho lab Sinh ngày 24/11: khung xem giải phẫu trong app, bàn mổ tự do (ếch, người) theo luật,
    /// các dụng cụ lab Sinh đã bỏ tương tác dựng sẵn. Chụp ảnh vào docs/anh-giai-phau-tu-do.
    /// Chạy: Unity -batchmode -projectPath ... -executeMethod Khkt.EditorTools.KhktGiaiPhauVerify.Run
    /// </summary>
    [InitializeOnLoad]
    public static class KhktGiaiPhauVerify
    {
        const string PrefCmd = "VERA_GP_VERIFY_CMD";
        const int ShotW = 1920, ShotH = 1080;
        static IEnumerator routine;
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static float deadline;
        static int pass, fail, runtimeErr;
        static readonly List<string> failNotes = new List<string>(), errNotes = new List<string>();

        static KhktGiaiPhauVerify()
        {
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            TryStart();
        }

        public static void Run() => Launch("ALL");
        public static void RunModel() => Launch("MODEL");

        /// <summary>Vật liệu mẫu URP Lit có bản đồ pháp tuyến trong Resources để bàn mổ tạo vật liệu mô (shader, biến thể có trong bản dựng).</summary>
        public static void TaoVatLieuMau()
        {
            const string dir = "Assets/Scripts/Demo/Bio/GiaiPhau/Resources";
            const string path = dir + "/BanMoMoUot.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return;
            Directory.CreateDirectory(dir);
            var m = new Material(sh) { name = "BanMoMoUot" };
            var n = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            m.SetTexture("_BumpMap", Texture2D.normalTexture);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", 0.7f);
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(n);
        }

        static void Launch(string cmd)
        {
            TaoVatLieuMau();
            EditorPrefs.SetString(PrefCmd, cmd);
            EditorSceneManager.OpenScene("Assets/Scenes/PhongThiNghiem.unity");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlay(PlayModeStateChange s) { if (s == PlayModeStateChange.EnteredPlayMode) TryStart(); }

        static void TryStart()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return;
            string cmd = EditorPrefs.GetString(PrefCmd, "");
            if (string.IsNullOrEmpty(cmd)) return;
            EditorPrefs.DeleteKey(PrefCmd);
            pass = fail = runtimeErr = 0; failNotes.Clear(); errNotes.Clear(); stack.Clear();
            deadline = Time.realtimeSinceStartup + 420f;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            routine = ChayTatCa(cmd == "ALL");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void OnLog(string c, string st, LogType t)
        {
            if (t != LogType.Exception && t != LogType.Error && t != LogType.Assert) return;
            if (c != null && (c.StartsWith("[GP_") || c.StartsWith("Curl error"))) return;
            if (st != null && st.Contains("UnityEditor.Search.SearchDatabase")) return;
            runtimeErr++;
            if (errNotes.Count < 8) errNotes.Add(c + " | " + (st ?? "").Split('\n').FirstOrDefault());
        }

        static void Check(bool ok, string ten, string ct = "")
        {
            if (ok) { pass++; Debug.Log($"[GP_PASS] {ten}" + (ct == "" ? "" : " | " + ct)); }
            else { fail++; string m = $"[GP_FAIL] {ten}" + (ct == "" ? "" : " | " + ct); failNotes.Add(m); Debug.LogError(m); }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (Time.realtimeSinceStartup > deadline)
            {
                EditorApplication.update -= Tick;
                Debug.LogError("[GP_TIMEOUT]");
                EditorApplication.Exit(2);
                return;
            }
            try
            {
                var top = stack.Count > 0 ? stack.Peek() : routine;
                if (top.MoveNext()) { if (top.Current is IEnumerator n) stack.Push(n); }
                else if (stack.Count > 0) stack.Pop();
                else { EditorApplication.update -= Tick; }
            }
            catch (Exception e)
            {
                EditorApplication.update -= Tick;
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static IEnumerator Frames(int n) { int t = Time.frameCount + n; while (Time.frameCount < t) yield return null; }
        static IEnumerator Giay(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        static string ThuMucAnh()
        {
            string d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "anh-giai-phau-tu-do"));
            Directory.CreateDirectory(d);
            return d;
        }

        static IEnumerator ChayTatCa(bool coCanh)
        {
            yield return Frames(30);
            // Màn chào mừng của lab (nếu có) đóng để không che khi chụp
            var intro = UnityEngine.Object.FindAnyObjectByType<IntroScreen>();
            if (intro != null && intro.IsOpen) intro.Close();
            yield return Frames(5);

            KiemMoHinhEch();
            KiemSinhLy();
            if (coCanh)
            {
                yield return KiemKhungXemTrongApp();
                yield return KiemBanMo();
                yield return KiemDungCu();
            }

            Debug.Log($"[GP_SUMMARY] pass={pass} fail={fail} runtimeErrors={runtimeErr}");
            if (failNotes.Count > 0) Debug.Log("[GP_FAILED] " + string.Join(" || ", failNotes));
            if (errNotes.Count > 0) Debug.Log("[GP_ERRORS] " + string.Join(" || ", errNotes));
            bool ok = fail == 0 && runtimeErr == 0;
            Debug.Log(ok ? "[GP_RESULT] ALL PASSED" : "[GP_RESULT] FAILED");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // ===================================================================== tiện ích mở mẫu

        static readonly Vector2[] Khung = new Vector2[0];

        static List<Vector2> D(params float[] xy)
        {
            var l = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new Vector2(xy[i], xy[i + 1]));
            return l;
        }

        static void ChuanBiEch(MauMo m)
        {
            m.HuyTuy(); m.HuyTuy();
            for (int c = 0; c < 4; c++) m.GhimChi(c);
        }

        /// <summary>Mở da bụng ếch thành hai vạt lật sang hai bên rồi ghim: một đường dọc giữa và hai đường ngang ở hai đầu.</summary>
        static void MoDaEch(MauMo m)
        {
            m.Rach(DungCuMo.DaoMo, D(0.5f, 0.03f, 0.5f, 0.97f));
            m.Rach(DungCuMo.DaoMo, D(0.5f, 0.95f, 0.0f, 0.95f));
            m.Rach(DungCuMo.DaoMo, D(0.5f, 0.95f, 1.0f, 0.95f));
            m.Rach(DungCuMo.DaoMo, D(0.5f, 0.05f, 0.0f, 0.05f));
            m.Rach(DungCuMo.DaoMo, D(0.5f, 0.05f, 1.0f, 0.05f));
            var l = m.Lop("da");
            m.LatVat(0.25f, 0.5f, false); GhimVatCuoi(m, l);
            m.LatVat(0.75f, 0.5f, false); GhimVatCuoi(m, l);
        }

        static void GhimVatCuoi(MauMo m, LopMo l)
        {
            var vm = m.vat.LastOrDefault(v => v.lop == l && v.oVat.Count > 0);
            if (vm != null) m.GhimVatTheoO(l, vm.oVat[0]);
        }

        /// <summary>Mở cơ bụng ếch lệch khỏi đường giữa (tránh tĩnh mạch bụng), cắt bỏ xương mỏ ác, màng tim.</summary>
        static void MoCoVaNgucEch(MauMo m, bool lechGiua = true)
        {
            float x = lechGiua ? 0.43f : 0.5f;
            m.Rach(DungCuMo.DaoMo, D(x, 0.04f, x, 0.69f));
            m.Rach(DungCuMo.Keo, D(x, 0.67f, 0.03f, 0.67f));
            m.Rach(DungCuMo.Keo, D(x, 0.67f, 0.97f, 0.67f));
            m.Rach(DungCuMo.Keo, D(x, 0.05f, 0.03f, 0.05f));
            m.Rach(DungCuMo.Keo, D(x, 0.05f, 0.97f, 0.05f));
            var co = m.Lop("co");
            m.LatVat(0.2f, 0.4f, false); GhimVatCuoi(m, co);
            m.LatVat(0.8f, 0.4f, false); GhimVatCuoi(m, co);
            // Xương: mép dưới tự do nên kéo luồn từ mép dưới lên
            m.Rach(DungCuMo.Keo, D(0.24f, 0.715f, 0.24f, 0.92f));
            m.Rach(DungCuMo.Keo, D(0.24f, 0.92f, 0.76f, 0.92f));
            m.Rach(DungCuMo.Keo, D(0.76f, 0.715f, 0.76f, 0.92f));
            m.LatVat(0.5f, 0.8f, false);
            // Màng tim: rạch vòng rồi gắp bỏ
            m.Rach(DungCuMo.DaoMo, D(0.39f, 0.71f, 0.39f, 0.90f, 0.61f, 0.90f, 0.61f, 0.71f, 0.39f, 0.71f));
            m.LatVat(0.5f, 0.8f, false);
        }

        static void Chay(MauMo m, float giay)
        {
            int n = Mathf.CeilToInt(giay / 0.02f);
            for (int k = 0; k < n; k++) m.Buoc(0.02f);
        }

        static HashSet<int> OMo(LopMo l)
        {
            var h = new HashSet<int>();
            for (int k = 0; k < l.o.Length; k++) if (l.o[k] != TrangThaiO.Lien) h.Add(k);
            return h;
        }

        // ===================================================================== mô hình ếch

        static void KiemMoHinhEch()
        {
            var m = new MauMo(LoaiMau.Ech);
            var r = m.Rach(DungCuMo.DaoMo, D(0.5f, 0.2f, 0.5f, 0.8f));
            Check(!r.ok && r.thongBao.Contains("hủy não"), "Ếch còn sống: dao bị từ chối, yêu cầu hủy não tủy trước", r.thongBao);
            m.HuyTuy();
            Check(m.huyNao && !m.sinhLy.coTheTho, "Hủy não: ếch ngừng thở bằng thềm miệng");
            r = m.Rach(DungCuMo.DaoMo, D(0.5f, 0.2f, 0.5f, 0.8f));
            Check(!r.ok && r.thongBao.Contains("Ghim"), "Chưa ghim 4 chi: dao bị từ chối", r.thongBao);
            ChuanBiEch(m);

            // Chỉ một đường dọc: vạt còn dính trên, dưới → không lật được, có lý do
            m.Rach(DungCuMo.DaoMo, D(0.5f, 0.03f, 0.5f, 0.97f));
            r = m.LatVat(0.25f, 0.5f, false);
            Check(!r.ok && r.thongBao.Contains("còn dính"), "Một đường rạch dọc: kẹp không lật được vạt, báo vạt còn dính", r.thongBao);

            // Kéo không có chỗ luồn
            var m2 = new MauMo(LoaiMau.Ech); ChuanBiEch(m2);
            r = m2.Rach(DungCuMo.Keo, D(0.3f, 0.3f, 0.3f, 0.6f));
            Check(!r.ok && r.thongBao.Contains("luồn"), "Kéo đặt giữa da nguyên: từ chối vì không có chỗ luồn mũi kéo", r.thongBao);

            // Thứ tự khác nhau → cùng trạng thái
            var a = new MauMo(LoaiMau.Ech); ChuanBiEch(a);
            var b = new MauMo(LoaiMau.Ech); ChuanBiEch(b);
            a.Rach(DungCuMo.DaoMo, D(0.5f, 0.03f, 0.5f, 0.97f));
            a.Rach(DungCuMo.DaoMo, D(0.0f, 0.95f, 1.0f, 0.95f));
            a.Rach(DungCuMo.DaoMo, D(0.0f, 0.05f, 1.0f, 0.05f));
            b.Rach(DungCuMo.DaoMo, D(1.0f, 0.05f, 0.0f, 0.05f));
            b.Rach(DungCuMo.DaoMo, D(1.0f, 0.95f, 0.0f, 0.95f));
            b.Rach(DungCuMo.DaoMo, D(0.5f, 0.97f, 0.5f, 0.03f));
            var ra = a.LatVat(0.2f, 0.5f, false);
            var rb = b.LatVat(0.2f, 0.5f, false);
            var sa = OMo(a.Lop("da")); var sb = OMo(b.Lop("da"));
            Check(ra.ok && rb.ok && sa.SetEquals(sb) && sa.Count > 50, "Rạch theo thứ tự và chiều khác nhau → cùng một vạt da lật được", $"{sa.Count} ô vs {sb.Count} ô");

            // Vạt chưa ghim tự rơi về
            Chay(a, 9f);
            Check(OMo(a.Lop("da")).Count == 0, "Vạt da chưa ghim tự rơi về sau vài giây");
            ra = a.LatVat(0.2f, 0.5f, false);
            GhimVatCuoi(a, a.Lop("da"));
            Chay(a, 8f);
            Check(ra.ok && OMo(a.Lop("da")).Count > 50, "Vạt đã ghim giữ mở");

            // Dao không cắt xương; kéo cắt được xương mỏng của ếch
            var e = new MauMo(LoaiMau.Ech); ChuanBiEch(e); MoDaEch(e);
            r = e.Rach(DungCuMo.DaoMo, D(0.3f, 0.72f, 0.3f, 0.9f));
            Check(!r.ok && r.thongBao.Contains("không cắt được"), "Dao mổ không cắt được đai vai, xương ức", r.thongBao);
            r = e.Rach(DungCuMo.Keo, D(0.24f, 0.715f, 0.24f, 0.92f));
            Check(r.ok, "Kéo luồn từ mép dưới tự do cắt được xương mỏng của ếch", r.thongBao);

            // Rạch cơ đúng đường giữa: đứt tĩnh mạch bụng; lệch giữa thì không
            var g = new MauMo(LoaiMau.Ech); ChuanBiEch(g); MoDaEch(g);
            r = g.Rach(DungCuMo.DaoMo, D(0.5f, 0.1f, 0.5f, 0.6f));
            var tm = g.Mach("tm_bung");
            Check(tm.dut && r.thongBao.Contains("tĩnh mạch bụng"), "Rạch cơ bụng ếch đúng đường giữa: đứt tĩnh mạch bụng", r.thongBao);
            float v0 = g.sinhLy.theTichMau; Chay(g, 10f);
            float mat10 = v0 - g.sinhLy.theTichMau;
            g.DungTrenMach(DungCuMo.KepCamMau, tm);
            float v1 = g.sinhLy.theTichMau; Chay(g, 10f);
            Check(mat10 > 0.02f && Mathf.Abs(g.sinhLy.theTichMau - v1) < 0.002f, "Máu chảy theo thời gian, kẹp cầm máu thì ngừng", $"mất 10 s = {mat10:0.000} mL, sau kẹp {v1 - g.sinhLy.theTichMau:0.0000}");
            var h = new MauMo(LoaiMau.Ech); ChuanBiEch(h); MoDaEch(h);
            h.Rach(DungCuMo.DaoMo, D(0.43f, 0.1f, 0.43f, 0.6f));
            Check(!h.Mach("tm_bung").dut, "Rạch lệch đường giữa: tĩnh mạch bụng còn nguyên");

            // Cơ quan bị che: chưa mở thì không chạm được; vén ruột để tới dây thần kinh tọa
            var n = new MauMo(LoaiMau.Ech); ChuanBiEch(n);
            r = n.DungTrenCoQuan(DungCuMo.OngTiem, n.CQ("tui_mat"));
            Check(!r.ok && r.thongBao.Contains("che"), "Túi mật chưa lộ: ống tiêm bị từ chối, nói rõ lớp che", r.thongBao);
            MoDaEch(n); MoCoVaNgucEch(n);
            var toa = n.TK("toa_phai");
            r = n.DungTrenThanKinh(DungCuMo.DienCuc, toa, 0.85f);
            Check(!r.ok && r.thongBao.Contains("ruột non"), "Dây tọa nằm sau ruột non: phải vén ruột trước", r.thongBao);
            n.DungTrenCoQuan(DungCuMo.KepPanh, n.CQ("ruot_non"));
            r = n.DungTrenThanKinh(DungCuMo.DienCuc, toa, 0.85f);
            Check(r.ok && n.chiCo[2] > 0.9f, "Kích thích dây tọa phải: chân sau phải co", r.thongBao);
            Chay(n, 3f);
            n.DungTrenThanKinh(DungCuMo.Keo, toa, 0.5f);
            n.DungTrenThanKinh(DungCuMo.DienCuc, toa, 0.2f);
            bool trungUong = n.chiCo[2] < 0.05f;
            n.DungTrenThanKinh(DungCuMo.DienCuc, toa, 0.85f);
            Check(trungUong && n.chiCo[2] > 0.9f, "Cắt dây tọa: kích thích đầu trung ương không co, đầu ngoại vi vẫn co");

            // Phản xạ tủy: còn tủy (chỉ hủy não) thì co; cắt dây tọa hoặc hủy tủy thì mất
            var p = new MauMo(LoaiMau.Ech);
            p.HuyTuy(); // chỉ hủy não
            r = p.NhoAcidChan(2);
            Check(p.chiCo[2] > 0.9f, "Ếch hủy não, còn tủy: nhỏ HCl lên chân sau → chân co (phản xạ tủy)", r.thongBao);
            p.HuyTuy(); Chay(p, 5f);
            r = p.NhoAcidChan(3);
            Check(p.chiCo[3] < 0.05f && r.thongBao.Contains("Tủy sống"), "Đã hủy tủy: nhỏ HCl chân không co", r.thongBao);

            // Tim ếch tách rời vẫn đập
            var t = new MauMo(LoaiMau.Ech); ChuanBiEch(t); MoDaEch(t); MoCoVaNgucEch(t);
            r = t.DungTrenCoQuan(DungCuMo.Keo, t.CQ("tim"));
            int truoc = t.sinhLy.soNhatBop; Chay(t, 10f);
            Check(r.ok && t.CQ("tim").tachRoi && t.sinhLy.soNhatBop - truoc >= 4, "Cắt rời tim ếch: tim tách rời vẫn tự đập", $"{t.sinhLy.soNhatBop - truoc} nhát / 10 s");
        }

        // ===================================================================== sinh lý

        static void KiemSinhLy()
        {
            // Ngoại tâm thu và nghỉ bù (tim ếch)
            var m = new MauMo(LoaiMau.Ech); ChuanBiEch(m); MoDaEch(m); MoCoVaNgucEch(m);
            var s = m.sinhLy;
            int buoc = 0;
            while (s.thatCo < 0.5f && buoc++ < 400) m.Buoc(0.01f);
            var rTruc = s.KichThichTim();
            // chờ một nhát thất do xoang, rồi kích thích cuối tâm trương (ngay trước nhát xoang kế): xung xoang kế rơi vào thời kỳ trơ
            int nhat = s.soNhatBop; buoc = 0;
            while (s.soNhatBop == nhat && buoc++ < 400) m.Buoc(0.01f);
            float chuKy = 60f / s.nhipDo;
            for (float t = 0f; t < chuKy - 0.3f; t += 0.01f) m.Buoc(0.01f);
            int chanTruoc = s.soXungBiChan;
            var rDung = s.KichThichTim();
            for (int k = 0; k < 300; k++) m.Buoc(0.01f);
            Check(!rTruc.ok && rTruc.thongBao.Contains("trơ"), "Kích thích lúc tâm thất đang co: không đáp ứng (thời kỳ trơ)", rTruc.thongBao);
            Check(rDung.ok && s.soNgoaiTamThu == 1 && s.soXungBiChan > chanTruoc, "Kích thích thì tâm trương: ngoại tâm thu, xung xoang kế bị chặn (nghỉ bù)", $"bị chặn {s.soXungBiChan - chanTruoc}");

            // Nhiệt độ, thuốc đổi nhịp đang chạy
            var a = new MauMo(LoaiMau.Ech); ChuanBiEch(a); MoDaEch(a); MoCoVaNgucEch(a);
            for (int k = 0; k < 500; k++) a.Buoc(0.02f);
            float hr22 = a.sinhLy.nhipDo;
            a.NhoThuoc(ThuocMo.RingerLanh);
            for (int k = 0; k < 500; k++) a.Buoc(0.02f);
            float hrLanh = a.sinhLy.nhipDo;
            Check(hrLanh < hr22 * 0.7f, "Nhỏ Ringer lạnh: tim ếch chậm lại (biến nhiệt, Q10 ≈ 2)", $"{hr22:0} → {hrLanh:0} lần/phút");
            a.NhoThuoc(ThuocMo.Ringer);
            a.NhoThuoc(ThuocMo.Adrenalin);
            for (int k = 0; k < 500; k++) a.Buoc(0.02f);
            float hrAdr = a.sinhLy.nhipDo;
            Check(hrAdr > hrLanh * 1.5f && a.sinhLy.tanSoNhuDong < 3.2f * 1.2f, "Adrenalin: tim nhanh lên, nhu động giảm", $"{hrAdr:0} lần/phút, nhu động {a.sinhLy.tanSoNhuDong:0.0}");
            var c = new MauMo(LoaiMau.Ech); ChuanBiEch(c); MoDaEch(c); MoCoVaNgucEch(c);
            c.NhoThuoc(ThuocMo.Acetylcholin); c.NhoThuoc(ThuocMo.Acetylcholin);
            for (int k = 0; k < 500; k++) c.Buoc(0.02f);
            Check(c.sinhLy.nhipDo < 3f, "Acetylcholin liều cao: tim ếch ngừng thì tâm trương", $"{c.sinhLy.nhipDo:0}");
            c.NhoThuoc(ThuocMo.Ringer); c.NhoThuoc(ThuocMo.Ringer);
            for (int k = 0; k < 500; k++) c.Buoc(0.02f);
            Check(c.sinhLy.nhipDo > 20f, "Rửa Ringer: tim ếch đập lại", $"{c.sinhLy.nhipDo:0}");

            // Mô khô dần nếu không nhỏ Ringer
            var d = new MauMo(LoaiMau.Ech); ChuanBiEch(d);
            for (int k = 0; k < 220; k++) Chay(d, 1f);
            Check(d.sinhLy.doAm < 0.15f && d.sinhLy.nhipDo < 20f, "Không nhỏ Ringer: mô khô, tim chậm dần", $"ẩm {d.sinhLy.doAm:P0}, {d.sinhLy.nhipDo:0} lần/phút");

            // Phổi ếch: hủy não → xẹp; thổi → phồng rồi xẹp dần
            var p = new MauMo(LoaiMau.Ech);
            for (int k = 0; k < 100; k++) p.Buoc(0.02f);
            float phoiSong = p.sinhLy.phoi;
            p.HuyTuy();
            for (int k = 0; k < 100; k++) p.Buoc(0.02f);
            float phoiChet = p.sinhLy.phoi;
            p.ThoiQuaThanhQuan();
            p.Buoc(0.02f);
            float phoiThoi = p.sinhLy.phoi;
            for (int k = 0; k < 1500; k++) p.Buoc(0.02f);
            Check(phoiSong > 0.5f && phoiChet < 0.05f && phoiThoi > 0.9f && p.sinhLy.phoi < phoiThoi * 0.6f,
                  "Phổi ếch: còn não thì phồng theo nhịp thở; hủy não xẹp; thổi qua thanh quản phồng rồi xẹp dần", $"{phoiSong:0.00}/{phoiChet:0.00}/{phoiThoi:0.00}/{p.sinhLy.phoi:0.00}");

        }

        // ===================================================================== khung xem trong app

        static IEnumerator KiemKhungXemTrongApp()
        {
            int uc = DesktopViewer.UiCapture;
            // Gọi đúng như thẻ Giải phẫu trên bảng điều khiển: tìm lớp cầu nối theo tên rồi gọi Mo()
            var cauNoi = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("Khkt.Bio.GiaiPhau.GiaiPhauMoDun", false)).FirstOrDefault(x => x != null);
            cauNoi?.GetMethod("Mo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null)?.Invoke(null, null);
            yield return Frames(4);
            Check(GiaiPhauMoDunImpl.DangMo && !BioWebViewBridge.IsOpen, "Thẻ Giải phẫu: mở khung xem 3D trong app, không mở trang web");
            var ctl = GiaiPhauMoDunImpl.HienTai;
            foreach (var mode in new[] { UiTheme.Mode.XanhMuc, UiTheme.Mode.Trang })
            {
                UiTheme.Set(mode, false);
                yield return Frames(3);
                ctl = GiaiPhauMoDunImpl.HienTai;
                var panel = ctl.UiCanvas.transform.Find("BangDieuKhienTrai")?.GetComponent<Image>();
                float lum = panel != null ? panel.color.grayscale : -1f;
                bool dung = mode == UiTheme.Mode.Trang ? lum > 0.8f : lum < 0.3f;
                Check(dung, $"Khung xem giải phẫu theo chủ đề {UiTheme.ModeLabel(mode)}", $"độ sáng nền bảng {lum:0.00}");
                ctl.ChonCoQuan("tim");
                ctl.DatBocLop(0.75f);
                yield return Giay(0.6f);
                Chup(ctl.StageCamera, ctl.UiCanvas, $"01_giai_phau_3d_{(mode == UiTheme.Mode.Trang ? "sang" : "toi")}.png");
            }
            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            yield return Frames(2);
            GiaiPhauMoDunImpl.Dong();
            yield return Frames(2);

            // Cổng Phòng Sinh học 3D (bấm E / nút Vào): trên Windows mở phòng giải phẫu trong game, đóng thì về lab
            var cong = UnityEngine.Object.FindAnyObjectByType<BioPortal>(FindObjectsInactive.Include);
            if (cong != null)
            {
                var camLab = Camera.main;
                int ucTruoc = DesktopViewer.UiCapture;
                cong.OnEnterButtonClicked();
                yield return Frames(4);
                var tr = BioPortalTransition.Instance;
                bool trongGame = GiaiPhauMoDunImpl.DangMo && !BioWebViewBridge.IsOpen;
                Check(BioPortalTransition.DungWebNhung || trongGame,
                      "Cổng Sinh trên Windows: bấm vào mở phòng giải phẫu trong game, không mở trình duyệt",
                      $"khungXem={GiaiPhauMoDunImpl.DangMo} web={BioWebViewBridge.IsOpen} trạngThái={tr?.CurrentState}");
                Check(tr != null && tr.CurrentState == BioPortalTransition.TransitionState.TrongPhong && DesktopViewer.UiCapture > ucTruoc,
                      "Cổng Sinh: lab khóa di chuyển khi đang ở trong phòng", $"uiCapture {DesktopViewer.UiCapture}");
                if (trongGame)
                {
                    yield return Giay(0.8f);
                    Chup(GiaiPhauMoDunImpl.HienTai.StageCamera, GiaiPhauMoDunImpl.HienTai.UiCanvas, "00_cong_sinh_mo_trong_game.png");
                    GiaiPhauMoDunImpl.Dong();   // nút Đóng / Esc
                    yield return Frames(4);
                }
                else BioWebViewBridge.Close();
                Check(tr != null && tr.CurrentState == BioPortalTransition.TransitionState.Nghi && DesktopViewer.UiCapture == ucTruoc
                      && (camLab == null || camLab.enabled),
                      "Cổng Sinh: đóng phòng thì về lại lab, mở khóa điều khiển, bật lại camera lab",
                      $"trạngThái={tr?.CurrentState} uiCapture {DesktopViewer.UiCapture} (gốc {ucTruoc}) cam={(camLab != null && camLab.enabled)}");
            }
            else Check(false, "Có cổng Phòng Sinh học 3D trong cảnh");

            GiaiPhauMoDunImpl.Mo();
            yield return Frames(3);
            // Từ khung xem sang bàn mổ
            var nut = GiaiPhauMoDunImpl.HienTai.UiCanvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "NutBanMo");
            bool coNut = nut != null;   // nút bị hủy cùng canvas khung xem sau khi bấm nên ghi lại trước
            nut?.onClick.Invoke();
            yield return Frames(4);
            Check(coNut && BanMoTuDoMoDun.DangMo && !GiaiPhauMoDunImpl.DangMo && DesktopViewer.UiCapture == uc + 1,
                  "Nút 'Bàn mổ tự do' trong khung xem chuyển sang bàn mổ, không rò khóa điều khiển",
                  $"nút={coNut} bànMổ={BanMoTuDoMoDun.DangMo} khungXem={GiaiPhauMoDunImpl.DangMo} uiCapture {DesktopViewer.UiCapture} (gốc {uc})");
            BanMoTuDoMoDun.Dong();
            yield return Frames(2);
            Check(DesktopViewer.UiCapture == uc, "Đóng bàn mổ trả điều khiển lab");
        }

        // ===================================================================== bàn mổ trong app + ảnh

        static IEnumerator KiemBanMo()
        {
            int uc = DesktopViewer.UiCapture;
            for (int k = 0; k < 4; k++)
            {
                BanMoTuDoMoDun.Mo(LoaiMau.Ech);
                yield return Frames(2);
                BanMoTuDoMoDun.Dong();
                yield return Frames(2);
            }
            Check(!BanMoTuDoMoDun.DangMo && DesktopViewer.UiCapture == uc, "Mở/đóng bàn mổ 4 lần sạch sẽ");

            BanMoTuDoMoDun.Mo(LoaiMau.Ech);
            yield return Frames(4);
            var bm = BanMoTuDoMoDun.HienTai;
            yield return Giay(1.2f);
            Chup(bm.Cam, bm.UiCanvas, "02_ech_ban_dau.png");

            // Bấm thật bằng tia từ camera: kim hủy tủy vào đầu ếch
            bm.ChonDungCu(DungCuMo.KimHuyTuy);
            var dau = bm.SanKhau.GetComponentsInChildren<BanMoNhan>(true).FirstOrDefault(x => x.loai == BanMoNhan.Loai.Dau);
            var kq = dau != null ? bm.BamTai(new Ray(bm.Cam.transform.position, (dau.transform.position - bm.Cam.transform.position).normalized)) : null;
            Check(kq != null && kq.ok && bm.Mau.huyNao, "Tia chuột vào đầu ếch với kim hủy tủy: hủy não", kq?.thongBao);
            bm.ChonDungCu(DungCuMo.KimGhim);
            int ghim = 0;
            foreach (var chi in bm.SanKhau.GetComponentsInChildren<BanMoNhan>(true).Where(x => x.loai == BanMoNhan.Loai.Chi && x.name == "BanChan"))
            {
                // tia thẳng từ trên xuống bàn chân, như con trỏ đặt ngay trên bàn chân
                var r = bm.BamTai(new Ray(chi.transform.position + Vector3.up * 0.3f, Vector3.down));
                if (r != null && r.ok) ghim++;
            }
            Check(bm.Mau.soChiGhim == 4, "Tia chuột vào bàn chân với kim ghim: ghim đủ 4 chi lên khay", $"{bm.Mau.soChiGhim}/4");
            ChuanBiEch(bm.Mau);
            // Kẹp panh vào giữa bụng khi da còn nguyên: lớp da được trúng (tam giác → ô) và từ chối có lý do
            bm.ChonDungCu(DungCuMo.KepPanh);
            var tam = bm.SanKhau.DiemTheGioi(new Vector3(0.5f, 0.5f, 0f));
            kq = bm.BamTai(new Ray(bm.Cam.transform.position, (tam - bm.Cam.transform.position).normalized));
            Check(kq != null && !kq.ok && kq.thongBao.Contains("chưa có đường rạch"), "Tia chuột vào da bụng nguyên với kẹp panh: báo chưa có đường rạch", kq?.thongBao);

            // Rạch bằng tia camera thật (như kéo chuột): ba đường, đường ngang dừng cách mép ~1 ô vẫn được nối tới mép
            bm.ChonDungCu(DungCuMo.DaoMo);
            foreach (var (a0, a1) in new[] { (new Vector2(0.5f, 0.04f), new Vector2(0.5f, 0.96f)), (new Vector2(0.5f, 0.95f), new Vector2(0.04f, 0.95f)), (new Vector2(0.5f, 0.05f), new Vector2(0.03f, 0.05f)) })
            {
                var duongUv = new List<Vector2>();
                for (int k = 0; k <= 24; k++)
                {
                    var w = bm.SanKhau.DiemTheGioi(new Vector3(Mathf.Lerp(a0.x, a1.x, k / 24f), Mathf.Lerp(a0.y, a1.y, k / 24f), 0f));
                    var man = bm.Cam.WorldToScreenPoint(w);
                    if (bm.SanKhau.GiaoMatLop(bm.Cam.ScreenPointToRay(man), 0f, out var uvTia)) duongUv.Add(uvTia);
                }
                bm.RachTheoDuong(duongUv);
            }
            bm.ChonDungCu(DungCuMo.KepPanh);
            var giuaVat = bm.SanKhau.DiemTheGioi(new Vector3(0.25f, 0.5f, 0f));
            kq = bm.BamTai(new Ray(bm.Cam.transform.position, (giuaVat - bm.Cam.transform.position).normalized));
            Check(kq != null && kq.ok && bm.Mau.Lop("da").TiLeMo() > 0.2f, "Rạch bằng tia camera (đường dừng sát mép được nối ra mép) rồi kẹp lật được vạt da trái", kq?.thongBao);
            var lDa = bm.Mau.Lop("da");
            var vt = bm.Mau.vat.LastOrDefault(x => x.lop == lDa && x.oVat.Count > 0);
            if (vt != null) bm.Mau.GhimVatTheoO(lDa, vt.oVat[0]);
            MoDaEch(bm.Mau);
            bm.BaoKetQua(new KetQua(true, bm.Mau.nhatKy.LastOrDefault()));
            bm.TuaNhanh(1.5f);
            yield return Giay(1.0f);
            Chup(bm.Cam, bm.UiCanvas, "03_ech_lat_da.png");
            MoCoVaNgucEch(bm.Mau);
            bm.BaoKetQua(bm.Mau.DungTrenCoQuan(DungCuMo.KepPanh, bm.Mau.CQ("gan")));
            bm.TuaNhanh(1.5f);
            yield return Giay(3.0f);
            bm.QuyDao.SetTarget(BanMoSanKhau.Goc + new Vector3(0f, 0.01f, 0.03f), 0f, 66f, 0.36f, true);
            yield return Giay(1.0f);
            Chup(bm.Cam, bm.UiCanvas, "04_ech_mo_khoang_tim_dap.png");
            // Kích thích dây tọa sau khi vén ruột
            bm.Mau.DungTrenCoQuan(DungCuMo.KepPanh, bm.Mau.CQ("ruot_non"));
            bm.ChonDungCu(DungCuMo.DienCuc);
            var tk = bm.Mau.TK("toa_phai");
            yield return Giay(0.5f);
            var p = bm.SanKhau.DiemTheGioi(MauMo.DiemTren(tk.duong, 0.85f));
            kq = bm.BamTai(new Ray(bm.Cam.transform.position, (p - bm.Cam.transform.position).normalized));
            Check(kq != null && kq.ok && kq.thongBao.Contains("tọa") && bm.Mau.chiCo[2] > 0.5f, "Tia chuột vào dây tọa với điện cực: trúng dây tọa, chân sau co", kq?.thongBao);
            yield return Frames(3);
            Chup(bm.Cam, bm.UiCanvas, "05_ech_kich_thich_day_toa.png");

            // Cận cảnh tim, ruột sau khi vén ruột
            bm.QuyDao.SetTarget(bm.SanKhau.DiemTheGioi(new Vector3(0.5f, 0.55f, 0.15f)), 0f, 60f, 0.26f, true);
            yield return Giay(1.2f);
            Chup(bm.Cam, bm.UiCanvas, "05b_ech_can_canh_noi_tang.png");
            bm.DatGocNhin(true);

            // Chủ đề Sáng / Tối navy khi đang mở bàn mổ
            UiTheme.Set(UiTheme.Mode.Trang, false);
            yield return Frames(3);
            var nen = bm.UiCanvas.transform.Find("SinhHieu")?.GetComponent<Image>();
            Check(nen != null && nen.color.grayscale > 0.8f, "Bàn mổ ếch theo chủ đề Sáng", $"{nen?.color.grayscale:0.00}");
            yield return Giay(1.0f);
            Chup(bm.Cam, bm.UiCanvas, "06_ech_chu_de_sang.png");
            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            yield return Frames(3);
            nen = bm.UiCanvas.transform.Find("SinhHieu")?.GetComponent<Image>();
            Check(nen != null && nen.color.grayscale < 0.3f, "Đổi chủ đề khi đang mở: bàn mổ dựng lại theo Tối navy", $"{nen?.color.grayscale:0.00}");
            // Cắt cung động mạch chủ: máu phụt theo nhịp tim, đọng trong khoang
            bm.BaoKetQua(bm.Mau.DungTrenMach(DungCuMo.Keo, bm.Mau.Mach("dm_chu")));
            yield return Giay(3f);
            bm.TuaNhanh(6f);
            yield return Giay(1.0f);
            Chup(bm.Cam, bm.UiCanvas, "07_ech_dut_dong_mach_chu.png");
            BanMoTuDoMoDun.Dong();
            yield return Frames(2);
        }

        // ===================================================================== dụng cụ lab Sinh đã bỏ tương tác dựng sẵn

        static IEnumerator KiemDungCu()
        {
            // Đồng hồ bàn 1 chỉ dựng khi khu tầng 2 được tải; kiểm trên một đồng hồ tạo riêng cùng lớp
            var goTimer = new GameObject("KiemDongHo");
            var timer = goTimer.AddComponent<BenchTimer>();
            {
                timer.isRunning = false; timer.elapsedSeconds = 0f;
                timer.Use(null);
                float t0 = timer.elapsedSeconds;
                yield return Giay(1.0f);
                Check(timer.isRunning && t0 < 0.5f && timer.elapsedSeconds > 0.7f && timer.elapsedSeconds < 3f, "Đồng hồ bấm giờ đếm thật, không nhảy lên 60 s", $"{t0:0.0} → {timer.elapsedSeconds:0.0} s");
                timer.Use(null); timer.Use(null);
            }
            UnityEngine.Object.Destroy(goTimer);

            var go = new GameObject("KiemDungCu"); go.transform.position = new Vector3(500f, 500f, 500f); // xa mọi bình, vùng nhiệt
            var th = go.AddComponent<Thermometer>();
            th.Use(null);
            Check(Mathf.Abs(th.currentReadingC - BioGanNhat.NhietDoPhong) < 0.01f, "Nhiệt kế ngoài không khí đọc nhiệt độ phòng (không còn 37 °C cố định)", $"{th.currentReadingC:0.0} °C");
            var sp = go.AddComponent<GlowingSplint>();
            sp.Use(null);
            Check(!sp.burstIntoFlame, "Que đóm không ở miệng bình sinh O₂: không bùng cháy");
            var flask = go.AddComponent<FermentationFlask>();
            double h0 = flask.elapsedHours; flask.Use(null);
            Check(flask.elapsedHours - h0 < 0.1, "Bấm bình lên men không tua 2 giờ");
            var yo = go.AddComponent<YogurtJar>();
            double y0 = yo.incubatedHours; yo.Use(null);
            Check(yo.incubatedHours - y0 < 0.1, "Bấm hũ sữa chua không tua 3 giờ");
            var bag = go.AddComponent<AutoclaveBag>();
            bag.Use(null);
            Check(!bag.indicatorTapeBlackStripes, "Túi hấp hủy ngoài nồi hấp: băng chỉ thị không đổi màu");
            var fo = go.AddComponent<Forceps>();
            fo.Use(null);
            Check(string.IsNullOrEmpty(fo.heldSpecimenId), "Bấm kẹp trống: không tự có biểu bì vảy hành");
            var wg = go.AddComponent<WatchGlass>();
            wg.Use(null);
            Check(wg.sample == null || wg.sample.IsEmpty, "Bấm đĩa đồng hồ trống: không tự có mẫu");
            var rod = go.AddComponent<GlassRod>();
            rod.Use(null);
            Check(!rod.hasSpoolDna, "Bấm đũa thủy tinh khi không có ADN: không quấn được ADN");
            var spr = go.AddComponent<CellSpreader>();
            spr.Use(null);
            Check(!spr.sterilizedWithEthanolFlame, "Que trang không có đèn cồn cháy gần: chưa khử trùng");
            var fun = go.AddComponent<FilterFunnel>();
            fun.Use(null);
            Check(!fun.isFiltered, "Phễu lọc không có dịch nghiền: không lọc ra gì");
            var bt = go.AddComponent<BrothTube>();
            go.transform.position = new Vector3(500f, 500f, 500f);
            double bh = bt.incubatedHours;
            bt.Use(null);
            yield return Giay(1.0f);
            // 25 °C: hệ số sinh trưởng ≈ 0,15; 1 s thật = 1 phút mô phỏng → tăng rất ít, không còn +2 giờ mỗi lần bấm
            Check(bt.incubatedHours - bh < 0.05, "Ống canh khuẩn: bấm không tua 2 giờ; sinh trưởng theo nhiệt độ chỗ đặt", $"+{(bt.incubatedHours - bh) * 60:0.00} phút hiệu dụng");
            UnityEngine.Object.Destroy(go);

            var ban = UnityEngine.Object.FindAnyObjectByType<DigitalAnatomyTable>(FindObjectsInactive.Include);
            if (ban != null)
            {
                int org = ban.selectedOrganIndex;
                ban.OnScroll(1f);
                Check(ban.selectedOrganIndex == org, "Con lăn trên bàn giải phẫu số chỉ xoay, không nhảy cơ quan");
                Check(DigitalAnatomyTable.OrganIds.All(id => Bp3dReader.TimCoQuan(id) != null), "Mã cơ quan của bàn giải phẫu số khớp danh mục kiến thức (da_day, ruot_non…)",
                      string.Join(",", DigitalAnatomyTable.OrganIds.Where(id => Bp3dReader.TimCoQuan(id) == null)));
            }
            yield return null;
        }

        // ===================================================================== chụp ảnh

        static void Chup(Camera cam, Canvas canvas, string ten)
        {
            if (cam == null) return;
            var rt = new RenderTexture(ShotW, ShotH, 24);
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            var mode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
            var oldRt = cam.targetTexture; int oldMask = cam.cullingMask; bool en = cam.enabled;
            try
            {
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = cam.nearClipPlane + 0.02f;
                }
                cam.targetTexture = rt;
                cam.cullingMask = oldMask | (1 << 5);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string p = Path.Combine(ThuMucAnh(), ten);
                File.WriteAllBytes(p, tex.EncodeToPNG());
                Debug.Log("[GP_SHOT] " + p);
            }
            finally
            {
                cam.targetTexture = oldRt; cam.cullingMask = oldMask; cam.enabled = en;
                if (canvas != null) { canvas.renderMode = mode; canvas.worldCamera = null; }
                rt.Release();
                UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
