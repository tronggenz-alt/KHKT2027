// -----------------------------------------------------------------------------
// DigitalAnatomyTable.cs — Bàn giải phẫu số 3D (Mục 5.1.19 PROMPT_DUNG_CU_GIAO_DIEN.md).
// Ghi công mô hình 3D: BodyParts3D © The Database Center for Life Science (DBCLS),
// cấp phép theo CC BY-SA 2.1 JP / CC BY 4.0.
// -----------------------------------------------------------------------------
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// Bàn giải phẫu số: mô hình BodyParts3D nằm trên mặt màn hình, bấm để đổi lớp:
    /// 0: Bộ xương -> 1: Hệ cơ -> 2: Nội tạng (tim đập 72 bpm, phổi thở 16 nhịp/phút) -> 3: Da.
    /// Hỗ trợ chọn cơ quan xem thẻ kiến thức (từ phong_sinh/tu_app_cu/kien_thuc/), xoay mô hình và câu hỏi ôn tập.
    /// </summary>
    public class DigitalAnatomyTable : MonoBehaviour, IDemoUsable, IInteractable, IScrollAdjustable
    {
        public GameObject layerSkeleton, layerMuscles, layerOrgans, layerSkin;
        public TextMesh layerText;
        public int layer;  // 0 xương, 1 cơ, 2 nội tạng (nếu có) hoặc da

        static readonly string[] Names4 = { "Bộ xương", "Hệ cơ", "Nội tạng", "Da" };
        static readonly string[] Names3 = { "Bộ xương", "Hệ cơ", "Da" };

        public static readonly string[] OrganIds = { "tim", "phoi", "gan", "than", "da-day", "nao" };
        public static readonly string[] OrganNames = { "Tim (72 nhịp/phút)", "Phổi (16 nhịp/phút)", "Gan & Mật", "Thận (Nephron)", "Dạ dày", "Não & Thần kinh" };
        public int selectedOrganIndex = 0;
        public float modelYawDeg = 0f;
        public int quizScore = 0;
        public int quizAnswered = 0;

        float anim = 1f;
        Vector3[] basePos;
        string cachedHint;

        GameObject[] ActiveLayers => layerOrgans != null
            ? new[] { layerSkeleton, layerMuscles, layerOrgans, layerSkin }
            : new[] { layerSkeleton, layerMuscles, layerSkin };

        string[] ActiveNames => layerOrgans != null ? Names4 : Names3;

        void Start()
        {
            var ls = ActiveLayers;
            basePos = new Vector3[ls.Length];
            for (int i = 0; i < ls.Length; i++) if (ls[i] != null) basePos[i] = ls[i].transform.localPosition;
            Show();
        }

        public string UseHint => cachedHint ?? $"Bàn giải phẫu số (BodyParts3D CC BY 4.0): đang xem {ActiveNames[layer % ActiveNames.Length].ToLowerInvariant()} · Cơ quan: {OrganNames[selectedOrganIndex]} · Bấm: đổi lớp / Con lăn: xoay & chọn cơ quan";

        public void Use(GameObject user) => Use();

        public void Use()
        {
            var names = ActiveNames;
            layer = (layer + 1) % names.Length;
            anim = 0f;
            Show();
            BioFeedback.PlayClick();
            BioFeedback.LogAction("ban_giai_phau_so", "doi_lop_giai_phau",
                $"Bàn giải phẫu số: chuyển sang lớp '{names[layer]}' (Cơ quan trọng tâm: {OrganNames[selectedOrganIndex]}).");
            enabled = true;
        }

        public void SelectNextOrgan()
        {
            selectedOrganIndex = (selectedOrganIndex + 1) % OrganNames.Length;
            RebuildHint();
            BioFeedback.PlayClick();
            BioFeedback.LogAction("ban_giai_phau_so", "chon_co_quan",
                $"Đã chọn cơ quan '{OrganNames[selectedOrganIndex]}' (kiến thức: phong_sinh/tu_app_cu/kien_thuc/{OrganIds[selectedOrganIndex]}.json).");
        }

        public void AnswerQuizQuestion(bool correct)
        {
            quizAnswered++;
            if (correct) quizScore++;
            BioFeedback.LogAction("ban_giai_phau_so", "tra_loi_on_tap",
                $"Ôn tập giải phẫu số: {(correct ? "ĐÚNG" : "SAI")} (Điểm: {quizScore}/{quizAnswered}).");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            modelYawDeg = (modelYawDeg + Mathf.Sign(delta) * 15f) % 360f;
            var ls = ActiveLayers;
            for (int i = 0; i < ls.Length; i++)
            {
                if (ls[i] != null) ls[i].transform.localRotation = Quaternion.Euler(0f, modelYawDeg, 0f);
            }
            SelectNextOrgan();
        }

        void RebuildHint()
        {
            var names = ActiveNames;
            cachedHint = $"Bàn giải phẫu số (BodyParts3D CC BY 4.0): lớp {names[layer % names.Length]} · Cơ quan: {OrganNames[selectedOrganIndex]} · Bấm: đổi lớp · Con lăn: xoay ({BioFeedback.FormatVi(modelYawDeg, "0")}°)";
        }

        void Show()
        {
            var ls = ActiveLayers;
            var names = ActiveNames;
            for (int i = 0; i < ls.Length; i++) if (ls[i] != null) ls[i].SetActive(i == layer);
            if (layerText != null) layerText.text = "Lớp: " + names[layer % names.Length];
            RebuildHint();
        }

        void Update()
        {
            var ls = ActiveLayers;
            int idx = layer % ls.Length;
            var go = ls[idx];

            if (anim < 1f)
            {
                anim = Mathf.Min(1f, anim + Time.deltaTime / 0.35f);
                if (go != null && basePos != null && idx < basePos.Length)
                {
                    float k = Khkt.Environment.Easing.EmphasizedDecelerate(anim);
                    go.transform.localPosition = basePos[idx] + Vector3.up * (0.04f * (1f - k));
                }
            }
            else if (layerOrgans == null || go != layerOrgans)
            {
                enabled = false;
            }
        }
    }
}
