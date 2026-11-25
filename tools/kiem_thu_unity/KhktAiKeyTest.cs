using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Khkt.Demo;
using Khkt.Demo.Ai;
using Khkt.Menu;
using Khkt.Menu.Dashboard;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

// Chỉ có trong bản sao kiểm thử (chế độ KHKT_TEST=khoa_ai): khóa API AI riêng của từng tài khoản.
// Khóa giả, nhà cung cấp giả chạy trên 127.0.0.1: không gọi mạng thật, không tốn tiền.
public static class KhktAiKeyTest
{
    // Khóa giả, đủ dài và lạ để dò rò rỉ trong log / tệp
    const string KeyA = "sk-ant-api03-KHOAGIA-AAAA-7f3c9d1e5b2a4c6e8f0a1b2c3d4e5f60";
    const string KeyB = "sk-proj-KHOAGIA-BBBB-0e1d2c3b4a5f6e7d8c9b0a1f2e3d4c5b6a7";
    const string KeyG = "AIzaKHOAGIA-GGGG-1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c";
    const string KeyQ = "gsk_KHOAGIAQQQQ9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d3e2f1";
    const string WrongKey = "sk-ant-api03-SAIKHOA-ZZZZ-00000000000000000000000000";

    static int pass, fail;
    static readonly List<string> logs = new List<string>();

    static void Log(string s) => Debug.Log("[KHKT-PLAY] [khoa_ai] " + s);

    static void Ok(bool cond, string what, string detail = "")
    {
        if (cond) pass++; else fail++;
        Log((cond ? "ĐẠT  " : "LỖI  ") + what + (detail.Length > 0 ? " · " + detail : ""));
    }

    // ------------------------------------------------------------------
    // Nhà cung cấp giả
    // ------------------------------------------------------------------

    sealed class Req { public string method, path, rawUrl, body; public Dictionary<string, string> headers = new Dictionary<string, string>(); }

    sealed class FakeProvider
    {
        HttpListener listener;
        Thread thread;
        public int Port;
        public readonly ConcurrentQueue<Req> Requests = new ConcurrentQueue<Req>();
        public volatile int GeminiSchemaFailures = 1; // lần đầu Gemini từ chối reasoning_effort (400) để thử dạng đơn giản hơn
        public volatile string PlanJson = "";

        public void Start()
        {
            for (int p = 18731; p < 18760; p++)
            {
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{p}/");
                    listener.Start();
                    Port = p;
                    break;
                }
                catch { listener = null; }
            }
            thread = new Thread(Loop) { IsBackground = true };
            thread.Start();
        }

        public void Stop()
        {
            try { listener?.Stop(); listener?.Close(); } catch { }
        }

        void Loop()
        {
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = listener.GetContext(); }
                catch { return; }
                try { Handle(ctx); }
                catch (Exception e) { try { Reply(ctx, 500, "{\"error\":{\"message\":\"" + e.GetType().Name + "\"}}"); } catch { } }
            }
        }

        static void Reply(HttpListenerContext ctx, int code, string json)
        {
            var b = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = b.Length;
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.Close();
        }

        void Handle(HttpListenerContext ctx)
        {
            var r = new Req { method = ctx.Request.HttpMethod, path = ctx.Request.Url.AbsolutePath, rawUrl = ctx.Request.RawUrl };
            foreach (string k in ctx.Request.Headers.AllKeys) r.headers[k.ToLowerInvariant()] = ctx.Request.Headers[k];
            using (var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) r.body = sr.ReadToEnd();
            Requests.Enqueue(r);

            string prov = r.path.Split('/')[1];
            string key = prov == "claude" ? (r.headers.TryGetValue("x-api-key", out var k1) ? k1 : "")
                       : r.headers.TryGetValue("authorization", out var k2) && k2.StartsWith("Bearer ") ? k2.Substring(7) : "";
            string want = prov == "claude" ? KeyA : prov == "openai" ? KeyB : prov == "groq" ? KeyQ : KeyG;
            if (key != want)
            {
                // Nhà cung cấp thật hay nhắc lại một phần khóa trong lỗi: app phải che đi
                Reply(ctx, 401, "{\"error\":{\"message\":\"Incorrect API key provided: " + key + "\"}}");
                return;
            }
            if (r.path.EndsWith("/models"))
            {
                string ids = prov == "claude" ? "\"claude-opus-5-5\",\"claude-sonnet-5-5\",\"claude-haiku-5-5\""
                           : prov == "openai" ? "\"gpt-5\",\"gpt-5-mini\",\"gpt-5-pro\",\"text-embedding-3-large\",\"gpt-4o-realtime-preview\",\"dall-e-3\""
                           : prov == "groq" ? "\"openai/gpt-oss-120b\",\"llama-3.3-70b-versatile\",\"whisper-large-v3\",\"playai-tts\""
                           : "\"models/gemini-flash-latest\",\"models/gemini-pro-latest\",\"models/text-embedding-004\"";
                Reply(ctx, 200, "{\"data\":[" + string.Join(",", ids.Split(',').Select(i => "{\"id\":" + i + "}")) + "]}");
                return;
            }
            var body = JObject.Parse(r.body);
            bool annotate = body.ToString().Contains("DỮ KIỆN:");
            string text = annotate ? "Ion Cu2+ kết hợp OH- tạo Cu(OH)2 kết tủa xanh lam.   Tránh để NaOH dính da." : PlanJson;
            if (prov == "claude")
            {
                Reply(ctx, 200, new JObject { ["type"] = "message", ["stop_reason"] = "end_turn",
                    ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = text }) }.ToString());
                return;
            }
            if (prov == "groq" && body["response_format"]?["type"]?.ToString() == "json_schema")
            {
                Reply(ctx, 400, "{\"error\":{\"message\":\"This model does not support response format `json_schema`\"}}");
                return;
            }
            if (prov == "gemini" && body["reasoning_effort"] != null && GeminiSchemaFailures > 0)
            {
                GeminiSchemaFailures--;
                Reply(ctx, 400, "{\"error\":{\"message\":\"Unknown name reasoning_effort\"}}");
                return;
            }
            Reply(ctx, 200, new JObject { ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop",
                ["message"] = new JObject { ["role"] = "assistant", ["content"] = text } }) }.ToString());
        }

        public List<Req> Drain()
        {
            var l = new List<Req>();
            while (Requests.TryDequeue(out var r)) l.Add(r);
            return l;
        }
    }

    // ------------------------------------------------------------------

    public static IEnumerator Run(Camera cam, string outDir)
    {
        Application.logMessageReceivedThreaded += (c, st, t) => { lock (logs) logs.Add(c + "\n" + st); };
        string oldRoot = ApiKeyStore.Root;
        string dir = Path.Combine(Application.temporaryCachePath, "khoa_ai_thu_" + DateTime.Now.Ticks);
        ApiKeyStore.Root = dir;
        var oldUser = GameSession.User;

        // ---------- 1. Kho khóa ----------
        string server = GameSession.ServerUrl;
        string ownerA = ApiKeyStore.OwnerFor(server, 9001), ownerB = ApiKeyStore.OwnerFor(server, 9002);
        ApiKeyStore.Save(ownerA, new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, model = "", key = KeyA });
        var files = Directory.GetFiles(dir, "*.bin");
        Ok(files.Length == 1, "lưu khóa tạo đúng một tệp .bin trong thư mục riêng", files.Length + " tệp");
        byte[] raw = files.Length > 0 ? File.ReadAllBytes(files[0]) : new byte[0];
        bool plainUtf8 = Encoding.UTF8.GetString(raw).Contains("KHOAGIA"), plainUtf16 = Encoding.Unicode.GetString(raw).Contains("KHOAGIA");
        Ok(raw.Length > 0 && !plainUtf8 && !plainUtf16, "tệp khóa không chứa khóa ở dạng chữ thường", raw.Length + " byte");
        Ok(raw.Length > 0 && raw[0] == (ApiKeyStore.UsesDpapi ? 1 : 2), ApiKeyStore.UsesDpapi ? "mã hóa bằng DPAPI của Windows" : "mã hóa AES (máy không phải Windows)", "nhãn " + (raw.Length > 0 ? raw[0] : -1));
        ApiKeyStore.Forget();
        var back = ApiKeyStore.Load(ownerA);
        Ok(back != null && back.key == KeyA && back.provider == ApiKeyStore.Claude, "đọc lại đúng khóa sau khi quên bộ nhớ");
        Ok(ApiKeyStore.Load(ownerB) == null, "tài khoản khác không thấy khóa của người kia");
        ApiKeyStore.Save(ownerB, new ApiKeyStore.Entry { provider = ApiKeyStore.OpenAi, key = KeyB });
        ApiKeyStore.Forget();
        Ok(ApiKeyStore.Load(ownerA)?.key == KeyA && ApiKeyStore.Load(ownerB)?.key == KeyB, "hai tài khoản giữ hai khóa riêng");
        Ok(ApiKeyStore.Load(ApiKeyStore.OwnerFor("http://127.0.0.1:8000", 9001)) == null, "cùng mã tài khoản nhưng khác máy chủ: không dùng chung khóa");
        // Chép tệp của A sang tên của B: entropy khác nên không giải được
        string pathA = files[0];
        string pathB = Directory.GetFiles(dir, "*.bin").First(f => f != pathA);
        File.Copy(pathA, pathB, true);
        ApiKeyStore.Forget();
        Ok(ApiKeyStore.Load(ownerB) == null, "tệp khóa chép sang tài khoản khác không giải mã được");
        ApiKeyStore.Delete(ownerB);
        ApiKeyStore.Delete(ownerA);
        ApiKeyStore.Forget();
        Ok(Directory.GetFiles(dir, "*.bin").Length == 0 && ApiKeyStore.Load(ownerA) == null, "xóa khóa: tệp mất, không còn khóa");
        string m = ApiKeyStore.Mask(KeyA);
        Ok(!m.Contains("KHOAGIA") && m.EndsWith(KeyA.Substring(KeyA.Length - 4)), "che khóa khi hiện", m);
        GameSession.User = null;
        Ok(ApiKeyStore.Owner == null && !ApiKeyStore.HasKey, "khách không có khóa riêng");

        // ---------- 2. Tra hóa chất, kiểm tra kế hoạch ----------
        var tra = new (string q, string want)[] { ("axit sunfuric", "sunfuric"), ("H2SO4", "sunfuric"), ("kali pemanganat", "pemanganat"),
                                                  ("NaOH", "natri hiđroxit"), ("sodium hydroxide", "natri hiđroxit"), ("axit oxalic", "oxalic"), ("CuSO4", "đồng") };
        foreach (var t in tra)
        {
            var c = RobotPlanLocal.FindChemical(t.q);
            Ok(c != null && c.ten.ToLowerInvariant().Contains(t.want), $"tra hóa chất '{t.q}'", c != null ? $"{c.stt} {c.ten} ({c.trang_thai})" : "không thấy");
        }
        // giống formulaKey của máy chủ: C trước, rồi H, rồi theo chữ cái (kể cả khi không có C)
        Ok(RobotPlanLocal.FormulaKey("Cu(OH)2") == "H2CuO2" && RobotPlanLocal.FormulaKey("C6H12O6") == "C6H12O6" && RobotPlanLocal.FormulaKey("axit") == "",
           "khóa công thức trùng máy chủ", RobotPlanLocal.FormulaKey("Cu(OH)2"));
        Ok(RobotPlanLocal.FindTemplate("nhận biết Cu2+") == "nhan_biet_cu2" && RobotPlanLocal.FindTemplate("chuan_do") == "chuan_do", "tìm thí nghiệm mẫu theo tên");

        var oxalic = RobotPlanLocal.FindChemical("axit oxalic");
        var plan = JArray.Parse(@"[
            {""ky_nang"":""lay_hoa_chat"",""doi_tuong"":"""",""dich"":"""",""so_luong"":0,""hoa_chat"":""axit oxalic"",""loi"":""""},
            {""ky_nang"":""rot"",""doi_tuong"":""hoa_chat:axit oxalic"",""dich"":""ong_nghiem_1"",""so_luong"":500,""hoa_chat"":"""",""loi"":""""},
            {""ky_nang"":""bay_len"",""doi_tuong"":"""",""dich"":"""",""so_luong"":0,""hoa_chat"":"""",""loi"":""""},
            {""ky_nang"":""di_toi"",""doi_tuong"":""vat_khong_co"",""dich"":"""",""so_luong"":0,""hoa_chat"":"""",""loi"":""""},
            {""ky_nang"":""lac"",""doi_tuong"":""ong_nghiem_1"",""dich"":"""",""so_luong"":0,""hoa_chat"":"""",""loi"":""""},
            {""ky_nang"":""dat"",""doi_tuong"":""ong_nghiem_1"",""dich"":""cho_la"",""so_luong"":0,""hoa_chat"":"""",""loi"":""""},
            {""ky_nang"":""thi_nghiem_mau"",""doi_tuong"":"""",""dich"":"""",""so_luong"":0,""hoa_chat"":""nhận biết Fe3+"",""loi"":""""},
            {""ky_nang"":""nho_giot"",""doi_tuong"":""hoa_chat:chat khong ton tai xyz"",""dich"":""ong_nghiem_1"",""so_luong"":3,""hoa_chat"":"""",""loi"":""""}
        ]");
        var dropped = new List<string>();
        var ids = new HashSet<string> { "ong_nghiem_1", "coc_1" };
        var steps = RobotPlanLocal.Validate(plan, ids, RobotPlanLocal.FindChemical, dropped);
        Log("kế hoạch sau kiểm tra: " + string.Join(" → ", steps.Select(s => $"{s.ky_nang}({s.doi_tuong}>{s.dich} {s.so_luong})")) + " · bỏ: " + string.Join("; ", dropped));
        Ok(steps.Count == 5 && dropped.Count == 3, "bỏ kỹ năng lạ, vật không có trong cảnh, hóa chất không có", $"{steps.Count} bước, bỏ {dropped.Count}");
        Ok(oxalic != null && steps.Count > 0 && steps[0].stt == oxalic.stt && steps[0].doi_tuong == $"hoa_chat:{oxalic.stt}", "lay_hoa_chat điền đúng stt");
        Ok(steps.Count > 1 && steps[1].doi_tuong == $"hoa_chat:{oxalic?.stt}" && Mathf.Approximately(steps[1].so_luong, 100), "rot gọi lọ vừa lấy theo tên, giới hạn 100 ml", steps.Count > 1 ? steps[1].doi_tuong + " " + steps[1].so_luong : "");
        Ok(steps.Count > 2 && Mathf.Approximately(steps[2].so_luong, 4), "lac không ghi thời gian: mặc định 4 s");
        Ok(steps.Count > 3 && steps[3].dich == "ban_nguoi_dung", "dat vào chỗ lạ: đổi thành bàn người dùng");
        Ok(steps.Count > 4 && steps[4].doi_tuong == "nhan_biet_fe3", "thi_nghiem_mau theo tên tiếng Việt");
        var dropped2 = new List<string>();
        var cut = RobotPlanLocal.CutLocked(steps, s => oxalic != null && s == oxalic.stt, dropped2, out string note);
        Ok(cut.Count == 0 && note.Contains("khóa"), "hóa chất giáo viên khóa: dừng kế hoạch từ bước đầu tiên dùng nó", note);
        var big = new JArray(Enumerable.Range(0, 25).Select(i => new JObject { ["ky_nang"] = "doi", ["so_luong"] = 5 }));
        var d3 = new List<string>();
        Ok(RobotPlanLocal.Validate(big, ids, RobotPlanLocal.FindChemical, d3).Count == RobotPlanLocal.MaxSteps && d3.Count == 1, "tối đa 20 bước");
        Ok(RobotPlanLocal.MachineCount > 10 && RobotPlanLocal.MachineContext("hướng dẫn mình dùng máy AAS đo nước").Contains("MÁY:"), "nạp quy trình máy từ StreamingAssets", RobotPlanLocal.MachineCount + " máy");
        Ok(RobotPlanLocal.MachineContext("cách dùng kính hiển vi").Length > 0, "ngữ cảnh dụng cụ / máy lab sinh");
        Ok(RobotPlanLocal.IsUnsafeBio("làm sao nuôi cấy vi khuẩn lao") && RobotPlanLocal.MachineContext("nuôi cấy vi khuẩn lao").Contains("BSL-1") && !RobotPlanLocal.IsUnsafeBio("nhuộm gram sữa chua"),
           "chặn yêu cầu sinh học nguy hiểm (chỉ BSL-1)");

        // ---------- 3. Danh sách kỹ năng trùng với máy chủ ----------
        string repo = Environment.GetEnvironmentVariable("KHKT_REPO");
        if (string.IsNullOrEmpty(repo)) repo = @"D:\KHKT 11 du phong";
        string ts = Path.Combine(repo, "cloudflare", "src", "robot_plan.ts");
        if (File.Exists(ts))
        {
            string src = File.ReadAllText(ts);
            List<string> Keys(string start)
            {
                int a = src.IndexOf(start, StringComparison.Ordinal);
                int b = src.IndexOf("\n};", a, StringComparison.Ordinal);
                return Regex.Matches(src.Substring(a, b - a), @"^\s{2}(\w+):\s", RegexOptions.Multiline).Cast<Match>().Select(x => x.Groups[1].Value).ToList();
            }
            var sk = Keys("export const SKILLS");
            var tp = Keys("export const TEMPLATES");
            Ok(sk.SequenceEqual(RobotPlanLocal.Skills.Select(s => s.key)), "kỹ năng robot trùng cloudflare/src/robot_plan.ts", $"{sk.Count} / {RobotPlanLocal.Skills.Length}");
            Ok(tp.SequenceEqual(RobotPlanLocal.Templates.Select(s => s.key)), "thí nghiệm mẫu trùng robot_plan.ts", $"{tp.Count} / {RobotPlanLocal.Templates.Length}");
            var lim = Regex.Match(src, @"const LIMITS[^{]*\{([^}]*)\}").Groups[1].Value;
            var lims = Regex.Matches(lim, @"(\w+):\s*\[(\d+),\s*(\d+)\]").Cast<Match>().ToDictionary(x => x.Groups[1].Value, x => (float.Parse(x.Groups[2].Value), float.Parse(x.Groups[3].Value)));
            Ok(lims.Count == RobotPlanLocal.Limits.Count && lims.All(kv => RobotPlanLocal.Limits.TryGetValue(kv.Key, out var v) && v.md == kv.Value.Item1 && v.lim == kv.Value.Item2), "giới hạn số lượng trùng robot_plan.ts");
            int sa = src.IndexOf("export const SKILLS", StringComparison.Ordinal), sb = src.IndexOf("\n};", sa, StringComparison.Ordinal);
            var skillText = Regex.Matches(src.Substring(sa, sb - sa), @"^\s{2}(\w+):\s""([^""]*)"",?\s*$", RegexOptions.Multiline).Cast<Match>()
                                 .GroupBy(x => x.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
            var diff = RobotPlanLocal.Skills.Where(s => skillText.TryGetValue(s.key, out var t) && t != s.text).Select(s => s.key).ToList();
            Ok(diff.Count == 0, "mô tả kỹ năng (một dòng) trùng robot_plan.ts", string.Join(",", diff));
        }
        else Log("bỏ qua so với robot_plan.ts: không thấy " + ts);

        // ---------- 4. Gọi thẳng qua nhà cung cấp giả ----------
        var fake = new FakeProvider();
        fake.Start();
        Ok(fake.Port > 0, "khởi động nhà cung cấp giả trên 127.0.0.1", fake.Port.ToString());
        string b0 = $"http://127.0.0.1:{fake.Port}";
        string oc = DirectAi.ClaudeBase, oo = DirectAi.OpenAiBase, og = DirectAi.GeminiBase;
        DirectAi.ClaudeBase = b0 + "/claude/v1";
        DirectAi.OpenAiBase = b0 + "/openai/v1";
        DirectAi.GeminiBase = b0 + "/gemini/v1beta/openai";
        string oq = DirectAi.GroqBase;
        DirectAi.GroqBase = b0 + "/groq/openai/v1";

        string okMsg = null, errMsg = null;
        yield return DirectAi.Check(new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, key = KeyA }, x => okMsg = x, x => errMsg = x);
        Ok(okMsg == "claude-opus-5-5", "kiểm tra khóa Claude: dùng được, mô hình mặc định", okMsg ?? errMsg);
        var rq = fake.Drain();
        Ok(rq.Count == 1 && rq[0].method == "GET" && rq[0].path.EndsWith("/models") && rq[0].headers.TryGetValue("x-api-key", out var hk) && hk == KeyA && !rq[0].rawUrl.Contains("KHOAGIA"),
           "kiểm tra khóa chỉ gọi danh sách mô hình, khóa trong header không trong URL", rq.Count > 0 ? rq[0].rawUrl : "");
        okMsg = errMsg = null;
        yield return DirectAi.Check(new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, key = WrongKey }, x => okMsg = x, x => errMsg = x);
        Ok(okMsg == null && errMsg != null && errMsg.Contains("401") && !errMsg.Contains("SAIKHOA-ZZZZ-0000"), "khóa sai: báo 401 và che khóa trong lỗi", errMsg);
        okMsg = errMsg = null;
        yield return DirectAi.Check(new ApiKeyStore.Entry { provider = ApiKeyStore.OpenAi, key = KeyB }, x => okMsg = x, x => errMsg = x);
        Ok(okMsg == "gpt-5", "OpenAI không ghi mô hình: tự chọn mô hình trò chuyện mạnh nhất (bỏ pro, realtime, embed)", okMsg ?? errMsg);
        okMsg = errMsg = null;
        yield return DirectAi.Check(new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, model = "claude-khong-co", key = KeyA }, x => okMsg = x, x => errMsg = x);
        Ok(okMsg == null && errMsg != null && errMsg.Contains("claude-khong-co"), "mô hình không có: báo rõ", errMsg);
        fake.Drain();

        // Robot lập kế hoạch bằng khóa riêng (đi qua AiClient như khung chat)
        var ai = AiClient.Instance;
        Ok(ai != null, "có AiClient trong cảnh lab");
        GameSession.User = new AccountUser { id = 9001, ten_dang_nhap = "hs_thu", ho_ten = "Học sinh thử", vai_tro = "hoc_sinh" };
        ApiKeyStore.Forget();
        Ok(ai != null && !ai.Direct, "chưa có khóa: đi qua máy chủ như cũ");
        ApiKeyStore.Save(ApiKeyStore.Owner, new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, key = KeyA });
        Ok(ai != null && ai.Direct && ai.Online && ai.Provider.Contains("Claude"), "có khóa: AI dùng được dù máy chủ không chạy", ai?.Provider + " " + ai?.Model);
        var scene = new SceneSnapshot
        {
            khu_vuc = "bàn 1", che_do = "tự do", su_kien = new[] { "vừa rót NaOH" },
            vat = new[] { new SceneObject { id = "ong_nghiem_1", ten = "Ống nghiệm 1", loai = "ống nghiệm", khoang_cach = 0.4f, chua = "trống" },
                          new SceneObject { id = "coc_1", ten = "Cốc 1", loai = "cốc", khoang_cach = 0.7f, chua = "" } },
        };
        fake.PlanJson = new JObject
        {
            ["tra_loi"] = "Mình lấy CuSO4 rồi nhỏ NaOH cho bạn xem kết tủa xanh.",
            ["ke_hoach"] = new JArray(
                new JObject { ["ky_nang"] = "lay_hoa_chat", ["doi_tuong"] = "", ["dich"] = "", ["so_luong"] = 0, ["hoa_chat"] = "CuSO4", ["loi"] = "" },
                new JObject { ["ky_nang"] = "rot", ["doi_tuong"] = "hoa_chat:CuSO4", ["dich"] = "ong_nghiem_1", ["so_luong"] = 2, ["hoa_chat"] = "", ["loi"] = "" },
                new JObject { ["ky_nang"] = "di_toi", ["doi_tuong"] = "ban_khong_co", ["dich"] = "", ["so_luong"] = 0, ["hoa_chat"] = "", ["loi"] = "" },
                new JObject { ["ky_nang"] = "quan_sat", ["doi_tuong"] = "ong_nghiem_1", ["dich"] = "", ["so_luong"] = 0, ["hoa_chat"] = "", ["loi"] = "" }),
        }.ToString(Newtonsoft.Json.Formatting.None);
        AiClient.PlanReply reply = null;
        string planErr = null;
        bool done = false;
        ai.Plan(new AiClient.PlanRequest { cau_hoi = "cho mình xem kết tủa Cu(OH)2", the_gioi = scene, lich_su = new[] { "Bạn: xin chào" } },
                r => { reply = r; done = true; }, e => { planErr = e; done = true; });
        float t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 20) yield return null;
        var cuso4 = RobotPlanLocal.FindChemical("CuSO4");
        Ok(reply != null && reply.nguon == "khoa_rieng_claude", "kế hoạch robot đến từ khóa riêng", reply?.nguon ?? planErr);
        Ok(reply != null && reply.ke_hoach.Length == 3 && reply.ke_hoach[0].stt == cuso4?.stt && reply.ke_hoach[1].doi_tuong == $"hoa_chat:{cuso4?.stt}" && reply.bo_qua.Length == 1,
           "kế hoạch đã kiểm tra trong app (bỏ vật không có, điền stt)", reply != null ? string.Join(" → ", reply.ke_hoach.Select(s => s.ky_nang + "(" + s.doi_tuong + ")")) : "");
        rq = fake.Drain();
        var post = rq.FirstOrDefault(x => x.method == "POST");
        JObject pb = post != null ? JObject.Parse(post.body) : null;
        Ok(post != null && post.path.EndsWith("/messages") && post.headers.TryGetValue("anthropic-beta", out var beta) && beta.Contains("fallback") && !post.rawUrl.Contains("KHOAGIA"),
           "gọi Claude Messages API, khóa trong header, có fallbacks", post?.rawUrl);
        Ok(pb != null && pb["model"]?.ToString() == "claude-opus-5-5" && pb["output_config"]?["format"]?["type"]?.ToString() == "json_schema" && pb["fallbacks"]?.ToString() == "default"
           && pb["system"]?[0]?["text"]?.ToString() == RobotPlanLocal.SystemPlan && pb["messages"]?[0]?["content"]?.ToString().Contains("ẢNH CHỤP CẢNH") == true
           && pb["messages"][0]["content"].ToString().Contains("ong_nghiem_1"), "nội dung gửi: lời dặn hệ thống + khuôn JSON + ảnh chụp cảnh");
        Ok(pb != null && !pb.ToString().Contains("KHOAGIA"), "thân yêu cầu không chứa khóa");

        // Chú thích hiện tượng
        AiClient.AnnotateReply ann = null;
        done = false;
        ai.Annotate(new AiClient.AnnotateRequest { tieu_de = "Kết tủa xanh lam", phuong_trinh = "CuSO4 + 2NaOH → Cu(OH)2 + Na2SO4", thanh_phan = new[] { "Cu(OH)2" }, ph = 11, nhiet_do = 25 },
                    r => { ann = r; done = true; }, e => { done = true; });
        t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 20) yield return null;
        Ok(ann != null && ann.nguon == "khoa_rieng" && ann.giai_thich.StartsWith("Ion Cu2+") && !ann.giai_thich.Contains("  "), "chú thích hiện tượng gọi thẳng bằng khóa riêng", ann?.giai_thich);
        fake.Drain();

        // Luật giáo viên thắng khóa riêng
        ClassRules.Apply(false, false, true, 0, new int[0]);
        reply = null; done = false;
        ai.Plan(new AiClient.PlanRequest { cau_hoi = "lấy NaOH", the_gioi = scene }, r => { reply = r; done = true; }, e => done = true);
        t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 5) yield return null;
        Ok(reply != null && reply.nguon == "khoa" && reply.ke_hoach.Length == 0 && fake.Drain().Count == 0, "giáo viên tắt AI: không gọi nhà cung cấp dù có khóa riêng");
        ClassRules.Apply(false, true, true, 0, cuso4 != null ? new[] { cuso4.stt } : new int[0]);
        reply = null; done = false;
        ai.Plan(new AiClient.PlanRequest { cau_hoi = "cho mình xem kết tủa Cu(OH)2", the_gioi = scene }, r => { reply = r; done = true; }, e => done = true);
        t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 20) yield return null;
        Ok(reply != null && reply.ke_hoach.Length == 0 && reply.tra_loi.Contains("khóa"), "hóa chất giáo viên khóa: robot không lấy", reply?.tra_loi);
        ClassRules.Apply(false, true, true, 0, new int[0]);
        fake.Drain();

        // OpenAI và Gemini
        ApiKeyStore.Save(ApiKeyStore.Owner, new ApiKeyStore.Entry { provider = ApiKeyStore.OpenAi, key = KeyB });
        reply = null; done = false;
        ai.Plan(new AiClient.PlanRequest { cau_hoi = "cho mình xem kết tủa", the_gioi = scene }, r => { reply = r; done = true; }, e => { planErr = e; done = true; });
        t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 20) yield return null;
        rq = fake.Drain();
        var op = rq.LastOrDefault(x => x.method == "POST");
        var ob = op != null ? JObject.Parse(op.body) : null;
        Ok(reply != null && reply.ke_hoach.Length == 3 && reply.nguon == "khoa_rieng_openai", "OpenAI: kế hoạch qua chat/completions", reply?.nguon ?? planErr);
        Ok(ob != null && ob["model"]?.ToString() == "gpt-5" && ob["response_format"]?["type"]?.ToString() == "json_schema" && ob["max_completion_tokens"] != null
           && op.headers.TryGetValue("authorization", out var au) && au == "Bearer " + KeyB, "OpenAI: tự chọn gpt-5, khuôn JSON chặt, Bearer trong header");
        ApiKeyStore.Save(ApiKeyStore.Owner, new ApiKeyStore.Entry { provider = ApiKeyStore.Gemini, key = KeyG });
        reply = null; done = false;
        ai.Plan(new AiClient.PlanRequest { cau_hoi = "cho mình xem kết tủa", the_gioi = scene }, r => { reply = r; done = true; }, e => { planErr = e; done = true; });
        t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 20) yield return null;
        rq = fake.Drain();
        var posts = rq.Where(x => x.method == "POST").ToList();
        Ok(reply != null && reply.ke_hoach.Length == 3 && posts.Count == 2 && posts[0].path.Contains("/gemini/v1beta/openai/chat/completions")
           && JObject.Parse(posts[1].body)["reasoning_effort"] == null && JObject.Parse(posts[1].body)["model"]?.ToString() == "gemini-flash-latest",
           "Gemini: lỗi 400 thì thử lại dạng đơn giản hơn, mô hình mặc định", $"{posts.Count} lần gọi, {reply?.nguon ?? planErr}");

        okMsg = errMsg = null;
        yield return DirectAi.Check(new ApiKeyStore.Entry { provider = ApiKeyStore.Groq, key = KeyQ }, x => okMsg = x, x => errMsg = x);
        Ok(okMsg == "openai/gpt-oss-120b", "kiểm tra khóa Groq: mô hình mặc định gpt-oss-120b", okMsg ?? errMsg);
        fake.Drain();
        ApiKeyStore.Save(ApiKeyStore.Owner, new ApiKeyStore.Entry { provider = ApiKeyStore.Groq, key = KeyQ });
        reply = null; done = false;
        ai.Plan(new AiClient.PlanRequest { cau_hoi = "cho mình xem kết tủa", the_gioi = scene }, r => { reply = r; done = true; }, e => { planErr = e; done = true; });
        t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 20) yield return null;
        posts = fake.Drain().Where(x => x.method == "POST").ToList();
        var q1 = posts.Count == 2 ? JObject.Parse(posts[1].body) : null;
        Ok(reply != null && reply.ke_hoach.Length == 3 && reply.nguon == "khoa_rieng_groq" && posts.Count == 2 && posts[0].path == "/groq/openai/v1/chat/completions"
           && q1?["response_format"]?["type"]?.ToString() == "json_object" && q1["model"]?.ToString() == "openai/gpt-oss-120b" && q1["reasoning_effort"]?.ToString() == "low"
           && posts[1].headers.TryGetValue("authorization", out var gq) && gq == "Bearer " + KeyQ,
           "Groq: mô hình không nhận json_schema thì chuyển json_object, gpt-oss suy nghĩ ít", $"{posts.Count} lần gọi, {reply?.nguon ?? planErr}");
        okMsg = errMsg = null;
        yield return DirectAi.Check(new ApiKeyStore.Entry { provider = ApiKeyStore.Groq, key = "gsk_SAIKHOA0000111122223333444455556666" }, x => okMsg = x, x => errMsg = x);
        Ok(errMsg != null && errMsg.Contains("401") && !errMsg.Contains("SAIKHOA0000111122223333"), "khóa Groq sai: báo 401 và che khóa", errMsg);
        fake.Drain();

        // Khóa sai khi đang trong lab: báo lỗi, không lặng lẽ chuyển sang máy chủ
        ApiKeyStore.Save(ApiKeyStore.Owner, new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, key = WrongKey });
        reply = null; planErr = null; done = false;
        ai.Plan(new AiClient.PlanRequest { cau_hoi = "xin chào", the_gioi = scene }, r => { reply = r; done = true; }, e => { planErr = e; done = true; });
        t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 20) yield return null;
        Ok(reply == null && planErr != null && planErr.Contains("401") && ai.LastDirectError.Contains("401"), "khóa sai trong lab: báo lỗi cho khung chat", planErr);
        fake.Drain();

        // ---------- 5. Trang Trợ lý AI ở sảnh ----------
        ApiKeyStore.Delete(ApiKeyStore.Owner);
        var canvasGo = new GameObject("Thử trang khóa AI", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.5f;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        canvasGo.AddComponent<GraphicRaycaster>();
        var bg = MenuKit.Full(canvasGo.transform, "Nền");
        bg.gameObject.AddComponent<Image>().color = MenuKit.PaperAlt;
        var pageRt = MenuKit.Full(canvasGo.transform, "Trang: Trợ lý AI");
        var page = pageRt.gameObject.AddComponent<PageAiKey>();
        page.Build(pageRt);
        page.Open();
        Ok(page.CurrentText.Contains("máy chủ"), "trang mở: chưa có khóa thì dùng AI máy chủ", page.CurrentText);
        Ok(page.Provider == ApiKeyStore.Gemini, "trang mở: mặc định chọn Gemini");
        page.SelectProvider(ApiKeyStore.OpenAi);
        page.KeyField.text = KeyB;
        page.Check();
        t0 = Time.realtimeSinceStartup;
        while (page.Busy && Time.realtimeSinceStartup - t0 < 15) yield return null;
        Ok(page.StatusText.Contains("dùng được") && page.StatusText.Contains("gpt-5"), "nút Kiểm tra khóa", page.StatusText);
        page.Save();
        Ok(page.KeyField.text == "" && ApiKeyStore.Current?.key == KeyB && page.CurrentText.Contains("OpenAI") && !page.CurrentText.Contains("KHOAGIA"),
           "nút Lưu: lưu khóa, xóa ô nhập, chỉ hiện dạng che", page.CurrentText);
        page.Open();
        Ok(page.KeyField.text == "" && page.Provider == ApiKeyStore.OpenAi, "mở lại trang: không hiện lại khóa thật, nhớ nhà cung cấp");
        yield return null;
        Canvas.ForceUpdateCanvases();
        Shot(cam, outDir, "khoa_ai_trang_sanh");
        page.Delete();
        Ok(ApiKeyStore.Current == null && page.CurrentText.Contains("máy chủ"), "nút Xóa khóa", page.CurrentText);
        UnityEngine.Object.Destroy(canvasGo);
        fake.Drain();

        // ---------- 6. Không rò khóa ----------
        yield return null;
        string all;
        lock (logs) all = string.Join("\n", logs);
        Ok(!all.Contains("KHOAGIA") && !all.Contains("SAIKHOA-ZZZZ-0000") && !all.Contains("SAIKHOA0000111122223333"), "khóa không xuất hiện trong log Unity", logs.Count + " dòng log");

        // dọn
        fake.Stop();
        DirectAi.ClaudeBase = oc; DirectAi.OpenAiBase = oo; DirectAi.GeminiBase = og; DirectAi.GroqBase = oq;
        GameSession.User = oldUser;
        ApiKeyStore.Forget();
        try { Directory.Delete(dir, true); } catch { }
        ApiKeyStore.Root = oldRoot;
        Log($"KẾT QUẢ: {pass} đạt, {fail} lỗi");
    }

    static void Shot(Camera cam, string outDir, string name)
    {
        var rt = new RenderTexture(1600, 900, 24);
        var old = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = old;
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        UnityEngine.Object.Destroy(rt);
        UnityEngine.Object.Destroy(tex);
    }
}
