using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Khkt.Environment;
using Khkt.Interaction.Bio;
using Khkt.Menu;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Nối với lớp học trên web: khoảng 2 giây gọi POST /app/nhip một lần, báo học sinh đang ở đâu, cầm gì, và nhận về
    /// trình chiếu, thông báo, lệnh "Tất cả chú ý" của giáo viên. Chỉ chạy khi đã đăng nhập; mỗi máy tự hỏi và tự hiện,
    /// không đi qua Relay.
    /// </summary>
    public class PresentationClient : MonoBehaviour
    {
        [Serializable]
        class Cue
        {
            public long phien_ban;
            public string loai;
            public string tieu_de;
            public string noi_dung;
            public string anh;
            public string[] man_hinh;
            public double dem_nguoc_den;
        }

        [Serializable]
        class Notice
        {
            public long id;
            public string loai;
            public string noi_dung;
            public RobotStep[] ke_hoach;
        }

        [Serializable]
        class Session
        {
            public long phien_ban;
            public string che_do;
            public bool ai_bat = true;
            public bool robot_bat = true;
            public double het_gio;
            public int[] hoa_chat_khoa;
        }

        [Serializable]
        class Attention
        {
            public bool bat;
            public string noi_dung;
        }

        [Serializable]
        class Beat
        {
            public double may_chu_luc;
            public Cue trinh_chieu;
            public Notice[] lenh;
            public Attention chu_y;
            public Session buoi;
        }

        [Serializable]
        class BeatBody
        {
            public long da_hien;
            public long da_nhan_lenh;
            public long da_buoi;
            public bool dang_khoa;
            public int tang;
            public float x;
            public float z;
            public string dang_cam;
            public string dang_lam;
        }

        public static PresentationClient Instance { get; private set; }

        const float BeatInterval = 2f, IdleInterval = 5f, RetryInterval = 5f, TimeoutS = 5f;
        const float LockFailsafeS = 15f;
        const int MaxHeldChars = 80, MaxDoingChars = 160;

        /// <summary>Phiên bản đang hiện xong; chỉ đổi sau khi đã hiện, trang giáo viên đếm số máy theo số này.</summary>
        public long ShownVersion { get; private set; }
        public event Action<long> Shown;
        public long AckedNotice { get; private set; }
        public string LastNotice { get; private set; } = "";
        public bool IsLocked => locked;

        Texture2D shownImage;
        LabScreen.Page page;
        string[] targets;
        string bodyBase = "";
        double countdownEnd, serverOffset;
        float nextTick;
        bool passwordNoticeSaid;
        long queuedNotice;
        bool showingNotices, locked;
        float lastGoodBeat;
        readonly Queue<Notice> notices = new Queue<Notice>();
        DesktopViewer viewer;
        HandController hand;
        GameObject attentionPanel;
        Text attentionText, examText;
        GameObject examPanel;
        long appliedSession;
        static readonly Regex NoLockList = new Regex("\"hoa_chat_khoa\"\\s*:\\s*null");

        void Awake() => Instance = this;

        void OnEnable() => ZoneStreamer.ZoneChanged += OnZoneChanged;

        void OnDisable() => ZoneStreamer.ZoneChanged -= OnZoneChanged;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (shownImage != null) Destroy(shownImage);
            ClassRules.Reset();
        }

        IEnumerator Start()
        {
            viewer = GetComponent<DesktopViewer>();
            lastGoodBeat = Time.realtimeSinceStartup;
            var wait = new WaitForSecondsRealtime(BeatInterval);
            var waitRetry = new WaitForSecondsRealtime(RetryInterval);
            var waitIdle = new WaitForSecondsRealtime(IdleInterval);
            while (true)
            {
                bool ok = true;
                if (!string.IsNullOrEmpty(GameSession.Token)) yield return Heartbeat(r => ok = r);
                else
                {
                    SetLocked(false, "");
                    if (appliedSession != 0)
                    {
                        appliedSession = 0;
                        ClassRules.Reset();
                    }
                }
                if (ok) lastGoodBeat = Time.realtimeSinceStartup;
                else if (locked && Time.realtimeSinceStartup - lastGoodBeat > LockFailsafeS)
                {
                    // mất mạng thì giáo viên không mở khóa được: không để học sinh kẹt mãi
                    SetLocked(false, "");
                    LabMessages.Say("Mất kết nối với lớp: đã mở khóa điều khiển", LabMessages.Tone.Warn);
                }
                // mỗi nhịp tính vào hạn mức lượt gọi của máy chủ: chỉ hỏi dày khi đang chiếu, kiểm tra hoặc bị khóa
                bool busy = page != null || ClassRules.Exam || locked;
                yield return !ok ? waitRetry : busy ? wait : waitIdle;
            }
        }

        void Update()
        {
            UpdateExamClock();
            if (countdownEnd <= 0 || page == null || Time.realtimeSinceStartup < nextTick) return;
            nextTick = Time.realtimeSinceStartup + 1f;
            page.body = BodyWithCountdown();
            ApplyToScreens();
        }

        // =====================================================================
        // Hỏi máy chủ
        // =====================================================================

        IEnumerator Heartbeat(Action<bool> done)
        {
            string body = JsonUtility.ToJson(MakeBody());
            string text = null;
            using (var req = new UnityWebRequest(GameSession.ServerUrl + "/app/nhip", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.CeilToInt(TimeoutS);
                AccountClient.Authorize(req);
                yield return req.SendWebRequest();
                AccountClient.CheckExpired(req);
                if (req.responseCode == 403 && !passwordNoticeSaid)
                {
                    // tài khoản phải đổi mật khẩu trước: chỉ nhắc một lần, về màn hình chính là đổi được
                    passwordNoticeSaid = true;
                    LabMessages.Say("Tài khoản cần đổi mật khẩu để nhận trình chiếu: về màn hình chính để đổi", LabMessages.Tone.Warn);
                }
                if (req.result == UnityWebRequest.Result.Success) text = req.downloadHandler.text;
            }
            if (text == null)
            {
                done(false);
                yield break;
            }
            Beat beat = null;
            try { beat = JsonUtility.FromJson<Beat>(text); }
            catch (ArgumentException e) { Debug.LogWarning("[VERA] Nhịp trình chiếu không đọc được: " + e.Message); }
            if (beat == null)
            {
                done(false);
                yield break;
            }
            passwordNoticeSaid = false;
            serverOffset = beat.may_chu_luc - Time.realtimeSinceStartupAsDouble;
            SetLocked(beat.chu_y != null && beat.chu_y.bat, beat.chu_y != null ? beat.chu_y.noi_dung : "");
            ApplySession(beat.buoi, NoLockList.IsMatch(text));
            QueueNotices(beat.lenh);
            var cue = beat.trinh_chieu;
            if (cue != null && cue.phien_ban != 0 && cue.phien_ban != ShownVersion) yield return ShowCue(cue);
            done(true);
        }

        BeatBody MakeBody()
        {
            var b = new BeatBody { da_hien = ShownVersion, da_nhan_lenh = AckedNotice, da_buoi = appliedSession, dang_khoa = locked, dang_cam = "", dang_lam = "" };
            if (viewer != null)
            {
                Vector3 p = viewer.transform.position;
                b.tang = p.y >= 3.6f ? 2 : 1;  // cùng ngưỡng với ZoneMap.At
                b.x = Mathf.Clamp(p.x, -500f, 500f);
                b.z = Mathf.Clamp(p.z, -500f, 500f);
            }
            if (hand == null) hand = HandController.Active;
            if (hand != null)
            {
                if (hand.Held != null) b.dang_cam = Cut(hand.Held.name, MaxHeldChars);
                var proc = hand.procedure;
                if (proc != null && proc.Active && (int)proc.Current < proc.StepNames.Length)
                    b.dang_lam = Cut("Chuẩn độ: " + proc.StepNames[(int)proc.Current], MaxDoingChars);
            }
            return b;
        }

        static string Cut(string s, int max) => s == null ? "" : s.Length <= max ? s : s.Substring(0, max);

        // =====================================================================
        // Thông báo, yêu cầu chú ý
        // =====================================================================

        void QueueNotices(Notice[] list)
        {
            if (list == null) return;
            Array.Sort(list, (a, b) => a.id.CompareTo(b.id));
            foreach (var n in list)
            {
                if (n == null || n.id <= queuedNotice) continue;
                queuedNotice = n.id;
                notices.Enqueue(n);
            }
            if (notices.Count > 0 && !showingNotices) StartCoroutine(ShowNotices());
        }

        /// <summary>Hiện từng cái, đủ lâu để đọc, hiện xong mới báo đã nhận: máy chủ sẽ gửi lại nếu app tắt giữa chừng.</summary>
        IEnumerator ShowNotices()
        {
            showingNotices = true;
            while (notices.Count > 0)
            {
                var n = notices.Dequeue();
                if (n.loai == "robot")
                {
                    RunRobot(n);
                    AckedNotice = n.id;
                    continue;
                }
                LastNotice = n.noi_dung ?? "";
                float seconds = Mathf.Clamp(4f + LastNotice.Length / 12f, 5f, 15f);
                BioFeedback.ShowToast(LastNotice, false, seconds, "Từ giáo viên");
                yield return new WaitForSecondsRealtime(seconds);
                AckedNotice = n.id;
            }
            showingNotices = false;
        }

        /// <summary>Lệnh robot giáo viên giao: chạy ngay, kể cả lúc đang Kiểm tra.</summary>
        void RunRobot(Notice n)
        {
            LastRobotCommand = n.id;
            var robot = LabRobot.Instance;
            if (robot == null || n.ke_hoach == null || n.ke_hoach.Length == 0)
            {
                Debug.LogWarning("[VERA] Lệnh robot không chạy được: " + (robot == null ? "không có robot" : "kế hoạch trống"));
                return;
            }
            foreach (var s in n.ke_hoach)
                if (s != null && s.stt == 0 && s.doi_tuong != null && s.doi_tuong.StartsWith("hoa_chat:") && int.TryParse(s.doi_tuong.Substring(9), out int stt))
                    s.stt = stt;
            if (!string.IsNullOrEmpty(n.noi_dung)) LabMessages.Say(n.noi_dung, LabMessages.Tone.Info);
            robot.ExecutePlan(new List<RobotStep>(n.ke_hoach));
        }

        public long LastRobotCommand { get; private set; }

        void ApplySession(Session s, bool keepLockList)
        {
            if (s == null || s.phien_ban == 0 || s.phien_ban == appliedSession) return;
            bool exam = s.che_do == "kiem_tra";
            ClassRules.Apply(exam, s.ai_bat, s.robot_bat, s.het_gio, keepLockList ? null : (s.hoa_chat_khoa ?? new int[0]));
            appliedSession = s.phien_ban;
            if (exam) LabMessages.Say("Bắt đầu kiểm tra: gợi ý và trợ lý AI tạm tắt", LabMessages.Tone.Warn);
        }

        void UpdateExamClock()
        {
            bool show = ClassRules.Exam;
            if (show && examPanel == null) BuildExamClock();
            if (examPanel == null) return;
            if (examPanel.activeSelf != show) examPanel.SetActive(show);
            if (!show) return;
            double left = ClassRules.ExamEnd > 0 ? ClassRules.ExamEnd - (Time.realtimeSinceStartupAsDouble + serverOffset) : -1;
            examText.text = ClassRules.ExamEnd <= 0 ? "Đang kiểm tra" : left > 0 ? $"Đang kiểm tra · còn {(int)left / 60:00}:{(int)left % 60:00}" : "Đang kiểm tra · hết giờ";
        }

        void BuildExamClock()
        {
            var ui = FindAnyObjectByType<ModernUI>();
            if (ui == null || ui.Canvas == null) return;
            var plate = GlassKit.Panel(ui.Canvas.transform, "Đồng hồ kiểm tra", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(380, 46), null, 3f);
            plate.sprite = GlassKit.Solid;
            plate.color = new Color(0.45f, 0.18f, 0.05f, 0.9f);
            examText = GlassKit.Label(plate.transform, "Chữ", "", 19, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(380, 46));
            examPanel = plate.gameObject;
        }

        public bool ExamClockVisible => examPanel != null && examPanel.activeSelf;
        public string ExamClockText => examText != null ? examText.text : "";

        void SetLocked(bool want, string message)
        {
            if (want)
            {
                if (attentionPanel == null) BuildAttention();
                if (attentionText != null) attentionText.text = string.IsNullOrEmpty(message) ? "Dừng tay và nhìn lên giáo viên" : message;
                if (attentionPanel != null)
                {
                    attentionPanel.SetActive(true);
                    attentionPanel.transform.SetAsLastSibling();
                }
            }
            else if (attentionPanel != null) attentionPanel.SetActive(false);
            // UiCapture khóa đi lại, nhìn quanh, thao tác tay và các phím mở bảng khác
            UiBridge.SetCapture(ref locked, want);
        }

        void BuildAttention()
        {
            var ui = FindAnyObjectByType<ModernUI>();
            if (ui == null || ui.Canvas == null) return;
            UiBridge.EnsureInteractive(ui.Canvas);
            var dimRt = GlassKit.Rect(ui.Canvas.transform, "Tất cả chú ý", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.05f, 0.09f, 0.72f);
            dim.raycastTarget = true;  // chặn bấm vào các bảng phía sau
            var plate = GlassKit.Panel(dimRt, "Khung", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 320), null, 1.4f);
            plate.sprite = GlassKit.Solid;
            plate.color = new Color(0.04f, 0.07f, 0.12f, 0.96f);
            GlassKit.Label(plate.transform, "Tiêu đề", "Tất cả chú ý", 44, GlassKit.Warn, true, TextAnchor.UpperCenter, new Vector2(40, -44), new Vector2(780, 60));
            attentionText = GlassKit.Label(plate.transform, "Nội dung", "", 26, GlassKit.Ink, false, TextAnchor.UpperCenter, new Vector2(40, -122), new Vector2(780, 120));
            GlassKit.Label(plate.transform, "Ghi chú", "Điều khiển đang tạm khóa, giáo viên sẽ mở lại", 17, GlassKit.Dim, false, TextAnchor.UpperCenter, new Vector2(40, -262), new Vector2(780, 28));
            attentionPanel = dimRt.gameObject;
        }

        // =====================================================================
        // Trình chiếu
        // =====================================================================

        IEnumerator ShowCue(Cue cue)
        {
            if (cue.loai == "slide")
            {
                Texture2D tex = null;
                yield return DownloadImage(cue.anh, t => tex = t);
                if (tex == null) yield break;  // nhịp sau thử lại; chưa hiện thì chưa báo đã hiện
                Replace(new LabScreen.Page { image = tex }, cue, tex);
            }
            else if (cue.loai == "chu")
            {
                bodyBase = cue.noi_dung ?? "";
                countdownEnd = cue.dem_nguoc_den;
                var p = new LabScreen.Page { title = cue.tieu_de ?? "", body = bodyBase };
                Replace(p, cue, null);
                if (countdownEnd > 0)
                {
                    p.body = BodyWithCountdown();
                    ApplyToScreens();
                }
            }
            else
            {
                Replace(null, cue, null);
            }
            ShownVersion = cue.phien_ban;
            Shown?.Invoke(cue.phien_ban);
        }

        IEnumerator DownloadImage(string path, Action<Texture2D> done)
        {
            // chỉ nhận đường dẫn trên chính máy chủ này: mã phiên không được gửi đi nơi khác
            if (string.IsNullOrEmpty(path) || path[0] != '/' || path.StartsWith("//"))
            {
                Debug.LogWarning("[VERA] Đường dẫn ảnh trình chiếu không hợp lệ: " + path);
                yield break;
            }
            using (var req = UnityWebRequest.Get(GameSession.ServerUrl + path))
            {
                req.timeout = 30;
                AccountClient.Authorize(req);
                yield return req.SendWebRequest();
                AccountClient.CheckExpired(req);
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[VERA] Không tải được ảnh trình chiếu ({req.responseCode}): {path}");
                    yield break;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "Trình chiếu", anisoLevel = 4 };
                if (!tex.LoadImage(req.downloadHandler.data, true))
                {
                    Destroy(tex);
                    yield break;
                }
                done(tex);
            }
        }

        // =====================================================================
        // Hiện lên màn hình
        // =====================================================================

        void Replace(LabScreen.Page next, Cue cue, Texture2D nextImage)
        {
            if (next == null || cue.loai != "chu") countdownEnd = 0;
            var old = shownImage;
            page = next;
            shownImage = nextImage;
            targets = cue.man_hinh ?? Array.Empty<string>();
            ApplyToScreens();
            if (old != null) Destroy(old);  // màn hình đã trỏ sang trang mới, xóa ảnh cũ để không đầy bộ nhớ
        }

        void ApplyToScreens()
        {
            foreach (var s in LabScreen.All)
            {
                if (page != null && Matches(s.screenId, targets)) s.Present(page);
                else s.EndPresentation();
            }
        }

        /// <summary>Mã "ban" khớp "ban_3"; "tuong_video" không khớp "t2_tuong_video_1".</summary>
        static bool Matches(string id, IEnumerable<string> codes)
        {
            if (codes == null) return false;
            foreach (string m in codes)
                if (!string.IsNullOrEmpty(m) && (id == m || id.StartsWith(m + "_", StringComparison.Ordinal))) return true;
            return false;
        }

        // màn hình ở khu vừa tải chưa biết đang chiếu gì
        void OnZoneChanged(int zone, bool loaded)
        {
            if (loaded && page != null) ApplyToScreens();
        }

        string BodyWithCountdown()
        {
            // dùng giờ máy chủ, giờ máy học sinh có thể sai
            double left = countdownEnd - (Time.realtimeSinceStartupAsDouble + serverOffset);
            string clock = left > 0 ? $"Còn {(int)left / 60:00}:{(int)left % 60:00}" : "Hết giờ";
            return string.IsNullOrEmpty(bodyBase) ? clock : bodyBase + "\n\n" + clock;
        }
    }
}
