using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Khkt.Demo.Bio.ThucVat
{
    /// <summary>
    /// Vùng bắt tương tác chuột & MacBook Trackpad trực tiếp trên nền Viewport 3D:
    ///   • Chặn hoàn toàn sự kiện chuột lọt xuống MainMenu/Dashboard bên dưới khi mở mô-đun từ Menu chính.
    ///   • Nhận đầy đủ sự kiện Kéo (OnDrag), Cuộn/Vuốt 2 ngón Trackpad (OnScroll) và Nhấp chọn (OnPointerClick).
    /// </summary>
    public class Viewport3DInputZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        public CameraQuyDao OrbitCamera;

        public void OnPointerDown(PointerEventData eventData)
        {
            OrbitCamera?.NotifyZonePointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            OrbitCamera?.NotifyZoneDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OrbitCamera?.NotifyZoneEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            OrbitCamera?.NotifyZoneScroll(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OrbitCamera?.NotifyZoneClick(eventData);
        }
    }

    /// <summary>
    /// Camera quỹ đạo 360° dùng chung cho các mô-đun 3D sân khấu riêng (G3 Thực vật & G4 Giải phẫu).
    /// Hỗ trợ đầy đủ Chuột & MacBook Trackpad:
    ///   • Kéo chuột trái / chuột phải / kéo 1 ngón hoặc 3 ngón trên Trackpad: Xoay 360° tự do (Yaw không giới hạn, Pitch -85°..+85°).
    ///   • Cuộn chuột / vuốt 2 ngón dọc trên Trackpad: Zoom In & Zoom Out mượt mà.
    ///   • Vuốt 2 ngón ngang trên Trackpad: Xoay 360° ngang (hoặc giữ Option/Alt/Cmd + vuốt 2 ngón để xoay 360° mọi hướng).
    ///   • Kéo chuột giữa (hoặc giữ Shift + kéo chuột trái): Trượt tâm nhìn (Pan).
    ///   • Phím + / - để Zoom In / Out, phím WASD / Mũi tên để xoay 360°.
    /// </summary>
    public class CameraQuyDao : MonoBehaviour
    {
        public Camera TargetCamera;
        public LayerMask PickLayerMask = ~0;
        public float MinDistance = 0.12f;
        public float MaxDistance = 6.0f;
        public float MinPitch = -85f;
        public float MaxPitch = 85f;
        public float SmoothTime = 12f;
        public bool AutoRotate;
        public float AutoRotateSpeedDeg = 22f;
        public int MinBlockingCanvasSortOrder = 150;

        public event Action<Collider> OnPartClicked;
        public event Action<Collider> OnPartDoubleClicked;
        public event Action OnEscapePressed;

        Vector3 targetPivot;
        Vector3 currentPivot;
        float targetYaw = 24f;
        float currentYaw = 24f;
        float targetPitch = 18f;
        float currentPitch = 18f;
        float targetDistance = 1.45f;
        float currentDistance = 1.45f;

        bool draggingOrbit;
        bool draggingPan;
        bool dragStartedOnUi;
        float dragAccumPixels;
        Vector2 lastMousePos;
        float lastClickTime = -10f;
        Collider lastClickCol;
        int lastZoneDragFrame = -1;
        int lastZoneScrollFrame = -1;
        int lastZoneClickFrame = -1;

        static readonly List<RaycastResult> s_raycastHits = new List<RaycastResult>(16);

        public Vector3 Pivot => targetPivot;
        public float Yaw => targetYaw;
        public float Pitch => targetPitch;
        public float Distance => targetDistance;

        void Awake()
        {
            if (TargetCamera == null) TargetCamera = GetComponent<Camera>();
        }

        /// <summary>
        /// Tạo tấm nền trong suốt ở lớp dưới cùng của Canvas mô-đun để bắt trọn thao tác Chuột & Trackpad
        /// trên vùng 3D và chặn sự kiện lọt xuống MainMenu phía sau.
        /// </summary>
        public Viewport3DInputZone AttachViewportZone(Canvas moduleCanvas)
        {
            if (moduleCanvas == null) return null;
            var go = new GameObject("VungTuongTac3D_Viewport", typeof(RectTransform), typeof(Image), typeof(Viewport3DInputZone));
            go.layer = moduleCanvas.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(moduleCanvas.transform, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;

            var zone = go.GetComponent<Viewport3DInputZone>();
            zone.OrbitCamera = this;
            return zone;
        }

        public void SetTarget(Vector3 pivot, float yawDeg, float pitchDeg, float distance, bool instant = false)
        {
            targetPivot = pivot;
            targetYaw = yawDeg;
            targetPitch = Mathf.Clamp(pitchDeg, MinPitch, MaxPitch);
            targetDistance = Mathf.Clamp(distance, MinDistance, MaxDistance);
            if (instant)
            {
                currentPivot = targetPivot;
                currentYaw = targetYaw;
                currentPitch = targetPitch;
                currentDistance = targetDistance;
                ApplyTransformNow();
            }
        }

        public void RotateBy(float deltaYawDeg, float deltaPitchDeg)
        {
            targetYaw += deltaYawDeg;
            targetPitch = Mathf.Clamp(targetPitch + deltaPitchDeg, MinPitch, MaxPitch);
        }

        public void SetAnglePreset(float yawDeg, float pitchDeg)
        {
            float delta = Mathf.DeltaAngle(currentYaw, yawDeg);
            targetYaw = currentYaw + delta;
            targetPitch = Mathf.Clamp(pitchDeg, MinPitch, MaxPitch);
        }

        public void ZoomBy(float factor)
        {
            targetDistance = Mathf.Clamp(targetDistance * factor, MinDistance, MaxDistance);
        }

        public void FocusOn(Bounds bounds, float distanceMultiplier = 2.0f, bool instant = false)
        {
            float rad = Mathf.Max(0.05f, bounds.extents.magnitude);
            float dist = Mathf.Clamp(rad * distanceMultiplier, MinDistance, MaxDistance);
            targetPivot = bounds.center;
            targetDistance = dist;
            if (instant)
            {
                currentPivot = targetPivot;
                currentDistance = targetDistance;
                ApplyTransformNow();
            }
        }

        // ==================== SỰ KIỆN TRỰC TIẾP TỪ VIEWPORT 3D (CHUỘT & TRACKPAD) ====================

        public void NotifyZonePointerDown(PointerEventData e)
        {
            dragAccumPixels = 0f;
        }

        public void NotifyZoneDrag(PointerEventData e)
        {
            lastZoneDragFrame = Time.frameCount;
            Vector2 delta = e.delta;
            dragAccumPixels += delta.magnitude;
            if (delta.sqrMagnitude < 0.0001f) return;

            var kb = Keyboard.current;
            bool shiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            bool isPan = e.button == PointerEventData.InputButton.Middle || (e.button == PointerEventData.InputButton.Left && shiftHeld);

            if (isPan && TargetCamera != null)
            {
                float panScale = targetDistance * 0.0014f;
                Vector3 right = TargetCamera.transform.right;
                Vector3 up = TargetCamera.transform.up;
                targetPivot -= (right * delta.x + up * delta.y) * panScale;
            }
            else
            {
                targetYaw += delta.x * 0.38f;
                targetPitch = Mathf.Clamp(targetPitch - delta.y * 0.32f, MinPitch, MaxPitch);
            }
        }

        public void NotifyZoneEndDrag(PointerEventData e)
        {
            draggingOrbit = false;
            draggingPan = false;
        }

        public void NotifyZoneScroll(PointerEventData e)
        {
            lastZoneScrollFrame = Time.frameCount;
            ApplyScrollOrTrackpadGesture(e.scrollDelta);
        }

        public void NotifyZoneClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (dragAccumPixels >= 7f) return;
            lastZoneClickFrame = Time.frameCount;
            HandleClick(e.position);
        }

        void ApplyScrollOrTrackpadGesture(Vector2 scroll)
        {
            var kb = Keyboard.current;
            bool altOrCmd = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed || kb.leftMetaKey.isPressed || kb.rightMetaKey.isPressed);
            // Chuẩn hoá biên độ giữa cuộn chuột rời (±120) và vuốt 2 ngón Trackpad macOS (liên tục)
            float normX = Mathf.Abs(scroll.x) > 20f ? scroll.x / 120f : scroll.x;
            float normY = Mathf.Abs(scroll.y) > 20f ? scroll.y / 120f : scroll.y;

            // Nếu giữ Option/Alt hoặc Command khi vuốt 2 ngón trên Trackpad -> xoay 360° cả Yaw & Pitch
            if (altOrCmd)
            {
                targetYaw -= Mathf.Clamp(normX * 6.5f, -18f, 18f);
                targetPitch = Mathf.Clamp(targetPitch + Mathf.Clamp(normY * 5.2f, -14f, 14f), MinPitch, MaxPitch);
                return;
            }

            // Vuốt 2 ngón ngang trên Trackpad -> xoay ngang 360° (Yaw)
            if (Mathf.Abs(normX) > 0.01f && Mathf.Abs(normX) > Mathf.Abs(normY) * 0.65f)
            {
                targetYaw -= Mathf.Clamp(normX * 5.8f, -16f, 16f);
            }

            // Cuộn chuột hoặc vuốt 2 ngón dọc trên Trackpad -> Zoom In / Zoom Out mượt mà
            if (Mathf.Abs(normY) > 0.005f)
            {
                float mag = Mathf.Clamp(Mathf.Abs(normY) * 0.11f, 0.012f, 0.18f);
                float factor = normY > 0f ? (1f - mag) : (1f + mag);
                targetDistance = Mathf.Clamp(targetDistance * factor, MinDistance, MaxDistance);
            }
        }

        /// <summary>
        /// Kiểm tra con trỏ có đang nằm trên bảng điều khiển/nút bấm của mô-đun hiện tại hay không.
        /// Bỏ qua các Canvas tầng thấp phía sau (như MainMenu sortingOrder = 0) và bỏ qua tấm Viewport3DInputZone.
        /// </summary>
        bool IsPointerOverBlockingUi(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var ped = new PointerEventData(EventSystem.current) { position = screenPos };
            s_raycastHits.Clear();
            EventSystem.current.RaycastAll(ped, s_raycastHits);

            for (int i = 0; i < s_raycastHits.Count; i++)
            {
                var hitGo = s_raycastHits[i].gameObject;
                if (hitGo == null) continue;
                if (hitGo.GetComponent<Viewport3DInputZone>() != null)
                    return false; // Đang trỏ trực tiếp vào vùng sân khấu 3D

                var cv = hitGo.GetComponentInParent<Canvas>();
                if (cv != null && cv.rootCanvas != null && cv.rootCanvas.sortingOrder < MinBlockingCanvasSortOrder)
                    continue; // Bỏ qua MainMenu / Dashboard nằm bên dưới mô-đun 3D

                return true;
            }
            return false;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    OnEscapePressed?.Invoke();
                    return;
                }
                // Phím mũi tên / WASD để xoay 360°
                float keyYaw = 0f, keyPitch = 0f;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) keyYaw -= 95f * dt;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) keyYaw += 95f * dt;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) keyPitch += 75f * dt;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) keyPitch -= 75f * dt;
                if (Mathf.Abs(keyYaw) > 0.001f || Mathf.Abs(keyPitch) > 0.001f)
                    RotateBy(keyYaw, keyPitch);

                // Phím + / = và - / _ để Zoom In / Zoom Out nhanh
                if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed)
                    ZoomBy(1f - 0.85f * dt);
                if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed)
                    ZoomBy(1f + 0.85f * dt);
            }

            var mouse = Mouse.current;
            if (mouse != null && TargetCamera != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                bool overBlockingUi = IsPointerOverBlockingUi(pos);
                bool shiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);

                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                {
                    dragStartedOnUi = overBlockingUi;
                    if (!overBlockingUi)
                    {
                        bool panBtn = mouse.middleButton.wasPressedThisFrame || (mouse.leftButton.wasPressedThisFrame && shiftHeld);
                        draggingPan = panBtn;
                        draggingOrbit = !panBtn;
                        dragAccumPixels = 0f;
                        lastMousePos = pos;
                    }
                }

                if ((draggingOrbit || draggingPan) && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed))
                {
                    Vector2 delta = pos - lastMousePos;
                    lastMousePos = pos;
                    if (lastZoneDragFrame != Time.frameCount)
                    {
                        dragAccumPixels += delta.magnitude;
                        if (delta.sqrMagnitude > 0.0001f)
                        {
                            if (draggingPan)
                            {
                                float panScale = targetDistance * 0.0014f;
                                Vector3 right = TargetCamera.transform.right;
                                Vector3 up = TargetCamera.transform.up;
                                targetPivot -= (right * delta.x + up * delta.y) * panScale;
                            }
                            else
                            {
                                targetYaw += delta.x * 0.38f;
                                targetPitch = Mathf.Clamp(targetPitch - delta.y * 0.32f, MinPitch, MaxPitch);
                            }
                        }
                    }
                }

                if (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame || mouse.middleButton.wasReleasedThisFrame)
                {
                    if ((draggingOrbit || draggingPan) && !dragStartedOnUi && mouse.leftButton.wasReleasedThisFrame && dragAccumPixels < 7f && lastZoneClickFrame != Time.frameCount)
                        HandleClick(pos);

                    if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed)
                    {
                        draggingOrbit = false;
                        draggingPan = false;
                    }
                }

                if (!overBlockingUi && lastZoneScrollFrame != Time.frameCount)
                {
                    Vector2 scroll = mouse.scroll.ReadValue();
                    if (scroll.sqrMagnitude > 0.0001f)
                        ApplyScrollOrTrackpadGesture(scroll);
                }
            }

            if (AutoRotate && !draggingOrbit && !draggingPan && lastZoneDragFrame != Time.frameCount)
            {
                targetYaw += AutoRotateSpeedDeg * dt;
            }

            float k = 1f - Mathf.Exp(-SmoothTime * dt);
            currentPivot = Vector3.Lerp(currentPivot, targetPivot, k);
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, k);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, k);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, k);

            ApplyTransformNow();
        }

        void HandleClick(Vector2 screenPos)
        {
            if (TargetCamera == null) return;
            Ray ray = TargetCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 50f, PickLayerMask)) return;

            float now = Time.unscaledTime;
            bool dbl = (now - lastClickTime) <= 0.36f && hit.collider == lastClickCol;
            lastClickTime = now;
            lastClickCol = hit.collider;

            OnPartClicked?.Invoke(hit.collider);
            if (dbl)
            {
                FocusOn(hit.collider.bounds, 2.15f, instant: false);
                OnPartDoubleClicked?.Invoke(hit.collider);
            }
        }

        public void ApplyTransformNow()
        {
            if (TargetCamera == null) return;
            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 camPos = currentPivot - (rot * Vector3.forward) * currentDistance;
            TargetCamera.transform.SetPositionAndRotation(camPos, rot);
        }
    }
}
