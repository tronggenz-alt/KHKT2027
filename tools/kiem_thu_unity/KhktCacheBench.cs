using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Khkt.Environment;
using Khkt.Demo;
using Khkt.Menu;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Chỉ có trong bản sao project: đo độ mượt khi mở app và lần đầu vào lab, ghi CSV ra ổ D.
// Mở từ màn hình chính giống người dùng thật, không tắt giới hạn FPS của app ngoài lúc đo.
//
// Tham số dòng lệnh:
//   -khkt-cache-bench <thư mục>    nơi ghi kết quả (bắt buộc)
//   -khkt-nhan <tên>               tên lượt đo, dùng làm tên tệp
//   -khkt-nap-truoc                nạp cảnh lab trước với allowSceneActivation=false, chờ 3 s rồi mới kích hoạt
//   -khkt-gsc-trace <tệp>          ghi GraphicsStateCollection trong lúc đi qua các khu
//   -khkt-gsc-warm <tệp>           nạp tệp đó và WarmUp trước khi vào lab
//   -khkt-warm-all                 Shader.WarmupAllShaders trước khi vào lab
//   -khkt-may-chu-chet             đo thời gian chờ khi máy chủ không phản hồi (0.0.0.0 cổng đóng và IP không đi tới được)
public class KhktCacheBench : MonoBehaviour
{
    const float Settle = 2.5f;  // giây đứng yên ở mỗi điểm

    readonly StringBuilder csv = new StringBuilder();
    readonly StringBuilder log = new StringBuilder();
    static bool started;

    void Awake()
    {
        if (started || Arg("-khkt-cache-bench") == null) { Destroy(gameObject); return; }
        started = true;
        DontDestroyOnLoad(gameObject);
    }

    IEnumerator Start()
    {
        float toMenu = Time.realtimeSinceStartup;
        string dir = Arg("-khkt-cache-bench");
        string tag = Arg("-khkt-nhan") ?? "luot";
        Directory.CreateDirectory(dir);
        Line($"# {SystemInfo.processorType} | RAM {SystemInfo.systemMemorySize} MB | {SystemInfo.graphicsDeviceName} | {SystemInfo.graphicsDeviceType} | {Screen.width}x{Screen.height}");
        Line($"mo_app_den_man_hinh_chinh_s\t{F(toMenu)}");
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        yield return new WaitForSecondsRealtime(1f);

        if (Has("-khkt-may-chu-chet"))
        {
            yield return TimeGet("http://127.0.0.1:9/suc-khoe", 6, "cho_cong_dong_s");
            yield return TimeGet("http://10.255.255.1:8000/suc-khoe", 6, "cho_ip_khong_toi_duoc_s");
        }

        if (Arg("-khkt-gsc-warm") != null)
        {
            float w0 = Time.realtimeSinceStartup;
            var gsc = new GraphicsStateCollection();
            bool loaded = gsc.LoadFromFile(Arg("-khkt-gsc-warm"));
            var job = gsc.WarmUp();
            job.Complete();
            Line($"gsc_warm_s\t{F(Time.realtimeSinceStartup - w0)}\tnap={loaded}\tbien_the={gsc.variantCount}\ttrang_thai={gsc.totalGraphicsStateCount}\txong={gsc.isWarmedUp}");
        }
        if (Has("-khkt-warm-all"))
        {
            float w0 = Time.realtimeSinceStartup;
            Shader.WarmupAllShaders();
            Line($"warmup_all_s\t{F(Time.realtimeSinceStartup - w0)}");
        }

        GraphicsStateCollection trace = null;
        if (Arg("-khkt-gsc-trace") != null)
        {
            trace = new GraphicsStateCollection();
            Line($"gsc_trace_bat_dau\t{trace.BeginTrace()}");
        }

        // Vào lab bằng đúng luồng MainMenu.LoadLab: khách, EnterLabPending
        GameSession.GuestActive = true;
        GameSession.EnterLabPending = true;
        var frames = new List<float>();
        float t0 = Time.realtimeSinceStartup;
        var op = SceneManager.LoadSceneAsync(GameSession.LabScene);
        if (Has("-khkt-nap-truoc"))
        {
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) { frames.Add(Time.unscaledDeltaTime); yield return null; }
            float prep = Time.realtimeSinceStartup - t0;
            Stat("nap_truoc_den_90", frames);
            Line($"nap_truoc_den_90_s\t{F(prep)}\tram_mb={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576}");
            frames.Clear();
            yield return new WaitForSecondsRealtime(3f);
            frames.Clear();
            t0 = Time.realtimeSinceStartup;
            op.allowSceneActivation = true;
        }
        while (!op.isDone) { frames.Add(Time.unscaledDeltaTime); yield return null; }
        Line($"bam_vao_den_canh_xong_s\t{F(Time.realtimeSinceStartup - t0)}");
        Stat("dang_tai", frames);

        // Lần đầu vẽ cảnh lab: khung ngay sau kích hoạt, đo cả khi còn màn giới thiệu
        frames.Clear();
        float t1 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t1 < 6f) { yield return null; frames.Add(Time.unscaledDeltaTime); }
        Stat("6s_dau_trong_lab", frames);
        Line($"khung_dau_lab_ms\t{F(frames.Count > 0 ? frames[0] * 1000f : -1)}");

        var intro = FindAnyObjectByType<IntroScreen>();
        if (intro != null) intro.Close();
        yield return null;

        var viewer = FindAnyObjectByType<DesktopViewer>();
        if (viewer == null) { Line("LOI\tkhong thay DesktopViewer"); Save(dir, tag); Application.Quit(); yield break; }
        var spots = viewer.Stations.OrderBy(s => s.order).ToArray();
        Line($"so_diem\t{spots.Length}");

        // Đi hai vòng: vòng 1 gặp shader, tải khu lần đầu; vòng 2 đã có mọi thứ trong driver và Unity.
        for (int vong = 1; vong <= 2; vong++)
        {
            var all = new List<float>();
            foreach (var s in spots)
            {
                viewer.GoTo(s);
                frames.Clear();
                float t = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t < Settle) { yield return null; frames.Add(Time.unscaledDeltaTime); }
                float worst = frames.Count > 0 ? frames.Max() * 1000f : 0f;
                csv.AppendLine($"{vong},{s.order},{s.displayName.Replace(',', ' ')},{frames.Count},{worst.ToString("0.0", CultureInfo.InvariantCulture)},{frames.Count(x => x > 0.05f)},{frames.Count(x => x > 0.1f)}");
                all.AddRange(frames);
            }
            Stat("vong_" + vong, all);
        }

        if (trace != null)
        {
            trace.EndTrace();
            bool saved = trace.SaveToFile(Arg("-khkt-gsc-trace"));
            Line($"gsc_trace\tluu={saved}\tbien_the={trace.variantCount}\ttrang_thai={trace.totalGraphicsStateCount}\tkieu={trace.graphicsDeviceType}");
        }

        Save(dir, tag);
        Application.Quit();
    }

    IEnumerator TimeGet(string url, int timeout, string name)
    {
        float t = Time.realtimeSinceStartup;
        using (var r = UnityWebRequest.Get(url))
        {
            r.timeout = timeout;
            yield return r.SendWebRequest();
            Line($"{name}\t{F(Time.realtimeSinceStartup - t)}\t{r.result}");
        }
    }

    void Stat(string name, List<float> f)
    {
        if (f.Count == 0) { Line($"{name}\t0 khung"); return; }
        var s = f.OrderBy(x => x).ToArray();
        float sum = f.Sum();
        float p99 = s[Mathf.Min(s.Length - 1, (int)(s.Length * 0.99f))];
        Line($"{name}\tkhung={f.Count}\ttong_s={F(sum)}\ttb_ms={F(sum / f.Count * 1000f)}\tp99_ms={F(p99 * 1000f)}\tmax_ms={F(s[s.Length - 1] * 1000f)}\tqua33={f.Count(x => x > 0.0333f)}\tqua50={f.Count(x => x > 0.05f)}\tqua100={f.Count(x => x > 0.1f)}\tqua250={f.Count(x => x > 0.25f)}");
    }

    void Line(string s)
    {
        log.AppendLine(s);
        Debug.Log("[KHKT-CACHE] " + s);
    }

    void Save(string dir, string tag)
    {
        File.WriteAllText(Path.Combine(dir, tag + ".txt"), log.ToString(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, tag + "_diem.csv"), "vong,thu_tu,diem,khung,max_ms,qua50,qua100\n" + csv, Encoding.UTF8);
    }

    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static bool Has(string name) => Array.IndexOf(System.Environment.GetCommandLineArgs(), name) >= 0;

    static string Arg(string name)
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }
}
