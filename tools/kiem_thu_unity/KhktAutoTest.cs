using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEngine;

// Chỉ có trong bản sao project để kiểm thử: chuẩn độ bằng bàn tay ảo, thí nghiệm định tính, tra cứu, robot; chụp ảnh.
public class KhktAutoTest : MonoBehaviour
{
    Camera cam;
    RenderTexture rt;
    Texture2D tex;
    string outDir;
    LoupeCamera loupe;
    DesktopViewer viewer;
    HandController hand;

    IEnumerator Start()
    {
        outDir = Environment.GetEnvironmentVariable("KHKT_SHOTS");
        Directory.CreateDirectory(outDir);
        float t0 = Time.realtimeSinceStartup;
        StartCoroutine(Watchdog());

        viewer = FindAnyObjectByType<DesktopViewer>();
        cam = viewer.cameraTransform.GetComponent<Camera>();
        hand = FindAnyObjectByType<HandController>();
        var proc = FindAnyObjectByType<LabProcedure>();
        loupe = FindAnyObjectByType<LoupeCamera>();
        rt = new RenderTexture(1600, 900, 24);
        tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        cam.targetTexture = rt;
        yield return new WaitForSeconds(1f);
        Log($"Danh mục: {ChemicalCatalog.Instance.Chemicals.Length} chất, lọ trong cảnh: {FindObjectsByType<ChemicalBottle>().Length}, vật chứa phản ứng: {FindObjectsByType<LabContainer>().Count(c => c.reactive)}");
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "goc_nhin")
        {
            // 12 góc nhìn cố định để so sánh trước / sau khi thiết kế lại
            var views = new (string name, Vector3 pos, Vector3 look)[]
            {
                ("01_toan_canh_cua_vao", new Vector3(1.6f, 0, 0.9f), new Vector3(12f, 1.0f, 9f)),
                ("02_tuong_bang", new Vector3(9.5f, 0, 7.5f), new Vector3(0.2f, 1.5f, 7.5f)),
                ("03_ban_hoc_sinh", new Vector3(15.5f, 0, 5.0f), new Vector3(6f, 0.9f, 8.5f)),
                ("04_ban_chuan_do", new Vector3(6.05f, 0, 2.6f), new Vector3(5.2f, 1.0f, 2.6f)),
                ("05_khu_tong_hop", new Vector3(12.5f, 0, 10.5f), new Vector3(17f, 1.0f, 8.5f)),
                ("06_tu_hut", new Vector3(16.5f, 0, 11.0f), new Vector3(16.8f, 1.2f, 14.6f)),
                ("07_loi_vao_kho", new Vector3(16.3f, 0, 5.0f), new Vector3(20.0f, 1.55f, 7.7f)),
                ("08_kho_ben_trong", new Vector3(21.3f, 0, 2.0f), new Vector3(24.5f, 1.3f, 9f)),
                ("09_hanh_lang", new Vector3(4.2f, 0, -1.3f), new Vector3(16f, 1.5f, -0.4f)),
                ("10_phong_may", new Vector3(2.6f, 0, -3.0f), new Vector3(6.75f, 1.0f, -8.4f)),
                ("11_phong_robot", new Vector3(21.5f, 0, -3.4f), new Vector3(18.5f, 1.0f, -6.5f)),
                ("12_khu_an_toan", new Vector3(4.5f, 0, 4.0f), new Vector3(0.5f, 1.2f, 1.0f)),
                ("13_tuong_video_gan", new Vector3(4.6f, 0, 7.5f), new Vector3(0.2f, 1.7f, 7.5f)),
                ("14_tuong_nha_hoa_hoc", new Vector3(3.2f, 0, 12.6f), new Vector3(0.1f, 1.55f, 12.6f)),
                ("15_hanh_lang_lich_su", new Vector3(6.6f, 0, -1.95f), new Vector3(6.15f, 1.5f, -0.1f)),
                ("16_tuong_trien_lam", new Vector3(11.2f, 0, 4.7f), new Vector3(10.6f, 1.45f, 0.1f)),
                ("17_cua_phong_may", new Vector3(6.7f, 0, -0.45f), new Vector3(6.7f, 1.35f, -2.2f)),
                ("18_tran_go_hang_ban", new Vector3(18.5f, 0, 5.0f), new Vector3(8f, 2.9f, 7.5f)),
            };
            foreach (var v in views)
            {
                Vector3 d = v.pos - v.look;
                viewer.GoNear(v.look, d, new Vector2(d.x, d.z).magnitude);
                yield return Frames(12);
                Shot(v.name);
            }
            Log($"[goc_nhin] Đã chụp {views.Length} góc vào {outDir}");
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "tay_cam_that")
        {
            // Mạch thật (joystick → Mega → ESP32) đang phát vào cổng 4210: nghe 10 s, ghi trạng thái mỗi giây
            var src = FindAnyObjectByType<Khkt.Input.UdpControllerSource>();
            viewer.GoTo(1);
            Vector3 start = viewer.transform.position;
            float yaw0 = viewer.transform.eulerAngles.y;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSecondsRealtime(1f);
                var s = src.Latest;
                Log($"[that] {i + 1,2}s | {src.StatusText} | Joy2=({s.Joy2.x:+0.00;-0.00},{s.Joy2.y:+0.00;-0.00}) nút={s.Pressed(Khkt.Input.ControllerButtons.Joy2Press)} " +
                    $"| chế độ={Khkt.Input.PadInput.Mode} | trôi {Vector3.Distance(start, viewer.transform.position):0.00} m, quay {Mathf.DeltaAngle(yaw0, viewer.transform.eulerAngles.y):+0;-0}°");
            }
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "logo")
        {
            hand.enabled = false;  // không để bàn tay ảo che logo trong ảnh
            foreach (var r in hand.hand.GetComponentsInChildren<Renderer>()) r.enabled = false;
            viewer.GoNear(new Vector3(0.12f, 2.6f, 7.5f), new Vector3(1f, 0.05f, 0f), 6.5f);
            yield return Frames(15);
            Shot("logo_xa");
            viewer.GoNear(new Vector3(0.12f, 3.1f, 7.5f), new Vector3(1f, -0.1f, 0.25f), 3.2f);
            yield return Frames(10);
            Shot("logo_gan");
            viewer.GoNear(new Vector3(19.9f, 2.6f, 3.7f), new Vector3(-1f, 0.05f, 0.35f), 7.5f);
            yield return Frames(12);
            Shot("logo_tuong_dong");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "may")
        {
            hand.enabled = false;
            foreach (var r in hand.hand.GetComponentsInChildren<Renderer>()) r.enabled = false;
            hand.enabled = true;
            yield return MachineTest();
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "may2")
        {
            hand.enabled = false;
            foreach (var r in hand.hand.GetComponentsInChildren<Renderer>()) r.enabled = false;
            hand.enabled = true;
            yield return MachineTest2();
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "dau")
        {
            yield return HeadTest();
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "hieu_ung")
        {
            hand.enabled = false;  // ảnh cận cảnh: không để bàn tay ảo che
            foreach (var r in hand.hand.GetComponentsInChildren<Renderer>()) r.enabled = false;
            yield return FlowTest(true);
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "dong_chay")
        {
            yield return FlowTest(false);
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "che_tao")
        {
            yield return FabTest();
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "lap_rap")
        {
            yield return FabAssemblyTest();
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "khu")
        {
            yield return ZoneTest();
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }
        if (Environment.GetEnvironmentVariable("KHKT_TEST") == "tay_cam")
        {
            yield return PadTestXyb();
            Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            yield break;
        }

        // 0. Màn hình giới thiệu và các khu
        var intro = FindAnyObjectByType<IntroScreen>();
        intro.Open();
        yield return Frames(5);
        Shot("w00_gioi_thieu");
        intro.Close();
        foreach (var (st, name) in new[] { (8, "w00b_phong_robot"), (9, "w01_toan_canh"), (6, "w02_phong_hoa_chat"), (3, "w03_khu_thiet_bi"), (4, "w04_ban_dun_nong"),
                                           (5, "w05_tu_hut"), (11, "w06_khu_an_toan"), (7, "w07_phong_may_phan_tich") })
        {
            viewer.GoTo(st);
            yield return Frames(4);
            if (st == 9) viewer.LookAt(new Vector3(6f, 0.6f, 6f));
            if (st == 6) viewer.LookAt(new Vector3(25.5f, 1.2f, 9f));
            if (st == 7) viewer.LookAt(new Vector3(6.75f, 1.0f, -8.4f));
            if (st == 8) viewer.LookAt(new Vector3(18.5f, 1.0f, -6.5f));
            yield return Frames(8);
            Shot(name);
        }
        Log($"Robot lúc đầu ở {LabRobot.Instance.transform.position}, NavGrid sẵn sàng={NavGrid.Ready}");

        // 1. Chuẩn độ HCl bằng NaOH (bài mặc định do người dùng chọn)
        proc.Begin(LabProcedure.Config.Default);
        viewer.GoTo(1);
        yield return Frames(5);
        yield return Titrate(proc);

        // 2. Thí nghiệm định tính ở bàn 2: HCl + AgNO3, CuSO4 + NaOH, FeCl3 + KSCN
        proc.Stop();
        viewer.GoTo(2);
        yield return Frames(5);
        var tubes = FindObjectsByType<LabContainer>().Where(c => c.label.StartsWith("Ống nghiệm ")).OrderBy(c => c.label).ToArray();
        Log($"Ống nghiệm ở bàn 2: {tubes.Length}");
        yield return PourBottle(98, tubes[0], 3f);
        yield return PourBottle(230, tubes[0], 1.5f);
        yield return new WaitForSeconds(0.6f);
        Log($"Ống 1: {tubes[0].Total.Describe(4)} · đục {tubes[0].Look.Turbidity:0.00} · chú thích: {ReactionAnnotator.Instance.Last?.Title}");
        yield return PourBottle(253, tubes[1], 3f);
        yield return PourBottle(124, tubes[1], 4f);
        yield return new WaitForSeconds(0.6f);
        Log($"Ống 2: {tubes[1].Total.Describe(4)} · màu {tubes[1].Look.Liquid.r:0.00} {tubes[1].Look.Liquid.g:0.00} {tubes[1].Look.Liquid.b:0.00}");
        yield return PourBottle(159, tubes[2], 3f);
        yield return PourBottle(351, tubes[2], 1f);
        yield return new WaitForSeconds(0.6f);
        Log($"Ống 3: {tubes[2].Total.Describe(4)}");
        viewer.LookAt(tubes[1].transform.position + Vector3.up * 0.05f);
        yield return Frames(10);
        Shot("w10_dinh_tinh");
        Log("Lịch sử hiện tượng: " + string.Join(" | ", ReactionAnnotator.Instance.History));

        // 3. Tra cứu: KMnO4
        var search = ChemSearchUI.Instance;
        search.Open();
        var input = search.GetComponentsInChildren<UnityEngine.UI.InputField>(true).FirstOrDefault();
        var field = FindObjectsByType<UnityEngine.UI.InputField>(FindObjectsInactive.Include).First(f => f.name == "Ô tìm kiếm");
        field.text = "kali pemanganat";
        yield return Frames(6);
        Shot("w11_tra_cuu");
        var kmno4 = ChemicalCatalog.Instance.Search("KMnO4", 1)[0];
        Log($"Tìm KMnO4: {kmno4.ten} ở {ChemicalCatalog.Instance.LocationText(kmno4)}");
        search.GoTo(kmno4);
        yield return Frames(15);
        Shot("w12_vi_tri_tren_ke");

        // 4. Robot mang CuSO4 ra bàn 2
        viewer.GoTo(2);
        yield return Frames(3);
        var robot = LabRobot.Instance;
        robot.BringChemical(253);
        float tr = Time.time;
        Time.timeScale = 3f;
        yield return new WaitUntil(() => !robot.Busy || Time.time - tr > 60f);
        Time.timeScale = 1f;
        var cu = search.FindBottle(253);
        Log($"Robot mang CuSO4: còn bận={robot.Busy}, lọ cách mắt {Vector3.Distance(cu.transform.position, cam.transform.position):0.00} m");
        viewer.LookAt(cu.transform.position);
        yield return Frames(10);
        Shot("w13_robot_mang_hoa_chat");

        // 5. Sự cố: Cu + HNO3 đặc ngoài tủ hút (khí NO2)
        var beaker = FindObjectsByType<LabContainer>().First(c => c.label == "Cốc có mỏ 100 ml" && Vector3.Distance(c.transform.position, cam.transform.position) < 2f);
        var cat = ChemicalCatalog.Instance;
        beaker.Add(cat.Make(cat.Get(104), 10f, 14f));
        beaker.Add(cat.Make(cat.Get(20), 0.5f));
        Time.timeScale = 2f;
        yield return new WaitForSeconds(4f);
        Time.timeScale = 1f;
        Log($"Cu + HNO3 đặc: {beaker.Total.Describe(4)} · khí {beaker.GasId} · robot: {robot.StatusText}");
        viewer.LookAt(beaker.transform.position + Vector3.up * 0.08f);
        yield return Frames(10);
        Shot("w14_su_co_khi_doc");

        // 6. Robot làm theo kế hoạch nhiều bước (như kế hoạch máy chủ AI trả về sau khi kiểm tra)
        var bot = LabRobot.Instance;
        viewer.GoTo(2);
        yield return Frames(3);
        RobotWorld.Capture(viewer.transform, hand, proc);
        var tube4 = tubes[3].GetComponent<Grabbable>();
        string tubeId = RobotWorld.Ids.FirstOrDefault(kv => kv.Value == (Component)tube4).Key;
        Log($"Ảnh chụp cảnh: {RobotWorld.Ids.Count} vật, ống 4 = {tubeId}");
        bot.ExecutePlan(new System.Collections.Generic.List<RobotStep>
        {
            new RobotStep { ky_nang = "noi", loi = "Mình minh họa nhận biết ion Cu²⁺ bằng NaOH." },
            new RobotStep { ky_nang = "rot", doi_tuong = "hoa_chat:253", dich = tubeId, so_luong = 2 },
            new RobotStep { ky_nang = "nho_giot", doi_tuong = "hoa_chat:124", dich = tubeId, so_luong = 8 },
            new RobotStep { ky_nang = "chi_vao", doi_tuong = tubeId },
        });
        float tp = Time.time;
        Time.timeScale = 3f;
        while (bot.Busy && Time.time - tp < 90f)
        {
            if (Time.time - tp > 6f && Time.time - tp < 6.2f) { viewer.LookAt(bot.transform.position + Vector3.up * 1.1f); Shot("w15_robot_dang_rot"); }
            yield return null;
        }
        Time.timeScale = 1f;
        Log($"Robot làm kế hoạch: bận={bot.Busy}, trạng thái={bot.StatusText}, ống 4: {tubes[3].Total.Describe(4)} · đục {tubes[3].Look.Turbidity:0.00}");
        viewer.LookAt(tubes[3].transform.position + Vector3.up * 0.05f);
        yield return Frames(10);
        Shot("w16_robot_xong_ke_hoach");

        // 7. Phòng máy phân tích: chạy máy trên ống mẫu có sẵn
        viewer.GoTo(7);
        yield return Frames(3);
        var analyzers = FindObjectsByType<AnalyzerInstrument>();
        Log($"Máy phân tích: {analyzers.Length}");
        foreach (var (kind, sample) in new[] { (AnalyzerInstrument.Kind.Aas, "Mẫu nước thải mạ điện"), (AnalyzerInstrument.Kind.AutoTitrator, "Mẫu giấm ăn"),
                                               (AnalyzerInstrument.Kind.GcMs, "Mẫu rượu"), (AnalyzerInstrument.Kind.UvVis, "Mẫu nước thải xi mạ crom"),
                                               (AnalyzerInstrument.Kind.Ftir, "Mẫu dầu chuối"), (AnalyzerInstrument.Kind.Hplc, "Mẫu nước cam") })
        {
            var a = analyzers.First(x => x.kind == kind);
            var s = FindObjectsByType<LabContainer>().First(c => c.label == sample);
            Log($"{sample}: {s.Total.Describe(5)} · pH {s.BulkPh:0.00}");
            hand.Pick(s.GetComponent<Grabbable>());
            hand.PlaceInSocket(a.slot);
            yield return Frames(2);
            a.RunQuick();  // chạy nhanh như robot (màn hình vận hành có bài riêng: may)
            Time.timeScale = 3f;
            yield return new WaitUntil(() => !a.Running);
            Time.timeScale = 1f;
            Log($"{AnalyzerInstrument.KindName(kind)} · {sample}: {a.LastReport.Replace("\n", " | ")}");
        }
        var aas = analyzers.First(x => x.kind == AnalyzerInstrument.Kind.Aas);
        viewer.GoNear(aas.transform.position + new Vector3(0.75f, 0.45f, 0), Vector3.forward, 0.9f);
        yield return Frames(10);
        Shot("w17_may_aas");
        var tit = analyzers.First(x => x.kind == AnalyzerInstrument.Kind.AutoTitrator);
        viewer.GoNear(tit.transform.position + new Vector3(0.55f, 0.45f, 0), Vector3.forward, 0.9f);
        yield return Frames(10);
        Shot("w18_chuan_do_tu_dong");
        viewer.GoTo(8);
        viewer.LookAt(new Vector3(16.6f, 1.2f, -7.6f));
        yield return Frames(10);
        Shot("w19_phong_robot_cobot");
        var rack = GameObject.Find("Tủ máy chủ AI");
        if (rack != null)
        {
            viewer.GoNear(rack.transform.position + Vector3.up * 1.2f, Vector3.left, 1.8f);
            yield return Frames(10);
            Shot("w20_tu_may_chu");
        }
        // robot cận cảnh, đang đi theo người dùng
        Time.timeScale = 3f;
        yield return new WaitForSeconds(9f);
        Time.timeScale = 1f;
        viewer.GoNear(bot.transform.position + Vector3.up * 1.2f, bot.transform.forward, 1.7f);
        yield return Frames(10);
        Shot("w21_robot_can_canh");
        Log($"Robot ở {bot.transform.position}, trạng thái {bot.StatusText}");

        // 8. Kỹ năng mới: robot tự chuẩn độ trọn vẹn, thí nghiệm mẫu đo pH, glucozơ + Cu(OH)2 có đun nóng
        bot.Spoke += s => Log("ROBOT NÓI: " + s);
        foreach (var (name, start, station) in new (string, Action, int)[]
                 {
                     ("chuan_do", () => bot.DemoTitration(), 1),
                     ("do_ph_dung_dich", () => bot.RunDemo("do_ph_dung_dich"), 2),
                     ("glucozo_cu_oh2", () => bot.RunDemo("glucozo_cu_oh2"), 2),
                 })
        {
            viewer.GoTo(station);
            yield return Frames(3);
            start();
            float t1 = Time.time, shotAt = 25f;
            Time.timeScale = 4f;
            while (bot.Busy && Time.time - t1 < 400f)
            {
                if (Time.time - t1 > shotAt) { shotAt = 1e9f; Time.timeScale = 1f; viewer.LookAt(bot.transform.position + Vector3.up * 1.0f); yield return Frames(4); Shot($"w3x_{name}_dang_lam"); Time.timeScale = 4f; }
                yield return null;
            }
            Time.timeScale = 1f;
            Log($"[{name}] xong sau {Time.time - t1:0} s (game), bận={bot.Busy}, trạng thái={bot.StatusText}");
            if (name == "chuan_do" && proc.LastResult.HasValue)
            {
                var rr = proc.LastResult.Value;
                Log($"[chuan_do] V={rr.volumeRead:0.00} C={rr.concentrationRead:0.0000} thật={rr.concentrationTrue:0.0000} sai số={rr.errorPercent:0.00}% giọt vượt={rr.overshootDrops:0.0}");
            }
            viewer.LookAt(bot.transform.position + Vector3.up * 0.9f);
            yield return Frames(8);
            Shot($"w4x_{name}_xong");
        }

        Log($"Xong sau {Time.realtimeSinceStartup - t0:0} s");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#endif
    }

    // ---------- Tay cầm Wi-Fi ESP32-CAM: gửi gói UDP thật vào game như firmware ----------

    System.Net.Sockets.UdpClient padSock;
    System.Net.IPEndPoint padGame;
    byte padSeq;
    readonly System.Collections.Generic.List<string> padHeard = new System.Collections.Generic.List<string>();

    void PadSend(Vector2 j1, Vector2 j2, byte buttons)
    {
        var g = new byte[29];
        g[0] = 0xA5; g[1] = 0x5A; g[2] = padSeq++;
        uint t = (uint)(Time.realtimeSinceStartup * 1000);
        g[3] = (byte)t; g[4] = (byte)(t >> 8); g[5] = (byte)(t >> 16); g[6] = (byte)(t >> 24);
        void U16(int i, int v) { g[i] = (byte)v; g[i + 1] = (byte)(v >> 8); }
        int Raw(float x) => Mathf.RoundToInt(x >= 0 ? 512 + x * 511 : 512 + x * 512);
        U16(11, 16384);  // az = 1 g: tay cầm nằm ngang
        U16(19, Raw(j1.x)); U16(21, Raw(j1.y)); U16(23, Raw(j2.x)); U16(25, Raw(j2.y));
        g[27] = buttons;
        byte crc = 0;
        for (int i = 2; i < 28; i++)
        {
            crc ^= g[i];
            for (int b = 0; b < 8; b++) crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
        }
        g[28] = crc;
        padSock.Send(g, g.Length, padGame);
        while (padSock.Available > 0)
        {
            var from = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
            foreach (var line in System.Text.Encoding.ASCII.GetString(padSock.Receive(ref from)).Split('\n'))
                if (line.Length > 0) padHeard.Add(line);
        }
    }

    IEnumerator PadHold(float seconds, Vector2 j1, Vector2 j2, byte buttons)
    {
        float end = Time.realtimeSinceStartup + seconds, next = 0;
        while (Time.realtimeSinceStartup < end)
        {
            if (Time.realtimeSinceStartup >= next)
            {
                PadSend(j1, j2, buttons);
                next = Time.realtimeSinceStartup + 0.01f;
            }
            yield return null;
        }
    }

    // ---------- Mạch joystick → Mega → ESP32: dòng chữ "X,Y,B" qua UDP, đúng như ESP32 thật gửi ----------

    void SendXyb(int x, int y, int b)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes($"{x},{y},{b}");
        padSock.Send(bytes, bytes.Length, padGame);
    }

    IEnumerator HoldXyb(float seconds, int x, int y, int b)
    {
        float end = Time.realtimeSinceStartup + seconds, next = 0;
        while (Time.realtimeSinceStartup < end)
        {
            if (Time.realtimeSinceStartup >= next)
            {
                SendXyb(x, y, b);
                next = Time.realtimeSinceStartup + 0.012f;  // ~80 gói/s như mạch thật
            }
            yield return null;
        }
    }

    IEnumerator PadTestXyb()
    {
        const int CX = 409, CY = 387;  // tâm lệch như mạch thật đo được
        var src = FindAnyObjectByType<Khkt.Input.UdpControllerSource>();
        src.enabled = false;
        src.listenPort = 4299;  // tránh ESP32 thật đang phát vào cổng 4210
        src.enabled = true;
        padSock = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        padGame = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 4299);
        string Hints() => string.Join(" | ", hand.Hints.Select(h => h.key + ": " + h.text));
        float Yaw() => viewer.transform.eulerAngles.y;
        viewer.GoTo(1);
        yield return Frames(3);

        yield return HoldXyb(0.6f, CX, CY, 0);
        Log($"[xyb] nối={Khkt.Input.PadInput.ControllerConnected} dạng chữ={src.TextFormat} 1 cần={Khkt.Input.PadInput.SingleStick} chế độ={Khkt.Input.PadInput.Mode} | {src.StatusText}");
        Log($"[xyb] gợi ý chế độ ĐI: {Hints()}");

        Vector3 p0 = viewer.transform.position; Vector3 f0 = viewer.transform.forward; float y0 = Yaw();
        yield return HoldXyb(1.0f, CX, 0, 0);
        Log($"[xyb] LÙI 1 s: {Vector3.Dot(viewer.transform.position - p0, f0):+0.00;-0.00} m theo hướng nhìn, quay {Mathf.DeltaAngle(y0, Yaw()):0}°");
        p0 = viewer.transform.position;
        yield return HoldXyb(0.6f, CX, 1023, 0);
        Log($"[xyb] TIẾN 0,6 s: {Vector3.Dot(viewer.transform.position - p0, f0):+0.00;-0.00} m theo hướng nhìn");
        y0 = Yaw();
        yield return HoldXyb(0.5f, 1023, CY, 0);
        Log($"[xyb] QUAY PHẢI 0,5 s: {Mathf.DeltaAngle(y0, Yaw()):+0;-0}°");
        y0 = Yaw();
        yield return HoldXyb(0.5f, 0, CY, 0);
        Log($"[xyb] QUAY TRÁI 0,5 s: {Mathf.DeltaAngle(y0, Yaw()):+0;-0}°");
        y0 = Yaw(); p0 = viewer.transform.position; f0 = viewer.transform.forward;
        yield return HoldXyb(0.5f, 1023, 1023, 0);
        Log($"[xyb] CHÉO (lên + phải) 0,5 s: đi {Vector3.Distance(p0, viewer.transform.position):0.00} m, quay {Mathf.DeltaAngle(y0, Yaw()):+0;-0}°");
        yield return HoldXyb(1.4f, CX, 1023, 0);
        Log($"[xyb] ĐẨY LÊN HẾT CỠ 1,4 s: chạy nhanh={Khkt.Input.PadInput.Shift}");
        yield return HoldXyb(0.3f, CX, CY, 0);

        viewer.GoTo(1);
        yield return HoldXyb(0.3f, CX, CY, 0);
        y0 = Yaw(); p0 = viewer.transform.position; Vector3 r0 = viewer.transform.right; float pitch0 = viewer.Pitch;
        yield return HoldXyb(0.8f, 1023, CY, 1);
        yield return HoldXyb(0.2f, CX, CY, 0);
        Log($"[xyb] GIỮ NÚT + PHẢI 0,8 s: bước ngang {Vector3.Dot(viewer.transform.position - p0, r0):+0.00;-0.00} m, quay {Mathf.DeltaAngle(y0, Yaw()):+0;-0}°, chế độ={Khkt.Input.PadInput.Mode}");
        yield return HoldXyb(0.6f, CX, 1023, 1);
        yield return HoldXyb(0.2f, CX, CY, 0);
        Log($"[xyb] GIỮ NÚT + LÊN 0,6 s: ngẩng {viewer.Pitch - pitch0:+0;-0}° (âm = nhìn lên)");
        bool seat0 = viewer.seated;
        yield return HoldXyb(0.1f, CX, CY, 1); yield return HoldXyb(0.12f, CX, CY, 0);
        yield return HoldXyb(0.1f, CX, CY, 1); yield return HoldXyb(0.6f, CX, CY, 0);
        Log($"[xyb] NHẤN 2 LẦN: ngồi {seat0} → {viewer.seated}, chế độ={Khkt.Input.PadInput.Mode}");

        viewer.GoTo(1);
        yield return HoldXyb(0.2f, CX, CY, 0);
        Grabbable target = null;
        foreach (var g in FindObjectsByType<Grabbable>().OrderBy(g => Vector3.Distance(g.transform.position, cam.transform.position)).Take(10))
        {
            var col = g.GetComponentInChildren<Collider>();
            if (col == null) continue;
            hand.OverrideAimPoint = col.bounds.center;
            yield return HoldXyb(0.05f, CX, CY, 0);
            if (hand.Current == HandController.Act.Pick && hand.HoverRoot == g.transform) { target = g; break; }
        }
        yield return HoldXyb(0.12f, CX, CY, 1);
        yield return HoldXyb(0.6f, CX, CY, 0);
        Log($"[xyb] NHẤN 1 LẦN: chế độ={Khkt.Input.PadInput.Mode}, thông báo='{LabMessages.Message}', gợi ý: {Hints()}");
        yield return HoldXyb(0.12f, CX, CY, 1);
        yield return HoldXyb(0.4f, CX, CY, 0);
        Log($"[xyb] TAY, nhấn khi trỏ vào {(target != null ? target.name : "?")}: đang cầm={(hand.Held != null ? hand.Held.name : "không")}");
        Shot("w60_joystick_mega_che_do_tay");
        hand.OverrideAimPoint = null;
        float pitch1 = viewer.Pitch; y0 = Yaw();
        yield return HoldXyb(0.5f, 1023, 1023, 0);
        Log($"[xyb] TAY, cần lên-phải 0,5 s: ngắm quay {Mathf.DeltaAngle(y0, Yaw()):+0;-0}°, ngẩng {viewer.Pitch - pitch1:+0;-0}°");
        hand.OverrideAimPoint = cam.transform.position + Vector3.up * 30f;
        yield return HoldXyb(0.1f, CX, CY, 0);
        yield return HoldXyb(0.12f, CX, CY, 1);
        yield return HoldXyb(0.4f, CX, CY, 0);
        Log($"[xyb] TAY, nhấn vào chỗ trống: chế độ={Khkt.Input.PadInput.Mode}, vẫn cầm={(hand.Held != null ? hand.Held.name : "không")}");
        hand.OverrideAimPoint = null;
        hand.ReturnHome();

        p0 = viewer.transform.position;
        yield return HoldXyb(1.3f, CX, CY, 1);
        yield return HoldXyb(0.3f, CX, CY, 0);
        Log($"[xyb] GIỮ NÚT 1,3 s (không đẩy cần): dịch chuyển {Vector3.Distance(p0, viewer.transform.position):0.0} m, chế độ={Khkt.Input.PadInput.Mode}");
        Shot("w61_joystick_mega_che_do_di");
        var ui = FindAnyObjectByType<ModernUI>();
        ui.ToggleHelp();
        yield return HoldXyb(0.2f, CX, CY, 0);
        Shot("w62_joystick_mega_tro_giup");
        ui.ToggleHelp();
        SendXyb(0, 0, 0);
        var noMega = System.Text.Encoding.ASCII.GetBytes("NO_MEGA_DATA");
        padSock.Send(noMega, noMega.Length, padGame);
        yield return new WaitForSecondsRealtime(1.0f);
        yield return null;
        Log($"[xyb] ESP32 báo NO_MEGA_DATA: nối={Khkt.Input.PadInput.ControllerConnected} | {src.StatusText} | thông báo='{LabMessages.Message}'");
        padSock.Close();
    }

    // Tay cầm 1 cần (ESP32-CAM + 1 mạch joystick): cần ghi vào j2, nút = bit 4, cờ bit 6 (1 cần) + bit 7 (không IMU)
    IEnumerator PadTest1Can()
    {
        var serial = FindAnyObjectByType<Khkt.Input.SerialControllerSource>();
        var src = FindAnyObjectByType<Khkt.Input.UdpControllerSource>();
        Log($"[1can] nguồn COM: {(serial != null ? serial.GetType().Assembly.GetName().Name : "KHÔNG")}, trạng thái: {serial?.StatusText}");
        padSock = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        padGame = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, src.listenPort);
        const byte F = (1 << 6) | (1 << 7), B = (1 << 4) | F;
        var Z = Vector2.zero;
        string Hints() => string.Join(" | ", hand.Hints.Select(h => h.key + ": " + h.text));
        viewer.GoTo(1);
        yield return Frames(3);

        yield return PadHold(0.6f, Z, Z, F);
        Log($"[1can] nối: {Khkt.Input.PadInput.ControllerConnected}, 1 cần={Khkt.Input.PadInput.SingleStick}, chế độ={Khkt.Input.PadInput.Mode}, thông báo='{LabMessages.Message}'");
        Vector3 p0 = viewer.transform.position;
        yield return PadHold(1.0f, Z, new Vector2(0, -1), F);
        Log($"[1can] ĐI, kéo cần xuống 1 s: {Vector3.Dot(viewer.transform.position - p0, viewer.transform.forward):+0.00;-0.00} m theo hướng nhìn");
        float y0 = viewer.transform.eulerAngles.y, pitch0 = viewer.Pitch;
        yield return PadHold(0.5f, Z, new Vector2(1, 0), F);
        Log($"[1can] ĐI, đẩy cần sang phải 0,5 s: xoay {Mathf.DeltaAngle(y0, viewer.transform.eulerAngles.y):0}°, ngẩng đổi {viewer.Pitch - pitch0:0}°");

        viewer.GoTo(1);
        yield return PadHold(0.2f, Z, Z, F);
        Grabbable target = null;
        foreach (var g in FindObjectsByType<Grabbable>().OrderBy(g => Vector3.Distance(g.transform.position, cam.transform.position)).Take(10))
        {
            var col = g.GetComponentInChildren<Collider>();
            if (col == null) continue;
            hand.OverrideAimPoint = col.bounds.center;
            yield return PadHold(0.05f, Z, Z, F);
            if (hand.Current == HandController.Act.Pick && hand.HoverRoot == g.transform) { target = g; break; }
        }
        Log($"[1can] trỏ vào {(target != null ? target.name : "KHÔNG THẤY")} ở chế độ ĐI, gợi ý: {Hints()}");
        Shot("w52_mot_can_che_do_di");
        yield return PadHold(0.15f, Z, Z, B);
        yield return PadHold(0.3f, Z, Z, F);
        Log($"[1can] nhấn cần: chế độ={Khkt.Input.PadInput.Mode}, đang cầm={(hand.Held != null ? hand.Held.name : "không")}, gợi ý: {Hints()}");
        yield return PadHold(0.15f, Z, Z, B);
        yield return PadHold(0.3f, Z, Z, F);
        Log($"[1can] nhấn cần lần 2 (TAY): đang cầm={(hand.Held != null ? hand.Held.name : "không")}, gợi ý: {Hints()}");
        Shot("w53_mot_can_che_do_tay");
        float pitch1 = viewer.Pitch;
        hand.OverrideAimPoint = cam.transform.position + Vector3.up * 30f;  // trỏ lên trần xa: không trúng gì
        yield return PadHold(0.5f, new Vector2(0, 1), new Vector2(0, 1), F);
        Log($"[1can] TAY, đẩy cần lên 0,5 s: ngẩng đổi {viewer.Pitch - pitch1:0}°, gợi ý: {Hints()}");
        yield return PadHold(0.15f, Z, Z, B);
        yield return PadHold(0.3f, Z, Z, F);
        Log($"[1can] nhấn vào chỗ trống: chế độ={Khkt.Input.PadInput.Mode}, vẫn cầm={(hand.Held != null ? hand.Held.name : "không")}");
        hand.OverrideAimPoint = null;
        hand.ReturnHome();

        Vector3 before = viewer.transform.position;
        yield return PadHold(0.9f, Z, Z, B);
        yield return PadHold(0.3f, Z, Z, F);
        Log($"[1can] ĐI, giữ cần 0,9 s: dịch chuyển {Vector3.Distance(before, viewer.transform.position):0.0} m, chế độ={Khkt.Input.PadInput.Mode}");
        var ui = FindAnyObjectByType<ModernUI>();
        ui.ToggleHelp();
        yield return PadHold(0.2f, Z, Z, F);
        Shot("w54_tro_giup_mot_can");
        ui.ToggleHelp();
        padSock.Close();
        yield return new WaitForSecondsRealtime(1.2f);
        yield return null;
        Log($"[1can] ngừng gửi: nối={Khkt.Input.PadInput.ControllerConnected}, thông báo='{LabMessages.Message}', COM: {serial?.StatusText}");
    }

    IEnumerator PadTest()
    {
        var src = FindAnyObjectByType<Khkt.Input.UdpControllerSource>();
        Log($"[tay_cam] nguồn UDP: {(src != null ? "có" : "KHÔNG")}, cổng {src?.listenPort}, lỗi cổng '{src?.PortError}'");
        if (src == null) yield break;
        padSock = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        padGame = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, src.listenPort);
        var Z = Vector2.zero;
        viewer.GoTo(1);
        yield return Frames(3);

        yield return PadHold(0.6f, Z, Z, 0);
        Log($"[tay_cam] nối: IsConnected={src.IsConnected} PadInput={Khkt.Input.PadInput.ControllerConnected} tay cầm={src.ControllerName} thông báo='{LabMessages.Message}'");

        Vector3 p0 = viewer.transform.position;
        yield return PadHold(1.0f, Z, new Vector2(0, -1), 0);  // cần đi kéo xuống: lùi (phía trước là bàn)
        Vector3 p1 = viewer.transform.position;
        Log($"[tay_cam] lùi 1 s: {Vector3.Distance(p0, p1):0.00} m, theo hướng nhìn {Vector3.Dot(p1 - p0, viewer.transform.forward):+0.00;-0.00} m");
        yield return PadHold(1.0f, Z, new Vector2(1, 0), 1 << 4);  // sang phải, giữ nhấn cần đi (chạy)
        Log($"[tay_cam] chạy sang phải 1 s: {Vector3.Distance(p1, viewer.transform.position):0.00} m");
        float y0 = viewer.transform.eulerAngles.y;
        yield return PadHold(0.5f, new Vector2(1, 0), Z, 0);
        Log($"[tay_cam] cần nhìn sang phải 0,5 s: xoay {Mathf.DeltaAngle(y0, viewer.transform.eulerAngles.y):0}°");

        viewer.GoTo(1);
        yield return PadHold(0.2f, Z, Z, 0);
        Grabbable target = null;
        foreach (var g in FindObjectsByType<Grabbable>().OrderBy(g => Vector3.Distance(g.transform.position, cam.transform.position)).Take(10))
        {
            var col = g.GetComponentInChildren<Collider>();
            if (col == null) continue;
            hand.OverrideAimPoint = col.bounds.center;
            yield return PadHold(0.05f, Z, Z, 0);
            if (hand.Current == HandController.Act.Pick && hand.HoverRoot == g.transform) { target = g; break; }
        }
        Log($"[tay_cam] trỏ vào {(target != null ? target.name : "KHÔNG THẤY")}, gợi ý: {string.Join(" | ", hand.Hints.Select(h => h.key + ": " + h.text))}");
        if (target == null) yield break;
        padHeard.Clear();
        yield return PadHold(0.15f, Z, Z, 1);  // bóp cò
        yield return PadHold(0.4f, Z, Z, 0);
        Log($"[tay_cam] bóp cò: đang cầm={(hand.Held != null ? hand.Held.name : "không")}, gợi ý: {string.Join(" | ", hand.Hints.Select(h => h.key + ": " + h.text))}");
        Log($"[tay_cam] lệnh game gửi tay cầm: {string.Join(", ", padHeard.Distinct())}");
        viewer.LookAt(target.transform.position);
        yield return PadHold(0.1f, Z, Z, 0);
        Shot("w50_tay_cam_cam_dung_cu");
        yield return PadHold(0.15f, Z, Z, 1 << 1);  // nhấn nhanh Căn lại
        yield return PadHold(0.3f, Z, Z, 0);
        Log($"[tay_cam] nhấn Căn lại: đang cầm={(hand.Held != null ? hand.Held.name : "không")}, cách chỗ cũ {Vector3.Distance(target.transform.position, target.HomePosition):0.000} m");
        hand.OverrideAimPoint = null;

        Vector3 before = viewer.transform.position;
        yield return PadHold(0.9f, Z, Z, 1 << 1);  // giữ Căn lại: sang khu kế tiếp
        yield return PadHold(0.2f, Z, Z, 0);
        Log($"[tay_cam] giữ Căn lại: dịch chuyển {Vector3.Distance(before, viewer.transform.position):0.0} m");
        yield return PadHold(0.9f, Z, Z, 1 << 2);  // giữ Hỏi AI: bảng trợ giúp
        yield return PadHold(0.3f, Z, Z, 0);
        var help = GameObject.Find("Trợ giúp");
        Log($"[tay_cam] giữ Hỏi AI: bảng trợ giúp {(help != null ? "MỞ" : "đóng")}, {src.PacketsPerSecond} gói/s");
        Shot("w51_tro_giup_tay_cam");
        yield return PadHold(0.9f, Z, Z, 1 << 2);
        yield return PadHold(0.2f, Z, Z, 0);

        padSock.Close();
        yield return new WaitForSecondsRealtime(1.2f);
        yield return null;
        Log($"[tay_cam] ngừng gửi 1,2 s: IsConnected={src.IsConnected}, thông báo='{LabMessages.Message}'");
    }

    IEnumerator PourBottle(int stt, LabContainer target, float ml)
    {
        var bottle = Khkt.Rendering.ZoneRoot.FindAll<ChemicalBottle>().Where(b => b.stt == stt).OrderBy(b => Vector3.Distance(b.transform.position, cam.transform.position)).First();
        var g = bottle.GetComponent<Grabbable>();
        hand.Pick(g);
        hand.OverrideAimPoint = target.transform.position + Vector3.up * 0.06f;
        yield return Frames(5);
        float start = target.VolumeMl;
        hand.OverrideHold = true;
        float t = Time.time;
        yield return new WaitUntil(() => target.VolumeMl - start >= ml || Time.time - t > 8f);
        hand.OverrideHold = false;
        yield return Frames(3);
        hand.OverrideAimPoint = null;
        hand.ReturnHome();
        Log($"Rót {bottle.DisplayName} vào {target.label}: {target.VolumeMl - start:0.0} ml ({hand.Current})");
    }

    IEnumerator Titrate(LabProcedure proc)
    {
        var buret = proc.buret;
        var buretGrab = buret.GetComponent<Grabbable>();
        var bottle = proc.naohStock.GetComponent<Grabbable>();
        var waste = proc.wasteBeaker.GetComponent<Grabbable>();
        var pip = proc.pipette;
        var flaskGrab = proc.flask.GetComponent<Grabbable>();
        var dropper = proc.indicatorDropper;
        Log($"HCl thật {proc.TrueAcidConcentration:0.0000} M");

        viewer.LookAt(buretGrab.transform.position + Vector3.up * 0.05f);
        hand.OverrideAimPoint = buretGrab.transform.TransformPoint(0, 0.4f, 0);
        yield return Frames(20);
        Shot("v01_tro_vao_buret");
        hand.Pick(buretGrab);
        hand.PlaceInSocket(proc.clampSocket);
        yield return Frames(3);
        hand.Pick(bottle);
        hand.OverrideAimPoint = buret.container.mouth.position + Vector3.down * 0.03f;
        viewer.LookAt(buret.container.mouth.position);
        yield return Frames(10);
        hand.OverrideHold = true;
        Time.timeScale = 3f;
        yield return new WaitUntil(() => buret.ReadingMl < -1.0f);
        hand.OverrideHold = false;
        Time.timeScale = 1f;
        yield return Frames(5);
        hand.ReturnHome();
        hand.Pick(waste);
        hand.PlaceInSocket(proc.underSocket);
        buret.AdjustOpening(0.55f);
        yield return new WaitUntil(() => buret.ReadingMl >= 0.25f);
        buret.Close();
        yield return Frames(5);
        hand.Pick(waste);
        hand.ReturnHome();
        yield return LevelEye(proc.reading);
        proc.RecordReading();
        Log($"V đầu = {proc.InitialRead:0.00}, bước {proc.Current}");
        viewer.EyeOffset = 0;

        hand.Pick(pip.GetComponent<Grabbable>());
        hand.OverrideAimPoint = proc.hclStock.transform.position + Vector3.up * 0.05f;
        yield return Frames(10);
        hand.OverrideHold = true;
        yield return new WaitUntil(() => pip.OffsetFromMarkMm > 4f);
        hand.OverrideHold = false;
        yield return Frames(10);
        hand.OverrideShift = true;
        hand.OverrideHold = true;
        yield return new WaitUntil(() => pip.OffsetFromMarkMm <= 0.2f);
        hand.OverrideHold = false;
        hand.OverrideShift = false;
        hand.OverrideAimPoint = proc.flask.transform.position + Vector3.up * 0.05f;
        yield return Frames(5);
        hand.OverrideHold = true;
        Time.timeScale = 3f;
        yield return new WaitUntil(() => pip.IsEmpty);
        hand.OverrideHold = false;
        Time.timeScale = 1f;
        hand.ReturnHome();
        dropper.DropInto(proc.flask);
        dropper.DropInto(proc.flask);
        hand.Pick(flaskGrab);
        hand.PlaceInSocket(proc.underSocket);
        hand.OverrideAimPoint = proc.flask.transform.position + Vector3.up * 0.06f;
        viewer.LookAt(buret.tip.position + Vector3.down * 0.04f);
        yield return Frames(10);
        Log($"Bình nón: {proc.flask.Total.Describe()}, bước {proc.Current}");

        buret.AdjustOpening(0.9f);
        hand.OverrideHold = true;
        Time.timeScale = 4f;
        yield return new WaitUntil(() => proc.NaohIntoFlaskMl >= proc.EquivalenceMl - 1.5f);
        buret.Close();
        buret.AdjustOpening(0.38f);
        Time.timeScale = 6f;
        yield return new WaitUntil(() => proc.flask.PinkPersistS > 0.6f || proc.NaohIntoFlaskMl > proc.EquivalenceMl + 1f);
        buret.Close();
        Time.timeScale = 1f;
        yield return Frames(3);
        Shot("v08_hong_nhat");
        Time.timeScale = 8f;
        yield return new WaitUntil(() => proc.EndpointPersistS > 31f);
        Time.timeScale = 1f;
        hand.OverrideHold = false;
        hand.OverrideAimPoint = null;
        yield return LevelEye(proc.reading);
        proc.RecordReading();
        viewer.EyeOffset = 0;
        viewer.LookAt(proc.flask.transform.position + Vector3.up * 0.15f);
        yield return new WaitForSeconds(0.8f);
        Shot("v09_ket_qua");
        if (proc.LastResult.HasValue)
        {
            var r = proc.LastResult.Value;
            Log($"KẾT QUẢ V={r.volumeRead:0.00} C={r.concentrationRead:0.0000} thật={r.concentrationTrue:0.0000} sai số={r.errorPercent:0.00}% axit={r.acidActualMl:0.000} ml giọt vượt={r.overshootDrops:0.0}");
        }
        else Log("KHÔNG CÓ KẾT QUẢ: " + proc.Message);
    }


    // ---------- Lắp ráp tự do (giai đoạn C1 + C2) ----------

    static Khkt.Assembly.ApparatusPart Part(string name) => Khkt.Rendering.ZoneRoot.FindAll<Khkt.Assembly.ApparatusPart>().First(p => p.partName == name);
    static Khkt.Assembly.Port PortOf(Khkt.Assembly.ApparatusPart part, string label) => part.ports.First(x => x != null && x.label == label);

    IEnumerator Lap(Khkt.Assembly.ApparatusPart held, string mine, Khkt.Assembly.ApparatusPart target, string targetPort)
    {
        hand.Pick(held.Grab);
        yield return Frames(2);
        var m = PortOf(held, mine); var tp = PortOf(target, targetPort);
        bool ok = Khkt.Assembly.Port.Compatible(m, tp);
        hand.AttachHeld(m, tp);
        yield return Frames(2);
        Log($"[lap_rap] {held.partName} [{m.Describe()}] → {target.partName} [{tp.Describe()}]: hợp={ok}, đã nối={(m.Connected == tp)}, lệch {Vector3.Distance(m.transform.position, tp.transform.position) * 1000:0.0} mm, góc {Vector3.Angle(m.transform.forward, -tp.transform.forward):0.0}°");
    }

    IEnumerator AssemblyTest()
    {
        viewer.GoTo(12);
        yield return Frames(6);
        Shot("c01_ban_lap_rap");
        var standA = Part("Giá sắt A"); var clamp1 = Part("Kẹp càng cua 1"); var flask = Part("Bình cầu 250 ml (1)");
        var still = Part("Đầu nối chưng cất"); var thermo = Part("Nhiệt kế có khớp nối"); var cond = Part("Ống sinh hàn thẳng");
        var adapter = Part("Đầu nối hứng"); var receiver = Part("Bình nón hứng 250 ml");
        var hose1 = Part("Ống cao su 1 (đầu A)"); var hose1b = Part("Ống cao su 1 (đầu B)");
        var hose2 = Part("Ống cao su 2 (đầu A)"); var hose2b = Part("Ống cao su 2 (đầu B)");
        var supply = Part("Vòi cấp nước 1"); var drain = Part("Ống thoát nước 1");
        Log($"[lap_rap] Bộ phận trên bàn: {Khkt.Rendering.ZoneRoot.FindAll<Khkt.Assembly.ApparatusPart>().Count(p => Vector3.Distance(p.transform.position, LabStationPos()) < 3f)}");

        // nước trong bình cầu (để có khối lượng như khi chưng cất)
        var cat = ChemicalCatalog.Instance;
        var water = cat.Search("nước cất", 1)[0];
        flask.Container.Add(cat.Make(water, 100f));

        // 1) Bộ chưng cất trên giá A, kẹp vươn về phía trước (dọc theo đế)
        yield return Lap(clamp1, "Khớp nối kẹp", standA, "Cọc giá ở 40 cm (trước)");
        yield return Lap(flask, "Cổ bình (chỗ kẹp)", clamp1, "Má kẹp");
        yield return Lap(still, "Khớp dưới (24/29)", flask, "Cổ bình (khớp 24/29)");
        yield return Lap(thermo, "Khớp nhiệt kế (14/23)", still, "Khớp trên (14/23)");
        yield return Lap(cond, "Đầu vào hơi (24/29)", still, "Nhánh xiên (24/29)");
        yield return Lap(adapter, "Khớp vào (24/29)", cond, "Đầu ra (24/29)");
        yield return Lap(hose1, "Đầu ống A", cond, "Vòi nước vào (dưới)");
        yield return Lap(hose1b, "Đầu ống B", supply, "Vòi cấp nước 1 (đầu nối ống cao su)");
        yield return Lap(hose2, "Đầu ống A", cond, "Vòi nước ra (trên)");
        yield return Lap(hose2b, "Đầu ống B", drain, "Ống thoát nước 1 (đầu nối ống cao su)");
        var tip = PortOf(adapter, "Đầu nhỏ giọt").transform;
        hand.Pick(receiver.Grab); yield return Frames(1);
        hand.PlaceOnSurface(new Vector3(tip.position.x, LabStationTopY(), tip.position.z));
        yield return Frames(30);
        var bulb = thermo.transform.Find("Điểm đo nhiệt độ");
        var armJoint = still.transform.Find("Khớp: Nhánh xiên (24/29)");
        Log($"[lap_rap] Đầu nhỏ giọt chĩa xuống: góc với phương thẳng đứng {Vector3.Angle(tip.forward, Vector3.down):0}°, cao hơn miệng bình hứng {(tip.position.y - PortOf(receiver, "Miệng bình nón").transform.position.y) * 100:0.0} cm");
        Log($"[lap_rap] Bầu nhiệt kế ở độ cao {bulb.position.y:0.000} m, chỗ rẽ nhánh {still.transform.TransformPoint(new Vector3(0, 0.085f, 0)).y:0.000} m");
        Log($"[lap_rap] Bộ chưng cất: {flask.DescribeAssembly()} · cây {standA.Subtree().Count} bộ phận · đổ: {(Khkt.Assembly.AssemblyManager.LastTopple ?? "không")}");
        Log($"[lap_rap] Áo nước sinh hàn: lối hở {cond.Openings(1).Count} (nối vòi cấp + ống thoát: chỉ còn đầu vòi của bàn)");
        viewer.GoNear(flask.transform.position + Vector3.up * 0.1f, new Vector3(-0.6f, 0.2f, -1f), 1.0f);
        yield return Frames(8);
        Shot("c02_bo_chung_cat");
        viewer.GoNear(cond.transform.position, new Vector3(0.3f, 0.3f, -1f), 0.8f);
        yield return Frames(8);
        Shot("c03_bo_chung_cat_gan");

        // 2) Bộ điều chế khí: bình nón + nút 1 lỗ + ống chữ L + ống cao su
        var erlen = Part("Bình nón phản ứng 250 ml"); var stopper = Part("Nút cao su 1 lỗ (bình nón)"); var lt = Part("Ống dẫn chữ L");
        var hose3 = Part("Ống cao su 3 (đầu A)");
        yield return Lap(stopper, "Thân nút", erlen, "Miệng bình nón");
        yield return Lap(lt, "Đầu dưới (cắm qua nút)", stopper, "Lỗ nút");
        yield return Lap(hose3, "Đầu ống A", lt, "Đầu ngang");
        Log($"[lap_rap] Bộ điều chế khí: {erlen.DescribeAssembly()}");

        // 3) Hệ kín: ống nghiệm đậy nút đặc
        var tt = Part("Ống nghiệm to"); var solid = Part("Nút cao su đặc (ống nghiệm)");
        yield return Lap(solid, "Thân nút", tt, "Miệng ống nghiệm");
        Log($"[lap_rap] Ống nghiệm đậy nút đặc: {tt.DescribeAssembly()}");
        // khớp không hợp: nút ống nghiệm vào bình nón (cỡ 17 vs 30) phải bị từ chối
        var stopper2 = Part("Nút cao su 1 lỗ (ống nghiệm)");
        Log($"[lap_rap] Nút Ø17 vào miệng bình nón Ø30: hợp={Khkt.Assembly.Port.Compatible(PortOf(stopper2, "Thân nút"), PortOf(Part("Bình nón hứng 250 ml"), "Miệng bình nón"))} (đúng: False)");
        Log($"[lap_rap] Khớp 14/23 vào cổ bình 24/29: hợp={Khkt.Assembly.Port.Compatible(PortOf(thermo, "Khớp nhiệt kế (14/23)"), PortOf(Part("Bình cầu 250 ml (2)"), "Cổ bình (khớp 24/29)"))} (đúng: False)");

        // 4) Tháo: cầm đầu nối chưng cất → mang theo nhiệt kế, sinh hàn, đầu nối hứng; lắp lại
        hand.Pick(still.Grab);
        yield return Frames(2);
        Log($"[lap_rap] Cầm đầu nối chưng cất: còn nối bình cầu={PortOf(still, "Khớp dưới (24/29)").Connected != null}, mang theo {still.Subtree().Count - 1} bộ phận, cây giá A còn {standA.Subtree().Count}");
        hand.AttachHeld(PortOf(still, "Khớp dưới (24/29)"), PortOf(flask, "Cổ bình (khớp 24/29)"));
        yield return Frames(2);
        Log($"[lap_rap] Lắp lại: cây giá A {standA.Subtree().Count} bộ phận");

        // 5) Đổ: xoay kẹp ra ngang, lắp lại đầu nối cho nhánh sinh hàn chĩa theo tay kẹp (cả đoàn chìa ra ngoài đế giá)
        Khkt.Assembly.AssemblyManager.Instance.verbose = true;
        Log($"[lap_rap] Có AssemblyManager: {Khkt.Assembly.AssemblyManager.Instance != null}, bật: {Khkt.Assembly.AssemblyManager.Instance?.isActiveAndEnabled}");
        yield return Lap(clamp1, "Khớp nối kẹp", standA, "Cọc giá ở 40 cm (phải)");
        var armDir = Vector3.ProjectOnPlane(PortOf(clamp1, "Má kẹp").transform.forward, Vector3.up).normalized;
        hand.Pick(still.Grab);
        yield return Frames(1);
        still.transform.rotation = Quaternion.LookRotation(Vector3.Cross(armDir, Vector3.up), Vector3.up);  // nhánh (trục +x) theo tay kẹp
        hand.AttachHeld(PortOf(still, "Khớp dưới (24/29)"), PortOf(flask, "Cổ bình (khớp 24/29)"));
        var parts5 = standA.Subtree();
        var com5 = Khkt.Assembly.AssemblyManager.CenterOfMass(parts5, out float m5);
        Khkt.Assembly.AssemblyManager.Footprint(parts5, out var fp5, out _);
        Log($"[lap_rap] Trước khi đổ: {m5:0} g, trọng tâm ({com5.x:0.000}, {com5.z:0.000}), đế x {fp5.xMin:0.000}..{fp5.xMax:0.000}, z {fp5.yMin:0.000}..{fp5.yMax:0.000}");
        float tw = Time.time;
        while (Khkt.Assembly.AssemblyManager.LastTopple == null && Time.time - tw < 3f) yield return null;
        yield return new WaitForSeconds(1f);
        Log($"[lap_rap] Kẹp vươn ngang: {(Khkt.Assembly.AssemblyManager.LastTopple ?? "KHÔNG ĐỔ")} · nước còn trong bình {flask.Container.VolumeMl:0} ml · thông báo: {LabMessages.Message}");
        viewer.GoNear(standA.transform.position + Vector3.up * 0.3f, new Vector3(0.2f, 0.4f, -1f), 1.3f);
        yield return Frames(8);
        Shot("c04_bo_do");

        // 6) Thao tác thật bằng tia ngắm: cầm kẹp 2, trỏ vào cọc giá B → hiện bóng mờ, bấm để lắp
        viewer.GoTo(12);
        yield return Frames(4);
        var standB = Part("Giá sắt B"); var clamp2 = Part("Kẹp càng cua 2");
        hand.Pick(clamp2.Grab);
        var rodPoint = standB.ports.First(x => x.label == "Cọc giá ở 45 cm (trước)").transform.position;
        hand.OverrideAimPoint = rodPoint;
        yield return Frames(6);
        var ghost = GameObject.Find("Bóng mờ lắp ráp");
        Log($"[lap_rap] Trỏ vào cọc giá B: thao tác={hand.Current}, khớp đích={hand.AttachTarget?.label}, bóng mờ hiện={(ghost != null && ghost.activeSelf)}, gợi ý: {string.Join(" | ", hand.Hints.Select(h => h.key + ": " + h.text))}");
        Shot("c05_bong_mo_truoc_khi_lap");
        hand.OverrideHold = true;
        yield return Frames(2);
        hand.OverrideHold = false;
        hand.OverrideAimPoint = null;
        yield return Frames(3);
        Log($"[lap_rap] Sau khi bấm: kẹp 2 nối vào {(clamp2.ParentPart != null ? clamp2.ParentPart.partName : "KHÔNG")} ở {clamp2.ParentJoint?.Connected?.label}, tay đang cầm={(hand.Held != null)}");

        yield return MultiExperimentTest();
    }

    /// <summary>Tháo hết các bộ lắp, trả mọi bộ phận về chỗ cũ (giữ 4 ống nghiệm cắm trên giá).</summary>
    string toppleMark;
    string NewTopple() => Khkt.Assembly.AssemblyManager.LastTopple != toppleMark ? Khkt.Assembly.AssemblyManager.LastTopple : "không";

    IEnumerator ResetAll()
    {
        toppleMark = Khkt.Assembly.AssemblyManager.LastTopple;
        if (hand.Held != null) hand.ReturnHome();
        var all = Khkt.Rendering.ZoneRoot.FindAll<Khkt.Assembly.ApparatusPart>().Where(p => !p.fixture).ToList();
        foreach (var p in all)
            if (p.ParentJoint != null && !(p.ParentPart != null && p.ParentPart.partName == "Giá ống nghiệm")) p.Detach();
        foreach (var p in all)
            if (p.ParentJoint == null && p.Grab != null) p.transform.SetPositionAndRotation(p.Grab.HomePosition, p.Grab.HomeRotation);
        foreach (var p in all) Khkt.Assembly.ApparatusPart.NotifyChanged(p);
        yield return Frames(6);
    }

    void Opening(string tag, Khkt.Assembly.ApparatusPart vessel)
    {
        var open = vessel.Openings();
        Log($"[lap_rap] {tag}: {(open.Count == 0 ? "HỆ KÍN" : "hệ hở ở " + string.Join(", ", open.Select(o => $"{o.label} ({o.Part.partName})")))}");
    }

    void Place(Khkt.Assembly.ApparatusPart part, Vector3 xz) => Place(part.Grab, xz);

    /// <summary>Đặt vật xuống mặt đỡ cao nhất ngay dưới điểm (mặt bàn, đế giá sắt...).</summary>
    void Place(Grabbable g, Vector3 xz)
    {
        hand.Pick(g);
        float y = LabStationTopY();
        foreach (var h in Physics.RaycastAll(new Vector3(xz.x, LabStationTopY() + 0.05f, xz.z), Vector3.down, 0.2f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(g.transform)) y = Mathf.Max(y, h.point.y);
        hand.PlaceOnSurface(new Vector3(xz.x, y, xz.z));
    }

    Grabbable Lamp(string name) => Khkt.Rendering.ZoneRoot.FindAll<Grabbable>().First(g => g.name == name);

    /// <summary>Lắp mẫu cho nhiều loại thí nghiệm, không chỉ chuẩn độ / chưng cất.</summary>
    IEnumerator MultiExperimentTest()
    {
        yield return ResetAll();
        var rack = Part("Giá ống nghiệm");
        Log($"[lap_rap] Giá ống nghiệm: {Enumerable.Range(1, 4).Count(i => Part($"Ống nghiệm {i}").ParentPart == rack)}/4 ống cắm sẵn, ống 1 thẳng đứng lệch {Vector3.Angle(Part("Ống nghiệm 1").transform.up, Vector3.up):0}°");
        Log($"[lap_rap] Sau khi tháo hết: giá A còn {Part("Giá sắt A").Subtree().Count} bộ phận, bình cầu (1) ở chỗ cũ lệch {Vector3.Distance(Part("Bình cầu 250 ml (1)").transform.position, Part("Bình cầu 250 ml (1)").Grab.HomePosition) * 1000:0} mm");

        // A) Đun hồi lưu – este hóa (giá B): bình cầu + ống sinh hàn bầu lắp đứng, nước vào dưới ra trên (cặp vòi 2)
        var standB = Part("Giá sắt B"); var cl1 = Part("Kẹp càng cua 1"); var rf = Part("Bình cầu 250 ml (2)"); var allihn = Part("Ống sinh hàn hồi lưu (bầu)");
        yield return Lap(cl1, "Khớp nối kẹp", standB, "Cọc giá ở 30 cm (trước)");
        yield return Lap(rf, "Cổ bình (chỗ kẹp)", cl1, "Má kẹp");
        yield return Lap(allihn, "Đầu ra (24/29)", rf, "Cổ bình (khớp 24/29)");
        yield return Lap(Part("Ống cao su 1 (đầu A)"), "Đầu ống A", allihn, "Vòi nước vào (dưới)");
        yield return Lap(Part("Ống cao su 1 (đầu B)"), "Đầu ống B", Part("Vòi cấp nước 2"), "Vòi cấp nước 2 (đầu nối ống cao su)");
        yield return Lap(Part("Ống cao su 2 (đầu A)"), "Đầu ống A", allihn, "Vòi nước ra (trên)");
        yield return Lap(Part("Ống cao su 2 (đầu B)"), "Đầu ống B", Part("Ống thoát nước 2"), "Ống thoát nước 2 (đầu nối ống cao su)");
        Log($"[lap_rap] Hồi lưu: ống sinh hàn lệch phương thẳng đứng {Vector3.Angle(allihn.transform.up, Vector3.up):0}°, áo nước lối hở {allihn.Openings(1).Count}");
        Opening("Hồi lưu – bình cầu", rf);

        // B) Điều chế – rửa – thu khí (giá C): bình cầu có nhánh + nút 1 lỗ + phễu nhỏ giọt → bình rửa khí → ống chữ L → kệ trong chậu nước, ống nghiệm to úp ngược thu khí
        var standC = Part("Giá sắt C"); var cl2 = Part("Kẹp càng cua 2"); var sa = Part("Bình cầu có nhánh 250 ml");
        var st26 = Part("Nút cao su 1 lỗ (bình cầu)"); var df = Part("Phễu nhỏ giọt"); var wash = Part("Bình rửa khí");
        var trough = Part("Chậu thủy tinh (thu khí)"); var lt = Part("Ống dẫn chữ L"); var big = Part("Ống nghiệm to");
        yield return Lap(cl2, "Khớp nối kẹp", standC, "Cọc giá ở 30 cm (trước)");
        yield return Lap(sa, "Cổ bình (chỗ kẹp)", cl2, "Má kẹp");
        yield return Lap(st26, "Thân nút", sa, "Miệng bình (đậy nút)");
        yield return Lap(df, "Đuôi phễu (cắm qua nút)", st26, "Lỗ nút");
        Opening("Bình điều chế chỉ đậy nút + phễu (nhánh còn để trống)", sa);
        var armT = PortOf(sa, "Nhánh dẫn khí").transform;
        Log($"[lap_rap] Nhánh bình điều chế chĩa theo ({armT.forward.x:0.0}, {armT.forward.y:0.0}, {armT.forward.z:0.0})");
        Place(wash, armT.position + Vector3.ProjectOnPlane(armT.forward, Vector3.up).normalized * 0.2f);
        yield return Frames(2);
        yield return Lap(Part("Ống cao su 5 (đầu A)"), "Đầu ống A", sa, "Nhánh dẫn khí");
        yield return Lap(Part("Ống cao su 5 (đầu B)"), "Đầu ống B", wash, "Ống vào (sục xuống đáy)");
        yield return Lap(Part("Ống cao su 6 (đầu A)"), "Đầu ống A", wash, "Ống ra");
        // ống chữ L: đầu dưới luồn ngang vào lỗ kệ, nhánh ngang dựng lên khỏi mặt nước
        hand.Pick(lt.Grab);
        yield return Frames(1);
        var hole = PortOf(trough, "Lỗ dẫn khí của kệ");
        lt.transform.rotation = Quaternion.LookRotation(Vector3.up, hole.transform.forward);
        hand.AttachHeld(PortOf(lt, "Đầu dưới (cắm qua nút)"), hole);
        yield return Frames(2);
        Log($"[lap_rap] Ống chữ L vào lỗ kệ: đã nối={PortOf(lt, "Đầu dưới (cắm qua nút)").Connected == hole}, đầu ngang cao hơn miệng chậu {(PortOf(lt, "Đầu ngang").transform.position.y - (trough.transform.position.y + 0.1f)) * 100:0.0} cm");
        yield return Lap(Part("Ống cao su 6 (đầu B)"), "Đầu ống B", lt, "Đầu ngang");
        yield return Lap(big, "Miệng ống nghiệm", trough, "Kệ úp ống");
        Log($"[lap_rap] Ống nghiệm to úp trên kệ: miệng chúc xuống lệch {Vector3.Angle(PortOf(big, "Miệng ống nghiệm").transform.forward, Vector3.down):0}°");
        Opening("Điều chế khí – bình có nhánh", sa);
        Log($"[lap_rap] Lọ thu khí Ø50 úp lên kệ được: {Khkt.Assembly.Port.Compatible(PortOf(Part("Lọ thu khí"), "Miệng lọ thu khí"), PortOf(trough, "Kệ úp ống"))}; bình rửa khí: {wash.DescribeAssembly()}");

        // C) Nhiệt phân chất rắn (giá A): ống nghiệm kẹp nằm nghiêng, nút 1 lỗ, ống dẫn thẳng, ống cao su
        var standA = Part("Giá sắt A"); var cl3 = Part("Kẹp càng cua 3"); var t1 = Part("Ống nghiệm 1");
        yield return Lap(cl3, "Khớp nối kẹp", standA, "Cọc giá ở 25 cm (trước)");
        yield return Lap(t1, "Thân ống (kẹp nghiêng, miệng sang phải)", cl3, "Má kẹp");
        yield return Lap(Part("Nút cao su 1 lỗ (ống nghiệm)"), "Thân nút", t1, "Miệng ống nghiệm");
        yield return Lap(Part("Ống dẫn thẳng"), "Đầu dưới", Part("Nút cao su 1 lỗ (ống nghiệm)"), "Lỗ nút");
        yield return Lap(Part("Ống cao su 3 (đầu A)"), "Đầu ống A", Part("Ống dẫn thẳng"), "Đầu trên");
        var mouth = PortOf(t1, "Miệng ống nghiệm").transform.position; var bottom = PortOf(t1, "Đáy ống nghiệm").transform.position;
        Log($"[lap_rap] Nhiệt phân: miệng ống hướng {(mouth.x > bottom.x ? "+x (vào giữa bàn)" : "-x (ra mép bàn)")}, trục ống lệch phương ngang {Vector3.Angle(bottom - mouth, Vector3.ProjectOnPlane(bottom - mouth, Vector3.up)):0}°, miệng thấp hơn đáy {(bottom.y - mouth.y) * 1000:0} mm, rời giá ống nghiệm={t1.ParentPart != rack}");
        Opening("Nhiệt phân – ống nghiệm", t1);
        yield return Frames(20);
        Log($"[lap_rap] Ba bộ cùng lúc: đổ={NewTopple()}");
        viewer.GoNear(standB.transform.position + new Vector3(0.1f, 0.3f, -0.1f), new Vector3(0.05f, 0.5f, -1f), 1.7f);
        yield return Frames(8);
        Shot("c06_ba_bo_hoi_luu_dieu_che_nhiet_phan");
        viewer.GoNear(t1.transform.position, new Vector3(0.5f, 0.3f, -1f), 0.6f);
        yield return Frames(8);
        Shot("c06b_nhiet_phan");
        viewer.GoNear(sa.transform.position + new Vector3(0.2f, 0.05f, 0), new Vector3(-0.2f, 0.35f, -1f), 1.1f);
        yield return Frames(8);
        Shot("c07_dieu_che_rua_thu_khi");
        viewer.GoNear(rf.transform.position + Vector3.up * 0.2f, new Vector3(-0.4f, 0.2f, -1f), 0.9f);
        yield return Frames(8);
        Shot("c08_hoi_luu_este_hoa");

        // D) Lọc, lọc hút, chiết, cô cạn
        yield return ResetAll();
        // dời đèn cồn trên đế giá sang bên để có chỗ đặt cốc hứng dưới vòng sắt
        Place(Lamp("Đèn cồn (giá A)"), standA.transform.position + new Vector3(0.17f, 0, -0.1f));
        Place(Lamp("Đèn cồn (giá B)"), standB.transform.position + new Vector3(0.17f, 0, -0.1f));
        yield return Frames(2);
        var ring1 = Part("Vòng sắt"); var funnel = Part("Phễu lọc"); var b250 = Part("Cốc thủy tinh 250 ml");
        yield return Lap(ring1, "Khớp nối vòng", standA, "Cọc giá ở 15 cm (trước)");
        yield return Lap(funnel, "Thân phễu (tựa vòng / miệng bình)", ring1, "Vòng đỡ");
        var stem = PortOf(funnel, "Đuôi phễu lọc").transform.position;
        Place(b250, stem);
        yield return Frames(2);
        Log($"[lap_rap] Lọc thường: đuôi phễu thấp hơn miệng cốc {(PortOf(b250, "Miệng cốc").transform.position.y - stem.y) * 100:0.0} cm (nằm trong cốc), lệch tâm {Vector2.Distance(new Vector2(stem.x, stem.z), new Vector2(b250.transform.position.x, b250.transform.position.z)) * 1000:0} mm");

        var suction = Part("Bình hút lọc 250 ml"); var buchner = Part("Phễu Buchner");
        yield return Lap(buchner, "Nón cao su (lắp miệng bình hút)", suction, "Miệng bình hút lọc");
        yield return Lap(Part("Ống cao su 4 (đầu A)"), "Đầu ống A", suction, "Nhánh hút chân không");
        yield return Lap(Part("Ống cao su 4 (đầu B)"), "Đầu ống B", Part("Vòi hút chân không"), "Vòi hút chân không (đầu nối ống cao su)");
        Opening("Lọc hút – bình hút lọc", suction);

        var ring2 = Part("Vòng sắt 2"); var sep = Part("Phễu chiết 125 ml"); var b100 = Part("Cốc thủy tinh 100 ml");
        yield return Lap(ring2, "Khớp nối vòng", standB, "Cọc giá ở 20 cm (trước)");
        yield return Lap(sep, "Bầu phễu (tựa vòng sắt)", ring2, "Vòng đỡ");
        var sstem = PortOf(sep, "Đuôi phễu chiết").transform.position;
        Place(b100, sstem);
        yield return Frames(2);
        Log($"[lap_rap] Chiết: phễu chiết lệch phương thẳng đứng {Vector3.Angle(sep.transform.up, Vector3.up):0}°, đuôi cao hơn miệng cốc {(sstem.y - PortOf(b100, "Miệng cốc").transform.position.y) * 100:0.0} cm");

        var tripod = Part("Kiềng ba chân"); var gauze = Part("Lưới tản nhiệt"); var dish = Part("Bát sứ cô cạn");
        yield return Lap(gauze, "Đáy lưới", tripod, "Vòng kiềng");
        yield return Lap(dish, "Đáy bát sứ", gauze, "Mặt lưới");
        Log($"[lap_rap] Cô cạn: đáy bát cao hơn mặt bàn {(dish.transform.position.y - LabStationTopY()) * 100:0.0} cm, lưới nằm ngang lệch {Vector3.Angle(gauze.transform.up, Vector3.up):0}°");
        Opening("Cô cạn – bát sứ", dish);
        Log($"[lap_rap] Cốc lên lưới được: {Khkt.Assembly.Port.Compatible(PortOf(b250, "Đáy cốc"), PortOf(gauze, "Mặt lưới"))}, bình cầu lên vòng sắt được: {Khkt.Assembly.Port.Compatible(PortOf(Part("Bình cầu 250 ml (1)"), "Đáy bình"), PortOf(ring1, "Vòng đỡ"))} (vòng đang bận: {!PortOf(ring1, "Vòng đỡ").Free})");
        yield return Frames(20);
        Log($"[lap_rap] Bốn bộ lọc / chiết / cô cạn: đổ={NewTopple()}");
        viewer.GoNear(new Vector3(standB.transform.position.x - 0.2f, LabStationTopY() + 0.2f, standB.transform.position.z - 0.1f), new Vector3(0.1f, 0.45f, -1f), 1.6f);
        yield return Frames(8);
        Shot("c09_loc_chiet_co_can");
        viewer.GoNear(suction.transform.position + Vector3.up * 0.1f, new Vector3(0.3f, 0.3f, -1f), 0.8f);
        yield return Frames(8);
        Shot("c10_loc_hut");
    }

    // ---------- Khu chế tạo dụng cụ (F1 + F2) ----------

    void ShotCam(Camera c, string name)
    {
        var old = c.targetTexture;
        bool was = c.enabled;
        c.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        c.Render();
        // giao diện khu chế tạo vẽ bằng camera phủ trong ngăn xếp URP: Render() thủ công không vẽ ngăn xếp, nên chụp riêng
        // bằng chính camera đó chuyển tạm sang camera gốc không xóa nền
        var fw = Khkt.Assembly.FabricationWorkspace.Instance;
        if (fw != null && fw.IsOpen && fw.Ui != null && c == fw.workCamera)
        {
            var uc = fw.Ui.UiCamera;
            var ud = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(uc);
            ud.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
            var clear = uc.clearFlags;
            var bg = uc.backgroundColor;
            uc.clearFlags = CameraClearFlags.SolidColor;
            uc.backgroundColor = new Color(0, 0, 0, 0);
            var uiRt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            uc.targetTexture = uiRt;
            uc.Render();
            uc.targetTexture = rt;
            uc.clearFlags = clear;
            uc.backgroundColor = bg;
            ud.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            var uiTex = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            RenderTexture.active = uiRt;
            uiTex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            RenderTexture.active = null;
            var a = tex.GetPixels32();
            var b = uiTex.GetPixels32();
            for (int i = 0; i < a.Length; i++)
            {
                float k = b[i].a / 255f;
                a[i] = new Color32((byte)(b[i].r * k + a[i].r * (1 - k)), (byte)(b[i].g * k + a[i].g * (1 - k)), (byte)(b[i].b * k + a[i].b * (1 - k)), 255);
            }
            tex.SetPixels32(a);
            tex.Apply();
            Destroy(uiTex);
            uiRt.Release();
            Destroy(uiRt);
            c.targetTexture = old;
            c.enabled = was;
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            return;
        }
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        c.targetTexture = old;
        c.enabled = was;
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
    }

    IEnumerator FabLap(Khkt.Assembly.FabricationWorkspace ws, string lib, string mine, Khkt.Assembly.ApparatusPart target, string targetPort, List<Khkt.Assembly.ApparatusPart> made)
    {
        var p = ws.SpawnHeld(lib);
        yield return null;
        var m = PortOf(p, mine);
        var tp = PortOf(target, targetPort);
        bool ok = ws.AttachHeld(m, tp);
        yield return null;
        made.Add(p);
        Log($"[che_tao] {p.partName} [{m.Describe()}] → {target.partName} [{tp.Describe()}]: nối={ok}, lệch {Vector3.Distance(m.transform.position, tp.transform.position) * 1000:0.0} mm, góc {Vector3.Angle(m.transform.forward, -tp.transform.forward):0.0}°");
    }

    static int Joints(IEnumerable<Khkt.Assembly.ApparatusPart> parts) => parts.Where(p => p != null).Sum(p => p.ports.Count(x => x != null && x.Connected != null));

    IEnumerator FabTest()
    {
        var ws = Khkt.Assembly.FabricationWorkspace.Instance;
        var lib = Khkt.Assembly.PartLibrary.Instance;
        Log($"[che_tao] Có xưởng ảo: {ws != null}, kho mẫu: {(lib != null ? lib.entries.Length : 0)} loại, nhóm: {string.Join(", ", Khkt.Assembly.PartLibrary.Groups.Select(g => $"{g} {lib.InGroup(g).Count()}"))}");
        Log($"[che_tao] Bộ phận rời còn trong phòng lab: {Khkt.Rendering.ZoneRoot.FindAll<Khkt.Assembly.ApparatusPart>().Count(p => !p.fixture)} (mong đợi 0), vòi ở bàn lắp ráp: {Khkt.Rendering.ZoneRoot.FindAll<Khkt.Assembly.ApparatusPart>().Count(p => p.fixture)}");

        // ngăn kính nhìn từ ngoài, rồi vào trạm 12 (trước bàn chế tạo)
        viewer.GoNear(new Vector3(18.4f, 1.1f, 11.6f), new Vector3(-0.55f, 0.35f, -1f), 4.6f);
        yield return Frames(10);
        Shot("f01_ngan_kinh_che_tao");
        viewer.GoNear(new Vector3(15.6f, 1.2f, 11.3f), new Vector3(0.35f, 0.45f, -1f), 3.6f);
        yield return Frames(10);
        Shot("f01b_ban_thi_nghiem_lap_rap");
        viewer.GoTo(12);
        yield return Frames(12);
        Shot("f02_ban_che_tao");
        var station = FindAnyObjectByType<Khkt.Assembly.FabricationStation>();
        Log($"[che_tao] Trạm 12: {viewer.Stations.First(s => s.order == 12).name}, bàn chế tạo cách mắt {Vector3.Distance(cam.transform.position, station.transform.position):0.00} m, gợi ý: {((Khkt.Demo.IDemoUsable)station).UseHint}");

        // mở giao diện
        station.Use();
        ws.workCamera.targetTexture = rt;  // bố cục giao diện theo đúng cỡ ảnh chụp 1600 × 900
        ws.Ui.UiCamera.targetTexture = rt;
        yield return Frames(3);
        Log($"[che_tao] Mở: IsOpen={ws.IsOpen}, camera người xem bật={cam.enabled}, camera xưởng bật={ws.workCamera.enabled}, UiCapture={DesktopViewer.UiCapture}");
        float tw = Time.realtimeSinceStartup;
        while (!ws.ThumbnailsReady && Time.realtimeSinceStartup - tw < 30f) yield return null;
        Log($"[che_tao] Ảnh nhỏ: xong={ws.ThumbnailsReady} sau {Time.realtimeSinceStartup - tw:0.0} s, có ảnh {lib.entries.Count(e => e.thumbnail != null)}/{lib.entries.Length}");
        yield return Frames(3);
        ShotCam(ws.workCamera, "f03_giao_dien_trong");

        // lắp bộ chưng cất trong xưởng ảo
        var made = new List<Khkt.Assembly.ApparatusPart>();
        var stand = ws.SpawnHeld("gia_sat");
        ws.PlaceHeld(-0.45f, 0.08f);
        made.Add(stand);
        yield return null;
        yield return FabLap(ws, "kep", "Khớp nối kẹp", stand, "Cọc giá ở 40 cm (trước)", made);
        var clamp = made[made.Count - 1];
        yield return FabLap(ws, "binh_cau", "Cổ bình (chỗ kẹp)", clamp, "Má kẹp", made);
        var flask = made[made.Count - 1];
        yield return FabLap(ws, "dau_noi", "Khớp dưới (24/29)", flask, "Cổ bình (khớp 24/29)", made);
        var still = made[made.Count - 1];
        yield return FabLap(ws, "nhiet_ke", "Khớp nhiệt kế (14/23)", still, "Khớp trên (14/23)", made);
        yield return FabLap(ws, "sinh_han", "Đầu vào hơi (24/29)", still, "Nhánh xiên (24/29)", made);
        var cond = made[made.Count - 1];
        yield return FabLap(ws, "dau_noi_hung", "Khớp vào (24/29)", cond, "Đầu ra (24/29)", made);
        var adapter = made[made.Count - 1];
        Log($"[che_tao] Kiểm tra khi sinh hàn chưa nối nước:\n{System.Text.RegularExpressions.Regex.Replace(ws.Report(), "<.*?>", "")}");
        var fixtures = ws.table.GetComponentsInChildren<Khkt.Assembly.ApparatusPart>().Where(p => p.fixture).ToList();
        var supply = fixtures.First(f => f.name == "Vòi cấp nước (xưởng) 1");
        var drain = fixtures.First(f => f.name == "Ống thoát nước (xưởng) 1");
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", cond, "Vòi nước vào (dưới)", made);
        var hoseA = made[made.Count - 1];
        ws.Pick(hoseA.linked);
        bool hb = ws.AttachHeld(PortOf(hoseA.linked, "Đầu ống B"), PortOf(supply, "Vòi cấp nước 1 (đầu nối ống cao su)"));
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", cond, "Vòi nước ra (trên)", made);
        var hose2 = made[made.Count - 1];
        ws.Pick(hose2.linked);
        bool hb2 = ws.AttachHeld(PortOf(hose2.linked, "Đầu ống B"), PortOf(drain, "Ống thoát nước 1 (đầu nối ống cao su)"));
        Log($"[che_tao] Nối đầu B hai ống cao su vào vòi xưởng: {hb}, {hb2}");
        var tip = PortOf(adapter, "Đầu nhỏ giọt").transform;
        var tl = ws.table.InverseTransformPoint(tip.position);
        var recv = ws.SpawnHeld("binh_non");
        ws.PlaceHeld(tl.x, tl.z);
        var fl = ws.table.InverseTransformPoint(flask.transform.position);
        ws.SpawnHeld("den_con");
        ws.PlaceHeld(fl.x, fl.z + 0.0f);
        yield return Frames(4);
        Log($"[che_tao] Đầu nhỏ giọt chĩa xuống lệch {Vector3.Angle(tip.forward, Vector3.down):0}°, cao hơn miệng bình hứng {(tip.position.y - PortOf(recv, "Miệng bình nón").transform.position.y) * 100:0.0} cm, lệch ngang {Vector2.Distance(new Vector2(tip.position.x, tip.position.z), new Vector2(recv.transform.position.x, recv.transform.position.z)) * 100:0.0} cm");
        Log($"[che_tao] Bộ chưng cất: {flask.DescribeAssembly()}");
        string rep = System.Text.RegularExpressions.Regex.Replace(ws.Report(), "<.*?>", "");
        Log($"[che_tao] Bảng kiểm tra:\n{rep}");
        Log($"[che_tao] Không bộ nào đổ trong xưởng: {(Khkt.Assembly.AssemblyManager.LastTopple ?? "đúng")}");
        ws.ResetView(-25f, 24f, 1.55f, new Vector3(-0.3f, 1.2f, 0.05f));
        yield return Frames(4);
        ShotCam(ws.workCamera, "f04_lap_chung_cat");

        // bóng mờ: cầm kẹp mới, trỏ vào cọc giá ở 20 cm
        var k2 = ws.SpawnHeld("kep");
        var rod = PortOf(stand, "Cọc giá ở 20 cm (phải)").transform.position;
        ws.ResetView(35f, 20f, 1.0f, ws.table.InverseTransformPoint(rod) + new Vector3(0, 0.05f, 0));
        yield return null;
        ws.OverrideRay = new Ray(ws.workCamera.transform.position, (rod - ws.workCamera.transform.position).normalized);
        yield return Frames(3);
        var ghost = GameObject.Find("Bóng mờ lắp ráp");
        Log($"[che_tao] Trỏ kẹp mới vào cọc giá: khớp đích={ws.AttachTarget?.label}, bóng mờ hiện={(ghost != null && ghost.activeSelf)}");
        ShotCam(ws.workCamera, "f05_bong_mo");
        ws.OverrideRay = null;
        int before = ws.Parts().Count;
        ws.Undo();  // như bấm Esc khi đang cầm: bỏ kẹp vừa lấy
        yield return Frames(2);
        Log($"[che_tao] Hoàn tác lấy kẹp: {before} → {ws.Parts().Count} bộ phận, khớp đang nối {Joints(ws.Parts())}");

        // tháo: cầm đầu nối chưng cất (kéo theo nhiệt kế, sinh hàn, đầu nối hứng, 2 đầu ống A), hoàn tác
        still = ws.Parts().First(p => p.libraryId == "dau_noi");
        int j0 = Joints(ws.Parts());
        ws.Pick(still);
        Log($"[che_tao] Cầm đầu nối chưng cất: mang theo {still.Subtree().Count - 1} bộ phận, còn nối bình cầu={PortOf(still, "Khớp dưới (24/29)").Connected != null}");
        ws.Undo();
        yield return Frames(2);
        Log($"[che_tao] Hoàn tác tháo: khớp đang nối {j0} → {Joints(ws.Parts())}, {ws.Parts().Count} bộ phận");

        // bỏ ống cao su: cả hai đầu cùng mất
        var hose = ws.Parts().First(p => p.libraryId == "ong_cao_su_dai" && p.librarySub == 0);
        int n0 = ws.Parts().Count;
        ws.Pick(hose);
        ws.DeleteHeld();
        yield return Frames(2);
        Log($"[che_tao] Bỏ một ống cao su: {n0} → {ws.Parts().Count} bộ phận (mất 2 đầu)");
        ws.Undo();
        yield return Frames(2);
        Log($"[che_tao] Hoàn tác: {ws.Parts().Count} bộ phận, khớp {Joints(ws.Parts())}");

        // bộ lọc thường + ống nghiệm đậy nút đặc (hệ kín) bên phải bàn
        var standB = ws.SpawnHeld("gia_sat");
        ws.PlaceHeld(0.55f, 0.08f);
        var m2 = new List<Khkt.Assembly.ApparatusPart>();
        yield return FabLap(ws, "vong_sat", "Khớp nối vòng", standB, "Cọc giá ở 15 cm (trước)", m2);
        yield return FabLap(ws, "pheu_loc", "Thân phễu (tựa vòng / miệng bình)", m2[0], "Vòng đỡ", m2);
        var stem = ws.table.InverseTransformPoint(PortOf(m2[1], "Đuôi phễu lọc").transform.position);
        var beaker = ws.SpawnHeld("coc250");
        ws.PlaceHeld(stem.x, stem.z);
        var tube = ws.SpawnHeld("ong_nghiem");
        ws.PlaceHeld(0.95f, -0.3f);
        yield return FabLap(ws, "nut0_17", "Thân nút", tube, "Miệng ống nghiệm", m2);
        yield return Frames(3);
        rep = System.Text.RegularExpressions.Regex.Replace(ws.Report(), "<.*?>", "");
        Log($"[che_tao] Bảng kiểm tra cả bàn:\n{rep}");
        ws.ResetView(0f, 30f, 2.3f, new Vector3(0.1f, 1.1f, 0f));
        yield return Frames(4);
        ShotCam(ws.workCamera, "f06_ca_ban");

        // xóa hết rồi hoàn tác
        int nAll = ws.Parts().Count, jAll = Joints(ws.Parts());
        ws.ClearAll();
        yield return Frames(2);
        Log($"[che_tao] Xóa hết: {nAll} → {ws.Parts().Count}");
        ws.Undo();
        yield return Frames(2);
        Log($"[che_tao] Hoàn tác xóa hết: {ws.Parts().Count} bộ phận (trước {nAll}), khớp {Joints(ws.Parts())} (trước {jAll})");

        // thoát
        ws.workCamera.targetTexture = null;
        ws.Ui.UiCamera.targetTexture = null;
        ws.Close();
        yield return Frames(3);
        Log($"[che_tao] Thoát: IsOpen={ws.IsOpen}, camera người xem bật={cam.enabled}, UiCapture={DesktopViewer.UiCapture}, bộ phận rời ngoài phòng lab: {Khkt.Rendering.ZoneRoot.FindAll<Khkt.Assembly.ApparatusPart>().Count(p => !p.fixture)}");
    }

    // ---------- F3 + F4: 9 bộ mẫu lắp trong xưởng ảo, gia công ra các bàn đích ----------

    Khkt.Assembly.ApparatusPart NewAt(Khkt.Assembly.FabricationWorkspace ws, string lib, float x, float z, float yaw = 0f)
    {
        var p = ws.SpawnHeld(lib);
        ws.PlaceHeld(x, z, yaw);
        return p;
    }

    Vector3 Local(Khkt.Assembly.FabricationWorkspace ws, Vector3 world) => ws.table.InverseTransformPoint(world);

    Khkt.Assembly.ApparatusPart Fix(Khkt.Assembly.FabricationWorkspace ws, string name) =>
        ws.table.GetComponentsInChildren<Khkt.Assembly.ApparatusPart>().First(p => p.fixture && p.name == name);

    IEnumerator HoseTo(Khkt.Assembly.FabricationWorkspace ws, Khkt.Assembly.ApparatusPart hoseA, Khkt.Assembly.ApparatusPart target, string port)
    {
        ws.Pick(hoseA.linked);
        bool ok = ws.AttachHeld(PortOf(hoseA.linked, "Đầu ống B"), PortOf(target, port));
        yield return null;
        Log($"[lap_rap] {hoseA.linked.partName} → {target.partName} [{port}]: nối={ok}");
    }

    static Khkt.Assembly.FabricationDestination Dest(string start) => Khkt.Assembly.FabricationDestination.All.First(d => d.label.StartsWith(start));

    static List<Khkt.Assembly.ApparatusPart> SetParts(int id) =>
        Khkt.Rendering.ZoneRoot.FindAll<Khkt.Assembly.ApparatusPart>().Where(p => p.fabricatedSet == id).ToList();

    IEnumerator OpenFab()
    {
        var ws = Khkt.Assembly.FabricationWorkspace.Instance;
        if (ws.IsOpen) yield break;
        FindAnyObjectByType<Khkt.Assembly.FabricationStation>().Use();
        ws.workCamera.targetTexture = rt;
        ws.Ui.UiCamera.targetTexture = rt;
        yield return Frames(2);
        float tw = Time.realtimeSinceStartup;
        while (!ws.ThumbnailsReady && Time.realtimeSinceStartup - tw < 30f) yield return null;
    }

    /// <summary>Gia công bộ đang có trên bàn ảo ra bàn đích (thử lần lượt các bàn trong danh sách), kiểm tra kết quả ngoài phòng.</summary>
    IEnumerator FabTo(string tag, params string[] dests)
    {
        var ws = Khkt.Assembly.FabricationWorkspace.Instance;
        var vessels = ws.Parts().Where(p => p.Container != null && p.Container.reactive).ToList();
        var openBefore = vessels.Select(v => v.Openings().Count).ToList();
        int nParts = ws.Parts().Count, nJoints = Joints(ws.Parts());
        Khkt.Assembly.FabricationDestination d = null;
        var tried = new List<string>();
        foreach (var name in dests)
        {
            var c = Dest(name);
            bool room = ws.HasRoom(c);
            tried.Add($"{c.label}: {(room ? "đủ chỗ" : "hết chỗ")}");
            if (room) { d = c; break; }
        }
        if (d == null) { Log($"[lap_rap] {tag}: KHÔNG CÓ BÀN NÀO ĐỦ CHỖ ({string.Join("; ", tried)})"); yield break; }
        string toppleMark0 = Khkt.Assembly.AssemblyManager.LastTopple;
        ws.Fabricate(d);
        float t0 = Time.realtimeSinceStartup;
        while ((ws.Busy || ws.IsOpen) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
        yield return Frames(3);
        var set = ws.LastSet;
        var parts = SetParts(set.id);
        float maxErr = 0;
        foreach (var p in parts)
            foreach (var port in p.ports)
                if (port != null && port.Connected != null) maxErr = Mathf.Max(maxErr, Vector3.Distance(port.transform.position, port.Connected.transform.position));
        float top = d.transform.position.y, lowest = float.PositiveInfinity;
        bool inside = true;
        foreach (var r in parts.Where(p => p.ParentJoint == null && p.linked == null))
        {
            foreach (var rr in r.GetComponentsInChildren<Renderer>())
                if (rr.enabled && rr.GetComponentInParent<Khkt.Assembly.ApparatusPart>() == r) lowest = Mathf.Min(lowest, rr.bounds.min.y);
            var l = d.transform.InverseTransformPoint(r.transform.position);
            if (Mathf.Abs(l.x) > d.size.x / 2 + 0.03f || Mathf.Abs(l.z) > d.size.y / 2 + 0.03f) inside = false;
        }
        var openAfter = vessels.Select(v => v.Openings().Count).ToList();
        float tw = Time.time;
        while (Time.time - tw < 1.2f) yield return null;
        string topple = Khkt.Assembly.AssemblyManager.LastTopple != toppleMark0 ? Khkt.Assembly.AssemblyManager.LastTopple : "không";
        var taps = parts.Where(p => p.ParentJoint != null && p.ParentPart != null && p.ParentPart.fixture).Select(p => p.ParentPart.name).ToList();
        Log($"[lap_rap] {tag} → {d.label}: {parts.Count}/{nParts} bộ phận, khớp {Joints(parts)}/{nJoints}, lệch khớp lớn nhất {maxErr * 1000:0.0} mm, "
            + $"đáy thấp nhất cách mặt bàn {(lowest - top) * 1000:0} mm, trong vùng đặt={inside}, kín/hở giữ nguyên={openBefore.SequenceEqual(openAfter)}, "
            + $"nối vòi bàn: [{string.Join(", ", taps)}], đổ: {topple}, xưởng ảo còn {ws.Parts().Count} bộ phận, chỗ đã thử: {string.Join("; ", tried)}");
    }

    IEnumerator FabAssemblyTest()
    {
        var ws = Khkt.Assembly.FabricationWorkspace.Instance;
        Log($"[lap_rap] Bàn đích: {string.Join(" | ", Khkt.Assembly.FabricationDestination.All.OrderBy(d => d.order).Select(d => d.label))}");
        viewer.GoTo(12);
        yield return Frames(8);
        yield return OpenFab();
        var m = new List<Khkt.Assembly.ApparatusPart>();

        // 1) Chưng cất → bàn thí nghiệm lắp ráp, ống cao su nối lại vào vòi thật
        var stand = NewAt(ws, "gia_sat", -0.45f, 0.08f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", stand, "Cọc giá ở 40 cm (trước)", m);
        yield return FabLap(ws, "binh_cau", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        var flask = m[m.Count - 1];
        yield return FabLap(ws, "dau_noi", "Khớp dưới (24/29)", flask, "Cổ bình (khớp 24/29)", m);
        var still = m[m.Count - 1];
        yield return FabLap(ws, "nhiet_ke", "Khớp nhiệt kế (14/23)", still, "Khớp trên (14/23)", m);
        yield return FabLap(ws, "sinh_han", "Đầu vào hơi (24/29)", still, "Nhánh xiên (24/29)", m);
        var cond = m[m.Count - 1];
        yield return FabLap(ws, "dau_noi_hung", "Khớp vào (24/29)", cond, "Đầu ra (24/29)", m);
        var adapter = m[m.Count - 1];
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", cond, "Vòi nước vào (dưới)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Vòi cấp nước (xưởng) 1"), "Vòi cấp nước 1 (đầu nối ống cao su)");
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", cond, "Vòi nước ra (trên)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Ống thoát nước (xưởng) 1"), "Ống thoát nước 1 (đầu nối ống cao su)");
        var tip = Local(ws, PortOf(adapter, "Đầu nhỏ giọt").transform.position);
        var recv = NewAt(ws, "binh_non", tip.x, tip.z);
        var fl = Local(ws, flask.transform.position);
        NewAt(ws, "den_con", fl.x, fl.z);
        yield return Frames(2);
        Log($"[lap_rap] Chưng cất trên bàn ảo: {flask.DescribeAssembly()}; bàn nhận đủ chỗ={ws.HasRoom(Dest("Bàn nhận"))}");
        ws.Ui.ShowDestinations();
        yield return Frames(2);
        ShotCam(ws.workCamera, "w01_chon_ban_dich");
        // chụp lúc máy đang gia công
        var dLap = Dest("Bàn thí nghiệm lắp ráp");
        ws.Fabricate(dLap);
        yield return new WaitForSeconds(0.8f);
        ShotCam(ws.workCamera, "w02_dang_gia_cong");
        float tb = Time.realtimeSinceStartup;
        while ((ws.Busy || ws.IsOpen) && Time.realtimeSinceStartup - tb < 10f) yield return null;
        yield return Frames(3);
        var distSet = ws.LastSet;
        {
            var parts = SetParts(distSet.id);
            var taps = parts.Where(p => p.ParentJoint != null && p.ParentPart != null && p.ParentPart.fixture).Select(p => p.ParentPart.name);
            var tipW = PortOf(adapter, "Đầu nhỏ giọt").transform;
            Log($"[lap_rap] Chưng cất → {distSet.where}: {parts.Count} bộ phận, khớp {Joints(parts)}, nối vòi bàn [{string.Join(", ", taps)}], "
                + $"đầu nhỏ giọt trên miệng bình hứng lệch ngang {Vector2.Distance(new Vector2(tipW.position.x, tipW.position.z), new Vector2(recv.transform.position.x, recv.transform.position.z)) * 100:0.0} cm, "
                + $"giao diện đóng={!ws.IsOpen}, camera người xem bật={cam.enabled}, thông báo: {LabMessages.Message}");
        }

        // 2) Đun hồi lưu – este hóa → bàn thí nghiệm lắp ráp (cặp vòi 2)
        yield return OpenFab();
        m.Clear();
        var standB = NewAt(ws, "gia_sat", 0.2f, 0.08f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", standB, "Cọc giá ở 30 cm (trước)", m);
        yield return FabLap(ws, "binh_cau", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        var rf = m[m.Count - 1];
        yield return FabLap(ws, "sinh_han_bau", "Đầu ra (24/29)", rf, "Cổ bình (khớp 24/29)", m);
        var allihn = m[m.Count - 1];
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", allihn, "Vòi nước vào (dưới)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Vòi cấp nước (xưởng) 2"), "Vòi cấp nước 2 (đầu nối ống cao su)");
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", allihn, "Vòi nước ra (trên)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Ống thoát nước (xưởng) 2"), "Ống thoát nước 2 (đầu nối ống cao su)");
        var rfl = Local(ws, rf.transform.position);
        NewAt(ws, "den_con", rfl.x, rfl.z);
        yield return FabTo("Hồi lưu (este hóa)", "Bàn thí nghiệm lắp ráp", "Bàn thí nghiệm 3");
        var refluxSet = ws.LastSet;

        // 3) Điều chế – rửa – thu khí → tủ hút 1
        yield return OpenFab();
        m.Clear();
        var standC = NewAt(ws, "gia_sat", -0.75f, 0.1f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", standC, "Cọc giá ở 30 cm (trước)", m);
        yield return FabLap(ws, "binh_cau_nhanh", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        var sa = m[m.Count - 1];
        yield return FabLap(ws, "nut1_26", "Thân nút", sa, "Miệng bình (đậy nút)", m);
        yield return FabLap(ws, "pheu_nho_giot", "Đuôi phễu (cắm qua nút)", m[m.Count - 1], "Lỗ nút", m);
        var armT = PortOf(sa, "Nhánh dẫn khí").transform;
        var flat = Vector3.ProjectOnPlane(armT.forward, Vector3.up).normalized;
        var wl = Local(ws, armT.position + flat * 0.2f);
        var wash = NewAt(ws, "binh_rua_khi", wl.x, wl.z);
        var tl = Local(ws, armT.position + flat * 0.62f);
        var trough = NewAt(ws, "chau", tl.x, tl.z);
        yield return FabLap(ws, "ong_cao_su_ngan", "Đầu ống A", sa, "Nhánh dẫn khí", m);
        yield return HoseTo(ws, m[m.Count - 1], wash, "Ống vào (sục xuống đáy)");
        yield return FabLap(ws, "ong_cao_su_ngan", "Đầu ống A", wash, "Ống ra", m);
        var hose6 = m[m.Count - 1];
        var lt = ws.SpawnHeld("ong_l");
        var hole = PortOf(trough, "Lỗ dẫn khí của kệ");
        lt.transform.rotation = Quaternion.LookRotation(Vector3.up, hole.transform.forward);
        bool ltOk = ws.AttachHeld(PortOf(lt, "Đầu dưới (cắm qua nút)"), hole);
        yield return HoseTo(ws, hose6, lt, "Đầu ngang");
        yield return FabLap(ws, "ong_nghiem_to", "Miệng ống nghiệm", trough, "Kệ úp ống", m);
        Log($"[lap_rap] Điều chế khí trên bàn ảo: ống L vào lỗ kệ={ltOk}; {sa.DescribeAssembly()}");
        yield return FabTo("Điều chế – rửa – thu khí", "Tủ hút khí độc 1", "Tủ hút khí độc 2", "Bàn thí nghiệm 8");

        // 4) Nhiệt phân → tủ hút 2
        yield return OpenFab();
        m.Clear();
        var standD = NewAt(ws, "gia_sat", -0.3f, 0.1f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", standD, "Cọc giá ở 25 cm (trước)", m);
        yield return FabLap(ws, "ong_nghiem", "Thân ống (kẹp nghiêng, miệng sang phải)", m[m.Count - 1], "Má kẹp", m);
        var t1 = m[m.Count - 1];
        yield return FabLap(ws, "nut1_17", "Thân nút", t1, "Miệng ống nghiệm", m);
        yield return FabLap(ws, "ong_thang", "Đầu dưới", m[m.Count - 1], "Lỗ nút", m);
        yield return FabLap(ws, "ong_cao_su_ngan", "Đầu ống A", m[m.Count - 1], "Đầu trên", m);
        var bt = Local(ws, PortOf(t1, "Đáy ống nghiệm").transform.position);
        NewAt(ws, "den_con", bt.x, bt.z);
        yield return FabTo("Nhiệt phân", "Tủ hút khí độc 2", "Tủ hút khí độc 3", "Bàn thí nghiệm 8");

        // 5) Lọc thường → bàn thí nghiệm 2
        yield return OpenFab();
        m.Clear();
        var standE = NewAt(ws, "gia_sat", 0f, 0.1f);
        yield return FabLap(ws, "vong_sat", "Khớp nối vòng", standE, "Cọc giá ở 15 cm (trước)", m);
        yield return FabLap(ws, "pheu_loc", "Thân phễu (tựa vòng / miệng bình)", m[m.Count - 1], "Vòng đỡ", m);
        var stem = Local(ws, PortOf(m[m.Count - 1], "Đuôi phễu lọc").transform.position);
        ws.SpawnHeld("coc250");
        ws.PlaceHeld(stem.x, stem.z);
        yield return FabTo("Lọc thường", "Bàn thí nghiệm 2", "Bàn thí nghiệm 3");

        // 6) Lọc hút → bàn thí nghiệm lắp ráp (vòi hút chân không)
        yield return OpenFab();
        m.Clear();
        var suction = NewAt(ws, "binh_hut_loc", 0.7f, 0f);
        yield return FabLap(ws, "buchner", "Nón cao su (lắp miệng bình hút)", suction, "Miệng bình hút lọc", m);
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", suction, "Nhánh hút chân không", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Vòi hút chân không (xưởng)"), "Vòi hút chân không (đầu nối ống cao su)");
        yield return FabTo("Lọc hút", "Bàn thí nghiệm lắp ráp", "Bàn thí nghiệm 3");

        // 7) Chiết → bàn thí nghiệm 5
        yield return OpenFab();
        m.Clear();
        var standF = NewAt(ws, "gia_sat", 0f, 0.1f);
        yield return FabLap(ws, "vong_sat", "Khớp nối vòng", standF, "Cọc giá ở 20 cm (trước)", m);
        yield return FabLap(ws, "pheu_chiet", "Bầu phễu (tựa vòng sắt)", m[m.Count - 1], "Vòng đỡ", m);
        var sstem = Local(ws, PortOf(m[m.Count - 1], "Đuôi phễu chiết").transform.position);
        ws.SpawnHeld("coc100");
        ws.PlaceHeld(sstem.x, sstem.z);
        yield return FabTo("Chiết", "Bàn thí nghiệm 5", "Bàn thí nghiệm 3");

        // 8) Cô cạn → bàn thí nghiệm 6
        yield return OpenFab();
        m.Clear();
        var tripod = NewAt(ws, "kieng", 0f, 0f);
        yield return FabLap(ws, "luoi", "Đáy lưới", tripod, "Vòng kiềng", m);
        yield return FabLap(ws, "bat_su", "Đáy bát sứ", m[m.Count - 1], "Mặt lưới", m);
        NewAt(ws, "den_con", 0f, 0f);
        yield return FabTo("Cô cạn", "Bàn thí nghiệm 6", "Bàn thí nghiệm 3");

        // 9) Giá ống nghiệm + 4 ống → bàn nhận dụng cụ (mặc định)
        yield return OpenFab();
        m.Clear();
        var rack = NewAt(ws, "gia_ong_nghiem", 0f, 0f);
        for (int i = 0; i < 4; i++) yield return FabLap(ws, "ong_nghiem", "Đáy ống nghiệm", rack, $"Lỗ giá {i + 1}", m);
        yield return FabTo("Giá ống nghiệm + 4 ống (phản ứng trong ống nghiệm)", "Bàn nhận dụng cụ");

        // ---------- ngoài phòng: cầm cả bộ, thao tác lẻ ----------
        var distParts = SetParts(distSet.id);
        var standW = distParts.First(p => p.libraryId == "gia_sat");
        var flaskW = distParts.First(p => p.libraryId == "binh_cau");
        var hoseB = distParts.First(p => p.libraryId == "ong_cao_su_dai" && p.librarySub == 1);
        var recvW = distParts.First(p => p.libraryId == "binh_non");
        Log($"[lap_rap] Trỏ bình cầu → cầm {Khkt.Assembly.ApparatusPart.WorldGrab(flaskW.Grab).name} (cả bộ: {Khkt.Assembly.ApparatusPart.WorldGrab(flaskW.Grab) == standW.Grab}); "
            + $"trỏ đầu ống cao su → cầm riêng={Khkt.Assembly.ApparatusPart.WorldGrab(hoseB.Grab) == hoseB.Grab}; trỏ bình hứng → cầm riêng={Khkt.Assembly.ApparatusPart.WorldGrab(recvW.Grab) == recvW.Grab}");
        int sub0 = standW.Subtree().Count;
        Vector3 home = standW.transform.position;
        hand.Pick(standW.Grab);
        yield return Frames(3);
        bool carried = standW.Subtree().Count == sub0;
        hand.ReturnHome();
        yield return Frames(3);
        Log($"[lap_rap] Cầm cả bộ chưng cất: giữ nguyên {standW.Subtree().Count}/{sub0} bộ phận khi cầm={carried}, Q trả về chỗ cũ lệch {Vector3.Distance(standW.transform.position, home) * 1000:0} mm");
        var tapPort = hoseB.ParentJoint.Connected;
        hand.Pick(hoseB.Grab);
        yield return Frames(2);
        bool off = hoseB.ParentJoint == null;
        hand.AttachHeld(PortOf(hoseB, "Đầu ống B"), tapPort);
        yield return Frames(2);
        Log($"[lap_rap] Đầu ống cao su: cầm lên thì tháo khỏi vòi={off}, lắp lại vào {tapPort.Part.name}={hoseB.ParentJoint != null && hoseB.ParentJoint.Connected == tapPort}");

        // ảnh ngoài phòng
        viewer.GoNear(new Vector3(15.25f, 1.15f, 11.3f), new Vector3(0.15f, 0.5f, -1f), 2.7f);
        yield return Frames(10);
        Shot("w03_ban_lap_rap_ba_bo");
        viewer.GoNear(new Vector3(16.1f, 1.2f, 14.3f), new Vector3(0.05f, 0.35f, -1f), 2.9f);
        yield return Frames(10);
        Shot("w04_tu_hut_dieu_che_nhiet_phan");
        viewer.GoNear(new Vector3(8.75f, 0.95f, 2.4f), new Vector3(1f, 0.7f, 0.25f), 1.7f);
        yield return Frames(10);
        Shot("w05_ban2_loc");
        viewer.GoNear(new Vector3(10.4f, 0.95f, 7.5f), new Vector3(1f, 0.9f, 0.15f), 3.0f);
        yield return Frames(10);
        Shot("w06_ban5_6_chiet_co_can");
        viewer.GoNear(new Vector3(17.5f, 1.0f, 11.25f), new Vector3(1f, 0.7f, -0.4f), 1.4f);
        yield return Frames(10);
        Shot("w07_ban_nhan_gia_ong_nghiem");

        // ---------- lấy bộ về sửa ----------
        flaskW.Container.Add(Solution.Water(50f));
        yield return OpenFab();
        string err = ws.Recall(ws.LiveSets().First(s => s.id == distSet.id));
        Log($"[lap_rap] Lấy bộ chưng cất (bình có 50 ml nước) về sửa: {err ?? "ĐƯỢC (sai: phải từ chối)"}");
        int rfParts = SetParts(refluxSet.id).Count, rfJoints = Joints(SetParts(refluxSet.id));
        err = ws.Recall(ws.LiveSets().First(s => s.id == refluxSet.id));
        yield return Frames(3);
        Log($"[lap_rap] Lấy bộ hồi lưu về sửa: lỗi={err ?? "không"}, bàn ảo {ws.Parts().Count}/{rfParts} bộ phận, khớp {Joints(ws.Parts())}/{rfJoints} (vòi xưởng nối lại), còn ngoài phòng {SetParts(refluxSet.id).Count}");
        ws.ResetView(0f, 28f, 1.6f, new Vector3(0.2f, 1.15f, 0f));
        yield return Frames(3);
        ShotCam(ws.workCamera, "w08_lay_bo_ve_sua");
        yield return FabTo("Hồi lưu (gia công lại sau khi sửa)", "Bàn thí nghiệm lắp ráp", "Bàn thí nghiệm 3");
        Log($"[lap_rap] Bộ đã gia công trong phòng: {string.Join(" | ", ws.LiveSets().Select(s => $"{s.name} @ {s.where}"))}");
        // bàn hết chỗ: bộ rất rộng (hai giá sắt cách nhau 2 m) và bộ chưng cất thứ hai
        yield return OpenFab();
        NewAt(ws, "gia_sat", -1.0f, 0.1f);
        NewAt(ws, "gia_sat", 1.0f, 0.1f);
        var wide = Khkt.Assembly.FabricationDestination.All.OrderBy(d => d.order).Select(d => $"{d.label}: {(ws.HasRoom(d) ? "đủ" : "hết")}").ToList();
        Log($"[lap_rap] Bộ rộng 2,2 m: {string.Join("; ", wide)}");
        {
            var dd = Dest("Bàn thí nghiệm lắp ráp");
            ws.DesignBounds(out var db);
            bool found = ws.FindSpot(dd, db, out var o, out var rr);
            Log($"[lap_rap] DEBUG thiết kế {db.size}, tâm {db.center}; chỗ tìm được={found} gốc {o}; bàn đích {dd.transform.position}, front {dd.FrontWorld}");
            foreach (var s in ws.LiveSets())
            {
                var ps = SetParts(s.id);
                var cols = ps.SelectMany(p => p.GetComponentsInChildren<Collider>()).ToList();
                Log($"[lap_rap] DEBUG {s.name} @ {s.where}: mốc {s.anchor.position}, va chạm {cols.Count} (bật {cols.Count(c => c.enabled)}), lớp {string.Join(",", cols.Select(c => c.gameObject.layer).Distinct())}");
            }
            var c = (o + rr * new Vector3(db.center.x, ws.tableTop, db.center.z)) + Vector3.up * ((db.max.y - ws.tableTop) / 2 + 0.012f);
            var hits = Physics.OverlapBox(c, new Vector3(db.size.x / 2, (db.max.y - ws.tableTop) / 2, db.size.z / 2), rr, ~0, QueryTriggerInteraction.Collide);
            Log($"[lap_rap] DEBUG va chạm tại chỗ đó: {string.Join(", ", hits.Select(h => h.name + (h.isTrigger ? "(trigger)" : "")))}");
        }
        ws.ClearAll();
        var s2 = NewAt(ws, "gia_sat", -0.45f, 0.08f);
        m.Clear();
        yield return FabLap(ws, "kep", "Khớp nối kẹp", s2, "Cọc giá ở 40 cm (trước)", m);
        yield return FabLap(ws, "binh_cau", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        yield return FabLap(ws, "dau_noi", "Khớp dưới (24/29)", m[m.Count - 1], "Cổ bình (khớp 24/29)", m);
        yield return FabLap(ws, "sinh_han", "Đầu vào hơi (24/29)", m[m.Count - 1], "Nhánh xiên (24/29)", m);
        ws.Ui.ShowDestinations();
        yield return Frames(2);
        Log($"[lap_rap] Bộ chưng cất thứ hai: bàn thí nghiệm lắp ráp (đã có 3 bộ) đủ chỗ={ws.HasRoom(Dest("Bàn thí nghiệm lắp ráp"))}, tủ hút 1 (đã có bộ điều chế khí) đủ chỗ={ws.HasRoom(Dest("Tủ hút khí độc 1"))}, tủ hút 3 (trống) đủ chỗ={ws.HasRoom(Dest("Tủ hút khí độc 3"))}");
        ShotCam(ws.workCamera, "w09_ban_het_cho");
        ws.Close();
    }

    // ---------- C3: mạng dòng chảy trên các bộ đã gia công ----------

    List<Khkt.Assembly.ApparatusPart> fabbed = new List<Khkt.Assembly.ApparatusPart>();
    Camera closeCam;
    Khkt.Assembly.FabricationDestination fabDest;

    /// <summary>Gia công bộ trên bàn ảo, ghi lại các bộ phận của bộ vừa ra (fabbed) và bàn đích.</summary>
    IEnumerator FabSet(string tag, params string[] dests)
    {
        var ws = Khkt.Assembly.FabricationWorkspace.Instance;
        var before = ws.LastSet;
        yield return FabTo(tag, dests);
        bool ok = ws.LastSet != null && ws.LastSet != before;
        fabbed = ok ? SetParts(ws.LastSet.id) : new List<Khkt.Assembly.ApparatusPart>();
        fabDest = ok ? Dest(ws.LastSet.where) : null;
    }

    Khkt.Assembly.ApparatusPart P(List<Khkt.Assembly.ApparatusPart> parts, string lib, int nth = 0) => parts.Where(p => p.libraryId == lib).Skip(nth).FirstOrDefault();

    static double MolOfStt(Khkt.Simulation.Chem.Mix m, int stt)
    {
        double s = 0;
        for (int i = 0; i < m.Count; i++) if (m.SpeciesAt(i).ChemStt == stt) s += m.MolAt(i);
        return s;
    }

    static double OrgMl(Khkt.Simulation.Chem.Mix m, int stt)
    {
        double ml = 0;
        for (int i = 0; i < m.Count; i++)
        {
            var d = m.SpeciesAt(i);
            if (d.ChemStt == stt) ml += m.MolAt(i) * Math.Max(1, d.MolarMass) / Math.Max(0.3, d.Density);
        }
        return ml;
    }

    static string Solids(LabContainer c)
    {
        var m = c.Total.Mix;
        var parts = new List<string>();
        for (int i = 0; i < m.Count; i++)
            if (m.SpeciesAt(i).IsSolidPhase && m.MolAt(i) > 1e-7) parts.Add($"{m.SpeciesAt(i).Id} {m.MolAt(i) * 1000:0.00} mmol");
        return parts.Count > 0 ? string.Join(", ", parts) : "không";
    }

    static double SolidMol(LabContainer c, string id) => c.Total.Mix.Get(id);

    static void Light(List<Khkt.Assembly.ApparatusPart> parts, bool on = true)
    {
        foreach (var p in parts.Where(p => p.libraryId == "den_con"))
        {
            var t = p.GetComponentInChildren<ToggleEffect>(true);
            if (t != null && t.isOn != on) t.Use();
        }
    }

    static string HeatInfo(List<Khkt.Assembly.ApparatusPart> parts, LabContainer c)
    {
        var lamp = parts.FirstOrDefault(p => p.libraryId == "den_con");
        var hs = lamp != null ? lamp.GetComponentInChildren<HeatSource>(true) : null;
        return $"đèn {(lamp != null ? "có" : "KHÔNG")}, nguồn nhiệt {(hs != null ? $"{hs.powerW:0} W" : "KHÔNG")}, bình cao hơn đế đèn {(lamp != null ? (c.transform.position.y - lamp.transform.position.y) * 100 : 0):0} cm";
    }

    void Look(Khkt.Assembly.ApparatusPart root, Khkt.Assembly.FabricationDestination d, float dist, float up = 0.55f)
    {
        var dir = (d != null ? d.FrontWorld : Vector3.back) + Vector3.up * up;
        viewer.GoNear(root.transform.position + Vector3.up * 0.25f, dir, dist);
    }

    IEnumerator FlowTest(bool fx)
    {
        var ws = Khkt.Assembly.FabricationWorkspace.Instance;
        var net = Khkt.Assembly.FluidNetwork.Instance;
        var cat = ChemicalCatalog.Instance;
        Log($"[dong_chay] Mạng dòng chảy: {net != null}, bảng nhiệt độ sôi nạp: {Khkt.Simulation.Chem.Volatility.Loaded}");

        // ---- 0) số liệu tĩnh: nhiệt độ sôi, hai lớp ----
        foreach (var (name, eth, water) in new[] { ("nước", 0f, 100f), ("rượu 10°", 10f, 90f), ("rượu 30°", 30f, 70f), ("rượu 50°", 50f, 50f), ("cồn 96°", 96f, 4f) })
        {
            var mix = eth > 0 ? cat.Make(cat.Get(468), eth).Mix : Khkt.Simulation.Chem.Mix.Empty;
            var L = Khkt.Simulation.Chem.Volatility.Analyze(mix, eth + water);
            var v = Khkt.Simulation.Chem.Volatility.Vaporize(L, 2000);
            double ethMl = 0;
            foreach (var (idx, mol) in v.org) ethMl += mol * 46.07 / 0.789;
            Log($"[dong_chay] Điểm sôi {name}: {Khkt.Simulation.Chem.Volatility.BubblePoint(L):0.0} °C, giọt cất đầu {ethMl / Math.Max(1e-9, v.liquidMl) * 100:0}% etanol (thể tích)");
        }
        {
            var sOil = cat.Make(cat.Get(550), 30);
            var (lo, up) = Khkt.Assembly.FluidNetwork.Layers(Solution.Of(70, sOil.Mix));
            Log($"[dong_chay] Phễu chiết dầu ăn 30 ml + nước 40 ml: lớp dưới {lo.volumeMl:0} ml (dầu {OrgMl(lo.Mix, 550):0} ml), lớp trên {up.volumeMl:0} ml (dầu {OrgMl(up.Mix, 550):0} ml)");
            var sCl = cat.Make(cat.Get(449), 20);
            var (lo2, up2) = Khkt.Assembly.FluidNetwork.Layers(Solution.Of(60, sCl.Mix));
            Log($"[dong_chay] Phễu chiết clorofom 20 ml + nước 40 ml: lớp dưới {lo2.volumeMl:0} ml (CHCl₃ {OrgMl(lo2.Mix, 449):0} ml), lớp trên {up2.volumeMl:0} ml (CHCl₃ {OrgMl(up2.Mix, 449):0} ml)");
        }

        viewer.GoTo(12);
        yield return Frames(6);
        var m = new List<Khkt.Assembly.ApparatusPart>();

        // ---- A) chưng cất rượu → bàn thí nghiệm lắp ráp (vòi 1) ----
        yield return OpenFab();
        var stand = NewAt(ws, "gia_sat", -0.45f, 0.08f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", stand, "Cọc giá ở 40 cm (trước)", m);
        yield return FabLap(ws, "binh_cau", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        var flask = m[m.Count - 1];
        yield return FabLap(ws, "dau_noi", "Khớp dưới (24/29)", flask, "Cổ bình (khớp 24/29)", m);
        var still = m[m.Count - 1];
        yield return FabLap(ws, "nhiet_ke", "Khớp nhiệt kế (14/23)", still, "Khớp trên (14/23)", m);
        yield return FabLap(ws, "sinh_han", "Đầu vào hơi (24/29)", still, "Nhánh xiên (24/29)", m);
        var cond = m[m.Count - 1];
        yield return FabLap(ws, "dau_noi_hung", "Khớp vào (24/29)", cond, "Đầu ra (24/29)", m);
        var adapter = m[m.Count - 1];
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", cond, "Vòi nước vào (dưới)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Vòi cấp nước (xưởng) 1"), "Vòi cấp nước 1 (đầu nối ống cao su)");
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", cond, "Vòi nước ra (trên)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Ống thoát nước (xưởng) 1"), "Ống thoát nước 1 (đầu nối ống cao su)");
        var tip = Local(ws, PortOf(adapter, "Đầu nhỏ giọt").transform.position);
        NewAt(ws, "binh_non", tip.x, tip.z);
        var fl = Local(ws, flask.transform.position);
        NewAt(ws, "den_con", fl.x, fl.z);
        yield return FabSet("A chưng cất", "Bàn thí nghiệm lắp ráp");
        var A = fabbed; var dA = fabDest;

        // ---- B1) đun hồi lưu có nước làm lạnh → bàn thí nghiệm lắp ráp (vòi 2) ----
        yield return OpenFab();
        m.Clear();
        var sB = NewAt(ws, "gia_sat", 0.2f, 0.08f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", sB, "Cọc giá ở 30 cm (trước)", m);
        yield return FabLap(ws, "binh_cau", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        var rf = m[m.Count - 1];
        yield return FabLap(ws, "sinh_han_bau", "Đầu ra (24/29)", rf, "Cổ bình (khớp 24/29)", m);
        var allihn = m[m.Count - 1];
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", allihn, "Vòi nước vào (dưới)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Vòi cấp nước (xưởng) 2"), "Vòi cấp nước 2 (đầu nối ống cao su)");
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", allihn, "Vòi nước ra (trên)", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Ống thoát nước (xưởng) 2"), "Ống thoát nước 2 (đầu nối ống cao su)");
        var rfl = Local(ws, rf.transform.position);
        NewAt(ws, "den_con", rfl.x, rfl.z);
        yield return FabSet("B1 hồi lưu có nước", "Bàn thí nghiệm lắp ráp");
        var B1 = fabbed; var dB1 = fabDest;

        // ---- B2) đun hồi lưu QUÊN nối nước → bàn thí nghiệm 3 ----
        yield return OpenFab();
        m.Clear();
        var sB2 = NewAt(ws, "gia_sat", 0f, 0.08f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", sB2, "Cọc giá ở 30 cm (trước)", m);
        yield return FabLap(ws, "binh_cau", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        var rf2 = m[m.Count - 1];
        yield return FabLap(ws, "sinh_han_bau", "Đầu ra (24/29)", rf2, "Cổ bình (khớp 24/29)", m);
        var rfl2 = Local(ws, rf2.transform.position);
        NewAt(ws, "den_con", rfl2.x, rfl2.z);
        yield return FabSet("B2 hồi lưu không nước", "Bàn thí nghiệm 3", "Bàn thí nghiệm 4");
        var B2 = fabbed;

        // ---- C) CaCO₃ + HCl (phễu nhỏ giọt) → CO₂ dẫn vào cốc nước vôi trong → tủ hút 1 ----
        yield return OpenFab();
        m.Clear();
        var sC = NewAt(ws, "gia_sat", -0.5f, 0.1f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", sC, "Cọc giá ở 30 cm (trước)", m);
        yield return FabLap(ws, "binh_cau_nhanh", "Cổ bình (chỗ kẹp)", m[m.Count - 1], "Má kẹp", m);
        var sa = m[m.Count - 1];
        yield return FabLap(ws, "nut1_26", "Thân nút", sa, "Miệng bình (đậy nút)", m);
        yield return FabLap(ws, "pheu_nho_giot", "Đuôi phễu (cắm qua nút)", m[m.Count - 1], "Lỗ nút", m);
        var armT = PortOf(sa, "Nhánh dẫn khí").transform;
        var flat = Vector3.ProjectOnPlane(armT.forward, Vector3.up).normalized;
        var bl = Local(ws, armT.position + flat * 0.3f);
        NewAt(ws, "coc100", bl.x, bl.z);
        yield return FabLap(ws, "ong_cao_su_ngan", "Đầu ống A", sa, "Nhánh dẫn khí", m);
        yield return FabSet("C điều chế CO₂ → nước vôi", "Tủ hút khí độc 1", "Tủ hút khí độc 2", "Bàn thí nghiệm 8");
        var C = fabbed; var dC = fabDest;

        // ---- D) nhiệt phân KMnO₄, thu O₂ bằng đẩy nước → tủ hút 2 ----
        yield return OpenFab();
        m.Clear();
        var sD = NewAt(ws, "gia_sat", -0.55f, 0.1f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", sD, "Cọc giá ở 25 cm (trước)", m);
        yield return FabLap(ws, "ong_nghiem", "Thân ống (kẹp nghiêng, miệng sang phải)", m[m.Count - 1], "Má kẹp", m);
        var tD = m[m.Count - 1];
        yield return FabLap(ws, "nut1_17", "Thân nút", tD, "Miệng ống nghiệm", m);
        yield return FabLap(ws, "ong_thang", "Đầu dưới", m[m.Count - 1], "Lỗ nút", m);
        yield return FabLap(ws, "ong_cao_su_ngan", "Đầu ống A", m[m.Count - 1], "Đầu trên", m);
        var hoseD = m[m.Count - 1];
        var mouthD = PortOf(tD, "Miệng ống nghiệm").transform.position;
        var trl = Local(ws, mouthD + Vector3.right * 0.38f);
        var troughD = NewAt(ws, "chau", trl.x, trl.z);
        var ltD = ws.SpawnHeld("ong_l");
        var holeD = PortOf(troughD, "Lỗ dẫn khí của kệ");
        ltD.transform.rotation = Quaternion.LookRotation(Vector3.up, holeD.transform.forward);
        bool ltOk = ws.AttachHeld(PortOf(ltD, "Đầu dưới (cắm qua nút)"), holeD);
        yield return HoseTo(ws, hoseD, ltD, "Đầu ngang");
        yield return FabLap(ws, "ong_nghiem_to", "Miệng ống nghiệm", troughD, "Kệ úp ống", m);
        var btD = Local(ws, PortOf(tD, "Đáy ống nghiệm").transform.position);
        NewAt(ws, "den_con", btD.x, btD.z);
        Log($"[dong_chay] Bộ nhiệt phân: ống L vào lỗ kệ={ltOk}; {tD.DescribeAssembly()}");
        yield return FabSet("D nhiệt phân KMnO₄ thu O₂", "Tủ hút khí độc 2", "Tủ hút khí độc 3", "Bàn thí nghiệm 8");
        var D = fabbed; var dD = fabDest;

        // ---- E) phễu chiết dầu – nước → bàn 5 ----
        yield return OpenFab();
        m.Clear();
        var sE = NewAt(ws, "gia_sat", 0f, 0.1f);
        yield return FabLap(ws, "vong_sat", "Khớp nối vòng", sE, "Cọc giá ở 20 cm (trước)", m);
        yield return FabLap(ws, "pheu_chiet", "Bầu phễu (tựa vòng sắt)", m[m.Count - 1], "Vòng đỡ", m);
        var sst = Local(ws, PortOf(m[m.Count - 1], "Đuôi phễu chiết").transform.position);
        NewAt(ws, "coc100", sst.x, sst.z);
        yield return FabSet("E chiết dầu – nước", "Bàn thí nghiệm 5", "Bàn thí nghiệm 7");
        var E = fabbed; var dE = fabDest;

        // ---- F) lọc kết tủa Cu(OH)₂ → bàn 2 ----
        yield return OpenFab();
        m.Clear();
        var sF = NewAt(ws, "gia_sat", 0f, 0.1f);
        yield return FabLap(ws, "vong_sat", "Khớp nối vòng", sF, "Cọc giá ở 15 cm (trước)", m);
        yield return FabLap(ws, "pheu_loc", "Thân phễu (tựa vòng / miệng bình)", m[m.Count - 1], "Vòng đỡ", m);
        var stemF = Local(ws, PortOf(m[m.Count - 1], "Đuôi phễu lọc").transform.position);
        NewAt(ws, "coc250", stemF.x, stemF.z);
        yield return FabSet("F lọc Cu(OH)₂", "Bàn thí nghiệm 2", "Bàn thí nghiệm 7");
        var F = fabbed; var dF = fabDest;

        // ---- G) lọc hút Buchner → bàn thí nghiệm lắp ráp (vòi hút chân không) ----
        yield return OpenFab();
        m.Clear();
        var suction = NewAt(ws, "binh_hut_loc", 0.7f, 0f);
        yield return FabLap(ws, "buchner", "Nón cao su (lắp miệng bình hút)", suction, "Miệng bình hút lọc", m);
        yield return FabLap(ws, "ong_cao_su_dai", "Đầu ống A", suction, "Nhánh hút chân không", m);
        yield return HoseTo(ws, m[m.Count - 1], Fix(ws, "Vòi hút chân không (xưởng)"), "Vòi hút chân không (đầu nối ống cao su)");
        yield return FabSet("G lọc hút", "Bàn thí nghiệm lắp ráp", "Bàn thí nghiệm 3");
        var G = fabbed;

        // ---- H) đun ống nghiệm đậy nút đặc (hệ kín) → bàn 4 ----
        yield return OpenFab();
        m.Clear();
        var sH = NewAt(ws, "gia_sat", -0.2f, 0.1f);
        yield return FabLap(ws, "kep", "Khớp nối kẹp", sH, "Cọc giá ở 25 cm (trước)", m);
        yield return FabLap(ws, "ong_nghiem", "Thân ống (kẹp nghiêng, miệng sang phải)", m[m.Count - 1], "Má kẹp", m);
        var tH = m[m.Count - 1];
        yield return FabLap(ws, "nut0_17", "Thân nút", tH, "Miệng ống nghiệm", m);
        var bH = Local(ws, PortOf(tH, "Đáy ống nghiệm").transform.position);
        NewAt(ws, "den_con", bH.x, bH.z);
        yield return FabSet("H đun hệ kín", "Bàn thí nghiệm 4", "Bàn thí nghiệm 7", "Bàn thí nghiệm 9");
        var H = fabbed; var dH = fabDest;

        // ---- I) cô cạn dung dịch NaCl → bàn 6 ----
        yield return OpenFab();
        m.Clear();
        var tripod = NewAt(ws, "kieng", 0f, 0f);
        yield return FabLap(ws, "luoi", "Đáy lưới", tripod, "Vòng kiềng", m);
        yield return FabLap(ws, "bat_su", "Đáy bát sứ", m[m.Count - 1], "Mặt lưới", m);
        NewAt(ws, "den_con", 0f, 0f);
        yield return FabSet("I cô cạn NaCl", "Bàn thí nghiệm 6", "Bàn thí nghiệm 9");
        var I = fabbed; var dI = fabDest;
        if (ws.IsOpen) ws.Close();
        yield return Frames(3);

        // ---- cho hóa chất ----
        var flaskA = P(A, "binh_cau").Container; var recvA = P(A, "binh_non").Container; var thermoA = P(A, "nhiet_ke");
        var flaskB1 = P(B1, "binh_cau").Container; var flaskB2 = P(B2, "binh_cau").Container;
        var flaskC = P(C, "binh_cau_nhanh").Container; var funnelC = P(C, "pheu_nho_giot"); var beakerC = P(C, "coc100").Container;
        var tubeD = P(D, "ong_nghiem").Container; var troughDw = P(D, "chau").Container; var jarD = P(D, "ong_nghiem_to").Container;
        var funnelE = P(E, "pheu_chiet"); var beakerE = P(E, "coc100").Container;
        var funnelF = P(F, "pheu_loc").Container; var beakerF = P(F, "coc250").Container;
        var buchG = P(G, "buchner").Container; var flaskG = P(G, "binh_hut_loc").Container;
        var tubeH = P(H, "ong_nghiem").Container; var stopperH = P(H, "nut0_17");
        var dishI = P(I, "bat_su").Container;

        flaskA.Add(cat.Make(cat.Get(468), 30)); flaskA.Add(Solution.Water(70));
        flaskB1.Add(cat.Make(cat.Get(468), 20)); flaskB1.Add(Solution.Water(30));
        flaskB2.Add(cat.Make(cat.Get(468), 20)); flaskB2.Add(Solution.Water(30));
        flaskC.Add(Solution.Water(10)); flaskC.Add(cat.Make(cat.Get(279), 5f));
        funnelC.Container.Add(cat.Make(cat.Get(98), 20, 1f));
        beakerC.Add(cat.Make(cat.Get(127), 60));
        tubeD.Add(cat.Make(cat.Get(696), 5f));
        troughDw.Add(Solution.Water(1500));
        funnelE.Container.Add(cat.Make(cat.Get(550), 30)); funnelE.Container.Add(Solution.Water(40));
        funnelF.Add(cat.Make(cat.Get(253), 20, 0.5f)); funnelF.Add(cat.Make(cat.Get(124), 25, 1f));
        buchG.Add(Solution.Water(40));
        tubeH.Add(Solution.Water(6));
        dishI.Add(cat.Make(cat.Get(149), 20, 1f));
        yield return Frames(3);
        double naI = dishI.Total.Mix.Get("Na+");
        Log($"[dong_chay] Phễu lọc sau khi đổ CuSO₄ + NaOH: chất rắn {Solids(funnelF)}");
        Log($"[dong_chay] Đun: A {HeatInfo(A, flaskA)} | B1 {HeatInfo(B1, flaskB1)} | D {HeatInfo(D, tubeD)} | H {HeatInfo(H, tubeH)} | I {HeatInfo(I, dishI)}");

        // đầu ống cao su của bộ C: thả vào cốc nước vôi trong, ngập dưới mặt nước
        var hoseCB = C.First(p => p.libraryId == "ong_cao_su_ngan" && p.librarySub == 1);
        var portCB = PortOf(hoseCB, "Đầu ống B");
        var rb = hoseCB.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        hoseCB.transform.rotation = Quaternion.FromToRotation(portCB.transform.forward, Vector3.down) * hoseCB.transform.rotation;
        hoseCB.transform.position += beakerC.transform.position + Vector3.up * 0.012f - portCB.transform.position;
        yield return Frames(2);

        // ---- chạy: đốt đèn, mở khóa ----
        float scale = 8f;
        Time.timeScale = scale;
        foreach (var set in new[] { A, B1, B2, D, H, I }) Light(set);
        funnelC.GetComponentInChildren<Khkt.Assembly.FlowValve>().Set(true);
        funnelE.GetComponentInChildren<Khkt.Assembly.FlowValve>().Set(true);
        double b1v0 = flaskB1.VolumeMl, b2v0 = flaskB2.VolumeMl, recvEthFirst = -1, firstThermo = -1, eWaterAt30 = -1, eOilAt30 = -1;
        float fRate = -1, gRate = -1, maxCaCO3 = 0, popAt = -1, firstDropT = -1, thermoMax = 0;
        var thermoTrace = new List<string>();
        float t0 = Time.time, next = 0;
        bool shotA = fx;  // bài hiệu ứng chụp cận cảnh riêng
        var tipA = PortOf(P(A, "dau_noi_hung"), "Đầu nhỏ giọt").transform;
        var condA = P(A, "sinh_han"); var condB1 = P(B1, "sinh_han_bau");
        var outE = PortOf(funnelE, "Đuôi phễu chiết").transform; var outF = PortOf(P(F, "pheu_loc"), "Đuôi phễu lọc").transform;
        var shots = new (float t, string name, Func<Vector3> at, float dist, Khkt.Assembly.FabricationDestination d)[]
        {
            (3f, "c4_01_chiet_dong_chay", () => outE.position, 0.42f, dE),
            (4f, "c4_02_loc_giot", () => outF.position, 0.38f, dF),
            (6f, "c4_03_pheu_nho_giot", () => flaskC.transform.position + Vector3.up * 0.12f, 0.45f, dC),
            (7f, "c4_04_suc_co2_nuoc_voi", () => beakerC.transform.position + Vector3.up * 0.03f, 0.3f, dC),
            (8f, "c4_06_chau_thu_o2", () => jarD.transform.position + Vector3.down * 0.06f, 0.4f, dD),
            (10f, "c4_05_he_kin_hoi", () => PortOf(P(H, "ong_nghiem"), "Miệng ống nghiệm").transform.position + Vector3.up * 0.08f, 0.7f, dH),
            (70f, "c4_07_co_can_soi_hoi", () => dishI.transform.position + Vector3.up * 0.03f, 0.4f, dI),
            (150f, "c4_08_dau_nho_giot", () => (tipA.position + recvA.mouth.position) / 2, 0.3f, dA),
            (151f, "c4_09_sinh_han_giot_ngung", () => condA.transform.position + (PortOf(condA, "Đầu vào hơi (24/29)").transform.position - condA.transform.position) * 0.5f, 0.55f, dA),
            (152f, "c4_10_nhiet_ke", () => P(A, "nhiet_ke").transform.position + Vector3.up * 0.04f, 0.28f, dA),
            (153f, "c4_11_binh_soi", () => flaskA.transform.position + Vector3.up * 0.04f, 0.38f, dA),
            (154f, "c4_12_hoi_luu", () => condB1.transform.position + Vector3.up * 0.1f, 0.6f, dB1),
        };
        int nextShot = 0;
        var fxs = Khkt.Assembly.FlowFx.Instance;
        while (Time.time - t0 < 480f)
        {
            float t = Time.time - t0;
            double rA = recvA.VolumeMl;
            float tr = net.Thermometers.TryGetValue(thermoA, out var tt) ? tt : 25f;
            thermoMax = Mathf.Max(thermoMax, tr);
            if (firstDropT < 0 && rA > 0.05) firstDropT = t;
            if (recvEthFirst < 0 && rA >= 5) { recvEthFirst = OrgMl(recvA.Total.Mix, 468) / rA * 100; firstThermo = tr; }
            maxCaCO3 = Mathf.Max(maxCaCO3, (float)(SolidMol(beakerC, "CaCO3") * 1000));
            if (eWaterAt30 < 0 && beakerE.VolumeMl >= 30) { eOilAt30 = OrgMl(beakerE.Total.Mix, 550); eWaterAt30 = beakerE.VolumeMl - eOilAt30; }
            if (fRate < 0 && t >= 10f) fRate = beakerF.VolumeMl / t;
            if (gRate < 0 && t >= 5f) gRate = flaskG.VolumeMl / t;
            if (popAt < 0 && net.LastPop != null) popAt = t;
            if (fx && nextShot < shots.Length && t >= shots[nextShot].t)
            {
                var sh = shots[nextShot++];
                Time.timeScale = 1f;
                var dir = ((sh.d != null ? sh.d.FrontWorld : Vector3.back) + Vector3.up * 0.45f).normalized;
                float tw0 = Time.time;
                while (Time.time - tw0 < 0.9f) yield return null;
                if (closeCam == null)
                {
                    closeCam = new GameObject("Camera cận cảnh (kiểm thử)").AddComponent<Camera>();
                    closeCam.CopyFrom(cam);
                    closeCam.enabled = false;
                    closeCam.nearClipPlane = 0.01f;
                    closeCam.fieldOfView = 38f;
                    UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(closeCam).renderPostProcessing = true;
                }
                var at = sh.at();
                closeCam.transform.position = at + dir * sh.dist;
                closeCam.transform.LookAt(at);
                ShotCam(closeCam, sh.name);
                Log($"[hieu_ung] {sh.name}: hạt đang bay {fxs.LiveParticles}, kênh {fxs.ActiveChannels}, CPU mạng {Khkt.Assembly.FluidNetwork.CostMs:0.00} ms/nhịp, hiệu ứng {Khkt.Assembly.FlowFx.CostMs:0.000} ms/khung [{net.TakeProfile()}], nhiệt kế {(net.Thermometers.TryGetValue(thermoA, out var th) ? th : 25f):0.0} °C (đầu cột y={fxs.ColumnTopLocal(thermoA) * 100:0.0} cm), ống úp còn nước {fxs.JarWaterFraction(jarD) * 100:0}%, thu {net.CollectedMl(jarD):0} ml");
                Time.timeScale = scale;
            }
            if (!shotA && rA >= 8)
            {
                shotA = true;
                Time.timeScale = 1f;
                Look(P(A, "gia_sat"), dA, 1.6f);
                yield return Frames(8);
                Shot("d01_chung_cat_dang_chay");
                Time.timeScale = scale;
            }
            if (t >= next)
            {
                next += t < 60f ? 10f : 30f;
                thermoTrace.Add($"{t:0}s:{tr:0}");
                Log($"[dong_chay] t={t:0}s | A: bình {flaskA.TemperatureC:0}°C (sôi {flaskA.BoilCapC:0}) {flaskA.VolumeMl:0.0} ml, nhiệt kế {tr:0.0}°C, hứng {rA:0.0} ml ({(rA > 0.01 ? OrgMl(recvA.Total.Mix, 468) / rA * 100 : 0):0}% etanol)"
                    + $" | B1 {flaskB1.VolumeMl:0.0} ml | B2 {flaskB2.VolumeMl:0.0} ml | C: phễu {funnelC.Container.VolumeMl:0.0} ml → {(net.OutflowTo.TryGetValue(funnelC.Container, out var oc) ? oc : "-")}, bình {flaskC.VolumeMl:0.0} ml Cl⁻ {flaskC.Total.Mix.Get("Cl-") * 1000:0.0} mmol, rắn {Solids(flaskC)}, cốc rắn {Solids(beakerC)}"
                    + $" | D: rắn {net.SolidTemp(tubeD):0}°C, thu {net.CollectedMl(jarD):0} ml {(net.CollectedGas.TryGetValue(jarD, out var gD) ? gD : "")}"
                    + $" | E: cốc {beakerE.VolumeMl:0.0} ml (dầu {OrgMl(beakerE.Total.Mix, 550):0.0}) | F: phễu {funnelF.VolumeMl:0.0} ml → {(net.OutflowTo.TryGetValue(funnelF, out var of) ? of : "-")}, nước lọc {beakerF.VolumeMl:0.0} ml | G: bình hút {flaskG.VolumeMl:0.0} ml"
                    + $" | H: {tubeH.TemperatureC:0}°C {(net.LastPop ?? "")} | I: bát {dishI.VolumeMl:0.0} ml {dishI.TemperatureC:0}°C");
            }
            yield return null;
        }
        Time.timeScale = 1f;
        foreach (var set in new[] { A, B1, B2, D, H, I }) Light(set, false);

        // ---- đánh giá ----
        double rAend = recvA.VolumeMl;
        Log($"[dong_chay] A chưng cất: giọt đầu sau {firstDropT:0} s; 5 ml đầu {recvEthFirst:0}% etanol (mong đợi > 50%), nhiệt kế lúc đó {firstThermo:0.0} °C (mong đợi 78–90), cao nhất {thermoMax:0.0} °C; "
            + $"tổng hứng {rAend:0.0} ml ({OrgMl(recvA.Total.Mix, 468) / Math.Max(0.01, rAend) * 100:0}% etanol), bình còn {flaskA.VolumeMl:0.0} ml ({OrgMl(flaskA.Total.Mix, 468) / Math.Max(0.01, flaskA.VolumeMl) * 100:0}% etanol); nhiệt kế theo thời gian {string.Join(" ", thermoTrace)}");
        Log($"[dong_chay] B hồi lưu: có nước {b1v0:0.0} → {flaskB1.VolumeMl:0.0} ml (mất {(b1v0 - flaskB1.VolumeMl) / b1v0 * 100:0.0}%, mong đợi < 2%); không nước {b2v0:0.0} → {flaskB2.VolumeMl:0.0} ml (mất {(b2v0 - flaskB2.VolumeMl) / b2v0 * 100:0}%)");
        Log($"[dong_chay] C CO₂: kết tủa CaCO₃ trong cốc nhiều nhất {maxCaCO3:0.00} mmol, cuối {SolidMol(beakerC, "CaCO3") * 1000:0.00} mmol; bình phản ứng còn rắn {Solids(flaskC)}; CO₂ thoát ra phòng {(net.Escaped.TryGetValue("CO2", out var eC) ? eC * 1000 : 0):0.0} mmol");
        Log($"[dong_chay] D KMnO₄: rắn còn {Solids(tubeD)}; O₂ thu được {net.CollectedMl(jarD):0} ml / ống {jarD.capacityMl:0} ml; O₂ thoát qua mặt nước {(net.Escaped.TryGetValue("O2", out var eO) ? eO * 1000 : 0):0.0} mmol");
        Log($"[dong_chay] E chiết: 30 ml đầu ra có nước {eWaterAt30:0.0} ml, dầu {eOilAt30:0.0} ml (mong đợi lớp nước dưới ra trước); cốc cuối {beakerE.VolumeMl:0.0} ml (dầu {OrgMl(beakerE.Total.Mix, 550):0.0}), phễu còn {funnelE.Container.VolumeMl:0.0} ml (dầu {OrgMl(funnelE.Container.Total.Mix, 550):0.0})");
        Log($"[dong_chay] F lọc: nước lọc {beakerF.VolumeMl:0.0} ml, rắn trong nước lọc: {Solids(beakerF)}, Cu²⁺ {beakerF.Total.Mix.Get("Cu2+") * 1000:0.000} mmol; trên giấy lọc: {Solids(funnelF)}");
        Log($"[dong_chay] G/F tốc độ: lọc hút {gRate:0.00} ml/s, lọc thường {fRate:0.00} ml/s (lọc hút nhanh hơn {gRate / Math.Max(0.01f, fRate):0.0} lần)");
        Log($"[dong_chay] H hệ kín: {(popAt >= 0 ? $"sau {popAt:0} s: {net.LastPop}" : "KHÔNG BẬT NÚT")}; nút còn trên ống={stopperH.ParentJoint != null}");
        Log($"[dong_chay] I cô cạn: bát còn {dishI.VolumeMl:0.00} ml, Na⁺ {naI * 1000:0.0} → {dishI.Total.Mix.Get("Na+") * 1000:0.0} mmol, rắn {Solids(dishI)}");
        Log($"[dong_chay] Nhật ký mạng dòng chảy ({net.Log.Count} dòng):\n  " + string.Join("\n  ", net.Log.Select(l => l.Length > 220 ? l.Substring(0, 220) : l)));

        // lọc hút khi tháo ống hút chân không: chậm lại
        var hoseGB = G.First(p => p.libraryId == "ong_cao_su_dai" && p.librarySub == 1);
        hoseGB.Detach();
        buchG.Add(Solution.Water(20));
        double g0 = flaskG.VolumeMl;
        float tg = Time.time;
        while (Time.time - tg < 8f) yield return null;
        Log($"[dong_chay] Lọc hút sau khi tháo ống chân không: {(flaskG.VolumeMl - g0) / 8:0.00} ml/s (trước {gRate:0.00} ml/s)");

        // ảnh
        Look(P(B1, "gia_sat"), dB1, 1.8f); yield return Frames(8); Shot("d02_ban_lap_rap_hoi_luu_loc_hut");
        Look(P(C, "gia_sat"), dC, 1.5f); yield return Frames(8); Shot("d03_co2_nuoc_voi");
        Look(P(D, "chau"), dD, 1.5f); yield return Frames(8); Shot("d04_nhiet_phan_thu_o2");
        Look(P(E, "gia_sat"), dE, 1.3f); yield return Frames(8); Shot("d05_chiet");
        Look(P(F, "gia_sat"), dF, 1.3f); yield return Frames(8); Shot("d06_loc");
        Look(P(H, "gia_sat"), dH, 1.2f); yield return Frames(8); Shot("d07_he_kin_bat_nut");
        Look(P(I, "kieng"), dI, 1.1f); yield return Frames(8); Shot("d08_co_can");
    }

    // ---------- Điều khiển bằng đầu qua webcam (HeadTracking + tools/head_tracker.py) ----------

    IEnumerator SendHead(System.Net.Sockets.UdpClient u, float seconds, bool ok, float yaw, float e)
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            string json = ok ? $"{{\"ok\":1,\"yaw\":{yaw.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"pitch\":0.8,\"e\":{e.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"t\":0}}" : "{\"ok\":0,\"t\":0}";
            var b = System.Text.Encoding.UTF8.GetBytes(json);
            u.Send(b, b.Length, "127.0.0.1", 9051);
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 0.04f) yield return null;  // 25 gói/giây như webcam
        }
    }

    IEnumerator HeadTest()
    {
        var ht = Khkt.Input.HeadTracking.Instance;
        var vcam = cam;
        Log($"[dau] Có HeadTracking: {ht != null}, trạng thái: {(ht != null ? ht.Status : "-")}, góc nhìn gốc {vcam.fieldOfView:0.0}°");
        if (ht == null) yield break;
        viewer.GoTo(5);
        yield return Frames(10);
        float fov0 = vcam.fieldOfView;
        Shot("dau_01_binh_thuong");

        // 1) gói giả qua UDP thật (không cần webcam)
        ht.Begin(launch: false);
        var u = new System.Net.Sockets.UdpClient();
        yield return SendHead(u, 2.4f, true, 2f, 0.10f);
        Log($"[dau] Sau 2,4 s nhìn thẳng (yaw 2°, e 0,10): đã căn={Khkt.Input.HeadTracking.Active}, trạng thái {ht.Status}, gói {ht.PacketsSeen}");

        float Y() => viewer.transform.eulerAngles.y;
        float y0 = Y();
        yield return SendHead(u, 1f, true, 2f + 40f, 0.10f);
        float dR = Mathf.DeltaAngle(y0, Y());
        y0 = Y();
        yield return SendHead(u, 1f, true, 2f - 40f, 0.10f);
        float dL = Mathf.DeltaAngle(y0, Y());
        y0 = Y();
        yield return SendHead(u, 1f, true, 2f + 20f, 0.10f);
        float dDead = Mathf.DeltaAngle(y0, Y());
        y0 = Y();
        yield return SendHead(u, 1f, true, 2f + 55f, 0.10f);
        float dFull = Mathf.DeltaAngle(y0, Y());
        Log($"[dau] Quay đầu 1 s: phải 40° → góc nhìn {dR:+0.0;-0.0}°, trái 40° → {dL:+0.0;-0.0}°, phải 20° (vùng chết 30°) → {dDead:+0.0;-0.0}°, phải 55° → {dFull:+0.0;-0.0}°");

        yield return SendHead(u, 1.2f, true, 2f, 0.13f);
        float fovIn = vcam.fieldOfView;
        Shot("dau_02_ghe_gan_phong_to");
        yield return SendHead(u, 1.2f, true, 2f, 0.08f);
        float fovOut = vcam.fieldOfView;
        Shot("dau_03_nga_xa_nhin_rong");
        Log($"[dau] Ghé gần (e ×1,3): góc nhìn {fov0:0.0}° → {fovIn:0.0}° (phóng ×{fov0 / fovIn:0.00}); ngả xa (e ×0,8): {fovOut:0.0}° (×{fov0 / fovOut:0.00})");

        y0 = Y();
        yield return SendHead(u, 0.3f, true, 2f + 45f, 0.10f);
        yield return SendHead(u, 1.2f, false, 0, 0);
        float dLost = Mathf.DeltaAngle(y0, Y());
        float tl = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - tl < 0.6f) yield return null;
        float yLost = Y();
        while (Time.realtimeSinceStartup - tl < 1.2f) yield return null;
        Log($"[dau] Mất mặt sau khi quay 45°: tổng xoay {dLost:+0.0;-0.0}° (chỉ trong 0,3 s + 0,4 s chờ), sau đó đứng yên={Mathf.Abs(Mathf.DeltaAngle(yLost, Y())) < 0.1f}, zoom về {Khkt.Input.HeadTracking.Zoom:0.00}, góc nhìn {vcam.fieldOfView:0.0}°");
        u.Close();
        ht.Stop();
        yield return Frames(3);

        // 2) Unity tự chạy chương trình nhận diện thật (Python + webcam)
        Environment.SetEnvironmentVariable("KHKT_HEAD_TRACKER", @"D:\KHKT 11 du phong\tools\head_tracker.py");
        int seen0 = ht.PacketsSeen;
        ht.Begin(launch: true);
        float tp = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - tp < 12f && ht.PacketsSeen - seen0 < 50) yield return null;
        Log($"[dau] Tự chạy head_tracker.py: tiến trình chạy={ht.ProcessRunning}, nhận {ht.PacketsSeen - seen0} gói sau {Time.realtimeSinceStartup - tp:0.0} s, thấy mặt={ht.FaceVisible}, trạng thái {ht.Status}, lỗi: {ht.LastError ?? "không"}");
        ht.Toggle();  // như bấm F8 lần nữa
        float tk = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - tk < 2f) yield return null;
        bool alive = System.Diagnostics.Process.GetProcessesByName("python").Any(p => { try { return p.StartTime > DateTime.Now.AddSeconds(-30); } catch { return false; } });
        Log($"[dau] Bấm F8 tắt: trạng thái {ht.Status}, còn tiến trình python mới chạy={alive}");
    }

    // ---------- Máy phân tích: màn hình vận hành UV-Vis, AAS ----------

    IEnumerator WaitReal(float seconds)
    {
        float t = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t < seconds) yield return null;
    }

    IEnumerator WaitGame(float seconds)
    {
        float t = Time.time;
        while (Time.time - t < seconds) yield return null;
    }

    Khkt.Demo.Instruments.InstrumentScreen OpenMachine(AnalyzerInstrument a)
    {
        a.Use();
        var scr = Khkt.Demo.Instruments.InstrumentScreen.Instance;
        scr.ScreenCamera.targetTexture = rt;  // bố cục theo đúng cỡ ảnh chụp 1600 × 900
        return scr;
    }

    void PutSample(string label, AnalyzerInstrument a)
    {
        var s = FindObjectsByType<LabContainer>().First(c => c.label == label);
        hand.Pick(s.GetComponent<Grabbable>());
        hand.PlaceInSocket(a.slot);
    }

    IEnumerator WaitFor(Func<bool> done, float maxGame = 60f)
    {
        float t = Time.time;
        while (!done() && Time.time - t < maxGame) yield return null;
    }

    void ClearSlot(AnalyzerInstrument a)
    {
        if (a.slot.Occupant == null) return;
        hand.Pick(a.slot.Occupant.GetComponent<Grabbable>());
        hand.ReturnHome();
    }

    /// <summary>Đợt 2: FTIR, GC-MS, HPLC, chuẩn độ điện thế, làm như học sinh với ống mẫu thật trong phòng.</summary>
    IEnumerator MachineTest2()
    {
        viewer.GoTo(7);
        float tz = Time.realtimeSinceStartup;
        while (FindObjectsByType<AnalyzerInstrument>().Length < 6 && Time.realtimeSinceStartup - tz < 20f) yield return null;
        yield return Frames(5);
        var an = FindObjectsByType<AnalyzerInstrument>();
        AnalyzerInstrument A(AnalyzerInstrument.Kind k) => an.First(x => x.kind == k);
        var data = Khkt.Demo.Instruments.InstrumentData.Instance;
        Log($"[may2] Thư viện: FTIR {data?.Ftir.Entries.Count} chất, GC-MS {data?.Gc.Entries.Count} chất, HPLC {data?.Hplc.Methods.Count} phương pháp; mẫu trong phòng: {string.Join(", ", FindObjectsByType<LabContainer>().Where(c => c.label != null && c.label.StartsWith("Mẫu")).Select(c => c.label).OrderBy(x => x))}");

        // ===== Chuẩn độ điện thế: giấm ăn (axit axetic 0,83 M) =====
        var tA = A(AnalyzerInstrument.Kind.AutoTitrator);
        PutSample("Mẫu giấm ăn", tA);
        yield return Frames(2);
        var scr = OpenMachine(tA);
        var ti = (Khkt.Demo.Instruments.TitratorProgram)scr.Program;
        scr.Guided = true;
        yield return Frames(2);
        Log($"[may2] Mở màn hình chuẩn độ: mở={Khkt.Demo.Instruments.InstrumentScreen.IsOpen}, camera lab bật={cam.enabled}, mẫu={ti.SampleName}");
        ti.StartSample();
        Log($"[may2] Chuẩn độ khi chưa hiệu chuẩn (Hướng dẫn): \"{scr.LastMessage}\"");
        for (int i = 0; i < 3; i++) ti.CalibrateNext();
        Log($"[may2] Hiệu chuẩn: độ dốc {ti.Slope * 100:0.0}%, lệch {ti.Offset:+0.0} mV; \"{scr.LastMessage}\"");
        ti.Prime();
        Time.timeScale = 4f;
        ti.StartTiter();
        yield return WaitFor(() => !ti.Running);
        Log($"[may2] KHP: \"{scr.LastMessage}\" (NaOH thật 0,0982 M)");
        ti.SetAliquot(2);
        ti.StartSample();
        yield return WaitFor(() => !ti.Running);
        Time.timeScale = 1f;
        Log($"[may2] Giấm 2 mL: {ti.Endpoints.Count} điểm tương đương, V = {(ti.Endpoints.Count > 0 ? ti.Endpoints[0].v : 0):0.000} mL (lý thuyết {0.83 * 2 / 0.0982:0.000}); C = {ti.LastResultEqPerL:0.0000} mol/L (thật 0,8300, sai {(ti.LastResultEqPerL / 0.83 - 1) * 100:+0.00;-0.00}%); Gran {ti.GranFit.veq:0.000} mL R² {ti.GranFit.r2:0.0000}; {ti.Curve.Count} điểm đo");
        Log($"[may2] Bước đã xong: {string.Join(", ", scr.DoneSteps)}");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "t01_chuan_do_giam");
        ti.SetPlot(1); yield return Frames(3); scr.Refresh(); ShotCam(scr.ScreenCamera, "t02_dao_ham");
        ti.SetPlot(2); yield return Frames(3); scr.Refresh(); ShotCam(scr.ScreenCamera, "t03_gran");
        scr.Close();
        yield return Frames(2);
        // Thực hành: không khuấy, không xác định hệ số
        scr = OpenMachine(tA);
        ti = (Khkt.Demo.Instruments.TitratorProgram)scr.Program;
        scr.Guided = false;
        ti.SetStirring(false);
        ti.SetAliquot(2);
        Time.timeScale = 4f;
        ti.StartSample();
        yield return WaitFor(() => !ti.Running);
        Time.timeScale = 1f;
        Log($"[may2] Thực hành (không hiệu chuẩn, không khuấy, không hệ số): V = {(ti.Endpoints.Count > 0 ? ti.Endpoints[0].v : 0):0.000} mL, C = {ti.LastResultEqPerL:0.0000} mol/L (sai {(ti.LastResultEqPerL / 0.83 - 1) * 100:+0.0;-0.0}%); \"{scr.LastMessage}\"");
        scr.Close();
        yield return Frames(2);

        // ===== GC-MS: rượu nghi pha metanol =====
        var gA = A(AnalyzerInstrument.Kind.GcMs);
        PutSample("Mẫu rượu nghi pha metanol", gA);
        yield return Frames(2);
        scr = OpenMachine(gA);
        var gc = (Khkt.Demo.Instruments.GcMsProgram)scr.Program;
        scr.Guided = true;
        gc.StartMs();
        Log($"[may2] GC-MS bật MS khi chưa có khí mang: \"{scr.LastMessage}\"");
        gc.OpenGas();
        gc.StartMs();
        Time.timeScale = 4f;
        yield return WaitFor(() => gc.Vacuum >= 4f);
        gc.Tune();
        yield return WaitFor(() => gc.Tuned);
        gc.SetMethod(80, 10, 50, 1.0, 29);
        gc.SetDilution(10);
        gc.UseSample();
        gc.StartRun();
        yield return WaitFor(() => !gc.Running);
        Log($"[may2] Lò đầu 80 °C: {gc.Identified.Count} pic ({string.Join(", ", gc.Identified.Select(x => $"{x.hit.Name} {x.p.Tr:0.000}"))}); \"{scr.LastMessage}\"");
        gc.SetMethod(40, 10, 50, 1.0, 29);
        for (int i = 0; i < 5; i++)
        {
            gc.SelectStd(i);
            gc.StartRun();
            yield return WaitFor(() => !gc.Running);
        }
        gc.UseSample();
        gc.StartRun();
        yield return WaitFor(() => !gc.Running);
        Time.timeScale = 1f;
        Log($"[may2] GC-MS rượu ×10: {string.Join("; ", gc.Identified.Select(x => $"{x.p.Tr:0.00} phút RI {x.ri:0} → {x.hit.Name} (khớp {x.match:0}, RI NIST {x.hit.RI:0})"))}");
        double per = gc.MeOHmgL / (gc.EthanolPct / 100);
        Log($"[may2] Định lượng: metanol {gc.MeOHmgL:0} mg/L (thật {0.05 * 32040:0}, sai {(gc.MeOHmgL / (0.05 * 32040) - 1) * 100:+0.0;-0.0}%), etanol {gc.EthanolPct:0.0}% (thật {5.2 * 46.07 / 789 * 100:0.0}%); {per:0} mg/L cồn 100° = {per / 100:0} lần giới hạn");
        Log($"[may2] Bước đã xong: {string.Join(", ", scr.DoneSteps)}");
        yield return Frames(3);
        gc.SetPlot(0); scr.Refresh(); yield return Frames(2); ShotCam(scr.ScreenCamera, "g01_gc_tic");
        gc.Select(0); yield return Frames(3); scr.Refresh(); ShotCam(scr.ScreenCamera, "g02_gc_pho_khoi_metanol");
        gc.SetPlot(2); yield return Frames(3); scr.Refresh(); ShotCam(scr.ScreenCamera, "g03_gc_duong_chuan");
        scr.ShowPrinciple(); yield return Frames(2); scr.Refresh(); ShotCam(scr.ScreenCamera, "g04_gc_nguyen_ly");
        scr.Close();
        yield return Frames(2);
        // Thực hành: không pha loãng, trễ dung môi dài, quét từ m/z 10
        scr = OpenMachine(gA);
        gc = (Khkt.Demo.Instruments.GcMsProgram)scr.Program;
        scr.Guided = false;
        gc.OpenGas(); gc.StartMs();
        Time.timeScale = 4f;
        yield return WaitFor(() => gc.Vacuum >= 4f);
        gc.Tune();
        yield return WaitFor(() => gc.Tuned);
        gc.SetMethod(40, 10, 10, 1.5, 10);
        gc.SetDilution(1);
        gc.StartRun();
        yield return WaitFor(() => !gc.Running);
        Time.timeScale = 1f;
        Log($"[may2] Thực hành GC-MS (×1, chia dòng 10:1, trễ 1,5 phút, m/z 10): {gc.Identified.Count} pic; quá tải={gc.Peaks.Any(p => p.Overloaded)}; thông báo cuối \"{scr.LastMessage}\"");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "g05_gc_thuc_hanh_loi");
        scr.Close();
        yield return Frames(2);

        // ===== HPLC: nước cam (axit xitric 0,03 M, glucozơ 0,25 M, saccarozơ 0,1 M) =====
        var hA = A(AnalyzerInstrument.Kind.Hplc);
        PutSample("Mẫu nước cam", hA);
        yield return Frames(2);
        scr = OpenMachine(hA);
        var lc = (Khkt.Demo.Instruments.HplcProgram)scr.Program;
        scr.Guided = true;
        lc.SelectMethod("axit_huu_co");
        lc.StartPump();
        lc.Purge();
        Log($"[may2] HPLC bơm: {lc.PressureBar:0} bar; \"{scr.LastMessage}\"");
        Time.timeScale = 4f;
        yield return WaitFor(() => lc.Equil >= 5f);
        lc.FilterSample();
        lc.SetDilution(10);
        lc.SelectAnalyte("citric");
        for (int i = 0; i < 5; i++) { lc.SelectStd(i); lc.StartRun(); yield return WaitFor(() => !lc.Running); }
        lc.UseSample();
        lc.StartRun();
        yield return WaitFor(() => !lc.Running);
        Time.timeScale = 1f;
        Log($"[may2] HPLC axit hữu cơ: pic {string.Join(", ", lc.Peaks.Select(p => $"{p.C.Name} {p.Tr:0.00} phút"))}; axit xitric {lc.Found:0} mg/L (thật {0.03 * 192120:0}, sai {(lc.Found / (0.03 * 192120) - 1) * 100:+0.0;-0.0}%)");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "h01_hplc_axit_huu_co");
        lc.SetPh(7.0);
        lc.SkipEquilibration();
        Time.timeScale = 4f;
        lc.StartRun();
        yield return WaitFor(() => !lc.Running);
        Time.timeScale = 1f;
        Log($"[may2] HPLC pha động pH 7: axit xitric ra ở {lc.Peaks.FirstOrDefault().Tr:0.00} phút (t₀ 2,70 phút)");
        lc.TurnOff();
        lc.SelectMethod("duong");
        lc.StartPump(); lc.Purge(); lc.SkipEquilibration();
        lc.SetDilution(10);
        lc.SelectAnalyte("saccarozo");
        Time.timeScale = 4f;
        for (int i = 0; i < 5; i++) { lc.SelectStd(i); lc.StartRun(); yield return WaitFor(() => !lc.Running); }
        lc.UseSample();
        lc.StartRun();
        yield return WaitFor(() => !lc.Running);
        Time.timeScale = 1f;
        Log($"[may2] HPLC đường (RID): pic {string.Join(", ", lc.Peaks.Select(p => $"{p.C.Name} {p.Tr:0.00} phút"))}; saccarozơ {lc.Found:0.0} g/L (thật {0.1 * 342.3:0.0}, sai {(lc.Found / (0.1 * 342.3) - 1) * 100:+0.0;-0.0}%)");
        Log($"[may2] Bước đã xong: {string.Join(", ", scr.DoneSteps)}");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "h02_hplc_duong");
        lc.SetPlot(1); yield return Frames(3); scr.Refresh(); ShotCam(scr.ScreenCamera, "h03_hplc_duong_chuan");
        lc.TurnOff();
        scr.Close();
        yield return Frames(2);
        // nước ngọt có ga: axit benzoic
        ClearSlot(hA);
        PutSample("Mẫu nước ngọt có ga", hA);
        yield return Frames(2);
        scr = OpenMachine(hA);
        lc = (Khkt.Demo.Instruments.HplcProgram)scr.Program;
        scr.Guided = false;
        lc.SelectMethod("bao_quan");
        lc.StartPump(); lc.Purge(); lc.SkipEquilibration();
        lc.SelectAnalyte("benzoic");
        Time.timeScale = 4f;
        for (int i = 0; i < 5; i++) { lc.SelectStd(i); lc.StartRun(); yield return WaitFor(() => !lc.Running); }
        lc.UseSample();
        lc.StartRun();
        yield return WaitFor(() => !lc.Running);
        Time.timeScale = 1f;
        Log($"[may2] HPLC nước ngọt: pic {string.Join(", ", lc.Peaks.Select(p => $"{p.C.Name} {p.Tr:0.00} phút"))}; axit benzoic {lc.Found:0} mg/L (thật {0.0012 * 122120:0}, sai {(lc.Found / (0.0012 * 122120) - 1) * 100:+0.0;-0.0}%)");
        lc.SetFlow(2.0);
        Log($"[may2] HPLC metanol 40%, 2 mL/phút: {lc.PressureBar:0} bar (giới hạn 400)");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "h04_hplc_benzoic");
        scr.Close();
        yield return Frames(2);

        // ===== FTIR: dầu chuối (isoamyl axetat 0,4 M + etanol 0,5 M trong nước) =====
        var fA = A(AnalyzerInstrument.Kind.Ftir);
        PutSample("Mẫu dầu chuối", fA);
        yield return Frames(2);
        scr = OpenMachine(fA);
        var ir = (Khkt.Demo.Instruments.FtirProgram)scr.Program;
        scr.Guided = true;
        ir.TogglePower();
        Time.timeScale = 4f;
        yield return WaitFor(() => ir.Ready);
        ir.PutSample();
        Log($"[may2] FTIR nhỏ mẫu khi chưa đo nền (Hướng dẫn): \"{scr.LastMessage}\"");
        ir.CleanCrystal();
        ir.MeasureBackground();
        ir.PutSample();
        ir.Measure();
        yield return WaitFor(() => !ir.Scanning);
        yield return Frames(2);
        Time.timeScale = 1f;
        scr.Refresh(); ShotCam(scr.ScreenCamera, "f01_ftir_pho_tho");
        ir.Analyze();
        Log($"[may2] FTIR dầu chuối: trừ nước hệ số {ir.WaterScale:0.000}; tách: {string.Join(", ", ir.Unmixed.Select(u => $"{u.Label} {u.Amount * 100:0.00}%"))} (thật: isoamyl axetat {0.4 * 130.19 / 876 * 100:0.00}%, etanol {0.5 * 46.07 / 789 * 100:0.00}%); thư viện: {string.Join("; ", ir.Hits.Select(h => $"{h.p.Label} {h.hqi:0}"))}");
        Log($"[may2] FTIR gán dải: {string.Join("; ", ir.Bands.Select(b => $"{b.nu:0} {b.group}"))}");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "f02_ftir_tru_nuoc_tach");
        // định lượng etanol trong rượu
        for (int i = 0; i < 5; i++)
        {
            ir.CleanCrystal();
            ir.SelectStd(i);
            Time.timeScale = 4f;
            ir.Measure();
            yield return WaitFor(() => !ir.Scanning);
            ir.RecordStd();
        }
        ir.CleanCrystal();
        scr.Close();
        ClearSlot(fA);
        PutSample("Mẫu rượu", fA);
        yield return Frames(2);
        scr = OpenMachine(fA);
        // màn hình mới: chương trình mới (đường chuẩn trong chương trình cũ), nên chạy lại chuẩn trong cùng phiên
        ir = (Khkt.Demo.Instruments.FtirProgram)scr.Program;
        scr.Guided = false;
        ir.TogglePower();
        yield return WaitFor(() => ir.Ready);
        ir.CleanCrystal(); ir.MeasureBackground();
        for (int i = 0; i < 5; i++) { ir.CleanCrystal(); ir.SelectStd(i); ir.Measure(); yield return WaitFor(() => !ir.Scanning); ir.RecordStd(); }
        ir.CleanCrystal(); ir.PutSample(); ir.Measure();
        yield return WaitFor(() => !ir.Scanning);
        Time.timeScale = 1f;
        ir.Analyze();
        Log($"[may2] FTIR rượu: etanol {ir.EthanolPct:0.0}% thể tích (thật {5.2 * 46.07 / 789 * 100:0.0}%), đường chuẩn {ir.Cal.Count} điểm R² {Khkt.Simulation.Chem.UvVisLibrary.Linear(ir.Cal.Select(c => (c.c, c.a)).ToList()).R2:0.0000}");
        ir.SetPlot(3); yield return Frames(3); scr.Refresh(); ShotCam(scr.ScreenCamera, "f03_ftir_duong_chuan_etanol");
        scr.Close();
        // Thực hành: không đo nền, quét 1 lần
        scr = OpenMachine(fA);
        ir = (Khkt.Demo.Instruments.FtirProgram)scr.Program;
        scr.Guided = false;
        ir.TogglePower();
        Time.timeScale = 4f;
        yield return WaitFor(() => ir.Ready);
        ir.SetScans(1);
        ir.PutSample(); ir.Measure();
        yield return WaitFor(() => !ir.Scanning);
        Time.timeScale = 1f;
        Log($"[may2] FTIR thực hành (không đo nền, quét 1 lần): \"{scr.LastMessage}\"");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "f04_ftir_khong_nen");
        ir.SetPlot(1); scr.Refresh(); yield return Frames(2);
        scr.Close();
        Log($"[may2] Đóng: mở={Khkt.Demo.Instruments.InstrumentScreen.IsOpen}, camera lab bật={cam.enabled}, UiCapture={DesktopViewer.UiCapture}");
    }

    IEnumerator MachineTest()
    {
        viewer.GoTo(7);  // phòng máy phân tích được tải theo khu: tới nơi rồi mới tìm máy
        float tz = Time.realtimeSinceStartup;
        while (FindObjectsByType<AnalyzerInstrument>().Length < 6 && Time.realtimeSinceStartup - tz < 20f) yield return null;
        yield return Frames(5);
        var analyzers = FindObjectsByType<AnalyzerInstrument>();
        var uvA = analyzers.First(x => x.kind == AnalyzerInstrument.Kind.UvVis);
        var aasA = analyzers.First(x => x.kind == AnalyzerInstrument.Kind.Aas);
        Log($"[may] Dữ liệu máy: {Khkt.Demo.Instruments.InstrumentData.Instance != null}, thư viện UV-Vis {Khkt.Demo.Instruments.InstrumentData.Instance?.Uv.Entries.Count} chất, AAS {Khkt.Demo.Instruments.InstrumentData.Instance?.Aas.Elements.Count} nguyên tố");

        // ===== UV-Vis, chế độ Hướng dẫn: mẫu nước thải xi mạ crom (Cr(VI) 0,02 M) =====
        PutSample("Mẫu nước thải xi mạ crom", uvA);
        yield return Frames(2);
        var scr = OpenMachine(uvA);
        var uv = (Khkt.Demo.Instruments.UvVisProgram)scr.Program;
        scr.Guided = true;
        yield return Frames(3);
        Log($"[may] Sau đặt cuvet: mẫu ở ổ UV-Vis = {uvA.slot.Occupant?.name}");
        Log($"[may] Mở màn hình UV-Vis: mở={Khkt.Demo.Instruments.InstrumentScreen.IsOpen}, camera phòng lab bật={cam.enabled}, UiCapture={DesktopViewer.UiCapture}, mẫu ở ổ={uv.SampleName}");
        scr.Refresh(); ShotCam(scr.ScreenCamera, "m01_uv_luc_mo");
        uv.AutoZero();
        Log($"[may] Đo nền khi máy chưa bật: \"{scr.LastMessage}\"");
        uv.TogglePower();
        Time.timeScale = 4f;
        yield return WaitGame(3.5f);
        uv.AutoZero();
        Log($"[may] Hướng dẫn: đo nền khi đèn chưa nóng: \"{scr.LastMessage}\"");
        yield return WaitGame(6.5f);
        Time.timeScale = 1f;
        uv.SelectMode(1);
        uv.SelectCuvette("thach_anh");
        uv.SetSource(Khkt.Demo.Instruments.UvVisProgram.Src.Blank);
        uv.AutoZero();
        Log($"[may] Đo nền với mẫu trắng: nền={uv.Baseline}, bước đã xong: {string.Join(", ", scr.DoneSteps)}");
        uv.SetSource(Khkt.Demo.Instruments.UvVisProgram.Src.Sample);
        uv.StartScan();
        yield return WaitGame(3f);
        double aMax1 = uv.Spectrum.Max(p => p.a);
        Log($"[may] Quét mẫu chưa pha loãng: A lớn nhất {aMax1:0.00} (bão hòa do ánh sáng lạc), thông báo: \"{scr.LastMessage}\"");
        uv.SetDilution(100);
        uv.StartScan();
        yield return WaitGame(3f);
        var pk = uv.Spectrum.Where(p => p.l >= 300).OrderByDescending(p => p.a).First();
        Log($"[may] Quét sau pha loãng ×100: đỉnh {pk.l:0} nm A {pk.a:0.000} (NIST: 350 nm, A = 1570 × 2×10⁻⁴ = 0,314); thông báo \"{scr.LastMessage}\"");
        uv.SetLambdaMax();
        uv.UnmixNow();
        var cr = uv.Unmixed?.FirstOrDefault(u => u.e.Id == "CrVI");
        Log($"[may] Tách hỗn hợp: {string.Join(", ", (uv.Unmixed ?? new List<(Khkt.Simulation.Chem.UvVisLibrary.Entry, double)>()).Select(u => $"{u.Item1.Id} {u.Item2:E2} M"))}, rms {uv.UnmixRms:0.0000}; Cr(VI) mẫu gốc = {(cr.HasValue ? cr.Value.c * 100 : 0):0.0000} M (thật 0,0200)");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "m02_uv_quet_pho_tach");
        // đường chuẩn Cr(VI) ở 350 nm
        uv.SelectMode(2);
        uv.SelectAnalyte("CrVI");
        uv.SetLambda(350);
        for (int i = 0; i < 5; i++) { uv.SelectStd(i); uv.RecordStandard(); }
        uv.QuantifySample();
        Log($"[may] Đường chuẩn Cr(VI) 350 nm: {uv.CalPoints.Count} điểm, A = {uv.CalFit.Slope:0}·c + {uv.CalFit.Intercept:0.0000}, R² {uv.CalFit.R2:0.0000}; mẫu = {uv.SampleConc:0.00000} M (thật 0,02000, sai {(uv.SampleConc / 0.02 - 1) * 100:+0.0;-0.0}%); thông báo \"{scr.LastMessage}\"");
        Log($"[may] Bước đã xong (Hướng dẫn): {string.Join(", ", scr.DoneSteps)}");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "m03_uv_duong_chuan");
        scr.ShowPrinciple();
        yield return Frames(2);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "m04_uv_nguyen_ly");
        scr.Close();
        yield return Frames(2);
        Log($"[may] Đóng: mở={Khkt.Demo.Instruments.InstrumentScreen.IsOpen}, camera phòng lab bật={cam.enabled}, UiCapture={DesktopViewer.UiCapture}");

        // ===== UV-Vis: hỗn hợp KMnO4 + Cr(VI) trong cuvet, tách bằng phổ =====
        var chrome = FindObjectsByType<LabContainer>().First(c => c.label == "Mẫu nước thải xi mạ crom");
        hand.Pick(chrome.GetComponent<Grabbable>());  // lấy ống mẫu crom ra khỏi ổ máy, trả về giá
        hand.ReturnHome();
        yield return Frames(2);
        var cuv = FindObjectsByType<LabContainer>().First(c => c.name.StartsWith("Cuvet") && c.VolumeMl < 0.1f);
        cuv.Add(Solution.Of(3f, Khkt.Simulation.Chem.Mix.Of(("MnO4-", 3e-3 * 2e-4), ("CrVI", 3e-3 * 3e-4), ("Cl-", 3e-3 * 0.01))));
        yield return Frames(2);
        hand.Pick(cuv.GetComponent<Grabbable>());
        hand.PlaceInSocket(uvA.slot);
        yield return Frames(2);
        scr = OpenMachine(uvA);
        uv = (Khkt.Demo.Instruments.UvVisProgram)scr.Program;
        scr.Guided = false;
        uv.TogglePower();
        Time.timeScale = 4f;
        yield return WaitGame(10f);
        Time.timeScale = 1f;
        uv.SelectCuvette("nhua");
        uv.SetSource(Khkt.Demo.Instruments.UvVisProgram.Src.Blank);
        uv.SetLambda(525);
        uv.MeasureNow();
        Log($"[may] Thực hành: quên đo nền, đo mẫu trắng ở 525 nm: A = {uv.LastA:0.000} (lẽ ra 0)");
        uv.AutoZero();
        uv.SetSource(Khkt.Demo.Instruments.UvVisProgram.Src.Sample);
        uv.StartScan();
        yield return WaitGame(3f);
        Log($"[may] Thực hành: cuvet nhựa quét từ 300 nm: \"{scr.LastMessage}\"");
        uv.UnmixNow();
        Log($"[may] Hỗn hợp KMnO₄ 2×10⁻⁴ + Cr(VI) 3×10⁻⁴ M: tách được {string.Join(", ", uv.Unmixed.Select(u => $"{u.e.Id} {u.c:E2} M"))}, rms {uv.UnmixRms:0.0000}");
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "m05_uv_hon_hop");
        scr.Close();
        yield return Frames(2);

        // ===== AAS, chế độ Hướng dẫn: nước thải mạ điện (Cu 254, Zn 196, Ni 117 mg/L) =====
        if (hand.Held != null) hand.ReturnHome();
        PutSample("Mẫu nước thải mạ điện", aasA);
        yield return Frames(2);
        scr = OpenMachine(aasA);
        var aas = (Khkt.Demo.Instruments.AasProgram)scr.Program;
        scr.Guided = true;
        aas.TogglePower();
        yield return WaitGame(2.5f);
        aas.OpenAcetylene();
        Log($"[may] AAS Hướng dẫn: mở axetilen trước không khí: \"{scr.LastMessage}\" → axetilen mở={aas.Acetylene}");
        aas.SetFan(true);
        aas.SelectLamp("Cu");
        Time.timeScale = 4f;
        yield return WaitGame(4.5f);
        aas.OpenAir();
        aas.OpenAcetylene();
        aas.Ignite();
        Log($"[may] Châm lửa: lửa={aas.Flame}; bước đã xong: {string.Join(", ", scr.DoneSteps)}");
        var found = new Dictionary<string, double>();
        foreach (var (el, dil, truth) in new[] { ("Cu", 100, 254.2), ("Zn", 1000, 196.1), ("Ni", 100, 117.4) })
        {
            if (el != "Cu") { aas.SelectLamp(el); yield return WaitGame(4.5f); }
            aas.SetSource(Khkt.Demo.Instruments.AasProgram.Src.Blank);
            aas.SetAspirate(true);
            yield return WaitGame(2f);
            aas.AutoZero();
            var std = aas.CalPoints;
            int nStd = 4;
            for (int i = 0; i < nStd; i++)
            {
                aas.SelectStd(i);
                yield return WaitGame(2f);
                aas.RecordStandard();
            }
            if (el == "Cu")
            {
                aas.SetSource(Khkt.Demo.Instruments.AasProgram.Src.Sample);
                aas.SetDilution(1);
                yield return WaitGame(2f);
                aas.MeasureSample();
                Log($"[may] Cu không pha loãng: \"{scr.LastMessage}\"");
            }
            aas.SetSource(Khkt.Demo.Instruments.AasProgram.Src.Sample);
            aas.SetDilution(dil);
            yield return WaitGame(2.5f);
            aas.MeasureSample();
            var r = aas.Results[el];
            found[el] = r.orig;
            Log($"[may] {el}: đường chuẩn R² {aas.CalFit.R2:0.0000}; đo {r.meas:0.000} mg/L × {r.dil} = {r.orig:0.0} mg/L (thật {truth:0.0}, sai {(r.orig / truth - 1) * 100:+0.0;-0.0}%) {r.note}");
            if (el == "Cu")
            {
                Time.timeScale = 1f;
                yield return WaitReal(0.5f);
                scr.Refresh(); ShotCam(scr.ScreenCamera, "m06_aas_cu");
                Time.timeScale = 4f;
            }
        }
        aas.SelectLamp("Al");
        Log($"[may] Chọn đèn Al: \"{scr.LastMessage}\"");
        aas.CloseAir();
        Log($"[may] Tắt: đóng không khí trước: \"{scr.LastMessage}\" (lửa={aas.Flame})");
        aas.CloseAcetylene();
        aas.CloseAir();
        Log($"[may] Tắt đúng thứ tự: lửa={aas.Flame}, không khí={aas.Air}; bước đã xong: {string.Join(", ", scr.DoneSteps)}");
        Time.timeScale = 1f;
        yield return Frames(3);
        scr.Refresh(); ShotCam(scr.ScreenCamera, "m07_aas_ket_qua");
        scr.Close();
        yield return Frames(2);

        // AAS chế độ Thực hành: mở sai thứ tự
        scr = OpenMachine(aasA);
        aas = (Khkt.Demo.Instruments.AasProgram)scr.Program;
        scr.Guided = false;
        aas.TogglePower();
        yield return WaitGame(2.5f);
        aas.OpenAcetylene();
        Log($"[may] AAS Thực hành: mở axetilen trước không khí: \"{scr.LastMessage}\"");
        scr.Close();
        yield return Frames(2);
        Log($"[may] Đóng hết: camera phòng lab bật={cam.enabled}, UiCapture={DesktopViewer.UiCapture}");
    }

    Vector3 LabStationPos() => viewer.Stations.First(s => s.order == 12).transform.position;
    float LabStationTopY() => 0.9f;
    // ---------- Tải khu cộng dồn + lưu trạng thái ----------

    string LoadedZones()
    {
        var zs = Khkt.Rendering.ZoneStreamer.Instance;
        return string.Join(", ", Enumerable.Range(0, Khkt.Rendering.ZoneMap.Zones.Length).Where(z => zs.IsLoaded(z)).Select(z => Khkt.Rendering.ZoneMap.Zones[z].name));
    }

    /// <summary>Đi bộ thật bằng CharacterController qua các điểm (bị cánh cửa đóng chặn lại), ghi lúc băng qua mỗi cửa.</summary>
    IEnumerator WalkPath(Vector3[] pts, string tag)
    {
        var zs = Khkt.Rendering.ZoneStreamer.Instance;
        var cc = viewer.GetComponent<CharacterController>();
        cc.enabled = false;
        viewer.transform.position = pts[0];
        cc.enabled = true;
        yield return Frames(3);
        const float speed = 1.4f;
        float t0 = Time.realtimeSinceStartup, maxStuck = 0;
        var prev = new System.Collections.Generic.Dictionary<Khkt.Rendering.AutoDoor, float>();
        bool shot = false;
        for (int i = 1; i < pts.Length; i++)
        {
            float stuck = 0;
            while (true)
            {
                Vector3 d = pts[i] - viewer.transform.position;
                d.y = 0;
                if (d.magnitude < 0.15f) break;
                viewer.LookAt(viewer.transform.position + d.normalized * 3f + Vector3.up * 1.45f);
                Vector3 before = viewer.transform.position;
                cc.Move(d.normalized * speed * Time.deltaTime);
                if ((viewer.transform.position - before).magnitude < speed * Time.deltaTime * 0.3f) stuck += Time.deltaTime; else stuck = 0;
                maxStuck = Mathf.Max(maxStuck, stuck);
                if (stuck > 12f) { Log($"[khu] {tag}: KẸT ở {viewer.transform.position}, đang tải [{LoadedZones()}]"); yield break; }
                foreach (var door in Khkt.Rendering.AutoDoor.All)
                {
                    float s = Vector3.Dot(viewer.transform.position - door.Center, door.Normal);
                    float lat = Mathf.Abs(Vector3.Dot(viewer.transform.position - door.Center, door.transform.right));
                    if (!shot && lat < 1.5f && Mathf.Abs(s) < 2.2f && door.Openness > 0.35f && door.Openness < 0.75f)
                    {
                        shot = true;
                        Shot("k01_cua_dang_mo_" + tag);
                    }
                    if (prev.TryGetValue(door, out float ps) && ps * s < 0 && lat < door.width)
                        Log($"[khu] {tag}: qua {door.doorName}, cửa mở {door.Openness:0.00}, khu {Khkt.Rendering.ZoneMap.Name(door.zoneA)} tải={zs.IsReady(door.zoneA)}, " +
                            $"khu {Khkt.Rendering.ZoneMap.Name(door.zoneB)} tải={zs.IsReady(door.zoneB)}");
                    prev[door] = s;
                }
                yield return null;
            }
        }
        Log($"[khu] {tag}: đi xong sau {Time.realtimeSinceStartup - t0:0.0} s, đứng chờ cửa lâu nhất {maxStuck:0.0} s, đang tải [{LoadedZones()}]");
    }

    IEnumerator ZoneTest()
    {
        var zs = Khkt.Rendering.ZoneStreamer.Instance;
        if (zs == null) { Log("[khu] KHÔNG có ZoneStreamer trong cảnh"); yield break; }
        Log($"[khu] Lúc đầu: đang tải [{LoadedZones()}], số cảnh đang mở {UnityEngine.SceneManagement.SceneManager.sceneCount}");
        viewer.GoTo(2);
        yield return Frames(5);
        yield return new WaitForSecondsRealtime(zs.unloadDelayS + 1.5f);
        Log($"[khu] Ở bàn 2 (phòng lab): đang tải [{LoadedZones()}], số cảnh {UnityEngine.SceneManagement.SceneManager.sceneCount}");

        // 1) Đổi trạng thái một ống nghiệm ở phòng lab
        var tube = FindObjectsByType<LabContainer>().Where(c => c.label.StartsWith("Ống nghiệm ")).OrderBy(c => c.label).First();
        yield return PourBottle(253, tube, 2f);
        yield return PourBottle(124, tube, 2f);
        yield return new WaitForSeconds(0.5f);
        string before = tube.Total.Describe(4);
        Vector3 posBefore = tube.transform.position;
        Log($"[khu] {tube.label} trước khi rời phòng: {before}");

        // 2) Đi bộ: phòng lab -> cửa -> hành lang -> phòng máy
        yield return WalkPath(new[] { new Vector3(4.6f, 0, 4.0f), new Vector3(2.4f, 0, 1.6f), new Vector3(2.4f, 0, -1.1f), new Vector3(6.7f, 0, -1.1f) }, "lab_hanh_lang");
        yield return WalkPath(new[] { new Vector3(6.7f, 0, -1.1f), new Vector3(6.7f, 0, -3.3f), new Vector3(6.75f, 0, -4.3f) }, "hanh_lang_phong_may");
        var labRoot = Khkt.Rendering.ZoneRoot.Of(Khkt.Rendering.ZoneMap.Lab);
        float tw = Time.realtimeSinceStartup;  // chờ robot đi theo ra khỏi phòng lab và phòng lab được gỡ thật
        while (!labRoot.Parked && Time.realtimeSinceStartup - tw < 30f) yield return null;
        Log($"[khu] Phòng lab được gỡ sau {Time.realtimeSinceStartup - tw:0.0} s (robot ở {Khkt.Rendering.ZoneMap.Name(Khkt.Rendering.ZoneMap.At(LabRobot.Instance.transform.position))})");
        Log($"[khu] Trong phòng máy: đang tải [{LoadedZones()}], đồ phòng lab ngủ={labRoot.Parked}, ống nghiệm đang hiện={tube.gameObject.activeInHierarchy}");
        viewer.LookAt(new Vector3(6.75f, 1.0f, -8.4f));
        yield return Frames(5);
        Shot("k02_phong_may_da_go_lab");

        // 3) Quay lại phòng lab bằng dịch chuyển (màn mờ, tải lại)
        viewer.GoTo(2);
        yield return Frames(3);
        string after = tube.Total.Describe(4);
        bool same = after == before && (tube.transform.position - posBefore).sqrMagnitude < 1e-6f;
        Log($"[khu] Quay lại bàn 2: đang tải [{LoadedZones()}], lần tải {zs.Loads}, lần gỡ {zs.Unloads}, lần tải gần nhất {zs.LastLoadMs:0} ms");
        Log($"[khu] {tube.label} sau khi quay lại: {after} · GIỮ NGUYÊN={same}");
        viewer.LookAt(tube.transform.position + Vector3.up * 0.05f);
        yield return Frames(5);
        Shot("k03_ong_nghiem_giu_nguyen");

        // 4) Mang một lọ từ phòng hóa chất sang phòng lab
        viewer.GoTo(6);
        yield return Frames(3);
        var bottle = Khkt.Rendering.ZoneRoot.FindAll<ChemicalBottle>().First(b => b.stt == 159);
        hand.Pick(bottle.GetComponent<Grabbable>());
        yield return Frames(2);
        viewer.GoTo(1);
        yield return Frames(3);
        var storeRoot = Khkt.Rendering.ZoneRoot.Of(Khkt.Rendering.ZoneMap.Store);
        tw = Time.realtimeSinceStartup;
        while (!storeRoot.Parked && Time.realtimeSinceStartup - tw < 30f) yield return null;
        Log($"[khu] Cầm {bottle.DisplayName} từ kho sang lab: kho đang tải={zs.IsLoaded(Khkt.Rendering.ZoneMap.Store)}, lọ còn hiện={bottle.gameObject.activeInHierarchy}, gốc hiện tại \"{bottle.transform.parent.name}\"");
        hand.PlaceOnSurface(viewer.transform.position + viewer.transform.forward * 0.55f + Vector3.up * 0.8f);
        yield return Frames(2);

        // 5) Robot đi lấy hóa chất ở phòng hóa chất (đang gỡ) trong khi người xem đứng ở phòng lab
        viewer.GoTo(2);
        yield return Frames(3);
        yield return new WaitForSecondsRealtime(zs.unloadDelayS + 1f);
        var robot = LabRobot.Instance;
        Log($"[khu] Trước khi robot đi: đang tải [{LoadedZones()}], robot ở {Khkt.Rendering.ZoneMap.Name(Khkt.Rendering.ZoneMap.At(robot.transform.position))}");
        robot.BringChemical(98);
        float tr = Time.time;
        bool sawStore = false;
        Time.timeScale = 3f;
        while (robot.Busy && Time.time - tr < 90f)
        {
            sawStore |= zs.IsLoaded(Khkt.Rendering.ZoneMap.Store);
            yield return null;
        }
        Time.timeScale = 1f;
        var got = Khkt.Rendering.ZoneRoot.FindAll<ChemicalBottle>().Where(b => b.stt == 98).OrderBy(b => Vector3.Distance(b.transform.position, cam.transform.position)).First();
        Log($"[khu] Robot lấy {got.DisplayName}: còn bận={robot.Busy}, kho được tải trong lúc robot đi={sawStore}, lọ cách mắt {Vector3.Distance(got.transform.position, cam.transform.position):0.00} m, trạng thái {robot.StatusText}");
        yield return new WaitForSecondsRealtime(zs.unloadDelayS + 2f);
        Log($"[khu] Cuối bài: đang tải [{LoadedZones()}], tổng lần tải {zs.Loads}, lần gỡ {zs.Unloads}");
    }
    static IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
        // tải khu cộng dồn: dịch chuyển tới khu chưa tải thì chờ tải xong (tối đa 15 s)
        float t0 = Time.realtimeSinceStartup;
        while (!Khkt.Rendering.ZoneStreamer.Settled && Time.realtimeSinceStartup - t0 < 15f) yield return null;
    }

    IEnumerator LevelEye(MeniscusReading reading)
    {
        float baseEye = viewer.seated ? viewer.seatedEyeHeight : viewer.standingEyeHeight;
        viewer.EyeOffset = reading.MeniscusPosition.y - viewer.transform.position.y - baseEye;
        for (int i = 0; i < 40; i++)
        {
            viewer.LookAt(reading.MeniscusPosition);
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator Watchdog()
    {
        yield return new WaitForSecondsRealtime(900f);
        Log("HẾT GIỜ");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(2);
#endif
    }

    void Shot(string name)
    {
        Canvas.ForceUpdateCanvases();
        if (loupe != null && loupe.Active) loupe.GetComponent<Camera>().Render();
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
    }

    static void Log(string s) => Debug.Log("[KHKT-PLAY] " + s);
}

