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

        public static readonly string[] OrganIds = {
            "tim", "phoi", "gan", "than", "da_day", "nao",
            "xuong", "co", "da", "mach_mau", "ruot_non", "ruot_gia", "tuy", "tuy_song", "lach"
        };
        public static readonly string[] OrganNames = {
            "Tim (72 nhịp/phút)", "Phổi (16 nhịp/phút)", "Gan & Mật", "Thận (Nephron)", "Dạ dày", "Não & Thần kinh",
            "Bộ xương (206 xương)", "Hệ cơ vân", "Da & Giác quan", "Mạch máu (Tuần hoàn)", "Ruột non (Hấp thụ)", "Ruột già", "Tuyến tụy (Insulin)", "Tủy sống", "Lách (Miễn dịch)"
        };
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

        public string UseHint => cachedHint ?? $"Bàn giải phẫu số (BodyParts3D CC BY 4.0) · Cơ quan: {OrganNames[selectedOrganIndex]} · Bấm: mở mô hình 3D và bàn mổ trong app / Con lăn: xoay mô hình trên bàn";

        public void Use(GameObject user) => Use();

        public void Use()
        {
            BioFeedback.PlayClick();
            BioFeedback.LogAction("ban_giai_phau_so", "mo_giai_phau_3d",
                $"Bàn giải phẫu số: mở mô hình giải phẫu người 3D trong app, chọn sẵn '{OrganNames[selectedOrganIndex]}'.");
            // 24/11: mở khung xem 3D ngay trong app (không mở trang web). Lớp hiển thị trên mặt bàn giữ nguyên.
            if (!Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.DangMo)
                Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.Mo();
            var ctl = Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.HienTai;
            if (ctl != null && Khkt.Demo.Bio.GiaiPhau.Bp3dReader.TimCoQuan(OrganIds[selectedOrganIndex]) != null)
                ctl.ChonCoQuan(OrganIds[selectedOrganIndex]);
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

        /// <summary>Con lăn chỉ xoay mô hình trên mặt bàn (trước đây vừa xoay vừa nhảy cơ quan: hai việc không liên quan).</summary>
        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            modelYawDeg = (modelYawDeg + Mathf.Sign(delta) * 15f) % 360f;
            var ls = ActiveLayers;
            for (int i = 0; i < ls.Length; i++)
            {
                if (ls[i] != null) ls[i].transform.localRotation = Quaternion.Euler(0f, modelYawDeg, 0f);
            }
            RebuildHint();
        }

        void RebuildHint()
        {
            var names = ActiveNames;
            cachedHint = $"Bàn giải phẫu số (BodyParts3D CC BY 4.0): lớp {names[layer % names.Length]} · Cơ quan: {OrganNames[selectedOrganIndex]} · Bấm: mở mô hình 3D trong app · Con lăn: xoay ({BioFeedback.FormatVi(modelYawDeg, "0")}°)";
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
