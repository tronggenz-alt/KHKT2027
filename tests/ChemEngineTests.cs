// Kiểm thử bộ máy hóa học (C# thuần, không cần Unity).
// Chạy:  powershell -ExecutionPolicy Bypass -File tools/chay_kiem_thu_hoa.ps1
using System;
using System.Collections.Generic;
using System.Linq;
using Khkt.Simulation.Chem;

static class ChemEngineTests
{
    static int failed, passed;

    static void Check(bool ok, string name, string detail = "")
    {
        if (ok) { passed++; Console.WriteLine("  ok   " + name); }
        else { failed++; Console.WriteLine("  SAI  " + name + "  " + detail); }
    }

    static void Near(double v, double want, double tol, string name) => Check(Math.Abs(v - want) <= tol, name, $"được {v:G5}, cần {want:G5} ± {tol}");

    /// <summary>Pha dung dịch: ml và danh sách (loài, mol/l).</summary>
    static (MixBuilder b, double V) Sol(double ml, params (string id, double c)[] parts)
    {
        var b = new MixBuilder();
        foreach (var (id, c) in parts) b.Add(id, c * ml / 1000);
        return (b, ml / 1000);
    }

    static (MixBuilder b, double V) Mixed((MixBuilder b, double V) x, (MixBuilder b, double V) y)
    {
        var m = new MixBuilder(x.b.ToMix() + y.b.ToMix());
        return (m, x.V + y.V);
    }

    static List<ChemEvent> Eq((MixBuilder b, double V) s, double T = 25)
    {
        var ev = new List<ChemEvent>();
        ChemEngine.Equilibrate(s.b, s.V, new ChemEnv { TempC = T }, ev);
        return ev;
    }

    static List<ChemEvent> Run((MixBuilder b, double V) s, double seconds, double T = 25)
    {
        var ev = Eq(s, T);
        float pp = 0;
        for (double t = 0; t < seconds; t += 0.2) ChemEngine.Step(s.b, s.V, 0.2, new ChemEnv { TempC = T }, ev, ref pp);
        return ev;
    }

    static double Ph((MixBuilder b, double V) s) => -Math.Log10(ChemEngine.SolveH(s.b, s.V));
    static bool Has(List<ChemEvent> ev, string keyPrefix) => ev.Any(e => e.Key != null && e.Key.StartsWith(keyPrefix));

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("pH các dung dịch quen thuộc");
        Near(Ph(Sol(25, ("Cl-", 0.1))), 1.0, 0.02, "HCl 0,1 M: pH 1");
        Near(Ph(Sol(25, ("Na+", 0.1))), 13.0, 0.02, "NaOH 0,1 M: pH 13");
        Near(Ph(Sol(25, ("Ac", 0.1))), 2.87, 0.03, "CH3COOH 0,1 M: pH 2,87");
        Near(Ph(Sol(25, ("NH3", 0.1))), 11.13, 0.03, "NH3 0,1 M: pH 11,13");
        Near(Ph(Sol(25, ("NH3", 0.1), ("Cl-", 0.1))), 5.13, 0.03, "NH4Cl 0,1 M: pH 5,13");
        Near(Ph(Sol(25, ("Na+", 0.2), ("CO3", 0.1))), 11.66, 0.05, "Na2CO3 0,1 M: pH 11,66");
        Near(Ph(Sol(25, ("Na+", 0.1), ("CO3", 0.1))), 8.34, 0.1, "NaHCO3 0,1 M: pH 8,3");
        Near(Ph(Sol(25, ("SO4", 0.1))), 0.96, 0.05, "H2SO4 0,1 M: pH ≈ 0,96");
        Near(Ph(Sol(25, ("Na+", 0.05), ("Ac", 0.1))), 4.74, 0.03, "Đệm axetat 1:1: pH = pKa");
        var hcl = Sol(25, ("Cl-", 0.1));
        Near(Ph(Mixed(hcl, Sol(25, ("Na+", 0.1)))), 7.0, 0.05, "HCl + NaOH vừa đủ: pH 7");

        Console.WriteLine("Kết tủa");
        var s1 = Mixed(Sol(5, ("Ag+", 0.1), ("NO3-", 0.1)), Sol(5, ("Na+", 0.1), ("Cl-", 0.1)));
        var ev = Eq(s1);
        Check(s1.b.Get("AgCl") > 4.9e-4, "AgNO3 + NaCl → AgCl↓", $"AgCl = {s1.b.Get("AgCl")}");
        Check(Has(ev, "kt:AgCl"), "có sự kiện kết tủa AgCl");
        var s2 = Mixed(Sol(5, ("Cu2+", 0.1), ("SO4", 0.1)), Sol(5, ("Na+", 0.2)));
        Eq(s2);
        Check(s2.b.Get("Cu(OH)2") > 4.5e-4, "CuSO4 + 2NaOH → Cu(OH)2↓ xanh", $"{s2.b.Get("Cu(OH)2")}");
        var s3 = Mixed(s2, Sol(5, ("NH3", 6)));
        ev = Eq(s3);
        Check(s3.b.Get("CuNH3") > 4.5e-4 && s3.b.Get("Cu(OH)2") < 1e-5, "NH3 dư hòa tan Cu(OH)2 → [Cu(NH3)4]2+", $"phức {s3.b.Get("CuNH3")}, kết tủa {s3.b.Get("Cu(OH)2")}");
        Check(Has(ev, "phuc:CuNH3") && Has(ev, "tan:Cu(OH)2"), "sự kiện tạo phức và kết tủa tan");
        var al = Mixed(Sol(5, ("Al3+", 0.1), ("Cl-", 0.3)), Sol(1.5, ("Na+", 1)));
        Eq(al);
        Check(al.b.Get("Al(OH)3") > 4e-4, "AlCl3 + 3NaOH → Al(OH)3↓", $"{al.b.Get("Al(OH)3")}");
        var al2 = Mixed(al, Sol(3, ("Na+", 1)));
        Eq(al2);
        Check(al2.b.Get("Al(OH)3") < 5e-5, "NaOH dư hòa tan Al(OH)3", $"{al2.b.Get("Al(OH)3")}");
        var ba = Mixed(Sol(5, ("Ba2+", 0.1), ("Cl-", 0.2)), Sol(5, ("SO4", 0.1)));
        Eq(ba);
        Check(ba.b.Get("BaSO4") > 4.9e-4, "BaCl2 + H2SO4 → BaSO4↓ (cả trong axit)");
        var mohr = Mixed(Sol(10, ("Na+", 0.12), ("Cl-", 0.1), ("CrVI", 0.01)), Sol(2, ("Ag+", 0.1), ("NO3-", 0.1)));
        Eq(mohr);
        Check(mohr.b.Get("AgCl") > 1.9e-4 && mohr.b.Get("Ag2CrO4") < 1e-6, "Mohr: AgCl kết tủa trước Ag2CrO4", $"AgCl {mohr.b.Get("AgCl")}, Ag2CrO4 {mohr.b.Get("Ag2CrO4")}");
        var limewater = Mixed(Sol(10, ("Ca2+", 0.02)), Sol(1, ("CO3", 0.03)));
        Eq(limewater);
        Check(limewater.b.Get("CaCO3") > 2e-5, "CO2 làm đục nước vôi trong");
        var fe = Sol(10, ("Fe3+", 0.1), ("Cl-", 0.3));
        Eq(fe);
        Check(fe.b.Get("Fe(OH)3") < 1e-6, "FeCl3 0,1 M trong, không tự kết tủa", $"{fe.b.Get("Fe(OH)3")}");
        var scn = Mixed(Sol(5, ("Fe3+", 0.01), ("Cl-", 0.03)), Sol(1, ("K+", 0.1), ("SCN-", 0.1)));
        ev = Eq(scn);
        Check(scn.b.Get("FeSCN") > 4e-5 && Has(ev, "phuc:FeSCN"), "Fe3+ + SCN- → đỏ máu");

        Console.WriteLine("Khí");
        var co2 = Mixed(Sol(10, ("Na+", 1), ("CO3", 0.5)), Sol(10, ("Cl-", 2)));
        ev = Eq(co2);
        Check(Has(ev, "khi:CO2"), "Na2CO3 + HCl → CO2↑");
        Check(Ph(co2) < 2, "dư HCl: pH < 2", $"pH {Ph(co2):0.00}");
        var nh = Sol(10, ("NH3", 0.5), ("Cl-", 0.5), ("Na+", 0.6));
        ev = Eq(nh, 80);
        Check(Has(ev, "khi:NH3"), "NH4Cl + NaOH đun nóng → NH3↑");

        Console.WriteLine("Oxi hóa - khử");
        var mn = Mixed(Sol(10, ("Fe2+", 0.1), ("SO4", 0.6)), Sol(2, ("K+", 0.1), ("MnO4-", 0.1)));
        ev = Eq(mn);
        Check(mn.b.Get("MnO4-") < 1e-7 && mn.b.Get("Mn2+") > 1.9e-4 && mn.b.Get("Fe3+") > 9e-4, "KMnO4 + FeSO4/H2SO4: mất màu, Fe3+", mn.b.ToMix().ToString());
        var cl = Mixed(Sol(5, ("K+", 0.1), ("I-", 0.1)), Sol(2, ("Cl2", 0.1)));
        Eq(cl);
        Check(cl.b.Get("I2") > 1.9e-4, "Cl2 + KI → I2");
        var look = ChemColor.Compute((cl.b.ToMix() + Mix.Of(("starch", 0.01))), 7, 6, 25);
        Check(look.Liquid.b < 0.6 && look.Liquid.r < 0.3, "I2 + hồ tinh bột: xanh tím đậm", $"{look.Liquid.r:0.00} {look.Liquid.g:0.00} {look.Liquid.b:0.00}");
        var thio = Mixed(Sol(5, ("I2", 0.01), ("K+", 0.05), ("I-", 0.05)), Sol(5, ("Na+", 0.04), ("S2O32-", 0.02)));
        Eq(thio);
        Check(thio.b.Get("I2") < 1e-8, "I2 + Na2S2O3: mất màu iot");

        Console.WriteLine("Theo thời gian");
        var zn = Sol(10, ("Cl-", 1));
        zn.b.Add("Zn(s)", 0.003);
        ev = Run(zn, 5);
        Check(Has(ev, "khi:H2") && zn.b.Get("Zn2+") > 1e-4, "Zn + HCl → H2↑", $"Zn2+ {zn.b.Get("Zn2+")}");
        var fecu = Sol(10, ("Cu2+", 0.1), ("SO4", 0.1));
        fecu.b.Add("Fe(s)", 0.002);
        ev = Run(fecu, 5);
        Check(fecu.b.Get("Cu(s)") > 1e-4 && Has(ev, "daykl"), "Fe + CuSO4 → Cu bám trên đinh sắt", $"Cu {fecu.b.Get("Cu(s)")}");
        var cuag = Sol(10, ("Ag+", 0.1), ("NO3-", 0.1));
        cuag.b.Add("Cu(s)", 0.002);
        Run(cuag, 5);
        Check(cuag.b.Get("Ag(s)") > 1e-4 && cuag.b.Get("Cu2+") > 5e-5, "Cu + AgNO3 → Ag (dung dịch xanh dần)");
        var na = Sol(20);
        na.b.Add("Na(s)", 0.002);
        ev = Run(na, 2);
        Check(Ph(na) > 12 && Has(ev, "khi:H2"), "Na + H2O → NaOH + H2", $"pH {Ph(na):0.0}");
        var h2o2 = Sol(10, ("H2O2", 0.9));
        h2o2.b.Add("MnO2(s)", 1e-4);
        ev = Run(h2o2, 5);
        Check(Has(ev, "khi:O2") && h2o2.b.Get("H2O2") < 0.003, "H2O2 + MnO2 → O2↑", $"còn {h2o2.b.Get("H2O2")}");
        var th = Mixed(Sol(5, ("Na+", 0.2), ("S2O32-", 0.1)), Sol(5, ("Cl-", 1)));
        ev = Run(th, 20);
        Check(th.b.Get("S(kt)") > 1e-4 && Has(ev, "kt:S"), "Na2S2O3 + HCl → S vẩn đục");
        var feoh = Mixed(Sol(5, ("Fe2+", 0.1), ("SO4", 0.1)), Sol(5, ("Na+", 0.4)));
        Run(feoh, 60);
        Check(feoh.b.Get("Fe(OH)3") > 1e-4, "Fe(OH)2 hóa nâu Fe(OH)3 ngoài không khí");
        var caco3 = Sol(10, ("Cl-", 1));
        var lump = ChemEngine.RegisterLump(279, "canxi cacbonat", "CaCO3", new[] { ("Ca2+", 1.0), ("CO3", 1.0) }, 100.09, Rgb.White, 0);
        caco3.b.Add(lump.Id, 0.002);
        ev = Run(caco3, 10);
        Check(Has(ev, "khi:CO2") && caco3.b.Get(lump.Id) < 0.0015, "Đá vôi tan dần trong HCl, sủi CO2", $"còn {caco3.b.Get(lump.Id)}");
        // bình điều chế CO2: đá vôi trong ít nước, nhỏ HCl từ phễu nhỏ giọt vào sau
        var wet = Sol(10);
        wet.b.Add(lump.Id, 0.05);
        Run(wet, 10);
        var acid = Mixed(wet, Sol(20, ("Cl-", 1)));
        ev = Run(acid, 20);
        double co2Mol = ev.Where(e => e.Kind == EventKind.Gas && e.GasId == "CO2").Sum(e => e.Amount);
        // nhỏ HCl từng đợt nhỏ (phễu nhỏ giọt): tổng CO2 vẫn gần đủ theo lượng axit (20 mmol HCl → khoảng 10 mmol CO2)
        var drip = Sol(10);
        drip.b.Add(lump.Id, 0.05);
        double co2Drip = 0;
        for (int k = 0; k < 14; k++)
        {
            drip = Mixed(drip, Sol(1.5, ("Cl-", 1)));
            co2Drip += Run(drip, 0.3).Where(e => e.Kind == EventKind.Gas && e.GasId == "CO2").Sum(e => e.Amount);
        }
        co2Drip += Run(drip, 10).Where(e => e.Kind == EventKind.Gas && e.GasId == "CO2").Sum(e => e.Amount);
        Check(co2Drip > 0.007, "Nhỏ HCl từng đợt lên đá vôi: tổng CO2 theo lượng axit", $"CO2 {co2Drip * 1000:0.0} mmol / HCl 21 mmol");
        Check(co2Mol > 0.005, "Đá vôi ngâm nước, nhỏ HCl vào sau: vẫn sủi CO2", $"CO2 {co2Mol * 1000:0.0} mmol, CaCO3 kt {acid.b.Get("CaCO3") * 1000:0.0} mmol, cục {acid.b.Get(lump.Id) * 1000:0.0} mmol");

        Console.WriteLine("Hữu cơ");
        var glu = ChemEngine.RegisterOrganic(578, "glucozơ", "C6H12O6", new[] { "andehit", "poliol", "cacbohidrat" }, 0, 0, 180.16, 1.5);
        var phen = ChemEngine.RegisterOrganic(480, "phenol", "C6H5OH", new[] { "phenol" }, 0, 0, 94.11, 1.07);
        var etilen = ChemEngine.RegisterOrganic(426, "etilen", "C2H4", new[] { "khong_no" }, 0, 0, 28.05, 1);
        var br = Mixed(Sol(5, ("Br2", 0.05)), Sol(2, (phen.Id, 0.05)));
        ev = Eq(br);
        Check(br.b.Get("TBP") > 5e-5 && Has(ev, "pu:Br2+@phenol"), "Phenol + nước brom → kết tủa trắng");
        var ene = Mixed(Sol(5, ("Br2", 0.02)), Sol(1, (etilen.Id, 0.2)));
        Eq(ene);
        Check(ene.b.Get("Br2") < 1e-7, "Etilen làm mất màu nước brom");
        var tollens = Mixed(Sol(2, ("Ag+", 0.1), ("NO3-", 0.1), ("NH3", 0.6)), Sol(2, (glu.Id, 0.2)));
        ev = Run(tollens, 30, 70);
        Check(tollens.b.Get("AgMirror") > 1e-4 && Has(ev, "trangbac"), "Glucozơ + Tollens (đun) → tráng bạc", $"{tollens.b.Get("AgMirror")}");
        var cuGlu = Mixed(Mixed(Sol(2, ("Cu2+", 0.1), ("SO4", 0.1)), Sol(2, ("Na+", 0.5))), Sol(2, (glu.Id, 0.5)));
        ev = Eq(cuGlu);
        Check(cuGlu.b.Get("CuPol") > 1e-4 && Has(ev, "phuc:CuPol"), "Glucozơ hòa tan Cu(OH)2 → xanh lam");
        Run(cuGlu, 40, 85);
        Check(cuGlu.b.Get("Cu2O") > 5e-5, "Đun nóng → Cu2O đỏ gạch");
        var cr = Mixed(Sol(5, ("K+", 0.1), ("CrVI", 0.1), ("SO4", 2)), Sol(1, ("C2H5OH", 0.5)));
        cr.b.Add("C2H5OH", 0);
        SpeciesRegistry.Get("C2H5OH").Tags.Add("ancol");
        ev = Eq(cr);
        Check(cr.b.Get("Cr3+") > 3e-4, "K2Cr2O7/H2SO4 + etanol: da cam → xanh lục", cr.b.ToMix().ToString());

        Console.WriteLine("Màu");
        var cu = Sol(10, ("Cu2+", 0.1), ("SO4", 0.1));
        Eq(cu);
        var lc = ChemColor.Compute(cu.b.ToMix(), 10, Ph(cu), 25);
        Check(lc.Liquid.b > lc.Liquid.r + 0.2, "CuSO4 xanh lam", $"{lc.Liquid.r:0.00} {lc.Liquid.g:0.00} {lc.Liquid.b:0.00}");
        var mo = ChemColor.IndicatorColor("mo", 2);
        Check(mo.r > 0.7 && mo.g < 0.4, "metyl da cam đỏ trong axit");
        Check(SpeciesRegistry.Pretty("Al2(SO4)3") == "Al₂(SO₄)₃" && SpeciesRegistry.Pretty("SO42-", -2) == "SO₄²⁻" && SpeciesRegistry.Pretty("CuSO4.5H2O") == "CuSO₄·5H₂O",
              "công thức hiển thị", SpeciesRegistry.Pretty("SO42-", -2) + " " + SpeciesRegistry.Pretty("CuSO4.5H2O"));

        Console.WriteLine("Bay hơi, chưng cất (định luật Raoult giản lược)");
        string root = Environment.GetEnvironmentVariable("KHKT_ROOT") ?? ".";
        Volatility.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "data", "chem", "nhiet_do_soi.json")));
        Check(Volatility.Loaded, "nạp bảng nhiệt độ sôi");
        var eth = ChemEngine.RegisterOrganic(468, "etanol", "C2H5OH", new[] { "ancol", "de_chay" }, 0, 0, 46.069, 0.789);
        Mix Ethanol(double mlEthanol) => Mix.Of((eth.Id, mlEthanol * 0.789 / 46.069));
        Near(Volatility.BubblePoint(Volatility.Analyze(Mix.Empty, 100)), 100, 0.05, "nước sôi ở 100 °C");
        Near(Volatility.BubblePoint(Volatility.Analyze(Ethanol(100), 100)), 78.4, 0.1, "etanol nguyên chất sôi ở 78,4 °C");
        var l50 = Volatility.Analyze(Ethanol(50), 100);
        double t50 = Volatility.BubblePoint(l50);
        Check(t50 > 79 && t50 < 92, "rượu 50° sôi giữa 78,4 và 100 °C", $"{t50:0.0} °C");
        double xEth = l50.org[0].mol / l50.Total;
        var v50 = Volatility.Vaporize(l50, 5000);
        double yEth = v50.org[0].mol / v50.TotalMol;
        Check(yEth > xEth * 1.5, "hơi giàu etanol hơn dung dịch", $"x = {xEth:0.00}, y = {yEth:0.00}");
        Near(v50.TotalMol * (yEth * 38600 + (1 - yEth) * 40700), 5000, 50, "nhiệt hóa hơi đúng lượng hơi");
        // chưng cất 100 ml rượu 20°: phần cất đầu giàu etanol, nhiệt độ sôi tăng dần
        double ml = 100, ethMol = 20 * 0.789 / 46.069, firstFrac = -1, t0 = -1, tEnd = 0;
        for (int i = 0; i < 40; i++)
        {
            var L = Volatility.Analyze(Mix.Of((eth.Id, ethMol)), ml);
            if (i == 0) t0 = Volatility.BubblePoint(L);
            var v = Volatility.Vaporize(L, 4000);
            double e = v.org.Count > 0 ? v.org[0].mol : 0;
            if (i == 0) firstFrac = e * 46.069 / 0.789 / v.liquidMl;  // phần thể tích etanol trong giọt cất đầu
            ethMol -= e;
            ml -= v.liquidMl;
            tEnd = Volatility.BubblePoint(Volatility.Analyze(Mix.Of((eth.Id, ethMol)), ml));
        }
        Check(firstFrac > 0.45, "phần cất đầu của rượu 20° giàu etanol", $"{firstFrac:P0} etanol");
        Check(tEnd > t0 + 5, "nhiệt độ sôi tăng dần khi etanol cạn", $"{t0:0.0} → {tEnd:0.0} °C");

        Console.WriteLine($"\n{passed} đạt, {failed} sai");
        return failed == 0 ? 0 : 1;
    }
}
