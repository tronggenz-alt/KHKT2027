using UnityEngine;

namespace Khkt.Interaction.Bio
{
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
    }
}
