using UnityEngine;

namespace Khkt.Environment
{
    /// <summary>
    /// Cho hoa văn trên mảng tường nhấn (mạch điện, phân tử) trôi chậm: dịch tọa độ ảnh của một bản vật liệu dùng chung
    /// cho mọi tấm tường được gán. Cập nhật 15 lần/giây là đủ mượt với tốc độ chậm, gần như không tốn gì.
    /// </summary>
    public class UvScroll : MonoBehaviour
    {
        public Renderer[] targets;
        public Vector2 speed = new Vector2(0.01f, 0.004f);

        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        Material mat;
        Vector4 st;
        float timer;

        void Start()
        {
            if (targets == null || targets.Length == 0 || targets[0] == null) { enabled = false; return; }
            mat = new Material(targets[0].sharedMaterial) { name = targets[0].sharedMaterial.name + " (trôi)" };
            foreach (var r in targets) if (r != null) r.sharedMaterial = mat;
            st = mat.GetVector(BaseMapSt);
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < 1f / 15f) return;
            st.z = Mathf.Repeat(st.z + speed.x * timer, 1f);
            st.w = Mathf.Repeat(st.w + speed.y * timer, 1f);
            mat.SetVector(BaseMapSt, st);
            timer = 0;
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
