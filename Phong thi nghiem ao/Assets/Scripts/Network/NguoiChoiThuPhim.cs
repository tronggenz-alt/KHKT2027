using Khkt.Environment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Network
{
    /// <summary>
    /// Phím Y trong phòng thí nghiệm: bật / tắt chế độ thử thân người 3D (NguoiChoiThu), chạy cả trong app đã dựng.
    /// Không bắt phím khi đang gõ vào ô chữ (DesktopViewer.UiCapture).
    /// </summary>
    public class NguoiChoiThuPhim : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<NguoiChoiThuPhim>() != null) return;
            var go = new GameObject("Phím thử thân người 3D") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<NguoiChoiThuPhim>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.yKey.wasPressedThisFrame || DesktopViewer.UiCapture != 0) return;
            if (FindAnyObjectByType<DesktopViewer>() == null) return;   // chỉ trong phòng thí nghiệm
            NguoiChoiThu.Toggle();
        }
    }
}
