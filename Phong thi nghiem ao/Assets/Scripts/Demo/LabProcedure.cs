using System.Collections.Generic;
using System.Globalization;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Bài chuẩn độ do người dùng tự chọn (không bắt buộc). Mặc định phòng lab ở chế độ tự do: bàn chuẩn độ
    /// vẫn bày sẵn dung dịch để ai muốn thì tự làm. Khi bắt đầu bài (Begin), người dùng chọn chất cần chuẩn,
    /// dung dịch chuẩn, chỉ thị và nồng độ (ẩn hoặc tự đặt); thanh các bước theo dõi từ trạng thái thật của dụng cụ:
    /// lắp buret → nạp dung dịch chuẩn → đọc V đầu → lấy 25,00 ml mẫu bằng pipet → nhỏ chỉ thị → chuẩn độ tới
    /// điểm cuối bền 30 s → đọc V cuối → tính nồng độ. Mọi sai sót đều đi vào kết quả và được chỉ ra.
    /// </summary>
    public class LabProcedure : MonoBehaviour
    {
        public enum Tone { Info, Good, Warn }
        public enum Step { LapBuret, NapNaOH, DocVDau, LayHCl, ChiThi, ChuanDo, DocVCuoi, Xong }

        /// <summary>Cấu hình một bài chuẩn độ.</summary>
        public struct Config
        {
            public int analyteStt, titrantStt, indicatorStt;
            public float titrantC, analyteC;          // analyteC <= 0: ẩn (ngẫu nhiên)
            public string analyteShort, titrantShort, indicatorName;
            public string analyteKey, titrantKey;     // loài theo dõi lượng chất (Cl⁻, Na⁺, K⁺...)
            public float analyteEq, titrantEq;        // đương lượng trên 1 mol (H⁺, OH⁻ hoặc electron)
            public bool redox;                        // KMnO₄ tự chỉ thị

            public static Config Default => Make("HCl", "NaOH", "pp", 0);

            /// <summary>Tạo cấu hình từ tên ngắn: chất cần chuẩn, dung dịch chuẩn, chỉ thị (pp, mo, btb, none).</summary>
            public static Config Make(string analyte, string titrant, string indicator, float analyteC)
            {
                var c = new Config { analyteC = analyteC };
                switch (analyte)
                {
                    case "H2SO4": c.analyteStt = 102; c.analyteKey = "SO4"; c.analyteEq = 2; break;
                    case "CH3COOH": c.analyteStt = 505; c.analyteKey = "Ac"; c.analyteEq = 1; break;
                    case "HNO3": c.analyteStt = 104; c.analyteKey = "NO3-"; c.analyteEq = 1; break;
                    case "H2C2O4": c.analyteStt = 509; c.analyteKey = "Ox"; c.analyteEq = 2; break;
                    case "NaOH": c.analyteStt = 124; c.analyteKey = "Na+"; c.analyteEq = 1; break;
                    case "KOH": c.analyteStt = 125; c.analyteKey = "K+"; c.analyteEq = 1; break;
                    case "NH3": c.analyteStt = 143; c.analyteKey = "NH3"; c.analyteEq = 1; break;
                    case "FeSO4": c.analyteStt = 251; c.analyteKey = "SO4"; c.analyteEq = 1; break;
                    default: c.analyteStt = 98; c.analyteKey = "Cl-"; c.analyteEq = 1; analyte = "HCl"; break;
                }
                switch (titrant)
                {
                    case "HCl": c.titrantStt = 98; c.titrantKey = "Cl-"; c.titrantEq = 1; c.titrantC = 0.1f; break;
                    case "KMnO4": c.titrantStt = 338; c.titrantKey = "K+"; c.titrantEq = 5; c.titrantC = 0.02f; c.redox = true; break;
                    default: c.titrantStt = 124; c.titrantKey = "Na+"; c.titrantEq = 1; c.titrantC = 0.1f; titrant = "NaOH"; break;
                }
                c.analyteShort = analyte;
                c.titrantShort = titrant;
                if (c.redox) { indicator = "none"; if (analyte == "H2C2O4") c.analyteEq = 2; }
                switch (indicator)
                {
                    case "mo": c.indicatorStt = 395; c.indicatorName = "metyl da cam"; break;
                    case "btb": c.indicatorStt = 397; c.indicatorName = "bromothymol xanh"; break;
                    case "none": c.indicatorStt = 0; c.indicatorName = "KMnO₄ tự chỉ thị"; break;
                    default: c.indicatorStt = 394; c.indicatorName = "phenolphtalein"; break;
                }
                return c;
            }

            public bool TitrantIsBase => titrantShort == "NaOH";
        }

        public BuretStopcock buret;
        public PlaceSocket clampSocket;
        public PlaceSocket underSocket;
        public LabContainer flask;
        public LabContainer hclStock;
        public LabContainer naohStock;
        public LabContainer wasteBeaker;
        public Pipette pipette;
        public MeniscusReading reading;
        public SwirlDetector swirl;
        public Dropper indicatorDropper;
        public TextMesh analyteLabel, titrantLabel;

        public float naohConcentration = 0.1f;
        public float nominalAcidMl = 25f;
        public Vector2 acidConcentrationRange = new Vector2(0.08f, 0.12f);
        public int randomSeed;

        public struct Result
        {
            public float initialRead, finalRead, volumeRead, concentrationRead, concentrationTrue, errorPercent;
            public float acidActualMl, parallaxInitial, parallaxFinal, overshootDrops;
            public bool noIndicator, contaminated, notMixedWell, hiddenConcentration;
        }

        /// <summary>Đang làm bài chuẩn độ (thanh bước, số liệu, kết quả hiện lên). Tắt = thí nghiệm tự do.</summary>
        public bool Active { get; private set; }
        public Config Cfg { get; private set; } = Config.Default;
        public string[] StepNames { get; private set; } = new string[7];
        public Step Current { get; private set; }
        public bool[] Done { get; } = new bool[7];
        public float? InitialRead { get; private set; }
        public float? FinalRead { get; private set; }
        public Result? LastResult { get; private set; }
        public float TrueAcidConcentration { get; private set; }
        public List<Vector2> Curve { get; } = new List<Vector2>();
        public int CurveVersion { get; private set; }
        public string Message => LabMessages.Message;
        /// <summary>Điểm cuối (đổi màu chỉ thị) giữ liên tục bao lâu (s).</summary>
        public float EndpointPersistS { get; private set; }
        /// <summary>Thể tích dung dịch chuẩn tương ứng điểm tương đương với lượng mẫu đã cho vào bình (ml).</summary>
        public float EquivalenceMl => (float)(analyteAdded * Cfg.analyteEq / (Cfg.titrantC * Cfg.titrantEq) * 1000.0);
        /// <summary>Dung dịch chuẩn đã vào bình nón kể từ lúc đọc V đầu (ml).</summary>
        public float NaohIntoFlaskMl => (float)((flask.Total.Mix.Get(Cfg.titrantKey) - titrantAtInitial) / Cfg.titrantC * 1000.0);

        double titrantAtInitial, analyteAdded;
        float initialTrue, lastCurveV = -1f, dripTime, dripSwirlTime;
        bool contaminated, endpointAnnounced;
        Grabbable[] grabbables;

        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
        public static string F(float v, string fmt) => v.ToString(fmt, Vi);

        void Start()
        {
            grabbables = Khkt.Rendering.ZoneRoot.FindAll<Grabbable>();
            flask.Added += OnFlaskAdded;
            NewRun();
        }

        void OnFlaskAdded(Solution s) => analyteAdded += s.Mix.Get(Cfg.analyteKey);

        /// <summary>Bắt đầu bài chuẩn độ theo cấu hình người dùng chọn.</summary>
        public void Begin(Config cfg)
        {
            Cfg = cfg;
            Active = true;
            NewRun();
        }

        /// <summary>Thôi làm bài (về thí nghiệm tự do), dụng cụ giữ nguyên.</summary>
        public void Stop()
        {
            Active = false;
            Say("Chế độ thí nghiệm tự do", Tone.Info);
        }

        public void NewRun()
        {
            var rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            float c = Cfg.analyteC > 0 ? Cfg.analyteC : Mathf.Lerp(acidConcentrationRange.x, acidConcentrationRange.y, (float)rng.NextDouble());
            TrueAcidConcentration = Mathf.Round(c * 10000f) / 10000f;
            naohConcentration = Cfg.titrantC;
            var cat = ChemicalCatalog.Instance;
            if (cat != null && cat.Get(Cfg.analyteStt) != null)
            {
                var a = cat.Make(cat.Get(Cfg.analyteStt), 120f, TrueAcidConcentration);
                if (Cfg.analyteShort == "FeSO4") a += Solution.Of(0, Khkt.Simulation.Chem.Mix.Of(("SO4", 0.12 * 0.5)));  // mẫu Fe²⁺ đã axit hóa bằng H₂SO₄
                hclStock.SetContents(a);
                naohStock.SetContents(cat.Make(cat.Get(Cfg.titrantStt), 400f, Cfg.titrantC));
            }
            else
            {
                hclStock.SetContents(Solution.HCl(120f, TrueAcidConcentration));
                naohStock.SetContents(Solution.NaOH(400f, naohConcentration));
            }
            hclStock.label = "Cốc " + Cfg.analyteShort;
            naohStock.label = "Chai " + Cfg.titrantShort;
            if (analyteLabel != null) analyteLabel.text = Pretty(Cfg.analyteShort);
            if (titrantLabel != null) titrantLabel.text = Pretty(Cfg.titrantShort) + "\n" + F(Cfg.titrantC, "0.0#") + " M";
            if (indicatorDropper != null) indicatorDropper.catalogStt = Cfg.indicatorStt == 394 ? 0 : Cfg.indicatorStt;
            flask.SetContents(default);
            buret.container.SetContents(default);
            buret.Close();
            buret.ResetCounters();
            pipette.container.SetContents(default);
            if (wasteBeaker != null) wasteBeaker.SetContents(default);
            analyteAdded = 0;

            var hand = FindAnyObjectByType<HandController>();
            if (hand != null && hand.Held != null) hand.ReturnHome();
            foreach (var g in grabbables)
            {
                if (g == null || g.IsHeld || !g.transform.IsChildOf(transform.root) || g.GetComponent<ChemicalBottle>() != null) continue;
                if (!IsTitrationTool(g)) continue;
                g.SetSocket(null);
                if (g.HomeSocket != null) g.HomeSocket.Put(g);
                else g.transform.SetPositionAndRotation(g.HomePosition, g.HomeRotation);
            }

            StepNames = new[] { "Lắp buret", "Nạp " + Cfg.titrantShort, "Đọc V đầu", "Lấy " + Cfg.analyteShort, Cfg.redox ? "Axit hóa" : "Chỉ thị", "Chuẩn độ", "Đọc V cuối" };
            InitialRead = FinalRead = null;
            LastResult = null;
            for (int i = 0; i < Done.Length; i++) Done[i] = false;
            contaminated = endpointAnnounced = wasPastEndpointPink = false;
            titrantAtInitial = 0;
            dripTime = dripSwirlTime = 0;
            EndpointPersistS = 0;
            Curve.Clear();
            lastCurveV = -1f;
            CurveVersion++;
            if (Active) Say($"Bài mới: xác định nồng độ {Cfg.analyteShort} bằng {Cfg.titrantShort} {F(Cfg.titrantC, "0.0#")} M", Tone.Info);
        }

        bool IsTitrationTool(Grabbable g)
        {
            Transform set = buret.transform.parent;
            return set != null && g.transform.IsChildOf(set);
        }

        static string Pretty(string s) => Khkt.Simulation.Chem.SpeciesRegistry.Pretty(s);

        public void Say(string text, Tone tone = Tone.Info) => LabMessages.Say(text, (LabMessages.Tone)(int)tone);

        bool PastEndpoint()
        {
            var t = flask.Total;
            if (t.volumeMl < 1f) return false;
            if (Cfg.redox) return t.Mix.Get("MnO4-") / (t.volumeMl / 1000.0) > 2e-5;
            double ph = flask.BulkPh;
            switch (Cfg.indicatorStt)
            {
                case 395: return Cfg.TitrantIsBase ? ph > 4.4 : ph < 3.9;
                case 397: return Cfg.TitrantIsBase ? ph > 7.4 : ph < 6.2;
                default: return Cfg.TitrantIsBase ? flask.contents.Pink > 0.05f : wasPastEndpointPink && flask.contents.Pink < 0.03f;
            }
        }

        bool wasPastEndpointPink;

        bool HasIndicator()
        {
            var t = flask.Total;
            if (Cfg.redox) return t.Mix.Get("SO4") > 1e-4;  // pemanganat cần môi trường axit (H₂SO₄)
            if (Cfg.indicatorStt == 394) return t.indicatorDrops >= 0.9f;
            string id = Cfg.indicatorStt == 395 ? "mo" : "btb";
            return t.Mix.Get(id) > 2e-5;
        }

        void Update()
        {
            if (!Active) return;
            var g = buret.GetComponent<Grabbable>();
            var b = buret.container.contents;
            if (flask.PinkVisible > 0.2f) wasPastEndpointPink = true;
            bool past = PastEndpoint();
            EndpointPersistS = past ? EndpointPersistS + Time.deltaTime : 0f;

            Done[0] |= clampSocket.Occupant == g;
            Done[1] |= Done[0] && b.Mix.Get(Cfg.titrantKey) > 0 && b.Mix.Get(Cfg.analyteKey) <= 1e-9 && buret.ReadingMl >= -0.02f && buret.ReadingMl <= 2f;
            Done[2] = InitialRead.HasValue;
            Done[3] |= analyteAdded > 0 && pipette.IsEmpty;
            Done[4] |= HasIndicator();
            if (EndpointPersistS >= 30f && Done[2]) Done[5] = true;
            Done[6] = FinalRead.HasValue;
            Current = Step.Xong;
            for (int i = 0; i < 7; i++) if (!Done[i]) { Current = (Step)i; break; }

            if (!InitialRead.HasValue && flask.Total.Mix.Get(Cfg.titrantKey) > 1e-7) contaminated = true;

            if (InitialRead.HasValue && !FinalRead.HasValue)
            {
                float v = NaohIntoFlaskMl;
                if (v - lastCurveV >= 0.05f || Curve.Count == 0)
                {
                    Curve.Add(new Vector2(v, (float)flask.BulkPh));
                    lastCurveV = v;
                    CurveVersion++;
                }
                if (buret.FlowMlPerS > 0)
                {
                    dripTime += Time.deltaTime;
                    if (swirl != null && swirl.Intensity > 0.3f) dripSwirlTime += Time.deltaTime;
                }
                if (!endpointAnnounced && EndpointPersistS >= 30f)
                {
                    endpointAnnounced = true;
                    Say("Màu chỉ thị bền 30 giây: đã tới điểm cuối. Khóa buret và đọc V cuối", Tone.Good);
                }
            }
        }

        /// <summary>Ghi số đọc buret (Enter). Lần đầu là V đầu, lần sau là V cuối.
        /// robotRead: số robot đọc khi làm mẫu (mắt ngang vạch, không lệch thị sai); bỏ trống thì đọc theo mắt người dùng.</summary>
        public void RecordReading(float? robotRead = null)
        {
            if (!Active) { Say("Chưa bắt đầu bài chuẩn độ: mở menu (Esc) để chọn bài", Tone.Info); return; }
            if (FinalRead.HasValue) { Say("Đã có kết quả. Nhấn R để làm lượt mới"); return; }
            if (!buret.mounted || !buret.HasLiquid) { Say("Buret chưa có dung dịch để đọc", Tone.Warn); return; }
            if (buret.Opening > 0.001f) { Say("Khóa buret trước khi đọc", Tone.Warn); return; }
            if (buret.ReadingMl < -0.02f) { Say("Mặt khum đang ở trên vạch 0: xả bớt vào cốc thải", Tone.Warn); return; }
            if (buret.ReadingMl > buret.lastMarkMl) { Say("Mặt khum ở dưới vạch 50: nạp thêm dung dịch", Tone.Warn); return; }

            float read = robotRead ?? reading.DisplayedReadingMl;
            if (!InitialRead.HasValue)
            {
                InitialRead = read;
                initialTrue = buret.ReadingMl;
                titrantAtInitial = flask.Total.Mix.Get(Cfg.titrantKey);
                buret.ResetCounters();
                Say($"V đầu = {F(read, "0.00")} ml", Tone.Good);
                return;
            }
            if (NaohIntoFlaskMl < 0.3f) { Say($"Chưa nhỏ {Cfg.titrantShort} vào bình nón", Tone.Warn); return; }

            FinalRead = read;
            float v = read - InitialRead.Value;
            float cRead = Cfg.titrantC * Cfg.titrantEq * v / (Cfg.analyteEq * nominalAcidMl);
            float acidMl = (float)(analyteAdded / TrueAcidConcentration * 1000.0);
            LastResult = new Result
            {
                initialRead = InitialRead.Value,
                finalRead = read,
                volumeRead = v,
                concentrationRead = cRead,
                concentrationTrue = TrueAcidConcentration,
                errorPercent = 100f * (cRead - TrueAcidConcentration) / TrueAcidConcentration,
                acidActualMl = acidMl,
                parallaxInitial = InitialRead.Value - initialTrue,
                parallaxFinal = read - buret.ReadingMl,
                overshootDrops = (NaohIntoFlaskMl - EquivalenceMl) / Solution.DropMl,
                noIndicator = !HasIndicator(),
                contaminated = contaminated,
                notMixedWell = dripTime > 2f && dripSwirlTime / dripTime < 0.5f,
                hiddenConcentration = Cfg.analyteC <= 0,
            };
            Say($"C({Cfg.analyteShort}) = {F(cRead, "0.0000")} M", Tone.Good);
        }
    }
}
