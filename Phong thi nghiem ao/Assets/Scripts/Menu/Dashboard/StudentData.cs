using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Khkt.Cache;
using UnityEngine;

namespace Khkt.Menu.Dashboard
{
    /// <summary>Một dòng của GET /api/toi/ket-qua. Điểm chưa chấm được đổi thành -1 lúc đọc, xem StudentData.Parse.</summary>
    [Serializable]
    public class StudentResult
    {
        public int id;
        public string ma_bai;
        public string ten_bai;
        public double thoi_gian;
        public string tom_tat;
        public float diem;
        public string nhan_xet;

        public bool Graded => diem >= 0f;

        public string DateText => DateTimeOffset.FromUnixTimeSeconds((long)thoi_gian).ToLocalTime().ToString("dd/MM/yyyy");
    }

    /// <summary>Số liệu tổng hợp, chỉ tính từ điểm thật. Bài chưa chấm không được tính vào trung bình.</summary>
    public class StudentStats
    {
        /// <summary>So sánh 3 bài gần nhất với 3 bài ngay trước đó nên cần ít nhất 6 bài đã chấm.</summary>
        public const int GrowthWindow = 3;

        public int done, graded;
        public float average;
        public bool hasGrowth;
        public float growth;

        public static StudentStats From(IReadOnlyList<StudentResult> results)
        {
            var s = new StudentStats { done = results.Count };
            var scores = new List<StudentResult>();
            foreach (var r in results)
                if (r.Graded) scores.Add(r);
            scores.Sort((a, b) => a.thoi_gian.CompareTo(b.thoi_gian));
            s.graded = scores.Count;
            if (s.graded == 0) return s;

            float sum = 0f;
            foreach (var r in scores) sum += r.diem;
            s.average = sum / s.graded;

            if (s.graded >= GrowthWindow * 2)
            {
                float recent = 0f, before = 0f;
                for (int i = 0; i < GrowthWindow; i++)
                {
                    recent += scores[s.graded - 1 - i].diem;
                    before += scores[s.graded - 1 - GrowthWindow - i].diem;
                }
                s.hasGrowth = true;
                s.growth = (recent - before) / GrowthWindow;
            }
            return s;
        }
    }

    public enum DataState { Empty, Ready, Offline, Expired, Stale }

    /// <summary>
    /// Kết quả thí nghiệm của người đang đăng nhập. Giữ trong bộ nhớ qua các lần đổi cảnh: từ lab về thì hiện ngay
    /// số cũ rồi cập nhật ở nền, không phải chờ.
    /// </summary>
    public static class StudentData
    {
        const string Path = "/api/toi/ket-qua";

        [Serializable]
        class Wrapper
        {
            public StudentResult[] items;
        }

        static readonly Regex NullScore = new Regex("\"diem\"\\s*:\\s*null", RegexOptions.Compiled);

        static int ownerId = -1;

        public static List<StudentResult> Results { get; private set; } = new List<StudentResult>();
        public static StudentStats Stats { get; private set; } = StudentStats.From(new List<StudentResult>());
        public static bool HasData { get; private set; }

        public static void Clear()
        {
            ownerId = -1;
            HasData = false;
            Results = new List<StudentResult>();
            Stats = StudentStats.From(Results);
        }

        /// <summary>Đọc JSON của máy chủ, mới nhất lên đầu. Dữ liệu hỏng trả null chứ không ném lỗi ra giao diện.</summary>
        public static List<StudentResult> Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string body = json.Trim();
            if (!body.StartsWith("[")) return null;
            // JsonUtility không phân biệt được null với 0, mà điểm 0 và chưa chấm là hai chuyện khác nhau
            body = NullScore.Replace(body, "\"diem\":-1");
            try
            {
                var w = JsonUtility.FromJson<Wrapper>("{\"items\":" + body + "}");
                if (w == null || w.items == null) return null;
                var list = new List<StudentResult>(w.items);
                list.Sort((a, b) => b.thoi_gian.CompareTo(a.thoi_gian));
                return list;
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning("[VERA] Dữ liệu kết quả không đọc được: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Lấy kết quả qua ApiCache: có bản lưu thì báo ngay (final = false), rồi báo lần cuối sau khi hỏi máy chủ.
        /// Mất mạng thì giữ số đang có. Tài khoản khác thì bỏ số của người trước.
        /// </summary>
        public static IEnumerator Load(Action<DataState, bool> update)
        {
            var user = GameSession.User;
            if (user == null)
            {
                Clear();
                update(DataState.Empty, true);
                yield break;
            }
            if (ownerId != user.id) Clear();
            ownerId = user.id;

            var final = DataState.Offline;
            yield return ApiCache.Get(Path, 0, r =>
            {
                // đăng xuất hoặc đổi tài khoản trong lúc chờ: câu trả lời này không còn của ai nữa
                if (GameSession.User == null || GameSession.User.id != user.id)
                {
                    // 401 làm mất GameSession.User ngay trong ApiCache, vẫn phải báo là hết phiên
                    final = r.status == 401 ? DataState.Expired : DataState.Stale;
                    return;
                }
                if (r.status == 401)
                {
                    final = DataState.Expired;
                    return;
                }
                if (r.offline || r.json == null)
                {
                    final = DataState.Offline;
                    return;
                }
                var list = Parse(r.json);
                if (list == null)
                {
                    final = DataState.Offline;
                    return;
                }
                Results = list;
                Stats = StudentStats.From(list);
                HasData = true;
                final = DataState.Ready;
                // bản lưu còn phải chờ máy chủ, bản mới thì đây là lần báo cuối ở dưới
                if (r.fromCache) update(DataState.Ready, false);
            });
            update(final, true);
        }
    }
}
