using System;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Bảng căn tay cầm hiện đè lên trang "Tay cầm":
    ///  • Căn joystick 6 bước (giữa, tiến, lùi, trái, phải, nhấn) như tools/canh_joystick.py: tự ghi khi giữ yên, tính bằng
    ///    VeraJoyCal.Compute, thử trực tiếp trước khi lưu. Bước giữa gửi lệnh J để firmware đặt tâm cần đúng chỗ buông tay.
    ///  • Căn trục BMI160 2 bước: cầm tự nhiên (cán dựng đứng, mũi chĩa ra trước) rồi chúc mũi xuống khoảng 45°; tính bằng
    ///    VeraMount.Compute (chung với phím F6 trong lab), lưu PlayerPrefs "vera_truc_gan" nên lab dùng luôn.
    /// </summary>
    public class ControllerCalPanel : MonoBehaviour
    {
        public enum Kind { None, Joystick, Mount }

        const float HoldJoy = 0.8f, HoldMount = 1.0f, HoldPress = 0.4f, PressTimeout = 8f;

        Func<ControllerLink> getLink;
        RectTransform root;
        Text title, stepText, hint, result, live;
        Image progress;
        readonly List<Text> stepLabels = new List<Text>();
        Button saveBtn, skipBtn, clearBtn;
        Text saveText;

        public Kind Current { get; private set; }
        public int Step { get; private set; }
        public bool HasResult => resultReady;

        string[] steps;
        float holdSince = -1f, stepStart;
        readonly List<Vector2> joyWin = new List<Vector2>();
        bool mvMode;  // gói có điện áp thật (USB): cả 6 bước đo bằng mV, tâm đo thẳng (không cần lệnh J)
        readonly List<Vector3> accWin = new List<Vector3>();
        Vector3 accPrev;
        Vector2 center, fwd, back, left, right;
        bool pressSeen, waitAfterJ;
        float jSentAt;
        Vector3 accFlat;
        VeraJoyCal.Cal joyResult;
        Quaternion mountResult;
        bool resultReady;

        static readonly string[] JoySteps = { "Giữa", "Tiến", "Lùi", "Trái", "Phải", "Nhấn" };
        static readonly string[] JoyHow =
        {
            "Buông tay khỏi cần, để cần tự về giữa",
            "Đẩy cần hết cỡ về phía TRƯỚC (về phía mũi tay cầm, như đi tới) và giữ yên",
            "Kéo cần hết cỡ về phía SAU (về phía cổ tay, như đi lùi) và giữ yên",
            "Gạt cần hết cỡ sang TRÁI và giữ yên",
            "Gạt cần hết cỡ sang PHẢI và giữ yên",
            "Ấn cần xuống (nút bấm) và giữ",
        };
        static readonly string[] MountSteps = { "Cầm tự nhiên", "Chúc mũi xuống" };
        static readonly string[] MountHow =
        {
            "Cầm tay cầm như lúc chơi: cán dựng đứng, mũi chĩa thẳng ra trước, giữ yên",
            "Giữ nguyên tay, chúc mũi tay cầm xuống khoảng 45° và giữ yên",
        };

        public static ControllerCalPanel Create(RectTransform page, Func<ControllerLink> link)
        {
            var rt = MenuKit.Full(page, "Bảng căn tay cầm");
            var p = rt.gameObject.AddComponent<ControllerCalPanel>();
            p.getLink = link;
            p.root = rt;
            p.Build();
            rt.gameObject.SetActive(false);
            return p;
        }

        void Build()
        {
            var shade = root.gameObject.AddComponent<Image>();
            shade.color = new Color(0.08f, 0.09f, 0.12f, 0.35f);
            shade.raycastTarget = true;
            var card = MenuKit.Fill(root, "Thẻ căn", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 480), MenuKit.Paper, 18f).transform;
            title = GlassKit.Label(card, "Tiêu đề căn", "", 26, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(32, -26), new Vector2(560, 36));
            for (int i = 0; i < 6; i++)
            {
                var t = GlassKit.Label(card, "Bước " + i, "", 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(32, -84 - i * 30), new Vector2(200, 26));
                stepLabels.Add(t);
            }
            stepText = GlassKit.Label(card, "Việc cần làm", "", 22, MenuKit.Accent, true, TextAnchor.UpperLeft, new Vector2(250, -80), new Vector2(480, 64));
            stepText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint = GlassKit.Label(card, "Gợi ý", "", 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(250, -150), new Vector2(480, 44));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            var bar = MenuKit.Fill(card, "Thanh giữ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(250, -204), new Vector2(480, 10), MenuKit.PaperHover, 1f);
            progress = MenuKit.Fill(bar.transform, "Mức giữ", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 10), MenuKit.Bio, 1f);
            result = GlassKit.Label(card, "Kết quả", "", 15, MenuKit.Ink, false, TextAnchor.UpperLeft, new Vector2(250, -228), new Vector2(480, 150));
            result.horizontalOverflow = HorizontalWrapMode.Wrap;
            live = GlassKit.Label(card, "Số hiện tại", "", 14, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(32, -380), new Vector2(700, 24));

            var close = MenuKit.MakeButton(card, "Nút đóng căn", "Đóng", new Vector2(-24, 24), new Vector2(110, 44), Close, 1, MenuKit.RadiusButton, new Vector2(1, 0));
            close.transform.Find("Chữ").GetComponent<Text>().fontSize = 15;
            saveBtn = MenuKit.MakeButton(card, "Nút lưu căn", "Lưu", new Vector2(-146, 24), new Vector2(130, 44), Save, 0, MenuKit.RadiusButton, new Vector2(1, 0));
            saveText = saveBtn.transform.Find("Chữ").GetComponent<Text>();
            saveText.fontSize = 15;
            var redo = MenuKit.MakeButton(card, "Nút làm lại căn", "Làm lại", new Vector2(24, 24), new Vector2(120, 44), () => Open(Current), 1, MenuKit.RadiusButton, new Vector2(0, 0));
            redo.transform.Find("Chữ").GetComponent<Text>().fontSize = 15;
            skipBtn = MenuKit.MakeButton(card, "Nút bỏ qua bước", "Bỏ qua bước", new Vector2(156, 24), new Vector2(140, 44), SkipPress, 1, MenuKit.RadiusButton, new Vector2(0, 0));
            skipBtn.transform.Find("Chữ").GetComponent<Text>().fontSize = 15;
            clearBtn = MenuKit.MakeButton(card, "Nút xoá căn", "Xoá căn đã lưu", new Vector2(308, 24), new Vector2(150, 44), ClearSaved, 1, MenuKit.RadiusButton, new Vector2(0, 0));
            clearBtn.transform.Find("Chữ").GetComponent<Text>().fontSize = 15;
        }

        public void Open(Kind k)
        {
            Current = k;
            Step = 0;
            resultReady = false;
            pressSeen = waitAfterJ = false;
            holdSince = -1f;
            stepStart = Time.unscaledTime;
            joyWin.Clear();
            accWin.Clear();
            steps = k == Kind.Joystick ? JoySteps : MountSteps;
            title.text = k == Kind.Joystick ? "Căn joystick" : "Căn trục cảm biến BMI160";
            result.text = "";
            result.color = MenuKit.Ink;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            MenuAnim.Enter(root.Find("Thẻ căn") as RectTransform, 0f, 10f, 0.22f);
        }

        public void Close()
        {
            Current = Kind.None;
            if (root != null) root.gameObject.SetActive(false);
        }

        void SkipPress()
        {
            if (Current == Kind.Joystick && Step == 5) FinishJoystick(false);
        }

        void ClearSaved()
        {
            if (Current == Kind.Joystick)
            {
                VeraJoyCal.Clear();
                result.text = "Đã xoá căn joystick: game dùng thẳng số firmware.";
            }
            else if (Current == Kind.Mount)
            {
                VeraMount.Clear();
                getLink()?.SetMount(Quaternion.identity);
                result.text = "Đã xoá căn trục BMI160.";
            }
            result.color = MenuKit.InkDim;
        }

        void Save()
        {
            if (!resultReady) return;
            if (Current == Kind.Joystick)
            {
                VeraJoyCal.Save(joyResult);
                result.text = "Đã lưu. Cần gạt trong sảnh và trong phòng thí nghiệm dùng phần căn này.\n" + result.text;
            }
            else if (Current == Kind.Mount)
            {
                VeraMount.Save(mountResult);
                getLink()?.SetMount(mountResult);
                result.text = "Đã lưu. Mô hình 3D và tia trỏ trong phòng thí nghiệm dùng trục này.\n" + result.text;
            }
            result.color = MenuKit.Bio;
            resultReady = false;
        }

        void Update()
        {
            if (Current == Kind.None) return;
            var link = getLink();
            for (int i = 0; i < stepLabels.Count; i++)
            {
                bool has = i < steps.Length;
                stepLabels[i].gameObject.SetActive(has);
                if (!has) continue;
                bool done = i < Step || resultReady && i <= Step;
                stepLabels[i].text = (done ? "✓  " : i == Step ? "▶  " : "·  ") + steps[i];
                stepLabels[i].color = done ? MenuKit.Bio : i == Step ? MenuKit.Accent : MenuKit.InkDim;
            }
            bool finished = Step >= steps.Length;
            saveBtn.interactable = resultReady;
            skipBtn.gameObject.SetActive(Current == Kind.Joystick && Step == 5);
            if (link == null || link.Active == null)
            {
                stepText.text = finished ? stepText.text : "Chờ tay cầm…";
                hint.text = finished ? hint.text : "Cắm cáp USB hoặc bật Wi-Fi của tay cầm.";
                live.text = "";
                progress.rectTransform.sizeDelta = new Vector2(0, 10);
                return;
            }
            var s = link.Latest;
            float now = Time.unscaledTime;
            if (Current == Kind.Joystick) TickJoystick(s, now, link);
            else TickMount(s, now);
        }

        // ---------------------------------------------------------------- joystick
        void TickJoystick(ControllerState s, float now, ControllerLink link)
        {
            Vector2 raw = s.JoyRaw;
            if (Step == 0 && joyWin.Count == 0 && !waitAfterJ) mvMode = s.HasJoyMv;
            Vector2 val = mvMode && s.HasJoyMv ? s.JoyMv : raw;
            float stableTh = mvMode ? 40f : 0.03f, farTh = mvMode ? VeraJoyCal.MinDeflectionMv : VeraJoyCal.MinDeflection;
            if (Step < JoySteps.Length)
            {
                stepText.text = JoyHow[Step];
                hint.text = Step == 0 ? "Tự ghi khi cần đứng yên 1 giây." : Step == 5 ? "Không thấy nút nhấn thì bấm \"Bỏ qua bước\" (kiểm tra dây SW, GPIO27)." :
                            "Tự ghi khi cần lệch đủ xa khỏi tâm và giữ yên.";
            }
            if (resultReady || Step >= JoySteps.Length)
            {
                var v = VeraJoyCal.Apply(joyResult, raw, s.JoyMv, s.HasJoyMv);
                live.text = (mvMode ? $"Thử ngay: chân X {s.JoyMv.x:0} mV, Y {s.JoyMv.y:0} mV" : $"Thử ngay: số firmware x {raw.x:+0.00;-0.00} y {raw.y:+0.00;-0.00}")
                            + $"  →  sau căn: phải {v.x:+0.00;-0.00}, tiến {v.y:+0.00;-0.00}";
                progress.rectTransform.sizeDelta = new Vector2(480, 10);
                return;
            }
            live.text = (mvMode ? $"Điện áp: chân X {s.JoyMv.x:0} mV, chân Y {s.JoyMv.y:0} mV" : $"Số firmware: x {raw.x:+0.00;-0.00}  y {raw.y:+0.00;-0.00}")
                        + (s.Has(VeraFlags.StickPress) ? "  · đang nhấn" : "");

            if (Step == 5)
            {
                bool press = s.Has(VeraFlags.StickPress) || s.Pressed(ControllerButtons.Joy2Press);
                if (press) { if (holdSince < 0) holdSince = now; } else holdSince = -1f;
                Progress(holdSince < 0 ? 0 : (now - holdSince) / HoldPress);
                if (holdSince >= 0 && now - holdSince >= HoldPress) { pressSeen = true; FinishJoystick(true); }
                else if (now - stepStart > PressTimeout) hint.text = "Chưa thấy nút nhấn sau 8 giây: kiểm tra dây SW (GPIO27) và GND, hoặc bấm \"Bỏ qua bước\".";
                return;
            }

            joyWin.Add(val);
            if (joyWin.Count > 30) joyWin.RemoveAt(0);
            bool stable = joyWin.Count >= 10 && Spread(joyWin) < stableTh;
            bool far = Step == 0 || Mathf.Max(Mathf.Abs(val.x - center.x), Mathf.Abs(val.y - center.y)) > farTh;
            if (waitAfterJ)
            {
                // lệnh J vừa đặt tâm trên mạch: chờ số firmware ổn định quanh tâm mới rồi ghi lại tâm
                if (now - jSentAt < 0.5f || !stable) { Progress(0.9f); return; }
                center = Mean(joyWin);
                waitAfterJ = false;
                NextStep(now);
                return;
            }
            if (stable && far) { if (holdSince < 0) holdSince = now; } else holdSince = -1f;
            float need = Step == 0 ? 1.0f : HoldJoy;
            Progress(holdSince < 0 ? 0 : (now - holdSince) / need);
            if (holdSince < 0 || now - holdSince < need) return;
            var m = Mean(joyWin);
            switch (Step)
            {
                case 0 when mvMode:
                    center = m;  // tâm đo thẳng bằng mV
                    break;
                case 0:
                    link.Send("J");  // firmware đặt tâm = vị trí đang buông
                    jSentAt = now;
                    waitAfterJ = true;
                    joyWin.Clear();
                    holdSince = -1f;
                    return;
                case 1: fwd = m; break;
                case 2: back = m; break;
                case 3: left = m; break;
                case 4: right = m; break;
            }
            NextStep(now);
        }

        void FinishJoystick(bool press)
        {
            Step = JoySteps.Length;
            var errors = new List<string>();
            var notes = new List<string>();
            bool ok = VeraJoyCal.Compute(center, fwd, back, left, right, out joyResult, errors, notes, mvMode);
            if (!press) notes.Add("Nút nhấn cần: không thấy (đã bỏ qua) — kiểm tra dây SW (GPIO27) và GND");
            var lines = new List<string>();
            if (ok)
            {
                lines.Add($"Tiến = trục {(joyResult.AxisFwd == 0 ? "x" : "y")} {(joyResult.SignFwd > 0 ? "dương" : "âm")}, " +
                          $"phải = trục {(joyResult.AxisRight == 0 ? "x" : "y")} {(joyResult.SignRight > 0 ? "dương" : "âm")} {(mvMode ? "của chân mạch" : "của firmware")}.");
                if (mvMode)
                    lines.Add($"Tâm X {joyResult.CenterMv.x:0} mV, Y {joyResult.CenterMv.y:0} mV (cố định, không phụ thuộc tâm firmware học lúc mở cổng). " +
                              $"Biên: tiến {joyResult.GainFwd:0}, lùi {joyResult.GainBack:0}, phải {joyResult.GainRight:0}, trái {joyResult.GainLeft:0} mV.");
                else
                    lines.Add($"Đẩy hết cỡ firmware chỉ ra: tiến {joyResult.GainFwd:0.00}, lùi {joyResult.GainBack:0.00}, " +
                              $"phải {joyResult.GainRight:0.00}, trái {joyResult.GainLeft:0.00} → game kéo giãn lên 1,0.");
                lines.Add("Đẩy thử cần, xem số \"sau căn\" bên dưới rồi bấm Lưu.");
            }
            lines.AddRange(errors);
            lines.AddRange(notes);
            result.text = string.Join("\n", lines);
            result.color = ok ? MenuKit.Ink : MenuKit.Error;
            resultReady = ok;
            stepText.text = ok ? "Xong, kiểm tra rồi bấm Lưu" : "Chưa căn được, bấm Làm lại";
            hint.text = "";
        }

        // ---------------------------------------------------------------- trục BMI160
        void TickMount(ControllerState s, float now)
        {
            Vector3 a = s.AccelG;
            live.text = $"Gia tốc x {a.x:+0.00;-0.00}  y {a.y:+0.00;-0.00}  z {a.z:+0.00;-0.00} g" + (s.Has(VeraFlags.ImuError) ? "  · LỖI cảm biến" : "");
            if (Step >= MountSteps.Length) { Progress(1); return; }
            stepText.text = MountHow[Step];
            hint.text = "Tự ghi khi tay cầm yên 1 giây.";
            bool still = (a - accPrev).magnitude < 0.02f && Mathf.Abs(a.magnitude - 1f) < 0.08f && !s.Has(VeraFlags.ImuError);
            accPrev = a;
            if (!still) { holdSince = -1f; accWin.Clear(); Progress(0); return; }
            if (holdSince < 0) holdSince = now;
            accWin.Add(a);
            Progress((now - holdSince) / HoldMount);
            if (now - holdSince < HoldMount) return;
            var mean = Vector3.zero;
            foreach (var v in accWin) mean += v;
            mean /= accWin.Count;
            if (Step == 0)
            {
                accFlat = mean;
                NextStep(now);
                accWin.Clear();
                return;
            }
            if (VeraMount.Compute(accFlat, mean, out var m, out float deg))
            {
                mountResult = m;
                resultReady = true;
                Step = MountSteps.Length;
                stepText.text = "Xong, bấm Lưu";
                hint.text = "";
                result.text = $"Chúc mũi {deg:0}° so với lúc cầm tự nhiên. Lưu xong, mô hình 3D sẽ đứng thẳng khi bạn cầm tự nhiên.";
                result.color = MenuKit.Ink;
            }
            else
            {
                hint.text = $"Mới chúc {deg:0}°: chúc mũi xuống nhiều hơn (khoảng 45°).";
                holdSince = -1f;
                accWin.Clear();
            }
        }

        // ---------------------------------------------------------------- tiện ích
        void NextStep(float now)
        {
            Step++;
            holdSince = -1f;
            stepStart = now;
            joyWin.Clear();
            accWin.Clear();
            Progress(0);
        }

        void Progress(float f) => progress.rectTransform.sizeDelta = new Vector2(480 * Mathf.Clamp01(f), 10);

        static float Spread(List<Vector2> w)
        {
            Vector2 lo = w[0], hi = w[0];
            foreach (var v in w) { lo = Vector2.Min(lo, v); hi = Vector2.Max(hi, v); }
            return Mathf.Max(hi.x - lo.x, hi.y - lo.y);
        }

        static Vector2 Mean(List<Vector2> w)
        {
            var m = Vector2.zero;
            foreach (var v in w) m += v;
            return m / Mathf.Max(1, w.Count);
        }
    }
}
