#if KHKT_MEDIAPIPE_PLUGIN
using System;
using System.Collections.Generic;
using System.IO;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using MpImage = Mediapipe.Image;
using MpFormat = Mediapipe.ImageFormat.Types.Format;
using MpBase = Mediapipe.Tasks.Core.BaseOptions;
using MpMode = Mediapipe.Tasks.Vision.Core.RunningMode;

namespace Khkt.Input
{
    /// <summary>
    /// Nhận diện tay bằng MediaPipe C# (MediaPipeUnityPlugin của homuler) ngay trong game, không gọi Python.
    /// Cùng nguyên lý với tay_tracker.py nên mượt như bản chạy ngoài:
    ///  • ảnh webcam thu nhỏ 320 px và lật như gương bằng GPU (Blit), đọc về bằng AsyncGPUReadback (không chặn khung hình);
    ///  • HandLandmarker chế độ LIVE_STREAM: MediaPipe tự chạy ở luồng riêng, kết quả về qua callback;
    ///  • gán trái / phải theo vị trí cũ, thao tác bỏ phiếu 5 khung, lọc 1€ (HandGesture.cs, giống tay_thao_tac.py).
    /// Bật bằng ký hiệu biên dịch KHKT_MEDIAPIPE_PLUGIN khi đã thêm gói com.github.homuler.mediapipe.
    /// </summary>
    public class PluginHandTracker : MonoBehaviour
    {
        public static PluginHandTracker Instance { get; private set; }
        public bool Running { get; private set; }
        public string LastError { get; private set; } = "";
        public float Fps { get; private set; }
        /// <summary>Ảnh webcam đang đưa vào MediaPipe (320×240, đã lật như gương, lộn ngược theo chiều dọc).</summary>
        public Texture Preview => small;

        const int ProcW = 320, ProcH = 240;

        WebCamTexture cam;
        RenderTexture small;
        HandLandmarker landmarker;
        bool readbackPending;
        byte[] staging;
        long lastTs;
        int frames, results;
        float statT0;

        // kết quả từ luồng của MediaPipe, Update lấy ra
        readonly object gate = new object();
        readonly List<(Vector2[] img, Vector3[] world)> pending = new List<(Vector2[], Vector3[])>();
        bool hasPending;

        readonly HandGesture.SideAssigner sides = new HandGesture.SideAssigner();
        readonly HandGesture.Vote[] votes = { new HandGesture.Vote(), new HandGesture.Vote() };
        readonly bool[] pinching = new bool[2];
        // beta theo tọa độ 0..1 (chuẩn của cả 63 số): beta lớn thì riêng nhiễu đã đẩy tần số cắt lên cao, coi như không lọc
        readonly Euro[] fImg = { new Euro(63, 1.0f, 1.0f), new Euro(63, 1.0f, 1.0f) };
        readonly Euro[] fWorld = { new Euro(63, 1.0f, 4f), new Euro(63, 1.0f, 4f) };

        void Awake() => Instance = this;
        void OnDestroy() { Stop(); if (Instance == this) Instance = null; }
        void OnDisable() => Stop();

        /// <summary>Đang nạp mô hình ở luồng phụ (chưa có kết quả).</summary>
        public bool Starting => creating != null;
        /// <summary>Nạp hỏng (thiếu tệp mô hình, DLL bị chặn...): HandTrackerProcess chuyển sang Python.</summary>
        public bool Failed { get; private set; }
        System.Threading.Tasks.Task<HandLandmarker> creating;

        /// <summary>Bắt đầu: tạo HandLandmarker ở luồng phụ (mất 1–2 giây) để game không khựng lúc vào lab.</summary>
        public bool Begin()
        {
            if (Running || creating != null) return true;
            Failed = false;
            string model = Path.Combine(Application.streamingAssetsPath, "hand_landmarker.task");
            if (!File.Exists(model)) model = Path.Combine(Application.dataPath, "..", "..", "tools", "mo_hinh", "hand_landmarker.task");
            if (!File.Exists(model)) { LastError = "không thấy hand_landmarker.task"; Failed = true; return false; }
            HandLandmarkerOptions.ResultCallback cb = OnResult;
            creating = System.Threading.Tasks.Task.Run(() => HandLandmarker.CreateFromOptions(new HandLandmarkerOptions(
                new MpBase(MpBase.Delegate.CPU, modelAssetBuffer: File.ReadAllBytes(model)),
                MpMode.LIVE_STREAM, numHands: 2,
                minHandDetectionConfidence: 0.6f, minHandPresenceConfidence: 0.5f, minTrackingConfidence: 0.5f,
                resultCallback: cb)));
            return true;
        }

        void FinishStart()
        {
            var t = creating;
            creating = null;
            try
            {
                if (t.IsFaulted) throw t.Exception.GetBaseException();
                landmarker = t.Result;
                cam = new WebCamTexture(PickCamera(), 640, 480, 30);
                cam.Play();
                small = new RenderTexture(ProcW, ProcH, 0, RenderTextureFormat.ARGB32);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Failed = true;
                Debug.LogWarning("[TayC#] Không bật được MediaPipe C#: " + e);
                Stop();
                return;
            }
            Running = true;
            statT0 = Time.realtimeSinceStartup;
            frames = results = 0;
        }

        public void Stop()
        {
            Running = false;
            if (creating != null)
            {
                // đang nạp dở: nạp xong thì đóng luôn
                creating.ContinueWith(t => { try { if (!t.IsFaulted) t.Result.Close(); } catch { } });
                creating = null;
            }
            if (cam != null) { cam.Stop(); Destroy(cam); cam = null; }
            if (small != null) { small.Release(); Destroy(small); small = null; }
            try { landmarker?.Close(); } catch { }
            landmarker = null;
        }

        static string PickCamera()
        {
            // ưu tiên webcam gắn liền máy, tránh camera ảo (OBS, điện thoại)
            foreach (var d in WebCamTexture.devices)
            {
                string n = d.name.ToLowerInvariant();
                if (!n.Contains("virtual") && !n.Contains("obs") && !n.Contains("droid") && !n.Contains("iriun")) return d.name;
            }
            return WebCamTexture.devices.Length > 0 ? WebCamTexture.devices[0].name : "";
        }

        void Update()
        {
            if (creating != null && creating.IsCompleted) FinishStart();
            if (!Running) return;
            if (cam != null && cam.didUpdateThisFrame && cam.width > 32 && !readbackPending)
            {
                // lật ngang (gương) và lật dọc (MediaPipe cần hàng trên cùng trước) trong một lần Blit trên GPU
                Graphics.Blit(cam, small, new Vector2(-1f, -1f), new Vector2(1f, 1f));
                readbackPending = true;
                AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, OnReadback);
            }
            Consume();
            float now = Time.realtimeSinceStartup;
            if (now - statT0 > 5f)
            {
                Fps = results / (now - statT0);
                Debug.Log($"[TayC#] {frames / (now - statT0):0.0} ảnh/giây gửi, {Fps:0.0} kết quả/giây");
                statT0 = now;
                frames = results = 0;
            }
        }

        void OnReadback(AsyncGPUReadbackRequest req)
        {
            readbackPending = false;
            if (req.hasError || landmarker == null) return;
            var data = req.GetData<byte>();
            long ts = Math.Max((long)(Time.realtimeSinceStartupAsDouble * 1000.0), lastTs + 1);
            lastTs = ts;
            // LIVE_STREAM xử lý ảnh sau, ở luồng của MediaPipe, và Image không chép điểm ảnh: phải cấp bộ nhớ riêng,
            // MediaPipe gọi FreePixels khi dùng xong (cấp Allocator.Temp thì bị giải phóng cuối khung -> sập)
            IntPtr buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(data.Length);
            try
            {
                if (staging == null || staging.Length != data.Length) staging = new byte[data.Length];
                data.CopyTo(staging);
                System.Runtime.InteropServices.Marshal.Copy(staging, 0, buf, staging.Length);
                using (var img = new MpImage(MpFormat.Srgba, ProcW, ProcH, ProcW * 4, buf, FreePixelsDelegate))
                    landmarker.DetectAsync(img, ts);
                frames++;
            }
            catch (Exception e) { LastError = e.Message; }
        }

        static readonly Mediapipe.ImageFrame.Deleter FreePixelsDelegate = FreePixels;

        [AOT.MonoPInvokeCallback(typeof(Mediapipe.ImageFrame.Deleter))]
        static void FreePixels(IntPtr p) => System.Runtime.InteropServices.Marshal.FreeHGlobal(p);

        // luồng của MediaPipe: chỉ chép số liệu ra, không đụng API Unity
        void OnResult(HandLandmarkerResult r, MpImage image, long ts)
        {
            var list = new List<(Vector2[], Vector3[])>();
            if (r.handLandmarks != null)
                for (int h = 0; h < r.handLandmarks.Count; h++)
                {
                    var lm = r.handLandmarks[h].landmarks;
                    if (lm == null || lm.Count < 21) continue;
                    var img = new Vector2[21];
                    for (int i = 0; i < 21; i++) img[i] = new Vector2(lm[i].x, lm[i].y);
                    Vector3[] w = null;
                    if (r.handWorldLandmarks != null && h < r.handWorldLandmarks.Count && r.handWorldLandmarks[h].landmarks != null)
                    {
                        var wl = r.handWorldLandmarks[h].landmarks;
                        w = new Vector3[21];
                        for (int i = 0; i < 21 && i < wl.Count; i++) w[i] = new Vector3(wl[i].x, wl[i].y, wl[i].z);
                    }
                    list.Add((img, w));
                }
            lock (gate) { pending.Clear(); pending.AddRange(list); hasPending = true; }
        }

        void Consume()
        {
            List<(Vector2[] img, Vector3[] world)> hands;
            lock (gate)
            {
                if (!hasPending) return;
                hasPending = false;
                hands = new List<(Vector2[], Vector3[])>(pending);
            }
            results++;
            var at = ArmTracking.Instance;
            if (at == null) return;
            float now = Time.realtimeSinceStartup;
            // bỏ phát hiện quá nhỏ (nhiễu)
            hands.RemoveAll(x => (x.img[9] - x.img[0]).magnitude < 0.04f);
            var wrists = hands.ConvertAll(x => x.img[0]);
            var ben = sides.Assign(wrists, now);
            bool[] seen = new bool[2];
            for (int k = 0; k < hands.Count; k++)
            {
                int s = ben[k];
                if (s < 0) continue;
                seen[s] = true;
                var (img, world) = hands[k];
                string g = HandGesture.Classify(img, world, ref pinching[s], ProcW, ProcH);
                g = votes[s].Add(g);
                // lọc 1€ trên điểm ảnh và điểm 3D
                var fi = fImg[s].F(Flat(img), now);
                for (int i = 0; i < 21; i++) img[i] = new Vector2(fi[i * 3], fi[i * 3 + 1]);
                var cs = new Vector3[21];
                if (world != null)
                {
                    var fw = fWorld[s].F(Flat(world), now);
                    // ảnh đã lật như gương: trục camera Unity = (x, -y, -z) của MediaPipe
                    for (int i = 0; i < 21; i++) cs[i] = new Vector3(fw[i * 3], -fw[i * 3 + 1], -fw[i * 3 + 2]);
                }
                at.FeedFull(s == 0 ? "trai" : "phai", cs, new Vector2(1f - img[0].x, img[0].y), pinching[s] || g == "chum", g, img);
            }
            for (int s = 0; s < 2; s++) if (!seen[s]) { votes[s].Clear(); pinching[s] = false; }
        }

        static float[] Flat(Vector2[] a)
        {
            var f = new float[63];
            for (int i = 0; i < 21; i++) { f[i * 3] = a[i].x; f[i * 3 + 1] = a[i].y; }
            return f;
        }

        static float[] Flat(Vector3[] a)
        {
            var f = new float[63];
            for (int i = 0; i < 21; i++) { f[i * 3] = a[i].x; f[i * 3 + 1] = a[i].y; f[i * 3 + 2] = a[i].z; }
            return f;
        }

        /// <summary>Bộ lọc 1€ trên mảng số (như OneEuro của webcam_tracker.py).</summary>
        class Euro
        {
            readonly float minCutoff, beta;
            float[] x, dx;
            float t;
            public Euro(int n, float minCutoff, float beta) { this.minCutoff = minCutoff; this.beta = beta; }
            static float A(float c, float dt) => 1f / (1f + 1f / (2f * Mathf.PI * c * dt));
            public float[] F(float[] v, float now)
            {
                if (x == null || now - t > 0.5f) { x = (float[])v.Clone(); dx = new float[v.Length]; t = now; return x; }
                float dt = Mathf.Max(1e-3f, now - t);
                t = now;
                float speed = 0f;
                float ad = A(1f, dt);
                for (int i = 0; i < v.Length; i++) { dx[i] = ad * (v[i] - x[i]) / dt + (1 - ad) * dx[i]; speed += dx[i] * dx[i]; }
                float a = A(minCutoff + beta * Mathf.Sqrt(speed), dt);
                for (int i = 0; i < v.Length; i++) x[i] = a * v[i] + (1 - a) * x[i];
                return (float[])x.Clone();
            }
        }
    }
}
#endif
