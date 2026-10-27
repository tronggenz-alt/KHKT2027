using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Khkt.Rendering
{
    /// <summary>
    /// Impostor thời gian thực (billboard động) cho vật ở xa, ví dụ cây ngoài sân. Lúc chạy, vật được chụp từ <see cref="frames"/>
    /// hướng quanh trục đứng vào một ảnh atlas (camera trực giao, nền trong suốt, có ánh sáng mặt trời thật của cảnh). Ở xa hơn
    /// switchDistance, mô hình 3D tắt, thay bằng một tấm phẳng luôn quay về người xem và hiện khung ảnh chụp từ hướng gần hướng
    /// nhìn nhất. Tới gần thì hiện lại mô hình thật. Gọi Capture() để chụp lại khi vật thay đổi.
    /// </summary>
    public class ImpostorBillboard : MonoBehaviour
    {
        public int frames = 8;
        public int frameSize = 256;
        public float switchDistance = 12f;
        [Tooltip("Vật liệu tấm impostor (URP Unlit, cắt alpha): tạo sẵn khi dựng cảnh để biến thể shader không bị loại khỏi bản build")]
        public Material billboardMaterial;
        [Tooltip("Lớp trống dùng tạm khi chụp (chỉ vật này được vẽ)")]
        public int captureLayer = 30;

        Renderer[] sources;
        RenderTexture atlas;
        Transform quad;
        Renderer quadRenderer;
        MaterialPropertyBlock mpb;
        Vector3 center;
        float size;
        bool billboard, ready;
        int frame = -1;
        static Transform cam;
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST"), BaseMap = Shader.PropertyToID("_BaseMap");

        public static int ActiveCount { get; private set; }

        IEnumerator Start()
        {
            yield return null;  // để ánh sáng, cảnh tải xong rồi mới chụp
            Capture();
        }

        void OnDestroy()
        {
            if (atlas != null) atlas.Release();
            if (billboard) ActiveCount--;
        }

        /// <summary>Chụp vật từ nhiều hướng vào atlas (gọi lại khi vật đổi hình dạng).</summary>
        public void Capture()
        {
            if (billboardMaterial == null) return;
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (quad == null || !r.transform.IsChildOf(quad)) list.Add(r);
            sources = list.ToArray();
            if (sources.Length == 0) return;
            var b = sources[0].bounds;
            foreach (var r in sources) b.Encapsulate(r.bounds);
            center = b.center;
            float half = Mathf.Max(b.extents.y, new Vector2(b.extents.x, b.extents.z).magnitude) * 1.04f;
            size = half * 2f;

            if (atlas == null)
            {
                atlas = new RenderTexture(frameSize * frames, frameSize, 0, RenderTextureFormat.ARGB32) { name = "Impostor " + name, useMipMap = true, autoGenerateMips = false };
                atlas.Create();
            }
            var tmp = RenderTexture.GetTemporary(frameSize, frameSize, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("Máy chụp impostor");
            var c = camGo.AddComponent<Camera>();
            c.enabled = false;
            c.orthographic = true;
            c.orthographicSize = half;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0, 0, 0, 0);
            c.cullingMask = 1 << captureLayer;
            c.nearClipPlane = 0.05f;
            c.farClipPlane = half * 6f;
            c.targetTexture = tmp;
            var data = c.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;

            var layers = new int[sources.Length];
            for (int i = 0; i < sources.Length; i++) { layers[i] = sources[i].gameObject.layer; sources[i].gameObject.layer = captureLayer; }
            var enabled = new bool[sources.Length];
            for (int i = 0; i < sources.Length; i++) { enabled[i] = sources[i].enabled; sources[i].enabled = true; }
            for (int f = 0; f < frames; f++)
            {
                Vector3 dir = Quaternion.Euler(0, 360f * f / frames, 0) * Vector3.forward;
                c.transform.position = center + dir * half * 3f;
                c.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                c.Render();
                Graphics.CopyTexture(tmp, 0, 0, 0, 0, frameSize, frameSize, atlas, 0, 0, f * frameSize, 0);
            }
            for (int i = 0; i < sources.Length; i++) { sources[i].gameObject.layer = layers[i]; sources[i].enabled = enabled[i] && !billboard; }
            atlas.GenerateMips();
            RenderTexture.ReleaseTemporary(tmp);
            Destroy(camGo);

            if (quad == null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Impostor (tấm phẳng)";
                quad = q.transform;
                quad.SetParent(transform, true);
                quadRenderer = q.GetComponent<Renderer>();
                quadRenderer.sharedMaterial = billboardMaterial;
                quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
                quadRenderer.receiveShadows = false;
                quadRenderer.enabled = false;
                mpb = new MaterialPropertyBlock();
            }
            quad.position = center;
            quad.localScale = new Vector3(size, size, 1f) / Mathf.Max(1e-4f, transform.lossyScale.x);
            frame = -1;
            ready = true;
        }

        void LateUpdate()
        {
            if (!ready) return;
            if (cam == null) { var c = Camera.main; if (c == null) return; cam = c.transform; }
            Vector3 d = cam.position - center;
            float dist = d.magnitude;
            bool want = dist > switchDistance;
            if (want != billboard)
            {
                billboard = want;
                ActiveCount += want ? 1 : -1;
                foreach (var r in sources) if (r != null) r.enabled = !want;
                quadRenderer.enabled = want;
            }
            if (!billboard) return;
            d.y = 0;
            if (d.sqrMagnitude < 1e-6f) return;
            quad.rotation = Quaternion.LookRotation(-d.normalized, Vector3.up);  // mặt nhìn thấy của Quad hướng về người xem
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            int f = ((Mathf.RoundToInt(yaw / (360f / frames)) % frames) + frames) % frames;
            if (f == frame) return;
            frame = f;
            quadRenderer.GetPropertyBlock(mpb);
            mpb.SetTexture(BaseMap, atlas);
            mpb.SetVector(BaseMapSt, new Vector4(1f / frames, 1f, (float)f / frames, 0f));
            quadRenderer.SetPropertyBlock(mpb);
        }
    }
}
