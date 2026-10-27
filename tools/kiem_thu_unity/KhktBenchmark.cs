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
    const float Warm = 1.5f, Measure = 5f;

    IEnumerator Start()
    {
        string outFile = Arg("-khkt-bench") ?? Path.Combine(Application.persistentDataPath, "bench.csv");
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        var intro = FindAnyObjectByType<IntroScreen>();
        yield return null;
        if (intro != null) intro.Close();
        var viewer = FindAnyObjectByType<DesktopViewer>();
        var sb = new StringBuilder();
        sb.AppendLine($"# {SystemInfo.processorType} | {SystemInfo.processorCount} luồng | RAM {SystemInfo.systemMemorySize} MB | {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType}) | {Screen.width}x{Screen.height} | chất lượng {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        sb.AppendLine("khu,fps_tb,fps_1pct_thap,ms_tb,ms_p95,batches,setpass,tam_giac_k,cpu_ms,gpu_ms");
        var all = new List<float>();
        var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        var setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        var spots = new List<(string name, Action go)>();
        foreach (var st in viewer.Stations.OrderBy(s => s.order).Take(9)) { var s = st; spots.Add((s.displayName, () => viewer.GoTo(s))); }
        spots.Add(("Đi bộ dọc phòng lab", () => viewer.GoTo(9)));
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
                    viewer.transform.position = Vector3.Lerp(new Vector3(18.5f, 0, 13f), new Vector3(2f, 0, 1.5f), k);
                    viewer.LookAt(viewer.transform.position + Quaternion.Euler(0, 200 + 140 * k, 0) * Vector3.forward * 3 + Vector3.up * 1.2f);
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
            float avg = ft.Average(), p95 = ft[(int)(ft.Count * 0.95f)], low = ft.Skip((int)(ft.Count * 0.99f)).DefaultIfEmpty(ft.Last()).Average();
            sb.AppendLine($"{name},{1000f / avg:0.0},{1000f / low:0.0},{avg:0.00},{p95:0.00},{(batches.Valid ? b / n : -1)},{(setpass.Valid ? sp / n : -1)},{(tris.Valid ? tr / n / 1000 : -1)},{(nt > 0 ? cpu / nt : -1):0.00},{(nt > 0 ? gpu / nt : -1):0.00}");
        }
        all.Sort();
        sb.AppendLine($"TỔNG,{1000f / all.Average():0.0},{1000f / all.Skip((int)(all.Count * 0.99f)).Average():0.0},{all.Average():0.00},{all[(int)(all.Count * 0.95f)]:0.00},,,,,");
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        Application.Quit();
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }
}
