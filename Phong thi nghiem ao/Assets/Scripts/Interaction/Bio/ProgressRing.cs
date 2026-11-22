namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Vòng tiến trình hiển thị quanh tâm màn hình khi người chơi giữ để thực hiện thao tác sinh học có thời lượng
    /// (ví dụ: hơ đỏ que cấy 2,0 s, hạ lamen góc 45° 1,0 s, thấm thuốc nhuộm 0,8 s).
    /// Phần vẽ nằm ở Khkt.Demo.LabToasts (vòng tròn thật, co giãn theo màn hình); lớp này giữ lại lời gọi cũ.
    /// </summary>
    public static class ProgressRing
    {
        public static void Show(string actionLabel, float progress01)
            => Khkt.Demo.LabToasts.Progress(string.IsNullOrEmpty(actionLabel) ? "Đang thao tác..." : actionLabel, progress01);

        /// <summary>Bàn tay gọi mỗi khung hình khi không giữ: chỉ hạ một cờ.</summary>
        public static void Hide() => Khkt.Demo.LabToasts.HideProgress();
    }
}
