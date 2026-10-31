using UnityEngine;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// Bàn giải phẫu số: mô hình BodyParts3D nằm trên mặt màn hình, bấm để đổi lớp bộ xương → hệ cơ → da.
    /// Chỉ một lớp hiện một lúc (mỗi lớp hơn 100 nghìn đỉnh). Lớp mới hiện ra bằng hoạt cảnh nâng lên nhẹ 0,35 s.
    /// </summary>
    public class DigitalAnatomyTable : MonoBehaviour, IDemoUsable
    {
        public GameObject layerSkeleton, layerMuscles, layerSkin;
        public TextMesh layerText;
        public int layer;  // 0 xương, 1 cơ, 2 da

        static readonly string[] Names = { "Bộ xương", "Hệ cơ", "Da" };
        float anim = 1f;
        Vector3[] basePos;

        GameObject[] Layers => new[] { layerSkeleton, layerMuscles, layerSkin };

        void Start()
        {
            var ls = Layers;
            basePos = new Vector3[ls.Length];
            for (int i = 0; i < ls.Length; i++) if (ls[i] != null) basePos[i] = ls[i].transform.localPosition;
            Show();
        }

        public string UseHint => $"Bàn giải phẫu số: đang xem {Names[layer].ToLowerInvariant()}. Bấm để đổi lớp.";

        public void Use()
        {
            layer = (layer + 1) % Names.Length;
            anim = 0f;
            Show();
            enabled = true;
        }

        void Show()
        {
            var ls = Layers;
            for (int i = 0; i < ls.Length; i++) if (ls[i] != null) ls[i].SetActive(i == layer);
            if (layerText != null) layerText.text = "Lớp: " + Names[layer];
        }

        void Update()
        {
            if (anim >= 1f) { enabled = false; return; }
            anim = Mathf.Min(1f, anim + Time.deltaTime / 0.35f);
            var go = Layers[layer];
            if (go == null || basePos == null) return;
            float k = Khkt.Environment.Easing.EmphasizedDecelerate(anim);
            go.transform.localPosition = basePos[layer] + Vector3.up * (0.04f * (1f - k));
        }
    }
}
