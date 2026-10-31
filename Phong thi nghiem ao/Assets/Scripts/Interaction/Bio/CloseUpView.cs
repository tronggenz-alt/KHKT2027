using Khkt.Environment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Chế độ góc nhìn cận cảnh (Close-Up View) khi thao tác tỉ mỉ trên bàn thí nghiệm sinh học
    /// (làm tiêu bản hiển vi, ria cấy 4 vùng trên đĩa Petri, nạp mẫu vào giếng gel điện di, đọc màn hình thiết bị).
    /// Nhấn Esc để thoát về góc nhìn người chơi bình thường.
    /// </summary>
    public class CloseUpView : MonoBehaviour
    {
        public static CloseUpView Instance { get; private set; }
        public static bool IsActive => Instance != null && Instance.active;

        bool active;
        string currentTitle = "";
        Transform camTransform;
        Vector3 savedLocalPos;
        Quaternion savedLocalRot;
        Vector3 startWorldPos, targetWorldPos;
        Quaternion startWorldRot, targetWorldRot;
        float transitionT = 1f;

        GUIStyle barStyle;
        Texture2D barBg;

        public static CloseUpView Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<CloseUpView>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var go = new GameObject("BioCloseUpView");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CloseUpView>();
            return Instance;
        }

        public static void Enter(Vector3 cameraWorldPos, Vector3 lookAtWorldPos, string title)
        {
            var inst = Ensure();
            var cam = Camera.main;
            if (cam == null) return;

            if (!inst.active)
            {
                inst.camTransform = cam.transform;
                inst.savedLocalPos = inst.camTransform.localPosition;
                inst.savedLocalRot = inst.camTransform.localRotation;
            }

            inst.active = true;
            inst.currentTitle = title ?? "Quan sát cận cảnh";
            inst.startWorldPos = inst.camTransform.position;
            inst.startWorldRot = inst.camTransform.rotation;
            inst.targetWorldPos = cameraWorldPos;
            Vector3 dir = lookAtWorldPos - cameraWorldPos;
            inst.targetWorldRot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : inst.camTransform.rotation;
            inst.transitionT = 0f;
            BioFeedback.PlayClick();
        }

        public static void Exit()
        {
            if (Instance == null || !Instance.active) return;
            Instance.active = false;
            if (Instance.camTransform != null)
            {
                Instance.camTransform.localPosition = Instance.savedLocalPos;
                Instance.camTransform.localRotation = Instance.savedLocalRot;
            }
            BioFeedback.PlayClick();
        }

        void Update()
        {
            if (!active || camTransform == null) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Exit();
                return;
            }

            if (transitionT < 1f)
            {
                transitionT = Mathf.MoveTowards(transitionT, 1f, Time.unscaledDeltaTime / 0.32f);
                float e = Easing.Standard(transitionT);
                camTransform.position = Vector3.Lerp(startWorldPos, targetWorldPos, e);
                camTransform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRot, e);
            }
            else
            {
                camTransform.position = targetWorldPos;
                camTransform.rotation = targetWorldRot;
            }
        }

        void OnGUI()
        {
            if (!active) return;
            if (barStyle == null)
            {
                barBg = new Texture2D(2, 2);
                var c = new Color(0.06f, 0.20f, 0.14f, 0.90f);
                barBg.SetPixels(new[] { c, c, c, c });
                barBg.Apply();
                barStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = barBg, textColor = new Color(0.85f, 0.98f, 0.90f) }
                };
            }

            float w = 520f, h = 34f;
            GUI.Box(new Rect((Screen.width - w) * 0.5f, 16f, w, h),
                $"Góc nhìn cận cảnh: {currentTitle}  ·  [Esc] để lùi ra", barStyle);
        }
    }
}
