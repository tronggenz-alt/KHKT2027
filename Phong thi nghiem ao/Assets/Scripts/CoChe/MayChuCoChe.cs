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
