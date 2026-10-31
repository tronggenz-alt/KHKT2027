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

        public static bool IsEnabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
