using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>Các loại khớp nối của dụng cụ thủy tinh và giá đỡ trong phòng thí nghiệm.</summary>
    public enum PortType
    {
        KhopNhamDuc,     // khớp nhám đực (côn ngoài) 14/23, 19/26, 24/29
        KhopNhamCai,     // khớp nhám cái (côn trong)
        CoBinh,          // miệng bình / ống nghiệm (đậy nút cao su, cắm phễu)
        NutCaoSu,        // thân nút cao su (vừa miệng bình cùng cỡ)
        LoNut,           // lỗ trên nút cao su (cắm ống dẫn thủy tinh)
        DauOngThuyTinh,  // đầu ống dẫn thủy tinh
        DauOngCaoSu,     // đầu ống cao su (lồng vào ống thủy tinh, vòi nhánh)
        VoiNhanh,        // vòi nhánh (bình có nhánh, vòi nước sinh hàn, vòi cấp nước)
        VungKep,         // vùng kẹp trên thân / cổ dụng cụ thủy tinh
        MaKep,           // má kẹp càng cua
        ThanhGia,        // điểm trên cọc giá sắt
        KhopNoiKep,      // khớp nối (đầu kẹp) lồng vào cọc giá sắt
        Vong,            // vòng sắt đỡ phễu, bình
        ChanPheu,        // phần côn của phễu / bình ngồi lên vòng sắt hoặc miệng bình
        DayBinh,         // đáy tròn bình cầu (ngồi lên vòng sắt, bếp bọc)
        MiengRa,         // đầu ra để hở (đầu nhỏ giọt đầu nối hứng, đuôi phễu)
        DayPhang,        // đáy phẳng (cốc, bát sứ, lọ): đặt lên lưới tản nhiệt
        MatLuoi,         // mặt lưới tản nhiệt (đặt cốc, bình lên để đun)
        LoGia,           // lỗ trên giá ống nghiệm
        DayOng,          // đáy ống nghiệm (cắm vào giá)
        GiaUp,           // kệ đỡ ống úp trong chậu nước (úp miệng ống nghiệm / lọ thu khí lên)
    }

    /// <summary>
    /// Một khớp nối trên bộ phận. Gốc của khớp là điểm tiếp xúc; trục z (forward) chỉ ra ngoài theo hướng nối, trục y (up) là
    /// hướng "trên" của khớp để xoay đúng chiều (khớp cứng như kẹp, vòng sắt) hoặc giữ chiều xoay người dùng đang cầm (khớp nhám).
    /// Khi lắp, bộ phận được đặt sao cho hai khớp trùng điểm, hướng ngược nhau.
    /// </summary>
    public class Port : MonoBehaviour
    {
        public PortType type;
        [Tooltip("Cỡ (mm): khớp nhám 14 / 19 / 24 / 29; đường kính miệng, ống, vùng kẹp")]
        public float size;
        [Tooltip("Kênh bên trong bộ phận: các khớp cùng kênh thông nhau (sinh hàn: 0 đường hơi, 1 áo nước); -1: không dẫn chất")]
        public int channel;
        public string label = "Khớp";
        [Tooltip("Nhóm loại trừ: các khớp cùng nhóm (ví dụ 4 hướng kẹp ở cùng một độ cao trên cọc giá) chỉ một khớp được dùng. -1: không")]
        public int exclusiveGroup = -1;

        [SerializeField, HideInInspector] Port connectedTo;  // lưu vào cảnh: bộ lắp sẵn khi dựng, lưu / mở buổi thí nghiệm
        ApparatusPart part;

        public ApparatusPart Part { get => part != null ? part : (part = GetComponentInParent<ApparatusPart>(true)); internal set => part = value; }
        public Port Connected { get => connectedTo; internal set => connectedTo = value; }
        public bool Free => Connected == null && (exclusiveGroup < 0 || Part == null || !Part.GroupBusy(exclusiveGroup));

        /// <summary>Khớp còn trống này có phải lối thông ra không khí không (miệng bình, đầu ống, vòi nhánh, khớp nhám để hở).</summary>
        public bool IsOpening => Free && channel >= 0 && IsOpeningType(type);

        static bool IsOpeningType(PortType t) =>
            t == PortType.KhopNhamDuc || t == PortType.KhopNhamCai || t == PortType.CoBinh || t == PortType.LoNut ||
            t == PortType.DauOngThuyTinh || t == PortType.DauOngCaoSu || t == PortType.VoiNhanh || t == PortType.MiengRa;

        /// <summary>Khớp cứng: khi lắp, hướng "trên" của bộ phận theo hướng của khớp đích (kẹp, giá, vòng). Khớp khác giữ chiều xoay đang cầm.</summary>
        public static bool FixedRoll(PortType t) =>
            t == PortType.MaKep || t == PortType.VungKep || t == PortType.ThanhGia || t == PortType.KhopNoiKep ||
            t == PortType.Vong || t == PortType.ChanPheu || t == PortType.DayBinh || t == PortType.DayPhang || t == PortType.MatLuoi ||
            t == PortType.LoGia || t == PortType.DayOng || t == PortType.GiaUp;

        /// <summary>Bảng tương thích: hai khớp có lắp được với nhau không (loại và cỡ).</summary>
        public static bool Compatible(Port a, Port b)
        {
            if (a == null || b == null || a.Part == b.Part) return false;
            return Pair(a.type, a.size, b.type, b.size) || Pair(b.type, b.size, a.type, a.size);
        }

        static bool Pair(PortType a, float sa, PortType b, float sb)
        {
            switch (a)
            {
                case PortType.KhopNhamDuc: return b == PortType.KhopNhamCai && Mathf.Abs(sa - sb) < 0.5f;
                case PortType.CoBinh:
                    if (b == PortType.NutCaoSu) return Mathf.Abs(sa - sb) <= 4f;
                    if (b == PortType.ChanPheu) return sb < sa + 30f;  // đuôi phễu cắm vào miệng bình
                    return false;
                case PortType.LoNut: return b == PortType.DauOngThuyTinh && sb >= 4f && sb <= 9f;
                case PortType.DauOngCaoSu: return (b == PortType.DauOngThuyTinh || b == PortType.VoiNhanh) && sb >= sa - 1.5f && sb <= sa + 3f;
                case PortType.MaKep: return b == PortType.VungKep && sb >= 8f && sb <= 75f;
                case PortType.ThanhGia: return b == PortType.KhopNoiKep;
                case PortType.Vong: return (b == PortType.ChanPheu || b == PortType.DayBinh) && sb >= sa * 0.5f && sb <= sa * 2.2f;
                case PortType.MatLuoi: return (b == PortType.DayPhang || b == PortType.DayBinh) && sb <= sa;
                case PortType.LoGia: return b == PortType.DayOng && sb <= sa;
                case PortType.GiaUp: return b == PortType.CoBinh && sb <= sa;
                default: return false;
            }
        }

        public static string TypeName(PortType t)
        {
            switch (t)
            {
                case PortType.KhopNhamDuc: return "khớp nhám đực";
                case PortType.KhopNhamCai: return "khớp nhám cái";
                case PortType.CoBinh: return "miệng bình";
                case PortType.NutCaoSu: return "nút cao su";
                case PortType.LoNut: return "lỗ nút";
                case PortType.DauOngThuyTinh: return "đầu ống thủy tinh";
                case PortType.DauOngCaoSu: return "đầu ống cao su";
                case PortType.VoiNhanh: return "vòi nhánh";
                case PortType.VungKep: return "vùng kẹp";
                case PortType.MaKep: return "má kẹp";
                case PortType.ThanhGia: return "cọc giá sắt";
                case PortType.KhopNoiKep: return "khớp nối kẹp";
                case PortType.Vong: return "vòng sắt";
                case PortType.ChanPheu: return "chân phễu";
                case PortType.DayBinh: return "đáy bình";
                case PortType.DayPhang: return "đáy phẳng";
                case PortType.MatLuoi: return "mặt lưới";
                case PortType.LoGia: return "lỗ giá";
                case PortType.DayOng: return "đáy ống";
                case PortType.GiaUp: return "kệ úp ống";
                default: return "đầu ra";
            }
        }

        /// <summary>Mô tả ngắn: "khớp nhám cái 24/29", "miệng bình Ø29".</summary>
        public string Describe()
        {
            string n = TypeName(type);
            if (type == PortType.KhopNhamDuc || type == PortType.KhopNhamCai)
                return $"{n} {size:0}/{(size >= 29 ? 32 : size >= 24 ? 29 : size >= 19 ? 26 : 23)}";
            return size > 0 && type != PortType.ThanhGia && type != PortType.KhopNoiKep ? $"{n} Ø{size:0}" : n;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Free ? new Color(0.2f, 1f, 0.6f) : new Color(1f, 0.6f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 0.01f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.04f);
        }
    }
}
