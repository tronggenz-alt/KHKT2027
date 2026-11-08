using System;
using System.Collections;
using System.IO;
using System.Linq;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Chỉ có trong bản sao project: app đăng nhập học sinh, tools/kiem_thu_unity/giao_vien_thu.py đóng vai giáo viên gọi API thật.
// Hai bên trao đổi bước qua tệp đánh dấu trong KHKT_NET_DIR.
public class KhktSlideTest : MonoBehaviour
{
    static bool started;
    int pass, fail, errors;
    string dir, outDir;

    void Awake()
    {
        if (started) { Destroy(gameObject); return; }
        started = true;
        DontDestroyOnLoad(gameObject);
        dir = Environment.GetEnvironmentVariable("KHKT_NET_DIR");
        outDir = Environment.GetEnvironmentVariable("KHKT_SHOTS") ?? dir;
        Application.logMessageReceived += (m, st, t) =>
        {
            if (t != LogType.Exception && t != LogType.Error) return;
            errors++;
            Debug.Log("[KHKT-PLAY] [slide] LỖI LOG: " + m.Split('\n')[0]);
        };
    }

    void Check(string name, bool ok, string detail = "")
    {
        if (ok) pass++; else fail++;
        Debug.Log($"[KHKT-PLAY] [slide] {(ok ? "ĐẠT" : "HỎNG")}: {name} {detail}");
    }

    IEnumerator Start()
    {
        yield return Run();
        Check("không có lỗi nào trong log", errors == 0, $"{errors} lỗi");
        Debug.Log($"[KHKT-PLAY] [slide] Tổng: đạt {pass}, hỏng {fail}");
        File.WriteAllText(Path.Combine(dir, "app_xong"), "1");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#else
        Application.Quit();
#endif
    }

    IEnumerator Run()
    {
        var acc = File.ReadAllLines(Environment.GetEnvironmentVariable("KHKT_MENU_TK"));
        PlayerPrefs.DeleteKey("vera.may_chu");
        GameSession.SignOut();
        yield return Frames(5);
        var menu = FindAnyObjectByType<MainMenu>();
        Field("Ô đăng nhập: tên").text = acc[0];
        Field("Ô đăng nhập: mật khẩu").text = acc[1];
        Click("Nút Đăng nhập");
        float t0 = Time.realtimeSinceStartup;
        while (menu != null && menu.Current != MainMenu.Screen.Home && Time.realtimeSinceStartup - t0 < 10f) yield return null;
        Check("học sinh đăng nhập được", GameSession.User != null);
        Click("Nút Vào phòng thí nghiệm");
        t0 = Time.realtimeSinceStartup;
        while (PresentationClient.Instance == null && Time.realtimeSinceStartup - t0 < 120f) yield return null;
        yield return Seconds(3f);

        var pc = PresentationClient.Instance;
        Check("có PresentationClient trong lab", pc != null);
        if (pc == null) yield break;
        var viewer = FindAnyObjectByType<DesktopViewer>();
        var screen = LabScreen.Find("tuong_video_2");
        var other = LabScreen.All.FirstOrDefault(s => s.screenId.StartsWith("ban_"));
        Check("tìm thấy màn hình tường video và màn hình bàn", screen != null && other != null, other != null ? other.screenId : "");
        if (screen == null) yield break;
        string firstTitle = screen.Current != null ? screen.Current.title : "";
        yield return WaitVersion(pc, 8f);  // nội dung còn lại từ lần trước (nếu có) hiện xong đã
        Mark("app_san_sang");
        Shot(screen, "trinh_chieu_truoc");

        // Slide trang 1
        long v0 = pc.ShownVersion;
        double lag1 = 0;
        yield return Step(pc, "s1", v0, l => lag1 = l);
        Check("slide 1 hiện trên tường video", screen.IsPresenting && screen.Current.image != null, $"trễ {lag1:0.00} s");
        var tex1 = screen.Current.image;
        Check("màn hình bàn không nằm trong danh sách thì giữ nguyên", other == null || !other.IsPresenting);
        Shot(screen, "trinh_chieu_trang_1");

        screen.Use();
        Check("đang chiếu thì bấm màn hình không đổi trang", screen.IsPresenting && screen.Current.image == tex1);
        // màn hình này mặc định không tự chuyển: bật tạm để mục kiểm tra có nghĩa
        float auto0 = screen.autoAdvanceS;
        int pages0 = screen.PageIndex;
        screen.autoAdvanceS = 1f;
        yield return Seconds(2.5f);
        Check("đang chiếu thì không tự chuyển trang", screen.IsPresenting && screen.Current.image == tex1 && screen.PageIndex == pages0, "đã bật tự chuyển 1 s, đợi 2,5 s");
        screen.autoAdvanceS = auto0;
        Mark("s1_xong", lag1.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));

        // Đổi sang trang 2
        double lag2 = 0;
        yield return Step(pc, "s2", pc.ShownVersion, l => lag2 = l);
        var tex2 = screen.Current != null ? screen.Current.image : null;
        Check("đổi trang 2 hiện ảnh mới", tex2 != null && tex2 != tex1, $"trễ {lag2:0.00} s");
        Check("ảnh trang cũ đã bị xóa khỏi bộ nhớ", tex1 == null);
        Check("không thêm trang vào danh sách của màn hình", screen.pages.All(p => p.image != tex2));
        Shot(screen, "trinh_chieu_trang_2");

        // Tầng 2: màn hình ở khu chưa tải phải nhận đúng nội dung khi tới nơi
        int sinh = System.Array.FindIndex(Khkt.Rendering.ZoneMap.Zones, z => z.id == "phong_sinh");
        var st = viewer.Stations.FirstOrDefault(x => Khkt.Rendering.ZoneMap.At(x.transform.position) == sinh);
        if (st != null)
        {
            viewer.GoTo(st);
            t0 = Time.realtimeSinceStartup;
            while (LabScreen.Find("t2_tuong_video_1") == null && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return Seconds(1.5f);
            var t2 = LabScreen.Find("t2_tuong_video_1");
            if (t2 != null) Shot(t2, "trinh_chieu_tang_2");
            Check("màn hình tầng 2 vừa tải cũng hiện slide", t2 != null && t2.IsPresenting && t2.Current.image == tex2, t2 != null ? t2.screenId : "chưa tải");
            viewer.GoTo(2);
            yield return Seconds(3f);
        }
        else Check("có trạm tầng 2 để thử", false);
        screen = LabScreen.Find("tuong_video_2");
        Mark("s2_xong", lag2.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));

        // Chữ, có đếm ngược
        double lag3 = 0;
        yield return Step(pc, "s3", pc.ShownVersion, l => lag3 = l);
        Check("chữ hiện trên tường video", screen != null && screen.IsPresenting && !string.IsNullOrEmpty(screen.Current.title) && screen.Current.image == null, $"trễ {lag3:0.00} s");
        string b1 = screen != null ? screen.bodyText != null ? screen.bodyText.text : screen.Current.body : "";
        yield return Seconds(2.2f);
        string b2 = screen != null ? screen.bodyText != null ? screen.bodyText.text : screen.Current.body : "";
        Check("đếm ngược chạy", b1.Contains("Còn") && b1 != b2, b1.Replace('\n', ' ') + " -> " + b2.Replace('\n', ' '));
        Shot(screen, "trinh_chieu_chu");
        Mark("s3_xong", lag3.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));

        // Tắt
        double lag4 = 0;
        yield return Step(pc, "s4", pc.ShownVersion, l => lag4 = l);
        Check("tắt: màn hình về như cũ", screen != null && !screen.IsPresenting && (screen.Current == null || screen.Current.title == firstTitle || screen.Current.image != null), $"trễ {lag4:0.00} s");
        Check("tắt: ảnh đã xóa", tex2 == null);
        Check("màn hình bàn vẫn không bị chiếu đè", other == null || !other.IsPresenting);
        Shot(screen, "trinh_chieu_sau_tat");
        Mark("s4_xong", lag4.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));

        // Giai đoạn 6: vị trí, đồ đang cầm, thông báo, chú ý
        viewer.GoTo(2);
        yield return Seconds(4.5f);
        Mark("vt1", $"{viewer.transform.position.x:0.0} {viewer.transform.position.z:0.0}");
        yield return WaitFile("vt1_doc", 30);
        viewer.GoTo(5);
        yield return Seconds(4.5f);
        var hand = HandController.Active;
        var pick = Khkt.Rendering.ZoneRoot.FindAll<Khkt.Interaction.Grabbable>()
            .Where(x => x.gameObject.activeInHierarchy)
            .OrderBy(x => (x.transform.position - viewer.transform.position).sqrMagnitude).FirstOrDefault();
        if (pick != null) hand.Pick(pick);
        yield return Seconds(4.5f);
        Mark("vt2", $"{viewer.transform.position.x:0.0} {viewer.transform.position.z:0.0} {(pick != null ? pick.name : "")}");
        yield return WaitFile("vt2_doc", 30);
        if (pick != null) hand.ReturnHome();

        yield return WaitFile("s5_luc", 30);
        t0 = Time.realtimeSinceStartup;
        while (pc.LastNotice.Length == 0 && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        Check("thông báo của giáo viên hiện trong app", pc.LastNotice.Contains("Chuẩn bị"), pc.LastNotice);
        long ack0 = pc.AckedNotice;
        ShotMain("truc_tiep_thong_bao");
        t0 = Time.realtimeSinceStartup;
        while (pc.AckedNotice == ack0 && Time.realtimeSinceStartup - t0 < 20f) yield return null;
        Check("hiện đủ lâu rồi mới báo đã nhận", pc.AckedNotice != ack0 && Time.realtimeSinceStartup - t0 >= 4f, $"{Time.realtimeSinceStartup - t0:0.0} s");
        Mark("s5_xong");

        yield return WaitFile("s6_luc", 30);
        t0 = Time.realtimeSinceStartup;
        while (!pc.IsLocked && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        Check("yêu cầu chú ý: khóa điều khiển", pc.IsLocked && DesktopViewer.UiCapture > 0);
        yield return Frames(3);
        ShotMain("truc_tiep_chu_y");
        // lúc khóa, kênh giả lập phím không vào được: kiểm tra đúng cờ mà DesktopViewer dùng để dừng đi lại
        Check("lúc khóa con trỏ được thả, không mở thêm bảng khác", DesktopViewer.UiCapture == 1, DesktopViewer.UiCapture.ToString());
        yield return Seconds(2.5f);
        Mark("s6_xong");
        yield return WaitFile("s7_luc", 30);
        t0 = Time.realtimeSinceStartup;
        while (pc.IsLocked && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        Check("giáo viên tắt: mở lại điều khiển", !pc.IsLocked && DesktopViewer.UiCapture == 0);
        Mark("s7_xong");

        // Mất máy chủ lúc đang khóa: không được kẹt
        yield return WaitFile("s8_luc", 30);
        t0 = Time.realtimeSinceStartup;
        while (!pc.IsLocked && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        Check("khóa lại lần nữa", pc.IsLocked);
        string goodUrl = GameSession.ServerUrl;
        GameSession.ServerUrl = "http://127.0.0.1:18799";
        t0 = Time.realtimeSinceStartup;
        while (pc.IsLocked && Time.realtimeSinceStartup - t0 < 30f) yield return null;
        Check("mất máy chủ thì tự mở khóa sau khoảng 15 giây", !pc.IsLocked && Time.realtimeSinceStartup - t0 >= 10f, $"{Time.realtimeSinceStartup - t0:0} s");
        GameSession.ServerUrl = goodUrl;
        Mark("s8_xong");
        yield return WaitFile("s9_xong", 20);

        // Giai đoạn 7: Kiểm tra, khóa hóa chất, lệnh robot
        yield return WaitFile("s10_luc", 30);
        t0 = Time.realtimeSinceStartup;
        while (!ClassRules.Exam && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        yield return Frames(3);
        Check("chế độ Kiểm tra: bật", ClassRules.Exam && ClassRules.HideHelp && ClassRules.ChatLocked);
        Check("đồng hồ đếm ngược hiện", pc.ExamClockVisible && pc.ExamClockText.Contains("còn"), pc.ExamClockText);
        Check("lọ số 1 bị khóa", ClassRules.IsLocked(1));
        var bottle = ChemSearchUI.Instance != null ? ChemSearchUI.Instance.FindBottle(1) : null;
        if (bottle != null)
        {
            hand.Pick(bottle.GetComponent<Khkt.Interaction.Grabbable>());
            Check("không cầm được lọ bị khóa", hand.Held == null);
        }
        else Check("tìm thấy lọ số 1", false);
        var chat = FindAnyObjectByType<RobotChatUI>();
        if (chat != null) chat.Open();
        Check("khóa khung trò chuyện robot", chat == null || !chat.IsOpen);
        ShotMain("kiem_tra");
        Mark("s10_xong");

        yield return WaitFile("s11_luc", 30);
        t0 = Time.realtimeSinceStartup;
        while (pc.LastRobotCommand == 0 && Time.realtimeSinceStartup - t0 < 10f) yield return null;
        Check("lệnh robot của giáo viên chạy cả khi đang Kiểm tra", pc.LastRobotCommand != 0);
        yield return Seconds(2.5f);
        Mark("s11_xong");

        yield return WaitFile("s12_luc", 30);
        t0 = Time.realtimeSinceStartup;
        while (ClassRules.Exam && Time.realtimeSinceStartup - t0 < 8f) yield return null;
        yield return Frames(3);
        Check("về Luyện tập: mở gợi ý, trò chuyện, bỏ khóa, ẩn đồng hồ", !ClassRules.Exam && !ClassRules.ChatLocked && !ClassRules.IsLocked(1) && !pc.ExamClockVisible);
        Mark("s12_xong");

        // Hết phiên giữa chừng
        yield return Seconds(1f);
        GameSession.Token = "ma_phien_sai_cua_unity";
        t0 = Time.realtimeSinceStartup;
        while (GameSession.Token != null && Time.realtimeSinceStartup - t0 < 6f) yield return null;
        Check("nhịp trả 401: bỏ mã, chuyển dùng thử, không lỗi", GameSession.Token == null && GameSession.GuestActive);
    }

    IEnumerator Step(PresentationClient pc, string step, long before, Action<double> done)
    {
        string f = Path.Combine(dir, step + "_luc");
        float t0 = Time.realtimeSinceStartup;
        while (!File.Exists(f) && Time.realtimeSinceStartup - t0 < 30f) yield return null;
        if (!File.Exists(f)) { Check("giáo viên đã đổi " + step, false); yield break; }
        double push = double.Parse(File.ReadAllText(f).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        t0 = Time.realtimeSinceStartup;
        while (pc.ShownVersion == before && Time.realtimeSinceStartup - t0 < 15f) yield return null;
        double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        Check("nhận " + step, pc.ShownVersion != before);
        done(now - push);
    }

    IEnumerator WaitVersion(PresentationClient pc, float seconds)
    {
        yield return Seconds(seconds);
    }

    IEnumerator WaitFile(string name, float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (!File.Exists(Path.Combine(dir, name)) && Time.realtimeSinceStartup - t0 < timeout) yield return null;
        if (!File.Exists(Path.Combine(dir, name))) Check("đợi " + name, false);
    }

    void ShotMain(string name)
    {
        var c = FindAnyObjectByType<ModernUI>().viewCamera;
        Canvas.ForceUpdateCanvases();
        var old = c.targetTexture;
        var rt = new RenderTexture(1600, 900, 24);
        c.targetTexture = rt;
        c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        c.targetTexture = old;
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        Destroy(rt);
        Destroy(tex);
    }

    void Mark(string name, string content = "1") => File.WriteAllText(Path.Combine(dir, name), content);

    void Shot(LabScreen s, string name)
    {
        if (s == null || s.surface == null) return;
        Vector3 c = s.surface.bounds.center;
        Vector3 best = Vector3.forward;
        float far = -1f;
        foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            float dist = Physics.Raycast(c + d * 0.1f, d, out var hit, 8f) ? hit.distance : 8f;
            if (dist > far) { far = dist; best = d; }
        }
        var go = new GameObject("Camera chụp");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        go.transform.position = c + best * Mathf.Min(3.2f, far - 0.3f) + Vector3.down * 0.2f;
        go.transform.LookAt(c);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = null;
        Destroy(go);
        Destroy(rt);
        Destroy(tex);
    }

    static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
    static IEnumerator Seconds(float s) { yield return new WaitForSecondsRealtime(s); }
    static InputField Field(string name) => FindObjectsByType<InputField>().First(f => f.name == name);

    static void Click(string name)
    {
        var b = FindObjectsByType<Button>().FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
        if (b != null) b.onClick.Invoke();
        else Debug.LogError("Không thấy nút " + name);
    }
}
