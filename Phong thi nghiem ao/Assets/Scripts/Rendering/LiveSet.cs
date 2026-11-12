using System;
using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Sổ đăng ký các thành phần đang tồn tại trong cảnh (vật chứa, dụng cụ cầm được...).
    ///
    /// Trước đây bếp, máy khuấy, tủ hút, cân, hiệu ứng khí mỗi 0,5 – 2 giây lại gọi FindObjectsByType quét toàn bộ cảnh
    /// (hàng nghìn vật, kể cả khu đang ngủ) rồi cấp phát mảng mới: mỗi lượt quét là một khung hình khựng và thêm rác bộ nhớ.
    /// Thành phần tự ghi tên vào đây lúc Awake, xóa lúc OnDestroy; nơi cần chỉ việc lọc danh sách có sẵn, không quét, không cấp phát.
    ///
    /// Kết quả giống ZoneRoot.FindAll: gồm cả vật ở khu đang ngủ, bỏ vật bị tắt vì lý do khác và nhánh ZoneRoot.Excluded.
    /// (Vật chưa từng được bật thì chưa Awake nên chưa có trong sổ; nó cũng chưa chạy nên không nơi nào cần tới.)
    /// </summary>
    public static class LiveSet<T> where T : Component
    {
        static readonly List<T> items = new List<T>();
        static readonly HashSet<T> index = new HashSet<T>();

        public static int Count => items.Count;

        public static void Add(T item)
        {
            if (item != null && index.Add(item)) items.Add(item);
        }

        public static void Remove(T item)
        {
            if (!ReferenceEquals(item, null) && index.Remove(item)) items.Remove(item);
        }

        /// <summary>
        /// Ghi vào danh sách into (xóa trước) mọi thành phần còn sống thỏa điều kiện. Không cấp phát bộ nhớ.
        /// activeOnly: chỉ lấy vật đang bật thật (bỏ cả vật ở khu đang ngủ).
        /// </summary>
        public static void Collect(List<T> into, Predicate<T> filter = null, bool activeOnly = false)
        {
            into.Clear();
            var excluded = ZoneRoot.Excluded;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var c = items[i];
                if (c == null)
                {
                    // vật đã bị hủy mà không qua OnDestroy (đổi cảnh, tắt Play khi không nạp lại miền): dọn khỏi sổ
                    index.Remove(c);
                    items.RemoveAt(i);
                    continue;
                }
                var go = c.gameObject;
                if (activeOnly ? !go.activeInHierarchy : !ZoneRoot.Alive(go)) continue;
                if (filter != null && !filter(c)) continue;
                bool skip = false;
                for (int e = 0; e < excluded.Count; e++)
                    if (excluded[e] != null && c.transform.IsChildOf(excluded[e])) { skip = true; break; }
                if (!skip) into.Add(c);
            }
        }
    }
}
