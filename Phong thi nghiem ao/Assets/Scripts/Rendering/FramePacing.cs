using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Giới hạn 60 khung hình/giây. Không giới hạn thì GPU tích hợp luôn chạy hết công suất, máy nóng rồi tự hạ xung:
    /// đo trên Iris Xe, cùng một cảnh tụt từ 60 xuống 37 FPS sau vài phút, và đó mới là lúc người dùng thấy giật.
    ///
    /// Đây là nơi DUY NHẤT đặt nhịp khung hình. Trước đây HdQualityManager đặt lại thành min(90, tần số màn hình) sau khi
    /// cảnh tải xong: trên màn 120 Hz thành 90 FPS, không chia hết cho 120 nên khung hình lúc 8 ms lúc 16 ms (rung nhẹ liên tục)
    /// và GPU chạy nóng hơn 50 % không để làm gì. 60 chia hết cho cả 60 Hz và 120 Hz.
    /// </summary>
    public static class FramePacing
    {
        public const int TargetFps = 60;

        /// <summary>Đang chạy ở nhịp chuẩn (bài đo hiệu năng tự bỏ giới hạn thì trả về false).</summary>
        public static bool Capped => Application.targetFrameRate == TargetFps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            QualitySettings.vSyncCount = 0;  // vSync bật thì targetFrameRate bị bỏ qua
            Application.targetFrameRate = TargetFps;
        }
    }
}
