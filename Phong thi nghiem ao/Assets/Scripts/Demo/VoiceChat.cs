using System;
using System.Collections;
using Khkt.Demo.Ai;
using Khkt.Input;
using Khkt.Menu;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Nói tiếng Việt với trợ lý AI (bản đi thi 27/11/2026):
    ///  • Tay cầm VERA: giữ nút chạm A 3 giây (lúc tay rảnh và không trỏ vào dụng cụ nào) thì khung trợ lý mở và bắt đầu thu âm;
    ///    giữ tiếp để nói, THẢ A là chuyển lời nói thành chữ và gửi cho AI.
    ///  • Nút Mic trên khung trợ lý (hoặc phím V khi khung mở): bấm để bắt đầu nói, bấm lần nữa để gửi.
    ///  • Chuyển giọng nói thành chữ bằng AI của Google (Gemini, khóa API ở Cài đặt → Trợ lý AI); khóa Groq / OpenAI dùng
    ///    Whisper. Ghi 16 kHz, tối đa 30 giây, gửi một lần khi thả (không phát trực tuyến).
    /// </summary>
    public class VoiceChat : MonoBehaviour
    {
        public enum Mode { Idle, Listening, Transcribing }

        public const float HoldSeconds = 3f;
        public const int Rate = 16000, MaxSeconds = 30;
        const float ReleaseGrace = 0.2f, PressGrace = 0.25f;

        public static VoiceChat Instance { get; private set; }
        public Mode State { get; private set; }
        /// <summary>Dòng trạng thái ngắn cho khung trợ lý (lỗi, đang xử lý, câu vừa nghe).</summary>
        public string Message { get; private set; } = "";
        public float Level { get; private set; }
        /// <summary>Kiểm thử: không dùng micro thật, tự sinh tiếng giả khi dừng.</summary>
        public static bool SimulatedMic;
        /// <summary>Kiểm thử: nhận chữ thay vì gửi cho khung trợ lý.</summary>
        public static Action<string> TestSink;
        public bool FromController { get; private set; }

        AudioClip clip;
        string device;
        float startedAt, aHoldStart = -1f, aFalseSince = -1f, lastTrue, msgUntil;
        bool aWasFree;
        bool busy;

        void Awake() => Instance = this;
        void OnDestroy()
        {
            if (State == Mode.Listening) Cancel();
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Nút A của tay cầm
        // ------------------------------------------------------------------

        bool ReadA()
        {
            var vp = VeraPointer.Instance;
            var src = vp != null ? vp.Source : null;
            if (src == null || !src.IsConnected) return false;
            return src.Latest.Has(VeraFlags.TouchA);
        }

        bool CanStartHold()
        {
            if (RobotChatUI.Instance == null || ClassRules.ChatLocked) return false;
            if (RobotChatUI.Instance.IsOpen) return true;
            var hand = FindAnyObjectByType<HandController>();
            if (hand == null) return true;
            // tay đang cầm vật: giữ A là dùng vật (rót, nhỏ giọt); đang trỏ thiết bị: A là bấm thiết bị
            return hand.Held == null && (hand.Current == HandController.Act.None || hand.Current == HandController.Act.PlaceSurface);
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame && RobotChatUI.Instance != null && RobotChatUI.Instance.IsOpen && !UiBridge.Typing) Toggle();

            bool a = ReadA();
            float now = Time.unscaledTime;
            if (State == Mode.Idle && Message.Length > 0 && now > msgUntil) Message = "";
            if (!a) aWasFree = true;

            if (State == Mode.Listening)
            {
                UpdateLevel();
                if (now - startedAt > MaxSeconds - 0.3f) { StopAndSend(); return; }
                if (FromController)
                {
                    if (a) aFalseSince = -1f;
                    else if (aFalseSince < 0f) aFalseSince = now;
                    if (aFalseSince >= 0f && now - aFalseSince >= ReleaseGrace) StopAndSend();
                }
                return;
            }
            if (State != Mode.Idle) { Hint(null); return; }

            // đếm 3 giây giữ A (A chập chờn dưới 0,25 s thì không tính là thả)
            if (!a)
            {
                if (aHoldStart >= 0f && now - lastTrue >= PressGrace) { aHoldStart = -1f; Hint(null); }
                return;
            }
            lastTrue = now;
            if (!aWasFree) return;   // A báo chạm liên tục từ lúc nối (dây chập): chờ thả rồi mới tính
            if (aHoldStart < 0f)
            {
                if (!CanStartHold()) return;
                aHoldStart = now;
            }
            float t = now - aHoldStart;
            if (t >= HoldSeconds)
            {
                aHoldStart = -1f;
                aWasFree = false;   // phải thả A rồi mới tính lần giữ sau
                FromController = true;
                Hint(null);
                StartListening();
                return;
            }
            if (t >= 0.5f)
            {
                int bars = Mathf.Clamp(Mathf.FloorToInt(t / HoldSeconds * 10f), 0, 10);
                Hint($"Giữ nút A để nói với trợ lý  [{new string('#', bars)}{new string('-', 10 - bars)}]  còn {HoldSeconds - t:0.0} s");
            }
        }

        static void Hint(string s) { if (RobotChatUI.Instance != null) RobotChatUI.Instance.SetVoiceHint(s); }

        // ------------------------------------------------------------------
        // Thu âm
        // ------------------------------------------------------------------

        public void Toggle()
        {
            if (State == Mode.Listening) StopAndSend();
            else if (State == Mode.Idle) { FromController = false; StartListening(); }
        }

        public bool StartListening()
        {
            if (State != Mode.Idle) return false;
            var chat = RobotChatUI.Instance;
            if (chat != null) chat.Open();
            if (SimulatedMic)
            {
                State = Mode.Listening;
                startedAt = Time.unscaledTime;
                Message = "● Đang nghe… nói tiếp, thả A (hoặc bấm Dừng) để gửi";
                return true;
            }
            if (Microphone.devices.Length == 0)
            {
                Fail("Không thấy micro: cắm micro hoặc bật quyền micro cho ứng dụng (Cài đặt Windows → Quyền riêng tư → Micro)");
                return false;
            }
            device = Microphone.devices[0];
            foreach (var d in Microphone.devices)
            {
                string n = d.ToLowerInvariant();
                if (n.Contains("micro") || n.Contains("mic") || n.Contains("array")) { device = d; break; }
            }
            clip = Microphone.Start(device, false, MaxSeconds, Rate);
            if (clip == null) { Fail("Không mở được micro"); return false; }
            State = Mode.Listening;
            startedAt = Time.unscaledTime;
            aFalseSince = -1f;
            Message = FromController ? "● Đang nghe… giữ A và nói, thả A để gửi" : "● Đang nghe… nói rồi bấm Dừng để gửi";
            LabMessages.Say("Trợ lý đang nghe: hãy nói tiếng Việt", LabMessages.Tone.Info);
            return true;
        }

        void UpdateLevel()
        {
            if (SimulatedMic || clip == null) { Level = 0.3f; return; }
            int pos = Microphone.GetPosition(device);
            if (pos < 256) return;
            var buf = new float[256];
            clip.GetData(buf, pos - 256);
            float peak = 0;
            foreach (var v in buf) peak = Mathf.Max(peak, Mathf.Abs(v));
            Level = Mathf.Lerp(Level, peak, 0.5f);
        }

        public void Cancel()
        {
            if (!SimulatedMic && Microphone.IsRecording(device)) Microphone.End(device);
            State = Mode.Idle;
            Message = "";
            clip = null;
        }

        public void StopAndSend()
        {
            if (State != Mode.Listening) return;
            float[] samples;
            if (SimulatedMic)
            {
                samples = new float[Rate * 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = 0.4f * Mathf.Sin(2f * Mathf.PI * 220f * i / Rate);
            }
            else
            {
                int pos = Microphone.GetPosition(device);
                Microphone.End(device);
                if (clip == null || pos <= 0) { Fail("Không thu được âm thanh"); return; }
                samples = new float[pos * clip.channels];
                clip.GetData(samples, 0);
                if (clip.channels > 1)
                {
                    var mono = new float[pos];
                    for (int i = 0; i < pos; i++) mono[i] = samples[i * clip.channels];
                    samples = mono;
                }
            }
            clip = null;
            if (samples.Length < Rate * 0.5f) { Fail("Nói quá ngắn: giữ lâu hơn một chút rồi thả"); return; }
            float peak = 0;
            foreach (var v in samples) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak < 0.004f) { Fail("Không nghe thấy tiếng: kiểm tra micro đã bật chưa (hoặc nói to hơn)"); return; }
            if (peak < 0.35f)
            {
                float g = 0.7f / peak;
                for (int i = 0; i < samples.Length; i++) samples[i] *= g;
            }
            State = Mode.Transcribing;
            Message = "Đang chuyển lời nói thành chữ…";
            var wav = EncodeWav(samples, Rate);
            StartCoroutine(Transcribe(wav));
        }

        IEnumerator Transcribe(byte[] wav)
        {
            var key = ApiKeyStore.Current;
            if (key == null)
            {
                Fail("Chưa có khóa API: Cài đặt → Trợ lý AI → dán khóa Gemini (miễn phí ở aistudio.google.com) để nói chuyện với trợ lý");
                yield break;
            }
            string text = null, err = null;
            yield return DirectAi.Transcribe(key, wav, t => text = t, x => err = x);
            if (err != null) { Fail(err); yield break; }
            text = (text ?? "").Trim();
            if (text.Length == 0) { Fail("Không nghe rõ lời nói: thử nói chậm và gần micro hơn"); yield break; }
            State = Mode.Idle;
            Message = "";
            if (TestSink != null) { TestSink(text); yield break; }
            if (RobotChatUI.Instance != null) RobotChatUI.Instance.Send(text);
        }

        void Fail(string why)
        {
            State = Mode.Idle;
            Message = why;
            msgUntil = Time.unscaledTime + 8f;
            Debug.LogWarning("[Giọng nói] " + why);
            LabMessages.Say(why, LabMessages.Tone.Warn);
            if (TestSink != null) TestSink("##LỖI## " + why);
        }

        // ------------------------------------------------------------------
        // WAV 16 bit mono
        // ------------------------------------------------------------------

        public static byte[] EncodeWav(float[] samples, int rate)
        {
            int n = samples.Length, bytes = n * 2;
            var b = new byte[44 + bytes];
            void S(int o, string s) { for (int i = 0; i < s.Length; i++) b[o + i] = (byte)s[i]; }
            void I(int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24); }
            S(0, "RIFF"); I(4, 36 + bytes); S(8, "WAVE"); S(12, "fmt "); I(16, 16);
            b[20] = 1; b[22] = 1; I(24, rate); I(28, rate * 2); b[32] = 2; b[34] = 16;
            S(36, "data"); I(40, bytes);
            for (int i = 0; i < n; i++)
            {
                short v = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767);
                b[44 + 2 * i] = (byte)v;
                b[45 + 2 * i] = (byte)(v >> 8);
            }
            return b;
        }
    }
}
