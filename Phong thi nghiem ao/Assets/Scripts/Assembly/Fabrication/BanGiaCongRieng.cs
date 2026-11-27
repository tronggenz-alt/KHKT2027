using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Bàn riêng cho dụng cụ gia công (bản đi thi 27/11/2026): bộ lắp lớn (giá sắt + bình + sinh hàn... 140 × 86 cm) không vừa
    /// bàn nhận 0,7 × 1,2 m hay các bàn thí nghiệm đang bày đồ, mọi bàn đều "hết chỗ". Lúc mở lab, tìm chỗ sàn trống gần khu
    /// chế tạo (có trần, không vướng vật, chừa lối đi) rồi dựng bàn đen 2,0 × 1,2 m làm bàn đích đầu danh sách.
    /// </summary>
    public class BanGiaCongRieng : MonoBehaviour
    {
        public const float W = 2.0f, D = 1.2f, Top = 0.9f;
        static readonly Vector3 Near = new Vector3(18.4f, 0f, 10.0f);   // trước cửa khu chế tạo (LabSceneBuilder.Fabrication)
        static BanGiaCongRieng made;
        float nextTry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<BanGiaCongRieng>() != null) return;
            var go = new GameObject("Bàn gia công riêng (tìm chỗ)");
            DontDestroyOnLoad(go);
            go.AddComponent<BanGiaCongRieng>();
        }

        void Update()
        {
            if (made != null || Time.unscaledTime < nextTry) return;
            nextTry = Time.unscaledTime + 2f;
            // chỉ dựng khi khu chế tạo đã tải (có bàn đích mặc định)
            FabricationDestination def = null;
            foreach (var d in FabricationDestination.All) if (d != null && d.isDefault) { def = d; break; }
            if (def == null) return;
            if (TryPlace(def, out var pos, out float yaw)) made = Build(def, pos, yaw);
            else nextTry = Time.unscaledTime + 10f;
        }

        static bool TryPlace(FabricationDestination def, out Vector3 best, out float bestYaw)
        {
            best = default;
            bestYaw = 0;
            float bestD = float.MaxValue;
            Vector3 c0 = new Vector3(def.transform.position.x, 0, def.transform.position.z);
            Vector3 near = Vector3.Distance(c0, Near) < 6f ? Near : c0;
            for (float dx = -7f; dx <= 7f; dx += 0.25f)
                for (float dz = -7f; dz <= 7f; dz += 0.25f)
                {
                    var p = near + new Vector3(dx, 0, dz);
                    float dist = new Vector2(dx, dz).magnitude;
                    if (dist >= bestD) continue;
                    foreach (float yaw in new[] { 0f, 90f })
                        if (Free(p, yaw)) { best = p; bestYaw = yaw; bestD = dist; break; }
                }
            return bestD < float.MaxValue;
        }

        /// <summary>Có sàn (cao ~0) và trần phía trên, khối bàn + lối đi 0,6 m quanh bàn trống.</summary>
        static bool Free(Vector3 p, float yaw)
        {
            if (!Physics.Raycast(p + Vector3.up * 1.6f, Vector3.down, out var floor, 2.5f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(floor.point.y) > 0.12f) return false;
            if (!Physics.Raycast(p + Vector3.up * 1.6f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore)) return false;   // ngoài trời
            var rot = Quaternion.Euler(0, yaw, 0);
            var half = new Vector3(W / 2 + 0.6f, 0.95f, D / 2 + 0.6f);
            foreach (var c in Physics.OverlapBox(new Vector3(p.x, floor.point.y + 0.08f + 0.95f, p.z), half, rot, ~0, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<CharacterController>() == null) return false;
            return true;
        }

        static BanGiaCongRieng Build(FabricationDestination def, Vector3 p, float yaw)
        {
            var root = new GameObject("Bàn gia công riêng");
            root.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            var comp = root.AddComponent<BanGiaCongRieng>();
            Material top = null, body = null;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (top == null && (r.name.StartsWith("Mặt bàn") || r.name.StartsWith("Mặt làm việc"))) top = r.sharedMaterial;
                if (body == null && r.name.StartsWith("Tủ")) body = r.sharedMaterial;
                if (top != null && body != null) break;
            }
            Box(root.transform, "Mặt bàn gia công", new Vector3(0, Top - 0.02f, 0), new Vector3(W, 0.04f, D), top, new Color(0.08f, 0.08f, 0.09f));
            Box(root.transform, "Thân bàn gia công", new Vector3(0, (Top - 0.04f) / 2, 0), new Vector3(W - 0.08f, Top - 0.04f, D - 0.1f), body, new Color(0.2f, 0.22f, 0.25f));
            var dg = new GameObject("Đích gia công");
            dg.transform.SetParent(root.transform, false);
            dg.transform.localPosition = new Vector3(0, Top, 0);
            var d = dg.AddComponent<FabricationDestination>();
            d.label = "Bàn gia công riêng (2,0 × 1,2 m)";
            d.order = -10;
            d.size = new Vector2(W - 0.06f, D - 0.06f);
            d.front = Vector3.back;
            d.isDefault = false;
            Debug.Log($"[Bàn gia công] dựng ở {p} xoay {yaw}°");
            return comp;
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Color fallback)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            else r.material.color = fallback;
        }
    }
}
