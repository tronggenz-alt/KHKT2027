using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Khkt.Menu;
using UnityEngine;
using UnityEngine.Networking;

namespace Khkt.CoChe
{
    /// <summary>
    /// Gọi máy chủ cơ chế (server/ai/co_che/may_chu.py, HOP_DONG_APP.md mục 5). URL lưu trong PlayerPrefs, mặc định
    /// http://127.0.0.1:8010. Kiểm thử đặt GiaLap để trả lời giả, không cần mạng.
    /// Có khóa AI riêng Claude/Gemini (Cài đặt → Trợ lý AI) thì các yêu cầu cần AI gửi kèm khóa qua tiêu đề X-Vera-*: máy chủ
    /// gọi AI bằng khóa đó cho riêng yêu cầu ấy, không lưu. Chỉ gửi tới máy chủ trên máy này, trong mạng LAN hoặc qua https.
    /// </summary>
    public static class MayChuCoChe
    {
        /// <summary>Đường dẫn có gọi AI đám mây trên máy chủ (giai_de.goi_ai_mac_dinh): chỉ những yêu cầu này mang khóa riêng.</summary>
        static readonly HashSet<string> DuongCoAi = new HashSet<string>
        {
            "/co-che/giai-de", "/co-che/giai-chuoi", "/co-che/mo-hinh-3d", "/co-che/hoi-dap", "/co-che/giai-thich-chi-tiet",
        };

        /// <summary>
        /// Tiêu đề khóa AI riêng cho một yêu cầu (rỗng khi không có khóa, khóa Groq/OpenAI mà máy chủ cơ chế chưa gọi được,
        /// đường dẫn không cần AI, hoặc máy chủ ở Internet qua http trơn).
        /// </summary>
        public static List<KeyValuePair<string, string>> TieuDeKhoaRieng(string duong, string url, ApiKeyStore.Entry e)
        {
            var ds = new List<KeyValuePair<string, string>>();
            if (e == null || !e.Valid || !DuongCoAi.Contains(duong) || !DuocGuiKhoa(url)) return ds;
            if (e.provider != ApiKeyStore.Claude && e.provider != ApiKeyStore.Gemini) return ds;
            ds.Add(new KeyValuePair<string, string>("X-Vera-Nha-Cung-Cap", e.provider));
            ds.Add(new KeyValuePair<string, string>("X-Vera-Khoa-AI", e.key.Trim()));
            if (!string.IsNullOrWhiteSpace(e.model)) ds.Add(new KeyValuePair<string, string>("X-Vera-Mo-Hinh", e.model.Trim()));
            return ds;
        }

        /// <summary>
        /// Được gửi khóa tới máy chủ này không: https, hoặc http tới máy này / mạng LAN (10.x, 172.16–31.x, 192.168.x, 169.254.x,
        /// IPv6 nội bộ, tên .local). Máy chủ ở Internet qua http trơn thì không, để khóa không đi qua mạng ở dạng chữ đọc được.
        /// </summary>
        public static bool DuocGuiKhoa(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            if (u.Scheme == Uri.UriSchemeHttps) return true;
            if (u.Scheme != Uri.UriSchemeHttp) return false;
            if (u.IsLoopback) return true;
            string h = u.Host.Trim('[', ']').ToLowerInvariant();
            if (h == "localhost" || h.EndsWith(".local")) return true;
            if (!IPAddress.TryParse(h, out var ip)) return false;
            var b = ip.GetAddressBytes();
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
            return ip.AddressFamily == AddressFamily.InterNetworkV6 && (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (b[0] & 0xFE) == 0xFC);
        }

        static void GanKhoaRieng(UnityWebRequest r, string duong)
        {
            foreach (var kv in TieuDeKhoaRieng(duong, Url, ApiKeyStore.Current)) r.SetRequestHeader(kv.Key, kv.Value);
        }

        public const string KhoaUrl = "VERA_CO_CHE_URL";
        public const string UrlMacDinh = "http://127.0.0.1:8010";

        /// <summary>Máy chủ giả cho kiểm thử: (phương thức, đường dẫn, thân) → (mã HTTP, chuỗi JSON).</summary>
        public static Func<string, string, string, (long ma, string json)> GiaLap;

        public static string Url
        {
            get => PlayerPrefs.GetString(KhoaUrl, UrlMacDinh).TrimEnd('/');
            set { PlayerPrefs.SetString(KhoaUrl, string.IsNullOrWhiteSpace(value) ? UrlMacDinh : value.Trim().TrimEnd('/')); PlayerPrefs.Save(); }
        }

        public static IEnumerator Goi(string phuongThuc, string duong, string than, float cho, Action<bool, string> xong)
        {
            if (GiaLap != null)
            {
                var (ma, json) = GiaLap(phuongThuc, duong, than);
                yield return null;
                xong(ma >= 200 && ma < 300, ma >= 200 && ma < 300 ? json : $"Máy chủ trả mã {ma}");
                yield break;
            }
            using var r = new UnityWebRequest(Url + duong, phuongThuc);
            r.downloadHandler = new DownloadHandlerBuffer();
            if (than != null)
            {
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(than));
                r.SetRequestHeader("Content-Type", "application/json");
            }
            GanKhoaRieng(r, duong);
            r.timeout = Mathf.CeilToInt(cho);
            yield return r.SendWebRequest();
            if (r.result != UnityWebRequest.Result.Success)
                xong(false, r.responseCode > 0 ? LoiTuMayChu(r.responseCode, r.downloadHandler?.text) : $"Không kết nối được {Url} ({r.error})");
            else xong(true, r.downloadHandler.text);
        }

        /// <summary>
        /// POST /co-che/giai-de: đề chữ và/hoặc ảnh đề (gửi base64 trong JSON) → JSON bài (cùng khung gói .verapack, không mã hóa),
        /// mở bằng BaiCoChe.TuJson. Lỗi: thông điệp tiếng Việt của máy chủ (detail.thong_diep + vài dòng chi_tiet) thay vì chỉ mã HTTP.
        /// </summary>
        public static IEnumerator GiaiDe(string vanBan, byte[] anh, string loai, float cho, Action<bool, string> xong, int mucTuDuy = -1)
        {
            var sb = new StringBuilder("{\"van_ban\":").Append(ChuoiJson(vanBan ?? ""));
            if (mucTuDuy >= 0) sb.Append(",\"muc_tu_duy\":").Append(mucTuDuy);
            if (!string.IsNullOrEmpty(loai)) sb.Append(",\"loai\":").Append(ChuoiJson(loai));
            if (anh != null && anh.Length > 0) sb.Append(",\"anh_base64\":\"").Append(Convert.ToBase64String(anh)).Append('"');
            string than = sb.Append('}').ToString();
            if (GiaLap != null)
            {
                var (ma, json) = GiaLap("POST", "/co-che/giai-de", than);
                yield return null;
                bool ok = ma >= 200 && ma < 300;
                xong(ok, ok ? json : LoiTuMayChu(ma, json));
                yield break;
            }
            using var r = new UnityWebRequest(Url + "/co-che/giai-de", "POST");
            r.downloadHandler = new DownloadHandlerBuffer();
            r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(than));
            r.SetRequestHeader("Content-Type", "application/json");
            GanKhoaRieng(r, "/co-che/giai-de");
            r.timeout = Mathf.CeilToInt(cho);
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success) xong(true, r.downloadHandler.text);
            else if (r.responseCode > 0) xong(false, LoiTuMayChu(r.responseCode, r.downloadHandler?.text));
            else xong(false, r.error != null && r.error.ToLowerInvariant().Contains("timeout")
                               ? $"Quá {cho:0} giây chưa có kết quả – thử lại."
                               : $"Không kết nối được {Url} ({r.error}). Bật máy chủ: python -m server.ai.co_che.may_chu");
        }

        /// <summary>{"detail": {"thong_diep", "ma", "chi_tiet": [...]}} hoặc {"detail": "..."} → một đoạn chữ cho người dùng.</summary>
        public static string LoiTuMayChu(long ma, string json)
        {
            try
            {
                var d = JsonNho.D(JsonNho.Doc(json));
                var det = JsonNho.Lay(d, "detail");
                if (det is string s1 && s1.Length > 0) return s1;
                var dd = JsonNho.D(det);
                string tb = JsonNho.S(dd, "thong_diep");
                var ct = new StringBuilder(tb);
                int n = 0;
                foreach (var o in JsonNho.L(JsonNho.Lay(dd, "chi_tiet")))
                {
                    if (n++ >= 4) { ct.Append("\n  …"); break; }
                    ct.Append("\n  • ").Append(o as string);
                }
                if (tb.Length > 0) return ct.ToString();
            }
            catch (Exception) { }
            return $"Máy chủ trả mã {ma}";
        }

        public static string ChuoiJson(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
