using System;
using System.Collections.Generic;
using System.Globalization;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Bộ quan sát: sau mỗi lần trộn, nếu bộ máy không ghi được hiện tượng rõ (kết tủa, khí, đổi màu...), tự so sánh
    /// trước / sau để mọi lần trộn đều có một thẻ đúng sự thật:
    ///   - phản ứng axit - bazơ (trung hòa) không có dấu hiệu nhìn thấy: nêu phương trình ion, pH trước / sau;
    ///   - màu đổi mà chưa có luật nào giải thích (chất chỉ thị, cromat ⇌ đicromat): nêu màu trước → sau;
    ///   - không phản ứng: nêu lý do (điều kiện phản ứng trao đổi ion, pha loãng, chất trơ).
    /// Không bao giờ nói "không phản ứng" khi thành phần đã đổi: khi đó luôn là thẻ phản ứng.
    /// </summary>
    public static class ChemObserver
    {
        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        static bool IsChemical(ChemEvent e)
        {
            string k = e.Key ?? "";
            if (k.StartsWith("mui:") || k == "nhiet" || k == "nhiet_lanh" || k == "soi") return false;
            return e.Kind != EventKind.Heat;
        }

        public static void Explain(Mix a, double mlA, float ppA, Mix s, double mlS, float ppS, Mix m, double ml, ChemEnv env, List<ChemEvent> events, int first)
        {
            bool aEmpty = a.IsEmpty && mlA < 0.002 && ppA <= 0;
            bool sEmpty = s.IsEmpty && ppS <= 0;
            if (aEmpty || (sEmpty && mlS < 0.002)) return;   // rót vào bình rỗng: chưa có gì để phản ứng
            bool chem = false;
            for (int i = first; i < events.Count; i++) if (IsChemical(events[i])) { chem = true; break; }
            FixAddedGas(s, mlS, events, first);
            string sig = Signature(s);
            bool gasAdded = mlS < 0.002 && IsGasOnly(s);
            // khí NH₃ gặp HCl đặc bốc khói: khói trắng NH₄Cl phía trên miệng ống
            if (gasAdded && s.Get("NH3") > 0 && mlA > 0.002 && a.Get("Cl-") / (mlA / 1000.0) > 5)
                events.Add(new ChemEvent { Kind = EventKind.Reaction, Key = "khoi:NH4Cl", Title = "Khói trắng NH₄Cl bay lên (NH₃ gặp hơi HCl)", Detail = "Nhận biết NH₃ hoặc HCl bằng khói trắng amoni clorua.", Equation = "NH₃ + HCl → NH₄Cl" });
            // khí tan vào nước (nước clo, nước sođa...): luôn nêu phương trình hòa tan
            if (gasAdded && IsOnlyWater(a)) { GasIntoWater(s, events, sig); return; }
            if (chem) return;
            // kim loại, MnO₂, phi kim... sẽ phản ứng dần trên bề mặt: thẻ phản ứng sẽ đến sau, không báo "không phản ứng"
            if (ChemEngine.SolidRedoxPending(m, ml, env)) return;

            double V = ml / 1000.0;
            double phM = ChemEngine.Ph(m, V);
            // nhỏ chất chỉ thị (hoặc thêm dung dịch vào chất chỉ thị): nêu màu theo môi trường
            if (IsIndicatorOnly(s, ppS) || IsIndicatorOnly(a, ppA))
            {
                string col = IndicatorColorName(a, s, ppA + ppS, phM);
                string env2 = phM < 6.5 ? "axit" : phM > 7.5 ? "bazơ" : "trung tính";
                string name = ppA + ppS > 0.3f && !HasIndicator(a, s, 0) ? "Phenolphtalein" : IndicatorName(a, s);
                Add(events, EventKind.Color, "quan_sat:chi_thi" + sig, $"{name} trong môi trường {env2} (pH ≈ {Fmt(phM)}): dung dịch màu {col}",
                    ChiThiNote(name, phM));
                return;
            }
            double Va = mlA > 0.002 ? mlA / 1000.0 : V, Vs = mlS > 0.002 ? mlS / 1000.0 : V;
            double phA = a.IsEmpty ? 7 : ChemEngine.Ph(a, Va);
            double phS = s.IsEmpty ? 7 : ChemEngine.Ph(s, Vs);

            // ---- màu trước / sau ----
            var lookA = ChemColor.Compute(a, ml, phA, env.TempC);
            var lookS = ChemColor.Compute(s, ml, phS, env.TempC);
            var lookM = ChemColor.Compute(m, ml, phM, env.TempC);
            var water = new Rgb(0.86f, 0.93f, 1f);
            var pred = new Rgb(lookA.Liquid.r * lookS.Liquid.r / water.r, lookA.Liquid.g * lookS.Liquid.g / water.g, lookA.Liquid.b * lookS.Liquid.b / water.b);
            float pinkA = Pink(ppA, mlA, phA), pinkS = Pink(ppS, mlS, phS), pinkM = Pink(ppA + ppS, ml, phM);
            float pinkPred = Math.Max(pinkA * (float)(mlA / Math.Max(ml, 1e-6)), pinkS * (float)(mlS / Math.Max(ml, 1e-6)));
            double dColor = Math.Max(Math.Abs(pred.r - lookM.Liquid.r), Math.Max(Math.Abs(pred.g - lookM.Liquid.g), Math.Abs(pred.b - lookM.Liquid.b)));
            bool colorChanged = dColor > 0.08 || Math.Abs(pinkM - pinkPred) > 0.25;
            string before = ChemPalette.ColorName(new ChemLook { Liquid = (lookA.Alpha > lookS.Alpha ? lookA : lookS).Liquid }, Math.Max(pinkA, pinkS));
            string after = ChemPalette.ColorName(lookM, pinkM);

            // ---- trao đổi proton ----
            var ab = AcidBase(a, Va, phA, s, Vs, phS, m, V, phM);

            if (ab.moved > Math.Max(2e-6, 1e-3 * V))
            {
                string eq = ab.equation;
                string phText = $"pH {Fmt(phA)} và {Fmt(phS)} → {Fmt(phM)}";
                string title, detail;
                if (colorChanged && before != after && CrViWhy(m, phA, phS, phM) != null)
                {
                    title = $"Dung dịch chuyển màu: {before} → {after}";
                    detail = CrViWhy(m, phA, phS, phM);
                    eq = phM < phA || phM < phS ? "2CrO₄²⁻ + 2H⁺ ⇌ Cr₂O₇²⁻ + H₂O" : "Cr₂O₇²⁻ + 2OH⁻ ⇌ 2CrO₄²⁻ + H₂O";
                }
                else if (colorChanged && before != after)
                {
                    title = $"Màu chất chỉ thị đổi: {before} → {after}";
                    detail = $"Phản ứng axit - bazơ làm pH đổi ({phText}), chất chỉ thị đổi màu theo pH.";
                }
                else
                {
                    title = ab.neutralization ? "Phản ứng trung hòa: không có dấu hiệu nhìn thấy" : "Phản ứng axit - bazơ: không có dấu hiệu nhìn thấy";
                    detail = $"{phText}. Nhận biết bằng chất chỉ thị (quỳ tím, phenolphtalein) hoặc máy đo pH; dung dịch hơi ấm lên.";
                }
                Add(events, EventKind.Reaction, "quan_sat:axit_bazo" + sig, title, detail, eq);
                return;
            }
            if (colorChanged && before != after)
            {
                string why = CrViWhy(m, phA, phS, phM) ?? (HasIndicator(a, s, ppA + ppS) ? "Chất chỉ thị đổi màu theo pH của dung dịch." : "Thành phần tạo màu trong dung dịch thay đổi.");
                string eq = why.StartsWith("Cân bằng") ? "2CrO₄²⁻ + 2H⁺ ⇌ Cr₂O₇²⁻ + H₂O" : null;
                Add(events, EventKind.Color, "quan_sat:mau" + sig, $"Dung dịch chuyển màu: {before} → {after}", why, eq);
                return;
            }

            // ---- không phản ứng: nêu lý do ----
            if (sEmpty || IsOnlyWater(s))
            {
                if (!IsOnlyWater(a)) Add(events, EventKind.NoReaction, "quan_sat:pha_loang" + sig, "Pha loãng: không có phản ứng hóa học", "Thêm nước chỉ làm giảm nồng độ các chất (màu nhạt đi, pH tiến về 7).");
                return;
            }
            if (IsOnlyWater(a))
            {
                Add(events, EventKind.NoReaction, "quan_sat:hoa_tan" + sig, HasLump(s) ? "Chất rắn tan dần trong nước" : "Hòa tan / pha loãng trong nước: không có phản ứng hóa học",
                    HasLump(s) ? "Khuấy hoặc lắc để tan nhanh hơn." : "Chỉ là quá trình hòa tan (biến đổi vật lí).");
                return;
            }
            if (HasLump(s) || HasLump(a))
            {
                Add(events, EventKind.NoReaction, "quan_sat:dang_tan" + sig, "Chất rắn đang tan dần, chưa thấy dấu hiệu phản ứng", "Chờ chất rắn tan hết (lắc nhẹ) rồi quan sát tiếp; có thể cần đun nóng.");
                return;
            }
            Add(events, EventKind.NoReaction, "quan_sat:khong_pu" + sig, "Không có phản ứng hóa học", NoReactionReason(a, s));
        }

        // ------------------------------------------------------------------

        static void Add(List<ChemEvent> events, EventKind kind, string key, string title, string detail, string eq = null)
        {
            events.Add(new ChemEvent { Kind = kind, Key = key, Title = title, Detail = detail, Equation = eq, Severity = Severity.Info });
        }

        /// <summary>Phần đuôi khóa theo chất vừa thêm: rót từng giọt cùng một chất thì thẻ không lặp, chất khác thì có thẻ mới.</summary>
        static string Signature(Mix s)
        {
            double best = 0; string id = "";
            for (int i = 0; i < s.Count; i++) if (s.MolAt(i) > best) { best = s.MolAt(i); id = s.SpeciesAt(i).Id; }
            return id.Length > 0 ? ":" + id : "";
        }

        static bool IsGasOnly(Mix s)
        {
            if (s.IsEmpty) return false;
            for (int i = 0; i < s.Count; i++)
            {
                var d = s.SpeciesAt(i);
                if (d.Kind == SpeciesKind.Lump || d.Kind == SpeciesKind.Solid || d.Kind == SpeciesKind.Precipitate || d.Kind == SpeciesKind.Indicator) return false;
            }
            return true;
        }

        static void GasIntoWater(Mix s, List<ChemEvent> events, string sig)
        {
            string title, eq, detail;
            if (s.Get("Cl2") > 0) { title = "Khí clo tan một phần vào nước (nước clo màu vàng lục nhạt)"; eq = "Cl₂ + H₂O ⇌ HCl + HClO"; detail = "Nước clo có tính tẩy màu do HClO; làm quỳ tím hóa đỏ rồi mất màu."; }
            else if (s.Get("NO3-") > 0 && s.Get("NO2") > 0) { title = "Khí NO₂ nâu đỏ tan vào nước tạo dung dịch axit"; eq = "3NO₂ + H₂O → 2HNO₃ + NO"; detail = "Có oxi thì tạo hoàn toàn HNO₃: 4NO₂ + O₂ + 2H₂O → 4HNO₃."; }
            else if (s.Get("CO3") > 0) { title = "Khí CO₂ tan một phần vào nước tạo axit cacbonic"; eq = "CO₂ + H₂O ⇌ H₂CO₃"; detail = "Axit rất yếu: quỳ tím chuyển hồng nhạt; đun nóng CO₂ bay ra hết."; }
            else if (s.Get("SO3") > 0) { title = "Khí SO₂ tan vào nước tạo axit sunfurơ"; eq = "SO₂ + H₂O ⇌ H₂SO₃"; detail = "Dung dịch làm quỳ tím hóa đỏ; có mùi hắc."; }
            else if (s.Get("NH3") > 0) { title = "Khí NH₃ tan rất nhiều trong nước, dung dịch có tính bazơ"; eq = "NH₃ + H₂O ⇌ NH₄⁺ + OH⁻"; detail = "Thí nghiệm vòi phun: NH₃ tan nhanh làm nước phun vào bình; phenolphtalein hóa hồng."; }
            else if (s.Get("S") > 0) { title = "Khí H₂S tan vào nước tạo axit sunfuhiđric"; eq = "H₂S ⇌ H⁺ + HS⁻"; detail = "Axit rất yếu, mùi trứng thối, độc."; }
            else { title = "Khí ít tan trong nước, sủi qua rồi thoát ra"; eq = null; detail = "Không có phản ứng với nước ở điều kiện thường."; }
            events.Add(new ChemEvent { Kind = eq != null ? EventKind.Reaction : EventKind.NoReaction, Key = "quan_sat:khi_tan" + sig, Title = title, Detail = detail, Equation = eq, Severity = Severity.Info });
        }

        static bool IsIndicatorOnly(Mix x, float pp)
        {
            bool ind = pp > 0.3f;
            for (int i = 0; i < x.Count; i++)
            {
                if (x.MolAt(i) <= 1e-12) continue;
                if (x.SpeciesAt(i).Kind == SpeciesKind.Indicator && x.SpeciesAt(i).Id != "starch") ind = true;
                else return false;
            }
            return ind;
        }

        static string IndicatorName(Mix a, Mix s)
        {
            foreach (var x in new[] { s, a })
                for (int i = 0; i < x.Count; i++)
                    if (x.SpeciesAt(i).Kind == SpeciesKind.Indicator && x.SpeciesAt(i).Id != "starch") { var n = x.SpeciesAt(i).Name; return char.ToUpper(n[0]) + n.Substring(1); }
            return "Phenolphtalein";
        }

        /// <summary>Tên màu chất chỉ thị theo khoảng đổi màu (lấy từ định nghĩa chất chỉ thị, không đoán từ ảnh).</summary>
        static string IndicatorColorName(Mix a, Mix s, float pp, double ph)
        {
            foreach (var x in new[] { s, a })
                for (int i = 0; i < x.Count; i++)
                {
                    var d = x.SpeciesAt(i);
                    if (d.Kind != SpeciesKind.Indicator || d.Id == "starch") continue;
                    switch (d.Id)
                    {
                        case "mo": return ph < 3.1 ? "đỏ" : ph < 4.4 ? "da cam" : "vàng";
                        case "mr": return ph < 4.4 ? "đỏ" : ph < 6.2 ? "da cam" : "vàng";
                        case "btb": return ph < 6.0 ? "vàng" : ph < 7.6 ? "xanh lục" : "xanh lam";
                        case "bcg": return ph < 3.8 ? "vàng" : ph < 5.4 ? "xanh lục" : "xanh lam";
                        case "tb": return ph < 1.2 ? "đỏ" : ph < 8.0 ? "vàng" : ph < 9.6 ? "xanh lục" : "xanh lam";
                        case "ay": return ph < 10.1 ? "vàng" : ph < 12 ? "da cam" : "đỏ";
                        case "univ": return ph < 3 ? "đỏ" : ph < 5 ? "da cam" : ph < 6.5 ? "vàng" : ph < 7.5 ? "xanh lục" : ph < 10 ? "xanh lam" : "tím";
                    }
                }
            if (pp > 0.3f) return ph < 8.2 ? "không màu" : ph < 10 ? "hồng" : "hồng đậm";
            return "không đổi";
        }

        static string ChiThiNote(string name, double ph)
        {
            switch (name.ToLowerInvariant())
            {
                case "phenolphtalein": return "Phenolphtalein không màu khi pH < 8,2, hồng khi pH > 8,2 (môi trường bazơ).";
                case "metyl da cam": return "Metyl da cam: đỏ khi pH < 3,1, da cam 3,1 - 4,4, vàng khi pH > 4,4.";
                case "metyl đỏ": return "Metyl đỏ: đỏ khi pH < 4,4, vàng khi pH > 6,2.";
                case "bromothymol xanh": return "Bromothymol xanh: vàng (axit), xanh lục (trung tính), xanh lam (bazơ).";
                default: return "Màu chất chỉ thị cho biết môi trường axit, trung tính hay bazơ.";
            }
        }

        static string Fmt(double ph) => ph.ToString("0.0", Vi);

        static float Pink(float drops, double ml, double ph)
        {
            if (drops < 0.3f || ml <= 0) return 0;
            return (float)(TitrationPink(ph) * Math.Min(1, drops / 2.0));
        }

        /// <summary>Phần phenolphtalein dạng hồng (giống TitrationModel, viết lại để không phụ thuộc Unity).</summary>
        public static double TitrationPink(double ph) => ph <= 8.2 ? 0 : ph >= 10 ? 1 : (ph - 8.2) / 1.8;

        static bool IsOnlyWater(Mix x)
        {
            for (int i = 0; i < x.Count; i++) if (x.MolAt(i) > 1e-9) return false;
            return true;
        }

        static bool HasLump(Mix x)
        {
            for (int i = 0; i < x.Count; i++) if (x.SpeciesAt(i).Kind == SpeciesKind.Lump && x.MolAt(i) > 1e-9) return true;
            return false;
        }

        static bool HasIndicator(Mix a, Mix s, float pp)
        {
            if (pp > 0.3f) return true;
            foreach (var x in new[] { a, s })
                for (int i = 0; i < x.Count; i++) if (x.SpeciesAt(i).Kind == SpeciesKind.Indicator) return true;
            return false;
        }

        static string CrViWhy(Mix m, double phA, double phS, double phM)
        {
            if (m.Get("CrVI") < 1e-7) return null;
            return "Cân bằng cromat - đicromat chuyển dịch theo pH: axit → Cr₂O₇²⁻ da cam, kiềm → CrO₄²⁻ vàng.";
        }

        static string NoReactionReason(Mix a, Mix s)
        {
            bool ionA = HasKind(a, SpeciesKind.Ion) || HasKind(a, SpeciesKind.Weak), ionS = HasKind(s, SpeciesKind.Ion) || HasKind(s, SpeciesKind.Weak);
            bool orgA = HasOrganic(a), orgS = HasOrganic(s);
            bool inert = HasTag(a, "tro") || HasTag(s, "tro");
            if (inert) return "Chất trơ ở điều kiện thường (không tan, không phản ứng với dung dịch này).";
            if (HasTag(a, "khi_tro") || HasTag(s, "khi_tro")) return "Khí này rất ít tan trong nước và không phản ứng với các chất trong dung dịch.";
            if (ionA && ionS && !orgA && !orgS)
                return "Phản ứng trao đổi ion chỉ xảy ra khi tạo kết tủa, chất khí hoặc chất điện li yếu; ở đây các ion cùng tồn tại trong dung dịch.";
            if (orgA || orgS) return "Không có nhóm chức phản ứng với chất kia ở điều kiện này (có thể cần xúc tác hoặc đun nóng).";
            return "Các chất không phản ứng với nhau ở điều kiện này.";
        }

        static bool HasKind(Mix x, SpeciesKind k)
        {
            for (int i = 0; i < x.Count; i++) if (x.SpeciesAt(i).Kind == k && x.SpeciesAt(i).ChemStt == 0) return true;
            return false;
        }

        static bool HasOrganic(Mix x)
        {
            for (int i = 0; i < x.Count; i++) if (x.SpeciesAt(i).ChemStt > 0 && x.SpeciesAt(i).Kind != SpeciesKind.Lump) return true;
            return false;
        }

        static bool HasTag(Mix x, string t)
        {
            for (int i = 0; i < x.Count; i++) if (x.SpeciesAt(i).HasTag(t)) return true;
            return false;
        }

        /// <summary>Khí sục vào dư thoát ra: không ghi phương trình "axit + muối" (khí không sinh ra từ phản ứng đó).</summary>
        static void FixAddedGas(Mix s, double mlS, List<ChemEvent> events, int first)
        {
            if (mlS > 0.002) return;
            for (int i = first; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Kind != EventKind.Gas || e.GasId == null) continue;
                string src = e.GasId == "CO2" ? "CO3" : e.GasId == "SO2" ? "SO3" : e.GasId == "H2S" ? "S" : e.GasId == "NH3" ? "NH3" : null;
                if (src == null || s.Get(src) <= 0) continue;
                e.Title = $"Khí {SpeciesRegistry.Pretty(e.GasId)} dư sủi qua dung dịch rồi thoát ra";
                e.Detail = "Lượng khí sục vào nhiều hơn lượng dung dịch hấp thụ được.";
                e.Equation = null;
            }
        }

        // ------------------------------------------------------------------
        // Phản ứng axit - bazơ: so số proton gắn trên mỗi hệ axit yếu và H⁺ / OH⁻ tự do trước, sau
        // ------------------------------------------------------------------

        struct AB { public double moved; public string equation; public bool neutralization; }

        static double Bound(SpeciesDef d, double h)
        {
            int n = d.Ka.Length;
            return ChemEngine.WeakCharge(d, h) - (d.Charge - n);
        }

        static AB AcidBase(Mix a, double Va, double phA, Mix s, double Vs, double phS, Mix m, double V, double phM)
        {
            double hA = Math.Pow(10, -phA), hS = Math.Pow(10, -phS), hM = Math.Pow(10, -phM);
            // H⁺, OH⁻ tự do (mol)
            double freeH0 = (a.IsEmpty ? 0 : hA * Va) + (s.IsEmpty ? 0 : hS * Vs), freeH1 = hM * V;
            double freeOH0 = (a.IsEmpty ? 0 : ChemEngine.Kw / hA * Va) + (s.IsEmpty ? 0 : ChemEngine.Kw / hS * Vs), freeOH1 = ChemEngine.Kw / hM * V;
            var gains = new List<(string who, double amount, SpeciesDef d, double b0, double b1)>();
            var losses = new List<(string who, double amount, SpeciesDef d, double b0, double b1)>();
            void Weak(Mix src, double h)
            {
                for (int i = 0; i < src.Count; i++)
                {
                    var d = src.SpeciesAt(i);
                    if (d.Kind != SpeciesKind.Weak || d.Ka == null || d.Ka.Length == 0) continue;
                    double n = src.MolAt(i);
                    double nAfter = m.Get(d.Index) * (src.MolAt(i) / Math.Max(1e-30, a.Get(d.Index) + s.Get(d.Index)));
                    double b0 = Bound(d, h), b1 = Bound(d, hM);
                    double dn = n * b0 - Math.Min(n, nAfter) * b1;
                    if (n - nAfter > 0.3 * n) continue;   // loài đã kết tủa / bay hơi: phần đó do luật khác báo
                    if (dn > 0) losses.Add((d.Id, dn, d, b0, b1)); else if (dn < 0) gains.Add((d.Id, -dn, d, b0, b1));
                }
            }
            if (!a.IsEmpty) Weak(a, hA);
            if (!s.IsEmpty) Weak(s, hS);
            if (freeH0 - freeH1 > 0) losses.Add(("H+", freeH0 - freeH1, null, 1, 0));
            if (freeOH0 - freeOH1 > 0) gains.Add(("OH-", freeOH0 - freeOH1, null, 0, 1));
            double moved = 0, lost = 0;
            foreach (var g in gains) moved += g.amount;
            foreach (var l in losses) lost += l.amount;
            moved = Math.Min(moved, lost);
            var res = new AB { moved = moved };
            if (moved <= 0 || gains.Count == 0 || losses.Count == 0) return res;
            gains.Sort((x, y) => y.amount.CompareTo(x.amount));
            losses.Sort((x, y) => y.amount.CompareTo(x.amount));
            var donor = losses[0];
            var acc = gains[0];
            res.neutralization = donor.who == "H+" && acc.who == "OH-";
            // dạng chất theo số proton gắn
            string dFrom, dTo, aFrom, aTo;
            if (donor.d == null) { dFrom = "H+"; dTo = null; }
            else { dFrom = Form(donor.d, (int)Math.Round(donor.b0)); dTo = Form(donor.d, (int)Math.Round(donor.b1)); if (dFrom == dTo) dTo = Form(donor.d, (int)Math.Round(donor.b0) - 1); }
            if (acc.d == null) { aFrom = "OH-"; aTo = "H2O"; }
            else { aFrom = Form(acc.d, (int)Math.Round(acc.b0)); aTo = Form(acc.d, (int)Math.Round(acc.b1)); if (aFrom == aTo) aTo = Form(acc.d, (int)Math.Round(acc.b0) + 1); }
            if (dFrom == null || aFrom == null || aTo == null || (donor.d != null && dTo == null)) return res;
            var lhs = new List<string> { dFrom, aFrom };
            var rhs = new List<string>();
            if (dTo != null) rhs.Add(dTo);
            rhs.Add(aTo);
            if (donor.d == null && acc.d != null) { /* H⁺ + B → BH⁺ */ }
            if (donor.d != null && acc.d == null && !rhs.Contains("H2O")) rhs.Add("H2O");
            res.equation = Equation.BalanceAuto(lhs, rhs.FindAll(x => x != null), true);
            return res;
        }

        /// <summary>Công thức dạng có k proton gắn (tính từ dạng mất hết proton) của một hệ axit - bazơ yếu.</summary>
        public static string Form(SpeciesDef d, int k)
        {
            int n = d.Ka.Length;
            if (k < 0 || k > n) return null;
            string[] forms = null;
            switch (d.Id)
            {
                case "SO4": forms = new[] { "SO4^2-", "HSO4-" }; break;
                case "SO3": forms = new[] { "SO3^2-", "HSO3-", "SO2" }; break;
                case "CO3": forms = new[] { "CO3^2-", "HCO3-", "CO2" }; break;
                case "PO4": forms = new[] { "PO4^3-", "HPO4^2-", "H2PO4-", "H3PO4" }; break;
                case "PO3": forms = new[] { "HPO3^2-", "H2PO3-", "H3PO3" }; break;
                case "PO2": forms = new[] { "H2PO2-", "H3PO2" }; break;
                case "S": forms = new[] { "S^2-", "HS-", "H2S" }; break;
                case "NO2": forms = new[] { "NO2-", "HNO2" }; break;
                case "F": forms = new[] { "F-", "HF" }; break;
                case "CN": forms = new[] { "CN-", "HCN" }; break;
                case "ClO": forms = new[] { "ClO-", "HClO" }; break;
                case "ClO2": forms = new[] { "ClO2-", "HClO2" }; break;
                case "SiO3": forms = new[] { "SiO3^2-", "HSiO3-", "H2SiO3" }; break;
                case "B": forms = new[] { "H2BO3-", "H3BO3" }; break;
                case "CrVI": forms = new[] { "CrO4^2-", "HCrO4-" }; break;
                case "NH3": forms = new[] { "NH3", "NH4+" }; break;
                case "Ac": forms = new[] { "CH3COO-", "CH3COOH" }; break;
                case "Fo": forms = new[] { "HCOO-", "HCOOH" }; break;
                case "Ox": forms = new[] { "C2O4^2-", "HC2O4-", "H2C2O4" }; break;
                case "Cit": forms = new[] { "C6H5O7^3-", "HC6H5O7^2-", "H2C6H5O7-", "H3C6H5O7" }; break;
                case "Tart": forms = new[] { "C4H4O6^2-", "HC4H4O6-", "H2C4H4O6" }; break;
                case "Bz": forms = new[] { "C6H5COO-", "C6H5COOH" }; break;
                case "Lac": forms = new[] { "CH3CH(OH)COO-", "CH3CH(OH)COOH" }; break;
            }
            if (forms != null) return k < forms.Length ? forms[k] : null;
            if (d.ChemStt > 0 && n == 1)
            {
                string f = AsciiFormula(d);
                if (f == null) return null;
                if (d.Charge == 1)   // bazơ hữu cơ (amin): k = 0 là amin, k = 1 là ion amoni
                {
                    if (k == 0) return f;
                    if (f.EndsWith("NH2")) return f.Substring(0, f.Length - 3) + "NH3+";
                    if (f.EndsWith("NH")) return f.Substring(0, f.Length - 2) + "NH2+";
                    if (f.EndsWith("N")) return f.Substring(0, f.Length - 1) + "NH+";
                    return null;
                }
                if (k == 1) return f;
                int c = f.LastIndexOf("COOH", StringComparison.Ordinal);
                if (c >= 0) return f.Substring(0, c) + "COO-" + f.Substring(c + 4);
                if (f.EndsWith("OH")) return f.Substring(0, f.Length - 2) + "O-";
            }
            return null;
        }

        /// <summary>Công thức ASCII (bỏ chỉ số dưới Unicode) của loài.</summary>
        static string AsciiFormula(SpeciesDef d)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in d.Formula ?? "")
            {
                int k = "₀₁₂₃₄₅₆₇₈₉".IndexOf(ch);
                if (k >= 0) sb.Append((char)('0' + k));
                else if (ch < 128) sb.Append(ch);
                else return null;
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
