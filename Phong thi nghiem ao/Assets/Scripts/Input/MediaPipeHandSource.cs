using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Input
{
    /// <summary>
    /// Nguồn tay cầm từ cử chỉ tay MediaPipe (Camera webcam), kế thừa ControllerSource.
    /// Nhận mảng dữ liệu hands từ cùng gói UDP 9051 qua HeadTracking.
    ///
    /// Ánh xạ cử chỉ:
    ///   - Chụm ngón cái + trỏ (pinch)       -> Trigger: cầm / thả
    ///   - Di chuyển bàn tay khi đang chụm     -> Joy1: chỉnh tinh vị trí bàn tay ảo
    ///   - Đưa tay lại gần / ra xa webcam     -> Độ sâu (HandDepth / DepthOffset)
    ///   - Open_Palm giữ 1 s                   -> Recenter: căn lại hướng
    ///   - Thumb_Up giữ 1 s                    -> AskAI: hỏi trợ lý AI
    ///   - Closed_Fist                         -> Thả vật / hủy
    ///
    /// Phím nóng:
    ///   - F10: Bật / tắt theo dõi tay
    ///   - F11: Bật / tắt HUD trạng thái MediaPipe
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class MediaPipeHandSource : ControllerSource
    {
        public static MediaPipeHandSource Instance { get; private set; }

        [Header("Cấu hình")]
        public bool handTrackingEnabled = true;
        public float joySensitivity = 4.0f;
        public float depthSensitivity = 2.5f;
        public float handTimeoutSec = 0.3f;
        public bool showHud = false;

        /// <summary>Cánh tay ảo (ArmRig) đang điều khiển bàn tay: thôi phát cần điều khiển ảo từ cử chỉ.</summary>
        public static bool SuppressController { get; set; }

        public static float HandDepth => Instance != null ? Instance.depthOffset : 0f;
        public static bool HandActive => Instance != null && Instance.IsConnected;

        bool isConnected;
        float lastHandTime = -10f;
        Vector2 anchorPos;
        bool wasPinching = false;
        Vector2 joy1;
        float calibratedCo = 0.15f;
        bool coCalibrated = false;
        float depthOffset;

        float openPalmSince = -1f;
        bool openPalmFired = false;
        float thumbUpSince = -1f;
        bool thumbUpFired = false;

        uint timeMs;
        byte seq;
        string currentGesture = "None";
        float gestureConfidence = 0f;
        bool isPinching = false;
        string handSide = "phai";

        HeadTracking.HandData trackedHand = new HeadTracking.HandData();

        public override bool IsConnected => isConnected;
        public override string ControllerName => "MediaPipe Hand";
        public override string StatusText => isConnected 
            ? $"Tay {handSide}: {currentGesture} (pinch: {(isPinching ? 1 : 0)})" 
            : (handTrackingEnabled ? "Chưa thấy tay" : "Tắt (F10)");

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void OnEnable()
        {
            PadInput.Register(this);
            HeadTracking.OnHandsReceived += HandleHandsReceived;
        }

        void OnDisable()
        {
            PadInput.Unregister(this);
            HeadTracking.OnHandsReceived -= HandleHandsReceived;
            ResetState();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void HandleHandsReceived(HeadTracking.HandData[] hands, double timestamp)
        {
            if (hands == null || hands.Length == 0) return;

            // Ưu tiên tay phải hoặc tay đầu tiên hợp lệ
            HeadTracking.HandData primary = null;
            for (int i = 0; i < hands.Length; i++)
            {
                if (hands[i] != null && hands[i].ok == 1)
                {
                    if (primary == null || hands[i].ben == "phai")
                    {
                        primary = hands[i];
                        if (primary.ben == "phai") break;
                    }
                }
            }

            if (primary != null)
            {
                trackedHand = primary;
                lastHandTime = Time.unscaledTime;
                currentGesture = primary.cu_chi ?? "None";
                gestureConfidence = primary.tin;
                isPinching = primary.pinch == 1;
                handSide = primary.ben ?? "phai";

                if (!coCalibrated && primary.co > 0.05f)
                {
                    calibratedCo = primary.co;
                    coCalibrated = true;
                }
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // F10 nay do InAppHandTracker giữ (bật webcam và nhận diện tay trong Unity)
                if (false)
                {
                    handTrackingEnabled = !handTrackingEnabled;
                    Debug.Log($"[MediaPipeHand] Theo dõi tay: {(handTrackingEnabled ? "BẬT" : "TẮT")}");
                }
                if (kb.f11Key.wasPressedThisFrame)
                {
                    showHud = !showHud;
                }
            }

            // Tạm ngưng khi giao diện màn hình máy hoặc khu chế tạo đang mở
            bool uiOpen = Demo.Instruments.InstrumentScreen.IsOpen ||
                          (Assembly.FabricationWorkspace.Instance != null && Assembly.FabricationWorkspace.Instance.IsOpen);

            bool handRecent = (Time.unscaledTime - lastHandTime) <= handTimeoutSec;
            bool shouldBeConnected = handTrackingEnabled && !uiOpen && handRecent && trackedHand.ok == 1 && !SuppressController;

            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            timeMs += (uint)(dt * 1000f);

            if (shouldBeConnected)
            {
                isConnected = true;
                ProcessGestureState(dt);
            }
            else
            {
                if (isConnected)
                {
                    ResetState();
                }
            }
        }

        void ProcessGestureState(float dt)
        {
            var buttons = ControllerButtons.None;

            // 1. Pinch: chụm ngón cái + trỏ -> Trigger
            bool pinch = trackedHand.pinch == 1;
            if (pinch)
            {
                buttons |= ControllerButtons.Trigger;
                if (!wasPinching)
                {
                    anchorPos = new Vector2(trackedHand.x, trackedHand.y);
                    wasPinching = true;
                }

                // Di chuyển bàn tay khi đang chụm -> Joy1
                // Chú ý: webcam x: 0..1 (trái sang phải), y: 0..1 (trên xuống dưới)
                // Joy1.x: phải dương, Joy1.y: trên dương
                float dx = (trackedHand.x - anchorPos.x) * joySensitivity;
                float dy = -(trackedHand.y - anchorPos.y) * joySensitivity;
                joy1 = new Vector2(Mathf.Clamp(dx, -1f, 1f), Mathf.Clamp(dy, -1f, 1f));
            }
            else
            {
                wasPinching = false;
                joy1 = Vector2.zero;
            }

            // 2. Closed_Fist -> hủy / thả vật
            if (currentGesture == "Closed_Fist")
            {
                buttons &= ~ControllerButtons.Trigger;
                wasPinching = false;
                joy1 = Vector2.zero;
            }

            // 3. Open_Palm giữ >= 1 s -> Recenter
            if (currentGesture == "Open_Palm")
            {
                if (openPalmSince < 0) openPalmSince = Time.unscaledTime;
                else if (Time.unscaledTime - openPalmSince >= 1.0f && !openPalmFired)
                {
                    buttons |= ControllerButtons.Recenter;
                    openPalmFired = true;
                }
            }
            else
            {
                openPalmSince = -1f;
                openPalmFired = false;
            }

            // 4. Thumb_Up giữ >= 1 s -> AskAI
            if (currentGesture == "Thumb_Up")
            {
                if (thumbUpSince < 0) thumbUpSince = Time.unscaledTime;
                else if (Time.unscaledTime - thumbUpSince >= 1.0f && !thumbUpFired)
                {
                    buttons |= ControllerButtons.AskAI;
                    thumbUpFired = true;
                }
            }
            else
            {
                thumbUpSince = -1f;
                thumbUpFired = false;
            }

            // 5. Độ sâu (xa/gần webcam)
            if (coCalibrated && trackedHand.co > 0.02f)
            {
                depthOffset = Mathf.Clamp((trackedHand.co - calibratedCo) * depthSensitivity, -0.5f, 0.5f);
            }

            Publish(new ControllerState
            {
                TimeMs = timeMs,
                Seq = seq++,
                Joy1 = joy1,
                Joy2 = Vector2.zero,
                Buttons = buttons,
                OrientationRH = Quaternion.identity
            });
        }

        void ResetState()
        {
            isConnected = false;
            wasPinching = false;
            joy1 = Vector2.zero;
            openPalmSince = -1f;
            openPalmFired = false;
            thumbUpSince = -1f;
            thumbUpFired = false;
            depthOffset = 0f;

            Publish(new ControllerState
            {
                TimeMs = timeMs,
                Seq = seq++,
                Joy1 = Vector2.zero,
                Joy2 = Vector2.zero,
                Buttons = ControllerButtons.None,
                OrientationRH = Quaternion.identity
            });
        }

        public void CalibrateNeutral()
        {
            if (trackedHand.ok == 1 && trackedHand.co > 0.05f)
            {
                calibratedCo = trackedHand.co;
                coCalibrated = true;
            }
        }

        public override void SetVibration(int level) { }
        public override void Pulse(int level, int ms) { }
        public override void SetLeds(bool green, bool red) { }
        public override void ResetOrientation() { }

        // ----- HUD Hiển thị trạng thái (F11) -----
        void OnGUI()
        {
            if (!showHud) return;

            int w = 260;
            int h = 180;
            int x = Screen.width - w - 20;
            int y = 20;

            GUI.Box(new Rect(x, y, w, h), "MediaPipe HUD (F11)");

            int py = y + 26;
            int lh = 18;

            string engine = HeadTracking.Engine;
            string htStatus = HeadTracking.Instance != null ? HeadTracking.Instance.Status : "Tắt";
            GUI.Label(new Rect(x + 10, py, w - 20, lh), $"Webcam: {htStatus} [{engine}]"); py += lh;

            float dist = HeadTracking.DistanceCm;
            Vector2 gaze = HeadTracking.Gaze;
            bool headOk = HeadTracking.Active;
            GUI.Label(new Rect(x + 10, py, w - 20, lh), $"Mặt: {(headOk ? "Thấy" : "Mất")} · Dist: {dist:0.0}cm · Gaze: ({gaze.x:+0.00;-0.00},{gaze.y:+0.00;-0.00})"); py += lh;

            GUI.Label(new Rect(x + 10, py, w - 20, lh), $"Tay: {(isConnected ? "Đang bắt" : handTrackingEnabled ? "Tìm kiếm..." : "Tắt (F10)")}"); py += lh;
            if (isConnected)
            {
                GUI.Label(new Rect(x + 10, py, w - 20, lh), $"Bên: {handSide} · Cử chỉ: {currentGesture} ({gestureConfidence:P0})"); py += lh;
                GUI.Label(new Rect(x + 10, py, w - 20, lh), $"Pinch: {(isPinching ? "CHỤM (Trigger)" : "Mở")} · Joy1: ({joy1.x:+0.00;-0.00},{joy1.y:+0.00;-0.00})"); py += lh;
                GUI.Label(new Rect(x + 10, py, w - 20, lh), $"Độ sâu (co): {trackedHand.co:0.00} (Δ {depthOffset:+0.00;-0.00})"); py += lh;
            }
            else
            {
                GUI.Label(new Rect(x + 10, py, w - 20, lh), "Đưa tay vào khung webcam để điều khiển"); py += lh;
            }

            GUI.Label(new Rect(x + 10, py, w - 20, lh), "F8: Đầu · F9: Mô phỏng · F10: Tay · F11: HUD · F12: Căn");
        }
    }
}
