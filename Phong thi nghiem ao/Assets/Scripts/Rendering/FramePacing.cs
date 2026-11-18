using System;
using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Nơi DUY NHẤT đặt nhịp khung hình (60 FPS).
    /// Xử lý đồng bộ mượt cho cả màn 60 Hz và màn 120 Hz (ProMotion / màn ngoài):
    /// 60 chia hết cho cả 60 Hz (vSync = 1) và 120 Hz (vSync = 2).
    /// </summary>
    public static class FramePacing
    {
        public const int TargetFps = 60;

        /// <summary>Đang chạy ở nhịp chuẩn (bài đo hiệu năng tự bỏ giới hạn thì trả về false).</summary>
        public static bool Capped => QualitySettings.vSyncCount > 0 || Application.targetFrameRate == TargetFps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            int cliVsync = ParseCliVsync();
            if (cliVsync >= 0)
            {
                QualitySettings.vSyncCount = cliVsync;
                Application.targetFrameRate = cliVsync == 0 ? TargetFps : -1;
                return;
            }

            double hz = Screen.currentResolution.refreshRateRatio.value;
            if (hz >= 115.0 && hz <= 125.0)
            {
                // Màn 120 Hz (ProMotion / màn ngoài): vSyncCount = 2 -> 60 FPS ổn định, chia hết cho 120 Hz
                QualitySettings.vSyncCount = 2;
                Application.targetFrameRate = TargetFps;
            }
            else if (hz >= 57.0 && hz <= 63.0)
            {
                // Màn 60 Hz: vSyncCount = 1 đồng bộ trực tiếp vào CVDisplayLink / VSync
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = TargetFps;
            }
            else
            {
                // Tần số khác (75 Hz, 144 Hz...): giới hạn phần mềm về 60 FPS
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = TargetFps;
            }
        }

        static int ParseCliVsync()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-vera-vsync=", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(args[i].Substring("-vera-vsync=".Length), out int v))
                        return Mathf.Clamp(v, 0, 4);
                }
            }
            return -1;
        }
    }
}
