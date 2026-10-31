using Khkt.Demo;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Tô viền/làm nổi bật vật nằm trong vùng nhìn thô (trái/giữa/phải × trên/giữa/dưới) quanh tâm màn hình.
    /// Tắt mặc định, bật bằng ô trong Inspector của Camera hoặc DesktopViewer.
    /// Không dùng làm con trỏ chính xác (do webcam thông thường sai số vài độ).
    /// </summary>
    public class GazeHighlight : MonoBehaviour
    {
        [Tooltip("Bật hỗ trợ ánh nhìn thô để làm nổi bật vật thể")]
        public bool activeHighlight = false;

        [Tooltip("Tầm nhìn tối đa (mét)")]
        public float maxDistance = 4.0f;

        [Tooltip("Lớp vật thể kiểm tra")]
        public LayerMask targetMask = ~0;

        Camera cam;
        GameObject lastTarget;
        Renderer lastRenderer;
        MaterialPropertyBlock propBlock;
        static readonly int HighlightId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            propBlock = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (!activeHighlight || cam == null || !HeadTracking.Active)
            {
                ClearHighlight();
                return;
            }

            Vector2 gaze = HeadTracking.Gaze;
            // Ánh xạ gaze [-1, 1] sang vùng thô quanh tâm màn hình (khoảng 35% - 65% màn hình)
            float screenX = 0.5f + Mathf.Clamp(gaze.x, -1f, 1f) * 0.15f;
            float screenY = 0.5f + Mathf.Clamp(gaze.y, -1f, 1f) * 0.15f;

            Ray ray = cam.ViewportPointToRay(new Vector3(screenX, screenY, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, targetMask))
            {
                var target = hit.collider.gameObject;
                if (target != lastTarget)
                {
                    ClearHighlight();
                    lastTarget = target;
                    lastRenderer = target.GetComponentInChildren<Renderer>();
                    if (lastRenderer != null)
                    {
                        lastRenderer.GetPropertyBlock(propBlock);
                        propBlock.SetColor(HighlightId, new Color(0.2f, 0.4f, 0.2f));
                        lastRenderer.SetPropertyBlock(propBlock);
                    }
                }
            }
            else
            {
                ClearHighlight();
            }
        }

        void ClearHighlight()
        {
            if (lastRenderer != null)
            {
                lastRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(HighlightId, Color.black);
                lastRenderer.SetPropertyBlock(propBlock);
            }
            lastTarget = null;
            lastRenderer = null;
        }

        void OnDisable() => ClearHighlight();
    }
}
