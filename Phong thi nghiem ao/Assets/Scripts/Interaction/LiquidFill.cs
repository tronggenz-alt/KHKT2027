using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Khối chất lỏng trong bình tròn xoay (bình nón, cốc, bình cầu...): tính độ cao mực chất lỏng
    /// từ thể tích theo đúng hình dạng thành trong của bình, rồi dựng lại lưới khi mực thay đổi.
    /// Nhờ vậy chất lỏng luôn nằm gọn trong bình, kể cả ở phần bình thu hẹp.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public class LiquidFill : MonoBehaviour
    {
        [Tooltip("Thành trong của bình: (bán kính, độ cao) theo mét, từ đáy lên, tính trong hệ tọa độ của vật này")]
        public Vector2[] innerProfile =
        {
            new Vector2(0.040f, 0.003f), new Vector2(0.0425f, 0.006f), new Vector2(0.0425f, 0.02f),
            new Vector2(0.0155f, 0.1f), new Vector2(0.0145f, 0.105f), new Vector2(0.0145f, 0.14f),
        };
        public float volumeMl = 50f;
        public int segments = 24;

        public float LevelM { get; private set; }

        Mesh mesh;
        float builtLevel = -1f, computedVolume = -1f;

        public void SetVolume(float ml) => volumeMl = ml;

        void OnEnable()
        {
            mesh = new Mesh { name = "Chất lỏng", hideFlags = HideFlags.DontSave };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            builtLevel = computedVolume = -1f;
            Rebuild();
        }

        void OnDisable()
        {
            if (mesh != null) DestroyImmediate(mesh);
        }

        void LateUpdate() => Rebuild();

        void Rebuild()
        {
            if (innerProfile == null || innerProfile.Length < 2 || mesh == null) return;
            if (Mathf.Abs(volumeMl - computedVolume) > 0.01f)
            {
                computedVolume = volumeMl;
                LevelM = HeightFor(volumeMl * 1e-6f);
            }
            if (Mathf.Abs(LevelM - builtLevel) < 0.0003f) return;
            builtLevel = LevelM;

            // Các vòng biên: đáy (tâm), các điểm thành trong thấp hơn mực, mực chất lỏng, nắp (tâm)
            var rings = new List<Vector2> { new Vector2(0, innerProfile[0].y), innerProfile[0], innerProfile[0] };
            for (int i = 1; i < innerProfile.Length && innerProfile[i].y < LevelM; i++) rings.Add(innerProfile[i]);
            var top = new Vector2(RadiusAt(LevelM), LevelM);
            rings.Add(top);
            rings.Add(top);
            rings.Add(new Vector2(0, LevelM));

            var v = new List<Vector3>();
            var t = new List<int>();
            int row = segments + 1;
            foreach (var p in rings)
                for (int s = 0; s <= segments; s++)
                {
                    float a = 2 * Mathf.PI * s / segments;
                    v.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
                }
            for (int i = 0; i < rings.Count - 1; i++)
                for (int s = 0; s < segments; s++)
                {
                    int a = i * row + s, b = a + row;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public float RadiusAt(float y)
        {
            if (y <= innerProfile[0].y) return innerProfile[0].x;
            for (int i = 1; i < innerProfile.Length; i++)
                if (y <= innerProfile[i].y)
                {
                    var a = innerProfile[i - 1];
                    var b = innerProfile[i];
                    return Mathf.Lerp(a.x, b.x, (y - a.y) / Mathf.Max(1e-6f, b.y - a.y));
                }
            return innerProfile[innerProfile.Length - 1].x;
        }

        /// <summary>Thể tích (m³) chứa được từ đáy tới độ cao h.</summary>
        public float VolumeTo(float h)
        {
            const float dy = 0.0005f;
            float vol = 0, y0 = innerProfile[0].y;
            for (float y = y0; y < h; y += dy)
            {
                float step = Mathf.Min(dy, h - y);
                float r = RadiusAt(y + step / 2);
                vol += Mathf.PI * r * r * step;
            }
            return vol;
        }

        float HeightFor(float volumeM3)
        {
            float lo = innerProfile[0].y, hi = innerProfile[innerProfile.Length - 1].y;
            if (volumeM3 <= 0) return lo + 0.0005f;
            if (VolumeTo(hi) <= volumeM3) return hi;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) / 2;
                if (VolumeTo(mid) < volumeM3) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }
    }
}
