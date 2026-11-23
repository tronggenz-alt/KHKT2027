using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Khkt.Rendering
{
    /// <summary>
    /// Gắn vào camera chỉ vẽ giao diện (hoặc giao diện + mô hình kiểu "giấy trắng") để HardwareProfile.ApplyCameras
    /// không bật hậu kỳ cho nó. Tonemapping Neutral kéo màu trắng #fff xuống ~208/255, làm cả giao diện xám đục.
    /// Camera mà cullingMask chỉ có lớp UI thì ApplyCameras tự bỏ qua, không cần gắn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraKhongHauKy : MonoBehaviour
    {
        void OnEnable() => Tat(GetComponent<Camera>());

        /// <summary>Tắt hậu kỳ ngay (không chờ lần ApplyCameras sau).</summary>
        public static void Tat(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = false;
        }

        /// <summary>Camera này có phải loại không được bật hậu kỳ không.</summary>
        public static bool BoQua(Camera cam)
        {
            if (cam == null) return true;
            if (cam.GetComponent<CameraKhongHauKy>() != null) return true;
            int ui = LayerMask.NameToLayer("UI");
            return ui >= 0 && cam.cullingMask != 0 && (cam.cullingMask & ~(1 << ui)) == 0;
        }

        /// <summary>Gắn đánh dấu (một lần) và tắt hậu kỳ.</summary>
        public static void Gan(Camera cam)
        {
            if (cam == null) return;
            if (cam.GetComponent<CameraKhongHauKy>() == null) cam.gameObject.AddComponent<CameraKhongHauKy>();
            Tat(cam);
        }
    }
}
