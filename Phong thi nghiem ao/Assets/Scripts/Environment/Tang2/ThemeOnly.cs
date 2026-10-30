using UnityEngine;

namespace Khkt.Environment.Tang2
{
    public enum LabThemeMode
    {
        QuocTe,
        ToiGian
    }

    /// <summary>
    /// Gắn lên đối tượng chỉ xuất hiện ở một chủ đề nhất định (ví dụ dải sọc màu, đèn LED mép bàn, vách ốp gỗ).
    /// </summary>
    public class ThemeOnly : MonoBehaviour
    {
        [Tooltip("Chủ đề mà đối tượng này được hiển thị")]
        public LabThemeMode theme = LabThemeMode.QuocTe;

        public void ApplyTheme(LabThemeMode currentTheme)
        {
            gameObject.SetActive(theme == currentTheme);
        }
    }
}
