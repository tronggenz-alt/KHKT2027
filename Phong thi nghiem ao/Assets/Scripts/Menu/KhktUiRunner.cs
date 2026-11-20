using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Khkt.Demo;
using Khkt.Menu.Dashboard;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Khkt.Menu
{
    /// <summary>
    /// Chụp và kiểm tra giao diện ngoài lab (C4, 18/11) trong PlayMode – gọi từ Editor/KhktUiVerify:
    ///   mỗi nền (Trắng, Đen, Xanh mực) × màn (đăng nhập, cài đặt, màn chờ, bảng điều khiển) × 1440×900 và 2880×1800 (Retina).
    ///   KHKT_UI_CHE_DO=truoc → chỉ chụp ảnh "trước"; mặc định "sau" → chụp và chạy các kiểm tra [UI_PASS]/[UI_FAIL].
    /// Ảnh: KHKT_UI_SHOTS hoặc Path.GetTempPath()/vera_ui_shots.
    /// </summary>
    public class KhktUiRunner : MonoBehaviour
    {
        static readonly (int w, int h)[] CoMan = { (1440, 900), (2880, 1800) };
        int passes, failures, runtimeErrors, shots;
        readonly List<string> failed = new List<string>(), errLines = new List<string>();
        string outDir, cheDo;
        Texture2D readTex;

        static KhktUiRunner dangChay;

        void Awake()
        {
            // Bộ chụp được thêm vào cảnh ManHinhChinh trước khi vào PlayMode, nên mỗi lần nạp lại cảnh Unity dựng thêm một bản:
            // chỉ giữ bản đầu tiên.
            if (dangChay != null && dangChay != this) { Destroy(gameObject); enabled = false; return; }
            dangChay = this;
            DontDestroyOnLoad(gameObject);
            cheDo = System.Environment.GetEnvironmentVariable("KHKT_UI_CHE_DO") ?? "sau";
            outDir = System.Environment.GetEnvironmentVariable("KHKT_UI_SHOTS");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "vera_ui_shots");
            Directory.CreateDirectory(outDir);
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy()
        {
            if (dangChay != this) return;
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string c, string st, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception && t != LogType.Assert) return;
            if (c != null && c.StartsWith("[UI_")) return;
            if (!string.IsNullOrEmpty(st) && st.Contains("SearchInit.IndexationOnStartup")) return;
            runtimeErrors++;
            errLines.Add($"{t}: {c} | {(st ?? "").Split('\n').FirstOrDefault()}");
        }

        void Check(bool ok, string ten, string ct = "")
        {
            if (cheDo == "truoc") return;
            if (ok) { passes++; Debug.Log($"[UI_PASS] {ten}" + (ct == "" ? "" : " | " + ct)); }
            else { failures++; failed.Add(ten); Debug.LogError($"[UI_FAIL] {ten}" + (ct == "" ? "" : " | " + ct)); }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        // Hoạt cảnh menu tiến tối đa 1/20 s mỗi khung hình: khung hình chậm (batchmode) thì phải chờ đủ cả thời gian lẫn số khung
        static IEnumerator Cho(float s)
        {
            float t = Time.realtimeSinceStartup;
            int khung = 0, canKhung = Mathf.CeilToInt(s * 40f);
            while (Time.realtimeSinceStartup - t < s || khung < canKhung) { khung++; yield return null; }
        }

        /// <summary>Bấm thẻ lớn: nút nằm ở thân thẻ (con của đối tượng mang tên thẻ).</summary>
        static bool BamThe(Transform root, string ten)
        {
            var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == ten && x.gameObject.activeInHierarchy);
            var b = t != null ? t.GetComponentInChildren<Button>() : null;
            if (b == null) return false;
            b.onClick.Invoke();
            return true;
        }

        static bool Bam(Transform root, string ten)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == ten && x.gameObject.activeInHierarchy && x.interactable);
            if (b == null) return false;
            b.onClick.Invoke();
            return true;
        }

        IEnumerator Start()
        {
            if (dangChay != this) yield break;
            StartCoroutine(Watchdog(400f));
            var uiCu = UiTheme.Current;
            yield return Frames(10);
            foreach (var nen in new[] { UiTheme.Mode.Trang, UiTheme.Mode.Den, UiTheme.Mode.XanhMuc })
            {
                var cu = FindAnyObjectByType<MainMenu>();
                UiTheme.Set(nen, false);
                GameSession.GuestActive = false;
                SceneManager.LoadScene("ManHinhChinh");
                yield return Frames(2);
                float t0 = Time.realtimeSinceStartup;
                while ((FindAnyObjectByType<MainMenu>() == null || FindAnyObjectByType<MainMenu>() == cu) && Time.realtimeSinceStartup - t0 < 20f) yield return null;
                yield return Cho(2.5f);                               // đổi nền cũng tự nạp lại cảnh: đợi cảnh ổn định
                var menu = FindAnyObjectByType<MainMenu>();
                if (menu == null) { Check(false, "Mở ManHinhChinh"); continue; }
                string ten = nen.ToString();

                // 18/11: màn đầu tiên là màn chọn 2 luồng (Giải bài tập / Phòng Lab)
                var chon = FindAnyObjectByType<ManChonLuong>();
                Check(chon != null && chon.DangHien, $"Mở app vào màn chọn 2 luồng ({ten})");
                yield return Cho(0.6f);
                yield return Chup(menu, $"man_chon_{ten}");
                if (nen == UiTheme.Mode.Trang && cheDo != "truoc") yield return KiemGiaiBaiTap(menu);
                bool vaoLab = chon != null && BamThe(menu.transform, "Nút lớn Phòng Lab");
                yield return Cho(0.6f);
                Check(vaoLab && menu.Current == MainMenu.Screen.Login && !chon.DangHien, $"Nút Phòng Lab mở màn đăng nhập ({ten})");

                menu.Show(MainMenu.Screen.Login);
                yield return Cho(0.6f);
                yield return Chup(menu, $"dang_nhap_{ten}");
                if (nen == UiTheme.Mode.Trang && cheDo != "truoc") KiemManDangNhap(menu);

                menu = FindAnyObjectByType<MainMenu>();
                if (menu == null) continue;
                menu.Show(MainMenu.Screen.Settings);
                yield return Cho(0.6f);
                yield return Chup(menu, $"cai_dat_{ten}");

                menu = FindAnyObjectByType<MainMenu>();
                if (menu == null) continue;
                menu.Show(MainMenu.Screen.Loading);
                yield return Cho(0.6f);
                yield return Chup(menu, $"man_cho_{ten}");
                if (nen == UiTheme.Mode.Trang && cheDo != "truoc") KiemManCho(menu);

                menu = FindAnyObjectByType<MainMenu>();
                if (menu == null) continue;
                menu.Show(MainMenu.Screen.Login);
                yield return Cho(0.3f);
                bool vao = menu != null && Bam(menu.transform, "Nút Dùng thử");
                yield return Cho(1.5f);
                var dash = FindAnyObjectByType<DashboardShell>();
                Check(vao && dash != null, $"Vào bảng điều khiển ({ten})");
                yield return Chup(menu, $"bang_dieu_khien_{ten}");
                menu = FindAnyObjectByType<MainMenu>();
                if (dash != null && menu != null && cheDo != "truoc") yield return KiemBangDieuKhien(menu, dash, ten);
            }
            if (cheDo != "truoc") KiemChung();
            UiTheme.Set(uiCu, false);

            Debug.Log($"[UI_SUMMARY] mode={cheDo} passes={passes} failures={failures} runtimeErrors={runtimeErrors} shots={shots}");
            if (failed.Count > 0) Debug.LogError("[UI_FAILED_LIST] " + string.Join(" || ", failed));
            if (errLines.Count > 0) Debug.LogError("[UI_RUNTIME_ERR_LIST] " + string.Join(" || ", errLines.Take(20)));
            bool ok = failures == 0 && runtimeErrors == 0;
            Debug.Log(ok ? "[UI_RESULT] ALL PASSED" : "[UI_RESULT] FAILED");
#if UNITY_EDITOR
            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }

        IEnumerator Watchdog(float s)
        {
            yield return new WaitForSecondsRealtime(s);
            Debug.LogError($"[UI_TIMEOUT] passes={passes} failures={failures}");
#if UNITY_EDITOR
            EditorApplication.Exit(2);
#endif
        }

        // ------------------------------------------------------------------ kiểm tra (chế độ "sau")
        void KiemChung()
        {
            const string dau = "Ặỷữợựẫ";
            foreach (var f in new[] { GlassKit.Regular, GlassKit.Semibold })
            {
                if (f == null) { Check(false, "Phông giao diện nạp được"); continue; }
                bool du = true;
                foreach (int co in new[] { 15, 36, 72 })
                {
                    f.RequestCharactersInTexture(dau, co);
                    foreach (char c in dau) du &= f.GetCharacterInfo(c, out _, co);
                }
                Check(du, $"Phông {f.name} đủ chữ tiếng Việt có dấu \"Ặ ỷ ữ ợ ự ẫ\" ở cỡ 15/36/72");
            }
            Check(Application.productName == "VERA Lab", "Tên app / tên cửa sổ là \"VERA Lab\"", Application.productName);
        }

        void KiemManDangNhap(MainMenu menu)
        {
            var canvas = menu.GetComponentInChildren<Canvas>(true);
            Check(canvas != null && canvas.pixelPerfect, "Canvas màn chính bật pixelPerfect (viền 1 px thẳng hàng điểm ảnh)");
            var sc = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            Check(sc != null && sc.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && sc.referenceResolution == new Vector2(1920, 1080),
                  "CanvasScaler: tham chiếu 1920×1080, co theo màn hình");
            var nen = menu.GetComponentsInChildren<RawImage>(true).FirstOrDefault(r => r.name == "Ảnh nền");
            Check(nen != null && nen.texture != null && nen.texture.width >= 2560, "Ảnh nền là ảnh dựng thật của lab, cỡ 2x", nen != null && nen.texture != null ? $"{nen.texture.name} {nen.texture.width}×{nen.texture.height}" : "thiếu");
            var logo = menu.GetComponentsInChildren<RawImage>(true).FirstOrDefault(r => r.name == "Logo");
            Check(logo != null && logo.texture != null && logo.texture.width >= 2 * logo.rectTransform.rect.width && logo.texture.mipmapCount == 1,
                  "Logo VERA đủ điểm ảnh cho Retina, không mipmap", logo != null && logo.texture != null ? $"{logo.texture.width}px cho khung {logo.rectTransform.rect.width:0}" : "thiếu");
        }

        void KiemManCho(MainMenu menu)
        {
            var meo = menu.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Mẹo an toàn" && t.gameObject.activeInHierarchy);
            Check(meo != null && meo.text.Length > 20, "Màn chờ có mẹo an toàn phòng thí nghiệm", meo != null ? meo.text : "thiếu");
            var thanh = menu.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Thanh tiến độ");
            Check(thanh != null, "Màn chờ có thanh tiến trình (AsyncOperation.progress)");
        }

        IEnumerator KiemBangDieuKhien(MainMenu menu, DashboardShell dash, string nen)
        {
            var nutNho = dash.GetComponentsInChildren<Button>(false).Where(b => b.gameObject.activeInHierarchy)
                .Where(b => ((RectTransform)b.transform).rect.height < 43.5f && b.GetComponentInChildren<Text>() != null).Select(b => b.name).ToList();
            Check(nutNho.Count == 0, $"Mọi nút bảng điều khiển cao ≥ 44 pt ({nen})", string.Join(", ", nutNho.Take(6)));
            var dsNut = dash.GetComponentsInChildren<Button>(false).Where(b => b.gameObject.activeInHierarchy).ToList();
            var chong = new List<string>();
            for (int i = 0; i < dsNut.Count; i++)
                for (int j = i + 1; j < dsNut.Count; j++)
                {
                    Rect a = KhungMan((RectTransform)dsNut[i].transform), b = KhungMan((RectTransform)dsNut[j].transform);
                    if (a.Overlaps(b) && Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax)).width > 1f
                        && !dsNut[i].transform.IsChildOf(dsNut[j].transform) && !dsNut[j].transform.IsChildOf(dsNut[i].transform))
                        chong.Add($"{dsNut[i].name} × {dsNut[j].name}");
                }
            Check(chong.Count == 0, $"Không có nút nào chồng lên nhau ({nen})", string.Join(", ", chong.Take(4)));
            // lớp phủ bán trong suốt che gần hết màn hình làm cả giao diện xám, nhạt (nhìn như mờ)
            var phu = menu.GetComponentsInChildren<Graphic>(false)
                .Where(g => g.isActiveAndEnabled && g.color.a > 0.05f && g.color.a < 0.95f && !(g is Text))
                .Where(g => { var k = KhungMan(g.rectTransform); var c = KhungMan((RectTransform)menu.GetComponentInChildren<Canvas>().transform); return k.width * k.height > 0.8f * c.width * c.height; })
                .Select(g => $"{DuongDan(g.transform)} a={g.color.a:0.00} {g.GetComponentInParent<CanvasGroup>()?.alpha:0.00}").ToList();
            var cd = menu.menuCamera != null ? menu.menuCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            Check(cd != null && !cd.renderPostProcessing, $"Camera màn chính không hậu kỳ (tonemapping không làm xám giao diện) ({nen})");
            Check(phu.Count == 0, $"Không có lớp phủ bán trong suốt che màn hình ({nen})", string.Join(" | ", phu.Take(3)));
            var nhom = menu.GetComponentsInChildren<CanvasGroup>(false).Where(g => g.alpha > 0.01f && g.alpha < 0.99f).Select(g => $"{DuongDan(g.transform)} alpha={g.alpha:0.00}").ToList();
            Check(nhom.Count == 0, $"Không còn nhóm giao diện đang mờ dở khi chụp ({nen})", string.Join(" | ", nhom.Take(3)));
            var the = dash.GetComponentsInChildren<RectTransform>(true).Where(r => r.name.StartsWith("Thẻ mô-đun ")).ToList();
            Check(the.Count == 5, $"Có 5 thẻ mô-đun ({nen})", string.Join(", ", the.Select(r => r.name)));
            if (nen != "Trang") yield break;
            int coAnh = the.Count(r => r.GetComponentsInChildren<RawImage>(true).Any(x => x.name == "Ảnh thu nhỏ" && x.texture != null));
            Check(coAnh == 5, "Mỗi thẻ mô-đun có ảnh thu nhỏ", $"{coAnh}/5");
            foreach (var (ten, lop) in new[] { ("Thực vật 3D", "Khkt.Bio.ThucVat.ThucVatMoDun"), ("Giải phẫu", "Khkt.Bio.GiaiPhau.GiaiPhauMoDun") })
            {
                bool coLop = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(lop, false) != null);
                var t = the.FirstOrDefault(r => r.name == "Thẻ mô-đun " + ten);
                var chu = t != null ? t.GetComponentsInChildren<Text>(true).Select(x => x.text).ToList() : new List<string>();
                Check(t != null && (coLop ? !chu.Contains("Sắp có") : chu.Contains("Sắp có")), $"Thẻ {ten}: {(coLop ? "mở được" : "ghi Sắp có")} (tìm lớp {lop})");
            }
            // bấm từng thẻ mô-đun → mở qua Mo() tìm theo tên lớp, Dong() đưa về bảng điều khiển
            // thẻ "Giải bài tập" (thay thẻ Cơ chế 3D) mở màn Giải bài tập, Quay lại về bảng điều khiển
            {
                var gbt = FindAnyObjectByType<ManGiaiBaiTap>(FindObjectsInactive.Include);
                var tg = the.FirstOrDefault(r => r.name == "Thẻ mô-đun Giải bài tập");
                var ng = tg != null ? tg.GetComponentsInChildren<Button>(true).FirstOrDefault() : null;
                ng?.onClick.Invoke();
                yield return Cho(0.5f);
                Check(gbt != null && gbt.DangHien, "Thẻ Giải bài tập trên bảng điều khiển mở màn Giải bài tập");
                Bam(gbt != null ? gbt.transform : menu.transform, "Nút Về màn chính");
                yield return Cho(0.5f);
                Check(gbt != null && !gbt.DangHien && dash.gameObject.activeInHierarchy && !FindAnyObjectByType<ManChonLuong>(FindObjectsInactive.Include).DangHien,
                      "Quay lại từ Giải bài tập (mở từ bảng) về bảng điều khiển");
            }
            foreach (var (ten, lop) in new[] { ("Thực vật 3D", "Khkt.Bio.ThucVat.ThucVatMoDun"), ("Giải phẫu", "Khkt.Bio.GiaiPhau.GiaiPhauMoDun") })
            {
                var kieu = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(lop, false)).FirstOrDefault(x => x != null);
                if (kieu == null) continue;                       // nhánh chưa gộp: đã kiểm "Sắp có" ở trên
                var tc = the.FirstOrDefault(r => r.name == "Thẻ mô-đun " + ten);
                var nut = tc != null ? tc.GetComponentsInChildren<Button>(true).FirstOrDefault() : null;
                if (nut == null) { Check(false, $"Thẻ {ten} có nút"); continue; }
                var dangMo = kieu.GetProperty("DangMo", BindingFlags.Public | BindingFlags.Static);
                nut.onClick.Invoke();
                yield return Cho(1.0f);
                bool mo = dangMo != null && (bool)dangMo.GetValue(null);
                Check(mo, $"Thẻ {ten} gọi {kieu.Name}.Mo() và mô-đun mở");
                kieu.GetMethod("Dong", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)?.Invoke(null, null);
                yield return Frames(5);
                bool dong = dangMo != null && !(bool)dangMo.GetValue(null);
                Check(dong && dash.gameObject.activeInHierarchy, $"{ten}: Dong() đưa về bảng điều khiển");
            }
        }

        /// <summary>
        /// Màn Giải bài tập (19/11, bố cục CHEMISTRY): thanh điều hướng 5 mục, trang chủ 3 thẻ công cụ + thẻ Hóa lý,
        /// trang Chuỗi (3 tab nhập, tuỳ chọn, kết quả từng bước gập/mở, báo sai), trang Cơ chế (tab, 8 bài mẫu, kiểu cơ chế,
        /// kết quả + mở 3D ⇄ Giấy), Mô hình 3D, Hóa lý 4 thẻ; Cài đặt chỉ Thông tin + API. Máy chủ giả (MayChuCoChe.GiaLap).
        /// </summary>
        IEnumerator KiemGiaiBaiTap(MainMenu menu)
        {
            bool bam = BamThe(menu.transform, "Nút lớn Giải bài tập");
            yield return Cho(0.6f);
            var gbt = FindAnyObjectByType<ManGiaiBaiTap>();
            Check(bam && gbt != null && gbt.DangHien, "Nút Giải bài tập mở màn Giải bài tập");
            if (gbt == null) yield break;
            yield return Chup(menu, "giai_bai_tap_Trang");
            var ten = gbt.GetComponentsInChildren<RectTransform>(true).Select(r => r.name).ToList();
            int nav = new[] { "Trang chủ", "Chuỗi phản ứng", "Cơ chế", "Mô hình 3D", "Hóa lý" }.Count(n => ten.Contains("Điều hướng " + n));
            Check(nav == 5, "Thanh điều hướng có 5 mục", $"{nav}/5");
            int congCu = new[] { "Công cụ Chuỗi phản ứng", "Công cụ Mô hình 3D", "Công cụ Cơ chế" }.Count(ten.Contains);
            Check(congCu == 3 && ten.Contains("Thẻ Hóa lý"), "Trang chủ: 3 thẻ công cụ + thẻ ngang Hóa lý", $"{congCu}/3");
            Check(!ten.Any(n => n.Contains("Phòng thí nghiệm Hóa học") || n.Contains("Sinh học 3D")), "Không có phòng lab Hóa/Sinh của CHEMISTRY");

            Bam(gbt.transform, "Nút Cài đặt Giải bài tập");
            yield return Cho(0.5f);
            var cd = FindAnyObjectByType<CaiDatGiaiBaiTap>();
            var muc = cd != null ? cd.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Mục ")).Select(b => b.name).ToList() : new List<string>();
            Check(cd != null && muc.Count == 2 && muc.Contains("Mục Thông tin") && muc.Contains("Mục API"), "Cài đặt Giải bài tập chỉ còn Thông tin và API", string.Join(", ", muc));
            yield return Chup(menu, "cai_dat_giai_bai_tap_Trang");
            if (cd != null) Bam(cd.transform, "Nút Đóng cài đặt");
            yield return Cho(0.3f);

            var giaCu = Khkt.CoChe.MayChuCoChe.GiaLap;
            Khkt.CoChe.MayChuCoChe.GiaLap = MayChuGia;

            // Chuỗi phản ứng
            BamThe(gbt.transform, "Công cụ Chuỗi phản ứng");
            yield return Cho(0.6f);
            Check(gbt.TrangDang == ManGiaiBaiTap.Trang.Chuoi, "Thẻ Chuỗi phản ứng mở trang Chuỗi");
            var tc = gbt.GetComponentsInChildren<RectTransform>(true).Where(r => r.name.StartsWith("Tab ") && r.name != "Tab nhập" && r.GetComponentInParent<TrangCuon>(true)?.name == "Trang Chuỗi phản ứng").Count();
            Check(tc == 3 && ten.Contains("Bước ngang 3") && ten.Contains("Mức độ đề") && ten.Contains("Mức thông minh") && ten.Contains("Công tắc tra cứu"),
                  "Trang Chuỗi: 3 tab nhập, bước ngang, tuỳ chọn mức độ / thông minh / tra cứu", $"{tc} tab");
            yield return Chup(menu, "chuoi_nhap_Trang");
            BamThe(gbt.transform, "Tab Chuỗi mẫu");
            yield return null;
            Bam(gbt.transform, "Chuỗi mẫu 1");               // tab Chuỗi mẫu: bấm là giải ngay
            float t0 = Time.realtimeSinceStartup;
            while ((gbt.DongChuoi.Khoi.Count == 0 || !gbt.DongChuoi.Khoi[0].CoDapAn) && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return Cho(0.7f);
            var k1 = gbt.DongChuoi.Khoi.Count > 0 ? gbt.DongChuoi.Khoi[0] : null;
            Check(k1 != null && k1.CoDapAn && gbt.SoBuocChuoi == 4 && k1.Hinh != null && k1.Hinh.GetComponentsInChildren<Text>(true).Count(x => x.name == "Chữ") >= 5,
                  "Giải chuỗi: khối đáp án có hình chuỗi (5 ô chất) và 4 bước", gbt.SoBuocChuoi.ToString());
            yield return Chup(menu, "chuoi_ket_qua_Trang");
            Bam(gbt.transform, "Giải chuỗi");
            gbt.GiaiChuoi();
            t0 = Time.realtimeSinceStartup;
            while ((gbt.DongChuoi.Khoi.Count < 2 || !gbt.DongChuoi.Khoi[1].CoDapAn) && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return Cho(0.7f);
            Check(gbt.DongChuoi.Khoi.Count == 2 && gbt.DongChuoi.Khoi[1].YTren < gbt.DongChuoi.Khoi[0].YTren, "Giải lần 2: thêm khối mới bên dưới, khối cũ giữ làm lịch sử");
            var ndChuoi = gbt.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(r => r.name == "Trang Chuỗi phản ứng");
            Check(ndChuoi != null && ndChuoi.content.anchoredPosition.y > 300, "Trang tự trượt xuống khối đáp án mới", ndChuoi != null ? ndChuoi.content.anchoredPosition.y.ToString("0") : "");
            yield return Chup(menu, "chuoi_lich_su_Trang");
            Bam(gbt.DongChuoi.Khoi[1].transform, "Nút Mở lớn");
            yield return Cho(0.5f);
            Check(gbt.Bang.DangMo && gbt.Bang.Than.GetComponentsInChildren<Button>(true).Any(b => b.name == "Nút Xem cơ chế 4"), "Mở lớn: bảng 75% có hình chuỗi lớn + bước có nút Xem cơ chế");
            yield return Chup(menu, "chuoi_bang_lon_Trang");
            Bam(gbt.transform, "Nút Đóng bảng lớn");
            yield return Cho(0.4f);
            Check(!gbt.Bang.DangMo, "Đóng bảng lớn");
            Bam(gbt.DongChuoi.Khoi[1].transform, "Nút Hỏi tiếp");
            yield return Cho(0.3f);
            var oHoi = gbt.DongChuoi.Khoi[1].GetComponentsInChildren<InputField>(true).FirstOrDefault(x => x.name == "Ô hỏi tiếp");
            if (oHoi != null) { oHoi.text = "Vì sao cần H2SO4?"; oHoi.onSubmit.Invoke(oHoi.text); }
            t0 = Time.realtimeSinceStartup;
            while (gbt.DongChuoi.Khoi[1].TraLoiCuoi == "" && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            Check(gbt.DongChuoi.Khoi[1].TraLoiCuoi.Contains("xúc tác"), "Hỏi tiếp ngay dưới thẻ đáp án (máy chủ giả)", gbt.DongChuoi.Khoi[1].TraLoiCuoi);
            yield return Cho(0.4f);
            yield return Chup(menu, "chuoi_hoi_tiep_Trang");
            Bam(gbt.DongChuoi.Khoi[1].transform, "Nút Báo sai chuỗi");
            yield return Cho(0.4f);
            var hop = gbt.GetComponentInChildren<HopBaoSai>(true);
            Check(hop != null && hop.DangMo, "Kết quả chuỗi: nút Báo sai mở hộp báo sai");
            hop?.Gui("Bước 2 thiếu điều kiện");
            yield return Cho(0.3f);
            Check(hop != null && hop.KetQuaCuoi.Contains("ok"), "Gửi báo sai chuỗi");
            yield return Cho(1.6f);
            // ô đề dính đáy (như ô chat): gõ + Enter là giải, thêm khối mới
            var tcChuoi = gbt.GetComponentsInChildren<TrangCuon>(true).FirstOrDefault(x => x.name == "Trang Chuỗi phản ứng");
            int truocDay = gbt.DongChuoi.Khoi.Count;
            if (tcChuoi != null && tcChuoi.ODeDay != null) { tcChuoi.ODeDay.text = "CH4 -> CH3Cl -> CH3OH -> HCHO"; tcChuoi.ODeDay.onSubmit.Invoke(tcChuoi.ODeDay.text); }
            t0 = Time.realtimeSinceStartup;
            while ((gbt.DongChuoi.Khoi.Count == truocDay || !gbt.DongChuoi.Khoi[gbt.DongChuoi.Khoi.Count - 1].CoDapAn) && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            Check(tcChuoi != null && tcChuoi.ODeDay != null && gbt.DongChuoi.Khoi.Count == truocDay + 1, "Ô đề dính đáy: Enter giải và thêm khối mới", $"{truocDay} → {gbt.DongChuoi.Khoi.Count}");
            yield return Cho(0.7f);
            yield return Chup(menu, "chuoi_o_de_day_Trang");

            // Cơ chế
            Bam(gbt.transform, "Điều hướng Cơ chế");
            yield return Cho(0.6f);
            Check(gbt.TrangDang == ManGiaiBaiTap.Trang.CoChe, "Thanh điều hướng mở trang Cơ chế");
            yield return Chup(menu, "co_che_nhap_Trang");
            Check(gbt.SoBaiMau == 8, "Trang Cơ chế: lưới 8 bài mẫu", gbt.SoBaiMau.ToString());
            Check(gbt.GetComponentsInChildren<Button>(true).Count(b => b.name.StartsWith("Loại ")) == 11, "Trang Cơ chế: 11 lựa chọn kiểu cơ chế");
            BamThe(gbt.transform, "Tab Bài mẫu");
            yield return Cho(0.4f);
            yield return Chup(menu, "co_che_bai_mau_Trang");
            BamThe(gbt.transform, "Tab Văn bản");
            var oDe = gbt.GetComponentsInChildren<InputField>(true).FirstOrDefault(x => x.name == "Ô đề");
            if (oDe != null) oDe.text = "CH3Br + NaOH loãng";
            Bam(gbt.transform, "Loại SN2");
            yield return null;
            Bam(gbt.transform, "Nút Vẽ cơ chế");
            t0 = Time.realtimeSinceStartup;
            while ((gbt.DongCoChe.Khoi.Count == 0 || !gbt.DongCoChe.Khoi[gbt.DongCoChe.Khoi.Count - 1].CoDapAn) && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return Cho(0.7f);
            var kc = gbt.DongCoChe.Khoi.LastOrDefault();
            Check(kc != null && kc.CoDapAn && gbt.BaiKetQua != null && kc.Hinh.GetComponentInChildren<Khkt.CoChe.SoDoGiay>(true) != null,
                  "Vẽ cơ chế (máy chủ giả, loại SN2): khối đáp án có sơ đồ giấy", gbt.BaiKetQua?.Ten);
            Check(guiLoai == "SN2", "Gửi kiểu cơ chế đã chọn", guiLoai);
            yield return Chup(menu, "co_che_ket_qua_Trang");
            yield return Cho(0.4f);
            Check(gbt.Xem3DMoi == null && !kc.Khu3DMo, "Cơ chế: đáp án chỉ là lời giải trên giấy, 3D chưa mở");
            Bam(kc.transform, "Nút Xem 3D");
            yield return Cho(0.5f);
            Check(kc.Khu3DMo && gbt.Xem3DMoi != null && gbt.Xem3DMoi.TP.SoNguyenTuDangVe > 0 && kc.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "Khung 3D đáp án"),
                  "Nút Xem 3D mở khung 3D ngay dưới đáp án");
            if (gbt.Xem3DMoi != null) { gbt.Xem3DMoi.TP.DatT(0.4f); yield return null; gbt.Xem3DMoi.VeNgay(); }
            var da = gbt.Xem3DMoi != null ? gbt.Xem3DMoi.DoAnh() : (Color.clear, 0);
            Check(da.Item2 > 150, "Khung 3D trong đáp án có hình phân tử", $"{da.Item2} điểm");
            var num = kc.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "Núm");
            Check(num != null && Mathf.Abs(num.rect.width - num.rect.height) < 0.5f, "Núm thanh tốc độ tròn đều", num != null ? $"{num.rect.width:0}×{num.rect.height:0}" : "");
            int nutChinh = kc.GetComponentsInChildren<Button>(true).Count(x => x.name.StartsWith("Nút ") && x.transform.parent.name == "Thân");
            Check(nutChinh <= 4, "Cơ chế: ít nút (2 nút chính + 2 liên kết nhỏ)", nutChinh.ToString());
            yield return Chup(menu, "co_che_3d_duoi_giay_Trang");
            Bam(kc.transform, "Nút Toàn màn hình 3D");
            yield return Cho(1.0f);
            Check(Khkt.CoChe.CoCheMoDun.DangMo && Khkt.CoChe.CoCheMoDun.Instance.ToanMan, "Nút Xem 3D toàn màn hình mở trình phát toàn màn");
            Khkt.CoChe.CoCheMoDun.Instance?.BatGiaiThich(false);
            Khkt.CoChe.CoCheMoDun.Dong();
            yield return Frames(3);

            // Mô hình 3D, Hóa lý
            Bam(gbt.transform, "Điều hướng Mô hình 3D");
            yield return Cho(0.6f);
            Check(gbt.TrangDang == ManGiaiBaiTap.Trang.MoHinh3D, "Thanh điều hướng mở trang Mô hình 3D");
            yield return Chup(menu, "mo_hinh_3d_Trang");
            var oChat = gbt.GetComponentsInChildren<InputField>(true).FirstOrDefault(x => x.name == "Ô chất");
            if (oChat != null) oChat.text = "ethanol";
            Bam(gbt.transform, "Nút Dựng mô hình 3D");
            t0 = Time.realtimeSinceStartup;
            while ((gbt.Dong3D.Khoi.Count == 0 || !gbt.Dong3D.Khoi[0].CoDapAn) && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return Cho(0.7f);
            int soNt = gbt.Dong3D.Khoi.Count > 0 && gbt.Dong3D.Khoi[0].Hinh != null ? gbt.Dong3D.Khoi[0].Hinh.GetComponentsInChildren<Image>(true).Count(x => x.name.StartsWith("Nguyên tử")) : 0;
            Check(soNt == 9 && gbt.Dong3D.Khoi[0].Hinh.GetComponentInChildren<Anh3DXoay>() != null, "Dựng 3D: ảnh 3D kiểu 3Dmol của CHEMISTRY (9 nguyên tử ethanol, tự xoay)", soNt.ToString());
            yield return Chup(menu, "mo_hinh_3d_ket_qua_Trang");
            Bam(gbt.Dong3D.Khoi[0].transform, "Nút Mở lớn");
            yield return Cho(1.0f);
            Check(Khkt.CoChe.MoHinh3DMoDun.DangMo, "3D Mở lớn mở khung xoay 3D");
            Khkt.CoChe.MoHinh3DMoDun.Dong();
            yield return Frames(3);
            Bam(gbt.transform, "Điều hướng Hóa lý");
            yield return Cho(0.6f);
            int hl = gbt.GetComponentsInChildren<RectTransform>(true).Count(r => r.name.StartsWith("Hóa lý ") && r.GetComponentInParent<TrangCuon>(true)?.name == "Trang Hóa lý");
            Check(gbt.TrangDang == ManGiaiBaiTap.Trang.HoaLy && hl == 4, "Trang Hóa lý có 4 mô phỏng", hl.ToString());
            yield return Chup(menu, "hoa_ly_Trang");
            Bam(gbt.transform, "Điều hướng Trang chủ");
            yield return Cho(0.4f);
            Khkt.CoChe.MayChuCoChe.GiaLap = giaCu;

            Bam(gbt.transform, "Nút Về màn chính");
            yield return Cho(0.5f);
            Check(!gbt.DangHien && FindAnyObjectByType<ManChonLuong>().DangHien, "Về màn chính từ Giải bài tập");
        }

        static string guiLoai = "";

        /// <summary>Máy chủ giả cho trang Giải bài tập: giải chuỗi 4 bước, giải đề trả gói bài mẫu SN2, báo sai.</summary>
        static (long, string) MayChuGia(string pt, string duong, string than)
        {
            switch (duong)
            {
                case "/co-che/giai-chuoi":
                    return (200, "{\"chuoi\":\"C2H4 -> C2H5OH -> CH3CHO -> CH3COOH -> CH3COOC2H5\",\"da_kiem_chung\":true,\"chat\":[{\"ki_hieu\":\"C2H5OH\",\"ten\":\"ethanol\"},{\"ki_hieu\":\"CH3CHO\",\"ten\":\"acetaldehyde\"}],\"buoc\":[" +
                        "{\"tu\":\"C2H4\",\"den\":\"C2H5OH\",\"phan_ung\":\"cộng H₂O (hydrat hoá)\",\"dieu_kien\":\"H₂O, H₂SO₄ loãng, t°\",\"loai\":\"AE\",\"ma_co_che\":\"cong_hbr_propen\",\"khop_mau\":\"cung_loai\",\"kiem_chung\":\"da_kiem_chung\"}," +
                        "{\"tu\":\"C2H5OH\",\"den\":\"CH3CHO\",\"phan_ung\":\"oxi hoá bằng CuO\",\"dieu_kien\":\"CuO, t°\",\"loai\":\"OXI_HOA\",\"ma_co_che\":null,\"kiem_chung\":\"da_kiem_chung\"}," +
                        "{\"tu\":\"CH3CHO\",\"den\":\"CH3COOH\",\"phan_ung\":\"oxi hoá aldehyde\",\"dieu_kien\":\"O₂, Mn²⁺\",\"loai\":\"OXI_HOA\",\"ma_co_che\":null,\"kiem_chung\":\"chua_kiem_chung\"}," +
                        "{\"tu\":\"CH3COOH\",\"den\":\"CH3COOC2H5\",\"phan_ung\":\"ester hoá\",\"dieu_kien\":\"C₂H₅OH, H₂SO₄ đặc\",\"loai\":\"THE_ACYL\",\"ma_co_che\":\"este_hoa_fischer\",\"khop_mau\":\"dung_mau\",\"kiem_chung\":\"da_kiem_chung\"}]}");
                case "/co-che/giai-de":
                    var m = System.Text.RegularExpressions.Regex.Match(than, "\"loai\":\"([A-Z_0-9]*)\"");
                    guiLoai = m.Success ? m.Groups[1].Value : "";
                    return (200, Khkt.CoChe.GoiCoChe.GiaiJson(File.ReadAllBytes(Path.Combine(Khkt.CoChe.GoiCoChe.ThuMuc, "sn2_ch3br.verapack"))));
                case "/co-che/bao-sai":
                    return (200, "{\"ok\":true,\"ma\":\"gia\"}");
                case "/co-che/hoi-dap":
                    return (200, "{\"tra_loi\":\"H2SO4 đặc là chất xúc tác, hút nước làm cân bằng chuyển dịch.\",\"goi_y\":[],\"nguon\":\"ai\"}");
                case "/co-che/mo-hinh-3d":
                    return (200, "{\"ten\":\"ethanol\",\"cong_thuc\":\"C2H6O\",\"khoi_luong\":46.07,\"smiles\":\"CCO\",\"nguon\":\"bang_sgk\",\"nguyen_tu\":[" +
                        "{\"e\":\"C\",\"x\":-1.2,\"y\":0.1,\"z\":0},{\"e\":\"C\",\"x\":0.2,\"y\":-0.4,\"z\":0.1},{\"e\":\"O\",\"x\":1.2,\"y\":0.5,\"z\":-0.1}," +
                        "{\"e\":\"H\",\"x\":-1.9,\"y\":-0.6,\"z\":0.4},{\"e\":\"H\",\"x\":-1.3,\"y\":1.0,\"z\":0.6},{\"e\":\"H\",\"x\":-1.5,\"y\":0.4,\"z\":-1.0}," +
                        "{\"e\":\"H\",\"x\":0.3,\"y\":-1.1,\"z\":0.9},{\"e\":\"H\",\"x\":0.4,\"y\":-1.0,\"z\":-0.8},{\"e\":\"H\",\"x\":2.0,\"y\":0.0,\"z\":0.0}]," +
                        "\"lien_ket\":[{\"a\":0,\"b\":1,\"bac\":1},{\"a\":1,\"b\":2,\"bac\":1},{\"a\":0,\"b\":3,\"bac\":1},{\"a\":0,\"b\":4,\"bac\":1},{\"a\":0,\"b\":5,\"bac\":1},{\"a\":1,\"b\":6,\"bac\":1},{\"a\":1,\"b\":7,\"bac\":1},{\"a\":2,\"b\":8,\"bac\":1}]}");
                case "/co-che/suc-khoe":
                    return (200, "{\"ok\":true,\"mo_hinh\":\"gia\",\"so_bai\":8}");
                case "/co-che/giai-thich-chi-tiet":
                    return (200, "{\"tong_quan\":\"SN2.\",\"buoc\":[],\"luu_y\":[],\"nguon\":\"ai\",\"mo_hinh\":\"gia\",\"giay\":0.1}");
            }
            return (404, "{}");
        }

        static string DuongDan(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null && p.parent != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        static Rect KhungMan(RectTransform rt)
        {
            var g = new Vector3[4];
            rt.GetWorldCorners(g);
            return Rect.MinMaxRect(Mathf.Min(g[0].x, g[2].x), Mathf.Min(g[0].y, g[2].y), Mathf.Max(g[0].x, g[2].x), Mathf.Max(g[0].y, g[2].y));
        }

        // ------------------------------------------------------------------ chụp
        IEnumerator Chup(MainMenu _, string ten)
        {
            // chuyển động vào màn (thẻ hiện lần lượt) phải xong rồi mới chụp; tối đa 3 giây
            float t0 = Time.realtimeSinceStartup;
            while (Khkt.Menu.Dashboard.MenuAnim.Busy && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            foreach (var (w, h) in CoMan)
            {
                var m = FindAnyObjectByType<MainMenu>();              // cảnh có thể vừa nạp lại: luôn lấy màn hình đang sống
                var cam = m != null ? m.menuCamera : null;
                if (cam == null) { yield return Frames(5); continue; }
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                var cu = cam.targetTexture;
                cam.targetTexture = rt;
                yield return Frames(3);                                   // canvas dựng lại theo cỡ mới
                // bản batchmode có màn 640×480: chữ đã dựng sẵn ở cỡ đó, đổi sang cỡ chụp thì ép dựng lại cho đúng điểm ảnh
                foreach (var t in FindObjectsByType<Text>()) t.SetAllDirty();
                yield return Frames(3);
                if (cam == null) { rt.Release(); continue; }
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                if (readTex == null || readTex.width != w || readTex.height != h) readTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                readTex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = cu;
                rt.Release();
                Destroy(rt);
                string p = Path.Combine(outDir, $"{cheDo}_{ten}_{w}x{h}.png");
                File.WriteAllBytes(p, readTex.EncodeToPNG());
                shots++;
            }
            yield return Frames(2);
        }
    }
}
