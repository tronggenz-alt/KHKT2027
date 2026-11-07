using System.Collections.Generic;

namespace Khkt.Demo
{
    /// <summary>
    /// Luật của buổi học do giáo viên đặt trên web (POST /app/nhip, trường buoi): chế độ Kiểm tra, bật tắt AI và robot,
    /// hóa chất bị khóa. PresentationClient cập nhật; bàn tay, robot, khung trò chuyện, chú thích đọc ở đây.
    /// </summary>
    public static class ClassRules
    {
        static readonly HashSet<int> lockedChemicals = new HashSet<int>();

        public static bool Exam { get; private set; }
        public static bool AiEnabled { get; private set; } = true;
        public static bool RobotEnabled { get; private set; } = true;

        /// <summary>Giờ máy chủ (giây epoch) hết bài kiểm tra; 0 là không đếm ngược.</summary>
        public static double ExamEnd { get; private set; }

        /// <summary>Ẩn gợi ý và chú thích hiện tượng.</summary>
        public static bool HideHelp => Exam || !AiEnabled;

        /// <summary>Học sinh không mở được khung trò chuyện với robot.</summary>
        public static bool ChatLocked => Exam || !AiEnabled || !RobotEnabled;

        public static bool IsLocked(int stt) => lockedChemicals.Contains(stt);

        public static void Apply(bool exam, bool ai, bool robot, double examEnd, int[] locked)
        {
            Exam = exam;
            AiEnabled = ai;
            RobotEnabled = robot;
            ExamEnd = exam ? examEnd : 0;
            // null: máy chủ không gửi lại danh sách vì chưa đổi, giữ nguyên danh sách cũ
            if (locked != null)
            {
                lockedChemicals.Clear();
                foreach (int s in locked) lockedChemicals.Add(s);
            }
        }

        public static void Reset() => Apply(false, true, true, 0, new int[0]);
    }
}
