// -----------------------------------------------------------------------------
// BioLabSync.cs — Đồng bộ bất đồng bộ hai chiều giữa Unity (Tầng 2 Khu Sinh học)
// và máy chủ Web (/lab/sinh/...) kèm hàng đợi ngoại tuyến hang_doi_sinh.jsonl
// (Mục 8.5 PROMPT_DUNG_CU_GIAO_DIEN.md & Mục 5 HOP_DONG_GIAO_DIEN_BACKEND.md).
// -----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Khkt.Interaction.Bio;

namespace Khkt.Demo.Bio
{
    [Serializable]
    public struct BioSyncEventItem
    {
        public string loai;
        public double thoi_gian;
        public string du_lieuJson;
    }

    [DisallowMultipleComponent]
    public class BioLabSync : MonoBehaviour
    {
        public static BioLabSync Instance { get; private set; }

        public const int MaxQueueSize = 500;
        public string serverUrl = "http://127.0.0.1:8000";
        public bool syncDisabledByEnv = false;
        public bool lastRequestSucceeded = false;
        public int totalEventsSent = 0;
        public int lastServerAcceptedCount = 0;

        private readonly List<BioSyncEventItem> eventQueue = new List<BioSyncEventItem>(64);
        private readonly StringBuilder jsonSb = new StringBuilder(4096);
        private bool requestInFlight = false;
        private float flushTimer = 0f;
        private float fridgeTempTimer = 0f;
        private float pollTimer = 0f;
        private float retryBackoffSec = 0f;
        private int consecutiveFailures = 0;

        public string QueueFilePath => Path.Combine(Application.persistentDataPath, "hang_doi_sinh.jsonl");
        public int PendingCount => eventQueue.Count;

        public static BioLabSync EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<BioLabSync>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var go = new GameObject("BioLabSync");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BioLabSync>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            string envUrl = System.Environment.GetEnvironmentVariable("KHKT_SERVER_URL");
            if (!string.IsNullOrEmpty(envUrl)) serverUrl = envUrl.TrimEnd('/');

            string envWebSinh = System.Environment.GetEnvironmentVariable("KHKT_WEB_SINH");
            syncDisabledByEnv = (envWebSinh == "0");

            LoadQueueFromDisk();

            BioFeedback.OnMistakeRecorded -= HandleMistakeRecorded;
            BioFeedback.OnMistakeRecorded += HandleMistakeRecorded;
            BioFeedback.OnActionLogged -= HandleActionLogged;
            BioFeedback.OnActionLogged += HandleActionLogged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SaveQueueToDisk();
                BioFeedback.OnMistakeRecorded -= HandleMistakeRecorded;
                BioFeedback.OnActionLogged -= HandleActionLogged;
                Instance = null;
            }
        }

        void OnApplicationQuit()
        {
            SaveQueueToDisk();
        }

        private void HandleMistakeRecorded(string equipId, string code, string desc, string reason)
        {
            string safeDesc = EscapeJson(desc + " (" + reason + ")");
            string safeTool = EscapeJson(equipId);
            EnqueueRaw("su_co",
                $"{{\"phong\":\"phong_sinh\",\"loai\":\"{EscapeJson(code)}\",\"muc_do\":\"canh_bao\",\"mo_ta\":\"[{safeTool}] {safeDesc}\"}}");

            if (code.Contains("vo_lam") || code.Contains("nam_ngang"))
            {
                EnqueueRaw("dung_cu_hong",
                    $"{{\"ma\":\"{safeTool}\",\"so_luong\":1,\"ly_do\":\"{EscapeJson(desc)}\",\"phong\":\"phong_sinh\"}}");
            }
        }

        private void HandleActionLogged(string toolId, string actionCode, string detail)
        {
            if (actionCode == "xuat_kho_sinh_pham")
            {
                EnqueueRaw("sinh_pham_xuat",
                    $"{{\"id\":\"sp_xanh_methylen_1\",\"so_luong\":1,\"nguoi\":\"Học sinh\"}}");
            }
        }

        public void EnqueueRaw(string loai, string duLieuJsonObject)
        {
            if (eventQueue.Count >= MaxQueueSize)
            {
                eventQueue.RemoveAt(0);
            }
            double nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            eventQueue.Add(new BioSyncEventItem
            {
                loai = loai,
                thoi_gian = nowUnix,
                du_lieuJson = string.IsNullOrEmpty(duLieuJsonObject) ? "{}" : duLieuJsonObject
            });

            if (syncDisabledByEnv)
            {
                SaveQueueToDisk();
            }
        }

        public void RecordExperimentResult(string maBai, int lop, string ban, string ketQuaTomTat)
        {
            string safeBai = EscapeJson(maBai);
            string safeBan = EscapeJson(ban);
            string safeKq = EscapeJson(ketQuaTomTat);
            EnqueueRaw("ket_qua_tn",
                $"{{\"hoc_sinh\":\"Học sinh\",\"lop\":\"{lop}A1\",\"ban\":\"{safeBan}\",\"ma_bai\":\"{safeBai}\",\"du_lieu\":{{\"tom_tat\":\"{safeKq}\",\"hoan_thanh\":true}}}}");
        }

        public void RecordFridgeTemperature(string tuId, float tempC, bool doorOpen)
        {
            string tempStr = tempC.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            EnqueueRaw("nhiet_do_tu",
                $"{{\"tu\":\"{EscapeJson(tuId)}\",\"nhiet_do\":{tempStr},\"cua_mo\":{(doorOpen ? "true" : "false")}}}");
        }

        public void RecordMachineStatus(string maMay, string trangThai, string moTa, float tienDo01)
        {
            string td = Mathf.Clamp01(tienDo01).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            EnqueueRaw("trang_thai_may",
                $"{{\"ma\":\"{EscapeJson(maMay)}\",\"trang_thai\":\"{EscapeJson(trangThai)}\",\"mo_ta\":\"{EscapeJson(moTa)}\",\"tien_do\":{td}}}");
        }

        public void SaveQueueToDisk()
        {
            try
            {
                string path = QueueFilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (eventQueue.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }

                using (var sw = new StreamWriter(path, false, Encoding.UTF8))
                {
                    for (int i = 0; i < eventQueue.Count; i++)
                    {
                        var ev = eventQueue[i];
                        sw.Write("{\"loai\":\"");
                        sw.Write(EscapeJson(ev.loai));
                        sw.Write("\",\"thoi_gian\":");
                        sw.Write(ev.thoi_gian.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                        sw.Write(",\"du_lieu\":");
                        sw.Write(ev.du_lieuJson);
                        sw.WriteLine("}");
                    }
                }
            }
            catch (Exception)
            {
                // Không chặn luồng chính nếu đĩa chỉ đọc
            }
        }

        public void LoadQueueFromDisk()
        {
            try
            {
                string path = QueueFilePath;
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length && eventQueue.Count < MaxQueueSize; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    // Phân tích nhẹ dòng JSONL đã lưu
                    int loaiIdx = line.IndexOf("\"loai\":\"", StringComparison.Ordinal);
                    int dlIdx = line.IndexOf("\"du_lieu\":", StringComparison.Ordinal);
                    if (loaiIdx >= 0 && dlIdx > loaiIdx)
                    {
                        int startL = loaiIdx + 8;
                        int endL = line.IndexOf('"', startL);
                        string loai = endL > startL ? line.Substring(startL, endL - startL) : "su_co";
                        string dl = line.Substring(dlIdx + 10);
                        if (dl.EndsWith("}")) dl = dl.Substring(0, dl.Length - 1);
                        eventQueue.Add(new BioSyncEventItem
                        {
                            loai = loai,
                            thoi_gian = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            du_lieuJson = dl
                        });
                    }
                }
            }
            catch (Exception) { }
        }

        void Update()
        {
            if (syncDisabledByEnv) return;

            if (retryBackoffSec > 0f)
            {
                retryBackoffSec -= Time.unscaledDeltaTime;
                return;
            }

            flushTimer += Time.unscaledDeltaTime;
            fridgeTempTimer += Time.unscaledDeltaTime;
            pollTimer += Time.unscaledDeltaTime;

            if (fridgeTempTimer >= 30f)
            {
                fridgeTempTimer = 0f;
                RecordFridgeTemperature("tu_mat_4", 4.0f, false);
                RecordFridgeTemperature("tu_dong_am20", -20.0f, false);
            }

            if (!requestInFlight && (eventQueue.Count >= 20 || (flushTimer >= 5f && eventQueue.Count > 0)))
            {
                flushTimer = 0f;
                StartCoroutine(FlushBatchCoroutine());
            }
            else if (!requestInFlight && pollTimer >= 60f)
            {
                pollTimer = 0f;
                StartCoroutine(PollOverviewCoroutine());
            }
        }

        public IEnumerator FlushBatchCoroutine()
        {
            if (syncDisabledByEnv || requestInFlight || eventQueue.Count == 0) yield break;
            requestInFlight = true;

            int batchCount = Mathf.Min(eventQueue.Count, 50);
            jsonSb.Clear();
            jsonSb.Append("{\"su_kien\":[");
            for (int i = 0; i < batchCount; i++)
            {
                if (i > 0) jsonSb.Append(',');
                var ev = eventQueue[i];
                jsonSb.Append("{\"loai\":\"").Append(EscapeJson(ev.loai)).Append("\",\"thoi_gian\":")
                      .Append(ev.thoi_gian.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
                      .Append(",\"du_lieu\":").Append(ev.du_lieuJson).Append('}');
            }
            jsonSb.Append("]}");

            string payload = jsonSb.ToString();
            string url = serverUrl.TrimEnd('/') + "/lab/sinh/su-kien";

            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(payload);
                req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 4;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300)
                {
                    eventQueue.RemoveRange(0, batchCount);
                    totalEventsSent += batchCount;
                    lastServerAcceptedCount = batchCount;
                    lastRequestSucceeded = true;
                    consecutiveFailures = 0;
                    retryBackoffSec = 0f;
                    SaveQueueToDisk();
                }
                else
                {
                    lastRequestSucceeded = false;
                    consecutiveFailures++;
                    // Lùi thời gian thử lại: 5 s, 10 s, 30 s, tối đa 60 s (Mục 8.5)
                    retryBackoffSec = consecutiveFailures == 1 ? 5f :
                                      (consecutiveFailures == 2 ? 10f :
                                      (consecutiveFailures == 3 ? 30f : 60f));
                    SaveQueueToDisk();
                }
            }

            requestInFlight = false;
        }

        public IEnumerator PollOverviewCoroutine()
        {
            if (syncDisabledByEnv || requestInFlight) yield break;
            requestInFlight = true;

            string url = serverUrl.TrimEnd('/') + "/lab/sinh/tong-quan";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 4;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    lastRequestSucceeded = true;
                    consecutiveFailures = 0;
                }
                else
                {
                    lastRequestSucceeded = false;
                    consecutiveFailures++;
                    retryBackoffSec = consecutiveFailures == 1 ? 5f : (consecutiveFailures == 2 ? 10f : 30f);
                }
            }

            requestInFlight = false;
        }

        public static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}

