using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Khkt.Input
{
    /// <summary>
    /// Bộ đọc cảm ứng đa điểm trên iPad:
    ///   • Ngón trái dưới: cần di chuyển nổi (nạp vào PadInput.Move).
    ///   • Ngón nửa phải (không trúng nút): vuốt xoay góc nhìn tuyến tính (nạp vào PadInput.LookDelta).
    ///   • Ngón chạm vào nút giao diện: thuộc về nút đó, không xoay góc nhìn.
    ///   • Đa điểm thật: vừa đi, vừa vuốt nhìn, vừa giữ nút tay cùng lúc.
    ///   • Tự bật khi có cảm ứng, trên iOS, hoặc khi biến môi trường KHKT_CAM_UNG=1.
    /// Đọc thẳng Touchscreen, không qua EnhancedTouch: trên iPad thật EnhancedTouch báo lỗi
    /// InputUpdateType.None lúc nhận màn hình cảm ứng (gói App UI bật nó từ trước).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class TouchControls : MonoBehaviour
    {
        public TouchHud hud;
        public float lookSensitivity = 0.18f;
        public float joystickRadius = 70f;

        enum TouchRole { Joystick, Look, UI }

        readonly Dictionary<int, TouchRole> fingerRoles = new Dictionary<int, TouchRole>();
        readonly Dictionary<int, Vector2> lastLookPos = new Dictionary<int, Vector2>();
        readonly HashSet<int> downNow = new HashSet<int>();
        readonly List<int> lifted = new List<int>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        int joystickFingerId = -1;
        Vector2 joystickOrigin;
        bool enabledByTouch;

        public bool IsTouchActive => Application.isMobilePlatform || enabledByTouch ||
                                     System.Environment.GetEnvironmentVariable("KHKT_CAM_UNG") == "1" ||
                                     PlayerPrefs.GetInt("KHKT_CAM_UNG", 0) == 1;

        void Awake()
        {
            float savedSens = PlayerPrefs.GetFloat("KHKT_TOUCH_SENSITIVITY", -1f);
            if (savedSens > 0.01f) lookSensitivity = savedSens;
        }

        void OnEnable()
        {
            if (IsTouchActive) PadInput.UsingTouch = true;
        }

        void OnDisable()
        {
            PadInput.UsingTouch = false;
            ResetTouchState();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ResetTouchState();
        }

        public void SetLookSensitivity(float s)
        {
            lookSensitivity = Mathf.Clamp(s, 0.05f, 0.5f);
            PlayerPrefs.SetFloat("KHKT_TOUCH_SENSITIVITY", lookSensitivity);
        }

        void Update()
        {
            downNow.Clear();
            var screen = Touchscreen.current;
            if (screen != null)
            {
                // Giữa hai khung hình ngón có thể vừa đặt vừa rê, nên xét theo mã ngón chứ không chờ pha Began
                foreach (var t in screen.touches)
                {
                    var phase = t.phase.ReadValue();
                    if (phase == TouchPhase.None || phase == TouchPhase.Ended || phase == TouchPhase.Canceled) continue;
                    int fid = t.touchId.ReadValue();
                    Vector2 pos = t.position.ReadValue();
                    downNow.Add(fid);
                    if (fingerRoles.ContainsKey(fid)) Drag(fid, pos);
                    else Begin(fid, pos);
                }
            }
            if (downNow.Count > 0) enabledByTouch = true;

            if (!IsTouchActive)
            {
                if (PadInput.UsingTouch) PadInput.UsingTouch = false;
                return;
            }
            PadInput.UsingTouch = true;

            lifted.Clear();
            foreach (var fid in fingerRoles.Keys)
                if (!downNow.Contains(fid)) lifted.Add(fid);
            foreach (var fid in lifted) End(fid);
        }

        void Begin(int fid, Vector2 pos)
        {
            TouchRole role;
            if (IsOverUi(pos))
            {
                role = TouchRole.UI;
            }
            else if (joystickFingerId < 0 && pos.x < Screen.width * 0.45f && pos.y < Screen.height * 0.55f)
            {
                role = TouchRole.Joystick;
                joystickFingerId = fid;
                joystickOrigin = pos;
                if (hud != null) hud.ShowJoystick(pos);
            }
            else
            {
                role = TouchRole.Look;
                lastLookPos[fid] = pos;
            }
            fingerRoles[fid] = role;
        }

        void Drag(int fid, Vector2 pos)
        {
            var role = fingerRoles[fid];
            if (role == TouchRole.Joystick)
            {
                Vector2 diff = pos - joystickOrigin;
                float dist = diff.magnitude;
                float maxR = joystickRadius * (Screen.dpi > 0 ? Screen.dpi / 160f : 1f);
                maxR = Mathf.Max(40f, maxR);
                Vector2 norm = dist > 5f ? diff / maxR : Vector2.zero;
                if (norm.magnitude > 1f) norm.Normalize();
                PadInput.TouchMove = norm;
                if (hud != null) hud.UpdateJoystick(joystickOrigin + Vector2.ClampMagnitude(diff, maxR));
            }
            else if (role == TouchRole.Look)
            {
                // Đang mở bảng giao diện thì DesktopViewer không đọc: cộng dồn sẽ làm góc nhìn giật khi đóng bảng
                if (DesktopViewer.UiCapture == 0 && lastLookPos.TryGetValue(fid, out var prev))
                    PadInput.LookDelta += (pos - prev) * lookSensitivity;
                lastLookPos[fid] = pos;
            }
        }

        void End(int fid)
        {
            if (fid == joystickFingerId)
            {
                joystickFingerId = -1;
                PadInput.TouchMove = Vector2.zero;
                if (hud != null) hud.HideJoystick();
            }
            fingerRoles.Remove(fid);
            lastLookPos.Remove(fid);
        }

        void ResetTouchState()
        {
            fingerRoles.Clear();
            lastLookPos.Clear();
            joystickFingerId = -1;
            PadInput.TouchMove = Vector2.zero;
            PadInput.TouchUse = false;
            PadInput.TouchShift = false;
            PadInput.TouchSwirl = false;
            PadInput.TouchBackPressed = false;
            PadInput.TouchRecordPressed = false;
            PadInput.TouchHelpPressed = false;
            PadInput.TouchSitPressed = false;
            PadInput.TouchStationStep = 0;
            PadInput.TouchStopcockAxis = 0;
            PadInput.LookDelta = Vector2.zero;
            if (hud != null) hud.HideJoystick();
        }

        /// <summary>Dò trực tiếp vị trí ngón: trạng thái con trỏ của EventSystem chưa kịp cập nhật ở khung hình đầu.</summary>
        bool IsOverUi(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, uiHits);
            return uiHits.Count > 0;
        }
    }
}
