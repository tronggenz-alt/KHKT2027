using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Khkt.Demo.Bio.ThucVat
{
    /// <summary>
    /// Camera quỹ đạo dùng chung cho các mô-đun 3D sân khấu riêng (G3 Thực vật & G4 Giải phẫu).
    /// Hỗ trợ: kéo chuột xoay, cuộn để phóng to/thu nhỏ, bấm chọn & nhấp đúp lấy nét bộ phận, Esc để đóng.
    /// </summary>
    public class CameraQuyDao : MonoBehaviour
    {
        public Camera TargetCamera;
        public LayerMask PickLayerMask = ~0;
        public float MinDistance = 0.25f;
        public float MaxDistance = 6.0f;
        public float MinPitch = -65f;
        public float MaxPitch = 80f;
        public float SmoothTime = 8f;
        /// <summary>Bật thì chỉ chuột phải (hoặc giữa) mới xoay; chuột trái để mô-đun dùng riêng (bàn mổ: rạch bằng dao). Mặc định tắt.</summary>
        public bool ChiXoayChuotPhai;

        public event Action<Collider> OnPartClicked;
        public event Action<Collider> OnPartDoubleClicked;
        public event Action OnEscapePressed;

        Vector3 targetPivot;
        Vector3 currentPivot;
        float targetYaw = 28f;
        float currentYaw = 28f;
        float targetPitch = 20f;
        float currentPitch = 20f;
        float targetDistance = 1.65f;
        float currentDistance = 1.65f;

        bool dragging;
        bool dragStartedOnUi;
        float dragAccumPixels;
        Vector2 lastMousePos;
        float lastClickTime = -10f;
        Collider lastClickCol;

        public Vector3 Pivot => targetPivot;
        public float Yaw => targetYaw;
        public float Pitch => targetPitch;
        public float Distance => targetDistance;

        void Awake()
        {
            if (TargetCamera == null) TargetCamera = GetComponent<Camera>();
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

        public void FocusOn(Bounds bounds, float distanceMultiplier = 2.0f, bool instant = false)
        {
            float rad = Mathf.Max(0.06f, bounds.extents.magnitude);
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

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                OnEscapePressed?.Invoke();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && TargetCamera != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

                bool nhan = ChiXoayChuotPhai ? (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                                             : (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
                bool giu = ChiXoayChuotPhai ? (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                                            : (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
                bool tha = ChiXoayChuotPhai ? (mouse.rightButton.wasReleasedThisFrame || mouse.middleButton.wasReleasedThisFrame)
                                            : (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame);
                if (nhan)
                {
                    dragging = !overUi;
                    dragStartedOnUi = overUi;
                    dragAccumPixels = 0f;
                    lastMousePos = pos;
                }

                if (dragging && giu)
                {
                    Vector2 delta = pos - lastMousePos;
                    lastMousePos = pos;
                    dragAccumPixels += delta.magnitude;
                    if (delta.sqrMagnitude > 0.0001f)
                    {
                        targetYaw += delta.x * 0.32f;
                        targetPitch = Mathf.Clamp(targetPitch - delta.y * 0.28f, MinPitch, MaxPitch);
                    }
                }

                if (tha && dragging)
                {
                    dragging = false;
                    if (!ChiXoayChuotPhai && !dragStartedOnUi && mouse.leftButton.wasReleasedThisFrame && dragAccumPixels < 6f)
                        HandleClick(pos);
                }

                if (!overUi)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        float step = Mathf.Sign(scroll) * Mathf.Clamp(Mathf.Abs(scroll) * 0.001f, 0.04f, 0.25f) * targetDistance;
                        targetDistance = Mathf.Clamp(targetDistance - step, MinDistance, MaxDistance);
                    }
                }
            }

            float k = 1f - Mathf.Exp(-SmoothTime * Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
            currentPivot = Vector3.Lerp(currentPivot, targetPivot, k);
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, k);
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
                FocusOn(hit.collider.bounds, 2.3f, instant: false);
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
