using Khkt.Network;
using UnityEditor;

namespace Khkt.EditorTools
{
    /// <summary>Menu thử thân người 3D khi đang Play (một máy, không cần phòng học chung). Trong lab bấm phím Y cũng được.</summary>
    public static class NguoiChoiThuMenu
    {
        const string Item = "KHKT/Thử thân người 3D (góc nhìn thứ ba, phím Y)";

        [MenuItem(Item)]
        static void Toggle() => NguoiChoiThu.Toggle();

        [MenuItem(Item, true)]
        static bool CanToggle() => EditorApplication.isPlaying;
    }
}
