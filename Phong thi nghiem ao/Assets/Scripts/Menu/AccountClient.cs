using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Khkt.Menu
{
    [Serializable]
    public class AccountClass
    {
        public int id;
        public string ten;
        public string nam_hoc;
    }

    [Serializable]
    public class AccountUser
    {
        public int id;
        public string ten_dang_nhap;
        public string ho_ten;
        public string vai_tro;
        public bool phai_doi_mat_khau;
        public AccountClass[] lop;
    }

    public struct AccountResult
    {
        public bool ok;
        public bool network;
        public bool expired;
        public string message;
    }

    /// <summary>
    /// Gọi API tài khoản của máy chủ (server/, tài liệu ở phiên trang quản lý). Mật khẩu chỉ đi qua
    /// lời gọi, không lưu lại; trên máy chỉ giữ mã phiên, hết hạn sau 14 ngày không dùng.
    /// </summary>
    public static class AccountClient
    {
        const int TimeoutSec = 6;

        [Serializable]
        class LoginBody
        {
            public string ten_dang_nhap;
            public string mat_khau;
            public string kieu;
        }

        [Serializable]
        class RegisterBody
        {
            public string ma_lop;
            public string ho_ten;
            public string ten_dang_nhap;
            public string mat_khau;
            public string kieu;
        }

        [Serializable]
        class ChangePasswordBody
        {
            public string mat_khau_cu;
            public string mat_khau_moi;
        }

        [Serializable]
        class LoginReply
        {
            public string ma_phien;
            public AccountUser nguoi_dung;
        }

        [Serializable]
        class ErrorReply
        {
            public string detail;
        }

        static string SessionFile => Path.Combine(Application.persistentDataPath, "phien_dang_nhap.txt");

        public static IEnumerator Login(string user, string password, Action<AccountResult> done)
        {
            var body = new LoginBody { ten_dang_nhap = user, mat_khau = password, kieu = "ung_dung" };
            yield return Send("POST", "/auth/dang-nhap", JsonUtility.ToJson(body), false, (r, text) =>
            {
                if (r.ok && !AcceptLogin(text))
                {
                    r.ok = false;
                    r.message = "Máy chủ trả về dữ liệu không đọc được.";
                }
                else if (!r.ok && r.message == null)
                {
                    r.message = "Sai tên đăng nhập hoặc mật khẩu.";
                }
                done(r);
            });
        }

        public static IEnumerator Register(string classCode, string fullName, string user, string password, Action<AccountResult> done)
        {
            // kieu "ung_dung": máy chủ trả mã phiên ngay trong lời đăng ký
            var body = new RegisterBody { ma_lop = classCode, ho_ten = fullName, ten_dang_nhap = user, mat_khau = password, kieu = "ung_dung" };
            yield return Send("POST", "/auth/dang-ky", JsonUtility.ToJson(body), false, (r, text) =>
            {
                if (r.ok && !AcceptLogin(text))
                {
                    r.ok = false;
                    r.message = "Máy chủ trả về dữ liệu không đọc được.";
                }
                else if (!r.ok && r.message == null)
                {
                    r.message = "Không tạo được tài khoản. Kiểm tra lại mã lớp.";
                }
                done(r);
            });
        }

        /// <summary>Dùng lại phiên đã lưu. Mã hết hạn thì xóa; mất mạng thì giữ để lần sau thử lại.</summary>
        public static IEnumerator Restore(Action<AccountResult> done)
        {
            string saved = ReadSavedToken();
            if (string.IsNullOrEmpty(saved))
            {
                done(new AccountResult { ok = false });
                yield break;
            }
            GameSession.Token = saved;
            yield return Send("GET", "/auth/toi", null, true, (r, text) =>
            {
                if (r.ok)
                {
                    try { GameSession.User = JsonUtility.FromJson<AccountUser>(text); }
                    catch (ArgumentException) { r.ok = false; }
                    if (GameSession.User == null) r.ok = false;
                }
                if (!r.ok)
                {
                    GameSession.Token = null;
                    if (!r.network) ClearSavedToken();
                }
                done(r);
            });
        }

        public static IEnumerator ChangePassword(string current, string next, Action<AccountResult> done)
        {
            var body = new ChangePasswordBody { mat_khau_cu = current, mat_khau_moi = next };
            yield return Send("POST", "/auth/doi-mat-khau", JsonUtility.ToJson(body), true, (r, _) =>
            {
                if (r.ok && GameSession.User != null) GameSession.User.phai_doi_mat_khau = false;
                if (r.expired) r.message = "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.";
                else if (!r.ok && r.message == null) r.message = "Mật khẩu hiện tại không đúng.";
                done(r);
            });
        }

        public static IEnumerator Logout()
        {
            string token = GameSession.Token;
            Khkt.Cache.ApiCache.ClearUser();  // gọi trước SignOut: còn biết đang là tài khoản nào
            GameSession.SignOut();
            ClearSavedToken();
            if (string.IsNullOrEmpty(token)) yield break;
            GameSession.Token = token;
            yield return Send("POST", "/auth/dang-xuat", "{}", true, (_, __) => { });
            GameSession.Token = null;
        }

        public static IEnumerator CheckServer(string url, Action<bool> done)
        {
            using (var req = UnityWebRequest.Get(url.TrimEnd('/') + "/suc-khoe"))
            {
                req.timeout = TimeoutSec;
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success);
            }
        }

        /// <summary>Gắn mã phiên vào yêu cầu tới máy chủ, nếu đang đăng nhập.</summary>
        public static void Authorize(UnityWebRequest req)
        {
            if (!string.IsNullOrEmpty(GameSession.Token)) req.SetRequestHeader("Authorization", "Bearer " + GameSession.Token);
        }

        /// <summary>Máy chủ trả 401 với mã phiên đang gửi: phiên đã hết. Bỏ mã, chơi tiếp ở chế độ dùng thử.</summary>
        public static void CheckExpired(UnityWebRequest req)
        {
            if (req.responseCode != 401 || string.IsNullOrEmpty(GameSession.Token)) return;
            ExpireSession();
            GameSession.GuestActive = true;
            Khkt.Demo.LabMessages.Say("Phiên đăng nhập đã hết hạn: kết quả từ giờ không được lưu vào tài khoản");
        }

        public static void ExpireSession()
        {
            Khkt.Cache.ApiCache.ClearUser();
            GameSession.SignOut();
            ClearSavedToken();
        }

        static bool AcceptLogin(string text)
        {
            LoginReply reply;
            try { reply = JsonUtility.FromJson<LoginReply>(text); }
            catch (ArgumentException) { return false; }
            if (reply == null || string.IsNullOrEmpty(reply.ma_phien) || reply.nguoi_dung == null) return false;
            GameSession.Token = reply.ma_phien;
            GameSession.User = reply.nguoi_dung;
            GameSession.GuestActive = false;
            SaveToken(reply.ma_phien);
            return true;
        }

        static IEnumerator Send(string method, string path, string json, bool auth, Action<AccountResult, string> done)
        {
            using (var req = new UnityWebRequest(GameSession.ServerUrl + path, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = TimeoutSec;
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (auth) Authorize(req);
                yield return req.SendWebRequest();

                var r = new AccountResult { ok = req.result == UnityWebRequest.Result.Success };
                if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
                {
                    r.network = true;
                    r.message = "Không kết nối được máy chủ tài khoản. Bạn vẫn có thể dùng thử không cần tài khoản.";
                }
                else if (!r.ok)
                {
                    r.expired = req.responseCode == 401;
                    r.message = ErrorMessage(req);
                }
                done(r, req.downloadHandler != null ? req.downloadHandler.text : "");
            }
        }

        static string ErrorMessage(UnityWebRequest req)
        {
            long code = req.responseCode;
            if (code == 429)
            {
                string wait = req.GetResponseHeader("Retry-After");
                return string.IsNullOrEmpty(wait) ? "Sai quá nhiều lần. Hãy đợi một lát rồi thử lại."
                                                  : $"Sai quá nhiều lần. Thử lại sau {wait} giây.";
            }
            if (code == 401) return null;
            if (code == 422) return "Thông tin chưa hợp lệ. Kiểm tra lại các ô đã nhập.";
            string text = req.downloadHandler.text;
            // FastAPI trả detail dạng mảng khi thiếu trường; chỉ câu chữ mới hiện cho người dùng
            if (text != null && text.Contains("\"detail\":\""))
            {
                try
                {
                    var e = JsonUtility.FromJson<ErrorReply>(text);
                    if (e != null && !string.IsNullOrEmpty(e.detail)) return e.detail;
                }
                catch (ArgumentException) { }
            }
            return code >= 500 ? $"Máy chủ gặp lỗi ({code}). Thử lại sau." : null;
        }

        static string ReadSavedToken()
        {
            try { return File.Exists(SessionFile) ? File.ReadAllText(SessionFile).Trim() : null; }
            catch (IOException) { return null; }
        }

        static void SaveToken(string token)
        {
            try { File.WriteAllText(SessionFile, token); }
            catch (IOException e) { Debug.LogWarning("[VERA] Không lưu được phiên đăng nhập: " + e.Message); }
        }

        static void ClearSavedToken()
        {
            try { if (File.Exists(SessionFile)) File.Delete(SessionFile); }
            catch (IOException e) { Debug.LogWarning("[VERA] Không xóa được phiên đăng nhập: " + e.Message); }
        }
    }
}
