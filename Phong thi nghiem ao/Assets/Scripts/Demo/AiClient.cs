using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Khkt.Menu;
using Khkt.Demo.Ai;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Khkt.Demo
{
    /// <summary>
    /// Gọi AI cho robot và chú thích hiện tượng. Mặc định qua máy chủ của dự án (khóa nằm trên máy chủ);
    /// nếu tài khoản đang đăng nhập đã nhập khóa API riêng (ApiKeyStore) thì gọi thẳng nhà cung cấp, kiểm tra kế hoạch ngay trong app.
    /// Máy chủ không chạy thì mọi thứ vẫn dùng được: chú thích soạn sẵn, robot hiểu lệnh cơ bản.
    /// </summary>
    public class AiClient : MonoBehaviour
    {
        [Tooltip("Địa chỉ máy chủ AI (uvicorn server.ai.main:app --port 8000)")]
        public string serverUrl
        {
            get => GameSession.ServerUrl;
            set => GameSession.ServerUrl = value;
        }
        public float timeoutS = 25f;
        const float PlanTimeoutS = 90f;  // kế hoạch robot dài; máy chủ thử mô hình mạnh trước, lỗi thì chuyển mô hình khác (tối đa 80 s)

        public static AiClient Instance { get; private set; }
        /// <summary>Máy chủ AI đang chạy và đã cấu hình mô hình.</summary>
        public bool ServerOnline { get; private set; }
        /// <summary>Tài khoản đang đăng nhập có khóa API riêng: gọi thẳng nhà cung cấp.</summary>
        public bool Direct => ApiKeyStore.HasKey;
        /// <summary>Có AI để dùng (khóa riêng hoặc máy chủ).</summary>
        public bool Online => Direct || ServerOnline;
        string serverProvider = "", serverModel = "";
        public string Provider => Direct ? "khóa riêng " + ApiKeyStore.ProviderName(ApiKeyStore.Current.provider) : serverProvider;
        public string Model => Direct ? ApiKeyStore.Current.ModelOrDefault : serverModel;
        /// <summary>Lỗi gần nhất của đường gọi thẳng (để giao diện báo người dùng).</summary>
        public string LastDirectError { get; private set; } = "";

        [Serializable] class Health { public bool ok; public string nha_cung_cap; public string mo_hinh; }

        [Serializable]
        public class AnnotateRequest
        {
            public string tieu_de, phuong_trinh, chi_tiet, muc_do, loai;
            public string[] thanh_phan;
            public float ph, nhiet_do;
            public string ngu_canh;
        }

        [Serializable] public class AnnotateReply { public string giai_thich; public string nguon; }

        [Serializable]
        public class AssistRequest
        {
            public string cau_hoi;
            public string ngu_canh;
            public string[] lich_su;
        }

        [Serializable] public class RobotAction { public string loai; public int stt; public string ghi_chu; }
        [Serializable] public class AssistReply { public string tra_loi; public RobotAction[] hanh_dong; public string nguon; }

        [Serializable] public class PlanRequest { public string cau_hoi; public SceneSnapshot the_gioi; public string[] lich_su; }
        [Serializable] public class PlanReply { public string tra_loi; public RobotStep[] ke_hoach; public string[] bo_qua; public string nguon; }

        /// <summary>Robot: gửi câu hỏi + ảnh chụp cảnh, nhận lời đáp + kế hoạch kỹ năng (đã kiểm tra trên máy chủ).</summary>
        public void Plan(PlanRequest req, Action<PlanReply> ok, Action<string> fail = null)
        {
            if (Direct) { StartCoroutine(PlanDirect(req, ok, fail)); return; }
            if (!ServerOnline) { fail?.Invoke("ngoại tuyến"); return; }
            StartCoroutine(Post("/robot/ke-hoach", req, ok, fail, Mathf.Max(timeoutS, PlanTimeoutS)));
        }

        public const string ServerUrlPrefKey = "KHKT_AI_SERVER_URL";
        bool firstCheck = true;

        void Awake()
        {
            Instance = this;
            if (PlayerPrefs.HasKey(ServerUrlPrefKey))
            {
                string saved = PlayerPrefs.GetString(ServerUrlPrefKey);
                if (!string.IsNullOrWhiteSpace(saved)) serverUrl = saved;
            }
        }

        public void SetServerUrl(string url)
        {
            serverUrl = url;
            PlayerPrefs.SetString(ServerUrlPrefKey, url);
            PlayerPrefs.Save();
        }

        IEnumerator Start()
        {
            if (Application.isBatchMode) yield break;
            while (true)
            {
                yield return Get<Health>("/suc-khoe", h =>
                {
                    Reachable = h != null;
                    ServerOnline = h != null && h.ok;
                    serverProvider = h?.nha_cung_cap ?? "";
                    serverModel = h?.mo_hinh ?? "";
                    if (firstCheck && ServerOnline)
                    {
                        LabMessages.Say($"Đã kết nối máy chủ AI ({Provider})", LabMessages.Tone.Good);
                        firstCheck = false;
                    }
                }, _ =>
                {
                    ServerOnline = false;
                    Reachable = false;
                    if (firstCheck)
                    {
                        LabMessages.Say("Máy chủ AI ngoại tuyến (phòng lab vẫn chạy bình thường)", LabMessages.Tone.Info);
                        firstCheck = false;
                    }
                }, 3f);
                yield return new WaitForSecondsRealtime(ServerOnline ? 60f : 15f);
            }
        }

        public void Annotate(AnnotateRequest req, Action<AnnotateReply> ok, Action<string> fail = null)
        {
            if (Direct) { StartCoroutine(AnnotateDirect(req, ok, fail)); return; }
            if (!ServerOnline) { fail?.Invoke("ngoại tuyến"); return; }
            StartCoroutine(Post("/chu-thich", req, ok, fail));
        }

        public void Assist(AssistRequest req, Action<AssistReply> ok, Action<string> fail = null)
        {
            if (!ServerOnline) { fail?.Invoke("ngoại tuyến"); return; }
            StartCoroutine(Post("/tro-ly", req, ok, fail));
        }

        [Serializable] class LogEntry { public string loai, tieu_de, noi_dung, muc_do, nguon; }
        [Serializable] class LogReply { public int id; }

        /// <summary>Ghi nhật ký lên web quản lý (hiện tượng, sự cố, kết quả phân tích). Máy chủ không chạy thì bỏ qua.</summary>
        public void Log(string loai, string tieuDe, string noiDung = "", string mucDo = "Info")
        {
            if (!Reachable) return;
            StartCoroutine(Post<LogEntry, LogReply>("/api/nhat-ky", new LogEntry { loai = loai, tieu_de = tieuDe, noi_dung = noiDung, muc_do = mucDo, nguon = "phong_lab_ao" }, null, null));
        }

        // ------------------------------------------------------------------
        // Gọi thẳng nhà cung cấp bằng khóa riêng (cùng quy tắc với máy chủ)
        // ------------------------------------------------------------------

        IEnumerator PlanDirect(PlanRequest req, Action<PlanReply> ok, Action<string> fail)
        {
            // Luật giáo viên luôn thắng khóa riêng của học sinh
            if (!ClassRules.AiEnabled || ClassRules.Exam)
            {
                ok?.Invoke(new PlanReply { tra_loi = RobotPlanLocal.AiOffByTeacher, ke_hoach = new RobotStep[0], bo_qua = new string[0], nguon = "khoa" });
                yield break;
            }
            var key = ApiKeyStore.Current;
            string user = RobotPlanLocal.PlanUserMessage(req.cau_hoi ?? "", req.the_gioi, req.lich_su);
            JObject data = null;
            string err = null;
            yield return DirectAi.Structured(key, RobotPlanLocal.SystemPlan, user, RobotPlanLocal.PlanSchema(), DirectAi.PlanMaxTokens, d => data = d, x => err = x);
            if (err != null) { LastDirectError = err; fail?.Invoke(err); yield break; }
            LastDirectError = "";
            var ids = new HashSet<string>();
            if (req.the_gioi?.vat != null) foreach (var v in req.the_gioi.vat) if (v?.id != null) ids.Add(v.id);
            var dropped = new List<string>();
            var steps = RobotPlanLocal.Validate(data["ke_hoach"] as JArray ?? new JArray(), ids, RobotPlanLocal.FindChemical, dropped);
            steps = RobotPlanLocal.CutLocked(steps, ClassRules.IsLocked, dropped, out string lockNote);
            string answer = data["tra_loi"]?.ToString() ?? "";
            if (lockNote.Length > 0) answer = (answer + " " + lockNote).Trim();
            string summary = string.Join(" → ", steps.ConvertAll(s => s.ky_nang + (string.IsNullOrEmpty(s.doi_tuong) ? "" : $"({s.doi_tuong})")));
            // Nhật ký cho giáo viên (chỉ câu hỏi và kế hoạch, không có khóa)
            Log("robot", "Lệnh robot (khóa riêng): " + Cut(req.cau_hoi, 200), (summary.Length > 0 ? summary : "chỉ trả lời") + (dropped.Count > 0 ? "\nBỏ: " + string.Join("; ", dropped) : ""),
                dropped.Count > 0 ? "Warning" : "Info");
            ok?.Invoke(new PlanReply { tra_loi = answer, ke_hoach = steps.ToArray(), bo_qua = dropped.ToArray(), nguon = "khoa_rieng_" + key.provider });
        }

        readonly Dictionary<string, string> annotateCache = new Dictionary<string, string>();

        IEnumerator AnnotateDirect(AnnotateRequest req, Action<AnnotateReply> ok, Action<string> fail)
        {
            if (!ClassRules.AiEnabled || ClassRules.Exam) { ok?.Invoke(new AnnotateReply { giai_thich = "", nguon = "khoa" }); yield break; }
            var parts = new List<string>(req.thanh_phan ?? new string[0]);
            parts.Sort(StringComparer.Ordinal);
            string cacheKey = req.tieu_de + "|" + req.phuong_trinh + "|" + string.Join(",", parts);
            if (annotateCache.TryGetValue(cacheKey, out var hit)) { ok?.Invoke(new AnnotateReply { giai_thich = hit, nguon = "cache" }); yield break; }
            string text = null, err = null;
            yield return DirectAi.Text(ApiKeyStore.Current, RobotPlanLocal.SystemAnnotate, RobotPlanLocal.AnnotateUserMessage(req), DirectAi.TextMaxTokens, t => text = t, x => err = x);
            if (err != null) { LastDirectError = err; fail?.Invoke(err); yield break; }
            LastDirectError = "";
            text = System.Text.RegularExpressions.Regex.Replace(text ?? "", @"\s+", " ").Trim();
            annotateCache[cacheKey] = text;
            ok?.Invoke(new AnnotateReply { giai_thich = text, nguon = "khoa_rieng" });
        }

        static string Cut(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length > n ? s.Substring(0, n) : s;

        /// <summary>Máy chủ đang chạy (kể cả khi chưa cấu hình AI).</summary>
        public bool Reachable { get; private set; }

        IEnumerator Post<TReq, TRes>(string path, TReq body, Action<TRes> ok, Action<string> fail, float timeout = 0f)
        {
            var json = JsonUtility.ToJson(body);
            using (var r = new UnityWebRequest(GameSession.ServerUrl + path, "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                r.downloadHandler = new DownloadHandlerBuffer();
                r.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                r.timeout = Mathf.CeilToInt(timeout > 0 ? timeout : timeoutS);
                AccountClient.Authorize(r);
                yield return r.SendWebRequest();
                AccountClient.CheckExpired(r);
                if (r.result != UnityWebRequest.Result.Success) { fail?.Invoke(r.error); yield break; }
                TRes res;
                try { res = JsonUtility.FromJson<TRes>(r.downloadHandler.text); }
                catch (Exception e) { fail?.Invoke(e.Message); yield break; }
                ok?.Invoke(res);
            }
        }

        IEnumerator Get<TRes>(string path, Action<TRes> ok, Action<string> fail, float timeout)
        {
            using (var r = UnityWebRequest.Get(GameSession.ServerUrl + path))
            {
                r.timeout = Mathf.CeilToInt(timeout);
                AccountClient.Authorize(r);
                yield return r.SendWebRequest();
                AccountClient.CheckExpired(r);
                if (r.result != UnityWebRequest.Result.Success) { fail?.Invoke(r.error); yield break; }
                TRes res = default;
                try { res = JsonUtility.FromJson<TRes>(r.downloadHandler.text); }
                catch (Exception e) { fail?.Invoke(e.Message); yield break; }
                ok?.Invoke(res);
            }
        }
    }
}
