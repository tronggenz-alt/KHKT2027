using System;
using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Kho bộ phận mẫu của khu chế tạo: mỗi loại bộ phận có một bản mẫu tắt sẵn (trình dựng cảnh tạo), con của bản mẫu là
    /// các bộ phận (ống cao su có hai: đầu A, đầu B). Lắp trong không gian ảo hay gia công ra bàn đều nhân bản từ đây, nên
    /// phòng lab không phải chứa sẵn hàng chục bộ phận rời.
    /// </summary>
    public class PartLibrary : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public string name;
            public string group;
            [Tooltip("Một dòng giới thiệu trên ô của bảng bộ phận")]
            public string hint;
            [Tooltip("Bản mẫu (tắt sẵn): con trực tiếp là các bộ phận, đặt ở tư thế nằm trên mặt bàn")]
            public GameObject template;
            [NonSerialized] public RenderTexture thumbnail;
        }

        /// <summary>Nhóm hiện trên các thẻ của bảng bộ phận.</summary>
        public static readonly string[] Groups = { "Giá đỡ & đun", "Bình & ống chứa", "Chưng cất", "Phễu & lọc", "Khí", "Ống & nút" };

        public Entry[] entries = new Entry[0];

        public static PartLibrary Instance { get; private set; }

        void Awake() => Instance = this;

        public Entry Get(string id) => Array.Find(entries, e => e.id == id);

        public IEnumerable<Entry> InGroup(string group)
        {
            foreach (var e in entries) if (e.group == group) yield return e;
        }

        /// <summary>
        /// Nhân bản một mẫu: các bộ phận thành con của parent, đặt ở pos (gốc bản mẫu) xoay yaw độ quanh trục đứng.
        /// Trả về các bộ phận theo thứ tự librarySub, kèm tư thế nghỉ (so với gốc bản mẫu) để đặt lại xuống bàn.
        /// </summary>
        public List<(ApparatusPart part, Vector3 restPos, Quaternion restRot)> Spawn(Entry e, Transform parent, Vector3 pos, float yaw)
        {
            var result = new List<(ApparatusPart, Vector3, Quaternion)>();
            if (e == null || e.template == null) return result;
            var go = Instantiate(e.template, parent);
            go.name = e.template.name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one;
            var kids = new List<Transform>();
            foreach (Transform c in go.transform) kids.Add(c);
            go.SetActive(true);
            foreach (var k in kids)
            {
                var p = k.GetComponent<ApparatusPart>();
                Vector3 rp = k.localPosition;
                Quaternion rr = k.localRotation;
                k.SetParent(parent, true);
                if (p != null) result.Add((p, rp, rr));
            }
            Destroy(go);
            result.Sort((a, b) => a.Item1.librarySub.CompareTo(b.Item1.librarySub));
            return result;
        }
    }
}
