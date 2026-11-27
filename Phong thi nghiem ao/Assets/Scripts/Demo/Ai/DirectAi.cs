using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Khkt.Menu;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Khkt.Demo.Ai
{
    /// <summary>
    /// Gọi thẳng nhà cung cấp AI đám mây bằng khóa riêng của người dùng (không qua máy chủ của dự án).
    /// Khóa chỉ nằm trong header của yêu cầu HTTPS; không bao giờ ở trong URL, log hay thông báo lỗi.
    /// Claude: Messages API. OpenAI, Gemini, Groq: chat/completions (Gemini, Groq dùng cổng tương thích OpenAI, như mạng lưới khóa trên máy chủ).
    /// </summary>
    public static class DirectAi
    {
        // Địa chỉ gốc; bài thử trỏ về máy chủ giả trên 127.0.0.1
        public static string ClaudeBase = "https://api.anthropic.com/v1";
        public static string OpenAiBase = "https://api.openai.com/v1";
        public static string GeminiBase = "https://generativelanguage.googleapis.com/v1beta/openai";
        public static string GroqBase = "https://api.groq.com/openai/v1";

        public const int PlanMaxTokens = 8000, TextMaxTokens = 600;
        const int TimeoutS = 90, CheckTimeoutS = 15;

        static string Base(string p) => p == ApiKeyStore.Claude ? ClaudeBase : p == ApiKeyStore.Gemini ? GeminiBase : p == ApiKeyStore.Groq ? GroqBase : OpenAiBase;

        // Mô hình tự chọn cho khóa OpenAI không ghi mô hình (nhớ trong bộ nhớ theo băm của khóa)
        static readonly Dictionary<string, string> autoModel = new Dictionary<string, string>();

        static string KeyId(string key)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key ?? "")), 0, 12);
        }

        // ------------------------------------------------------------------
        // Kiểm tra khóa: lấy danh sách mô hình (không tốn lượt gọi mô hình)
        // ------------------------------------------------------------------

        /// <summary>Kiểm tra khóa; ok nhận tên mô hình sẽ dùng (tự chọn nếu chưa ghi).</summary>
        public static IEnumerator Check(ApiKeyStore.Entry e, Action<string> ok, Action<string> fail)
        {
            if (e == null || !e.Valid) { fail?.Invoke("Chưa nhập khóa."); yield break; }
            using (var r = UnityWebRequest.Get(Base(e.provider) + "/models" + (e.provider == ApiKeyStore.Claude ? "?limit=100" : "")))
            {
                Auth(r, e);
                r.timeout = CheckTimeoutS;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { fail?.Invoke(Describe(r, e)); yield break; }
                List<string> ids;
                try { ids = ModelIds(r.downloadHandler.text); }
                catch { fail?.Invoke("Nhà cung cấp trả lời không đúng định dạng."); yield break; }
                string want = e.ModelOrDefault;
                if (want.Length == 0)
                {
                    want = PickModel(e.provider, ids);
                    if (want == null) { fail?.Invoke("Khóa dùng được nhưng không thấy mô hình trò chuyện nào. Hãy ghi tên mô hình."); yield break; }
                    autoModel[KeyId(e.key)] = want;
                }
                else if (ids.Count > 0 && !ids.Contains(want) && !ids.Contains("models/" + want) && !want.EndsWith("-latest"))
                {
                    fail?.Invoke($"Khóa dùng được nhưng không có mô hình '{want}'. Bỏ trống ô mô hình để dùng mặc định.");
                    yield break;
                }
                ok?.Invoke(want);
            }
        }

        static List<string> ModelIds(string json)
        {
            var o = JObject.Parse(json);
            var arr = o["data"] as JArray ?? o["models"] as JArray ?? new JArray();
            return arr.OfType<JObject>().Select(m => (m["id"] ?? m["name"])?.ToString()).Where(s => !string.IsNullOrEmpty(s))
                      .Select(s => s.StartsWith("models/") ? s.Substring(7) : s).ToList();
        }

        static readonly Regex NotChat = new Regex(@"embed|tts|whisper|audio|speech|transcri|guard|moderat|image|imagen|veo|lyria|banana|aqa|(^|-)live(-|$)|native|computer-use|robotics|realtime|search|codex|playai|orpheus|compound|distil|instruct|davinci|babbage|dall|sora|(^|-)pro(-|$)|chat-latest|deep-research|\d{4}-\d{2}-\d{2}");

        /// <summary>Chọn mô hình trò chuyện mạnh nhất trong danh sách (giống cách xếp hạng của mạng lưới khóa).</summary>
        public static string PickModel(string provider, IEnumerable<string> ids)
        {
            var names = ids.Where(i => !NotChat.IsMatch(i.ToLowerInvariant()))
                           .Where(i => provider != ApiKeyStore.OpenAi || Regex.IsMatch(i, @"^(gpt-|o\d)"))
                           .Where(i => provider != ApiKeyStore.Claude || i.StartsWith("claude-"))
                           .Distinct().ToList();
            if (names.Count == 0) return null;
            return names.OrderByDescending(Strength).ThenBy(n => n, StringComparer.Ordinal).First();
        }

        static double Strength(string id)
        {
            string m = id.ToLowerInvariant();
            double version = 0;
            var words = new HashSet<string>();
            foreach (var tok in m.Split('-', '_', '/', ':'))
            {
                var mm = Regex.Match(tok, @"^[a-z]*(\d+(?:\.\d+)?)$");
                if (mm.Success && version == 0 && double.TryParse(mm.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && v < 20) version = v;
                else words.Add(tok);
            }
            double s = 10 * version;
            if (words.Contains("mini")) s -= 12;
            if (words.Contains("nano")) s -= 20;
            if (words.Contains("lite")) s -= 15;
            if (words.Contains("preview") || words.Contains("exp")) s -= 2;
            if (words.Contains("haiku")) s -= 12;
            if (words.Contains("opus")) s += 25;
            return s;
        }

        // ------------------------------------------------------------------
        // Gọi mô hình
        // ------------------------------------------------------------------

        /// <summary>JSON theo khuôn (kế hoạch robot).</summary>
        public static IEnumerator Structured(ApiKeyStore.Entry e, string system, string user, JObject schema, int maxTokens, Action<JObject> ok, Action<string> fail)
        {
            string text = null, err = null;
            yield return Call(e, system, user, schema, maxTokens, t => text = t, x => err = x);
            if (err != null) { fail?.Invoke(err); yield break; }
            JObject o = ParseLenient(text);
            if (o == null) { fail?.Invoke("AI trả lời không đúng định dạng JSON."); yield break; }
            ok?.Invoke(o);
        }

        /// <summary>Câu trả lời chữ (chú thích hiện tượng).</summary>
        public static IEnumerator Text(ApiKeyStore.Entry e, string system, string user, int maxTokens, Action<string> ok, Action<string> fail)
            => Call(e, system, user, null, maxTokens, ok, fail);

        public static JObject ParseLenient(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int a = text.IndexOf('{'), b = text.LastIndexOf('}');
            if (a < 0 || b <= a) return null;
            try { return JObject.Parse(text.Substring(a, b - a + 1)); }
            catch { return null; }
        }

        static IEnumerator Call(ApiKeyStore.Entry e, string system, string user, JObject schema, int maxTokens, Action<string> ok, Action<string> fail)
        {
            if (e == null || !e.Valid) { fail?.Invoke("Chưa có khóa API."); yield break; }
            string model = e.ModelOrDefault;
            if (model.Length == 0 && !autoModel.TryGetValue(KeyId(e.key), out model))
            {
                string picked = null, err = null;
                yield return Check(e, m => picked = m, x => err = x);
                if (err != null) { fail?.Invoke(err); yield break; }
                model = picked;
            }

            // Mô hình dự phòng khi nhà cung cấp quá tải (5xx / 429): chỉ khi người dùng không tự ghi mô hình
            var models = new List<string> { model };
            if (string.IsNullOrWhiteSpace(e.model))
                foreach (var m in FallbackModels(e.provider)) if (!models.Contains(m)) models.Add(m);
            string url = Base(e.provider) + (e.provider == ApiKeyStore.Claude ? "/messages" : "/chat/completions");
            string lastErr = "Không gọi được AI.";
            foreach (var mdl in models)
            {
                var variants = e.provider == ApiKeyStore.Claude ? ClaudeBodies(mdl, system, user, schema, maxTokens) : ChatBodies(e.provider, mdl, system, user, schema, maxTokens);
                bool nextModel = false;
                foreach (var body in variants)
                {
                    long code = 0;
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        if (attempt > 0) yield return new WaitForSecondsRealtime(2f);
                        string text = null, why = null, err = null;
                        yield return Send(url, e, body, (c, t) => { code = c; text = t; }, x => err = x);
                        if (err == null)
                        {
                            try { text = e.provider == ApiKeyStore.Claude ? ClaudeText(text, out why) : ChatText(text, out why); }
                            catch { fail?.Invoke("Nhà cung cấp trả lời không đúng định dạng."); yield break; }
                            if (why != null) { fail?.Invoke(why); yield break; }
                            ok?.Invoke(text);
                            yield break;
                        }
                        lastErr = err;
                        if (!(code == 429 || code >= 500)) break;  // chỉ gọi lại khi quá tải / lỗi tạm thời
                    }
                    if (code == 400) continue;                     // thử dạng yêu cầu đơn giản hơn (mô hình không nhận khuôn JSON / mức suy nghĩ)
                    if (code == 0 || code == 404 || code == 429 || code >= 500) { nextModel = true; break; }  // quá lâu / quá tải: sang mô hình dự phòng
                    fail?.Invoke(lastErr);
                    yield break;
                }
                if (!nextModel) break;
            }
            fail?.Invoke(lastErr);
        }

        static IEnumerable<string> FallbackModels(string p) =>
            p == ApiKeyStore.Gemini ? new[] { "gemini-flash-lite-latest", "gemini-2.5-flash" }
            : p == ApiKeyStore.Groq ? new[] { "llama-3.3-70b-versatile" }
            : p == ApiKeyStore.Claude ? new[] { "claude-sonnet-5-5" }
            : new string[0];

        /// <summary>Gửi một yêu cầu; ok nhận (mã, thân), fail nhận lỗi đã che khóa (mã vẫn được báo qua ok trước).</summary>
        static IEnumerator Send(string url, ApiKeyStore.Entry e, JObject body, Action<long, string> ok, Action<string> fail)
        {
            using (var r = new UnityWebRequest(url, "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                r.downloadHandler = new DownloadHandlerBuffer();
                r.SetRequestHeader("Content-Type", "application/json");
                Auth(r, e);
                if (body["fallbacks"] != null) r.SetRequestHeader("anthropic-beta", "server-side-fallback-2026-07-01");
                r.timeout = TimeoutS;
                yield return r.SendWebRequest();
                ok?.Invoke(r.responseCode, r.downloadHandler?.text ?? "");
                if (r.result != UnityWebRequest.Result.Success) fail?.Invoke(Describe(r, e));
            }
        }

        // ------------------------------------------------------------------
        // Giọng nói → chữ (bản đi thi 27/11/2026): Gemini nhận âm thanh trực tiếp; Groq và OpenAI dùng Whisper
        // ------------------------------------------------------------------

        const string VoicePrompt =
            "Chép lại CHÍNH XÁC lời nói tiếng Việt trong đoạn âm thanh. Người nói là học sinh trong phòng thí nghiệm hóa học và sinh học: " +
            "giữ nguyên tên hóa chất và công thức (NaOH, HCl, CuSO4, KSCN, FeCl3, phenolphtalein...), tên dụng cụ (ống nghiệm, buret, pipet, bình tam giác...) " +
            "và thuật ngữ sinh học. Có dấu tiếng Việt và dấu câu. Chỉ trả về đúng nội dung lời nói, không giải thích, không thêm lời dẫn. " +
            "Nếu không có lời nói rõ ràng thì trả về một chuỗi rỗng.";

        /// <summary>Chuyển tệp WAV (16 bit, mono) thành chữ tiếng Việt. Claude không nhận âm thanh nên báo lỗi rõ ràng.</summary>
        public static IEnumerator Transcribe(ApiKeyStore.Entry e, byte[] wav, Action<string> ok, Action<string> fail)
        {
            if (e == null || !e.Valid) { fail?.Invoke("Chưa có khóa AI."); yield break; }
            if (e.provider == ApiKeyStore.Claude)
            {
                fail?.Invoke("Khóa Claude không nhận âm thanh: dùng khóa Google Gemini (miễn phí), Groq hoặc OpenAI để nói chuyện với trợ lý");
                yield break;
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                UnityWebRequest r;
                if (e.provider == ApiKeyStore.Gemini)
                {
                    string model = e.ModelOrDefault.StartsWith("gemini") ? e.ModelOrDefault : "gemini-flash-latest";
                    var body = new JObject
                    {
                        ["contents"] = new JArray(new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject { ["text"] = VoicePrompt },
                                new JObject { ["inline_data"] = new JObject { ["mime_type"] = "audio/wav", ["data"] = Convert.ToBase64String(wav) } }),
                        }),
                        ["generationConfig"] = new JObject { ["temperature"] = 0 },
                    };
                    string url = GeminiBase.Replace("/openai", "") + "/models/" + model + ":generateContent";
                    r = new UnityWebRequest(url, "POST")
                    {
                        uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None))),
                        downloadHandler = new DownloadHandlerBuffer(),
                    };
                    r.SetRequestHeader("Content-Type", "application/json");
                    r.SetRequestHeader("x-goog-api-key", e.key.Trim());
                }
                else
                {
                    bool groq = e.provider == ApiKeyStore.Groq;
                    var form = new List<IMultipartFormSection>
                    {
                        new MultipartFormDataSection("model", groq ? "whisper-large-v3-turbo" : "whisper-1"),
                        new MultipartFormDataSection("language", "vi"),
                        new MultipartFormDataSection("response_format", "json"),
                        new MultipartFormDataSection("prompt", "Phòng thí nghiệm hóa học, sinh học: NaOH, HCl, CuSO4, ống nghiệm, buret, pipet."),
                        new MultipartFormFileSection("file", wav, "giong_noi.wav", "audio/wav"),
                    };
                    r = UnityWebRequest.Post(Base(e.provider) + "/audio/transcriptions", form);
                    Auth(r, e);
                }
                using (r)
                {
                    r.timeout = 40;
                    yield return r.SendWebRequest();
                    if (r.result != UnityWebRequest.Result.Success)
                    {
                        if (attempt == 0 && (r.responseCode >= 500 || r.responseCode == 0)) { yield return new WaitForSecondsRealtime(0.8f); continue; }
                        fail?.Invoke(Describe(r, e));
                        yield break;
                    }
                    string text;
                    try
                    {
                        var root = JObject.Parse(r.downloadHandler.text);
                        if (e.provider == ApiKeyStore.Gemini)
                        {
                            var parts = root["candidates"]?[0]?["content"]?["parts"] as JArray;
                            text = parts == null ? "" : string.Concat(parts.Select(p => p["text"]?.ToString() ?? ""));
                        }
                        else text = root["text"]?.ToString() ?? "";
                    }
                    catch { fail?.Invoke("Nhà cung cấp trả lời không đúng định dạng."); yield break; }
                    text = text.Trim().Trim('"', '\u201c', '\u201d').Trim();
                    ok?.Invoke(text);
                    yield break;
                }
            }
        }

        static void Auth(UnityWebRequest r, ApiKeyStore.Entry e)
        {
            if (e.provider == ApiKeyStore.Claude)
            {
                r.SetRequestHeader("x-api-key", e.key.Trim());
                r.SetRequestHeader("anthropic-version", "2023-06-01");
            }
            else r.SetRequestHeader("Authorization", "Bearer " + e.key.Trim());
        }

        static bool ClaudeHasEffort(string m) => Regex.IsMatch(m, @"^claude-(opus|sonnet|fable|mythos|haiku)-[5-9]") || Regex.IsMatch(m, @"^claude-opus-4-[5-9]") || m == "claude-sonnet-4-6";
        static bool ClaudeHasFallbacks(string m) => m == "claude-opus-5-5" || m == "claude-opus-5" || m == "claude-sonnet-5-5" || m == "claude-fable-5-1" || m == "claude-fable-5-2";

        static IEnumerable<JObject> ClaudeBodies(string model, string system, string user, JObject schema, int maxTokens)
        {
            for (int v = 0; v < 2; v++)
            {
                var body = new JObject
                {
                    ["model"] = model,
                    ["max_tokens"] = maxTokens,
                    ["system"] = new JArray(new JObject { ["type"] = "text", ["text"] = system, ["cache_control"] = new JObject { ["type"] = "ephemeral" } }),
                    ["messages"] = new JArray(new JObject { ["role"] = "user", ["content"] = schema != null && v == 1 ? user + "\n\nChỉ trả lời bằng một đối tượng JSON đúng khuôn: " + schema.ToString(Newtonsoft.Json.Formatting.None) : user }),
                };
                var oc = new JObject();
                if (v == 0 && ClaudeHasEffort(model)) oc["effort"] = "low";
                if (v == 0 && schema != null) oc["format"] = new JObject { ["type"] = "json_schema", ["schema"] = schema };
                if (oc.Count > 0) body["output_config"] = oc;
                if (v == 0 && ClaudeHasFallbacks(model)) body["fallbacks"] = "default";
                yield return body;
            }
        }

        static string ClaudeText(string json, out string why)
        {
            var d = JObject.Parse(json);
            why = null;
            string stop = d["stop_reason"]?.ToString();
            if (stop == "refusal") { why = "Mô hình từ chối trả lời yêu cầu này."; return null; }
            if (stop == "max_tokens") { why = "Câu trả lời bị cắt (hết max_tokens)."; return null; }
            var sb = new StringBuilder();
            foreach (var b in (d["content"] as JArray ?? new JArray()).OfType<JObject>())
                if (b["type"]?.ToString() == "text") sb.Append(b["text"]);
            return sb.ToString().Trim();
        }

        static IEnumerable<JObject> ChatBodies(string provider, string model, string system, string user, JObject schema, int maxTokens)
        {
            bool gemini = provider == ApiKeyStore.Gemini, groq = provider == ApiKeyStore.Groq, lowFirst = gemini || (groq && model.Contains("gpt-oss"));
            // Thứ tự thử: khuôn JSON chặt (+ suy nghĩ ít với Gemini, gpt-oss trên Groq), khuôn JSON chặt, rồi JSON thường / chỉ dặn bằng lời
            if (groq && schema != null)
            {
                foreach (var b in GroqBodies(model, system, user, schema, maxTokens, lowFirst)) yield return b;
                yield break;
            }
            int n = schema == null ? (lowFirst ? 2 : 1) : (gemini ? 3 : 2);
            for (int v = 0; v < n; v++)
            {
                bool useSchema = schema != null && (gemini ? v < 2 : v == 0);
                bool lowEffort = lowFirst && v == 0;
                string sys = system + (schema != null && !useSchema ? "\nChỉ trả lời bằng một đối tượng JSON đúng khuôn: " + schema.ToString(Newtonsoft.Json.Formatting.None) : "");
                var body = new JObject
                {
                    ["model"] = model,
                    ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = sys }, new JObject { ["role"] = "user", ["content"] = user }),
                };
                // OpenAI đời mới không nhận max_tokens; Gemini (cổng tương thích) dùng max_tokens, cộng chỗ cho phần suy nghĩ
                if (gemini) body["max_tokens"] = maxTokens + 2048;
                else body["max_completion_tokens"] = maxTokens + 4096;
                if (lowEffort) body["reasoning_effort"] = "low";
                if (useSchema) body["response_format"] = new JObject { ["type"] = "json_schema", ["json_schema"] = new JObject { ["name"] = "ket_qua", ["strict"] = true, ["schema"] = schema } };
                else if (schema != null && !gemini) body["response_format"] = new JObject { ["type"] = "json_object" };
                yield return body;
            }
        }

        static IEnumerable<JObject> GroqBodies(string model, string system, string user, JObject schema, int maxTokens, bool low)
        {
            string strict = "\nChỉ trả lời bằng một đối tượng JSON đúng khuôn: " + schema.ToString(Newtonsoft.Json.Formatting.None);
            // json_schema chặt (chỉ một số mô hình Groq nhận) → json_object → chỉ dặn bằng lời
            for (int v = 0; v < 3; v++)
            {
                var body = new JObject
                {
                    ["model"] = model,
                    ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = system + (v > 0 ? strict : "") }, new JObject { ["role"] = "user", ["content"] = user }),
                    ["max_completion_tokens"] = maxTokens + 2048,
                };
                if (low) body["reasoning_effort"] = "low";
                if (v == 0) body["response_format"] = new JObject { ["type"] = "json_schema", ["json_schema"] = new JObject { ["name"] = "ket_qua", ["strict"] = true, ["schema"] = schema } };
                else if (v == 1) body["response_format"] = new JObject { ["type"] = "json_object" };
                yield return body;
            }
        }

        static string ChatText(string json, out string why)
        {
            var d = JObject.Parse(json);
            why = null;
            var choice = (d["choices"] as JArray)?.FirstOrDefault() as JObject;
            if (choice == null) { why = "Nhà cung cấp không trả câu trả lời nào."; return null; }
            var msg = choice["message"] as JObject;
            if (msg?["refusal"] != null && msg["refusal"].Type == JTokenType.String) { why = "Mô hình từ chối trả lời yêu cầu này."; return null; }
            string text = msg?["content"]?.ToString() ?? "";
            if (choice["finish_reason"]?.ToString() == "length" && text.Trim().Length == 0) { why = "Câu trả lời bị cắt (hết max_tokens)."; return null; }
            return text.Trim();
        }

        /// <summary>Lỗi dễ hiểu, không bao giờ chứa khóa.</summary>
        static string Describe(UnityWebRequest r, ApiKeyStore.Entry e)
        {
            long c = r.responseCode;
            string detail = "";
            try
            {
                var root = JToken.Parse(r.downloadHandler?.text ?? "");
                if (root is JArray ra) root = ra.FirstOrDefault();  // Gemini (cổng tương thích OpenAI) trả lỗi dạng mảng
                var er = root?["error"];
                detail = er is JObject eo ? eo["message"]?.ToString() ?? "" : er?.ToString() ?? "";
                if (er is JArray) detail = "";
            }
            catch { }
            if (!string.IsNullOrEmpty(e.key) && detail.Contains(e.key.Trim())) detail = detail.Replace(e.key.Trim(), ApiKeyStore.Mask(e.key));
            detail = Regex.Replace(detail, @"(sk-[A-Za-z0-9_\-]{6})[A-Za-z0-9_\-]+|(AIza[A-Za-z0-9_\-]{4})[A-Za-z0-9_\-]+|(gsk_[A-Za-z0-9]{4})[A-Za-z0-9]+|(AQ\.[A-Za-z0-9]{4})[A-Za-z0-9_\-\.]+", "$1$2$3$4…");
            if (detail.Length > 160) detail = detail.Substring(0, 160) + "…";
            string head;
            if (c == 0 && (r.error ?? "").IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0) head = ApiKeyStore.ProviderName(e.provider) + " trả lời quá lâu (quá " + TimeoutS + " s)";
            else if (c == 0) head = "Không kết nối được tới " + ApiKeyStore.ProviderName(e.provider) + " (mạng?)";
            else if (c == 401 || c == 403) head = $"Khóa không hợp lệ hoặc không có quyền ({c})";
            else if (c == 404) head = $"Không có mô hình '{e.ModelOrDefault}' hoặc sai địa chỉ (404)";
            else if (c == 429) head = "Hết hạn mức hoặc gọi quá nhanh (429)";
            else if (c >= 500) head = $"Nhà cung cấp đang lỗi ({c})";
            else head = $"Lỗi {c}";
            return detail.Length > 0 ? head + ": " + detail : head;
        }
    }
}
