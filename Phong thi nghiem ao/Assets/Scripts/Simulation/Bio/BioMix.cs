using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Bio
{
    public enum BioPhase { Liquid, Powder, Tissue, Culture }

    /// <summary>Một chất / mẫu trong phòng thí nghiệm sinh học. Mã trùng với data/sinh/sinh_pham_mac_dinh.json.</summary>
    public sealed class BioMatter
    {
        public string id, name;
        public BioPhase phase;
        public float r, g, b;
        /// <summary>Độ đậm màu: thuốc nhuộm ít mà nhuộm cả ống, nước không màu.</summary>
        public float tint;
    }

    public static class BioMatterDb
    {
        public const string Water = "nuoc_cat", Yeast = "sp_men_banh_mi", Glucose = "sp_duong_glucose", H2O2 = "sp_h2o2_3", Lugol = "sp_lugol",
            Methylene = "sp_xanh_methylen_1", Nacl10 = "sp_nacl_10", Nacl09 = "sp_nacl_sinh_ly", Ethanol70 = "sp_con_70", ColdEthanol = "sp_con_96_lanh",
            Potato = "sp_khoai_tay", Liver = "sp_gan_ga", Banana = "sp_chuoi_chin", Milk = "sp_sua_tuoi", YogurtStarter = "sp_sua_chua_cai",
            Soap = "sp_nuoc_rua_tay_sat_khuan", ExpiredLysis = "sp_kit_ly_giai_het_han", Agarose = "sp_agarose_bot", Tae = "sp_dem_tae_50x",
            Bicarb = "sp_nahco3", Broth = "sp_ong_canh_lb_10ml", EColi = "sp_giong_e_coli", BSubtilis = "sp_giong_b_subtilis", Dh5a = "sp_te_bao_kha_bien_dh5a",
            Onion = "sp_hanh_tim", Tradescantia = "sp_la_thai_lai", Elodea = "sp_rong_duoi_cho", ImmersionOil = "sp_dau_soi_kinh",
            NfWater = "sp_nuoc_khong_nuclease";

        static readonly Dictionary<string, BioMatter> all = new Dictionary<string, BioMatter>();

        static void Def(string id, string name, BioPhase phase, float r, float g, float b, float tint)
            => all[id] = new BioMatter { id = id, name = name, phase = phase, r = r, g = g, b = b, tint = tint };

        static BioMatterDb()
        {
            Def(Water, "Nước cất", BioPhase.Liquid, 0.86f, 0.93f, 0.98f, 0.02f);
            Def(NfWater, "Nước không nuclease", BioPhase.Liquid, 0.86f, 0.93f, 0.98f, 0.02f);
            Def(Methylene, "Xanh methylen 1 %", BioPhase.Liquid, 0.05f, 0.25f, 0.8f, 40f);
            Def(Lugol, "Lugol", BioPhase.Liquid, 0.62f, 0.34f, 0.05f, 12f);
            Def(Nacl10, "NaCl 10 %", BioPhase.Liquid, 0.86f, 0.93f, 0.98f, 0.02f);
            Def(Nacl09, "NaCl 0,9 %", BioPhase.Liquid, 0.86f, 0.93f, 0.98f, 0.02f);
            Def(H2O2, "H₂O₂ 3 %", BioPhase.Liquid, 0.88f, 0.94f, 0.98f, 0.02f);
            Def(Ethanol70, "Cồn 70 %", BioPhase.Liquid, 0.88f, 0.94f, 0.98f, 0.02f);
            Def(ColdEthanol, "Cồn 96 % lạnh", BioPhase.Liquid, 0.88f, 0.94f, 0.98f, 0.02f);
            Def(ImmersionOil, "Dầu soi kính", BioPhase.Liquid, 0.95f, 0.88f, 0.55f, 1f);
            Def(Tae, "Đệm TAE", BioPhase.Liquid, 0.88f, 0.93f, 0.95f, 0.02f);
            Def(Soap, "Xà phòng rửa", BioPhase.Liquid, 0.45f, 0.75f, 0.85f, 0.6f);
            Def(ExpiredLysis, "Dung dịch ly giải (quá hạn)", BioPhase.Liquid, 0.8f, 0.78f, 0.6f, 0.4f);
            Def(Milk, "Sữa tươi", BioPhase.Liquid, 0.97f, 0.95f, 0.88f, 8f);
            Def(YogurtStarter, "Sữa chua cái", BioPhase.Liquid, 0.97f, 0.96f, 0.9f, 8f);
            Def(Broth, "Canh thang LB", BioPhase.Liquid, 0.9f, 0.78f, 0.45f, 0.8f);
            Def(EColi, "Giống E. coli K-12", BioPhase.Culture, 0.85f, 0.8f, 0.6f, 0.5f);
            Def(BSubtilis, "Giống B. subtilis", BioPhase.Culture, 0.85f, 0.8f, 0.6f, 0.5f);
            Def(Dh5a, "E. coli DH5α", BioPhase.Culture, 0.85f, 0.8f, 0.6f, 0.5f);
            Def(Yeast, "Men bánh mì", BioPhase.Powder, 0.8f, 0.68f, 0.45f, 2f);
            Def(Glucose, "Đường glucose", BioPhase.Powder, 0.97f, 0.97f, 0.95f, 0.05f);
            Def(Bicarb, "NaHCO₃", BioPhase.Powder, 0.97f, 0.97f, 0.97f, 0.05f);
            Def(Agarose, "Bột agarose", BioPhase.Powder, 0.96f, 0.95f, 0.9f, 0.3f);
            Def(Potato, "Khoai tây", BioPhase.Tissue, 0.9f, 0.85f, 0.55f, 3f);
            Def(Liver, "Gan gà", BioPhase.Tissue, 0.42f, 0.13f, 0.1f, 8f);
            Def(Banana, "Chuối chín", BioPhase.Tissue, 0.96f, 0.9f, 0.62f, 3f);
            Def(Onion, "Vảy hành tím", BioPhase.Tissue, 0.75f, 0.45f, 0.65f, 3f);
            Def(Tradescantia, "Lá thài lài tía", BioPhase.Tissue, 0.45f, 0.2f, 0.45f, 3f);
            Def(Elodea, "Rong đuôi chó", BioPhase.Tissue, 0.2f, 0.55f, 0.2f, 3f);
            Def("sp_kit_pcr_mastermix", "Taq 2X MasterMix", BioPhase.Liquid, 0.9f, 0.9f, 0.95f, 0.3f);
            Def("sp_cap_moi_pcr", "Cặp mồi PCR", BioPhase.Liquid, 0.9f, 0.9f, 0.95f, 0.05f);
            Def("sp_dna_ladder_100bp", "Thang chuẩn ADN 100 bp", BioPhase.Liquid, 0.5f, 0.5f, 0.9f, 2f);
            Def("sp_dem_nap_mau_6x", "Đệm nạp mẫu 6X", BioPhase.Liquid, 0.3f, 0.35f, 0.85f, 8f);
            Def("sp_nhuom_adn_gelgreen", "GelGreen", BioPhase.Liquid, 0.4f, 0.85f, 0.4f, 8f);
            Def("sp_plasmid_puc19", "Plasmid pUC19", BioPhase.Liquid, 0.9f, 0.9f, 0.95f, 0.05f);
            Def("sp_enzyme_ecori", "Enzyme EcoRI", BioPhase.Liquid, 0.9f, 0.9f, 0.95f, 0.05f);
            Def("sp_enzyme_bamhi", "Enzyme BamHI", BioPhase.Liquid, 0.9f, 0.9f, 0.95f, 0.05f);
            Def("sp_ligaza_t4", "ADN ligaza T4", BioPhase.Liquid, 0.9f, 0.9f, 0.95f, 0.05f);
            Def("sp_bo_nhuom_gram", "Bộ thuốc nhuộm Gram", BioPhase.Liquid, 0.45f, 0.1f, 0.6f, 6f);
        }

        public static BioMatter Get(string id) => id != null && all.TryGetValue(id, out var m) ? m : null;
        public static string Name(string id) => Get(id)?.name ?? id;
        public static IEnumerable<BioMatter> All => all.Values;
    }

    /// <summary>
    /// Thứ đang có trong một bình: lượng từng chất (mL với chất lỏng, g với chất rắn) và trạng thái sinh ra trong lúc làm.
    /// Không có "bước": kết quả chỉ phụ thuộc trong bình có gì, ở nhiệt độ nào, đã bao lâu, nên làm theo thứ tự nào cũng được
    /// miễn là ngoài đời cũng ra như vậy.
    /// </summary>
    public sealed class BioMix
    {
        public readonly Dictionary<string, double> amounts = new Dictionary<string, double>();

        /// <summary>Enzyme catalase đã biến tính (đun quá 65 °C). Không hồi phục khi nguội.</summary>
        public bool catalaseDead;
        /// <summary>Tế bào men, vi khuẩn sữa chua đã chết vì nóng.</summary>
        public bool yeastDead, starterDead;
        public bool bacteriaDead;
        /// <summary>Mô đã nghiền nhỏ: phản ứng nhanh hơn, tế bào dễ vỡ.</summary>
        public bool ground;
        /// <summary>Đã mở nắp ngoài vùng vô trùng khi đang có môi trường nuôi cấy.</summary>
        public bool contaminated;
        /// <summary>Bọt (0..1), khí O₂ và CO₂ đã sinh (mL).</summary>
        public double foam, o2Ml, co2Ml, o2Recent;
        public double ethanolMl;
        /// <summary>Tiến độ ly giải tế bào (0..1) và lượng sợi ADN đã kết tủa (0..1).</summary>
        public double lysis, dnaThreads;
        /// <summary>Tiến độ đông sữa chua (0..1), axit lactic sinh ra làm giảm pH.</summary>
        public double yogurt;
        /// <summary>Độ đục vi khuẩn OD600.</summary>
        public double od = 0;
        public bool iodineStarch;
        public bool agaroseMelted, gelSet;

        public double Total => amounts.Values.Sum();
        public bool IsEmpty => Total < 1e-6;
        public double Of(string id) => amounts.TryGetValue(id, out var v) ? v : 0;
        public bool Has(string id, double min = 1e-4) => Of(id) >= min;

        public double LiquidMl
        {
            get
            {
                double s = 0;
                foreach (var kv in amounts)
                {
                    var m = BioMatterDb.Get(kv.Key);
                    if (m == null || m.phase == BioPhase.Liquid || m.phase == BioPhase.Culture) s += kv.Value;
                }
                return s;
            }
        }

        public void Add(string id, double amount)
        {
            if (amount <= 0) return;
            // mẫu sống mới cho vào: bình chưa có loại đó thì không kế thừa trạng thái "đã chết"
            if ((id == BioMatterDb.Potato || id == BioMatterDb.Liver) && !Has(BioMatterDb.Potato) && !Has(BioMatterDb.Liver)) catalaseDead = false;
            if (id == BioMatterDb.Yeast && !Has(id)) yeastDead = false;
            if (id == BioMatterDb.YogurtStarter && !Has(id)) starterDead = false;
            if ((id == BioMatterDb.EColi || id == BioMatterDb.BSubtilis || id == BioMatterDb.Dh5a) && !BioRules.HasBacteria(this)) bacteriaDead = false;
            amounts[id] = Of(id) + amount;
        }

        /// <summary>Lấy một phần đồng đều (đã khuấy) sang bình khác: trạng thái đi theo chất.</summary>
        public BioMix Take(double amount)
        {
            var r = new BioMix();
            double total = Total;
            if (total <= 1e-9 || amount <= 0) return r;
            double f = Math.Min(1.0, amount / total);
            foreach (var k in amounts.Keys.ToList())
            {
                double v = amounts[k] * f;
                r.amounts[k] = v;
                amounts[k] -= v;
                if (amounts[k] < 1e-7) amounts.Remove(k);
            }
            CopyStateTo(r, f);
            o2Ml *= 1 - f; co2Ml *= 1 - f; ethanolMl *= 1 - f;
            return r;
        }

        /// <summary>Chỉ lấy phần lỏng, chất rắn ở lại (lọc, rót gạn, ống nhỏ giọt không hút được mô).</summary>
        public BioMix TakeLiquid(double amount)
        {
            var r = new BioMix();
            double liq = LiquidMl;
            if (liq <= 1e-9 || amount <= 0) return r;
            double f = Math.Min(1.0, amount / liq);
            foreach (var k in amounts.Keys.ToList())
            {
                var m = BioMatterDb.Get(k);
                if (m != null && m.phase != BioPhase.Liquid && m.phase != BioPhase.Culture) continue;
                double v = amounts[k] * f;
                r.amounts[k] = v;
                amounts[k] -= v;
                if (amounts[k] < 1e-7) amounts.Remove(k);
            }
            CopyStateTo(r, f);
            return r;
        }

        void CopyStateTo(BioMix r, double f)
        {
            r.catalaseDead = catalaseDead; r.yeastDead = yeastDead; r.starterDead = starterDead; r.bacteriaDead = bacteriaDead;
            r.ground = ground; r.contaminated = contaminated;
            r.foam = foam; r.lysis = lysis; r.dnaThreads = dnaThreads; r.yogurt = yogurt; r.od = od;
            r.iodineStarch = iodineStarch; r.agaroseMelted = agaroseMelted; r.gelSet = gelSet;
            r.o2Ml = o2Ml * f; r.co2Ml = co2Ml * f; r.ethanolMl = ethanolMl * f;
        }

        /// <summary>Đổ thêm vào: trạng thái sinh học gộp lại theo tỉ lệ thể tích.</summary>
        public void Merge(BioMix o)
        {
            if (o == null || o.IsEmpty) return;
            double a = Total, b = o.Total, wa = a / (a + b), wb = 1 - wa;
            // còn nguồn sống ở một bên thì hỗn hợp vẫn sống
            catalaseDead = (catalaseDead || !Has(BioMatterDb.Potato) && !Has(BioMatterDb.Liver)) && (o.catalaseDead || !o.Has(BioMatterDb.Potato) && !o.Has(BioMatterDb.Liver));
            yeastDead = (yeastDead || !Has(BioMatterDb.Yeast)) && (o.yeastDead || !o.Has(BioMatterDb.Yeast));
            starterDead = (starterDead || !Has(BioMatterDb.YogurtStarter)) && (o.starterDead || !o.Has(BioMatterDb.YogurtStarter));
            bacteriaDead = (bacteriaDead || !BioRules.HasBacteria(this)) && (o.bacteriaDead || !BioRules.HasBacteria(o));
            foreach (var kv in o.amounts) amounts[kv.Key] = Of(kv.Key) + kv.Value;
            ground |= o.ground; contaminated |= o.contaminated;
            foam = Math.Max(foam, o.foam);
            lysis = lysis * wa + o.lysis * wb; dnaThreads = Math.Max(dnaThreads, o.dnaThreads);
            yogurt = yogurt * wa + o.yogurt * wb; od = od * wa + o.od * wb;
            iodineStarch |= o.iodineStarch; agaroseMelted |= o.agaroseMelted;
            o2Ml += o.o2Ml; co2Ml += o.co2Ml; ethanolMl += o.ethanolMl;
        }

        public void Clear()
        {
            amounts.Clear();
            catalaseDead = yeastDead = starterDead = bacteriaDead = ground = contaminated = iodineStarch = agaroseMelted = gelSet = false;
            foam = o2Ml = co2Ml = o2Recent = ethanolMl = lysis = dnaThreads = yogurt = od = 0;
        }

        /// <summary>Tên ngắn những gì đang có, nhiều nhất trước.</summary>
        public string Describe()
        {
            if (IsEmpty) return "trống";
            var parts = amounts.OrderByDescending(kv => kv.Value).Take(4).Select(kv =>
            {
                var m = BioMatterDb.Get(kv.Key);
                string unit = m != null && (m.phase == BioPhase.Powder || m.phase == BioPhase.Tissue) ? "g" : "mL";
                return $"{BioMatterDb.Name(kv.Key)} {kv.Value:0.##} {unit}";
            });
            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// Luật sinh học chạy trên BioMix theo thời gian. Giây thật nhân với timeScale (tủ ấm tua nhanh) để quá trình hàng giờ vẫn
    /// xem được trong buổi học. Hiện tượng lấy từ các mô hình sẵn có (Catalase, Fermentation), không rẽ nhánh theo bài.
    /// </summary>
    public static class BioRules
    {
        /// <summary>Một lần chạy luật. events nhận thông báo hiện tượng mới xuất hiện (mỗi loại một lần).</summary>
        public static void Step(BioMix m, double tempC, double dtSeconds, double timeScale, List<string> events)
        {
            if (m.IsEmpty) return;
            double hours = dtSeconds * timeScale / 3600.0;

            // Nhiệt làm biến tính, giết tế bào
            if (tempC >= Catalase.DenatureTempC && !m.catalaseDead && (m.Has(BioMatterDb.Potato) || m.Has(BioMatterDb.Liver)))
            {
                m.catalaseDead = true;
                events?.Add("Mẫu mô bị đun quá 65 °C: enzyme catalase đã biến tính.");
            }
            if (tempC >= 60 && !m.yeastDead && m.Has(BioMatterDb.Yeast)) { m.yeastDead = true; events?.Add("Nhiệt độ trên 60 °C làm chết tế bào nấm men."); }
            if (tempC >= 55 && !m.starterDead && m.Has(BioMatterDb.YogurtStarter)) { m.starterDead = true; events?.Add("Quá nóng: vi khuẩn sữa chua đã chết."); }
            if (tempC >= 50 && !m.bacteriaDead && HasBacteria(m)) { m.bacteriaDead = true; events?.Add("Quá nóng: vi khuẩn trong ống đã chết."); }
            if (m.Has(BioMatterDb.Ethanol70, m.LiquidMl * 0.3) || m.Has(BioMatterDb.ColdEthanol, m.LiquidMl * 0.3))
            {
                if (HasBacteria(m) && !m.bacteriaDead) { m.bacteriaDead = true; events?.Add("Nồng độ cồn cao đã diệt vi khuẩn trong ống."); }
                if (m.Has(BioMatterDb.Yeast) && !m.yeastDead) { m.yeastDead = true; events?.Add("Nồng độ cồn cao đã diệt nấm men."); }
            }

            CatalaseStep(m, tempC, dtSeconds, events);
            IodineStep(m, tempC, events);
            FermentStep(m, tempC, hours, events);
            YogurtStep(m, tempC, hours, events);
            DnaStep(m, tempC, dtSeconds, events);
            GrowthStep(m, tempC, hours, events);
            AgaroseStep(m, tempC, events);

            m.foam = Math.Max(0, m.foam - dtSeconds * 0.05);
            m.o2Recent = Math.Max(0, m.o2Recent - dtSeconds * 0.4);
        }

        public static bool HasBacteria(BioMix m) => m.Has(BioMatterDb.EColi) || m.Has(BioMatterDb.BSubtilis) || m.Has(BioMatterDb.Dh5a);

        static void CatalaseStep(BioMix m, double t, double dt, List<string> ev)
        {
            double tissue = m.Of(BioMatterDb.Potato) * 0.4 + m.Of(BioMatterDb.Liver);  // gan giàu catalase hơn khoai tây
            double h2o2 = m.Of(BioMatterDb.H2O2);
            if (tissue <= 0 || h2o2 <= 1e-4) return;
            double act = Catalase.ActivityFactor(t, m.catalaseDead) * (m.ground ? 3 : 1);
            if (act <= 0.001) return;
            double used = Math.Min(h2o2, act * tissue * 0.6 * dt);
            m.amounts[BioMatterDb.H2O2] = h2o2 - used;
            m.Add(BioMatterDb.Water, used);
            double o2 = used * 10;  // H₂O₂ 3 % giải phóng khoảng 10 thể tích O₂
            bool first = m.o2Ml < 0.01;
            m.o2Ml += o2;
            m.o2Recent = Math.Min(20, m.o2Recent + o2);
            m.foam = Math.Min(1, m.foam + used * 4);
            if (first) ev?.Add("Sủi bọt khí: catalase đang phân hủy H₂O₂ thành nước và O₂.");
        }

        static void IodineStep(BioMix m, double t, List<string> ev)
        {
            bool starch = m.Has(BioMatterDb.Potato);
            bool on = starch && m.Has(BioMatterDb.Lugol) && t < 70;
            if (on && !m.iodineStarch) ev?.Add("Xuất hiện màu xanh tím đen: Lugol đã gặp tinh bột.");
            else if (m.iodineStarch && t >= 70) ev?.Add("Đun nóng làm mất màu xanh tím: phức iốt – tinh bột tách ra (nguội lại sẽ hiện lại).");
            m.iodineStarch = on;
        }

        static void FermentStep(BioMix m, double t, double hours, List<string> ev)
        {
            double yeast = m.Of(BioMatterDb.Yeast), sugar = m.Of(BioMatterDb.Glucose);
            if (yeast <= 0 || sugar <= 1e-4 || m.yeastDead || m.LiquidMl < 0.1 || t < 5) return;
            // tốc độ theo nhiệt độ của mô hình Fermentation: đo chênh lệch CO₂ sau một bước thời gian
            double perGram = Fermentation.Co2MolesAt(1.0, t, 1.0, true);
            double dSugar = Math.Min(sugar, perGram * 90 * Math.Min(1, yeast / Math.Max(0.1, sugar)) * hours);  // 90 g đường cho 1 mol CO₂ (2 CO₂ / 180 g)
            if (dSugar <= 0) return;
            bool first = m.co2Ml < 0.01;
            m.amounts[BioMatterDb.Glucose] = sugar - dSugar;
            m.co2Ml += dSugar / 90 * 24000;
            m.ethanolMl += dSugar * 0.51 / 0.789;
            m.foam = Math.Min(0.4, m.foam + dSugar * 0.1);
            if (first) ev?.Add("Bọt CO₂ xuất hiện: nấm men đang lên men đường.");
        }

        static void YogurtStep(BioMix m, double t, double hours, List<string> ev)
        {
            if (!m.Has(BioMatterDb.Milk) || !m.Has(BioMatterDb.YogurtStarter) || m.starterDead) return;
            double rate = t < 20 ? 0.01 : t < 30 ? 0.05 : t <= 46 ? 1 / 6.0 : 0.05;  // đông sau khoảng 6 giờ ở 37–45 °C
            bool wasSet = m.yogurt >= 1;
            m.yogurt = Math.Min(1, m.yogurt + rate * hours);
            if (!wasSet && m.yogurt >= 1) ev?.Add("Sữa đã đông thành sữa chua, pH giảm khoảng 4,5.");
        }

        static void DnaStep(BioMix m, double t, double dt, List<string> ev)
        {
            bool tissue = m.Has(BioMatterDb.Banana) || m.Has(BioMatterDb.Onion);
            double fresh = m.Of(BioMatterDb.Soap), old = m.Of(BioMatterDb.ExpiredLysis);
            if (tissue && fresh + old > 0.01 && m.Has(BioMatterDb.Nacl10, 0.01) || tissue && fresh + old > 0.01 && m.Has(BioMatterDb.Nacl09, 0.01))
            {
                double speed = (m.ground ? 0.08 : 0.01) * (fresh > 0 ? 1 : 0.25);
                double before = m.lysis;
                m.lysis = Math.Min(1, m.lysis + speed * dt);
                if (before < 0.5 && m.lysis >= 0.5) ev?.Add("Màng tế bào đã bị phá, ADN đi vào dịch.");
            }
            if (m.lysis > 0.05 && m.Has(BioMatterDb.ColdEthanol, 0.1))
            {
                double cold = t <= 10 ? 1 : t <= 25 ? 0.4 : 0.1;
                double target = m.lysis * cold;
                if (m.dnaThreads < 0.01 && target > 0.01) ev?.Add("Sợi trắng nổi lên ở lớp cồn: đó là ADN kết tủa.");
                m.dnaThreads = Math.Max(m.dnaThreads, target);
            }
        }

        static void GrowthStep(BioMix m, double t, double hours, List<string> ev)
        {
            if (!HasBacteria(m) || m.bacteriaDead || !m.Has(BioMatterDb.Broth, 0.1)) return;
            double opt = t < 10 ? 0 : t < 25 ? 0.15 : t <= 40 ? 1 : 0.2;
            double before = m.od;
            double od = Math.Max(m.od, 0.02);
            m.od = Math.Min(2.5, od * Math.Pow(2, opt * hours * 2));  // nhân đôi mỗi khoảng 30 phút ở 37 °C
            if (before < 0.3 && m.od >= 0.3) ev?.Add("Canh thang đã đục: vi khuẩn đang sinh trưởng.");
        }

        static void AgaroseStep(BioMix m, double t, List<string> ev)
        {
            if (!m.Has(BioMatterDb.Agarose) || m.LiquidMl < 0.5) return;
            if (!m.agaroseMelted && t >= 90) { m.agaroseMelted = true; ev?.Add("Agarose đã tan hết, dung dịch trong."); }
            if (m.agaroseMelted && !m.gelSet && t < 40) { m.gelSet = true; ev?.Add("Agarose đã đông thành gel."); }
            if (m.gelSet && t >= 90) m.gelSet = false;
        }

        /// <summary>pH ước lượng để giấy chỉ thị, bút đo đọc được.</summary>
        public static double Ph(BioMix m)
        {
            if (m.IsEmpty) return 7;
            double liq = Math.Max(0.01, m.LiquidMl), ph = 7;
            if (m.Has(BioMatterDb.Bicarb)) ph = 8.3;
            if (m.Has(BioMatterDb.H2O2)) ph = Math.Min(ph, 5.5);
            if (m.Has(BioMatterDb.Lugol)) ph = Math.Min(ph, 5.5);
            if (m.Has(BioMatterDb.Milk)) ph = 6.6 - 2.1 * m.yogurt;
            if (m.Has(BioMatterDb.Soap)) ph = Math.Max(ph, 8);
            if (m.co2Ml > 1 && m.ethanolMl > 0) ph = Math.Min(ph, 5);
            if (m.Has(BioMatterDb.Tae)) ph = 8.3;
            return ph;
        }

        /// <summary>Màu dung dịch (r, g, b) và độ đục (0 trong, 1 đặc) tính từ thành phần.</summary>
        public static (float r, float g, float b, float a) Look(BioMix m)
        {
            if (m.IsEmpty) return (1, 1, 1, 0);
            double wr = 0, wg = 0, wb = 0, w = 0, total = Math.Max(0.001, m.Total);
            foreach (var kv in m.amounts)
            {
                var d = BioMatterDb.Get(kv.Key);
                if (d == null) continue;
                double k = kv.Value / total * d.tint + 1e-3;
                wr += d.r * k; wg += d.g * k; wb += d.b * k; w += k;
            }
            float r = (float)(wr / w), g = (float)(wg / w), b = (float)(wb / w);
            float a = 0.35f + (float)Math.Min(0.6, w * 0.25);
            if (m.iodineStarch) { r = 0.08f; g = 0.06f; b = 0.3f; a = 0.92f; }
            if (m.yogurt > 0.5) a = 0.98f;
            if (m.od > 0.1) { r = r * 0.8f + 0.15f; g = g * 0.8f + 0.14f; b = b * 0.8f + 0.1f; a = Math.Max(a, 0.4f + (float)Math.Min(0.5, m.od * 0.4)); }
            return (r, g, b, a);
        }

        /// <summary>Tóm tắt hiện tượng đang thấy, để hiện khi trỏ vào bình.</summary>
        public static string Phenomena(BioMix m)
        {
            var s = new List<string>();
            if (m.foam > 0.1) s.Add("đang sủi bọt");
            if (m.iodineStarch) s.Add("màu xanh tím đen");
            if (m.yogurt >= 1) s.Add("đã đông");
            else if (m.yogurt > 0.1) s.Add($"đang đông {m.yogurt * 100:0} %");
            if (m.dnaThreads > 0.01) s.Add("có sợi ADN trắng");
            if (m.od > 0.1) s.Add($"đục (OD600 ≈ {m.od:0.00})");
            if (m.gelSet) s.Add("gel đã đông");
            else if (m.agaroseMelted) s.Add("agarose đã tan");
            if (m.co2Ml > 1) s.Add($"CO₂ {m.co2Ml:0} mL");
            return string.Join(", ", s);
        }
    }
}
