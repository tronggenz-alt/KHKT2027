using System;
using System.Collections.Generic;
using System.Linq;
using Khkt.Demo;
using Khkt.Interaction;
using Khkt.Simulation;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Mạng dòng chảy của các bộ dụng cụ đã lắp (giai đoạn C3), chạy 10 lần mỗi giây trên cây lắp ráp và các khớp nối:
    ///  • Khí: khí sinh ra trong bình đi theo các khớp cùng kênh dẫn chất tới lối thoát; qua bình rửa khí (vào ống dài) thì
    ///    khí tan / phản ứng được bị giữ lại; đầu ống ngập trong dung dịch thì sục vào (CO₂ + nước vôi trong → CaCO₃↓);
    ///    vào kệ chậu nước có ống úp thì đẩy nước, dồn vào ống; đầu ống trong lọ ngửa thì thu bằng đẩy không khí (khí
    ///    nặng hơn không khí); hệ kín thì áp suất tăng, quá ngưỡng thì nút bật ra. Khí độc thoát ngoài tủ hút, khí dễ
    ///    cháy gần ngọn lửa: cảnh báo.
    ///  • Hơi: bình đang sôi hóa hơi theo định luật Raoult (Volatility); hơi đi tới ống sinh hàn: có nước làm lạnh (ống cao
    ///    su nối vòi cấp và ống thoát) thì ngưng tụ; đầu dưới của sinh hàn quay về bình (đun hồi lưu) hay sang đầu nối hứng
    ///    (chưng cất: nhỏ giọt vào bình hứng). Nhiệt kế ở đầu nối đọc nhiệt độ hơi. Không có sinh hàn: hơi bay đi (cô cạn).
    ///  • Chất lỏng: phễu lọc / Buchner giữ chất rắn, nước lọc chảy xuống (hút chân không nhanh hơn); phễu chiết mở khóa thì
    ///    lớp nặng hơn chảy ra trước; phễu nhỏ giọt nhỏ vào bình bên dưới.
    ///  • Nhiệt phân: chất rắn trong ống đun khô (KMnO₄, KClO₃, CuSO₄·5H₂O, NH₄Cl, Cu(OH)₂) phân hủy khi đủ nóng.
    /// Mô hình mạng ở mức giáo dục (không mô phỏng chất lỏng từng hạt): đúng định tính, gần đúng định lượng.
    /// </summary>
    public class FluidNetwork : MonoBehaviour
    {
        public static FluidNetwork Instance { get; private set; }
        public float tickS = 0.1f;

        // ---- trạng thái công khai (hiển thị, kiểm thử) ----
        /// <summary>Khí đã thu được trong ống / lọ thu (mol) và loại khí.</summary>
        public readonly Dictionary<LabContainer, double> CollectedMol = new Dictionary<LabContainer, double>();
        public readonly Dictionary<LabContainer, string> CollectedGas = new Dictionary<LabContainer, string>();
        /// <summary>Nhiệt độ nhiệt kế đang chỉ (°C).</summary>
        public readonly Dictionary<ApparatusPart, float> Thermometers = new Dictionary<ApparatusPart, float>();
        /// <summary>Khí thoát ra phòng (mol).</summary>
        public readonly Dictionary<string, double> Escaped = new Dictionary<string, double>();
        /// <summary>Chất lỏng đã ngưng tụ / chảy qua (ml) theo bình nhận, để kiểm thử.</summary>
        public readonly Dictionary<LabContainer, double> ReceivedMl = new Dictionary<LabContainer, double>();
        public readonly List<string> Log = new List<string>();
        /// <summary>Nơi chất lỏng chảy ra từ phễu đi tới (tên bình nhận, hoặc "ra ngoài"), để kiểm thử.</summary>
        public readonly Dictionary<LabContainer, string> OutflowTo = new Dictionary<LabContainer, string>();
        readonly Dictionary<string, float> logStamp = new Dictionary<string, float>();
        public string LastPop { get; private set; }

        public const double MolarVolumeMl = 24465;  // ml/mol ở 25 °C, 1 atm

        readonly Dictionary<LabContainer, double> sealedMol = new Dictionary<LabContainer, double>();
        readonly Dictionary<LabContainer, float> solidTemp = new Dictionary<LabContainer, float>();
        readonly Dictionary<string, double> gasBuf = new Dictionary<string, double>();
        readonly List<LabContainer> containers = new List<LabContainer>();
        float timer, nextScan;

        public float SolidTemp(LabContainer c) => solidTemp.TryGetValue(c, out var t) ? t : 25f;
        public double CollectedMl(LabContainer c) => CollectedMol.TryGetValue(c, out var m) ? m * MolarVolumeMl : 0;

        float tickDt = 0.1f;
        static FlowFx Fx => FlowFx.Instance;
        readonly HashSet<LabContainer> bubbledAt = new HashSet<LabContainer>();  // đã vẽ bọt ở đầu ống trong nhịp này

        /// <summary>Số bọt khí mỗi giây theo lưu lượng khí (mol trong một nhịp).</summary>
        float BubbleRate(double mol) => Mathf.Clamp((float)(mol / Math.Max(0.02f, tickDt)) * 3000f, 3f, 30f);

        void Awake()
        {
            Instance = this;
            if (GetComponent<FlowFx>() == null) gameObject.AddComponent<FlowFx>();
        }

        /// <summary>Miệng bình đang đậy / nối kín (khí không bốc lên ở miệng mà đi theo khớp nối).</summary>
        public static bool MouthClosed(LabContainer c)
        {
            var part = c != null ? c.GetComponent<ApparatusPart>() : null;
            if (part == null || c.mouth == null) return false;
            foreach (var p in part.ports)
                if (p != null && p.channel == 0 && p.Connected != null && Vector3.Distance(p.transform.position, c.mouth.position) < 0.03f) return true;
            return false;
        }

        /// <summary>Điểm mặt chất lỏng của bình (thế giới) ngay dưới / quanh điểm p; bình rỗng: đáy bình.</summary>
        static Vector3 SurfaceUnder(LabContainer c, Vector3 p)
        {
            float y = c.transform.position.y + 0.003f;
            if (c.fill != null && c.VolumeMl > 0.3f) y = c.fill.transform.TransformPoint(new Vector3(0, c.fill.LevelM, 0)).y;
            return new Vector3(p.x, Mathf.Min(y, p.y), p.z);
        }

        // =====================================================================
        // Bảng khí
        // =====================================================================

        struct GasInfo
        {
            public double M;
            public (string id, double n)[] dissolve;
            public bool toxic, flammable;
            public string formula;
        }

        static readonly Dictionary<string, GasInfo> Gases = new Dictionary<string, GasInfo>
        {
            ["CO2"] = new GasInfo { M = 44, dissolve = new[] { ("CO3", 1.0) }, formula = "CO₂" },
            ["SO2"] = new GasInfo { M = 64, dissolve = new[] { ("SO3", 1.0) }, toxic = true, formula = "SO₂" },
            ["H2S"] = new GasInfo { M = 34, dissolve = new[] { ("S", 1.0) }, toxic = true, flammable = true, formula = "H₂S" },
            ["NH3"] = new GasInfo { M = 17, dissolve = new[] { ("NH3", 1.0) }, toxic = true, formula = "NH₃" },
            ["HCl"] = new GasInfo { M = 36.5, dissolve = new[] { ("Cl-", 1.0) }, toxic = true, formula = "HCl" },
            ["Cl2"] = new GasInfo { M = 71, dissolve = new[] { ("Cl2", 1.0) }, toxic = true, formula = "Cl₂" },
            ["NO2"] = new GasInfo { M = 46, dissolve = new[] { ("NO3-", 0.5), ("NO2", 0.5) }, toxic = true, formula = "NO₂" },
            ["HCN"] = new GasInfo { M = 27, dissolve = new[] { ("CN", 1.0) }, toxic = true, flammable = true, formula = "HCN" },
            ["NO"] = new GasInfo { M = 30, toxic = true, formula = "NO" },
            ["O2"] = new GasInfo { M = 32, formula = "O₂" },
            ["H2"] = new GasInfo { M = 2, flammable = true, formula = "H₂" },
            ["N2"] = new GasInfo { M = 28, formula = "N₂" },
            ["CH4"] = new GasInfo { M = 16, flammable = true, formula = "CH₄" },
            ["C2H2"] = new GasInfo { M = 26, flammable = true, formula = "C₂H₂" },
            ["C2H4"] = new GasInfo { M = 28, flammable = true, formula = "C₂H₄" },
        };

        static GasInfo Info(string id) => id != null && Gases.TryGetValue(id, out var g) ? g : new GasInfo { M = 30, formula = id ?? "khí" };

        // =====================================================================
        // Vòng chạy
        // =====================================================================

        /// <summary>Thời gian CPU trung bình mỗi nhịp mạng (ms), để đo hiệu năng.</summary>
        public static float CostMs { get; private set; }
        readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        readonly System.Diagnostics.Stopwatch prof = new System.Diagnostics.Stopwatch();
        readonly double[] profMs = new double[5];
        const int SlowEvery = 3;
        int tickIndex;
        readonly Dictionary<LabContainer, float> lastSlow = new Dictionary<LabContainer, float>();
        int profTicks;

        /// <summary>Thời gian trung bình mỗi nhịp theo phần việc (điểm sôi, sôi, khí, đun khô, chảy) kể từ lần gọi trước; xóa số đếm.</summary>
        public string TakeProfile()
        {
            int n = Math.Max(1, profTicks);
            string s = $"điểm sôi {profMs[0] / n:0.00} | sôi {profMs[1] / n:0.00} | khí {profMs[2] / n:0.00} | đun khô {profMs[3] / n:0.00} | chảy {profMs[4] / n:0.00} ms ({containers.Count} bình)";
            Array.Clear(profMs, 0, profMs.Length);
            profTicks = 0;
            return s;
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < tickS) return;
            watch.Restart();
            Tick();
            CostMs = Mathf.Lerp(CostMs, (float)watch.Elapsed.TotalMilliseconds, 0.1f);
        }

        void Tick()
        {
            float dt = timer;
            timer = 0;
            tickDt = dt;
            bubbledAt.Clear();
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 1f;
                containers.Clear();
                foreach (var c in Khkt.Rendering.ZoneRoot.FindAll<LabContainer>()) if (c.reactive) containers.Add(c);
            }
            var sw = prof;
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                var part = c.GetComponent<ApparatusPart>();
                bool heated = Time.time - c.LastHeatedTime < 0.5f;
                // sôi, chảy (mỗi lần lấy ra / đổ vào bình phải cân bằng hóa học lại): nhịp chậm 0,3 s, rải đều các bình
                // qua các nhịp; khí, đun khô vẫn 10 lần/giây
                bool slow = (tickIndex + i) % SlowEvery == 0;
                float slowDt = 0;
                if (slow)
                {
                    slowDt = Mathf.Clamp(Time.time - (lastSlow.TryGetValue(c, out var ls) ? ls : Time.time - dt * SlowEvery), dt, 1f);
                    lastSlow[c] = Time.time;
                }
                sw.Restart();
                if (slow && (heated || c.ExcessHeatJ > 0))
                {
                    double bp = Volatility.BubblePoint(Volatility.Analyze(c.Total.Mix, c.VolumeMl));
                    c.BoilCapC = (float)Math.Min(400, bp);
                    if (heated && part != null && Trace(part, false).exits.Count == 0)
                        Note(c, "dun_he_kin", $"Đang đun nóng hệ kín ({c.label})", "Hơi và khí không có lối thoát, áp suất tăng: phải có lối thông ra ngoài (đầu nhỏ giọt, đầu sinh hàn) trước khi đun.", Severity.Danger, "no", gap: 12f);
                }
                profMs[0] += sw.Elapsed.TotalMilliseconds; sw.Restart();
                tickDt = slow ? slowDt : dt;
                if (slow && c.ExcessHeatJ > 1f && c.VolumeMl > 0.05f) Boil(c, part);
                tickDt = dt;
                profMs[1] += sw.Elapsed.TotalMilliseconds; sw.Restart();
                gasBuf.Clear();
                if (c.TakeGas(gasBuf) && part != null)
                    foreach (var kv in gasBuf) RouteGas(c, part, kv.Key, kv.Value);
                profMs[2] += sw.Elapsed.TotalMilliseconds; sw.Restart();
                if (c.DryHeatJ > 0 || solidTemp.ContainsKey(c)) DryHeat(c, part, dt);
                profMs[3] += sw.Elapsed.TotalMilliseconds; sw.Restart();
                tickDt = slow ? slowDt : dt;
                if (slow && part != null) Outflow(c, part, slowDt);
                tickDt = dt;
                profMs[4] += sw.Elapsed.TotalMilliseconds;
            }
            profTicks++;
            tickIndex++;
            // nhiệt kế không còn hơi đi qua thì nguội dần về nhiệt độ phòng
            foreach (var k in Thermometers.Keys.ToList())
            {
                if (k == null) { Thermometers.Remove(k); continue; }
                if (Time.time - (thermoStamp.TryGetValue(k, out var s) ? s : 0) > 0.5f)
                    Thermometers[k] = Mathf.MoveTowards(Thermometers[k], 25f, 2f * dt);
            }
        }

        readonly Dictionary<ApparatusPart, float> thermoStamp = new Dictionary<ApparatusPart, float>();

        // =====================================================================
        // Đi theo khớp nối
        // =====================================================================

        sealed class Node
        {
            public ApparatusPart part;
            public Port entry;
            public int prev;
        }

        sealed class TraceResult
        {
            public readonly List<Node> nodes = new List<Node>();
            public readonly List<(Port port, int node)> exits = new List<(Port, int)>();
            public int condenser = -1;
            public bool washReversed;
            public IEnumerable<int> PathTo(int node)
            {
                var list = new List<int>();
                for (int i = node; i >= 0 && list.Count < 200; i = nodes[i].prev) list.Add(i);
                list.Reverse();
                return list;
            }
        }

        static bool IsCondenser(ApparatusPart p) => p.libraryId != null && p.libraryId.StartsWith("sinh_han");
        static bool IsWash(ApparatusPart p) => p.libraryId == "binh_rua_khi";
        static bool IsTrough(ApparatusPart p) => p.libraryId == "chau";
        static bool IsThermometer(ApparatusPart p) => p.libraryId == "nhiet_ke";

        /// <summary>Đi theo các khớp kênh dẫn chất (0) từ bộ phận src; dừng ở ống sinh hàn đầu tiên nếu stopAtCondenser.</summary>
        TraceResult Trace(ApparatusPart src, bool stopAtCondenser)
        {
            var r = new TraceResult();
            var seen = new HashSet<ApparatusPart> { src };
            r.nodes.Add(new Node { part = src, entry = null, prev = -1 });
            for (int i = 0; i < r.nodes.Count; i++)
            {
                var n = r.nodes[i];
                var p = n.part;
                if (stopAtCondenser && i > 0 && IsCondenser(p)) { if (r.condenser < 0) r.condenser = i; continue; }
                IEnumerable<Port> ports = p.ports.Where(x => x != null && x.channel == 0 && x != n.entry);
                if (IsWash(p) && p.Container != null && p.Container.VolumeMl > 3)
                {
                    if (i == 0) ports = ports.Where(x => x.label.Contains(" ra"));  // khí thoát ra khỏi dung dịch rửa: đi ống ngắn
                    else
                    {
                        bool viaInlet = n.entry != null && n.entry.label.Contains("vào");
                        r.washReversed |= !viaInlet;
                        ports = ports.Where(x => viaInlet ? x.label.Contains(" ra") : x.label.Contains("vào"));
                    }
                }
                if (IsTrough(p) && n.entry != null && n.entry.type == PortType.LoNut)
                    ports = ports.Where(x => x.type == PortType.MiengRa);  // khí sủi lên ở kệ: vào ống úp hoặc thoát qua mặt nước
                foreach (var port in ports)
                {
                    if (port.IsOpening) { r.exits.Add((port, i)); continue; }
                    var c = port.Connected;
                    if (c == null || c.channel < 0) continue;
                    var q = c.Part;
                    if (q == null || seen.Contains(q)) continue;
                    seen.Add(q);
                    r.nodes.Add(new Node { part = q, entry = c, prev = i });
                }
                if (p.linked != null && !seen.Contains(p.linked))
                {
                    seen.Add(p.linked);
                    r.nodes.Add(new Node { part = p.linked, entry = null, prev = i });
                }
            }
            return r;
        }

        // =====================================================================
        // Khí
        // =====================================================================

        void RouteGas(LabContainer src, ApparatusPart part, string gas, double mol)
        {
            if (mol <= 0) return;
            var tr = Trace(part, false);
            if (tr.exits.Count == 0) { Pressure(src, part, mol, Info(gas).formula); return; }
            if (tr.washReversed)
                Note(src, "rua_khi_nguoc", "Bình rửa khí lắp ngược: khí vào ống ngắn đẩy dung dịch ra ngoài", "Khí phải vào ống DÀI (sục xuống đáy) và ra ống NGẮN.", Severity.Warning, gap: 15f);
            double share = mol / tr.exits.Count;
            foreach (var (port, node) in tr.exits)
            {
                double m = share;
                var path = tr.PathTo(node).ToList();
                var pathParts = new HashSet<ApparatusPart>(path.Select(i => tr.nodes[i].part));
                foreach (int i in path)
                {
                    var p = tr.nodes[i].part;
                    if (i > 0 && IsWash(p) && p.Container != null && p.Container.VolumeMl > 3 && !tr.washReversed) m = Absorb(p.Container, gas, m, src);
                    if (IsTrough(p) && tr.nodes[i].entry != null && tr.nodes[i].entry.type == PortType.LoNut) m = CollectUnderWater(p, gas, m, src);
                    if (m <= 0) break;
                }
                if (m > 0) ExitGas(port, gas, m, src, pathParts);
            }
        }

        /// <summary>Khí đi qua dung dịch: phần tan / phản ứng được bị giữ lại. Trả về phần đi tiếp (mol).</summary>
        double Absorb(LabContainer liquid, string gas, double mol, LabContainer src)
        {
            var g = Info(gas);
            if (liquid.VolumeMl >= 1 && Fx != null && !bubbledAt.Contains(liquid))
            {
                // bọt khí sục qua dung dịch (bình rửa khí: từ đầu ống dài gần đáy)
                var bottom = liquid.transform.position + liquid.transform.up * 0.008f;
                Fx.Request(liquid, 4, FlowFx.Kind.Bubble, bottom, SurfaceUnder(liquid, bottom + Vector3.up), BubbleRate(mol), Color.white);
            }
            if (g.dissolve == null || liquid.VolumeMl < 1) return mol;
            var b = new MixBuilder();
            foreach (var (id, n) in g.dissolve) if (SpeciesRegistry.Get(id) != null) b.Add(id, n * mol);
            liquid.Add(Solution.Of(0, b.ToMix()));
            Note(liquid, "hap_thu:" + gas, $"Khí {g.formula} được hấp thụ trong {liquid.label}", "Khí tan / phản ứng với dung dịch nên bị giữ lại, không đi tiếp.", Severity.Info, gas: gas, gap: 20f);
            return 0;
        }

        /// <summary>Khí sủi lên ở kệ trong chậu nước: dồn vào ống / lọ úp trên kệ (đẩy nước ra), đầy thì tràn qua mặt nước.</summary>
        double CollectUnderWater(ApparatusPart trough, string gas, double mol, LabContainer src)
        {
            var water = trough.Container;
            if (water == null || water.VolumeMl < 150)
            {
                Note(src, "chau_can", "Chậu thu khí chưa có nước", "Thu khí bằng đẩy nước: đổ nước vào chậu, đổ đầy nước vào ống nghiệm rồi úp ngược lên kệ.", Severity.Notice, gap: 15f);
                return mol;
            }
            var shelf = trough.ports.FirstOrDefault(x => x != null && x.type == PortType.GiaUp);
            var jar = shelf != null && shelf.Connected != null ? shelf.Connected.Part : null;
            var c = jar != null ? jar.Container : null;
            if (c == null) return mol;  // không có ống úp: bọt khí thoát qua mặt nước
            double have = CollectedMol.TryGetValue(c, out var h) ? h : 0;
            double room = Math.Max(0, c.capacityMl * 0.97 / MolarVolumeMl - have);
            double take = Math.Min(mol, room);
            var hole = trough.ports.FirstOrDefault(x => x != null && x.type == PortType.LoNut);
            if (Fx != null && hole != null)
            {
                if (take > 0)
                {
                    // bọt khí từ lỗ kệ dồn lên ống úp, tới mặt phân cách khí – nước trong ống
                    float yMouth = c.mouth != null ? c.transform.InverseTransformPoint(c.mouth.position).y : 0.15f;
                    float frac = (float)Math.Min(1, (have + take) * MolarVolumeMl / Math.Max(1, c.capacityMl * 0.97));
                    var top = c.transform.TransformPoint(new Vector3(0, Mathf.Lerp(0.004f, yMouth, frac), 0));
                    Fx.Request(trough, 5, FlowFx.Kind.Bubble, hole.transform.position, top, BubbleRate(take), Color.white);
                }
                if (mol - take > 1e-9)
                    {
                    // ống úp đã đầy: bọt tràn ra mép miệng ống rồi nổi lên mặt nước chậu
                    var m = c.mouth != null ? c.mouth.position : hole.transform.position;
                    var side = Vector3.ProjectOnPlane(m - trough.transform.position, Vector3.up);
                    side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
                    var from = m + side * 0.016f;
                    Fx.Request(trough, 6, FlowFx.Kind.Bubble, from, SurfaceUnder(water, from + Vector3.up), BubbleRate(mol - take), Color.white);
                }
            }
            if (take > 0)
            {
                CollectedMol[c] = have + take;
                CollectedGas[c] = CollectedGas.TryGetValue(c, out var old) && old != gas && have > 1e-6 ? old + "+" + gas : gas;
                Note(c, "thu_khi", $"Khí {Info(gas).formula} đẩy nước, dồn vào {c.label}", "Thu khí bằng phương pháp đẩy nước: khí ít tan trong nước (O₂, H₂, N₂, CH₄...).", Severity.Info, gas: gas, gap: 30f);
                if (have + take >= c.capacityMl * 0.97 / MolarVolumeMl - 1e-9)
                    Note(c, "thu_khi_day", $"{c.label} đã đầy khí {Info(gas).formula}", "Bịt miệng ống dưới nước rồi lấy ra; khí sinh thêm sủi qua mặt nước.", Severity.Notice, gas: gas, gap: 60f);
            }
            return mol - take;
        }

        /// <summary>Khí ra ở lối thoát: đầu ống ngập trong dung dịch thì sục vào, trong lọ ngửa thì thu, còn lại thoát ra phòng.</summary>
        void ExitGas(Port port, string gas, double mol, LabContainer src, HashSet<ApparatusPart> path)
        {
            var tip = port.transform.position;
            var target = ContainerAt(tip, path, out bool submerged);
            var g = Info(gas);
            if (target != null)
            {
                if (submerged)
                {
                    if (Fx != null)
                    {
                        Fx.Request(port, 0, FlowFx.Kind.Bubble, tip, SurfaceUnder(target, tip + Vector3.up), BubbleRate(mol), Color.white);
                        bubbledAt.Add(target);
                    }
                    Note(target, "suc_khi:" + gas, $"Sục khí {g.formula} vào {target.label}", g.dissolve != null ? "Khí tan / phản ứng với dung dịch." : "Khí ít tan: sủi bọt rồi bay lên.", Severity.Info, gas: gas, gap: 20f);
                    mol = Absorb(target, gas, mol, src);
                    if (mol <= 0) return;
                }
                else if (target.transform.up.y > 0.7f && g.M > 29)
                {
                    // thu bằng đẩy không khí: lọ ngửa, khí nặng hơn không khí chìm xuống đáy
                    double have = CollectedMol.TryGetValue(target, out var h) ? h : 0;
                    double room = Math.Max(0, (target.capacityMl - target.VolumeMl) / MolarVolumeMl - have);
                    double take = Math.Min(mol, room);
                    if (take > 0)
                    {
                        CollectedMol[target] = have + take;
                        CollectedGas[target] = gas;
                        Note(target, "thu_kk", $"Thu khí {g.formula} vào {target.label} (đẩy không khí)", "Khí nặng hơn không khí (M > 29) nên để ngửa bình, đầu ống dẫn sát đáy.", Severity.Info, gas: gas, gap: 30f);
                    }
                    mol -= take;
                    if (mol <= 0) return;
                }
                else if (target.transform.up.y > 0.7f && g.M < 29)
                    Note(target, "thu_kk_sai", $"Khí {g.formula} nhẹ hơn không khí bay ra khỏi {target.label}", "Khí nhẹ (H₂, NH₃, CH₄): úp ngược bình thu hoặc thu bằng đẩy nước.", Severity.Notice, gas: gas, gap: 20f);
            }
            Escape(tip, gas, mol, src);
        }

        void Escape(Vector3 at, string gas, double mol, LabContainer src)
        {
            Escaped[gas] = (Escaped.TryGetValue(gas, out var e) ? e : 0) + mol;
            var g = Info(gas);
            if (Fx != null && ReactionFx.FumeColor(gas, out var fume))
                Fx.Request(src, 100 + (gas.GetHashCode() & 0xff), FlowFx.Kind.Fume, at, at, Mathf.Clamp((float)(mol / tickDt) * 2000f, 2f, 10f), fume);
            if (g.toxic && !FumeHoodZone.Covers(at))
                Note(src, "doc_thoat:" + gas, $"Khí độc {g.formula} thoát ra ngoài tủ hút!", "Làm thí nghiệm sinh khí độc trong tủ hút, hoặc dẫn khí vào dung dịch hấp thụ (NaOH...).", Severity.Danger, "khi_doc", gas, 10f);
            if (g.flammable && NearFlame(at))
                Note(src, "chay_thoat:" + gas, $"Khí dễ cháy {g.formula} thoát ra gần ngọn lửa!", "Nguy cơ cháy nổ: tắt đèn cồn hoặc dẫn khí ra xa ngọn lửa.", Severity.Danger, "chay_no", gas, 10f);
        }

        static bool NearFlame(Vector3 p)
        {
            foreach (var h in UnityEngine.Object.FindObjectsByType<HeatSource>(FindObjectsInactive.Exclude))
                if (h.On && Vector3.Distance(h.transform.position, p) < 0.45f) return true;
            return false;
        }

        /// <summary>Bình chứa điểm p bên trong lòng bình (không kể các bộ phận trên đường đi); submerged: p dưới mực chất lỏng.</summary>
        LabContainer ContainerAt(Vector3 p, HashSet<ApparatusPart> exclude, out bool submerged)
        {
            submerged = false;
            foreach (var c in containers)
            {
                if (c == null || !c.isActiveAndEnabled || c.fill == null || c.mouth == null) continue;
                var cp = c.GetComponent<ApparatusPart>();
                if (cp != null && exclude.Contains(cp)) continue;
                var l = c.transform.InverseTransformPoint(p);
                float mouthY = c.transform.InverseTransformPoint(c.mouth.position).y;
                var prof = c.fill.innerProfile;
                if (prof == null || prof.Length < 2 || l.y < prof[0].y - 0.002f || l.y > mouthY + 0.002f) continue;
                if (new Vector2(l.x, l.z).magnitude > RadiusAt(prof, l.y) * 1.05f + 0.002f) continue;
                submerged = c.VolumeMl > 0.5f && l.y < c.fill.LevelM - 0.002f;
                return c;
            }
            return null;
        }

        static float RadiusAt(Vector2[] prof, float y)
        {
            if (y <= prof[0].y) return prof[0].x;
            for (int i = 1; i < prof.Length; i++)
                if (y <= prof[i].y) return Mathf.Lerp(prof[i - 1].x, prof[i].x, Mathf.InverseLerp(prof[i - 1].y, prof[i].y, y));
            return prof[prof.Length - 1].x;
        }

        /// <summary>Khí / hơi trong hệ kín: áp suất tăng theo pV = nRT; quá 2 atm thì nút bật ra.</summary>
        void Pressure(LabContainer c, ApparatusPart part, double mol, string what)
        {
            double n = (sealedMol.TryGetValue(c, out var s) ? s : 0) + mol;
            sealedMol[c] = n;
            double headL = Math.Max(0.004, (c.capacityMl - c.VolumeMl) / 1000.0);
            double T = 273.15 + Math.Max(c.TemperatureC, SolidTemp(c));
            double p = 1 + n * 0.08206 * T / headL;
            if (p < 2.0)
            {
                Note(c, "ap_suat", $"Hệ kín: áp suất trong {c.label} tăng ({p:0.0} atm)", $"{what} không có lối thoát. Hệ kín không được sinh khí hay đun nóng.", Severity.Warning, "no", gap: 8f);
                return;
            }
            sealedMol[c] = 0;
            ApparatusPart stopper = null;
            foreach (var port in part.ports)
            {
                var o = port != null ? port.Connected : null;
                if (o != null && o.Part != null && o.Part.ParentJoint == o && o.type == PortType.NutCaoSu) { stopper = o.Part; break; }
            }
            if (stopper != null)
            {
                stopper.Detach();
                var at = c.transform.position + c.transform.right * 0.15f + Vector3.up * 0.05f;
                float y = at.y;
                if (Physics.Raycast(at + Vector3.up * 0.2f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) y = hit.point.y;
                stopper.transform.SetPositionAndRotation(new Vector3(at.x, y, at.z), Quaternion.identity);
                LastPop = $"{stopper.partName} bật khỏi {c.label} khi áp suất vượt 2 atm";
                Note(c, "bat_nut", $"Áp suất trong {c.label} vượt 2 atm: {stopper.partName.ToLowerInvariant()} bật ra!", "Không đun nóng / sinh khí trong hệ kín. Phải có lối thông ra ngoài.", Severity.Danger, "no", gap: 2f);
            }
            else
            {
                LastPop = $"{c.label}: áp suất vượt 2 atm, khí rò qua khớp";
                Note(c, "no_vo", $"Áp suất trong {c.label} vượt 2 atm: nguy cơ nổ vỡ dụng cụ!", "Hệ kín bằng khớp nhám: khí rò qua khớp, có thể làm bật khớp hoặc vỡ bình.", Severity.Danger, "no", gap: 2f);
            }
        }

        // =====================================================================
        // Sôi, hơi, sinh hàn
        // =====================================================================

        void Boil(LabContainer c, ApparatusPart part)
        {
            var L = Volatility.Analyze(c.Total.Mix, c.VolumeMl);
            var v = Volatility.Vaporize(L, c.ExcessHeatJ);
            c.ExcessHeatJ = 0;
            if (v.liquidMl <= 1e-5) return;
            var vapor = Solution.Of((float)v.liquidMl, Volatility.OrganicMix(v));
            if (Fx != null && c.VolumeMl > 0.5f)
            {
                // sôi: bọt hơi từ đáy bình nổi lên
                var bottom = c.transform.position + c.transform.up * 0.006f;
                Fx.Request(c, 1, FlowFx.Kind.Bubble, bottom, SurfaceUnder(c, bottom + Vector3.up), Mathf.Clamp((float)(v.liquidMl / tickDt) * 60f, 8f, 30f), Color.white);
            }
            c.TakeExact(vapor);
            RouteVapor(c, part, vapor, (float)v.tC, v.TotalMol);
            if (c.VolumeMl < 0.3f)
            {
                bool solids = HasSolids(c.Total.Mix);
                Note(c, "can", solids ? $"Cô cạn xong {c.label}: còn lại chất rắn" : $"{c.label} đã cạn khô", solids ? "Tắt lửa khi gần cạn để nhiệt dư làm khô, tránh bắn chất rắn." : "Đun cạn bình thủy tinh dễ nứt vỡ: tắt lửa.", solids ? Severity.Notice : Severity.Warning, "bong", gap: 30f);
            }
        }

        static bool HasSolids(Mix m)
        {
            for (int i = 0; i < m.Count; i++) if (m.MolAt(i) > 1e-7 && m.SpeciesAt(i).Kind != SpeciesKind.Indicator) return true;
            return false;
        }

        void RouteVapor(LabContainer c, ApparatusPart part, Solution vapor, float tC, double mol)
        {
            if (part == null) { EscapeVapor(c, c.mouth != null ? c.mouth.position : c.transform.position, vapor); return; }
            var tr = Trace(part, true);
            // nhiệt kế trên đường hơi đi (đầu nối chưng cất): chỉ nhiệt độ hơi
            int last = tr.condenser >= 0 ? tr.condenser : tr.nodes.Count - 1;
            for (int i = 0; i <= last && i < tr.nodes.Count; i++)
                foreach (var port in tr.nodes[i].part.ports)
                {
                    var o = port != null ? port.Connected : null;
                    if (o != null && o.Part != null && IsThermometer(o.Part)) { Thermometers[o.Part] = tC; thermoStamp[o.Part] = Time.time; }
                }
            if (tr.condenser >= 0)
            {
                var cd = tr.nodes[tr.condenser].part;
                if (Cooling(cd, out bool reversed))
                {
                    float eff = reversed ? 0.6f : 1f;
                    if (reversed)
                        Note(c, "nuoc_nguoc", "Nước làm lạnh đi ngược chiều (vào vòi trên)", "Nước phải vào ở vòi DƯỚI, ra ở vòi TRÊN để áo nước luôn đầy; nối ngược thì làm lạnh kém, hơi thoát ra.", Severity.Warning, gap: 15f);
                    Condense(tr, vapor.Scale(eff), c);
                    if (eff < 1f) EscapeVapor(c, cd.transform.position, vapor.Scale(1 - eff));
                    return;
                }
                Note(c, "chua_nuoc", "Ống sinh hàn chưa có nước làm lạnh: hơi không ngưng tụ, thoát ra ngoài", "Nối ống cao su: vòi nước vào (dưới) với vòi cấp nước, vòi nước ra (trên) với ống thoát nước.", Severity.Warning, gap: 15f);
                var tail = Trace(cd, false);
                if (tail.exits.Count > 0) { EscapeVapor(c, tail.exits[0].port.transform.position, vapor); return; }
            }
            if (tr.exits.Count > 0) { EscapeVapor(c, tr.exits[0].port.transform.position, vapor); return; }
            Pressure(c, part, mol, "Hơi");
        }

        /// <summary>Nước làm lạnh chảy qua áo sinh hàn: hai vòi nước nối ống cao su, một ống tới vòi cấp, một ống tới ống thoát.</summary>
        static bool Cooling(ApparatusPart cd, out bool reversed)
        {
            reversed = false;
            Port inlet = null, outlet = null;
            foreach (var p in cd.ports)
            {
                if (p == null || p.type != PortType.VoiNhanh || p.channel != 1) continue;
                var hose = p.Connected != null ? p.Connected.Part : null;
                var far = hose != null ? hose.linked : null;
                var fx = far != null && far.ParentJoint != null && far.ParentJoint.Connected != null ? far.ParentJoint.Connected.Part : null;
                if (fx == null || !fx.fixture) continue;
                if (fx.name.Contains("cấp")) inlet = p;
                else if (fx.name.Contains("thoát")) outlet = p;
            }
            if (inlet == null || outlet == null) return false;
            reversed = inlet.transform.position.y > outlet.transform.position.y + 0.01f;
            return true;
        }

        /// <summary>Chất ngưng tụ chảy theo đầu dưới của sinh hàn: về bình đang đun (hồi lưu) hoặc sang đầu nối hứng rồi nhỏ giọt.</summary>
        void Condense(TraceResult tr, Solution liquid, LabContainer source)
        {
            var node = tr.nodes[tr.condenser];
            var cd = node.part;
            var vap = cd.ports.Where(x => x != null && x.channel == 0).ToList();
            if (vap.Count == 0) return;
            var lower = vap.OrderBy(x => x.transform.position.y).First();
            var upper = vap.OrderBy(x => x.transform.position.y).Last();
            if (Fx != null && upper != lower)  // giọt ngưng chảy dọc ống trong, từ đầu cao xuống đầu thấp
                Fx.Request(cd, 2, FlowFx.Kind.Condensate, upper.transform.position, lower.transform.position, Mathf.Clamp(liquid.volumeMl / tickDt * 30f, 2f, 12f), FlowFx.LiquidColor(source));
            if (lower == node.entry || (lower.Connected != null && tr.nodes.Any(n => n.part == lower.Connected.Part) && lower.Connected.Part != cd))
            {
                source.Add(liquid);  // đun hồi lưu: chất ngưng tụ chảy ngược về bình
                Received(source, liquid.volumeMl);
                Note(source, "hoi_luu", "Đun hồi lưu: hơi ngưng tụ trong ống sinh hàn chảy về bình", "Phản ứng (este hóa, thủy phân) được đun lâu mà không mất chất bay hơi.", Severity.Info, gap: 60f);
                return;
            }
            // đi tiếp từ đầu dưới tới đầu nhỏ giọt
            Port drip = lower;
            var cur = lower;
            for (int hop = 0; hop < 4 && cur != null && cur.Connected != null; hop++)
            {
                var q = cur.Connected.Part;
                var outlet = q.ports.FirstOrDefault(x => x != null && x != cur.Connected && x.channel == 0 && x.type == PortType.MiengRa)
                          ?? q.ports.FirstOrDefault(x => x != null && x != cur.Connected && x.channel == 0);
                if (outlet == null) break;
                drip = outlet;
                cur = outlet;
            }
            Drip(drip.transform.position, liquid, source, cd.Root);
        }

        /// <summary>Chất lỏng nhỏ xuống từ điểm p: rơi vào bình có miệng ngay bên dưới, không thì tràn ra.</summary>
        void Drip(Vector3 p, Solution liquid, LabContainer source, ApparatusPart skipRoot)
        {
            var c = BelowTip(p, source);
            var color = FlowFx.LiquidColor(source);
            if (c != null)
            {
                Fx?.Request(source, 3, FlowFx.Kind.Drop, p, SurfaceUnder(c, p), liquid.volumeMl / tickDt, color);
                c.Add(liquid);
                Received(c, liquid.volumeMl);
                OutflowTo[source] = c.label;
                return;
            }
            OutflowTo[source] = "ra ngoài";
            var floor = p + Vector3.down * 1.0f;
            foreach (var h in Physics.RaycastAll(p, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                var ap = h.collider.GetComponentInParent<ApparatusPart>();
                if (h.collider.GetComponentInParent<LabContainer>() == source || (ap != null && skipRoot != null && ap.Root == skipRoot)) continue;
                OutflowTo[source] = "ra ngoài: chạm " + h.collider.name;
                floor = h.point;
                break;
            }
            Fx?.Request(source, 3, FlowFx.Kind.Drop, p, floor, liquid.volumeMl / tickDt, color);
            Note(source, "nho_ra_ngoai", "Chất lỏng nhỏ ra ngoài: chưa có bình hứng ngay dưới đầu nhỏ giọt", "Đặt bình hứng (bình nón, cốc) ngay dưới đầu nhỏ giọt / đuôi phễu.", Severity.Warning, gap: 12f);
        }

        /// <summary>
        /// Bình đứng thẳng có miệng ngay dưới điểm p (hoặc p nằm trong miệng, như đuôi phễu cắm vào cổ bình).
        /// So hình học với miệng bình thay cho tia chiếu: tia đi qua miệng bình rỗng xuyên mặt trong lưới va chạm, không chạm đáy.
        /// </summary>
        LabContainer BelowTip(Vector3 p, LabContainer source)
        {
            LabContainer best = null;
            float bestY = float.NegativeInfinity;
            foreach (var c in containers)
            {
                if (c == null || c == source || !c.isActiveAndEnabled || c.mouth == null || c.transform.up.y < 0.7f) continue;
                var m = c.mouth.position;
                if (m.y > p.y + 0.15f || p.y - m.y > 0.8f || p.y < c.transform.position.y) continue;
                float r = 0.02f;
                var prof = c.fill != null ? c.fill.innerProfile : null;
                if (prof != null && prof.Length > 1) r = RadiusAt(prof, c.transform.InverseTransformPoint(m).y) * c.transform.lossyScale.x;
                if (new Vector2(m.x - p.x, m.z - p.z).magnitude > r + 0.01f) continue;
                if (m.y > bestY) { bestY = m.y; best = c; }
            }
            return best;
        }
        void Received(LabContainer c, double ml) => ReceivedMl[c] = (ReceivedMl.TryGetValue(c, out var v) ? v : 0) + ml;

        void EscapeVapor(LabContainer c, Vector3 at, Solution vapor)
        {
            Fx?.Request(c, 8, FlowFx.Kind.Steam, at, at, Mathf.Clamp(vapor.volumeMl / tickDt * 40f, 3f, 14f), new Color(1f, 1f, 1f, 0.2f));
            if (vapor.Mix.IsEmpty) return;  // hơi nước bay đi: không cảnh báo
            bool flammable = false, toxic = false;
            string names = "";
            for (int i = 0; i < vapor.Mix.Count; i++)
            {
                var d = vapor.Mix.SpeciesAt(i);
                if (d.HasTag("de_chay") || d.HasTag("ancol") || d.HasTag("este")) flammable = true;
                if (d.ChemStt > 0) { var e = ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(d.ChemStt) : null; if (e != null && e.HasHazard("doc")) toxic = true; }
                names += (names.Length > 0 ? ", " : "") + d.Name;
            }
            if (flammable && NearFlame(at))
                Note(c, "hoi_chay", $"Hơi dễ cháy ({names}) thoát ra gần ngọn lửa!", "Nguy cơ bắt lửa: phải ngưng tụ hơi bằng ống sinh hàn có nước làm lạnh, hoặc đun cách thủy.", Severity.Danger, "chay_no", gap: 10f);
            if (toxic && !FumeHoodZone.Covers(at))
                Note(c, "hoi_doc", $"Hơi độc ({names}) bay ra ngoài tủ hút", "Đun chất lỏng độc trong tủ hút.", Severity.Warning, "khi_doc", gap: 10f);
        }

        // =====================================================================
        // Chất lỏng: phễu lọc, Buchner, phễu chiết, phễu nhỏ giọt
        // =====================================================================

        void Outflow(LabContainer c, ApparatusPart part, float dt)
        {
            string id = part.libraryId;
            if (id != "pheu_loc" && id != "buchner" && id != "pheu_chiet" && id != "pheu_nho_giot") return;
            if (c.VolumeMl < 0.02f) return;
            float rate;
            bool filter = id == "pheu_loc" || id == "buchner";
            Port outlet;
            if (filter)
            {
                outlet = id == "pheu_loc" ? part.ports.FirstOrDefault(x => x != null && x.type == PortType.MiengRa && x.label.StartsWith("Đuôi"))
                                          : part.ports.FirstOrDefault(x => x != null && x.type == PortType.NutCaoSu);
                rate = id == "pheu_loc" ? 1.5f : (Vacuum(part) ? 5f : 0.6f);
            }
            else
            {
                var valve = part.GetComponentInChildren<FlowValve>();
                if (valve == null || !valve.open) return;
                outlet = part.ports.FirstOrDefault(x => x != null && (x.type == PortType.MiengRa || x.type == PortType.DauOngThuyTinh) && x.label.StartsWith("Đuôi"));
                rate = valve.rateMlPerS;
            }
            if (outlet == null) return;
            float fillFrac = Mathf.Clamp01(c.VolumeMl / Mathf.Max(1f, c.capacityMl));
            float ml = rate * dt * (0.35f + 0.65f * Mathf.Sqrt(fillFrac));
            var total = c.Total;
            Solution part1;
            if (filter) part1 = LiquidOnly(total, Mathf.Min(ml, total.volumeMl));
            else if (id == "pheu_chiet")
            {
                var (lower, upper) = Layers(total);
                part1 = lower.volumeMl > 0.01f ? lower.Portion(Mathf.Min(ml, lower.volumeMl)) : upper.Portion(Mathf.Min(ml, upper.volumeMl));
            }
            else part1 = total.Portion(Mathf.Min(ml, total.volumeMl));
            if (part1.volumeMl <= 1e-5f) return;
            c.TakeExact(part1);
            var recv = Receiver(part, outlet);
            OutflowTo[c] = recv != null ? recv.label : "(nhỏ giọt)";
            if (recv != null)
            {
                // đuôi phễu cắm vào bình: giọt / dòng từ đuôi phễu xuống mặt chất lỏng trong bình
                var tip = outlet.transform.position;
                Fx?.Request(c, 3, FlowFx.Kind.Drop, tip, SurfaceUnder(recv, tip), part1.volumeMl / tickDt, FlowFx.LiquidColor(c));
                recv.Add(part1);
                Received(recv, part1.volumeMl);
            }
            else Drip(outlet.transform.position, part1, c, part.Root);
            if (filter && HasSolidPhase(c.Total.Mix) && c.VolumeMl < 0.05f)
                Note(c, "loc_xong", $"Lọc xong: chất rắn giữ lại trên giấy lọc trong {c.label}", "Nước lọc trong chảy xuống bình hứng; rửa kết tủa bằng ít nước cất nếu cần.", Severity.Info, gap: 60f);
        }

        /// <summary>Bình nhận chất lỏng khi đuôi phễu cắm vào (qua nút cao su, miệng bình): đi theo các khớp dẫn chất.</summary>
        static LabContainer Receiver(ApparatusPart funnel, Port outlet)
        {
            var seen = new HashSet<ApparatusPart> { funnel };
            var queue = new Queue<Port>();
            if (outlet.Connected != null) queue.Enqueue(outlet.Connected);
            foreach (var p in funnel.ports)
                if (p != null && p != outlet && p.type == PortType.ChanPheu && p.Connected != null && p.Connected.type == PortType.CoBinh) queue.Enqueue(p.Connected);
            for (int guard = 0; queue.Count > 0 && guard < 8; guard++)
            {
                var at = queue.Dequeue();
                var q = at.Part;
                if (q == null || seen.Contains(q)) continue;
                seen.Add(q);
                if (q.Container != null) return q.Container;
                foreach (var p in q.ports)
                    if (p != null && p != at && p.Connected != null && (p.type == PortType.NutCaoSu || p.type == PortType.CoBinh || p.type == PortType.KhopNhamDuc || p.type == PortType.KhopNhamCai))
                        queue.Enqueue(p.Connected);
            }
            return null;
        }

        /// <summary>Bình hút lọc dưới phễu Buchner có nhánh nối ống cao su tới vòi hút chân không.</summary>
        static bool Vacuum(ApparatusPart buchner)
        {
            var neck = buchner.ports.FirstOrDefault(x => x != null && x.type == PortType.NutCaoSu);
            var flask = neck != null && neck.Connected != null ? neck.Connected.Part : null;
            if (flask == null) return false;
            foreach (var p in flask.ports)
            {
                if (p == null || p.type != PortType.VoiNhanh || p.Connected == null) continue;
                var far = p.Connected.Part.linked;
                var fx = far != null && far.ParentJoint != null && far.ParentJoint.Connected != null ? far.ParentJoint.Connected.Part : null;
                if (fx != null && fx.fixture && fx.name.Contains("chân không")) return true;
            }
            return false;
        }

        static bool HasSolidPhase(Mix m)
        {
            for (int i = 0; i < m.Count; i++) if (m.SpeciesAt(i).IsSolidPhase && m.MolAt(i) > 1e-8) return true;
            return false;
        }

        /// <summary>Nước lọc: ml chất lỏng (mọi chất tan) không kèm chất rắn / kết tủa.</summary>
        static Solution LiquidOnly(Solution s, float ml)
        {
            if (s.volumeMl <= 0) return default;
            double f = Math.Min(1, ml / s.volumeMl);
            var b = new MixBuilder();
            for (int i = 0; i < s.Mix.Count; i++)
                if (!s.Mix.SpeciesAt(i).IsSolidPhase) b.Add(s.Mix.IndexAt(i), s.Mix.MolAt(i) * f);
            return Solution.Of((float)(s.volumeMl * f), b.ToMix(), (float)(s.indicatorDrops * f));
        }

        /// <summary>Hai lớp trong phễu chiết: chất hữu cơ không tan là một lớp (nặng hay nhẹ hơn nước theo khối lượng riêng).</summary>
        public static (Solution lower, Solution upper) Layers(Solution s)
        {
            var org = new MixBuilder();
            var aq = new MixBuilder();
            double orgMl = 0, orgG = 0;
            for (int i = 0; i < s.Mix.Count; i++)
            {
                var d = s.Mix.SpeciesAt(i);
                double n = s.Mix.MolAt(i);
                if (d.Kind == SpeciesKind.Molecule && d.Insoluble)
                {
                    org.Add(s.Mix.IndexAt(i), n);
                    double m = n * Math.Max(1, d.MolarMass);
                    orgG += m;
                    orgMl += m / Math.Max(0.3, d.Density);
                }
                else aq.Add(s.Mix.IndexAt(i), n);
            }
            orgMl = Math.Min(orgMl, s.volumeMl);
            var orgSol = Solution.Of((float)orgMl, org.ToMix());
            var aqSol = Solution.Of((float)(s.volumeMl - orgMl), aq.ToMix(), s.indicatorDrops);
            bool orgHeavy = orgMl > 0 && orgG / orgMl > 1.0;
            return orgHeavy ? (orgSol, aqSol) : (aqSol, orgSol);
        }

        // =====================================================================
        // Nhiệt phân chất rắn khi đun khô
        // =====================================================================

        struct Decomp
        {
            public double tC, tCatC;
            public (string id, double n)[] products;
            public (string gas, double n)[] gases;
            public double water;
            public string title, eq;
        }

        static readonly Dictionary<string, Decomp> Decomps = new Dictionary<string, Decomp>
        {
            ["KMnO4"] = new Decomp { tC = 220, products = new[] { ("K+", 1.0), ("MnO42-", 0.5), ("MnO2(s)", 0.5) }, gases = new[] { ("O2", 0.5) },
                                     title = "KMnO₄ bị nhiệt phân: thoát khí O₂ (que đóm còn tàn đỏ bùng cháy)", eq = "2KMnO₄ → K₂MnO₄ + MnO₂ + O₂↑" },
            ["KClO3"] = new Decomp { tC = 380, tCatC = 240, products = new[] { ("K+", 1.0), ("Cl-", 1.0) }, gases = new[] { ("O2", 1.5) },
                                     title = "KClO₃ bị nhiệt phân: thoát khí O₂", eq = "2KClO₃ → 2KCl + 3O₂↑ (xúc tác MnO₂, t°)" },
            ["CuSO4.5H2O"] = new Decomp { tC = 110, products = new[] { ("Cu2+", 1.0), ("SO4", 1.0) }, water = 5,
                                     title = "Tinh thể CuSO₄·5H₂O xanh mất nước, hóa trắng; hơi nước ngưng ở miệng ống", eq = "CuSO₄·5H₂O → CuSO₄ + 5H₂O" },
            ["NH4Cl"] = new Decomp { tC = 340, products = new (string, double)[0], gases = new[] { ("NH3", 1.0), ("HCl", 1.0) },
                                     title = "NH₄Cl bị nhiệt phân, NH₃ và HCl kết hợp lại ở chỗ nguội (khói trắng)", eq = "NH₄Cl ⇌ NH₃ + HCl" },
            ["Cu(OH)2"] = new Decomp { tC = 80, products = new[] { ("CuO(s)", 1.0) }, water = 1,
                                     title = "Cu(OH)₂ xanh lam hóa đen (CuO)", eq = "Cu(OH)₂ → CuO + H₂O" },
        };

        static string Formula(SpeciesDef d)
        {
            if (d.Kind == SpeciesKind.Lump && d.ChemStt > 0 && ChemicalCatalog.Instance != null)
                return ChemicalCatalog.Instance.Get(d.ChemStt)?.cong_thuc;
            return d.Id;
        }

        void DryHeat(LabContainer c, ApparatusPart part, float dt)
        {
            float t = SolidTemp(c);
            var mix = c.Total.Mix;
            double grams = 0;
            for (int i = 0; i < mix.Count; i++) if (mix.SpeciesAt(i).IsSolidPhase) grams += mix.MolAt(i) * Math.Max(20, mix.SpeciesAt(i).MolarMass);
            float heatCap = 12f + (float)grams * 0.9f;  // ống nghiệm + chất rắn (J/K)
            t += c.DryHeatJ / heatCap;
            c.DryHeatJ = 0;
            t -= (t - 25f) * 0.04f * dt;  // tỏa nhiệt ra không khí: đèn cồn giữ được ~550 °C
            if (c.VolumeMl > 0.05f) t = Mathf.Min(t, c.TemperatureC);
            if (t < 26f) { solidTemp.Remove(c); return; }
            solidTemp[c] = t;
            if (c.VolumeMl > 0.05f || mix.IsEmpty) return;
            bool catalyst = mix.Get("MnO2(s)") > 1e-6;
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                string f = Formula(d);
                if (f == null || !Decomps.TryGetValue(f, out var r)) continue;
                double tc = catalyst && r.tCatC > 0 ? r.tCatC : r.tC;
                if (t < tc) continue;
                double n = mix.MolAt(i);
                double dn = Math.Min(n, Math.Max(n * 0.06 * dt * Math.Min(1.5, 0.3 + (t - tc) / 60), 2e-6 * dt));
                var b = new MixBuilder();
                foreach (var (id, k) in r.products) if (SpeciesRegistry.Get(id) != null) b.Add(id, k * dn);
                // lấy bớt chất phản ứng, thêm sản phẩm rắn
                c.TakeExact(Solution.Of(0, Mix.Of((d.Id, dn))));
                if (!b.ToMix().IsEmpty) c.Add(Solution.Of(0, b.ToMix()));
                Note(c, "nhiet_phan:" + f, r.title, $"Nung nóng chất rắn trên {tc:0} °C.", Severity.Info, equation: r.eq, gap: 60f);
                if (r.gases != null)
                    foreach (var (gas, k) in r.gases)
                        if (part != null) RouteGas(c, part, gas, k * dn);
                        else Escape(c.transform.position, gas, k * dn, c);
                if (r.water > 0)
                {
                    var steam = Solution.Of((float)(r.water * dn * Volatility.WaterM), Mix.Empty);
                    if (part != null && TubeMouthUp(c))
                        Note(c, "mieng_ong_huong_len", "Miệng ống nghiệm hướng lên: nước ngưng chảy ngược xuống đáy nóng, ống dễ vỡ", "Kẹp ống nằm nghiêng, miệng hơi chúc xuống khi nung chất rắn.", Severity.Warning, gap: 20f);
                    RouteVapor(c, part, steam, 100f, r.water * dn);
                }
                break;  // mỗi bước một phản ứng
            }
        }

        static bool TubeMouthUp(LabContainer c) => c.mouth != null && c.mouth.position.y > c.transform.position.y + 0.005f;

        // =====================================================================

        void Note(LabContainer c, string key, string title, string detail, Severity sev, string hazard = null, string gas = null, float gap = 8f, string equation = null)
        {
            var e = new ChemEvent
            {
                Kind = sev >= Severity.Warning ? EventKind.Hazard : gas != null ? EventKind.Gas : EventKind.Reaction,
                Severity = sev, Key = "dong_chay:" + key, Title = title, Detail = detail, Hazard = hazard, GasId = gas, Equation = equation,
            };
            if (c != null) c.Announce(e, gap);
            string line = $"{Time.time:0.0}s {title}";
            if (!logStamp.TryGetValue(title, out var last) || Time.time - last > 15f)
            {
                logStamp[title] = Time.time;
                Log.Add(line);
                if (Log.Count > 300) Log.RemoveAt(0);
            }
        }
    }
}
