using Khkt.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Environment
{
    /// <summary>
    /// Xem phòng lab bằng chuột-bàn phím (hoặc tay cầm Wi-Fi ESP32-CAM / tay cầm chơi game, xem PadInput) khi chưa có kính VR.
    ///   Rê chuột           : nhìn quanh (khóa con trỏ, tâm ngắm giữa màn hình)
    ///   Tab                : bật / tắt nhìn theo chuột (tắt thì con trỏ tự do, giữ chuột phải + rê để nhìn)
    ///   Giữ Alt            : tạm thả con trỏ (bấm nút giao diện)
    ///   WASD               : đi (Shift: đi nhanh)
    ///   C                  : ngồi / đứng
    ///   Z / X              : hạ / nâng tầm mắt (cúi xuống đọc vạch buret)
    ///   ← → / PageUp PageDn: xoay / ngẩng cúi bằng phím (khi touchpad bị Windows khóa lúc đang nhấn phím)
    ///   1-9                : dịch chuyển tới các trạm (LabStation, theo thứ tự order); 0: các trạm phụ
    /// Khi một bảng giao diện đang mở (UiCapture > 0), con trỏ tự thả ra và bàn phím không điều khiển đi lại.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DesktopViewer : MonoBehaviour
    {
        public Transform cameraTransform;
        public float walkSpeed = 1.4f;
        public float runMultiplier = 2f;
        public float lookSensitivity = 0.12f;
        public float standingEyeHeight = 1.6f;
        public float seatedEyeHeight = 1.25f;
        public bool seated = true;
        [Tooltip("Tốc độ hạ/nâng tầm mắt bằng Z/X (m/s)")]
        public float eyeAdjustSpeed = 0.25f;
        public Vector2 eyeOffsetRange = new Vector2(-0.5f, 0.5f);
        [Tooltip("Tốc độ xoay bằng phím ← → (độ/giây)")]
        public float keyTurnSpeed = 100f;
        [Tooltip("Nhìn theo chuột (khóa con trỏ) khi bắt đầu")]
        public bool mouseLookOnStart = true;
        [Tooltip("Tốc độ xoay khi đẩy hết cần nhìn của tay cầm (độ/giây)")]
        public float padTurnSpeed = 140f;
        [Tooltip("Tốc độ xoay khi quay đầu hết mức (điều khiển bằng đầu qua webcam, F8) (độ/giây)")]
        public float headTurnSpeed = 70f;

        /// <summary>Đang nhìn theo chuột (con trỏ khóa giữa màn hình).</summary>
        public static bool MouseLook { get; private set; }
        /// <summary>Số bảng giao diện đang mở cần con trỏ (tìm kiếm, trợ lý, menu).</summary>
        public static int UiCapture { get; set; }

        /// <summary>Độ lệch tầm mắt so với tư thế ngồi/đứng chuẩn (m), chỉnh bằng Z/X.</summary>
        public float EyeOffset { get; set; }
        public float Pitch => pitch;
        public LabStation[] Stations => stations;

        CharacterController controller;
        LabStation[] stations;
        float yaw, pitch, verticalSpeed;
        Camera viewCam;
        float baseFov;
        bool lookEnabled;
        int extraStation, padStation = -1;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
            stations = FindObjectsByType<LabStation>();
            System.Array.Sort(stations, (a, b) => a.order.CompareTo(b.order));
            lookEnabled = mouseLookOnStart;
            ApplyCullDistances();
            viewCam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (viewCam != null) baseFov = viewCam.fieldOfView;
            if (FindAnyObjectByType<Khkt.Input.HeadTracking>() == null) gameObject.AddComponent<Khkt.Input.HeadTracking>();
        }

        /// <summary>
        /// Thôi vẽ chi tiết nhỏ khi ở xa (lớp do trình dựng cảnh gán): chữ nhãn lọ quá 6 m (lúc đó chỉ còn vài điểm ảnh),
        /// chữ thường quá 15 m, vật dưới 20 cm quá 14 m. Giảm hàng nghìn lệnh vẽ ở phòng hóa chất và góc nhìn toàn cảnh.
        /// </summary>
        void ApplyCullDistances()
        {
            var cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (cam == null) return;
            var d = new float[32];
            void Cull(string layer, float dist)
            {
                int l = LayerMask.NameToLayer(layer);
                if (l >= 0) d[l] = dist;
            }
            Cull("ChuNho", 6f);
            Cull("ChuVua", 15f);
            Cull("ChiTietNho", 14f);
            cam.layerCullDistances = d;
            cam.layerCullSpherical = true;
        }

        void Update()
        {
            HandleInput(Keyboard.current, Mouse.current);  // máy không có bàn phím / chuột (chỉ tay cầm) vẫn đi được

            if (cameraTransform != null)
            {
                Vector3 p = cameraTransform.localPosition;
                float target = (seated ? seatedEyeHeight : standingEyeHeight) + EyeOffset;
                p.y = Mathf.MoveTowards(p.y, target, 1.5f * Time.deltaTime);
                cameraTransform.localPosition = p;
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            // điều khiển bằng đầu: ghé gần webcam thì thu hẹp góc nhìn (phóng to), ngả xa thì mở rộng
            if (viewCam != null && baseFov > 0)
            {
                float want = baseFov / Khkt.Input.HeadTracking.Zoom;
                if (Mathf.Abs(viewCam.fieldOfView - want) > 0.01f) viewCam.fieldOfView = want;
            }
        }

        void ApplyCursor(bool locked)
        {
            MouseLook = locked;
            if (Application.isBatchMode) return;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void HandleInput(Keyboard kb, Mouse mouse)
        {
            bool ui = UiCapture > 0;
            if (kb != null && mouse != null)
            {
                if (!ui && kb.tabKey.wasPressedThisFrame) lookEnabled = !lookEnabled;
                bool locked = lookEnabled && !ui && !kb.leftAltKey.isPressed && Application.isFocused;
                if (locked != MouseLook || (locked && Cursor.lockState != CursorLockMode.Locked && !Application.isBatchMode)) ApplyCursor(locked);

                if (locked || (!ui && mouse.rightButton.isPressed))
                {
                    Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
                    yaw += d.x;
                    pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
                }
            }
            if (ui) return;  // đang gõ trong bảng giao diện

            // Xoay bằng phím ← → (và PageUp/PageDown để ngẩng/cúi). Laptop dùng touchpad: Windows tạm khóa touchpad
            // khi đang nhấn phím (chống chạm nhầm) nên vừa giữ W vừa rê touchpad sẽ không xoay; phím này luôn xoay được.
            if (kb != null)
            {
                float turn = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
                yaw += turn * keyTurnSpeed * Time.deltaTime;
                float tilt = (kb.pageDownKey.isPressed ? 1 : 0) - (kb.pageUpKey.isPressed ? 1 : 0);
                pitch = Mathf.Clamp(pitch + tilt * keyTurnSpeed * 0.6f * Time.deltaTime, -80f, 80f);
            }

            // Tay cầm: cần nhìn xoay / ngẩng (bình phương để đẩy nhẹ thì ngắm tinh), giữ nút Căn lại hoặc LB / RB để sang khu khác
            Vector2 padLook = PadInput.Look;
            yaw += padLook.x * Mathf.Abs(padLook.x) * padTurnSpeed * Time.deltaTime;
            if (!PadInput.LookYUsedByTool)
                pitch = Mathf.Clamp(pitch - padLook.y * Mathf.Abs(padLook.y) * padTurnSpeed * 0.6f * Time.deltaTime, -80f, 80f);
            // điều khiển bằng đầu (webcam, F8): quay đầu sang bên nào thì góc nhìn xoay liên tục sang bên đó
            yaw += Khkt.Input.HeadTracking.Turn * headTurnSpeed * Time.deltaTime;
            int step = PadInput.StationStep;
            if (step != 0 && stations.Length > 0)
            {
                int n = stations.Length;
                GoTo(stations[((padStation + step) % n + n) % n]);
            }

            if (PadInput.SitPressed)  // tay cầm 1 cần: nhấn nút 2 lần
            {
                seated = !seated;
                EyeOffset = 0;
            }
            Vector2 input = Vector2.zero;
            bool run = PadInput.Shift;
            if (kb != null)
            {
                if (kb.cKey.wasPressedThisFrame)
                {
                    seated = !seated;
                    EyeOffset = 0;
                }
                float eye = (kb.xKey.isPressed ? 1 : 0) - (kb.zKey.isPressed ? 1 : 0);
                EyeOffset = Mathf.Clamp(EyeOffset + eye * eyeAdjustSpeed * Time.deltaTime, eyeOffsetRange.x, eyeOffsetRange.y);
                for (int i = 0; i < stations.Length && i < 9; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) GoTo(stations[i]);
                if (kb.digit0Key.wasPressedThisFrame && stations.Length > 9)  // 0: lần lượt các trạm phụ (bàn giáo viên, khu an toàn)
                    GoTo(stations[9 + extraStation++ % (stations.Length - 9)]);
                input = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                                    (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                run |= kb.leftShiftKey.isPressed;
            }

            input = Vector2.ClampMagnitude(input + PadInput.Move, 1f);
            if (input.sqrMagnitude > 0)
            {
                seated = false;  // đi thì đứng dậy
                EyeOffset = 0;
            }
            float speed = walkSpeed * (run ? runMultiplier : 1f);
            Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y) * speed;
            verticalSpeed = controller.isGrounded ? -0.5f : verticalSpeed - 9.81f * Time.deltaTime;
            move.y = verticalSpeed;
            controller.Move(move * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        public void GoTo(LabStation station)
        {
            int i = System.Array.IndexOf(stations, station);
            if (i >= 0) padStation = i;
            controller.enabled = false;
            transform.position = station.transform.position;
            controller.enabled = true;
            yaw = station.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            pitch = 12f;  // hơi cúi nhìn mặt bàn
            seated = station.seated;
            EyeOffset = 0;
            SnapEye();
        }

        public void GoTo(int order)
        {
            foreach (var s in stations) if (s.order == order) { GoTo(s); return; }
        }

        /// <summary>Dịch chuyển tới đứng trước một điểm (cách dist mét) và nhìn vào nó.</summary>
        public void GoNear(Vector3 target, Vector3 standDirection, float dist = 1.1f)
        {
            Vector3 d = standDirection;
            d.y = 0;
            d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.back;
            controller.enabled = false;
            transform.position = new Vector3(target.x, 0, target.z) + d * dist;
            controller.enabled = true;
            seated = false;
            EyeOffset = 0;
            SnapEye();
            LookAt(target);
        }

        /// <summary>Đưa tầm mắt về ngay độ cao chuẩn (sau khi dịch chuyển).</summary>
        void SnapEye()
        {
            if (cameraTransform == null) return;
            var p = cameraTransform.localPosition;
            p.y = (seated ? seatedEyeHeight : standingEyeHeight) + EyeOffset;
            cameraTransform.localPosition = p;
        }

        /// <summary>Đặt hướng nhìn (dùng cho kiểm thử tự động và nút "nhìn vào buret").</summary>
        public void LookAt(Vector3 worldPoint)
        {
            if (cameraTransform == null) return;
            Vector3 d = worldPoint - cameraTransform.position;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
