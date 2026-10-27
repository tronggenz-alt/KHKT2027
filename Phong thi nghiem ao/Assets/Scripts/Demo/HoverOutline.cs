using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Viền sáng quanh dụng cụ đang được trỏ: gắn tạm một "lớp vỏ" (cùng lưới, vật liệu Khkt/VienSang)
    /// vào từng phần của dụng cụ, bỏ đi khi thôi trỏ. Vỏ đi theo vật nên vẫn đúng khi vật đang được cầm,
    /// và hiện ở mọi camera (màn hình, kính lúp, kính VR).
    /// Vật đã gộp lô tĩnh (static batching) không viền được, builder đã loại các dụng cụ ra.
    /// </summary>
    public class HoverOutline : MonoBehaviour
    {
        public Shader outlineShader;
        public Color color = new Color(0.45f, 0.9f, 1f, 1f);
        public float widthPx = 4f;

        Material mat;
        readonly List<GameObject> shells = new List<GameObject>();
        Transform target;

        void Awake()
        {
            if (outlineShader == null) outlineShader = Shader.Find("Khkt/VienSang");
            if (outlineShader != null) mat = new Material(outlineShader) { hideFlags = HideFlags.DontSave };
        }

        /// <summary>Đặt dụng cụ cần viền (null để tắt).</summary>
        public void SetTarget(Transform t)
        {
            if (t == target) return;
            Clear();
            target = t;
            if (t == null || mat == null) return;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null || !r.enabled) continue;
                if (mf.GetComponent<TextMesh>() != null || mf.sharedMesh.name.StartsWith("Combined Mesh")) continue;
                if (mf.name.ToLowerInvariant().Contains("kính")) continue;  // tấm kính lớn (cửa tủ) mà viền thì sáng trùm cả mặt
                var shell = new GameObject("Viền sáng") { hideFlags = HideFlags.DontSave };
                shell.transform.SetParent(mf.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var sr = shell.AddComponent<MeshRenderer>();
                var mats = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                sr.sharedMaterials = mats;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                shells.Add(shell);
            }
        }

        void Clear()
        {
            foreach (var s in shells) if (s != null) Destroy(s);
            shells.Clear();
        }

        void LateUpdate()
        {
            if (mat == null || shells.Count == 0) return;
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f);
            mat.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * pulse));
            mat.SetFloat("_Width", widthPx);
        }

        void OnDestroy()
        {
            Clear();
            if (mat != null) Destroy(mat);
        }
    }
}
