using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Thông báo ngắn trên đầu màn hình, dùng chung cho quy trình, bàn tay, robot. Thẻ hiện ra do LabToasts vẽ;
    /// Message / MessageTone / MessageTime giữ lại cho quy trình và bài kiểm thử đọc thông báo gần nhất.
    /// </summary>
    public static class LabMessages
    {
        public enum Tone { Info, Good, Warn }

        public static string Message { get; private set; } = "";
        public static Tone MessageTone { get; private set; }
        public static float MessageTime { get; private set; } = -100f;

        public static void Say(string text, Tone tone = Tone.Info)
        {
            if (text == Message && Time.time - MessageTime < 2f) return;  // tránh nhấp nháy khi lặp lại
            Message = text;
            MessageTone = tone;
            MessageTime = Time.time;
            if (tone == Tone.Warn) Khkt.Input.PadInput.Pulse(220, 160);  // cảnh báo: rung mạnh tay cầm Wi-Fi
            LabToasts.Push(text, tone == Tone.Warn ? LabToasts.Tone.Warn : tone == Tone.Good ? LabToasts.Tone.Good : LabToasts.Tone.Info,
                           tone == Tone.Warn ? 5f : 4f);
        }
    }
}
