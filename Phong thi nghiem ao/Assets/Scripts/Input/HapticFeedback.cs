using System.Collections;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// "Ngôn ngữ rung và đèn" thống nhất cho cả phòng lab (docs/01-de-xuat-dieu-khien.md, mục 5).
    /// Mỗi ý nghĩa có một mẫu riêng để học sinh học thuộc như học tín hiệu đèn giao thông.
    /// </summary>
    public class HapticFeedback : MonoBehaviour
    {
        public ControllerSource source;

        Coroutine running;

        /// <summary>Một giọt rơi (chỉ khi nhỏ từng giọt, dưới ~4 giọt/s).</summary>
        public void Drop() => source.Pulse(150, 45);

        /// <summary>Cầm được / thả vật.</summary>
        public void GrabConfirm() => source.Pulse(110, 60);

        /// <summary>Gợi ý ở chế độ luyện tập: sắp tới điểm tương đương. Hai nhịp nhẹ + đèn xanh.</summary>
        public void NearEndpointHint() => Play(Pattern(new[] { 140, 0, 140 }, new[] { 70, 90, 70 }, green: true, red: false));

        /// <summary>Thao tác sai (quên tráng, rót tràn): rung mạnh 0,6 s + đèn đỏ.</summary>
        public void Mistake() => Play(Pattern(new[] { 255 }, new[] { 600 }, green: false, red: true));

        /// <summary>Sự cố nguy hiểm (cháy, bắn hóa chất): rung ngắt quãng + đèn đỏ nháy.</summary>
        public void Danger() => Play(Pattern(new[] { 255, 0, 255, 0, 255 }, new[] { 150, 100, 150, 100, 150 }, false, true));

        /// <summary>Đang chạm vật nóng: rung liên tục theo nhiệt độ (gọi mỗi khung hình, 0 = tắt).</summary>
        public void Heat(float level01) => source.SetVibration(Mathf.RoundToInt(Mathf.Clamp01(level01) * 200));

        void Play(IEnumerator pattern)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(pattern);
        }

        IEnumerator Pattern(int[] levels, int[] ms, bool green, bool red)
        {
            source.SetLeds(green, red);
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] > 0) source.Pulse(levels[i], ms[i]);
                yield return new WaitForSeconds(ms[i] / 1000f);
            }
            source.SetLeds(false, false);
            running = null;
        }
    }
}
