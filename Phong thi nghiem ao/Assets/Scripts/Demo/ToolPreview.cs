using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Khkt.Demo
{
    /// <summary>
    /// Chụp ảnh xem trước 3D nền trong suốt (768×768) cho dụng cụ, chai hóa chất và máy trong bảng chi tiết.
    /// Cô lập tạm thời đối tượng mục tiêu sang layer 30 trong try/finally để luôn khôi phục layer gốc.
    /// </summary>
    public static class ToolPreview
    {
        const int TextureSize = 768;
        const int PreviewLayer = 30;
        const float PreviewFov = 26f;

        static GameObject studioGo;
        static Camera previewCam;
        static Light keyLight;
        static Light fillLight;
        static RenderTexture rt;
        static float lastRenderTime = -999f;

        static readonly List<(Transform tr, int layer)> SavedLayers = new List<(Transform tr, int layer)>(64);
        static readonly HashSet<Transform> SeenTransforms = new HashSet<Transform>();
        static readonly List<Renderer> TempRenderers = new List<Renderer>(32);

        public static Texture Texture => rt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Release();
            lastRenderTime = -999f;
        }

        public static bool ShouldRefresh(float interval = 0.2f)
        {
            if (rt == null) return true;
            return Time.unscaledTime - lastRenderTime >= interval;
        }

        public static Texture Render(GameObject target, float yawDeg = -28f, float pitchDeg = 24f, GameObject extra = null)
        {
            if (target == null) return null;

            if (!TryComputeBounds(target, extra, out Bounds bounds))
                return null;

            EnsureStudio();
            if (previewCam == null || rt == null) return null;

            var rot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            float radius = Mathf.Max(0.03f, bounds.extents.magnitude);
            float halfFovRad = PreviewFov * 0.5f * Mathf.Deg2Rad;
            float halfTan = Mathf.Tan(halfFovRad);

            // Chiếu 8 đỉnh AABB lên trục phải/lên của camera để đối tượng luôn chiếm ~75% khung vuông bất kể tỷ lệ cao/rộng
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 ext = bounds.extents;
            float maxRight = Mathf.Abs(right.x) * ext.x + Mathf.Abs(right.y) * ext.y + Mathf.Abs(right.z) * ext.z;
            float maxUp = Mathf.Abs(up.x) * ext.x + Mathf.Abs(up.y) * ext.y + Mathf.Abs(up.z) * ext.z;
            float maxDepth = Mathf.Abs(fwd.x) * ext.x + Mathf.Abs(fwd.y) * ext.y + Mathf.Abs(fwd.z) * ext.z;

            float projHalf = Mathf.Max(0.02f, Mathf.Max(maxRight, maxUp));
            float projDist = projHalf / (halfTan * 0.76f) + maxDepth * 0.25f;
            float sphereDist = (radius / Mathf.Sin(halfFovRad)) * 0.96f;
            float dist = Mathf.Max(projDist, sphereDist);

            previewCam.fieldOfView = PreviewFov;
            previewCam.aspect = 1f;
            previewCam.transform.SetPositionAndRotation(bounds.center - fwd * dist, rot);
            previewCam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 1.8f);
            previewCam.farClipPlane = dist + radius * 2.4f;
            previewCam.targetTexture = rt;

            if (keyLight != null)
                keyLight.transform.rotation = rot * Quaternion.Euler(28f, -34f, 0f);
            if (fillLight != null)
                fillLight.transform.rotation = rot * Quaternion.Euler(-16f, 44f, 0f);

            SavedLayers.Clear();
            SeenTransforms.Clear();
            PushLayerRecursive(target.transform);
            if (extra != null) PushLayerRecursive(extra.transform);

            try
            {
                if (keyLight != null) keyLight.enabled = true;
                if (fillLight != null) fillLight.enabled = true;
                previewCam.Render();
                lastRenderTime = Time.unscaledTime;
            }
            finally
            {
                if (keyLight != null) keyLight.enabled = false;
                if (fillLight != null) fillLight.enabled = false;
                previewCam.targetTexture = null;
                for (int i = SavedLayers.Count - 1; i >= 0; i--)
                {
                    var entry = SavedLayers[i];
                    if (entry.tr != null) entry.tr.gameObject.layer = entry.layer;
                }
                SavedLayers.Clear();
                SeenTransforms.Clear();
            }

            return rt;
        }

        public static void Release()
        {
            if (previewCam != null) previewCam.targetTexture = null;
            if (rt != null)
            {
                rt.Release();
                DestroyObj(rt);
                rt = null;
            }
            if (studioGo != null)
            {
                DestroyObj(studioGo);
                studioGo = null;
            }
            previewCam = null;
            keyLight = null;
            fillLight = null;
            SavedLayers.Clear();
            SeenTransforms.Clear();
        }

        static bool TryComputeBounds(GameObject target, GameObject extra, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            AccumulateBounds(target, ref has, ref bounds);
            if (extra != null) AccumulateBounds(extra, ref has, ref bounds);
            return has && bounds.size.sqrMagnitude > 1e-8f;
        }

        static void AccumulateBounds(GameObject root, ref bool has, ref Bounds bounds)
        {
            if (root == null) return;
            TempRenderers.Clear();
            root.GetComponentsInChildren(false, TempRenderers);
            for (int i = 0; i < TempRenderers.Count; i++)
            {
                var r = TempRenderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer) continue;
                if (r.GetComponent<TextMesh>() != null) continue;
                var rb = r.bounds;
                if (rb.size.sqrMagnitude < 1e-9f) continue;
                if (!has) { bounds = rb; has = true; }
                else bounds.Encapsulate(rb);
            }
            TempRenderers.Clear();
        }

        static void PushLayerRecursive(Transform root)
        {
            if (root == null) return;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var tr = all[i];
                if (tr == null || !SeenTransforms.Add(tr)) continue;
                SavedLayers.Add((tr, tr.gameObject.layer));
                tr.gameObject.layer = PreviewLayer;
            }
        }

        static void EnsureStudio()
        {
            if (rt == null)
            {
                rt = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
                {
                    name = "ToolPreview_RT",
                    antiAliasing = 2,
                    useMipMap = false,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                rt.Create();
            }

            if (studioGo == null)
            {
                studioGo = new GameObject("ToolPreview Studio")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var camGo = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
                camGo.transform.SetParent(studioGo.transform, false);
                previewCam = camGo.AddComponent<Camera>();
                previewCam.enabled = false;
                previewCam.orthographic = false;
                previewCam.fieldOfView = PreviewFov;
                previewCam.aspect = 1f;
                previewCam.clearFlags = CameraClearFlags.SolidColor;
                previewCam.backgroundColor = Color.clear;
                previewCam.cullingMask = 1 << PreviewLayer;
                previewCam.allowHDR = false;
                previewCam.allowMSAA = true;

                var urp = previewCam.GetUniversalAdditionalCameraData();
                if (urp != null)
                {
                    urp.renderPostProcessing = false;
                    urp.renderShadows = false;
                    urp.volumeLayerMask = 0;
                    urp.antialiasing = AntialiasingMode.None;
                }

                var keyGo = new GameObject("KeyLight") { hideFlags = HideFlags.HideAndDontSave };
                keyGo.transform.SetParent(studioGo.transform, false);
                keyLight = keyGo.AddComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.color = new Color(1f, 0.98f, 0.95f, 1f);
                keyLight.intensity = 1.15f;
                keyLight.shadows = LightShadows.None;
                keyLight.cullingMask = 1 << PreviewLayer;
                keyLight.enabled = false;

                var fillGo = new GameObject("FillLight") { hideFlags = HideFlags.HideAndDontSave };
                fillGo.transform.SetParent(studioGo.transform, false);
                fillLight = fillGo.AddComponent<Light>();
                fillLight.type = LightType.Directional;
                fillLight.color = new Color(0.84f, 0.92f, 1f, 1f);
                fillLight.intensity = 0.55f;
                fillLight.shadows = LightShadows.None;
                fillLight.cullingMask = 1 << PreviewLayer;
                fillLight.enabled = false;
            }
        }

        static void DestroyObj(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
