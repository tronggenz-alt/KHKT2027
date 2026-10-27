using System;
using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Demo
{
    [Serializable]
    public class SceneObject
    {
        public string id, ten, loai, chua;
        public float khoang_cach;
    }

    /// <summary>Ảnh chụp cảnh dạng chữ gửi cho AI (AI không nhìn thấy 3D, chỉ biết danh sách này).</summary>
    [Serializable]
    public class SceneSnapshot
    {
        public string khu_vuc, nguoi_dang_cam, robot_dang_cam, che_do;
        public SceneObject[] vat;
        public string[] su_kien;
    }

    /// <summary>Một bước kế hoạch: kỹ năng + mã vật (máy chủ đã kiểm tra).</summary>
    [Serializable]
    public class RobotStep
    {
        public string ky_nang, doi_tuong, dich, loi;
        public float so_luong;
        public int stt;
    }

    /// <summary>
    /// Chụp cảnh quanh người dùng thành danh sách vật có mã (ong_nghiem_1, coc_2, hoa_chat:253, den_con_1...),
    /// và giữ bảng mã → vật thật để robot thực hiện kế hoạch tham chiếu theo mã.
    /// </summary>
    public static class RobotWorld
    {
        public static readonly Dictionary<string, Component> Ids = new Dictionary<string, Component>();

        static string Kind(Grabbable g)
        {
            switch (g.kind)
            {
                case "ong_nghiem": return "ống nghiệm";
                case "coc": return "cốc";
                case "binh_non": return "bình nón";
                case "binh_cau": return "bình cầu";
                case "cuvet": return "cuvet";
                case "buret": return "buret";
                case "pipet": return "pipet";
                case "lo_nho_giot": return "lọ nhỏ giọt";
                case "binh_tia": return "bình tia nước cất";
                case "dau_do": return "dụng cụ đo";
                case "giay": return "giấy thử";
                default: return "dụng cụ";
            }
        }

        public static SceneSnapshot Capture(Transform user, HandController hand, LabProcedure proc, float radius = 3.5f)
        {
            Ids.Clear();
            var list = new List<(float d, SceneObject o, Component c)>();
            Vector3 u = user.position;
            float Dist(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(u.x, u.z));
            var counters = new Dictionary<string, int>();
            string Next(string prefix)
            {
                counters.TryGetValue(prefix, out int n);
                counters[prefix] = ++n;
                return prefix + "_" + n;
            }

            var robot = LabRobot.Instance;
            var grabs = new List<Grabbable>();
            foreach (var g in Khkt.Rendering.ZoneRoot.FindAll<Grabbable>())
                if (Dist(g.transform.position) <= radius || (robot != null && robot.Holding == g) || (hand != null && hand.Held == g)) grabs.Add(g);
            grabs.Sort((a, b) => Dist(a.transform.position).CompareTo(Dist(b.transform.position)));
            foreach (var g in grabs)
            {
                var cb = g.GetComponent<ChemicalBottle>();
                var lc = g.GetComponent<LabContainer>();
                string id, ten, loai;
                if (cb != null && cb.Entry != null)
                {
                    id = "hoa_chat:" + cb.stt;
                    if (Ids.ContainsKey(id)) continue;
                    ten = cb.Entry.ten + (string.IsNullOrEmpty(cb.Entry.cong_thuc) ? "" : $" ({cb.Entry.cong_thuc})");
                    loai = cb.IsSolid ? "lọ hóa chất rắn" : cb.IsGas ? "bình khí" : cb.IsPaper ? "giấy thử" : g.GetComponent<Dropper>() != null ? "lọ nhỏ giọt" : "chai hóa chất";
                }
                else
                {
                    id = Next(g.kind);
                    ten = lc != null ? lc.label : g.name;
                    loai = Kind(g) + (lc != null ? $", chứa tối đa {lc.capacityMl:0} ml" : "");
                }
                string chua = lc != null ? (cb != null && cb.IsSolid ? $"còn {cb.GramsLeft:0} g" : lc.Total.Describe(4)) : "";
                if (lc != null && lc.reactive && lc.VolumeMl > 0.05f) chua += $", pH {lc.BulkPh:0.0}, {lc.TemperatureC:0} °C";
                if (hand != null && hand.Held == g) loai += ", người dùng đang cầm";
                if (robot != null && robot.Holding == g) loai += ", robot đang cầm";
                list.Add((Dist(g.transform.position), new SceneObject { id = id, ten = ten, loai = loai, chua = chua }, g));
            }
            foreach (var t in Khkt.Rendering.ZoneRoot.FindAll<ToggleEffect>())
            {
                float d = Dist(t.transform.position);
                if (d > radius) continue;
                var info = t.GetComponentInParent<EquipmentInfo>();
                list.Add((d, new SceneObject { id = Next("thiet_bi"), ten = info != null ? info.title : t.name, loai = "thiết bị bật/tắt", chua = t.isOn ? "đang bật" : "đang tắt" }, t));
            }
            foreach (var a in Khkt.Rendering.ZoneRoot.FindAll<AnalyzerInstrument>())
            {
                float d = Dist(a.transform.position);
                if (d > radius + 1) continue;
                list.Add((d, new SceneObject { id = Next("may"), ten = AnalyzerInstrument.KindName(a.kind), loai = "máy phân tích (bat_tat = chạy phân tích mẫu trong ổ)", chua = a.LastReport }, a));
            }
            foreach (var s in Khkt.Rendering.ZoneRoot.FindAll<PlaceSocket>())
            {
                float d = Dist(s.transform.position);
                if (d > radius) continue;
                list.Add((d, new SceneObject { id = Next("cho_dat"), ten = s.label, loai = "chỗ đặt nhận: " + string.Join(", ", s.accepts), chua = s.Occupant != null ? "đang có vật" : "trống" }, s));
            }
            list.Sort((a, b) => a.d.CompareTo(b.d));
            var objs = new List<SceneObject>();
            foreach (var (d, o, c) in list)
            {
                if (objs.Count >= 60) break;
                o.khoang_cach = Mathf.Round(d * 10) / 10f;
                objs.Add(o);
                Ids[o.id] = c;
            }

            string area = "phòng thí nghiệm";
            var viewer = user.GetComponentInParent<DesktopViewer>();
            if (viewer != null && viewer.Stations != null)
            {
                float best = 4f;
                foreach (var st in viewer.Stations)
                {
                    float d = Dist(st.transform.position);
                    if (d < best) { best = d; area = st.displayName; }
                }
            }
            if (u.z < 0) area = u.x < 13.5f ? "phòng máy phân tích hiện đại" : "phòng tự động hóa & robot";
            if (u.x > 20 && u.z > 0) area = "phòng hóa chất";
            var ann = ReactionAnnotator.Instance;
            return new SceneSnapshot
            {
                khu_vuc = area,
                nguoi_dang_cam = hand != null && hand.Held != null ? Describe(hand.Held) : "",
                robot_dang_cam = robot != null && robot.Holding != null ? Describe(robot.Holding) : "",
                che_do = proc != null && proc.Active ? $"bài chuẩn độ {proc.Cfg.analyteShort} bằng {proc.Cfg.titrantShort}, bước {proc.Current}" : "thí nghiệm tự do",
                vat = objs.ToArray(),
                su_kien = ann != null ? ann.History.ToArray() : new string[0],
            };
        }

        static string Describe(Grabbable g)
        {
            var cb = g.GetComponent<ChemicalBottle>();
            if (cb != null && cb.Entry != null) return cb.Entry.ten;
            var lc = g.GetComponent<LabContainer>();
            return lc != null ? lc.label : g.name;
        }

        /// <summary>Tìm vật theo mã (mã hoa_chat:stt không có trong ảnh chụp thì tìm lọ gần nhất trong cảnh).</summary>
        public static Component Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Ids.TryGetValue(id, out var c) && c != null) return c;
            if (id.StartsWith("hoa_chat:") && int.TryParse(id.Substring(9), out int stt))
            {
                ChemicalBottle best = null;
                float bd = float.MaxValue;
                var robot = LabRobot.Instance;
                Vector3 p = robot != null ? robot.transform.position : Vector3.zero;
                foreach (var b in Khkt.Rendering.ZoneRoot.FindAll<ChemicalBottle>())
                {
                    if (b.stt != stt) continue;
                    float d = Vector3.Distance(b.transform.position, p);
                    if (d < bd) { bd = d; best = b; }
                }
                return best;
            }
            return null;
        }
    }
}
