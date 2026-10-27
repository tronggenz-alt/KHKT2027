using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Số học sinh ĐỌC ĐƯỢC trên buret, có tính sai số thị sai theo vị trí mắt thật trong VR.
    ///
    /// Vạch in ở thành ngoài ống, mặt khum ở giữa. Mắt cao hơn mặt khum Δy, cách trục D thì tia
    /// nhìn cắt thành ống cao hơn đáy mặt khum Δy·r/D, nên đọc lệch (buret: đọc NHỎ hơn khi nhìn
    /// từ trên). Bản Python có kiểm thử: khkt/titration.py, parallax_error_ml.
    ///
    /// Màn hình điện thoại không đủ nét để thấy vạch 0,1 ml, nên khi nhìn vào buret sẽ hiện kính
    /// lúp với số đọc này; học sinh vẫn phải tự cúi cho ngang vạch để số đúng.
    /// </summary>
    public class MeniscusReading : MonoBehaviour
    {
        public BuretStopcock buret;
        public Transform eye;
        [Tooltip("Vị trí vạch 0 ml (đỉnh thang chia)")]
        public Transform zeroMark;
        [Tooltip("Chiều dài thang chia ứng với 1 ml (m). Buret 50 ml ~ 0,0105 m/ml")]
        public float metersPerMl = 0.0105f;
        public float wallRadiusM = 0.007f;
        [Tooltip("Chế độ luyện tập: hiện gợi ý 'mắt cao hơn vạch ... cm'")]
        public bool practiceHints = true;

        public Vector3 MeniscusPosition => zeroMark.position + Vector3.down * (buret.ReadingMl * metersPerMl);

        public float EyeAboveMeniscusM => eye.position.y - MeniscusPosition.y;

        public float ParallaxErrorMl
        {
            get
            {
                Vector3 d = eye.position - MeniscusPosition;
                float dist = Mathf.Max(wallRadiusM * 1.5f, new Vector2(d.x, d.z).magnitude);
                float dh = d.y * wallRadiusM / dist;
                return -dh / metersPerMl;
            }
        }

        /// <summary>Số đọc hiển thị trên kính lúp, làm tròn 0,05 ml như đọc buret thật.</summary>
        public float DisplayedReadingMl => Mathf.Round((buret.ReadingMl + ParallaxErrorMl) / 0.05f) * 0.05f;

        public string Hint()
        {
            if (!practiceHints) return "";
            float cm = EyeAboveMeniscusM * 100f;
            if (Mathf.Abs(cm) < 1f) return "Mắt đã ngang vạch";
            return cm > 0 ? $"Mắt cao hơn vạch {cm:0} cm: hạ mắt xuống" : $"Mắt thấp hơn vạch {-cm:0} cm: nâng mắt lên";
        }
    }
}
