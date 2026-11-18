using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Khkt.Demo;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Khkt.Rendering
{
    /// <summary>
    /// Đo hiệu năng đường đi chuẩn (90 giây, hoặc bản rút gọn 30 giây).
    /// Bật qua tham số dòng lệnh -vera-do, phím F7, hoặc nút "Đo thử 30 giây" trong Cài đặt.
    /// Xuất CSV từng khung hình và bản tóm tắt vào Application.persistentDataPath/do_khung/.
    /// </summary>
    public class DoKhungHinh : MonoBehaviour
    {
        public struct KetQuaDo
        {
            public string nhan;
            public int tongKhung;
            public float thoiGianGiay;
            public float fpsTrungBinh;
            public float msTrungBinh;
            public float p50Ms;
            public float p95Ms;
            public float p99Ms;
            public float maxMs;
            public int khungTren33Ms;
            public int khungTren50Ms;
            public float khungTren50MoiPhut;
            public float gcTongMb;
            public float gcKbMoiKhung;
            public int gcSoLan;
            public MucDoHoa mucDeXuat;
            public string duongDanCsv;
            public string duongDanTomTat;
        }

        struct MauKhung
        {
            public int thuTu;
            public float giay;
            public int chang;
            public int khu;
            public float ms;
            public float cpuMs;
            public float gpuMs;
            public long gcBytes;
            public int gcCount;
            public float x, y, z, yaw;
        }

        struct MocDuongDi
        {
            public float thoiDiemChuan; // theo mốc 90 giây
            public int chang;
            public Vector3 viTri;
            public float yaw;
            public float pitch;
        }

        // Đường đi chuẩn 90 giây:
        // Chặng 1: Sảnh tầng 1 → Phòng thí nghiệm
        // Chặng 2: Quầy hóa chất (Kho hóa chất)
        // Chặng 3: Xoay 360 độ
        // Chặng 4: Hành lang → Cầu thang bộ → Tầng 2
        // Chặng 5: Nhà kính tầng 2
        // Chặng 6: Hành lang tầng 2 → Kính hiển vi
        static readonly MocDuongDi[] DuongDiChuan =
        {
            new MocDuongDi { thoiDiemChuan = 0f,  chang = 1, viTri = new Vector3(2.40f, 0.0f, -1.10f),  yaw = 0f,    pitch = 4f },
            new MocDuongDi { thoiDiemChuan = 4f,  chang = 1, viTri = new Vector3(2.40f, 0.0f,  2.20f),  yaw = 45f,   pitch = 5f },
            new MocDuongDi { thoiDiemChuan = 8f,  chang = 1, viTri = new Vector3(2.40f, 0.0f,  5.05f),  yaw = 90f,   pitch = 4f },
            new MocDuongDi { thoiDiemChuan = 14f, chang = 1, viTri = new Vector3(16.00f, 0.0f, 5.05f),  yaw = 60f,   pitch = 4f },
            new MocDuongDi { thoiDiemChuan = 17f, chang = 2, viTri = new Vector3(18.80f, 0.0f, 7.70f),  yaw = 90f,   pitch = 5f },
            new MocDuongDi { thoiDiemChuan = 21f, chang = 2, viTri = new Vector3(21.60f, 0.0f, 7.70f),  yaw = 45f,   pitch = 6f },
            new MocDuongDi { thoiDiemChuan = 26f, chang = 2, viTri = new Vector3(21.60f, 0.0f, 11.20f), yaw = 120f,  pitch = 5f },
            new MocDuongDi { thoiDiemChuan = 30f, chang = 2, viTri = new Vector3(21.60f, 0.0f, 6.20f),  yaw = 180f,  pitch = 6f },
            new MocDuongDi { thoiDiemChuan = 44f, chang = 3, viTri = new Vector3(21.60f, 0.0f, 6.20f),  yaw = 540f,  pitch = 6f },
            new MocDuongDi { thoiDiemChuan = 48f, chang = 4, viTri = new Vector3(21.60f, 0.0f, -1.10f), yaw = 90f,   pitch = 3f },
            new MocDuongDi { thoiDiemChuan = 52f, chang = 4, viTri = new Vector3(27.80f, 0.0f, -1.10f), yaw = 45f,   pitch = 2f },
            new MocDuongDi { thoiDiemChuan = 54f, chang = 4, viTri = new Vector3(29.175f, 0.0f, 0.33f), yaw = 0f,    pitch = -8f },
            new MocDuongDi { thoiDiemChuan = 60f, chang = 4, viTri = new Vector3(29.175f, 3.8f, 5.90f), yaw = -45f,  pitch = 2f },
            new MocDuongDi { thoiDiemChuan = 64f, chang = 4, viTri = new Vector3(27.80f, 3.8f, -1.80f), yaw = -90f,  pitch = 3f },
            new MocDuongDi { thoiDiemChuan = 68f, chang = 5, viTri = new Vector3(19.80f, 3.8f, -1.80f), yaw = 180f,  pitch = 4f },
            new MocDuongDi { thoiDiemChuan = 72f, chang = 5, viTri = new Vector3(19.80f, 3.8f, -5.80f), yaw = 125f,  pitch = 7f },
            new MocDuongDi { thoiDiemChuan = 76f, chang = 5, viTri = new Vector3(22.60f, 3.8f, -6.50f), yaw = 180f,  pitch = 8f },
            new MocDuongDi { thoiDiemChuan = 80f, chang = 6, viTri = new Vector3(19.80f, 3.8f, -1.80f), yaw = -90f,  pitch = 3f },
            new MocDuongDi { thoiDiemChuan = 85f, chang = 6, viTri = new Vector3(2.40f, 3.8f, -1.80f),  yaw = 0f,    pitch = 4f },
            new MocDuongDi { thoiDiemChuan = 88f, chang = 6, viTri = new Vector3(2.40f, 3.8f,  2.60f),  yaw = 90f,   pitch = 8f },
            new MocDuongDi { thoiDiemChuan = 90f, chang = 6, viTri = new Vector3(6.05f, 3.8f,  2.60f),  yaw = -90f,  pitch = 14f },
        };

        public static DoKhungHinh Instance { get; private set; }
        public static bool DangDo => Instance != null && Instance.running;

        bool running;
        bool cliMode;
        bool cliShort;
        bool cliSweep;
        string cliTag;
        string cliShotDir;
        string cliShotPrefix = "anh";
        Coroutine activeRoutine;
        readonly List<MauKhung> samples = new List<MauKhung>(16384);
        readonly FrameTiming[] timingBuf = new FrameTiming[1];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInit()
        {
            if (Instance != null) return;
            var go = new GameObject("DoKhungHinh");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DoKhungHinh>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ParseCli();
        }

        void Start()
        {
            if (cliSweep && !running)
                activeRoutine = StartCoroutine(CoRunSweepCli());
            else if (!string.IsNullOrEmpty(cliShotDir) && !running)
                activeRoutine = StartCoroutine(CoRunShotsCli());
            else if (cliMode && !running)
                activeRoutine = StartCoroutine(CoRunCli());
        }

        void Update()
        {
            if (cliMode || cliSweep || !string.IsNullOrEmpty(cliShotDir)) return;
            var kb = Keyboard.current;
            if (kb != null && kb.f7Key.wasPressedThisFrame)
            {
                if (running) StopMeasurement();
                else StartMeasurement(90f, "phim_f7", quitWhenDone: false, onDone: null);
            }
        }

        void ParseCli()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "-vera-do", StringComparison.OrdinalIgnoreCase))
                    cliMode = true;
                else if (string.Equals(a, "-vera-do-ngan", StringComparison.OrdinalIgnoreCase))
                    cliShort = true;
                else if (string.Equals(a, "-vera-sweep", StringComparison.OrdinalIgnoreCase))
                    cliSweep = true;
                else if (a.StartsWith("-vera-nhan=", StringComparison.OrdinalIgnoreCase))
                    cliTag = SanitizeTag(a.Substring("-vera-nhan=".Length));
                else if (a.StartsWith("-vera-chup-g1=", StringComparison.OrdinalIgnoreCase))
                    cliShotDir = a.Substring("-vera-chup-g1=".Length).Trim();
                else if (a.StartsWith("-vera-chup-tien-to=", StringComparison.OrdinalIgnoreCase))
                    cliShotPrefix = SanitizeTag(a.Substring("-vera-chup-tien-to=".Length));
            }
        }

        public static void ChayThu30Giay(Action<MucDoHoa, KetQuaDo> onDone = null)
        {
            if (Instance == null) AutoInit();
            if (Instance == null || Instance.running) return;
            Instance.StartMeasurement(30f, "do_thu_30s", quitWhenDone: false, onDone: onDone);
        }

        public void StopMeasurement()
        {
            if (!running) return;
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = null;
            running = false;
            LabMessages.Say("Đã dừng đo khung hình.", LabMessages.Tone.Info);
        }

        IEnumerator CoRunSweepCli()
        {
            running = true;
            Application.runInBackground = true;
            if (SceneManager.GetActiveScene().name != "PhongThiNghiem")
            {
                var op = SceneManager.LoadSceneAsync("PhongThiNghiem", LoadSceneMode.Single);
                while (op != null && !op.isDone) yield return null;
            }
            float w0 = Time.realtimeSinceStartup;
            while (FindAnyObjectByType<DesktopViewer>() == null && Time.realtimeSinceStartup - w0 < 15f)
                yield return null;
            yield return null;
            yield return null;
            var intro = FindAnyObjectByType<IntroScreen>();
            if (intro != null && intro.IsOpen) intro.Close();
            var viewer = FindAnyObjectByType<DesktopViewer>();
            ApplyPose(viewer, new Vector3(5.8f, 0f, 5.05f), 90f, 4f);
            float tSettle = Time.realtimeSinceStartup;
            while (!ZoneStreamer.Settled && Time.realtimeSinceStartup - tSettle < 10f) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var cam = viewer.viewCamera;
            var camData = cam != null ? cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            var vol = FindAnyObjectByType<UnityEngine.Rendering.Volume>();
            UnityEngine.Rendering.Universal.Bloom bloom = null;
            if (vol != null && vol.sharedProfile != null) vol.sharedProfile.TryGet(out bloom);

            var sb = new StringBuilder();
            sb.AppendLine($"SWEEP {Screen.width}x{Screen.height}");

            IEnumerator MeasureStep(string label, Action apply)
            {
                apply();
                for (int k = 0; k < 10; k++) yield return null;
                double gpuSum = 0, cpuSum = 0, dtSum = 0; int cnt = 0, gc = 0;
                float s0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - s0 < 1.5f)
                {
                    yield return null;
                    dtSum += Time.unscaledDeltaTime * 1000.0;
                    cnt++;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timingBuf) > 0 && timingBuf[0].gpuFrameTime > 0)
                    {
                        cpuSum += timingBuf[0].cpuFrameTime;
                        gpuSum += timingBuf[0].gpuFrameTime;
                        gc++;
                    }
                }
                string line = $"{label}: dt={dtSum / cnt:0.00}ms cpu={(gc > 0 ? cpuSum / gc : -1):0.00}ms gpu={(gc > 0 ? gpuSum / gc : -1):0.00}ms";
                sb.AppendLine(line);
                Debug.Log("[VERA-SWEEP] " + line);
            }

            if (urp != null && camData != null)
            {
                yield return MeasureStep("1_hien_tai(msaa2+smaa+hdr64+depth+opaque+bloomHQ)", () => { });
                yield return MeasureStep("2_hdr32", () => { urp.hdrColorBufferPrecision = UnityEngine.Rendering.Universal.HDRColorBufferPrecision._32Bits; });
                yield return MeasureStep("3_no_depth_no_opaque", () => { urp.supportsCameraDepthTexture = false; urp.supportsCameraOpaqueTexture = false; });
                yield return MeasureStep("4_bloom_thuong", () => { if (bloom != null) bloom.highQualityFiltering.value = false; });
                yield return MeasureStep("5_no_smaa(msaa2_only)", () => { camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; });
                yield return MeasureStep("6_msaa4_only", () => { urp.msaaSampleCount = 4; camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; });
                yield return MeasureStep("7_smaa_only(msaa1)", () => { urp.msaaSampleCount = 1; camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing; camData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.Medium; });
                yield return MeasureStep("8_no_post_msaa4", () => { urp.msaaSampleCount = 4; camData.renderPostProcessing = false; });
                yield return MeasureStep("9_no_hdr_msaa4_post_on", () => { camData.renderPostProcessing = true; urp.supportsHDR = false; urp.msaaSampleCount = 4; camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; });
                yield return MeasureStep("10_scale0.85_msaa2_smaa_post_hdr32", () => { urp.supportsHDR = true; urp.renderScale = 0.85f; urp.msaaSampleCount = 2; camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing; });
            }

            string outP = Path.Combine(Application.persistentDataPath, "do_khung", "sweep_gpu.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outP));
            File.WriteAllText(outP, sb.ToString());
            running = false;
            Application.Quit();
        }

        IEnumerator CoRunCli()
        {
            if (SceneManager.GetActiveScene().name != "PhongThiNghiem")
            {
                var op = SceneManager.LoadSceneAsync("PhongThiNghiem", LoadSceneMode.Single);
                while (op != null && !op.isDone) yield return null;
            }

            float wait0 = Time.realtimeSinceStartup;
            while (FindAnyObjectByType<DesktopViewer>() == null && Time.realtimeSinceStartup - wait0 < 15f)
                yield return null;

            yield return null;
            yield return null;

            float duration = cliShort ? 30f : 90f;
            string tag = !string.IsNullOrEmpty(cliTag) ? cliTag : (cliShort ? "cli_30s" : "cli_90s");
            yield return CoMeasure(duration, tag, quitWhenDone: true, onDone: null);
        }

        IEnumerator CoRunShotsCli()
        {
            running = true;
            Application.runInBackground = true;
            if (SceneManager.GetActiveScene().name != "PhongThiNghiem")
            {
                var op = SceneManager.LoadSceneAsync("PhongThiNghiem", LoadSceneMode.Single);
                while (op != null && !op.isDone) yield return null;
            }

            float wait0 = Time.realtimeSinceStartup;
            while (FindAnyObjectByType<DesktopViewer>() == null && Time.realtimeSinceStartup - wait0 < 15f)
                yield return null;

            yield return null;
            yield return null;

            var intro = FindAnyObjectByType<IntroScreen>();
            if (intro != null && intro.IsOpen) intro.Close();

            var viewer = FindAnyObjectByType<DesktopViewer>();
            if (viewer == null)
            {
                running = false;
                Application.Quit();
                yield break;
            }

            Directory.CreateDirectory(cliShotDir);

            // 1. Buret (vạch chia & chữ gần)
            var buret = FindAnyObjectByType<Khkt.Interaction.BuretStopcock>();
            if (buret != null)
            {
                Vector3 focus = buret.transform.position + Vector3.up * 0.22f;
                viewer.SetPose(new Vector3(focus.x + 0.42f, 0f, focus.z), -90f, 0f);
                yield return null;
                viewer.LookAt(focus);
            }
            else
            {
                ApplyPose(viewer, new Vector3(6.05f, 0f, 2.60f), -90f, 10f);
            }
            yield return CaptureNamedShot("buret");

            // 2. Biển báo khu thực hành & dãy bàn
            ApplyPose(viewer, new Vector3(5.8f, 0f, 5.05f), 90f, -8f);
            yield return CaptureNamedShot("bien_bao");

            // 3. Vân sàn & mặt bàn góc nghiêng (lọc dị hướng)
            ApplyPose(viewer, new Vector3(2.6f, 0f, 4.9f), 82f, 22f);
            yield return CaptureNamedShot("san_ban");

            // 4. Nhà kính tầng 2
            ApplyPose(viewer, new Vector3(19.8f, 3.8f, -5.8f), 135f, 6f);
            yield return CaptureNamedShot("nha_kinh");

            // 5. Màn hình Cài đặt -> tab Hiển thị (chụp cho ảnh nghiệm thu SAU)
            if (string.Equals(cliShotPrefix, "sau", StringComparison.OrdinalIgnoreCase) && intro != null)
            {
                ApplyPose(viewer, new Vector3(5.8f, 0f, 5.05f), 90f, -8f);
                yield return null;
                intro.OpenDisplaySettings();
                yield return CaptureNamedShot("cai_dat_hien_thi");
                intro.Close();
            }

            Debug.Log($"[DO-KHUNG-CHUP] Hoàn tất chụp ảnh '{cliShotPrefix}' ({Screen.width}x{Screen.height}) vào {cliShotDir}");
            running = false;
            yield return null;
            Application.Quit();
        }

        IEnumerator CaptureNamedShot(string suffix)
        {
            // Chờ 2 khung để ZoneStreamer.Update() nhận biết vị trí mới sau SetPose
            yield return null;
            yield return null;

            float t0 = Time.realtimeSinceStartup;
            while (!ZoneStreamer.Settled && Time.realtimeSinceStartup - t0 < 12f)
                yield return null;

            // Chờ màn mờ chuyển khu (nếu vừa stream cảnh tầng 2) hạ xuống hoàn toàn
            float waitFadeUntil = Time.realtimeSinceStartup + 0.7f;
            while (Time.realtimeSinceStartup < waitFadeUntil)
                yield return null;

            for (int i = 0; i < 6; i++) yield return null;
            yield return new WaitForEndOfFrame();

            string path = Path.Combine(cliShotDir, $"{cliShotPrefix}_{suffix}.png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);

            float tw = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - tw < 2.5f)
                yield return null;
            yield return null;
        }

        void StartMeasurement(float durationS, string tag, bool quitWhenDone, Action<MucDoHoa, KetQuaDo> onDone)
        {
            if (running) return;
            activeRoutine = StartCoroutine(CoMeasure(durationS, tag, quitWhenDone, onDone));
        }

        IEnumerator CoMeasure(float durationS, string tag, bool quitWhenDone, Action<MucDoHoa, KetQuaDo> onDone)
        {
            running = true;
            Application.runInBackground = true;

            var intro = FindAnyObjectByType<IntroScreen>();
            if (intro != null && intro.IsOpen) intro.Close();

            var viewer = FindAnyObjectByType<DesktopViewer>();
            if (viewer == null)
            {
                Debug.LogWarning("[DO-KHUNG] Không tìm thấy DesktopViewer trong cảnh.");
                running = false;
                if (quitWhenDone) Application.Quit();
                yield break;
            }

            if (!cliMode)
                LabMessages.Say($"Đang chạy đo chuẩn {durationS:0} giây...", LabMessages.Tone.Info);

            // Đưa người chơi về điểm xuất phát và chờ ổn định tải khu đầu
            ApplyPose(viewer, DuongDiChuan[0].viTri, DuongDiChuan[0].yaw, DuongDiChuan[0].pitch);
            float settleWait = Time.realtimeSinceStartup;
            while (!ZoneStreamer.Settled && Time.realtimeSinceStartup - settleWait < 10f)
                yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            samples.Clear();
            int prevGen0 = GC.CollectionCount(0);
            long prevMem = GC.GetTotalMemory(false);
            float tStart = Time.realtimeSinceStartup;
            int frameIdx = 0;

            while (true)
            {
                float elapsed = Time.realtimeSinceStartup - tStart;
                if (elapsed >= durationS) break;

                float norm90 = Mathf.Clamp(elapsed / durationS * 90f, 0f, 90f);
                EvaluatePath(norm90, out Vector3 pos, out float yaw, out float pitch, out int stage);
                ApplyPose(viewer, pos, yaw, pitch);

                yield return null;

                float now = Time.realtimeSinceStartup;
                float dtMs = Time.unscaledDeltaTime * 1000f;

                float cpuMs = -1f, gpuMs = -1f;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timingBuf) > 0)
                {
                    if (timingBuf[0].cpuFrameTime > 0) cpuMs = (float)timingBuf[0].cpuFrameTime;
                    if (timingBuf[0].gpuFrameTime > 0) gpuMs = (float)timingBuf[0].gpuFrameTime;
                }

                int gen0 = GC.CollectionCount(0);
                int dGen0 = Math.Max(0, gen0 - prevGen0);
                long mem = GC.GetTotalMemory(false);
                long dMem = dGen0 == 0 && mem > prevMem ? mem - prevMem : 0L;
                prevGen0 = gen0;
                prevMem = mem;

                samples.Add(new MauKhung
                {
                    thuTu = frameIdx++,
                    giay = now - tStart,
                    chang = stage,
                    khu = ZoneMap.At(pos),
                    ms = dtMs,
                    cpuMs = cpuMs,
                    gpuMs = gpuMs,
                    gcBytes = dMem,
                    gcCount = dGen0,
                    x = pos.x,
                    y = pos.y,
                    z = pos.z,
                    yaw = yaw
                });
            }

            KetQuaDo kq = BuildAndSaveReport(tag, durationS);
            running = false;
            activeRoutine = null;

            if (!cliMode)
                LabMessages.Say($"Đo xong: p95 = {kq.p95Ms:0.0} ms, đề xuất {HardwareProfile.TenMuc(kq.mucDeXuat)}", LabMessages.Tone.Good);

            onDone?.Invoke(kq.mucDeXuat, kq);

            if (quitWhenDone)
            {
                yield return null;
                Application.Quit();
            }
        }

        static void EvaluatePath(float t90, out Vector3 pos, out float yaw, out float pitch, out int stage)
        {
            for (int i = 0; i < DuongDiChuan.Length - 1; i++)
            {
                var a = DuongDiChuan[i];
                var b = DuongDiChuan[i + 1];
                if (t90 <= b.thoiDiemChuan || i == DuongDiChuan.Length - 2)
                {
                    float span = Mathf.Max(0.001f, b.thoiDiemChuan - a.thoiDiemChuan);
                    float u = Mathf.Clamp01((t90 - a.thoiDiemChuan) / span);
                    pos = Vector3.Lerp(a.viTri, b.viTri, u);
                    yaw = Mathf.Lerp(a.yaw, b.yaw, u);
                    pitch = Mathf.Lerp(a.pitch, b.pitch, u);
                    stage = b.chang;
                    return;
                }
            }
            var last = DuongDiChuan[DuongDiChuan.Length - 1];
            pos = last.viTri;
            yaw = last.yaw;
            pitch = last.pitch;
            stage = last.chang;
        }

        static void ApplyPose(DesktopViewer viewer, Vector3 pos, float yaw, float pitch)
        {
            viewer.SetPose(pos, yaw, pitch);
        }

        KetQuaDo BuildAndSaveReport(string tag, float durationS)
        {
            int n = samples.Count;
            var sorted = new float[n];
            double sumMs = 0;
            int over33 = 0, over50 = 0, gcTotalCount = 0;
            long gcTotalBytes = 0;

            for (int i = 0; i < n; i++)
            {
                float ms = samples[i].ms;
                sorted[i] = ms;
                sumMs += ms;
                if (ms > 33.333f) over33++;
                if (ms > 50f) over50++;
                gcTotalBytes += samples[i].gcBytes;
                gcTotalCount += samples[i].gcCount;
            }
            Array.Sort(sorted);

            float avgMs = n > 0 ? (float)(sumMs / n) : 0f;
            float fpsAvg = n > 0 && durationS > 0 ? n / durationS : 0f;
            float p50 = Percentile(sorted, 0.50f);
            float p95 = Percentile(sorted, 0.95f);
            float p99 = Percentile(sorted, 0.99f);
            float maxMs = n > 0 ? sorted[n - 1] : 0f;
            float over50PerMin = durationS > 0f ? over50 / (durationS / 60f) : 0f;
            float gcMb = gcTotalBytes / (1024f * 1024f);
            float gcKbPerFrame = n > 0 ? (gcTotalBytes / 1024f) / n : 0f;

            MucDoHoa deXuat = RecommendFromStats(p95, over50PerMin);

            string dir = Path.Combine(Application.persistentDataPath, "do_khung");
            Directory.CreateDirectory(dir);
            string safeTag = SanitizeTag(tag);
            string csvPath = Path.Combine(dir, $"do_khung_{safeTag}.csv");
            string latestCsvPath = Path.Combine(dir, "do_khung_moi_nhat.csv");
            string sumPath = Path.Combine(dir, $"tom_tat_{safeTag}.txt");
            string latestSumPath = Path.Combine(dir, "tom_tat_moi_nhat.txt");

            var ci = CultureInfo.InvariantCulture;
            var csv = new StringBuilder(n * 64 + 256);
            csv.AppendLine("khung,giay,chang,khu,ms,cpu_ms,gpu_ms,gc_bytes,gc_gen0,x,y,z,yaw");
            for (int i = 0; i < n; i++)
            {
                var s = samples[i];
                csv.Append(s.thuTu).Append(',')
                   .Append(s.giay.ToString("0.000", ci)).Append(',')
                   .Append(s.chang).Append(',')
                   .Append(s.khu).Append(',')
                   .Append(s.ms.ToString("0.000", ci)).Append(',')
                   .Append(s.cpuMs.ToString("0.000", ci)).Append(',')
                   .Append(s.gpuMs.ToString("0.000", ci)).Append(',')
                   .Append(s.gcBytes).Append(',')
                   .Append(s.gcCount).Append(',')
                   .Append(s.x.ToString("0.00", ci)).Append(',')
                   .Append(s.y.ToString("0.00", ci)).Append(',')
                   .Append(s.z.ToString("0.00", ci)).Append(',')
                   .Append(s.yaw.ToString("0.0", ci)).Append('\n');
            }
            string csvText = csv.ToString();
            File.WriteAllText(csvPath, csvText, new UTF8Encoding(false));
            File.WriteAllText(latestCsvPath, csvText, new UTF8Encoding(false));

            var summary = new StringBuilder();
            summary.AppendLine($"Nhãn đo: {safeTag}");
            summary.AppendLine($"Thời gian: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Thời lượng: {durationS:0.0} s");
            summary.AppendLine($"Phần cứng: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB) | RAM {SystemInfo.systemMemorySize} MB | {SystemInfo.processorCount} nhân");
            summary.AppendLine($"Màn hình: {Screen.width}x{Screen.height} (vật lý {Display.main?.systemWidth ?? 0}x{Display.main?.systemHeight ?? 0}), DPI {Screen.dpi:0}");
            summary.AppendLine($"Cấu hình: mức={HardwareProfile.TenMuc(HardwareProfile.Muc)} (tự động={HardwareProfile.TuDong}), targetFPS={Application.targetFrameRate}, vSync={QualitySettings.vSyncCount}");
            summary.AppendLine($"Tổng khung: {n} | FPS TB: {fpsAvg:0.0} | ms TB: {avgMs:0.00}");
            summary.AppendLine($"p50={p50:0.00} ms | p95={p95:0.00} ms | p99={p99:0.00} ms | max={maxMs:0.00} ms");
            summary.AppendLine($"Khung > 33,3 ms: {over33} | Khung > 50 ms: {over50} ({over50PerMin:0.00} khung/phút)");
            summary.AppendLine($"GC: tổng {gcMb:0.00} MB ({gcKbPerFrame:0.00} KB/khung), số lần Gen0: {gcTotalCount}");
            summary.AppendLine($"Đề xuất mức từ bài đo: {HardwareProfile.TenMuc(deXuat)}");

            // Chi tiết theo từng chặng
            summary.AppendLine("--- Theo chặng ---");
            for (int c = 1; c <= 6; c++)
            {
                int cnt = 0, c33 = 0, c50 = 0;
                float cMax = 0f;
                double cSum = 0;
                for (int i = 0; i < n; i++)
                {
                    if (samples[i].chang != c) continue;
                    float ms = samples[i].ms;
                    cnt++;
                    cSum += ms;
                    if (ms > cMax) cMax = ms;
                    if (ms > 33.333f) c33++;
                    if (ms > 50f) c50++;
                }
                if (cnt > 0)
                    summary.AppendLine($"Chặng {c}: khung={cnt}, TB={cSum / cnt:0.00} ms, max={cMax:0.0} ms, >33ms={c33}, >50ms={c50}");
            }

            string sumText = summary.ToString();
            File.WriteAllText(sumPath, sumText, new UTF8Encoding(false));
            File.WriteAllText(latestSumPath, sumText, new UTF8Encoding(false));

            Debug.Log($"[DO-KHUNG] tag={safeTag} frames={n} fps={fpsAvg:0.0} p50={p50:0.00} p95={p95:0.00} p99={p99:0.00} max={maxMs:0.00} >33={over33} >50={over50} ({over50PerMin:0.00}/phut) gcMB={gcMb:0.00} -> {sumPath}");

            return new KetQuaDo
            {
                nhan = safeTag,
                tongKhung = n,
                thoiGianGiay = durationS,
                fpsTrungBinh = fpsAvg,
                msTrungBinh = avgMs,
                p50Ms = p50,
                p95Ms = p95,
                p99Ms = p99,
                maxMs = maxMs,
                khungTren33Ms = over33,
                khungTren50Ms = over50,
                khungTren50MoiPhut = over50PerMin,
                gcTongMb = gcMb,
                gcKbMoiKhung = gcKbPerFrame,
                gcSoLan = gcTotalCount,
                mucDeXuat = deXuat,
                duongDanCsv = csvPath,
                duongDanTomTat = sumPath
            };
        }

        static MucDoHoa RecommendFromStats(float p95Ms, float over50PerMin)
        {
            if (p95Ms <= 16.8f && over50PerMin <= 2f)
                return HardwareProfile.DeXuat() >= MucDoHoa.SieuNet ? MucDoHoa.SieuNet : MucDoHoa.Cao;
            if (p95Ms <= 21f && over50PerMin <= 4f)
                return MucDoHoa.Cao;
            if (p95Ms <= 30f && over50PerMin <= 8f)
                return MucDoHoa.Vua;
            return MucDoHoa.Thap;
        }

        static float Percentile(float[] sorted, float q)
        {
            if (sorted == null || sorted.Length == 0) return 0f;
            int idx = Mathf.Clamp(Mathf.FloorToInt(sorted.Length * q), 0, sorted.Length - 1);
            return sorted[idx];
        }

        static string SanitizeTag(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "mac_dinh";
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw.Trim())
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
            return sb.Length > 0 ? sb.ToString() : "mac_dinh";
        }
    }
}
