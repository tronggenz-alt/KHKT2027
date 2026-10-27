using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>Thông báo ngắn trên đầu màn hình (một dòng), dùng chung cho quy trình, bàn tay, robot.</summary>
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
        }
    }
}
