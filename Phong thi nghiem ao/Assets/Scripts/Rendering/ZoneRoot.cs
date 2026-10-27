using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Gốc chứa đồ tương tác của một khu trong cảnh lõi (lọ, dụng cụ, máy, chỗ đặt, công tắc, đèn bật tắt được).
    /// Khi khu bị gỡ, ZoneStreamer tắt cả nhánh này ("ngủ"): không vẽ, không chạy script, nhưng mọi trạng thái
    /// (vị trí, dung dịch trong bình, nhiệt độ, máy đang bật...) vẫn nằm nguyên trong bộ nhớ, tải lại khu là như cũ.
    /// </summary>
    public class ZoneRoot : MonoBehaviour
    {
        public int zone;

        static readonly ZoneRoot[] byZone = new ZoneRoot[ZoneMap.Zones.Length];
        static readonly List<ZoneRoot> all = new List<ZoneRoot>();

        public static IReadOnlyList<ZoneRoot> All => all;

        /// <summary>Gốc tương tác của khu (null nếu cảnh không chia khu).</summary>
        public static ZoneRoot Of(int zone) => zone >= 0 && zone < byZone.Length ? byZone[zone] : null;

        /// <summary>Đang ngủ (khu đã gỡ).</summary>
        public bool Parked => !gameObject.activeSelf;

        void Awake() => Register();

        /// <summary>Đăng ký cả khi gốc bị tắt sẵn (Awake không chạy với vật tắt): ZoneStreamer gọi khi khởi động.</summary>
        public void Register()
        {
            if (zone >= 0 && zone < byZone.Length) byZone[zone] = this;
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDestroy()
        {
            all.Remove(this);
            if (zone >= 0 && zone < byZone.Length && byZone[zone] == this) byZone[zone] = null;
        }

        /// <summary>
        /// Vật này có "còn sống" không: đang bật, hoặc chỉ tắt vì khu của nó đang ngủ (mọi nút từ vật lên tới gốc khu đều bật).
        /// Dùng khi robot, tra cứu cần biết cả đồ ở khu đã gỡ.
        /// </summary>
        public static bool Alive(GameObject go)
        {
            if (go == null) return false;
            if (go.activeInHierarchy) return true;
            for (var t = go.transform; t != null; t = t.parent)
            {
                var root = t.GetComponent<ZoneRoot>();
                if (root != null) return true;  // tới gốc khu: phía dưới đều bật, chỉ gốc tắt vì ngủ
                if (!t.gameObject.activeSelf) return false;
            }
            return false;
        }

        /// <summary>Nhánh không thuộc tòa nhà (xưởng lắp ráp ảo của khu chế tạo): FindAll bỏ qua.</summary>
        public static readonly List<Transform> Excluded = new List<Transform>();

        /// <summary>Mọi T trong cảnh, kể cả ở khu đang ngủ (bỏ qua vật bị tắt vì lý do khác và nhánh Excluded).</summary>
        public static T[] FindAll<T>() where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            var list = new List<T>(found.Length);
            foreach (var c in found)
            {
                if (!Alive(c.gameObject)) continue;
                bool skip = false;
                foreach (var e in Excluded) if (e != null && c.transform.IsChildOf(e)) { skip = true; break; }
                if (!skip) list.Add(c);
            }
            return list.ToArray();
        }
    }
}
