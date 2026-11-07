using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Khkt.Input
{
    /// <summary>
    /// Bảng F9: dựng lại người dùng từ số liệu tracking để xem MediaPipe nhận đúng chưa.
    ///  • Hình như soi gương, trùng khung webcam: giơ tay phải thì hình hiện bên phải bảng.
    ///  • Hình nộm đi theo góc đầu, hướng nhìn, chớp mắt trong gói 9051 (số game đang dùng).
    ///  • Lưới xanh và khung xương tay vẽ từ điểm mốc thô ở cổng 9053, chưa qua bộ lọc.
    ///    Hình nộm lệch lưới nghĩa là bước tính góc sai, lưới lệch mặt thật là MediaPipe nhận sai.
    ///  • Chỉ dựng khi mở lần đầu và chỉ vẽ lúc đang mở, tracker cũng chỉ gửi điểm mốc lúc này.
    /// </summary>
    public class TrackingViewer : MonoBehaviour
    {
        [Serializable]
        class HandPoints
        {
            public string ben;
            public int[] d;
        }

        [Serializable]
        class LandmarkPacket
        {
            public float t;
            public float ti_le;
            public int[] mat;
            public HandPoints[] tay;
        }

        class HandVisual
        {
            public string side;
            public GameObject root;
            public Transform[] joints;
            public Transform[] bones;
            public Renderer thumbTip, indexTip;
            public Material normal;
            public Text label;
        }

        public static TrackingViewer Instance { get; private set; }

        const int StageLayer = 31;
        const float StageWidth = 4f / 3f;
        const float WireZ = -0.8f, HandZ = -1f;
        const float LandmarkStaleSec = 0.6f, PacketStaleSec = 1.5f;
        const int RtW = 960, RtH = 720;
        const float ImageW = 640, ImageH = 480;

        // Chỉ số trong DIEM_MAT_RUT_GON của tools/mediapipe_tinh.py
        const int FacePoints = 112, CheekLeft = 8, CheekRight = 28, Forehead = 0, Chin = 18, NoseTip = 111;

        static readonly Vector3 StageOrigin = new Vector3(0, -300, 0);
        static readonly int[] HandBones =
        {
            0, 1, 1, 2, 2, 3, 3, 4,
            0, 5, 5, 6, 6, 7, 7, 8,
            5, 9, 9, 10, 10, 11, 11, 12,
            9, 13, 13, 14, 14, 15, 15, 16,
            13, 17, 17, 18, 18, 19, 19, 20,
            0, 17,
        };

        public bool IsOpen { get; private set; }
        public int LandmarkPackets { get; private set; }
        public string Readout => readout != null ? readout.text : "";
        public string Notice => notice != null ? notice.text : "";
        public Camera StageCamera => stageCam;
        public Quaternion HeadRotation => head != null ? head.localRotation : Quaternion.identity;
        public Vector3 HeadPosition => head != null ? head.localPosition : Vector3.zero;
        public Vector3 HandCenter(string side)
        {
            var h = side == "trai" ? hands[1] : hands[0];
            return h != null && h.root.activeSelf ? h.joints[9].localPosition : Vector3.zero;
        }

        GameObject stage, panel;
        Camera stageCam;
        RenderTexture rt;
        Text readout, notice;
        Transform head, eyeL, eyeR, pupilL, pupilR;
        Mesh wire;
        Vector3[] wireVerts;
        GameObject wireObj;
        readonly HandVisual[] hands = new HandVisual[2];
        Material pinchMat, skinDark, eyeWhite;

        UdpClient socket;
        HeadTracking.Packet lastPacket;
        LandmarkPacket lastLandmarks;
        float lastPacketAt = -100f, lastLandmarkAt = -100f, nextKeepAlive, nextText;

        void Awake()
        {
            Instance = this;
            HeadTracking.OnPacketReceived += OnPacket;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) SetOpen(!IsOpen);
            if (!IsOpen) return;

            float now = Time.realtimeSinceStartup;
            var ht = HeadTracking.Instance;
            if (ht != null && now >= nextKeepAlive)
            {
                nextKeepAlive = now + 1f;
                ht.SendControl("{\"lenh\":\"diem_moc_bat\"}");
            }
            DrainSocket(now);
            UpdateStage(now);
            if (now >= nextText)
            {
                nextText = now + 0.1f;
                UpdateText(now);
            }
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            if (open && stage == null && !Build()) return;
            IsOpen = open;
            stage.SetActive(open);
            panel.SetActive(open);
            stageCam.enabled = open;
            if (open)
            {
                nextKeepAlive = 0f;
                try
                {
                    int port = (HeadTracking.Instance != null ? HeadTracking.Instance.port : 9051) + 2;
                    socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                }
                catch (Exception e)
                {
                    socket = null;
                    Debug.LogWarning("[TrackingViewer] Không mở được cổng điểm mốc: " + e.Message);
                }
            }
            else
            {
                CloseSocket();
                lastLandmarks = null;
                rt.Release();
            }
        }

        void OnPacket(HeadTracking.Packet p)
        {
            if (!IsOpen) return;
            lastPacket = p;
            lastPacketAt = Time.realtimeSinceStartup;
        }

        void DrainSocket(float now)
        {
            if (socket == null) return;
            try
            {
                byte[] newest = null;
                var from = new IPEndPoint(IPAddress.Any, 0);
                while (socket.Available > 0) newest = socket.Receive(ref from);
                if (newest == null) return;
                lastLandmarks = JsonUtility.FromJson<LandmarkPacket>(Encoding.UTF8.GetString(newest));
                lastLandmarkAt = now;
                LandmarkPackets++;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TrackingViewer] Gói điểm mốc lỗi: " + e.Message);
            }
        }

        void CloseSocket()
        {
            if (socket != null) socket.Close();
            socket = null;
        }

        void OnDestroy()
        {
            CloseSocket();
            HeadTracking.OnPacketReceived -= OnPacket;
            if (rt != null) rt.Release();
        }

        // =====================================================================
        // Cập nhật hình
        // =====================================================================

        /// <summary>Tọa độ chuẩn hóa của webcam sang sân khấu: lật ngang như gương, giữ tỉ lệ khung.</summary>
        static Vector3 ToStage(int[] d, int i, float aspect, float z0)
        {
            float s = Mathf.Min(1f, StageWidth / aspect);
            return new Vector3(
                (0.5f - d[i * 3] / 1000f) * aspect * s,
                (0.5f - d[i * 3 + 1] / 1000f) * s,
                z0 + d[i * 3 + 2] / 1000f * aspect * s);
        }

        void UpdateStage(float now)
        {
            bool packetFresh = now - lastPacketAt < PacketStaleSec && lastPacket != null;
            bool lmFresh = now - lastLandmarkAt < LandmarkStaleSec && lastLandmarks != null;
            bool hasFacePoints = lmFresh && lastLandmarks.mat != null && lastLandmarks.mat.Length >= FacePoints * 3;
            bool faceOk = packetFresh && lastPacket.ok != 0;
            float aspect = lmFresh && lastLandmarks.ti_le > 0.1f ? lastLandmarks.ti_le : StageWidth;

            wireObj.SetActive(hasFacePoints);
            if (hasFacePoints)
            {
                for (int i = 0; i < FacePoints; i++) wireVerts[i] = ToStage(lastLandmarks.mat, i, aspect, WireZ);
                wire.vertices = wireVerts;
            }

            head.gameObject.SetActive(faceOk);
            if (faceOk)
            {
                if (hasFacePoints)
                {
                    var cl = wireVerts[CheekLeft];
                    var cr = wireVerts[CheekRight];
                    var mid = (cl + cr + wireVerts[Forehead] + wireVerts[Chin]) * 0.25f;
                    head.localPosition = new Vector3(mid.x, mid.y, 0);
                    // góc yaw lớn làm hai má chụm lại, chiều cao mặt ít đổi hơn
                    float faceH = Vector3.Distance(wireVerts[Forehead], wireVerts[Chin]);
                    head.localScale = Vector3.one * Mathf.Max(0.02f, faceH * 0.72f);
                }
                else
                {
                    // YuNet không có điểm mốc: đặt giữa khung, cỡ theo khoảng cách
                    float dist = lastPacket.dist_cm > 1f ? lastPacket.dist_cm : 60f;
                    head.localPosition = Vector3.zero;
                    head.localScale = Vector3.one * Mathf.Clamp(0.3f * 60f / dist, 0.12f, 0.6f);
                }
                head.localRotation = Quaternion.Euler(-lastPacket.pitch, -lastPacket.yaw, -lastPacket.roll);
                SetEye(eyeL, pupilL, lastPacket.blink_l != 0);
                SetEye(eyeR, pupilR, lastPacket.blink_r != 0);
            }

            var seen = new bool[2];
            if (lmFresh && lastLandmarks.tay != null)
            {
                foreach (var t in lastLandmarks.tay)
                {
                    if (t == null || t.d == null || t.d.Length < 63) continue;
                    int slot = t.ben == "trai" ? 1 : 0;
                    if (seen[slot]) continue;
                    seen[slot] = true;
                    DrawHand(hands[slot], t.d, aspect);
                }
            }
            for (int i = 0; i < 2; i++)
            {
                if (!seen[i])
                {
                    hands[i].root.SetActive(false);
                    hands[i].label.gameObject.SetActive(false);
                }
            }
        }

        void SetEye(Transform eye, Transform pupil, bool closed)
        {
            eye.localScale = closed ? new Vector3(0.22f, 0.025f, 0.2f) : new Vector3(0.22f, 0.19f, 0.2f);
            eye.GetComponent<Renderer>().sharedMaterial = closed ? skinDark : eyeWhite;
            pupil.gameObject.SetActive(!closed);
            pupil.localPosition = eye.localPosition + new Vector3(
                Mathf.Clamp(lastPacket.gaze_x, -1f, 1f) * 0.06f,
                Mathf.Clamp(lastPacket.gaze_y, -1f, 1f) * 0.05f,
                -0.085f);
        }

        void DrawHand(HandVisual h, int[] d, float aspect)
        {
            h.root.SetActive(true);
            var palm = Vector3.Distance(ToStage(d, 0, aspect, HandZ), ToStage(d, 9, aspect, HandZ));
            palm = Mathf.Max(0.02f, palm);
            for (int i = 0; i < 21; i++)
            {
                h.joints[i].localPosition = ToStage(d, i, aspect, HandZ);
                h.joints[i].localScale = Vector3.one * palm * (i == 0 ? 0.2f : 0.13f);
            }
            for (int b = 0; b < h.bones.Length; b++)
            {
                var pa = h.joints[HandBones[b * 2]].localPosition;
                var pb = h.joints[HandBones[b * 2 + 1]].localPosition;
                var dir = pb - pa;
                var tr = h.bones[b];
                tr.localPosition = (pa + pb) * 0.5f;
                tr.localRotation = dir.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, dir) : Quaternion.identity;
                tr.localScale = new Vector3(palm * 0.08f, dir.magnitude * 0.5f, palm * 0.08f);
            }

            var info = FindHand(h.side);
            bool pinch = info != null && info.pinch != 0;
            h.thumbTip.sharedMaterial = pinch ? pinchMat : h.normal;
            h.indexTip.sharedMaterial = pinch ? pinchMat : h.normal;

            // nhãn trái / phải đúng như tracker báo, đặt ở cổ tay
            var w = h.joints[0].localPosition;
            h.label.gameObject.SetActive(true);
            h.label.text = h.side == "trai" ? "Tracker: tay TRÁI" : "Tracker: tay PHẢI";
            ((RectTransform)h.label.transform).anchoredPosition = new Vector2(
                (w.x / StageWidth + 0.5f) * ImageW - 70f,
                (w.y - 0.5f) * ImageH - 10f);
        }

        HeadTracking.HandData FindHand(string side)
        {
            if (lastPacket == null || lastPacket.hands == null) return null;
            foreach (var h in lastPacket.hands)
                if (h != null && h.ben == side) return h;
            return null;
        }

        // =====================================================================
        // Chữ
        // =====================================================================

        static string Dir(float v, float dead, string plus, string minus, string mid) =>
            v > dead ? plus : v < -dead ? minus : mid;

        void UpdateText(float now)
        {
            var ht = HeadTracking.Instance;
            bool packetFresh = now - lastPacketAt < PacketStaleSec && lastPacket != null;
            bool lmFresh = now - lastLandmarkAt < LandmarkStaleSec && lastLandmarks != null;
            var sb = new StringBuilder(512);

            string note = "";
            if (ht == null || !packetFresh)
            {
                note = !string.IsNullOrEmpty(lastPacket?.loi) && packetFresh ? "Lỗi webcam: " + lastPacket.loi
                     : "Chưa có dữ liệu.\nNhấn F8 để bật tracker và webcam.";
            }
            else if (lastPacket.ok == 0 && !lmFresh) note = "Không thấy mặt trong khung webcam";
            notice.text = note;
            notice.gameObject.SetActive(note.Length > 0);

            if (packetFresh)
            {
                var p = lastPacket;
                bool mp = p.nguon == "mp";
                sb.Append($"<b>Nguồn</b>  {(mp ? "MediaPipe" : "YuNet")} · {p.fps:0} FPS\n");
                if (!mp) sb.Append("<color=#FFC860>YuNet không có điểm mốc: chỉ có góc đầu</color>\n");
                sb.Append("\n");
                if (p.ok != 0)
                {
                    sb.Append("<b>Đầu</b>\n");
                    sb.Append($"Yaw  {p.yaw:+0;-0;0}°  {Dir(p.yaw, 8, "quay phải", "quay trái", "nhìn thẳng")}\n");
                    sb.Append($"Pitch  {p.pitch:+0;-0;0}°  {Dir(p.pitch, 8, "cúi", "ngẩng", "ngang")}\n");
                    sb.Append($"Roll  {p.roll:+0;-0;0}°  {Dir(p.roll, 8, "nghiêng phải", "nghiêng trái", "thẳng")}\n");
                    sb.Append($"Cách webcam  {p.dist_cm:0} cm\n\n");
                    sb.Append("<b>Mắt</b>\n");
                    sb.Append($"Nhìn  {Dir(p.gaze_x, 0.25f, "phải", "trái", "giữa")}, {Dir(p.gaze_y, 0.25f, "lên", "xuống", "giữa")}  ({p.gaze_x:+0.00;-0.00}, {p.gaze_y:+0.00;-0.00})\n");
                    sb.Append($"Mắt trái  {(p.blink_l != 0 ? "<color=#FFC860>nhắm</color>" : "mở")} · phải  {(p.blink_r != 0 ? "<color=#FFC860>nhắm</color>" : "mở")}\n\n");
                }
                else sb.Append("<color=#FFC860>Không thấy mặt</color>\n\n");

                sb.Append("<b>Tay</b>\n");
                AppendHand(sb, "phai", "Phải");
                AppendHand(sb, "trai", "Trái");
                sb.Append("\n");
                AppendCrossCheck(sb, lmFresh);
            }
            else sb.Append("Đợi gói tin từ tracker…\n");
            sb.Append($"\n<size=13>Điểm mốc nhận được: {LandmarkPackets}</size>");
            readout.text = sb.ToString();
        }

        void AppendHand(StringBuilder sb, string side, string name)
        {
            var h = FindHand(side);
            if (h == null || h.ok == 0)
            {
                sb.Append($"{name}: không thấy\n");
                return;
            }
            string gesture = string.IsNullOrEmpty(h.cu_chi) ? "chưa rõ cử chỉ" : $"{h.cu_chi} ({h.tin:0.00})";
            sb.Append($"{name}: {gesture}{(h.pinch != 0 ? " · <color=#FF7A7A>chụm</color>" : "")}\n");
        }

        /// <summary>Hai nguồn độc lập phải cùng chiều: góc yaw từ ma trận và vị trí mũi so với hai má.</summary>
        void AppendCrossCheck(StringBuilder sb, bool lmFresh)
        {
            if (!lmFresh || lastPacket.ok == 0 || lastLandmarks.mat == null || lastLandmarks.mat.Length < FacePoints * 3) return;
            var cl = wireVerts[CheekLeft];
            var cr = wireVerts[CheekRight];
            float half = Mathf.Max(1e-4f, Mathf.Abs(cl.x - cr.x) * 0.5f);
            float offset = (wireVerts[NoseTip].x - (cl.x + cr.x) * 0.5f) / half;
            if (Mathf.Abs(offset) > 0.15f && Mathf.Abs(lastPacket.yaw) > 8f && Mathf.Sign(offset) != Mathf.Sign(lastPacket.yaw))
                sb.Append("<color=#FF7A7A>Yaw ngược chiều với điểm mốc</color>\n");
            else
                sb.Append("<color=#7CE8A0>Góc đầu khớp điểm mốc</color>\n");
        }

        // =====================================================================
        // Dựng
        // =====================================================================

        bool Build()
        {
            var ui = FindAnyObjectByType<ModernUI>();
            if (ui == null || ui.Canvas == null) return false;

            rt = new RenderTexture(RtW, RtH, 24) { name = "Tracking F9", antiAliasing = 2 };
            stage = new GameObject("Sân khấu tracking");
            stage.transform.position = StageOrigin;
            stage.layer = StageLayer;

            var camGo = new GameObject("Camera tracking");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = new Vector3(0, 0, -3f);
            stageCam = camGo.AddComponent<Camera>();
            stageCam.orthographic = true;
            stageCam.orthographicSize = 0.5f;
            stageCam.nearClipPlane = 0.1f;
            stageCam.farClipPlane = 6f;
            stageCam.cullingMask = 1 << StageLayer;
            stageCam.clearFlags = CameraClearFlags.SolidColor;
            stageCam.backgroundColor = new Color(0.035f, 0.055f, 0.09f, 1f);
            stageCam.targetTexture = rt;
            stageCam.allowHDR = false;
            var extra = stageCam.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.renderShadows = false;
            extra.volumeLayerMask = 0;

            if (ui.viewCamera != null) ui.viewCamera.cullingMask &= ~(1 << StageLayer);

            var lightGo = new GameObject("Đèn tracking");
            lightGo.transform.SetParent(stage.transform, false);
            lightGo.transform.localPosition = new Vector3(-0.5f, 0.7f, -2f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 6f;
            l.intensity = 3f;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << StageLayer;

            BuildFrame();
            BuildHead();
            BuildWire();
            pinchMat = Lit(new Color(1f, 0.25f, 0.25f));
            BuildPanel(ui);
            hands[0] = BuildHand("phai", new Color(1f, 0.62f, 0.2f));
            hands[1] = BuildHand("trai", new Color(0.35f, 0.9f, 0.5f));
            stage.SetActive(false);
            panel.SetActive(false);
            stageCam.enabled = false;
            return true;
        }

        static Material Lit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.35f);
            return m;
        }

        /// <summary>Màu tự phát sáng, không phụ thuộc đèn. Bản .exe không có shader URP/Unlit nên dùng Lit.</summary>
        static Material Glow(Color c)
        {
            var m = Lit(Color.black);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        /// <summary>Lưới đường không có pháp tuyến thì Lit tính ra NaN: cho tất cả nhìn về phía camera.</summary>
        static Vector3[] FacingNormals(int count)
        {
            var n = new Vector3[count];
            for (int i = 0; i < count; i++) n[i] = Vector3.back;
            return n;
        }

        GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.layer = StageLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        void BuildHead()
        {
            var pivot = new GameObject("Hình nộm");
            pivot.layer = StageLayer;
            head = pivot.transform;
            head.SetParent(stage.transform, false);

            var skin = Lit(new Color(0.86f, 0.72f, 0.62f));
            skinDark = Lit(new Color(0.55f, 0.4f, 0.34f));
            eyeWhite = Lit(new Color(0.96f, 0.96f, 0.96f));
            var hair = Lit(new Color(0.13f, 0.11f, 0.12f));
            var iris = Lit(new Color(0.08f, 0.14f, 0.28f));
            var ray = Glow(new Color(0.38f, 0.86f, 1f));

            Prim(PrimitiveType.Sphere, head, "Sọ", new Vector3(0, 0f, 0.12f), new Vector3(1.05f, 1.4f, 1.1f), skin);
            Prim(PrimitiveType.Sphere, head, "Tóc", new Vector3(0, 0.16f, 0.24f), new Vector3(1.1f, 1.22f, 1.02f), hair);
            Prim(PrimitiveType.Sphere, head, "Mũi", new Vector3(0, -0.04f, -0.44f), new Vector3(0.13f, 0.24f, 0.3f), skinDark);
            Prim(PrimitiveType.Cube, head, "Miệng", new Vector3(0, -0.4f, -0.34f), new Vector3(0.3f, 0.035f, 0.1f), skinDark);
            var line = Prim(PrimitiveType.Cylinder, head, "Hướng đầu", new Vector3(0, -0.04f, -0.9f), new Vector3(0.018f, 0.36f, 0.018f), ray);
            line.transform.localRotation = Quaternion.Euler(90, 0, 0);

            // Soi gương: mắt trái của người dùng nằm bên trái bảng (x âm)
            eyeL = Prim(PrimitiveType.Sphere, head, "Mắt trái", new Vector3(-0.2f, 0.16f, -0.38f), Vector3.one * 0.2f, eyeWhite).transform;
            eyeR = Prim(PrimitiveType.Sphere, head, "Mắt phải", new Vector3(0.2f, 0.16f, -0.38f), Vector3.one * 0.2f, eyeWhite).transform;
            pupilL = Prim(PrimitiveType.Sphere, head, "Tròng trái", eyeL.localPosition, Vector3.one * 0.085f, iris).transform;
            pupilR = Prim(PrimitiveType.Sphere, head, "Tròng phải", eyeR.localPosition, Vector3.one * 0.085f, iris).transform;
        }

        static void AddLoop(System.Collections.Generic.List<int> idx, int first, int count, bool closed)
        {
            for (int i = 0; i < count - 1; i++)
            {
                idx.Add(first + i);
                idx.Add(first + i + 1);
            }
            if (closed)
            {
                idx.Add(first + count - 1);
                idx.Add(first);
            }
        }

        void BuildWire()
        {
            wireObj = new GameObject("Lưới điểm mốc");
            wireObj.layer = StageLayer;
            wireObj.transform.SetParent(stage.transform, false);
            wire = new Mesh { name = "Lưới mặt", hideFlags = HideFlags.DontSave };
            wire.MarkDynamic();
            wireVerts = new Vector3[FacePoints];
            wire.vertices = wireVerts;
            wire.normals = FacingNormals(FacePoints);
            wire.subMeshCount = 2;

            var lines = new System.Collections.Generic.List<int>();
            AddLoop(lines, 0, 36, true);      // viền mặt
            AddLoop(lines, 36, 16, true);     // mắt trái
            AddLoop(lines, 52, 16, true);     // mắt phải
            AddLoop(lines, 76, 5, false);     // mày trái
            AddLoop(lines, 81, 5, false);     // mày phải
            AddLoop(lines, 86, 20, true);     // môi
            AddLoop(lines, 106, 6, false);    // sống mũi
            wire.SetIndices(lines, MeshTopology.Lines, 0);

            var iris = new System.Collections.Generic.List<int>();
            AddLoop(iris, 68, 4, true);
            AddLoop(iris, 72, 4, true);
            wire.SetIndices(iris, MeshTopology.Lines, 1);
            wire.bounds = new Bounds(Vector3.zero, Vector3.one * 8f);

            wireObj.AddComponent<MeshFilter>().sharedMesh = wire;
            var r = wireObj.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { Glow(new Color(0.3f, 0.95f, 1f)), Glow(new Color(1f, 0.9f, 0.25f)) };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void BuildFrame()
        {
            // Lưới mờ chia ba để dễ thấy lệch trái, phải, trên, dưới so với khung webcam
            var go = new GameObject("Lưới khung");
            go.layer = StageLayer;
            go.transform.SetParent(stage.transform, false);
            float hw = StageWidth / 2f;
            var v = new System.Collections.Generic.List<Vector3>();
            var idx = new System.Collections.Generic.List<int>();
            void Seg(Vector3 a, Vector3 b)
            {
                idx.Add(v.Count); v.Add(a);
                idx.Add(v.Count); v.Add(b);
            }
            for (int i = 1; i < 3; i++)
            {
                float x = -hw + StageWidth * i / 3f;
                float y = -0.5f + i / 3f;
                Seg(new Vector3(x, -0.5f, 1f), new Vector3(x, 0.5f, 1f));
                Seg(new Vector3(-hw, y, 1f), new Vector3(hw, y, 1f));
            }
            var m = new Mesh { name = "Lưới khung", hideFlags = HideFlags.DontSave };
            m.SetVertices(v);
            m.normals = FacingNormals(v.Count);
            m.SetIndices(idx, MeshTopology.Lines, 0);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Glow(new Color(0.16f, 0.24f, 0.34f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        HandVisual BuildHand(string side, Color color)
        {
            var root = new GameObject("Tay " + side);
            root.layer = StageLayer;
            root.transform.SetParent(stage.transform, false);
            var h = new HandVisual
            {
                side = side,
                root = root,
                normal = Lit(color),
                joints = new Transform[21],
                bones = new Transform[HandBones.Length / 2],
            };
            var boneMat = Lit(Color.Lerp(color, Color.white, 0.35f));
            for (int i = 0; i < 21; i++)
                h.joints[i] = Prim(PrimitiveType.Sphere, root.transform, "Khớp " + i, Vector3.zero, Vector3.one * 0.01f, h.normal).transform;
            for (int b = 0; b < h.bones.Length; b++)
                h.bones[b] = Prim(PrimitiveType.Cylinder, root.transform, "Xương " + b, Vector3.zero, Vector3.one * 0.01f, boneMat).transform;
            h.thumbTip = h.joints[4].GetComponent<Renderer>();
            h.indexTip = h.joints[8].GetComponent<Renderer>();

            h.label = GlassKit.Label(imageRoot, "Nhãn " + side, "", 15, color, true, TextAnchor.UpperCenter, Vector2.zero, new Vector2(140, 22));
            h.label.gameObject.SetActive(false);
            root.SetActive(false);
            return h;
        }

        Transform imageRoot;

        void BuildPanel(ModernUI ui)
        {
            var glass = GlassKit.Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"));
            var img = GlassKit.Panel(ui.Canvas.transform, "Bảng tracking", new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                                     new Vector2(-24, 0), new Vector2(984, 566), glass, 1.4f);
            panel = img.gameObject;
            var p = panel.transform;
            // nền tối thêm để chữ đọc được cả khi phía sau là tường sáng
            var plate = GlassKit.Panel(p, "Nền tối", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(984, 566), null, 1.4f);
            plate.color = new Color(0.03f, 0.05f, 0.09f, 0.78f);
            GlassKit.Label(p, "Tiêu đề", "Mô phỏng tracking", 22, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(20, -14), new Vector2(380, 30));
            GlassKit.Label(p, "Phím", "F9 đóng · F12 căn lại", 15, GlassKit.Ink, false, TextAnchor.UpperRight, new Vector2(-260, -20), new Vector2(240, 24), new Vector2(1, 1));

            var imgRt = GlassKit.Rect(p, "Hình", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -52), new Vector2(ImageW, ImageH));
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;
            imageRoot = imgRt;

            GlassKit.Label(p, "Chú giải", "Như soi gương · Hình nộm: số game dùng · Lưới xanh: điểm mốc thô", 14, GlassKit.Ink, false,
                           TextAnchor.UpperLeft, new Vector2(20, -536), new Vector2(ImageW, 22));
            readout = GlassKit.Label(p, "Số liệu", "", 16, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(680, -52), new Vector2(284, 490));
            notice = GlassKit.Label(imgRt, "Thông báo", "", 20, GlassKit.Ink, true, TextAnchor.MiddleCenter, new Vector2(60, -190), new Vector2(ImageW - 120, 100));
            notice.gameObject.SetActive(false);
        }
    }
}
