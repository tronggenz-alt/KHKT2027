using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Khkt.Demo;
using Khkt.Environment;
using Unity.Profiling;
using UnityEngine;

// Chỉ có trong bản sao project: đo khung hình bản .exe tại từng khu (không giới hạn FPS, tắt vsync), ghi CSV.
public class KhktBenchmark : MonoBehaviour
{
    const float Warm = 1.5f, Measure = 4f;

    // Bảng F9 trong bản .exe thật: shader, số liệu, và chi phí khung hình khi mở / đóng
    IEnumerator F9(DesktopViewer viewer, string dir, string dataFile)
    {
        var log = new StringBuilder();
        var ht = Khkt.Input.HeadTracking.Instance;
        var tv = Khkt.Input.TrackingViewer.Instance;
        viewer.GoTo(5);
        yield return new WaitForSecondsRealtime(2f);
        var scenes = new Dictionary<string, string[]>();
        foreach (var l in File.ReadAllLines(dataFile)) { var p = l.Split('\t'); scenes[p[0]] = p; }
        var u = new System.Net.Sockets.UdpClient();
        IEnumerator Send(string[] s, float sec)
        {
            var a = Encoding.UTF8.GetBytes(s[1]);
            var b = Encoding.UTF8.GetBytes(s[2]);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < sec)
            {
                u.Send(a, a.Length, "127.0.0.1", 9051);
                u.Send(b, b.Length, "127.0.0.1", 9053);
                yield return null;
            }
        }
        IEnumerator Fps(string name, string[] s)
        {
            int n = 0;
            float t0 = Time.realtimeSinceStartup;
            var a = Encoding.UTF8.GetBytes(s[1]);
            var b = Encoding.UTF8.GetBytes(s[2]);
            while (Time.realtimeSinceStartup - t0 < 5f)
            {
                u.Send(a, a.Length, "127.0.0.1", 9051);
                u.Send(b, b.Length, "127.0.0.1", 9053);
                n++;
                yield return null;
            }
            log.AppendLine($"{name}: {n / 5f:0.0} FPS, {5000f / n:0.00} ms");
        }
        ht.Begin(launch: false);
        yield return Send(scenes["faraday"], 2.5f);
        for (int r = 0; r < 2; r++)
        {
            yield return Fps("dong", scenes["faraday"]);
            tv.SetOpen(true);
            yield return Send(scenes["faraday"], 1f);
            yield return Fps("mo", scenes["faraday"]);
            tv.SetOpen(false);
            yield return null;
        }
        tv.SetOpen(true);
        foreach (var name in new[] { "dalton", "tay_phai_ben_trai_anh" })
        {
            yield return Send(scenes[name], 1f);
            log.AppendLine($"--- {name}: goi={tv.LandmarkPackets}");
            log.AppendLine(tv.Readout);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "f9_exe_" + name + ".png"));
            yield return Send(scenes[name], 0.3f);
        }
        tv.SetOpen(false);
        u.Close();
        File.WriteAllText(Path.Combine(dir, "f9_exe.txt"), log.ToString());
    }

    IEnumerator Start()
    {
        if (Arg("-khkt-demo") != null) yield break;  // chi choi thu, khong do
        string outFile = Arg("-khkt-bench") ?? Path.Combine(Application.persistentDataPath, "bench.csv");
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        var intro = FindAnyObjectByType<IntroScreen>();
        yield return null;
        if (intro != null) intro.Close();
        var viewer = FindAnyObjectByType<DesktopViewer>();
        if (Arg("-khkt-f9") != null) { yield return F9(viewer, Arg("-khkt-f9"), Arg("-khkt-f9-data")); Application.Quit(); yield break; }
        if (Arg("-khkt-phan-tich") != null) { yield return Sweep(viewer, Arg("-khkt-phan-tich")); Application.Quit(); yield break; }
        var sb = new StringBuilder();
        sb.AppendLine($"# {SystemInfo.processorType} | {SystemInfo.processorCount} luồng | RAM {SystemInfo.systemMemorySize} MB | {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType}) | {Screen.width}x{Screen.height} | chất lượng {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        sb.AppendLine("khu,fps_tb,fps_1pct_thap,ms_tb,ms_p95,batches,setpass,tam_giac_k,cpu_ms,gpu_ms,ms_max,so_giat");
        var all = new List<float>();
        var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        var setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        var spots = new List<(string name, Action go)>();
        foreach (var st in viewer.Stations.OrderBy(s => s.order).Take(9)) { var s = st; spots.Add((s.displayName, () => viewer.GoTo(s))); }
        spots.Add(("Đi bộ dọc phòng lab", () => viewer.GoTo(9)));
        foreach (var st in viewer.Stations.Where(s => s.order >= 20 && s.order <= 31).OrderBy(s => s.order))
        {
            var s = st;
            spots.Add((s.displayName, () => viewer.GoTo(s)));
        }
        spots.Add(("Đi bộ dọc hành lang tầng 2 (3,6 m)", () => viewer.GoTo(27)));
        spots.Add(("Đi bộ dọc hành lang tầng 1", () => viewer.GoTo(9)));
        int standard = spots.Count;
        // khu chế tạo: bàn thí nghiệm lắp ráp trống / có 38 bộ phận rời như cách cũ / đang mở giao diện lắp ráp
        var loose = new List<GameObject>();
        var ws = Khkt.Assembly.FabricationWorkspace.Instance;
        var lib = Khkt.Assembly.PartLibrary.Instance;
        var benchStation = viewer.Stations.FirstOrDefault(s => s.order == 13);
        var fabStation = viewer.Stations.FirstOrDefault(s => s.order == 12);
        if (benchStation != null) spots.Add(("Bàn thí nghiệm lắp ráp (trống)", () => viewer.GoTo(benchStation)));
        if (benchStation != null && lib != null)
            spots.Add(("Bàn lắp ráp có 38 bộ phận rời (cách cũ)", () =>
            {
                var zone = Khkt.Rendering.ZoneRoot.Of(Khkt.Rendering.ZoneMap.Lab);
                int i = 0;
                foreach (var e in lib.entries)
                {
                    var pos = new Vector3(14.2f + (i % 10) * 0.21f, 0.9f, 11.0f + (i / 10) * 0.2f);
                    foreach (var (p, _, _) in lib.Spawn(e, zone.transform, pos, 0f)) loose.Add(p.gameObject);
                    i++;
                }
                viewer.GoTo(benchStation);
            }));
        if (fabStation != null && ws != null)
            spots.Add(("Giao diện chế tạo (bàn ảo 14 bộ phận)", () =>
            {
                foreach (var g in loose) if (g != null) Destroy(g);
                viewer.GoTo(fabStation);
                ws.Open();
                string[] ids = { "gia_sat", "kep", "binh_cau", "dau_noi", "nhiet_ke", "sinh_han", "dau_noi_hung", "binh_non", "den_con", "ong_cao_su_dai", "vong_sat", "pheu_loc", "coc250", "bat_su" };
                for (int i = 0; i < ids.Length; i++)
                {
                    ws.SpawnHeld(ids[i]);
                    ws.PlaceHeld(-1.0f + (i % 7) * 0.32f, -0.3f + (i / 7) * 0.45f);
                }
            }));
        int spotIndex = 0;
        foreach (var (name, go) in spots)
        {
            bool extra = spotIndex++ >= standard;
            go();
            yield return null;
            float tw = Time.realtimeSinceStartup;
            while (!Khkt.Rendering.ZoneStreamer.Settled && Time.realtimeSinceStartup - tw < 15f) yield return null;
            yield return new WaitForSecondsRealtime(Warm);
            var ft = new List<float>();
            long b = 0, sp = 0, tr = 0; int n = 0;
            double cpu = 0, gpu = 0; int nt = 0;
            var timings = new FrameTiming[1];
            float t0 = Time.realtimeSinceStartup;
            bool walk = name.StartsWith("Đi bộ");
            while (Time.realtimeSinceStartup - t0 < Measure)
            {
                if (walk)  // lướt camera dọc phòng, xoay đều: đo cả lúc cảnh thay đổi liên tục
                {
                    float k = (Time.realtimeSinceStartup - t0) / Measure;
                    if (name.Contains("tầng 1"))
                    {
                        viewer.transform.position = Vector3.Lerp(new Vector3(1.5f, 0, -1.1f), new Vector3(26.0f, 0, -1.1f), k);
                        viewer.LookAt(viewer.transform.position + Vector3.right * 3f + Vector3.up * 1.5f);
                    }
                    else if (name.Contains("tầng 2"))
                    {
                        viewer.transform.position = Vector3.Lerp(new Vector3(1.5f, 3.8f, -1.8f), new Vector3(26.0f, 3.8f, -1.8f), k);
                        viewer.LookAt(viewer.transform.position + Vector3.right * 3f);
                    }
                    else
                    {
                        viewer.transform.position = Vector3.Lerp(new Vector3(18.5f, 0, 13f), new Vector3(2f, 0, 1.5f), k);
                        viewer.LookAt(viewer.transform.position + Quaternion.Euler(0, 200 + 140 * k, 0) * Vector3.forward * 3 + Vector3.up * 1.2f);
                    }
                }
                yield return null;
                ft.Add(Time.unscaledDeltaTime * 1000f);
                if (batches.Valid) b += batches.LastValue;
                if (setpass.Valid) sp += setpass.LastValue;
                if (tris.Valid) tr += tris.LastValue;
                n++;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                {
                    cpu += timings[0].cpuMainThreadFrameTime; gpu += timings[0].gpuFrameTime; nt++;
                }
            }
            if (!extra) all.AddRange(ft);  // TỔNG chỉ tính các điểm đo cũ để so được với lần đo trước
            ft.Sort();
            float trung = ft[ft.Count / 2];
            int giat = ft.Count(x => x > Mathf.Max(33.3f, trung * 2f));  // khung hình giật: quá 33 ms và gấp đôi trung vị
            float avg = ft.Average(), p95 = ft[(int)(ft.Count * 0.95f)], low = ft.Skip((int)(ft.Count * 0.99f)).DefaultIfEmpty(ft.Last()).Average();
            sb.AppendLine($"{name},{1000f / avg:0.0},{1000f / low:0.0},{avg:0.00},{p95:0.00},{(batches.Valid ? b / n : -1)},{(setpass.Valid ? sp / n : -1)},{(tris.Valid ? tr / n / 1000 : -1)},{(nt > 0 ? cpu / nt : -1):0.00},{(nt > 0 ? gpu / nt : -1):0.00},{ft.Last():0.0},{giat}");
        }
        all.Sort();
        sb.AppendLine($"TỔNG,{1000f / all.Average():0.0},{1000f / all.Skip((int)(all.Count * 0.99f)).Average():0.0},{all.Average():0.00},{all[(int)(all.Count * 0.95f)]:0.00},,,,,,{all.Last():0.0},{all.Count(x => x > 33.3f)}");
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Application.Quit();
    }

    // Đo riêng từng bước vẽ: bật / tắt từng thứ lúc chạy, xen kẽ với cấu hình gốc để loại trôi nhiệt, ghi ms GPU, CPU
    IEnumerator Sweep(DesktopViewer viewer, string outFile)
    {
        var asset = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var camData = viewer.GetComponentInChildren<Camera>().GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        UnityEngine.Rendering.Universal.Bloom bloom = null;
        foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>())
            if (v.sharedProfile != null && v.sharedProfile.TryGet(out UnityEngine.Rendering.Universal.Bloom bl)) bloom = bl;
        var fld = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rd = ((UnityEngine.Rendering.Universal.ScriptableRendererData[])fld.GetValue(asset))[0];
        var ssao = rd.rendererFeatures.FirstOrDefault(f => f != null && f.name.Contains("Occlusion"));
        var lights = FindObjectsByType<Light>().Where(l => l.type != LightType.Directional).ToArray();
        var cam = viewer.GetComponentInChildren<Camera>();
        float sd = asset.shadowDistance; var aa = camData.antialiasing; var aq = camData.antialiasingQuality;
        var tests = new (string name, Action on, Action off)[]
        {
            ("smaa_tat", () => camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None, () => camData.antialiasing = aa),
            ("smaa_vua", () => camData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.Medium, () => camData.antialiasingQuality = aq),
            ("fxaa", () => camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing, () => camData.antialiasing = aa),
            ("bloom_tat", () => { if (bloom != null) bloom.active = false; }, () => { if (bloom != null) bloom.active = true; }),
            ("bloom_thuong", () => { if (bloom != null) { bloom.highQualityFiltering.overrideState = true; bloom.highQualityFiltering.value = false; } }, () => { if (bloom != null) bloom.highQualityFiltering.value = true; }),
            ("hau_ky_tat", () => camData.renderPostProcessing = false, () => camData.renderPostProcessing = true),
            ("ssao_tat", () => { if (ssao != null) ssao.SetActive(false); }, () => { if (ssao != null) ssao.SetActive(true); }),
            ("ban_sao_mau_tat", () => camData.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off, () => camData.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings),
            ("bong_tat", () => camData.renderShadows = false, () => camData.renderShadows = true),
            ("bong_15m", () => asset.shadowDistance = 15f, () => asset.shadowDistance = sd),
            ("hdr_tat", () => cam.allowHDR = false, () => cam.allowHDR = true),
            ("den_tat", () => { foreach (var l in lights) if (l != null) l.enabled = false; }, () => { foreach (var l in lights) if (l != null) l.enabled = true; }),
        };
        var sb = new StringBuilder("diem,bien,gpu_goc,gpu_bien,cpu_goc,cpu_bien\n");
        foreach (var order in new[] { 7, 21, 4, 29 })
        {
            var st = viewer.Stations.FirstOrDefault(x => x.order == order) ?? viewer.Stations.First();
            viewer.GoTo(st);
            float tw = Time.realtimeSinceStartup;
            while (!Khkt.Rendering.ZoneStreamer.Settled && Time.realtimeSinceStartup - tw < 15f) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            foreach (var (name, on, off) in tests)
            {
                var goc = new double[2]; var bien = new double[2];
                yield return Sample(goc);
                on();
                yield return null; yield return null;
                yield return Sample(bien);
                off();
                yield return null; yield return null;
                var goc2 = new double[2];
                yield return Sample(goc2);
                sb.AppendLine($"{st.displayName.Replace(',', ' ')},{name},{(goc[0] + goc2[0]) / 2:0.00},{bien[0]:0.00},{(goc[1] + goc2[1]) / 2:0.00},{bien[1]:0.00}");
            }
        }
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
    }

    static IEnumerator Sample(double[] kq)
    {
        var timings = new FrameTiming[1];
        double g = 0, c = 0; int n = 0;
        for (int i = 0; i < 6; i++) yield return null;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 1.5f)
        {
            yield return null;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) { g += timings[0].gpuFrameTime; c += timings[0].cpuMainThreadFrameTime; n++; }
        }
        kq[0] = n > 0 ? g / n : -1; kq[1] = n > 0 ? c / n : -1;
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }
}
