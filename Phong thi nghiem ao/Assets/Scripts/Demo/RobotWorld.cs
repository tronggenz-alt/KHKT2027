using System;
using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
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

            // Hai tầng trùng mặt bằng (lab sinh nằm ngay trên lab hóa): chỉ lấy vật cùng tầng với người dùng.
            // u có thể là vị trí chân hoặc mắt: chân tầng 2 ở 3,8 m, mắt tầng 1 cao nhất 1,7 m.
            int userFloor = u.y >= 3f ? 1 : 0;
            bool Near(Vector3 p, float extra = 0) => NavGrid.FloorOf(p) == userFloor && Dist(p) <= radius + extra;

            var robot = LabRobot.Instance;
            var grabs = new List<Grabbable>();
            // Lab sinh: kẹp, dao, sinh phẩm nằm ở bàn khác và giá sinh phẩm cách xa vài mét, robot tự đi lấy được, nên tầm chụp
            // đồ sinh học rộng hơn (12 m) và mỗi loại chỉ ghi cái gần nhất (xem DedupeKey).
            float bioExtra = userFloor == 1 ? Mathf.Max(0f, 12f - radius) : 0f;
            foreach (var g in Khkt.Rendering.ZoneRoot.FindAll<Grabbable>())
                if (Near(g.transform.position) || (bioExtra > 0 && IsBio(g) && Near(g.transform.position, bioExtra))
                    || (robot != null && robot.Holding == g) || (hand != null && hand.Held == g)) grabs.Add(g);
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
                else if (IsBio(g))
                {
                    // dụng cụ, bình, mẫu sinh học tầng 2: robot dùng bằng kỹ năng dung_dung_cu (dụng cụ → vật đích)
                    BioEntry(g, out string prefix, out ten, out loai, out string bioChua);
                    id = Next(prefix);
                    if (hand != null && hand.Held == g) loai += ", người dùng đang cầm";
                    if (robot != null && robot.Holding == g) loai += ", robot đang cầm";
                    list.Add((Dist(g.transform.position), new SceneObject { id = id, ten = ten, loai = loai, chua = bioChua }, g));
                    continue;
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
            if (userFloor == 1) AddBioFixtures(list, Near, Dist, Next, bioExtra);
            foreach (var t in Khkt.Rendering.ZoneRoot.FindAll<ToggleEffect>())
            {
                float d = Dist(t.transform.position);
                if (!Near(t.transform.position)) continue;
                var info = t.GetComponentInParent<EquipmentInfo>();
                list.Add((d, new SceneObject { id = Next("thiet_bi"), ten = info != null ? info.title : t.name, loai = "thiết bị bật/tắt", chua = t.isOn ? "đang bật" : "đang tắt" }, t));
            }
            foreach (var a in Khkt.Rendering.ZoneRoot.FindAll<AnalyzerInstrument>())
            {
                float d = Dist(a.transform.position);
                if (!Near(a.transform.position, 1f)) continue;
                list.Add((d, new SceneObject { id = Next("may"), ten = AnalyzerInstrument.KindName(a.kind), loai = "máy phân tích (bat_tat = chạy phân tích mẫu trong ổ)", chua = a.LastReport }, a));
            }
            foreach (var s in Khkt.Rendering.ZoneRoot.FindAll<PlaceSocket>())
            {
                float d = Dist(s.transform.position);
                if (!Near(s.transform.position)) continue;
                list.Add((d, new SceneObject { id = Next("cho_dat"), ten = s.label, loai = "chỗ đặt nhận: " + string.Join(", ", s.accepts), chua = s.Occupant != null ? "đang có vật" : "trống" }, s));
            }
            list.Sort((a, b) => a.d.CompareTo(b.d));
            var objs = new List<SceneObject>();
            var seenKeys = new HashSet<string>();
            foreach (var (d, o, c) in list)
            {
                if (objs.Count >= MaxObjects) break;
                if (userFloor == 1)
                {
                    string key = DedupeKey(c);
                    if (key != null && !seenKeys.Add(key)) continue;  // đã có một cái cùng loại ở gần hơn
                }
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
            if (userFloor == 1)
            {
                int z = Khkt.Rendering.ZoneMap.At(new Vector3(u.x, NavGrid.Floor2Y + 1f, u.z));
                area = (z >= 0 ? Khkt.Rendering.ZoneMap.Name(z) : "khu sinh học") + " (tầng 2, khu sinh học)";
            }
            string mode = proc != null && proc.Active ? $"bài chuẩn độ {proc.Cfg.analyteShort} bằng {proc.Cfg.titrantShort}, bước {proc.Current}" : "thí nghiệm tự do";
            if (userFloor == 1) mode = "thực hành sinh học tự do: dùng dung_dung_cu (dụng cụ → vật đích), lay_dung_cu, chinh_num, bat_tat, quan_sat";
            var ann = ReactionAnnotator.Instance;
            return new SceneSnapshot
            {
                khu_vuc = area,
                nguoi_dang_cam = hand != null && hand.Held != null ? Describe(hand.Held) : "",
                robot_dang_cam = robot != null && robot.Holding != null ? Describe(robot.Holding) : "",
                che_do = mode,
                vat = objs.ToArray(),
                su_kien = ann != null ? ann.History.ToArray() : new string[0],
            };
        }

        // ------------------------------------------------------------------
        // Vật sinh học tầng 2
        // ------------------------------------------------------------------

        /// <summary>Số vật tối đa trong ảnh chụp cảnh (máy chủ cũng cắt ở mức này).</summary>
        public const int MaxObjects = 80;

        static bool IsBio(Grabbable g) =>
            g.GetComponent<BioVessel>() != null || g.GetComponent<BioTool>() != null || g.GetComponent<IBioTarget>() != null || g.GetComponent<BioSample>() != null;

        static string Short(string s, int max = 110) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max) + "…";

        static string Hint(IDemoUsable u)
        {
            try { return Short(u.UseHint); } catch { return ""; }
        }

        /// <summary>Tên, loại, trạng thái của một dụng cụ / bình / mẫu sinh học cầm được.</summary>
        static void BioEntry(Grabbable g, out string prefix, out string ten, out string loai, out string chua)
        {
            var v = g.GetComponent<BioVessel>();
            var tool = g.GetComponent<BioTool>();
            var info = g.GetComponent<EquipmentInfo>();
            if (v != null)
            {
                prefix = v.isStock ? "sinh_pham" : "binh_sinh";
                ten = v.label;
                loai = v.isStock ? "chai / khay sinh phẩm gốc (chỉ lấy ra: cầm bình khác hoặc kẹp, ống nhỏ giọt rồi dung_dung_cu lên nó)"
                                 : $"bình sinh học, chứa tối đa {v.capacityMl:0} ml (cầm lên rồi dung_dung_cu để rót; cũng là vật đích)";
                chua = v.isStock ? $"còn {v.mix.Of(v.stockId):0.#} {v.unit}" : v.mix.IsEmpty ? "trống" : Short($"{v.mix.Describe()}, {v.TemperatureC:0} °C");
                return;
            }
            bool target = g.GetComponent<IBioTarget>() != null;
            prefix = tool != null ? "dung_cu_sinh" : "mau_sinh";
            ten = tool != null ? tool.displayName : info != null && !string.IsNullOrEmpty(info.title) ? info.title : g.name;
            loai = tool != null ? "dụng cụ sinh học cầm tay (doi_tuong của dung_dung_cu)" + (target ? ", cũng là vật đích" : "")
                                : "mẫu / vật sinh học" + (target ? " (vật đích của dung_dung_cu)" : "");
            var sample = g.GetComponent<BioSample>();
            chua = tool != null ? Short(tool.GetStatusSummary()) : sample != null ? Short(sample.DescribeSummary()) : "";
        }

        /// <summary>
        /// Đồ sinh học không cầm lên được quanh người dùng: chai / khay sinh phẩm đặt cố định, hộp cấp dụng cụ, núm chỉnh, nút bấm,
        /// nắp thiết bị, máy (kính hiển vi, tủ ấm, bể ổn nhiệt, máy PCR, bể điện di...). Mỗi vật một mã để AI gọi kỹ năng.
        /// </summary>
        /// <summary>
        /// Khóa gộp trùng trong ảnh chụp cảnh tầng 2: chai sinh phẩm cùng loại, bình trống cùng tên, hộp cấp cùng loại, dụng cụ
        /// cầm tay cùng tên (chưa mang mẫu) chỉ ghi cái gần người dùng nhất. null: luôn ghi (bình đang chứa mẫu, thiết bị...).
        /// </summary>
        static string DedupeKey(Component c)
        {
            if (c is BioDispenser disp) return "h:" + disp.itemName;
            var v = c.GetComponent<BioVessel>();
            if (v != null) return v.isStock ? "s:" + v.stockId : v.mix.IsEmpty ? "e:" + v.label : null;
            var tool = c.GetComponent<BioTool>();
            if (tool != null && c is Grabbable) return string.IsNullOrEmpty(tool.reagentOrSampleId) ? "t:" + tool.displayName : null;
            return null;
        }

        static void AddBioFixtures(List<(float d, SceneObject o, Component c)> list, Func<Vector3, float, bool> near, Func<Vector3, float> dist, Func<string, string> next, float farExtra)
        {
            var seen = new HashSet<GameObject>();
            foreach (var mb in Khkt.Rendering.ZoneRoot.FindAll<MonoBehaviour>())
            {
                if (mb == null || !mb.enabled) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (!ns.StartsWith("Khkt.Demo.Bio") && !ns.StartsWith("Khkt.Interaction.Bio")) continue;
                Vector3 p = mb.transform.position;
                bool far = mb is BioVessel || mb is BioDispenser;  // sinh phẩm, hộp cấp: robot đi lấy được nên chụp xa hơn
                if (!near(p, far ? farExtra : 0f) || mb.GetComponentInParent<Grabbable>() != null || seen.Contains(mb.gameObject)) continue;
                SceneObject o = null;
                switch (mb)
                {
                    case BioVessel v:
                        o = new SceneObject { id = next(v.isStock ? "sinh_pham" : "binh_sinh"), ten = v.label,
                            loai = v.isStock ? "chai / khay sinh phẩm gốc đặt cố định (vật đích của dung_dung_cu để lấy ra)" : "bình sinh học đặt cố định (vật đích của dung_dung_cu)",
                            chua = v.isStock ? $"còn {v.mix.Of(v.stockId):0.#} {v.unit}" : v.mix.IsEmpty ? "trống" : Short(v.mix.Describe()) };
                        break;
                    case BioDispenser disp:
                        o = new SceneObject { id = next("hop_cap"), ten = "Hộp cấp " + disp.itemName, loai = "hộp cấp dụng cụ mới (kỹ năng lay_dung_cu)", chua = $"còn {disp.remaining}" };
                        break;
                    case BioKnob knob:
                        o = new SceneObject { id = next("num"), ten = knob.label, loai = $"núm chỉnh từ {knob.minValue:0.##} tới {knob.maxValue:0.##} {knob.unit} (kỹ năng chinh_num, so_luong = giá trị)", chua = knob.FormattedValue() };
                        break;
                    case BioButton button:
                        o = new SceneObject { id = next("nut"), ten = button.label, loai = "nút bấm (kỹ năng bat_tat)", chua = button.locked ? "đang khóa" : button.isToggle ? (button.isOn ? "đang bật" : "đang tắt") : "" };
                        break;
                    case BioHinge hinge:
                        o = new SceneObject { id = next("nap"), ten = hinge.label, loai = "nắp / cửa thiết bị (kỹ năng bat_tat để mở hoặc đóng)", chua = Hint(hinge) };
                        break;
                    default:
                        bool isTarget = mb is IBioTarget, isUsable = mb is IDemoUsable;
                        if (!isTarget && !isUsable) continue;
                        var info = mb.GetComponentInParent<EquipmentInfo>();
                        string name = isTarget ? ((IBioTarget)mb).TargetName : "";
                        if (string.IsNullOrEmpty(name)) name = info != null && !string.IsNullOrEmpty(info.title) ? info.title : mb.name;
                        o = new SceneObject { id = next("thiet_bi_sinh"), ten = name,
                            loai = "thiết bị sinh học" + (isTarget ? ", nhận dụng cụ (vật đích của dung_dung_cu)" : "") + (isUsable ? ", bấm được (bat_tat)" : ""),
                            chua = isUsable ? Hint((IDemoUsable)mb) : "" };
                        break;
                }
                seen.Add(mb.gameObject);
                list.Add((dist(p), o, mb));
            }
        }

        static string Describe(Grabbable g)
        {
            var bv = g.GetComponent<BioVessel>();
            if (bv != null) return bv.label;
            var bt = g.GetComponent<BioTool>();
            if (bt != null) return bt.displayName;
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
