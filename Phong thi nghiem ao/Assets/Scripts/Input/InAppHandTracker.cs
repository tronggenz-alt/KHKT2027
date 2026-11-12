using System;
using System.Collections.Generic;
using Khkt.Demo;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Input
{
    /// <summary>
    /// Nhận diện bàn tay ngay trong Unity bằng mô hình MediaPipe (Inference Engine, GPU), không cần Python:
    /// Smart App Control của Windows chặn thư viện của MediaPipe Python trên nhiều máy.
    ///  • F10 bật / tắt webcam và nhận diện tay.
    ///  • Mỗi khung: lật ảnh như soi gương, cắt vùng tay đã xoay thẳng, chạy mô hình 21 điểm (224 px).
    ///    Mô hình dò bàn tay (192 px) chỉ chạy khi còn thiếu tay, cách 3 khung một lần.
    ///  • Kết quả (21 điểm 3D theo mét, vị trí cổ tay trên ảnh, chụm ngón) đưa vào ArmTracking cho ArmRig.
    /// </summary>
    public class InAppHandTracker : MonoBehaviour
    {
        class Track
        {
            public bool active;
            public Vector2 center;       // trong khung vuông đã lật, 0..1, y hướng xuống
            public float size, rot;      // cạnh vùng cắt (0..1), góc xoay (rad)
            public bool pinch;
            public string side;          // chốt lúc mới thấy tay rồi giữ nguyên: nhãn của mô hình hay nhảy qua lại
            public int misses;
        }

        /// <summary>Lỡ mất tay vài khung (mờ, chớp sáng) vẫn giữ vùng cắt để bắt lại, không bật tắt liên tục.</summary>
        const int HoldFrames = 6;

        const int DetN = 192, LmN = 224, Anchors = 2016;
        const float DetThreshold = 0.5f, PresenceThreshold = 0.5f;

        public static InAppHandTracker Instance { get; private set; }
        public bool Running { get; private set; }
        public float Fps { get; private set; }
        public float InferMs { get; private set; }
        public int HandsVisible { get; private set; }

        WebCamTexture cam;
        Color32[] px;
        Worker detWorker, lmWorker;
        Tensor<float> detIn, lmIn;
        readonly float[] detBuf = new float[DetN * DetN * 3], lmBuf = new float[LmN * LmN * 3];
        Vector2[] anchors;
        readonly Track[] tracks = { new Track(), new Track() };
        readonly Vector3[] world = new Vector3[21];
        readonly Vector2[] img = new Vector2[21];
        int frame, statFrames, statSeen;
        float statT0, statMs, startedAt;
        bool warned;

        void Awake() => Instance = this;

        // vào lab là bật luôn để hai tay theo tay thật; F10 tắt
        System.Collections.IEnumerator Start()
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForSecondsRealtime(1.5f);
            if (!Running) Begin();
        }

        void OnDestroy()
        {
            Stop();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame)
            {
                if (Running) Stop(); else Begin();
            }
            if (!Running) return;
            if (cam == null || !cam.isPlaying || cam.width < 32)
            {
                if (!warned && Time.realtimeSinceStartup - startedAt > 4f)
                {
                    warned = true;
                    LabMessages.Say("Không mở được webcam: tắt điều khiển bằng đầu (F8) hoặc ứng dụng khác đang dùng webcam", LabMessages.Tone.Warn);
                }
                return;
            }
            if (!cam.didUpdateThisFrame) return;
            Process();
        }

        // =====================================================================
        // Bật, tắt
        // =====================================================================

        void Begin()
        {
            try
            {
                if (detWorker == null)
                {
                    detWorker = new Worker(ModelLoader.Load(Resources.Load<ModelAsset>("hand_detector")), BackendType.GPUCompute);
                    lmWorker = new Worker(ModelLoader.Load(Resources.Load<ModelAsset>("hand_landmarks_detector")), BackendType.GPUCompute);
                    detIn = new Tensor<float>(new TensorShape(1, DetN, DetN, 3));
                    lmIn = new Tensor<float>(new TensorShape(1, LmN, LmN, 3));
                    LoadAnchors();
                }
                cam = new WebCamTexture(PickCamera(), 640, 480, 30);
                cam.Play();
            }
            catch (Exception e)
            {
                LabMessages.Say("Không bật được nhận diện tay: " + e.Message, LabMessages.Tone.Warn);
                Debug.LogWarning("[TayUnity] " + e);
                return;
            }
            Running = true;
            warned = false;
            startedAt = statT0 = Time.realtimeSinceStartup;
            statFrames = statSeen = 0;
            statMs = 0;
            foreach (var t in tracks) t.active = false;
            if (MediaPipeHandSource.Instance != null) MediaPipeHandSource.Instance.handTrackingEnabled = true;
            LabMessages.Say("Nhận diện tay: BẬT (F10 để tắt). Đưa tay vào khung webcam", LabMessages.Tone.Info);
        }

        void Stop()
        {
            if (cam != null) { cam.Stop(); Destroy(cam); cam = null; }
            if (Running) LabMessages.Say("Nhận diện tay: TẮT", LabMessages.Tone.Info);
            Running = false;
            HandsVisible = 0;
        }

        void OnDisable() => Stop();

        void OnApplicationQuit()
        {
            Stop();
            detWorker?.Dispose(); lmWorker?.Dispose(); detIn?.Dispose(); lmIn?.Dispose();
        }

        /// <summary>Webcam của máy, bỏ qua camera ảo và camera điện thoại.</summary>
        static string PickCamera()
        {
            string[] skip = { "virtual", "obs", "droidcam", "iriun", "phone", "epoccam", "camo" };
            foreach (var d in WebCamTexture.devices)
            {
                string n = d.name.ToLowerInvariant();
                bool bad = false;
                foreach (var s in skip) if (n.Contains(s)) bad = true;
                if (!bad) return d.name;
            }
            return WebCamTexture.devices.Length > 0 ? WebCamTexture.devices[0].name : "";
        }

        void LoadAnchors()
        {
            var text = Resources.Load<TextAsset>("anchors_tay").text.Split('\n');
            var list = new List<Vector2>(Anchors);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in text)
            {
                var p = line.Trim().Split(',');
                if (p.Length < 2) continue;
                list.Add(new Vector2(float.Parse(p[0], ci), float.Parse(p[1], ci)));
            }
            anchors = list.ToArray();
        }

        // =====================================================================
        // Một khung
        // =====================================================================

        void Process()
        {
            float t0 = Time.realtimeSinceStartup;
            int w = cam.width, h = cam.height;
            if (px == null || px.Length != w * h) px = new Color32[w * h];
            cam.GetPixels32(px);
            frame++;

            // thiếu tay: dò lại, cách 3 khung một lần cho nhẹ
            int active = (tracks[0].active ? 1 : 0) + (tracks[1].active ? 1 : 0);
            if (active < 2 && frame % 3 == 0) Detect(w, h);

            int seen = 0;
            foreach (var t in tracks)
            {
                if (!t.active) continue;
                if (Landmarks(t, w, h)) { seen++; t.misses = 0; }
                else if (++t.misses > HoldFrames) { t.active = false; t.side = null; }
            }
            HandsVisible = seen;

            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            statFrames++;
            statMs += ms;
            if (seen > 0) statSeen++;
            float span = Time.realtimeSinceStartup - statT0;
            if (span >= 5f)
            {
                Fps = statFrames / span;
                InferMs = statMs / Mathf.Max(1, statFrames);
                Debug.Log($"[TayUnity] {Fps:0} khung/giây, xử lý {InferMs:0.0} ms/khung, thấy tay {100f * statSeen / Mathf.Max(1, statFrames):0}% khung, webcam {w}x{h}");
                statT0 = Time.realtimeSinceStartup;
                statFrames = statSeen = 0;
                statMs = 0;
            }
        }

        /// <summary>
        /// Lấy mẫu vùng vuông đã xoay vào bộ đệm N×N (0..1, RGB). Tọa độ trong khung vuông chứa ảnh webcam
        /// đã lật ngang như gương, y hướng xuống. Xoay r: hướng "lên" của vùng cắt là (sin r, -cos r).
        /// </summary>
        void Sample(float[] buf, int n, Vector2 c, float size, float r, int w, int h)
        {
            float s = Mathf.Max(w, h), padX = (s - w) * 0.5f, padY = (s - h) * 0.5f;
            float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            int k = 0;
            for (int j = 0; j < n; j++)
            {
                float b = ((j + 0.5f) / n - 0.5f) * size;
                for (int i = 0; i < n; i++)
                {
                    float a = ((i + 0.5f) / n - 0.5f) * size;
                    float u = c.x + a * cs - b * sn, v = c.y + a * sn + b * cs;
                    float x = (1f - u) * s - padX;  // lật ngang
                    float y = v * s - padY;
                    Color32 p = Bilinear(x, h - 1 - y, w, h);
                    buf[k++] = p.r / 255f; buf[k++] = p.g / 255f; buf[k++] = p.b / 255f;
                }
            }
        }

        Color32 Bilinear(float x, float y, int w, int h)
        {
            if (x < 0 || y < 0 || x > w - 1 || y > h - 1) return new Color32(0, 0, 0, 255);
            int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = x - x0, fy = y - y0;
            Color32 a = px[y0 * w + x0], b = px[y0 * w + x1], c = px[y1 * w + x0], d = px[y1 * w + x1];
            return new Color32(
                (byte)((a.r * (1 - fx) + b.r * fx) * (1 - fy) + (c.r * (1 - fx) + d.r * fx) * fy),
                (byte)((a.g * (1 - fx) + b.g * fx) * (1 - fy) + (c.g * (1 - fx) + d.g * fx) * fy),
                (byte)((a.b * (1 - fx) + b.b * fx) * (1 - fy) + (c.b * (1 - fx) + d.b * fx) * fy), 255);
        }

        static float Prob(float v) => v < 0f || v > 1f ? 1f / (1f + Mathf.Exp(-v)) : v;

        void Detect(int w, int h)
        {
            Sample(detBuf, DetN, new Vector2(0.5f, 0.5f), 1f, 0f, w, h);
            detIn.Upload(detBuf);
            detWorker.Schedule(detIn);
            float[] box = (detWorker.PeekOutput(0) as Tensor<float>).DownloadToArray();
            float[] score = (detWorker.PeekOutput(1) as Tensor<float>).DownloadToArray();

            // ứng viên tốt nhất, bỏ trùng; mỗi ứng viên là một bàn tay mới nếu không trùng tay đang theo
            var found = new List<(float s, Vector2 c, float bw, float bh, Vector2 k0, Vector2 k2)>();
            for (int i = 0; i < Anchors; i++)
            {
                float sc = 1f / (1f + Mathf.Exp(-Mathf.Clamp(score[i], -100f, 100f)));
                if (sc < DetThreshold) continue;
                int o = i * 18;
                Vector2 an = anchors[i];
                var c = new Vector2(an.x + box[o] / DetN, an.y + box[o + 1] / DetN);
                found.Add((sc, c, box[o + 2] / DetN, box[o + 3] / DetN,
                    new Vector2(an.x + box[o + 4] / DetN, an.y + box[o + 5] / DetN),
                    new Vector2(an.x + box[o + 8] / DetN, an.y + box[o + 9] / DetN)));
            }
            found.Sort((x, y) => y.s.CompareTo(x.s));
            foreach (var f in found)
            {
                bool dup = false;
                foreach (var t in tracks)
                    if (t.active && Vector2.Distance(t.center, f.c) < t.size * 0.35f) dup = true;
                if (dup) continue;
                var free = tracks[0].active ? tracks[1] : tracks[0];
                if (free.active) break;
                // xoay để cổ tay -> gốc ngón giữa hướng lên; dời tâm về phía ngón, phóng 2,6 lần như MediaPipe
                Vector2 d = f.k2 - f.k0;
                free.rot = Mathf.Atan2(d.x, -d.y);
                Vector2 up = new Vector2(Mathf.Sin(free.rot), -Mathf.Cos(free.rot));
                float side = Mathf.Max(f.bw, f.bh);
                free.center = f.c + up * (0.5f * f.bh);
                free.size = side * 2.6f;
                free.active = true;
                free.misses = 0;
                free.side = null;
                foreach (var t in tracks)
                    if (t != free && t.active && Vector2.Distance(t.center, free.center) < t.size * 0.35f) free.active = false;
            }
        }

        bool Landmarks(Track t, int w, int h)
        {
            Sample(lmBuf, LmN, t.center, t.size, t.rot, w, h);
            lmIn.Upload(lmBuf);
            lmWorker.Schedule(lmIn);
            float[] lm = (lmWorker.PeekOutput(0) as Tensor<float>).DownloadToArray();
            float presence = Prob((lmWorker.PeekOutput(1) as Tensor<float>).DownloadToArray()[0]);
            float handed = Prob((lmWorker.PeekOutput(2) as Tensor<float>).DownloadToArray()[0]);
            float[] wl = (lmWorker.PeekOutput(3) as Tensor<float>).DownloadToArray();
            if (presence < PresenceThreshold) return false;

            float cs = Mathf.Cos(t.rot), sn = Mathf.Sin(t.rot);
            for (int i = 0; i < 21; i++)
            {
                float a = (lm[i * 3] / LmN - 0.5f) * t.size, b = (lm[i * 3 + 1] / LmN - 0.5f) * t.size;
                img[i] = new Vector2(t.center.x + a * cs - b * sn, t.center.y + a * sn + b * cs);
                // điểm 3D theo trục vùng cắt: xoay về trục ảnh rồi đổi sang trục camera Unity (y lên, z ra trước)
                float wx = wl[i * 3], wy = wl[i * 3 + 1], wz = wl[i * 3 + 2];
                world[i] = new Vector3(wx * cs - wy * sn, -(wx * sn + wy * cs), -wz);
            }

            // vùng cắt khung sau theo chính 21 điểm vừa thấy
            Vector2 d = img[9] - img[0];
            float r = Mathf.Atan2(d.x, -d.y);
            float c2 = Mathf.Cos(-r), s2 = Mathf.Sin(-r);
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = -mn;
            foreach (var p in img)
            {
                var q = new Vector2(p.x * c2 - p.y * s2, p.x * s2 + p.y * c2);
                mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q);
            }
            Vector2 mid = (mn + mx) * 0.5f;
            Vector2 center = new Vector2(mid.x * Mathf.Cos(r) - mid.y * Mathf.Sin(r), mid.x * Mathf.Sin(r) + mid.y * Mathf.Cos(r));
            float ext = Mathf.Max(mx.x - mn.x, mx.y - mn.y);
            Vector2 up = new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
            t.rot = r;
            t.center = center + up * (0.1f * ext);
            t.size = Mathf.Clamp(ext * 2.0f, 0.05f, 1.5f);

            // chụm: khoảng cách đầu ngón cái - đầu ngón trỏ so với cỡ lòng bàn tay (không phụ thuộc xa gần), có trễ
            float ratio = (world[4] - world[8]).magnitude / Mathf.Max(1e-4f, (world[0] - world[9]).magnitude);
            t.pinch = t.pinch ? ratio < 0.40f : ratio < 0.28f;

            // bên tay: chốt một lần. Đã có tay kia thì lấy bên còn lại; không thì theo mô hình (ảnh đã lật như
            // gương nên "right" là tay phải người dùng), mô hình lưỡng lự thì theo nửa khung hình
            if (t.side == null)
            {
                var other = t == tracks[0] ? tracks[1] : tracks[0];
                if (other.active && other.side != null) t.side = other.side == "phai" ? "trai" : "phai";
                else if (Mathf.Abs(handed - 0.5f) > 0.3f) t.side = handed >= 0.5f ? "phai" : "trai";
                else t.side = img[0].x >= 0.5f ? "phai" : "trai";
            }
            string ben = t.side;
            var at = ArmTracking.Instance;
            if (at != null) at.FeedHand(ben, world, new Vector2(1f - img[0].x, img[0].y), t.pinch);
            return true;
        }
    }
}
