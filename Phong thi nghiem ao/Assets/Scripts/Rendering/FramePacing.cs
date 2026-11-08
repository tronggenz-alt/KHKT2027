using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Giới hạn 60 khung hình/giây. Không giới hạn thì GPU tích hợp luôn chạy hết công suất, máy nóng rồi tự hạ xung:
    /// đo trên Iris Xe, cùng một cảnh tụt từ 60 xuống 37 FPS sau vài phút, và đó mới là lúc người dùng thấy giật.
    /// </summary>
    public static class FramePacing
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply() => Application.targetFrameRate = 60;
    }
}
