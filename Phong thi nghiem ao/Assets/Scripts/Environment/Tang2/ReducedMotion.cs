using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Quản lý tùy chọn "Giảm chuyển động" (Reduced Motion) theo chuẩn Accessibility & Material 3.
    /// Tắt các hoạt cảnh trang trí (lắc lư, xoay nhàn rỗi), giữ nguyên hoạt cảnh mang thông tin khoa học (bọt O2, vạch gel, khuẩn lạc).
    /// </summary>
    public static class ReducedMotion
    {
        const string PrefKey = "KHKT_GIAM_CHUYEN_DONG";

        // Nhớ lại giá trị: thẻ thông báo, quạt trần, nút bấm hỏi cờ này mỗi khung hình, đọc PlayerPrefs mỗi lần là thừa
        static int cached = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cached = -1;

        public static bool IsEnabled
        {
            get
            {
                if (cached < 0) cached = PlayerPrefs.GetInt(PrefKey, 0) == 1 ? 1 : 0;
                return cached == 1;
            }
            set
            {
                cached = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
