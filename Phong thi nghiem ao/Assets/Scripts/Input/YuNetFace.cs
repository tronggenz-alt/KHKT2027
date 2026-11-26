using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Dò khuôn mặt bằng YuNet (face_detection_yunet_2023mar, OpenCV Zoo) ngay trong Unity (Inference Engine), không cần
    /// Python: bản đi thi chạy trên máy bật Smart App Control, nơi Python / OpenCV / MediaPipe có thể bị chặn.
    /// Giải mã giống hệt cv2.FaceDetectorYN (đã đối chiếu từng điểm trên 6 ảnh chân dung, docs/16) và cách tính tư thế
    /// giống tools/head_tracker.py (cùng ảnh 320 px), nên các ngưỡng của HeadTracking giữ nguyên.
    /// </summary>
    public sealed class YuNetFace : IDisposable
    {
        public const int W = 320, H = 256;           // ảnh thu nhỏ: bề rộng 320 (như head_tracker.py), cao đệm lên bội của 32
        /// <summary>
        /// Lưới giải mã = khung mô hình. Bản ONNX nhập vào Unity cố định 640×640: ghép thêm lớp đệm chạy trên GPU (Pad) trước
        /// mô hình, CPU chỉ gửi ảnh 320×256 (1 MB thay vì 4,9 MB), phần còn lại đen. Mạng tích chập thuần nên kết quả như
        /// chạy riêng ảnh 320×256 (đã đối chiếu với OpenCV).
        /// </summary>
        readonly int inW, inH;
        readonly int[] outIndex = new int[12];
        public const float ScoreThreshold = 0.7f;
        /// <summary>Mũi nhô trước đường hai mắt khoảng 0,45 lần khoảng cách hai mắt (head_tracker.py, NHO_MUI).</summary>
        const float NoseDepth = 0.45f;
        static readonly int[] Strides = { 8, 16, 32 };

        readonly Worker worker;
        readonly Tensor<float> input;
        readonly float[] buf = new float[3 * W * H];

        /// <summary>5 điểm của mặt vừa dò (ảnh 320 px, chưa lật): mắt phải, mắt trái, mũi, khóe miệng phải, khóe miệng trái của người.</summary>
        public readonly Vector2[] Points = new Vector2[5];
        public float Score { get; private set; }
        public Rect Box { get; private set; }

        YuNetFace(ModelAsset asset, BackendType backend)
        {
            var model = ModelLoader.Load(asset);
            inW = W; inH = H;
            try
            {
                var shp = model.inputs[0].shape;
                if (shp.rank == 4 && shp.IsStatic())
                {
                    var st = shp.ToTensorShape();
                    inH = Mathf.Max(H, st[2]);
                    inW = Mathf.Max(W, st[3]);
                }
            }
            catch (Exception) { }
            for (int i = 0; i < OutNames.Length; i++)
            {
                outIndex[i] = -1;
                for (int k = 0; k < model.outputs.Count; k++) if (model.outputs[k].name == OutNames[i]) outIndex[i] = k;
                if (outIndex[i] < 0) throw new Exception("mô hình YuNet thiếu đầu ra " + OutNames[i]);
            }
            if (inW != W || inH != H)
            {
                var g = new FunctionalGraph();
                var x = g.AddInput<float>(new TensorShape(1, 3, H, W), "anh");
                var padded = x.Pad(new[] { 0, inW - W, 0, inH - H }, 0f);
                model = g.Compile(Functional.Forward(model, padded));  // thứ tự đầu ra giữ như mô hình gốc
            }
            worker = new Worker(model, backend);
            input = new Tensor<float>(new TensorShape(1, 3, H, W));
        }

        /// <summary>Nạp mô hình Resources/face_yunet; không có thì trả null.</summary>
        public static YuNetFace Create(BackendType backend = BackendType.GPUCompute)
        {
            var asset = Resources.Load<ModelAsset>("face_yunet");
            if (asset == null) return null;
            if (!SystemInfo.supportsComputeShaders && backend == BackendType.GPUCompute) backend = BackendType.CPU;
            return new YuNetFace(asset, backend);
        }

        public void Dispose()
        {
            worker?.Dispose();
            input?.Dispose();
        }

        /// <summary>
        /// Dò mặt trên ảnh webcam px (w×h, hàng 0 ở DƯỚI như WebCamTexture.GetPixels32). Thu về bề rộng 320, giữ tỉ lệ,
        /// đệm đen phía dưới. Trả true nếu thấy mặt (lấy mặt điểm cao nhất).
        /// </summary>
        public bool Detect(Color32[] px, int w, int h)
        {
            Fill(px, w, h);
            input.Upload(buf);
            worker.Schedule(input);
            return Decode();
        }

        static readonly string[] OutNames =
        {
            "cls_8", "cls_16", "cls_32", "obj_8", "obj_16", "obj_32", "bbox_8", "bbox_16", "bbox_32", "kps_8", "kps_16", "kps_32",
        };
        readonly Tensor<float>[] pending = new Tensor<float>[OutNames.Length];

        /// <summary>Đang có một lượt dò chưa xong (Submit → TryCollect).</summary>
        public bool Busy { get; private set; }

        /// <summary>Thời gian (ms) ở luồng chính của lượt gần nhất (lấy mẫu chạy ở luồng phụ), để đo.</summary>
        public float FillMs, UploadMs, ScheduleMs, DecodeMs;

        Color32[] pxCopy;
        int copyW, copyH;
        System.Threading.Tasks.Task filling;
        bool scheduled;

        /// <summary>
        /// Bắt đầu một lượt dò, về ngay: chép ảnh (≈0,3 ms) rồi thu nhỏ ở luồng phụ; các khung sau gọi TryCollect để
        /// gửi GPU khi thu nhỏ xong, rồi nhận kết quả khi GPU tính xong (không chờ đồng bộ nào ở luồng chính).
        /// </summary>
        public void Submit(Color32[] px, int w, int h)
        {
            if (Busy) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (pxCopy == null || pxCopy.Length != px.Length) pxCopy = new Color32[px.Length];
            Array.Copy(px, pxCopy, px.Length);
            copyW = w;
            copyH = h;
            filling = System.Threading.Tasks.Task.Run(() => Fill(pxCopy, copyW, copyH));
            scheduled = false;
            Busy = true;
            FillMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>Đi tiếp lượt dò đang chạy; xong thì giải mã (found = thấy mặt) và trả true.</summary>
        public bool TryCollect(out bool found)
        {
            found = false;
            if (!Busy) return false;
            if (!scheduled)
            {
                if (!filling.IsCompleted) return false;
                if (filling.IsFaulted) { Busy = false; throw filling.Exception.GetBaseException(); }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                input.Upload(buf);
                UploadMs = (float)sw.Elapsed.TotalMilliseconds;
                worker.Schedule(input);
                for (int i = 0; i < OutNames.Length; i++)
                {
                    pending[i] = worker.PeekOutput(outIndex[i]) as Tensor<float>;
                    pending[i].ReadbackRequest();
                }
                ScheduleMs = (float)sw.Elapsed.TotalMilliseconds - UploadMs;
                scheduled = true;
                return false;
            }
            foreach (var t in pending)
                if (t != null && !t.IsReadbackRequestDone()) return false;
            Busy = false;
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            found = Decode();
            DecodeMs = (float)sw2.Elapsed.TotalMilliseconds;
            return true;
        }

        int lastW = -1, lastH = -1, lastRows;
        int[] mapX0, mapX1;
        float[] mapFx;

        /// <summary>
        /// Thu ảnh về bề rộng 320 (nội suy song tuyến như cv2.resize), ghi BGR vào bộ đệm vào mô hình. Bảng ánh xạ cột tính
        /// một lần cho mỗi cỡ ảnh; vùng đệm đen chỉ xóa khi cỡ ảnh đổi. Chạy được ở luồng phụ (không gọi API Unity).
        /// </summary>
        void Fill(Color32[] px, int w, int h)
        {
            float s = (float)W / w;
            int hh = Mathf.Min(H, Mathf.RoundToInt(h * s));
            int plane = W * H;
            if (w != lastW || h != lastH)
            {
                Array.Clear(buf, 0, buf.Length);
                mapX0 = new int[W]; mapX1 = new int[W]; mapFx = new float[W];
                for (int x = 0; x < W; x++)
                {
                    float sx = Mathf.Clamp((x + 0.5f) / s - 0.5f, 0, w - 1);
                    mapX0[x] = (int)sx;
                    mapX1[x] = Mathf.Min(mapX0[x] + 1, w - 1);
                    mapFx[x] = sx - mapX0[x];
                }
                lastW = w; lastH = h; lastRows = hh;
            }
            if (w == 2 * W && hh * 2 <= h)
            {
                // webcam 640 px: thu đúng 2:1, nội suy song tuyến tại tâm 2×2 điểm ảnh = trung bình 4 điểm (như cv2.resize)
                for (int y = 0; y < hh; y++)
                {
                    int r0 = (h - 1 - 2 * y) * w, r1 = (h - 2 - 2 * y) * w, o = y * W;
                    for (int x = 0; x < W; x++)
                    {
                        Color32 a = px[r0 + 2 * x], b = px[r0 + 2 * x + 1], c = px[r1 + 2 * x], d = px[r1 + 2 * x + 1];
                        int i = o + x;
                        buf[i] = (a.b + b.b + c.b + d.b) >> 2;
                        buf[plane + i] = (a.g + b.g + c.g + d.g) >> 2;
                        buf[2 * plane + i] = (a.r + b.r + c.r + d.r) >> 2;
                    }
                }
                return;
            }
            for (int y = 0; y < hh; y++)
            {
                // hàng y (từ trên xuống) của ảnh thu nhỏ lấy ở hàng nguồn (y + 0.5)/s - 0.5, đổi sang hàng tính từ dưới
                float sy = h - 1 - Mathf.Clamp((y + 0.5f) / s - 0.5f, 0, h - 1);
                int y0 = (int)sy, y1 = Mathf.Min(y0 + 1, h - 1);
                float fy = sy - y0, gy = 1f - fy;
                int r0 = y0 * w, r1 = y1 * w, o = y * W;
                for (int x = 0; x < W; x++)
                {
                    float fx = mapFx[x], gx = 1f - fx;
                    Color32 a = px[r0 + mapX0[x]], b = px[r0 + mapX1[x]], c = px[r1 + mapX0[x]], d = px[r1 + mapX1[x]];
                    float wa = gx * gy, wb = fx * gy, wc = gx * fy, wd = fx * fy;
                    int i = o + x;
                    // BGR, 0..255, không chuẩn hóa (như cv2.dnn.blobFromImage); làm tròn xuống như bản cũ (byte)
                    buf[i] = (int)(a.b * wa + b.b * wb + c.b * wc + d.b * wd);
                    buf[plane + i] = (int)(a.g * wa + b.g * wb + c.g * wc + d.g * wd);
                    buf[2 * plane + i] = (int)(a.r * wa + b.r * wb + c.r * wc + d.r * wd);
                }
            }
        }

        static Color32 Bilinear(Color32[] px, float x, float y, int w, int h)
        {
            int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = x - x0, fy = y - y0;
            Color32 a = px[y0 * w + x0], b = px[y0 * w + x1], c = px[y1 * w + x0], d = px[y1 * w + x1];
            return new Color32(
                (byte)((a.r * (1 - fx) + b.r * fx) * (1 - fy) + (c.r * (1 - fx) + d.r * fx) * fy),
                (byte)((a.g * (1 - fx) + b.g * fx) * (1 - fy) + (c.g * (1 - fx) + d.g * fx) * fy),
                (byte)((a.b * (1 - fx) + b.b * fx) * (1 - fy) + (c.b * (1 - fx) + d.b * fx) * fy), 255);
        }

        float[] Out(string name) => (worker.PeekOutput(outIndex[Array.IndexOf(OutNames, name)]) as Tensor<float>).DownloadToArray();

        /// <summary>
        /// Giải mã như cv2.FaceDetectorYN::postProcess: điểm = √(cls·obj), hộp và 5 điểm theo ô lưới mỗi bước.
        /// Chỉ tải hộp và 5 điểm của bước lưới có ô điểm cao nhất (bớt khoảng 3/4 bộ nhớ tải về mỗi lần).
        /// </summary>
        bool Decode()
        {
            float best = ScoreThreshold;
            int bestSt = -1, bestI = -1;
            foreach (int st in Strides)
            {
                int n = (inW / st) * (inH / st);
                float[] cls = Out("cls_" + st), obj = Out("obj_" + st);
                for (int i = 0; i < n; i++)
                {
                    float c = cls[i], o = obj[i];
                    if (c <= 0f || o <= 0f) continue;
                    float sc = Mathf.Sqrt(Mathf.Min(c, 1f) * Mathf.Min(o, 1f));
                    if (sc <= best) continue;
                    best = sc; bestSt = st; bestI = i;
                }
            }
            Score = bestSt > 0 ? best : 0f;
            if (bestSt < 0) return false;
            int cols = inW / bestSt, r = bestI / cols, col = bestI % cols;
            float[] bb = Out("bbox_" + bestSt), kp = Out("kps_" + bestSt);
            int i4 = bestI * 4, i10 = bestI * 10;
            float cx = (col + bb[i4]) * bestSt, cy = (r + bb[i4 + 1]) * bestSt;
            float bw = Mathf.Exp(bb[i4 + 2]) * bestSt, bh = Mathf.Exp(bb[i4 + 3]) * bestSt;
            for (int k = 0; k < 5; k++) Points[k] = new Vector2((kp[i10 + 2 * k] + col) * bestSt, (kp[i10 + 2 * k + 1] + r) * bestSt);
            Box = new Rect(cx - bw / 2, cy - bh / 2, bw, bh);
            return true;
        }

        /// <summary>
        /// Tư thế đầu từ 5 điểm, y hệt tu_the() của head_tracker.py: yaw (độ, dương = người quay đầu sang PHẢI của họ),
        /// pitch thô (mũi dưới đường mắt, cúi xuống thì tăng), e (khoảng cách hai mắt đã bù quay / bề rộng ảnh: ghé gần thì tăng).
        /// </summary>
        public static bool Pose(Vector2[] p, out float yaw, out float pitch, out float e, out float roll)
        {
            Vector2 mp = p[0], mt = p[1], nose = p[2];
            Vector2 v = mt - mp;
            float d = v.magnitude;
            yaw = pitch = e = roll = 0f;
            if (d < 1e-6f) return false;
            float tilt = Mathf.Atan2(v.y, v.x);
            float c = Mathf.Cos(-tilt), s = Mathf.Sin(-tilt);
            Vector2 o = nose - (mp + mt) / 2;
            float ox = c * o.x - s * o.y, oy = s * o.x + c * o.y;
            yaw = -Mathf.Rad2Deg * Mathf.Atan2(ox / d, NoseDepth);
            pitch = oy / d;
            e = d / Mathf.Max(1e-6f, Mathf.Cos(yaw * Mathf.Deg2Rad)) / W;
            roll = tilt * Mathf.Rad2Deg;
            return true;
        }
    }

    /// <summary>Bộ lọc One Euro (như tools/webcam_tracker.py): đứng yên lọc mạnh hết rung, chuyển nhanh lọc nhẹ ít trễ.</summary>
    public sealed class OneEuroScalar
    {
        readonly float minCutoff, beta, dCutoff;
        float x, dx, t;
        bool has;

        public OneEuroScalar(float minCutoff, float beta, float dCutoff = 1f)
        {
            this.minCutoff = minCutoff;
            this.beta = beta;
            this.dCutoff = dCutoff;
        }

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }

        public void Reset() => has = false;

        public float Filter(float v, float now)
        {
            if (!has)
            {
                x = v; dx = 0; t = now; has = true;
                return v;
            }
            float dt = Mathf.Max(1e-3f, now - t);
            t = now;
            float ad = Alpha(dCutoff, dt);
            dx = ad * ((v - x) / dt) + (1 - ad) * dx;
            float a = Alpha(minCutoff + beta * Mathf.Abs(dx), dt);
            x = a * v + (1 - a) * x;
            return x;
        }
    }
}
