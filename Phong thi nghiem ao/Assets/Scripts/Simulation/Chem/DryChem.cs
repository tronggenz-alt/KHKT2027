using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Phản ứng không có dung dịch (bình khô): nhiệt phân, chất rắn + chất rắn (Fe + S, nhiệt nhôm, C + CuO), khí + chất rắn
    /// khi đun (H₂ / CO khử CuO, Cl₂ + kim loại), đốt cháy trong không khí hoặc oxi, khí + khí (NH₃ + HCl → khói trắng).
    /// Mỗi luật là một dòng dữ liệu: chất phản ứng (mã loài, hoặc "f:CÔNG_THỨC" cho hóa chất rắn chưa tan), nhiệt độ tối
    /// thiểu, sản phẩm, khí thoát ra, hơi nước; phương trình đi qua bộ cân bằng nên luôn cân bằng.
    /// </summary>
    public static partial class ChemEngine
    {
        public sealed class DryRule
        {
            public string Key, Title, Detail;
            public (string who, double n)[] In;
            public (string id, double n)[] Out;
            public (string gas, double n)[] Gas;
            public double Water, TMin, TCatalyst = -1, HeatKJ;
            public string Catalyst;
            public bool Air;              // cần oxi không khí (bình hở)
            public string[] EqLhs, EqRhs; // công thức cho phương trình
            public Severity Sev = Severity.Info;
            public string Hazard;
            public string Eq;
        }

        static List<DryRule> dryRules;

        static void D(string key, string title, double tMin, (string, double)[] inp, (string, double)[] outp, (string, double)[] gas, string[] lhs, string[] rhs,
                      double water = 0, bool air = false, string detail = null, double heat = 0, string cat = null, double tCat = -1, Severity sev = Severity.Info, string hazard = null)
        {
            var r = new DryRule
            {
                Key = key, Title = title, TMin = tMin, In = inp, Out = outp ?? new (string, double)[0], Gas = gas ?? new (string, double)[0], Water = water, Air = air,
                Detail = detail, HeatKJ = heat, Catalyst = cat, TCatalyst = tCat, EqLhs = lhs, EqRhs = rhs, Sev = sev, Hazard = hazard,
            };
            r.Eq = Equation.Balance(lhs, rhs);
            dryRules.Add(r);
        }

        static (string, double)[] L(params (string, double)[] x) => x;

        public static IReadOnlyList<DryRule> DryRules { get { BuildDry(); return dryRules; } }

        static void BuildDry()
        {
            if (dryRules != null) return;
            dryRules = new List<DryRule>();
            // ---------- Nhiệt phân ----------
            D("nhiet_phan:KMnO4", "KMnO₄ bị nhiệt phân: thoát khí O₂ (que đóm còn tàn đỏ bùng cháy)", 220, L(("f:KMnO4", 2)), L(("K+", 2), ("MnO42-", 1), ("MnO2(s)", 1)), L(("O2", 1)),
              new[] { "KMnO4" }, new[] { "K2MnO4", "MnO2", "O2" }, detail: "Điều chế oxi trong phòng thí nghiệm.");
            D("nhiet_phan:KClO3", "KClO₃ bị nhiệt phân: thoát khí O₂", 380, L(("f:KClO3", 2)), L(("K+", 2), ("Cl-", 2)), L(("O2", 3)), new[] { "KClO3" }, new[] { "KCl", "O2" },
              cat: "MnO2(s)", tCat: 240, detail: "Có MnO₂ xúc tác thì phản ứng xảy ra ở nhiệt độ thấp hơn nhiều.");
            D("nhiet_phan:KNO3", "KNO₃ nóng chảy, thoát khí O₂ (que đóm bùng cháy)", 400, L(("f:KNO3", 2)), L(("K+", 2), ("NO2", 2)), L(("O2", 1)), new[] { "KNO3" }, new[] { "KNO2", "O2" },
              detail: "Muối nitrat của kim loại kiềm nhiệt phân tạo muối nitrit và O₂.");
            D("nhiet_phan:NaNO3", "NaNO₃ nóng chảy, thoát khí O₂", 400, L(("f:NaNO3", 2)), L(("Na+", 2), ("NO2", 2)), L(("O2", 1)), new[] { "NaNO3" }, new[] { "NaNO2", "O2" });
            D("nhiet_phan:Cu(NO3)2", "Cu(NO₃)₂ hóa đen (CuO), khí NO₂ nâu đỏ thoát ra", 200, L(("f:Cu(NO3)2", 2)), L(("CuO(s)", 2)), L(("NO2", 4), ("O2", 1)),
              new[] { "Cu(NO3)2" }, new[] { "CuO", "NO2", "O2" }, sev: Severity.Warning, hazard: "khi_doc", detail: "Nitrat của kim loại từ Mg đến Cu nhiệt phân tạo oxit, NO₂ và O₂.");
            D("nhiet_phan:Pb(NO3)2", "Pb(NO₃)₂ nổ lách tách, khí NO₂ nâu đỏ thoát ra", 400, L(("f:Pb(NO3)2", 2)), L(("PbO(s)", 2)), L(("NO2", 4), ("O2", 1)),
              new[] { "Pb(NO3)2" }, new[] { "PbO", "NO2", "O2" }, sev: Severity.Warning, hazard: "khi_doc");
            D("nhiet_phan:AgNO3", "AgNO₃ để lại bạc kim loại, khí NO₂ nâu đỏ thoát ra", 300, L(("f:AgNO3", 2)), L(("Ag(s)", 2)), L(("NO2", 2), ("O2", 1)),
              new[] { "AgNO3" }, new[] { "Ag", "NO2", "O2" }, sev: Severity.Warning, hazard: "khi_doc", detail: "Nitrat của kim loại sau Cu nhiệt phân tạo kim loại, NO₂ và O₂.");
            D("nhiet_phan:NH4Cl", "NH₄Cl \"thăng hoa\": NH₃ và HCl kết hợp lại ở chỗ nguội thành khói trắng", 340, L(("f:NH4Cl", 1)), null, L(("NH3", 1), ("HCl", 1)),
              new[] { "NH4Cl" }, new[] { "NH3", "HCl" }, detail: "Không phải thăng hoa thật: NH₄Cl phân hủy rồi tạo lại ở thành ống nguội.");
            D("nhiet_phan:NH4NO3", "NH₄NO₃ nóng chảy, thoát khí N₂O", 210, L(("f:NH4NO3", 1)), null, L(("N2O", 1)), new[] { "NH4NO3" }, new[] { "N2O", "H2O" }, water: 2,
              sev: Severity.Warning, hazard: "no", detail: "Đun mạnh dễ nổ: chỉ đun lượng rất nhỏ.");
            D("nhiet_phan:(NH4)2Cr2O7", "\"Núi lửa\": (NH₄)₂Cr₂O₇ da cam bùng cháy thành Cr₂O₃ lục thẫm, phồng lên", 200, L(("f:(NH4)2Cr2O7", 1)), L(("Cr2O3(s)", 1)), L(("N2", 1)),
              new[] { "(NH4)2Cr2O7" }, new[] { "Cr2O3", "N2", "H2O" }, water: 4, sev: Severity.Warning, hazard: "doc");
            D("nhiet_phan:NaHCO3", "NaHCO₃ bị nhiệt phân: hơi nước đọng ở miệng ống, khí CO₂ làm đục nước vôi trong", 150, L(("f:NaHCO3", 2)), L(("Na+", 2), ("CO3", 1)), L(("CO2", 1)),
              new[] { "NaHCO3" }, new[] { "Na2CO3", "CO2", "H2O" }, water: 1);
            D("nhiet_phan:KHCO3", "KHCO₃ bị nhiệt phân, thoát khí CO₂", 150, L(("f:KHCO3", 2)), L(("K+", 2), ("CO3", 1)), L(("CO2", 1)), new[] { "KHCO3" }, new[] { "K2CO3", "CO2", "H2O" }, water: 1);
            D("nhiet_phan:CaCO3", "CaCO₃ bị nhiệt phân (nung vôi): khí CO₂ thoát ra, còn lại vôi sống", 840, L(("CaCO3", 1)), L(("Ca2+", 1)), L(("CO2", 1)), new[] { "CaCO3" }, new[] { "CaO", "CO2" },
              detail: "Cần nhiệt độ rất cao (≈ 900 °C): đèn cồn không đủ, phải dùng lò nung.");
            D("nhiet_phan:MgCO3", "MgCO₃ bị nhiệt phân, thoát khí CO₂", 500, L(("MgCO3", 1)), L(("MgO(s)", 1)), L(("CO2", 1)), new[] { "MgCO3" }, new[] { "MgO", "CO2" });
            D("nhiet_phan:Cu(OH)2", "Cu(OH)₂ xanh lam hóa đen (CuO), có hơi nước", 80, L(("Cu(OH)2", 1)), L(("CuO(s)", 1)), null, new[] { "Cu(OH)2" }, new[] { "CuO", "H2O" }, water: 1);
            D("nhiet_phan:Fe(OH)3", "Fe(OH)₃ nâu đỏ chuyển thành Fe₂O₃, có hơi nước", 300, L(("Fe(OH)3", 2)), L(("Fe2O3(s)", 1)), null, new[] { "Fe(OH)3" }, new[] { "Fe2O3", "H2O" }, water: 3);
            D("nhiet_phan:Al(OH)3", "Al(OH)₃ mất nước thành Al₂O₃ trắng", 300, L(("Al(OH)3", 2)), L(("Al2O3(s)", 1)), null, new[] { "Al(OH)3" }, new[] { "Al2O3", "H2O" }, water: 3);
            D("nhiet_phan:Mg(OH)2", "Mg(OH)₂ mất nước thành MgO", 350, L(("Mg(OH)2", 1)), L(("MgO(s)", 1)), null, new[] { "Mg(OH)2" }, new[] { "MgO", "H2O" }, water: 1);
            D("nhiet_phan:Zn(OH)2", "Zn(OH)₂ mất nước thành ZnO", 150, L(("Zn(OH)2", 1)), L(("ZnO(s)", 1)), null, new[] { "Zn(OH)2" }, new[] { "ZnO", "H2O" }, water: 1);
            D("nhiet_phan:CuSO4.5H2O", "Tinh thể CuSO₄·5H₂O xanh mất nước, hóa trắng; hơi nước ngưng ở miệng ống", 110, L(("f:CuSO4.5H2O", 1)), L(("f:CuSO4", 1)), null,
              new[] { "CuSO4.5H2O" }, new[] { "CuSO4", "H2O" }, water: 5);
            D("nhiet_phan:HgO", "HgO đỏ phân hủy: giọt thủy ngân bám thành ống, khí O₂", 400, L(("HgO(s)", 2)), L(("Hg(l)", 2)), L(("O2", 1)), new[] { "HgO" }, new[] { "Hg", "O2" },
              sev: Severity.Warning, hazard: "doc");
            D("nhiet_phan:Ag2O", "Ag₂O đen phân hủy thành bạc", 300, L(("Ag2O(s)", 2)), L(("Ag(s)", 4)), L(("O2", 1)), new[] { "Ag2O" }, new[] { "Ag", "O2" });
            D("nhiet_phan:duong", "Đường nóng chảy, hóa đen (than), hơi nước đọng ở miệng ống", 190, L(("@cacbohidrat", 1)), L(("C(than)", 6)), null,
              new[] { "C6H12O6" }, new[] { "C", "H2O" }, water: 6, detail: "Cacbohiđrat bị nhiệt phân (than hóa): chứng tỏ có nguyên tố C, H, O.");
            // ---------- Đốt cháy trong không khí (bình hở) ----------
            D("chay:Mg", "Magie cháy sáng chói, tạo bột trắng MgO", 600, L(("Mg(s)", 2)), L(("MgO(s)", 2)), null, new[] { "Mg", "O2" }, new[] { "MgO" }, air: true, heat: 601,
              sev: Severity.Warning, hazard: "bong", detail: "Không nhìn thẳng vào ngọn lửa magie.");
            D("chay:S", "Lưu huỳnh nóng chảy, cháy với ngọn lửa xanh nhạt, khí SO₂ mùi hắc", 250, L(("S(s)", 1)), null, L(("SO2", 1)), new[] { "S", "O2" }, new[] { "SO2" }, air: true, heat: 297,
              sev: Severity.Warning, hazard: "khi_doc");
            D("chay:P", "Photpho cháy sáng, tạo khói trắng dày (P₂O₅)", 260, L(("P(s)", 4)), L(("P2O5(s)", 2)), null, new[] { "P", "O2" }, new[] { "P2O5" }, air: true, heat: 750);
            D("chay:Pw", "Photpho trắng tự bốc cháy, khói trắng dày", 40, L(("Pw(s)", 4)), L(("P2O5(s)", 2)), null, new[] { "P", "O2" }, new[] { "P2O5" }, air: true, heat: 750,
              sev: Severity.Danger, hazard: "chay_no", detail: "Photpho trắng rất độc, tự bốc cháy trong không khí: bảo quản ngâm trong nước.");
            D("chay:C", "Than nóng đỏ, cháy tạo khí CO₂", 700, L(("C(s)", 1)), null, L(("CO2", 1)), new[] { "C", "O2" }, new[] { "CO2" }, air: true, heat: 394);
            D("chay:Cu", "Đồng nung nóng phủ lớp CuO màu đen", 300, L(("Cu(s)", 2)), L(("CuO(s)", 2)), null, new[] { "Cu", "O2" }, new[] { "CuO" }, air: true);
            D("chay:Zn", "Kẽm cháy, tạo ZnO trắng (vàng khi nóng)", 450, L(("Zn(s)", 2)), L(("ZnO(s)", 2)), null, new[] { "Zn", "O2" }, new[] { "ZnO" }, air: true);
            D("chay:Fe", "Sắt cháy sáng chói trong oxi, bắn hoa lửa, tạo Fe₃O₄ đen", 500, L(("Fe(s)", 3), ("khi:O2", 2)), L(("Fe3O4(s)", 1)), null, new[] { "Fe", "O2" }, new[] { "Fe3O4" },
              heat: 1118, sev: Severity.Warning, hazard: "bong", detail: "Lót cát hoặc nước dưới đáy bình oxi để hạt sắt nóng chảy không làm vỡ bình.");
            D("chay:Na", "Natri cháy với ngọn lửa vàng", 120, L(("Na(s)", 4)), L(("Na+", 4)), null, new[] { "Na", "O2" }, new[] { "Na2O" }, air: true, sev: Severity.Warning, hazard: "chay_no");
            // ---------- Chất rắn + chất rắn ----------
            D("kho:Fe+S", "Hỗn hợp Fe + S nóng đỏ, tự cháy lan, tạo chất rắn đen FeS", 300, L(("Fe(s)", 1), ("S(s)", 1)), L(("FeS", 1)), null, new[] { "Fe", "S" }, new[] { "FeS" }, heat: 100);
            D("kho:Zn+S", "Zn + S cháy sáng, tạo ZnS trắng", 400, L(("Zn(s)", 1), ("S(s)", 1)), L(("ZnS", 1)), null, new[] { "Zn", "S" }, new[] { "ZnS" }, heat: 206);
            D("kho:Hg+S", "Thủy ngân + bột lưu huỳnh tạo HgS đen ngay ở nhiệt độ thường (thu gom thủy ngân rơi vãi)", 0, L(("Hg(l)", 1), ("S(s)", 1)), L(("HgS", 1)), null,
              new[] { "Hg", "S" }, new[] { "HgS" });
            D("kho:nhietnhom", "Phản ứng nhiệt nhôm: cháy sáng chói, tỏa rất nhiều nhiệt, sắt nóng chảy", 500, L(("Al(s)", 2), ("Fe2O3(s)", 1)), L(("Al2O3(s)", 1), ("Fe(s)", 2)), null,
              new[] { "Al", "Fe2O3" }, new[] { "Al2O3", "Fe" }, heat: 852, sev: Severity.Danger, hazard: "bong", detail: "Mồi bằng dây magie đang cháy; điều chế kim loại khó nóng chảy, hàn đường ray.");
            D("kho:C+CuO", "CuO đen chuyển dần thành đồng màu đỏ, khí CO₂ làm đục nước vôi trong", 500, L(("C(s)", 1), ("CuO(s)", 2)), L(("Cu(s)", 2)), L(("CO2", 1)),
              new[] { "C", "CuO" }, new[] { "Cu", "CO2" });
            D("kho:C+Fe2O3", "Fe₂O₃ bị cacbon khử thành sắt, thoát khí CO₂", 700, L(("C(s)", 3), ("Fe2O3(s)", 2)), L(("Fe(s)", 4)), L(("CO2", 3)), new[] { "C", "Fe2O3" }, new[] { "Fe", "CO2" });
            D("kho:Mg+CO2", "Magie tiếp tục cháy trong khí CO₂, tạo bột trắng MgO và muội than đen", 600, L(("Mg(s)", 2), ("CO3", 1)), L(("MgO(s)", 2), ("C(than)", 1)), null,
              new[] { "Mg", "CO2" }, new[] { "MgO", "C" }, detail: "Không dùng CO₂ để dập đám cháy magie, nhôm.");
            D("kho:I2+Al", "Al + I₂ (nhỏ giọt nước xúc tác): phản ứng mãnh liệt, hơi iot màu tím bốc lên", 0, L(("Al(s)", 2), ("I2", 3)), L(("Al3+", 2), ("I-", 6)), null,
              new[] { "Al", "I2" }, new[] { "AlI3" }, sev: Severity.Warning);
            // ---------- Khí + chất rắn ----------
            D("kho:H2+CuO", "CuO đen chuyển thành đồng màu đỏ, có hơi nước đọng ở thành ống", 300, L(("khi:H2", 1), ("CuO(s)", 1)), L(("Cu(s)", 1)), null, new[] { "H2", "CuO" }, new[] { "Cu", "H2O" }, water: 1);
            D("kho:CO+CuO", "CuO đen chuyển thành đồng đỏ, khí CO₂ làm đục nước vôi trong", 300, L(("khi:CO", 1), ("CuO(s)", 1)), L(("Cu(s)", 1)), L(("CO2", 1)), new[] { "CO", "CuO" }, new[] { "Cu", "CO2" });
            D("kho:H2+Fe2O3", "Fe₂O₃ nâu đỏ bị khử thành sắt màu xám", 500, L(("khi:H2", 3), ("Fe2O3(s)", 1)), L(("Fe(s)", 2)), null, new[] { "H2", "Fe2O3" }, new[] { "Fe", "H2O" }, water: 3);
            D("kho:CO+Fe2O3", "Fe₂O₃ bị CO khử thành sắt, khí CO₂ thoát ra (nguyên tắc luyện gang)", 500, L(("khi:CO", 3), ("Fe2O3(s)", 1)), L(("Fe(s)", 2)), L(("CO2", 3)),
              new[] { "CO", "Fe2O3" }, new[] { "Fe", "CO2" });
            D("kho:NH3+CuO", "CuO đen hóa đỏ (Cu), có hơi nước, khí N₂", 400, L(("NH3", 2), ("CuO(s)", 3)), L(("Cu(s)", 3)), L(("N2", 1)), new[] { "NH3", "CuO" }, new[] { "Cu", "N2", "H2O" }, water: 3);
            D("kho:Fe+Cl2", "Sắt cháy sáng trong clo, tạo khói nâu đỏ FeCl₃", 250, L(("Fe(s)", 2), ("Cl2", 3)), L(("Fe3+", 2), ("Cl-", 6)), null, new[] { "Fe", "Cl2" }, new[] { "FeCl3" },
              sev: Severity.Warning, hazard: "khi_doc", detail: "Clo oxi hóa sắt lên mức +3 (khác với HCl chỉ tạo Fe²⁺).");
            D("kho:Cu+Cl2", "Dây đồng nóng đỏ cháy trong clo, tạo khói nâu (CuCl₂); hòa vào nước được dung dịch xanh", 200, L(("Cu(s)", 1), ("Cl2", 1)), L(("Cu2+", 1), ("Cl-", 2)), null,
              new[] { "Cu", "Cl2" }, new[] { "CuCl2" }, sev: Severity.Warning, hazard: "khi_doc");
            D("kho:Na+Cl2", "Natri nóng chảy cháy sáng trong clo, khói trắng NaCl", 100, L(("Na(s)", 2), ("Cl2", 1)), L(("Na+", 2), ("Cl-", 2)), null, new[] { "Na", "Cl2" }, new[] { "NaCl" },
              sev: Severity.Warning, hazard: "khi_doc");
            D("kho:Al+Cl2", "Nhôm cháy trong clo, khói trắng AlCl₃", 250, L(("Al(s)", 2), ("Cl2", 3)), L(("Al3+", 2), ("Cl-", 6)), null, new[] { "Al", "Cl2" }, new[] { "AlCl3" },
              sev: Severity.Warning, hazard: "khi_doc");
            // ---------- Khí + khí ----------
            D("khoi:NH4Cl", "Khói trắng NH₄Cl (NH₃ gặp khí HCl)", 0, L(("NH3", 1), ("khi:HClk", 1)), L(("NH3", 1), ("Cl-", 1)), null, new[] { "NH3", "HCl" }, new[] { "NH4Cl" },
              detail: "Nhận biết NH₃ (hoặc HCl): đưa đũa thủy tinh nhúng HCl đặc lại gần miệng bình NH₃.");
            D("kho:H2S+SO2", "Thành bình xuất hiện bột vàng lưu huỳnh, có giọt nước", 0, L(("S", 2), ("SO3", 1)), L(("S(kt)", 3)), null, new[] { "H2S", "SO2" }, new[] { "S", "H2O" }, water: 2);
        }

        static double DryAmount(MixBuilder b, string who, out List<int> ids)
        {
            ids = new List<int>();
            double n = 0;
            if (who.StartsWith("f:"))
            {
                string f = who.Substring(2);
                foreach (int s in b.Indices)
                {
                    var d = SpeciesRegistry.At(s);
                    if (d.Kind == SpeciesKind.Lump && d.HasTag("f:" + f)) { ids.Add(s); n += b.Get(s); }
                }
                return n;
            }
            if (who[0] == '@')
            {
                string t = who.Substring(1);
                foreach (int s in b.Indices) if (SpeciesRegistry.At(s).HasTag(t)) { ids.Add(s); n += b.Get(s); }
                return n;
            }
            int i = SpeciesRegistry.IndexOf(who);
            if (i >= 0 && b.Get(i) > 0) { ids.Add(i); n = b.Get(i); }
            // hóa chất rắn chưa tan chỉ gồm đúng chất đó (iot rắn...)
            foreach (int s in b.Indices)
            {
                var d = SpeciesRegistry.At(s);
                if (d.Kind == SpeciesKind.Lump && d.Composition != null && d.Composition.Length == 1 && d.Composition[0].id == who) { ids.Add(s); n += b.Get(s) * d.Composition[0].n; }
            }
            return n;
        }

        /// <summary>Trong bình khô có chất nào tham gia được phản ứng khô (để không tính khi bình chỉ có một chất trơ).</summary>
        public static bool HasDryReactant(Mix mix)
        {
            BuildDry();
            if (mix == null || mix.IsEmpty) return false;
            var b = mix.ToBuilder();
            foreach (var r in dryRules)
            {
                bool all = true;
                foreach (var (who, _) in r.In) if (DryAmount(b, who, out _) <= 1e-12) { all = false; break; }
                if (all) return true;
            }
            return false;
        }

        /// <summary>
        /// Một bước phản ứng khô ở nhiệt độ chất rắn tC trong dt giây. air: bình hở (có oxi không khí).
        /// Khí sinh ra ghi vào gases (để dẫn qua ống / thoát ra), hơi nước ghi vào waterMol. Trả về nhiệt tỏa ra (J).
        /// </summary>
        public static double DryStep(MixBuilder b, double tC, double dt, bool air, List<ChemEvent> events, Dictionary<string, double> gases, out double waterMol)
        {
            BuildDry();
            waterMol = 0;
            double heat = 0;
            foreach (var r in dryRules)
            {
                if (r.Air && !air) continue;
                double tNeed = r.TCatalyst > 0 && r.Catalyst != null && b.Get(r.Catalyst) > 1e-7 ? r.TCatalyst : r.TMin;
                if (tC < tNeed) continue;
                double ext = double.MaxValue;
                var idsAll = new List<List<int>>();
                foreach (var (who, n) in r.In)
                {
                    double have = DryAmount(b, who, out var ids);
                    idsAll.Add(ids);
                    ext = Math.Min(ext, have / n);
                }
                if (ext <= 1e-12) continue;
                double rate = Math.Max(ext * 0.08 * dt * Math.Min(4, 1 + (tC - tNeed) / 80), 2e-6 * dt);
                double x = Math.Min(ext, rate);
                for (int k = 0; k < r.In.Length; k++)
                {
                    double need = x * r.In[k].n;
                    foreach (int s in idsAll[k]) { if (need <= 0) break; need -= b.Take(s, need); }
                }
                foreach (var (id, n) in r.Out)
                {
                    if (id.StartsWith("f:"))
                    {
                        var lump = RegisterLump(681, "đồng(ii) sunfat khan", id.Substring(2), new[] { ("Cu2+", 1.0), ("SO4", 1.0) }, 159.6, Rgb.Hex("#EEEEEE"), 66.5);
                        b.Add(lump.Index, n * x);
                    }
                    else b.Add(id, n * x);
                }
                if (gases != null) foreach (var (g, n) in r.Gas) gases[g] = (gases.TryGetValue(g, out var o) ? o : 0) + n * x;
                waterMol += r.Water * x;
                heat += r.HeatKJ * 1000 * x;
                if (events != null)
                {
                    string gasId = r.Gas.Length > 0 ? r.Gas[0].gas : null;
                    Emit2(events, gasId != null ? EventKind.Gas : EventKind.Reaction, r.Key, r.Title, r.Detail ?? "", r.Eq, r.Sev, r.Hazard, gasId, x);
                }
            }
            return heat;
        }

        static void Emit2(List<ChemEvent> events, EventKind kind, string key, string title, string detail, string eq, Severity sev, string hazard, string gas, double amount)
        {
            foreach (var e in events) if (e.Key == key) { e.Amount += amount; return; }
            events.Add(new ChemEvent { Kind = kind, Key = key, Title = title, Detail = detail, Equation = eq, Severity = sev, Hazard = hazard, GasId = gas, Amount = amount });
        }
    }
}
