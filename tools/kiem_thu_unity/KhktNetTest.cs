using System;
using System.Collections;
using System.IO;
using System.Linq;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Menu;
using Khkt.Network;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.UI;

// Chỉ có trong bản sao project: ba tiến trình thật cùng vào một phòng, trao đổi bước qua tệp đánh dấu trong KHKT_NET_DIR.
// Vai trò: -khkt-net host | c1 | c2. Đường nối: KHKT_NET_ROUTE = lan | relay.
public class KhktNetTest : MonoBehaviour
{
    static bool started;
    string role, dir, route;
    int pass, fail, errors;
    DesktopViewer viewer;
    HandController hand;
    LabPresence lab;

    void Awake()
    {
        role = Arg("-khkt-net");
        if (started || role == null) { Destroy(gameObject); return; }
        started = true;
        DontDestroyOnLoad(gameObject);
        dir = Environment.GetEnvironmentVariable("KHKT_NET_DIR");
        route = Environment.GetEnvironmentVariable("KHKT_NET_ROUTE") ?? "lan";
        RoomConnection.LanListenAddress = "127.0.0.1";  // chỉ trên máy này, Windows không hỏi tường lửa
        Application.logMessageReceived += (m, st, t) =>
        {
            if (t != LogType.Exception && t != LogType.Error) return;
            errors++;
            Note("LỖI LOG: " + m.Split('\n')[0]);
        };
    }

    IEnumerator Start()
    {
        yield return null;
        float t0 = Time.realtimeSinceStartup;
        if (role == "host") yield return Host();
        else if (role == "c1") yield return Client1();
        else yield return Client2();
        Check("không có lỗi nào trong log", errors == 0, $"{errors} lỗi");
        Note($"Tổng: đạt {pass}, hỏng {fail}, {Time.realtimeSinceStartup - t0:0} s");
        File.WriteAllText(Path.Combine(dir, role + "_xong"), $"{pass} {fail}");
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    // ------------------------------------------------------------------ máy mở phòng

    IEnumerator Host()
    {
        yield return Frames(5);
        GameSession.GuestActive = true;
        var menu = FindAnyObjectByType<MainMenu>();
        menu.Show(MainMenu.Screen.Room);
        yield return Frames(2);
        Shot("net_man_phong");
        yield return Frames(3);
        Click(route == "relay" ? "Nút Mở phòng Internet" : "Nút Mở phòng LAN");
        yield return WaitRoomResult();
        if (!RoomConnection.InRoom) { Check("mở được phòng", false, RoomConnection.Instance.Error); Mark("h_mo", "LOI"); yield break; }
        Check("mở được phòng, có mã", RoomConnection.Instance.Code.Length > 0, route == "relay" ? "mã Relay" : RoomConnection.Instance.Code);
        Mark("h_mo", route == "relay" ? RoomConnection.Instance.Code : "127.0.0.1");
        yield return EnterLab();

        yield return WaitMark("c1_vao", 90);
        yield return WaitUntil(() => lab.RemoteCount == 1, 8);
        Check("máy mở phòng thấy người thứ hai", lab.RemoteCount == 1);

        viewer.GoTo(5);
        yield return Seconds(1.5f);
        Mark("h_doi_cho", Vec(viewer.cameraTransform.position));
        yield return WaitMark("c1_thay_doi_cho", 20);

        viewer.GoTo(2);
        yield return Seconds(1.5f);
        var g = PickTarget();
        Check("có vật để thử", g != null, g != null ? g.name : "");
        int id = lab.IdOf(g);
        Vector3 home = g.transform.position;
        hand.Pick(g);
        yield return Seconds(0.8f);
        Mark("h_cam_goc", Vec(home));
        Mark("h_cam_vt", Vec(g.transform.position));
        Mark("h_cam", id.ToString());
        yield return WaitMark("c1_thay_cam", 15);

        Vector3 put = home + new Vector3(0.12f, 0, 0.05f);
        hand.ReleaseHeldInto(g.transform.parent, put, g.transform.rotation);
        Mark("h_tha", Vec(put));
        yield return WaitMark("c1_thay_tha", 15);

        // Người khác cầm: máy này không chọn được, cố cầm thì bị từ chối
        yield return WaitMark("c1_cam", 15);
        yield return Seconds(0.8f);
        Check("người khác cầm thì vật bị khóa ở máy này", lab.IsRemoteHeld(id) && g.IsHeld);
        bool colliderOff = g.GetComponentsInChildren<Collider>(true).All(c => !c.enabled);
        Check("vật bị khóa không chọn được (tắt va chạm)", colliderOff);
        hand.Pick(g);
        yield return Seconds(1f);
        Check("cố cầm vật người khác đang cầm thì bị từ chối", hand.Held == null);
        Check("vật vẫn thuộc người đang cầm", lab.IsRemoteHeld(id) && g.IsHeld);
        Mark("h_bi_tu_choi");

        yield return WaitMark("c1_tha", 15);
        var c = ReadMark("c1_tha");
        yield return Seconds(0.8f);
        Check("thấy người kia thả vật đúng chỗ", Vector3.Distance(g.transform.position, ParseVec(c)) < 0.02f, $"lệch {Vector3.Distance(g.transform.position, ParseVec(c)) * 100:0.0} cm");
        Check("thả xong lại chọn được", g.GetComponentsInChildren<Collider>(true).Any(x => x.enabled) && !lab.IsRemoteHeld(id));

        // Đo băng thông khi có một người khác, một người đang cầm vật
        long s0 = lab.PayloadBytesSent, r0 = lab.PayloadBytesReceived;
        hand.Pick(g);
        yield return Seconds(10f);
        Note($"Dữ liệu (chưa tính gói mạng), 2 người, 1 vật đang cầm: gửi {(lab.PayloadBytesSent - s0) / 10f:0} B/s, nhận {(lab.PayloadBytesReceived - r0) / 10f:0} B/s");
        hand.ReleaseHeldInto(g.transform.parent, put, g.transform.rotation);
        yield return Seconds(0.5f);

        Mark("h_doi_c2");
        yield return WaitMark("c2_xong_vao", 90);
        yield return WaitUntil(() => lab.RemoteCount == 2, 8);
        Check("người đến sau: máy mở phòng thấy 2 người", lab.RemoteCount == 2);
        Mark("h_thay_c2");
        yield return WaitMark("c2_roi", 30);
        yield return WaitUntil(() => lab.RemoteCount == 1, 6);
        Check("một người rời phòng thì hình người đó biến mất", lab.RemoteCount == 1);

        Mark("h_dong");
        yield return Seconds(1f);
        GameSession.ReturnToMenu();
        yield return Seconds(1.5f);
        Check("đóng phòng, về màn hình chính", !RoomConnection.InRoom && FindAnyObjectByType<MainMenu>() != null);
    }

    // ------------------------------------------------------------------ người thứ hai

    IEnumerator Client1()
    {
        yield return WaitMark("h_mo", 120);
        string code = ReadMark("h_mo");
        if (code == "LOI" || code == null) { Check("máy mở phòng sẵn sàng", false, code); yield break; }
        yield return Frames(5);
        GameSession.GuestActive = true;
        var menu = FindAnyObjectByType<MainMenu>();
        menu.Show(MainMenu.Screen.Room);
        yield return Frames(2);
        Field("Ô mã phòng").text = code;
        Click("Nút Vào phòng");
        yield return WaitRoomResult();
        if (!RoomConnection.InRoom) { Check("vào được phòng", false, RoomConnection.Instance.Error); yield break; }
        Check("vào được phòng bằng mã", true);
        yield return EnterLab();
        Mark("c1_vao");

        yield return WaitUntil(() => lab.RemoteCount >= 1, 10);
        Check("thấy máy mở phòng", lab.RemoteCount == 1);

        yield return WaitMark("h_doi_cho", 20);
        Vector3 hostPos = ParseVec(ReadMark("h_doi_cho"));
        yield return Seconds(1f);
        Vector3 seen = Vector3.zero;
        string name = "";
        bool has = lab.RemoteHeadOf(0, out seen, out name);
        Check("thấy người kia đúng vị trí", has && Vector3.Distance(seen, hostPos) < 0.5f, has ? $"lệch {Vector3.Distance(seen, hostPos) * 100:0} cm, tên {name}" : "");
        ShotRemote();
        Mark("c1_thay_doi_cho");

        viewer.GoTo(2);
        yield return WaitMark("h_cam", 30);
        int id = int.Parse(ReadMark("h_cam"));
        var g = lab.ObjectById(id);
        Check("cùng mã vật trỏ tới cùng một vật", g != null, g != null ? g.name : "");
        Vector3 home = ParseVec(ReadMark("h_cam_goc")), heldAt = ParseVec(ReadMark("h_cam_vt"));
        yield return Seconds(1f);
        Check("thấy vật bị khóa khi người kia cầm", g != null && g.IsHeld && lab.IsRemoteHeld(id));
        Check("thấy vật di chuyển theo tay người kia", g != null && Vector3.Distance(heldAt, home) > 0.03f && Vector3.Distance(g.transform.position, heldAt) < 0.05f, g != null ? $"máy cầm dời {Vector3.Distance(heldAt, home) * 100:0} cm, máy này lệch {Vector3.Distance(g.transform.position, heldAt) * 100:0.0} cm" : "");
        Mark("c1_thay_cam");

        yield return WaitMark("h_tha", 15);
        Vector3 put = ParseVec(ReadMark("h_tha"));
        yield return Seconds(0.8f);
        Check("thả xuống đúng chỗ trên máy này", g != null && Vector3.Distance(g.transform.position, put) < 0.02f, g != null ? $"lệch {Vector3.Distance(g.transform.position, put) * 100:0.0} cm" : "");
        Check("thả xong mở khóa", g != null && !g.IsHeld && !lab.IsRemoteHeld(id));
        Mark("c1_thay_tha");

        hand.Pick(g);
        yield return Seconds(0.6f);
        Check("đến lượt máy này cầm", hand.Held == g);
        Mark("c1_cam");
        yield return WaitMark("h_bi_tu_choi", 20);
        Check("vẫn đang cầm sau khi máy kia cố giành", hand.Held == g);
        Vector3 back = put + new Vector3(-0.08f, 0, 0.03f);
        hand.ReleaseHeldInto(g.transform.parent, back, g.transform.rotation);
        Mark("c1_tha", Vec(back));

        yield return WaitMark("h_dong", 180);
        yield return WaitUntil(() => !RoomConnection.InRoom, 12);
        Check("máy mở phòng đóng: tự rời phòng, không treo", !RoomConnection.InRoom && FindAnyObjectByType<DesktopViewer>() != null);
        yield return Seconds(1f);
        Check("vẫn làm tiếp một mình được", g == null || !g.IsHeld);
        GameSession.ReturnToMenu();
        yield return Seconds(1.5f);
        Check("về màn hình chính sau khi mất phòng", FindAnyObjectByType<MainMenu>() != null);
    }

    // ------------------------------------------------------------------ người đến sau

    IEnumerator Client2()
    {
        yield return WaitMark("h_doi_c2", 240);
        string code = ReadMark("h_mo");
        string put = ReadMark("h_tha");
        yield return Frames(5);
        GameSession.GuestActive = true;
        var menu = FindAnyObjectByType<MainMenu>();
        menu.Show(MainMenu.Screen.Room);
        yield return Frames(2);
        Field("Ô mã phòng").text = code;
        Click("Nút Vào phòng");
        yield return WaitRoomResult();
        if (!RoomConnection.InRoom) { Check("vào được phòng", false, RoomConnection.Instance.Error); yield break; }
        yield return EnterLab();
        viewer.GoTo(2);
        yield return Seconds(2f);
        Mark("c2_xong_vao");
        yield return WaitUntil(() => lab.RemoteCount == 2, 10);
        Check("người đến sau thấy 2 người", lab.RemoteCount == 2);
        int id = int.Parse(ReadMark("h_cam"));
        var g = lab.ObjectById(id);
        Check("người đến sau thấy vật ở đúng chỗ đã bị dời", g != null && Vector3.Distance(g.transform.position, ParseVec(put)) < 0.02f, g != null ? $"lệch {Vector3.Distance(g.transform.position, ParseVec(put)) * 100:0.0} cm" : "");
        yield return WaitMark("h_thay_c2", 15);
        GameSession.ReturnToMenu();
        yield return Seconds(1.5f);
        Check("rời phòng về màn hình chính", !RoomConnection.InRoom && FindAnyObjectByType<MainMenu>() != null);
        Mark("c2_roi");
    }

    // ------------------------------------------------------------------ tiện ích

    // Đợi tới khi vào được phòng (đã chuyển màn tải) hoặc báo lỗi
    IEnumerator WaitRoomResult()
    {
        float t0 = Time.realtimeSinceStartup;
        var room = RoomConnection.Instance;
        while (Time.realtimeSinceStartup - t0 < 40f)
        {
            if (RoomConnection.InRoom && FindAnyObjectByType<IntroScreen>() != null) yield break;
            var m = FindAnyObjectByType<MainMenu>();
            if (m != null && m.Current == MainMenu.Screen.Loading) { t0 = Time.realtimeSinceStartup; }
            if (room.Error.Length > 0) yield break;
            yield return null;
        }
    }

    IEnumerator EnterLab()
    {
        yield return WaitUntil(() => LabPresence.Instance != null, 90);
        yield return Seconds(2f);
        lab = LabPresence.Instance;
        viewer = FindAnyObjectByType<DesktopViewer>();
        hand = HandController.Active;
    }

    Grabbable PickTarget()
    {
        Vector3 me = viewer.cameraTransform.position;
        return ZoneRoot.FindAll<Grabbable>()
            .Where(x => x.gameObject.activeInHierarchy && lab.IdOf(x) != 0 && x.GetComponent<Khkt.Assembly.ApparatusPart>() == null && x.GetComponent<BuretStopcock>() == null)
            .OrderBy(x => (x.transform.position - me).sqrMagnitude)
            .FirstOrDefault();
    }

    void ShotRemote()
    {
        if (!lab.RemoteHeadOf(0, out var head, out _)) return;
        var go = new GameObject("Camera chụp");
        var c = go.AddComponent<Camera>();
        Vector3 best = Vector3.forward;
        float far = 0f;
        for (int i = 0; i < 16; i++)
        {
            Vector3 dir2 = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
            float d = Physics.Raycast(head, dir2, out var hit, 6f) ? hit.distance : 6f;
            if (d > far) { far = d; best = dir2; }
        }
        go.transform.position = head + best * Mathf.Min(1.5f, far - 0.25f) + Vector3.up * 0.05f;
        go.transform.LookAt(head + Vector3.down * 0.4f);
        var rt = new RenderTexture(1280, 720, 24);
        c.targetTexture = rt;
        c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, "net_nguoi_cung_phong.png"), tex.EncodeToPNG());
        c.targetTexture = null;
        Destroy(go);
        Destroy(rt);
        Destroy(tex);
    }

    void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));

    void Check(string name, bool ok, string detail = "")
    {
        if (ok) pass++; else fail++;
        Note($"{(ok ? "ĐẠT" : "HỎNG")}: {name} {detail}");
    }

    void Note(string text)
    {
        string line = $"[{role}] {text}";
        Debug.Log("[KHKT-PLAY] [net] " + line);
        try { File.AppendAllText(Path.Combine(dir, "ket_qua.txt"), line + "\n"); } catch (IOException) { }
    }

    void Mark(string name, string content = "1") => File.WriteAllText(Path.Combine(dir, name), content);

    string ReadMark(string name)
    {
        string p = Path.Combine(dir, name);
        return File.Exists(p) ? File.ReadAllText(p).Trim() : null;
    }

    IEnumerator WaitMark(string name, float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (!File.Exists(Path.Combine(dir, name)) && Time.realtimeSinceStartup - t0 < timeout) yield return null;
        if (!File.Exists(Path.Combine(dir, name))) Note("quá giờ đợi " + name);
        yield return null;
    }

    static IEnumerator WaitUntil(Func<bool> f, float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (!f() && Time.realtimeSinceStartup - t0 < timeout) yield return null;
    }

    static IEnumerator Seconds(float s) { yield return new WaitForSecondsRealtime(s); }
    static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    static InputField Field(string name) => FindObjectsByType<InputField>().First(f => f.name == name);

    static void Click(string name)
    {
        var b = FindObjectsByType<Button>().FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
        if (b != null) b.onClick.Invoke();
        else Debug.LogError("Không thấy nút " + name);
    }

    static string Vec(Vector3 v)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return v.x.ToString("R", ci) + " " + v.y.ToString("R", ci) + " " + v.z.ToString("R", ci);
    }

    static Vector3 ParseVec(string s)
    {
        if (string.IsNullOrEmpty(s)) return Vector3.positiveInfinity;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var p = s.Split(' ');
        return new Vector3(float.Parse(p[0], ci), float.Parse(p[1], ci), float.Parse(p[2], ci));
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }
}
