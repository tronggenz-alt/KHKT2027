using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Giao diện tương tác bấm chuột trái trên dụng cụ/thiết bị sinh học Tầng 2 (tương thích IDemoUsable của HandController).
    /// </summary>
    public interface IInteractable : Khkt.Demo.IDemoUsable
    {
        void Use(GameObject user);
        void Khkt.Demo.IDemoUsable.Use() => Use(null);
    }

    /// <summary>
    /// Giao diện cho đối tượng điều chỉnh trực tiếp bằng con lăn chuột (scroll wheel) khi trỏ vào.
    /// </summary>
    public interface IScrollAdjustable
    {
        void OnScroll(float delta);
    }

    /// <summary>
    /// Giao diện cho đối tượng nhận tác động từ dụng cụ cầm tay (BioTool) hoặc thao tác trực tiếp bằng chuột trái.
    /// Ví dụ: lam kính nhận mẫu/thuốc nhuộm/giấy thấm, đèn cồn nung que cấy, đĩa Petri nhận đường ria,
    /// cốc Elodea nhận NaHCO3, bồn thủy canh nhận bút đo pH/EC, đầu gối nhận búa phản xạ...
    /// </summary>
    public interface IBioTarget
    {
        string TargetName { get; }

        /// <summary>
        /// Kiểm tra dụng cụ đang cầm (có thể null nếu thao tác tay không) có áp dụng lên mục tiêu này được không.
        /// </summary>
        bool CanApplyTool(BioTool tool, out string promptOrReason);

        /// <summary>
        /// Thời gian cần giữ chuột trái (giây). Trả về 0 nếu tác động tức thì khi bấm chuột trái.
        /// </summary>
        float GetRequiredHoldSeconds(BioTool tool);

        /// <summary>
        /// Gọi mỗi khung hình trong lúc người học đang giữ chuột trái trên mục tiêu.
        /// </summary>
        void OnToolApplyStep(BioTool tool, float dt, float progress01);

        /// <summary>
        /// Gọi khi hoàn tất thao tác (ngay khi click nếu holdSeconds == 0, hoặc khi ProgressRing đạt 100%).
        /// </summary>
        void OnToolApplyComplete(BioTool tool);

        // Chữ ký tương thích rút gọn theo mục 4.2 PROMPT_DUNG_CU_GIAO_DIEN.md
        bool CanApply(BioTool tool, out string hint) => CanApplyTool(tool, out hint);
        bool Apply(BioTool tool, float hold, float dt)
        {
            float req = GetRequiredHoldSeconds(tool);
            if (req <= 0.01f)
            {
                OnToolApplyComplete(tool);
                return true;
            }
            float p = Mathf.Clamp01(hold / req);
            OnToolApplyStep(tool, dt, p);
            if (hold >= req)
            {
                OnToolApplyComplete(tool);
                return true;
            }
            return false;
        }
        void Cancel(BioTool tool) { }
    }
}
