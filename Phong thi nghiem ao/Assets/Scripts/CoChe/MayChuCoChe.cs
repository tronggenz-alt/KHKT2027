using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Khkt.CoChe
{
    /// <summary>
    /// Gọi máy chủ cơ chế (server/ai/co_che/may_chu.py, HOP_DONG_APP.md mục 5). URL lưu trong PlayerPrefs, mặc định
    /// http://127.0.0.1:8010. Kiểm thử đặt GiaLap để trả lời giả, không cần mạng.
    /// </summary>
    public static class MayChuCoChe
    {
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
            r.timeout = Mathf.CeilToInt(cho);
            yield return r.SendWebRequest();
            if (r.result != UnityWebRequest.Result.Success)
                xong(false, r.responseCode > 0 ? $"Máy chủ trả mã {r.responseCode}" : $"Không kết nối được {Url} ({r.error})");
            else xong(true, r.downloadHandler.text);
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
