using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Khkt.Menu;

namespace Khkt.Demo
{
    /// <summary>
    /// Gọi máy chủ AI của dự án (server/ai/main.py). Khóa API của các mô hình AI đám mây chỉ nằm trên máy chủ,
    /// ứng dụng chỉ gửi ngữ cảnh thí nghiệm (đã do bộ máy hóa học tính) và nhận lời giải thích / hành động.
    /// Máy chủ không chạy thì mọi thứ vẫn dùng được: chú thích soạn sẵn, robot hiểu lệnh cơ bản.
    /// </summary>
    public class AiClient : MonoBehaviour
    {
        [Tooltip("Địa chỉ máy chủ AI (uvicorn server.ai.main:app --port 8000)")]
        public float timeoutS = 25f;
        const float PlanTimeoutS = 90f;  // kế hoạch robot dài; máy chủ thử mô hình mạnh trước, lỗi thì chuyển mô hình khác (tối đa 80 s)

        public static AiClient Instance { get; private set; }
        public bool Online { get; private set; }
        public string Provider { get; private set; } = "";
        public string Model { get; private set; } = "";

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
            if (!Online) { fail?.Invoke("ngoại tuyến"); return; }
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
                    Online = h != null && h.ok;
                    Provider = h?.nha_cung_cap ?? "";
                    Model = h?.mo_hinh ?? "";
                    if (firstCheck && Online)
                    {
                        LabMessages.Say($"Đã kết nối máy chủ AI ({Provider})", LabMessages.Tone.Good);
                        firstCheck = false;
                    }
                }, _ =>
                {
                    Online = false;
                    Reachable = false;
                    if (firstCheck)
                    {
                        LabMessages.Say("Máy chủ AI ngoại tuyến (phòng lab vẫn chạy bình thường)", LabMessages.Tone.Info);
                        firstCheck = false;
                    }
                }, 3f);
                yield return new WaitForSecondsRealtime(Online ? 60f : 15f);
            }
        }

        public void Annotate(AnnotateRequest req, Action<AnnotateReply> ok, Action<string> fail = null)
        {
            if (!Online) { fail?.Invoke("ngoại tuyến"); return; }
            StartCoroutine(Post("/chu-thich", req, ok, fail));
        }

        public void Assist(AssistRequest req, Action<AssistReply> ok, Action<string> fail = null)
        {
            if (!Online) { fail?.Invoke("ngoại tuyến"); return; }
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
