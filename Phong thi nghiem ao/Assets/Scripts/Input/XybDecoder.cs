using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Đổi dòng chữ "X,Y,B" của bộ joystick → Arduino Mega → ESP32 thành ControllerState của tay cầm 1 cần.
    /// X, Y là analogRead thô: 0-1023 (Mega) hoặc 0-4095 (đọc thẳng bằng ESP32, tự nhận ra khi thấy giá trị trên 1023).
    /// Tâm: trung bình các mẫu đầu tiên (mạch joystick rẻ thường lệch tâm, ví dụ nghỉ ở 409 thay vì 512, hoặc cấp 3,3 V
    /// cho Mega đo theo 5 V). Biên độ: tự học dần, lần đầu đẩy hết cỡ là từ đó đẩy hết cỡ ra đúng 1.
    /// Chỉ gọi ToState từ một luồng.
    /// </summary>
    public sealed class XybDecoder
    {
        const int CalibrationSamples = 15;

        public float CenterX => cx;
        public float CenterY => cy;

        int n, scale = 1023;
        float sumX, sumY, cx = 511.5f, cy = 511.5f;
        // độ lệch lớn nhất đã thấy mỗi phía của mỗi trục (âm, dương)
        float loX, hiX, loY, hiY, prevX, prevY;

        public void Reset()
        {
            n = 0;
            scale = 1023;
            sumX = sumY = 0;
            cx = cy = 511.5f;
            loX = hiX = loY = hiY = prevX = prevY = 0;
        }

        /// <summary>"409,387,0" → (409, 387, false). Bỏ dòng hỏng (nhiễu đường truyền) thay vì cho số sai vào game.</summary>
        public static bool TryParse(string line, out int x, out int y, out bool pressed)
        {
            x = y = 0;
            pressed = false;
            var p = (line ?? "").Trim().Split(',');
            if (p.Length != 3 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out int b))
                return false;
            if (x < 0 || y < 0 || x > 4095 || y > 4095 || (b != 0 && b != 1))
                return false;
            pressed = b == 1;
            return true;
        }

        public ControllerState ToState(int x, int y, bool pressed, float deadzone, bool invertX, bool invertY, bool swapAxes)
        {
            if ((x > 1023 || y > 1023) && scale != 4095)
            {
                scale = 4095;  // đọc thẳng 12 bit: lấy lại tâm
                n = 0;
                sumX = sumY = 0;
                loX = hiX = loY = hiY = prevX = prevY = 0;
            }
            if (n < CalibrationSamples)
            {
                n++;
                sumX += x;
                sumY += y;
                cx = sumX / n;
                cy = sumY / n;
            }
            float jx = Axis(x, cx, ref loX, ref hiX, ref prevX, deadzone);
            float jy = Axis(y, cy, ref loY, ref hiY, ref prevY, deadzone);
            if (swapAxes) (jx, jy) = (jy, jx);
            var buttons = ControllerButtons.SingleStick | ControllerButtons.ImuError;
            if (pressed) buttons |= ControllerButtons.Joy2Press;
            return new ControllerState
            {
                OrientationRH = Quaternion.identity,
                Joy2 = new Vector2(invertX ? -jx : jx, invertY ? -jy : jy),
                Buttons = buttons,
            };
        }

        float Axis(int raw, float center, ref float lo, ref float hi, ref float prev, float deadzone)
        {
            // Biên độ giả định ban đầu: 35% khoảng từ tâm tới mép gần nhất; đẩy xa hơn thì nới ra.
            // Chỉ nới theo giá trị giữ được 2 mẫu liền nhau, để một mẫu nhiễu đường truyền không làm cần "chai" mãi.
            float guess = 0.35f * Mathf.Min(center, scale - center);
            float d = raw - center;
            if (n >= CalibrationSamples)
            {
                if (d > 0 && prev > 0) hi = Mathf.Max(hi, Mathf.Min(d, prev));
                if (d < 0 && prev < 0) lo = Mathf.Max(lo, Mathf.Min(-d, -prev));
            }
            prev = d;
            float span = d >= 0 ? Mathf.Max(hi, guess) : Mathf.Max(lo, guess);
            float v = Mathf.Clamp(d / Mathf.Max(span, 1f), -1f, 1f);
            if (Mathf.Abs(v) < deadzone) return 0;
            return Mathf.Sign(v) * (Mathf.Abs(v) - deadzone) / (1 - deadzone);
        }
    }
}
