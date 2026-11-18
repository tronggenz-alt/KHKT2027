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
            foreach (var (ten, lop) in new[] { ("Cơ chế 3D", "Khkt.CoChe.CoCheMoDun"), ("Thực vật 3D", "Khkt.Bio.ThucVat.ThucVatMoDun"), ("Giải phẫu", "Khkt.Bio.GiaiPhau.GiaiPhauMoDun") })
            {
                bool coLop = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(lop, false) != null);
                var t = the.FirstOrDefault(r => r.name == "Thẻ mô-đun " + ten);
                var chu = t != null ? t.GetComponentsInChildren<Text>(true).Select(x => x.text).ToList() : new List<string>();
                Check(t != null && (coLop ? !chu.Contains("Sắp có") : chu.Contains("Sắp có")), $"Thẻ {ten}: {(coLop ? "mở được" : "ghi Sắp có")} (tìm lớp {lop})");
            }
            // bấm thẻ Cơ chế 3D → mở mô-đun qua Mo() tìm theo tên lớp
            var tc = the.FirstOrDefault(r => r.name == "Thẻ mô-đun Cơ chế 3D");
            var nut = tc != null ? tc.GetComponentsInChildren<Button>(true).FirstOrDefault() : null;
            if (nut != null)
            {
                nut.onClick.Invoke();
                yield return Cho(0.5f);
                var kieu = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Khkt.CoChe.CoCheMoDun", false)).FirstOrDefault(x => x != null);
                bool mo = kieu != null && (bool)kieu.GetProperty("DangMo", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Check(mo, "Thẻ Cơ chế 3D gọi CoCheMoDun.Mo()");
                kieu?.GetMethod("Dong", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                yield return Frames(3);
            }
            else Check(false, "Thẻ Cơ chế 3D có nút");
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
