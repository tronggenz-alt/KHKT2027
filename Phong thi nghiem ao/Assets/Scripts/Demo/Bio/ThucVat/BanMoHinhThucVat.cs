using Khkt.Demo;
using Khkt.Interaction.Bio;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khkt.Demo.Bio.ThucVat
{
    /// <summary>
    /// Lối vào trong lab: tự gắn "Bàn mô hình thực vật" vào Nhà kính tầng 2 lúc chạy (runtime)
    /// khi khu Nhà kính thực vật được nạp, mà không cần dựng lại cảnh trước khi hoàn tất G4.
    /// </summary>
    public class BanMoHinhThucVat : MonoBehaviour, IInteractable
    {
        public string UseHint => "Bàn mô hình 3D Thực vật (Cấu trúc rễ–thân–lá, 3 lát cắt, quang hợp, thoát hơi nước, hướng sáng) · Bấm để mở";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            ZoneStreamer.ZoneChanged -= OnZoneChanged;
            ZoneStreamer.ZoneChanged += OnZoneChanged;
            if (ZoneStreamer.Instance != null && ZoneStreamer.Instance.IsLoaded(ZoneMap.Greenhouse))
                EnsureSpawned();
        }

        static void OnZoneChanged(int zone, bool loaded)
        {
            if (loaded && zone == ZoneMap.Greenhouse)
                EnsureSpawned();
        }

        public static BanMoHinhThucVat EnsureSpawned()
        {
            var existing = FindAnyObjectByType<BanMoHinhThucVat>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            GameObject greenhouse = FindGreenhouseEvenIfInactive();
            if (greenhouse == null) return null;

            var tableGo = new GameObject("Bàn mô hình thực vật");
            tableGo.transform.SetParent(greenhouse.transform, false);
            // Đặt sát lối đi bắc nhà kính xoay mặt về phía cửa vào, không chắn đường đo DoKhungHinh
            tableGo.transform.localPosition = new Vector3(22.2f, 0f, -4.15f);
            tableGo.transform.localEulerAngles = new Vector3(0f, 65f, 0f);

            var whiteWood = MakeMat(new Color(0.92f, 0.94f, 0.91f), 0.25f);
            var metalMat = MakeMat(new Color(0.24f, 0.28f, 0.32f), 0.55f, 0.65f);
            var greenMat = MakeMat(new Color(0.18f, 0.52f, 0.24f), 0.30f);
            var potMat = MakeMat(new Color(0.76f, 0.42f, 0.26f), 0.35f);
            var accentMat = MakeMat(new Color(0.12f, 0.48f, 0.34f), 0.40f);

            // Mặt bàn + chân bàn gọn
            MakePart(PrimitiveType.Cube, tableGo.transform, "Mặt bàn", new Vector3(0f, 0.80f, 0f), new Vector3(0.92f, 0.045f, 0.58f), whiteWood);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    MakePart(PrimitiveType.Cube, tableGo.transform, "Chân bàn", new Vector3(sx * 0.40f, 0.39f, sz * 0.23f), new Vector3(0.045f, 0.78f, 0.045f), metalMat);

            // Đế trưng bày và chậu cây đậu thu nhỏ trên mặt bàn
            MakePart(PrimitiveType.Cylinder, tableGo.transform, "Đế kính", new Vector3(0f, 0.835f, 0f), new Vector3(0.32f, 0.015f, 0.32f), accentMat);
            MakePart(PrimitiveType.Cylinder, tableGo.transform, "Chậu mẫu", new Vector3(0f, 0.895f, 0f), new Vector3(0.12f, 0.055f, 0.12f), potMat);
            MakePart(PrimitiveType.Cylinder, tableGo.transform, "Thân mẫu", new Vector3(0f, 1.03f, 0f), new Vector3(0.018f, 0.11f, 0.018f), greenMat);
            MakePart(PrimitiveType.Sphere, tableGo.transform, "Lá 1", new Vector3(0.06f, 1.08f, 0.02f), new Vector3(0.09f, 0.015f, 0.055f), greenMat);
            MakePart(PrimitiveType.Sphere, tableGo.transform, "Lá 2", new Vector3(-0.06f, 1.11f, -0.02f), new Vector3(0.09f, 0.015f, 0.055f), greenMat);
            MakePart(PrimitiveType.Sphere, tableGo.transform, "Ngọn hoa", new Vector3(0f, 1.17f, 0f), new Vector3(0.04f, 0.04f, 0.04f), MakeMat(new Color(0.96f, 0.82f, 0.20f), 0.4f));

            // Biển tên phía trước bàn (-Z hướng ra lối đi)
            MakePart(PrimitiveType.Cube, tableGo.transform, "Biển tên", new Vector3(0f, 0.74f, -0.295f), new Vector3(0.76f, 0.08f, 0.015f), accentMat);
            var lblGo = new GameObject("Chữ biển");
            lblGo.transform.SetParent(tableGo.transform, false);
            lblGo.transform.localPosition = new Vector3(0f, 0.74f, -0.305f);
            var tm = lblGo.AddComponent<TextMesh>();
            tm.text = "MÔ HÌNH 3D THỰC VẬT";
            tm.fontSize = 180;
            tm.characterSize = 0.0028f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            tm.fontStyle = FontStyle.Bold;

            var col = tableGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.62f, 0f);
            col.size = new Vector3(0.95f, 1.24f, 0.62f);

            var info = tableGo.AddComponent<EquipmentInfo>();
            info.title = "Bàn mô hình 3D Thực vật";
            info.description = "Mô hình giải phẫu & sinh lý cây hai lá mầm thời gian thực: 3 lát cắt (Rễ – Thân – Lá), hút nước, thoát hơi nước qua khí khổng, quang hợp và hướng sáng.";
            info.usage = "Nhấn chuột trái (hoặc phím E) để mở mô-đun 3D toàn màn hình.";

            return tableGo.AddComponent<BanMoHinhThucVat>();
        }

        public void Use(GameObject user) => ThucVatMoDunImpl.Mo();

        static GameObject FindGreenhouseEvenIfInactive()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var tr = FindNamedRecursive(roots[i].transform, "Nhà kính thực vật");
                if (tr != null) return tr.gameObject;
            }
            return null;
        }

        static Transform FindNamedRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindNamedRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static GameObject MakePart(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            return go;
        }

        static Material MakeMat(Color color, float smoothness, float metallic = 0f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }
    }
}
