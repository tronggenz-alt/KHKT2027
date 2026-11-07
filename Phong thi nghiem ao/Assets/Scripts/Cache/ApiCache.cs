using System;
using System.Collections;
using System.Text;
using Khkt.Menu;
using UnityEngine;
using UnityEngine.Networking;

namespace Khkt.Cache
{
    public sealed class CacheResult
    {
        /// <summary>Nội dung trả về; null khi không có cả dữ liệu lưu lẫn dữ liệu mới.</summary>
        public string json;
        /// <summary>true nếu json lấy từ đĩa, false nếu vừa tải từ máy chủ.</summary>
        public bool fromCache;
        /// <summary>Số giây kể từ lúc dữ liệu được tải; -1 khi không có dữ liệu.</summary>
        public long ageSec = -1;
        /// <summary>Đã thử gọi máy chủ mà không được (mất mạng, hết giờ, máy chủ lỗi 5xx).</summary>
        public bool offline;
        /// <summary>Mã HTTP của lần gọi; 0 khi chưa gọi hoặc không kết nối được.</summary>
        public long status;
    }

    /// <summary>
    /// Đọc API kiểu hiện dữ liệu cũ ngay, tải mới ở nền.
    /// onData được gọi tối đa hai lần:
    ///   lần 1: có dữ liệu đã lưu thì trả ngay (fromCache = true);
    ///   lần 2: sau khi gọi máy chủ, với dữ liệu mới, hoặc với dữ liệu cũ kèm offline = true nếu không gọi được.
    /// Dữ liệu còn mới hơn maxAgeSec thì không gọi máy chủ, chỉ có lần 1. maxAgeSec = 0 là luôn gọi.
    /// Không có dữ liệu lưu và không gọi được: một lần với json = null, offline = true.
    /// </summary>
    public static class ApiCache
    {
        const int TimeoutSec = 6;

        public static IEnumerator Get(string path, int maxAgeSec, Action<CacheResult> onData)
        {
            string key = GameSession.ServerUrl + path;
            string folder = CacheStore.CurrentUserFolder;  // cố định từ đầu: đăng xuất giữa chừng không ghi nhầm sang người khác
            string cached = null;
            long age = -1;
            if (CacheStore.TryRead(key, out byte[] raw, out age, folder))
            {
                cached = Encoding.UTF8.GetString(raw);
                Deliver(onData, new CacheResult { json = cached, fromCache = true, ageSec = age });
                if (age < maxAgeSec) yield break;
            }

            using (var req = UnityWebRequest.Get(key))
            {
                req.timeout = TimeoutSec;
                AccountClient.Authorize(req);
                yield return req.SendWebRequest();
                AccountClient.CheckExpired(req);

                long code = req.responseCode;
                bool ok = req.result == UnityWebRequest.Result.Success && code >= 200 && code < 300;
                if (ok)
                {
                    string text = req.downloadHandler.text;
                    // Đăng xuất trong lúc chờ mạng thì thư mục đã bị xóa: không ghi lại dữ liệu cá nhân.
                    if (folder == CacheStore.CurrentUserFolder) CacheStore.Write(key, Encoding.UTF8.GetBytes(text), folder);
                    Deliver(onData, new CacheResult { json = text, fromCache = false, ageSec = 0, status = code });
                    yield break;
                }

                if (code == 401 || code == 403 || code == 404)
                {
                    // Máy chủ trả lời rõ là không còn quyền hoặc không còn dữ liệu: bỏ bản lưu, không hiện lại nữa.
                    CacheStore.Remove(key, folder);
                    Deliver(onData, new CacheResult { status = code });
                    yield break;
                }

                // Mất mạng, hết giờ, máy chủ lỗi: giữ nguyên bản lưu.
                Deliver(onData, new CacheResult { json = cached, fromCache = cached != null, ageSec = age, offline = true, status = code });
            }
        }

        /// <summary>Xóa dữ liệu đã lưu của tài khoản đang đăng nhập. Gọi khi đăng xuất.</summary>
        public static void ClearUser() => CacheStore.ClearUser();

        static void Deliver(Action<CacheResult> onData, CacheResult result)
        {
            // Lỗi ở phía hiển thị không được làm dừng việc lưu và lần gọi sau
            try { onData?.Invoke(result); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
