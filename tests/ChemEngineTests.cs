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

        Console.WriteLine("Máy phân tích: UV-Vis (phổ, định luật Beer, tách hỗn hợp) và AAS (đường chuẩn, pha loãng)");
        var uv = UvVisLibrary.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "data", "may", "tinh_chat", "uv_vis.json")));
        var unk = new List<string>();
        // KMnO4: A tuyến tính theo nồng độ ở 525 nm, độ dốc = ε·b
        var beer = new List<(double x, double y)>();
        foreach (double c in new[] { 1e-4, 2e-4, 3e-4, 4e-4, 5e-4 })
            beer.Add((c, UvVisLibrary.Absorbance(uv.Absorbers(Mix.Of(("MnO4-", c)), 1000, 7, 0, unk), 525)));
        var fitMn = UvVisLibrary.Linear(beer);
        Check(Math.Abs(fitMn.Slope - 2240) < 224 && fitMn.R2 > 0.9999, "KMnO₄ tuân theo định luật Beer, ε(525 nm) ≈ 2,24×10³", $"độ dốc {fitMn.Slope:0}, R² {fitMn.R2:0.0000}");
        Near(uv.Get("MnO4-").LambdaMax(400, 700), 525, 1, "λmax của KMnO₄ = 525 nm");
        // Cr(VI) trong axit: đúng số liệu chứng nhận NIST SRM 935a ở 350 nm (theo mol Cr)
        Near(UvVisLibrary.Absorbance(uv.Absorbers(Mix.Of(("CrVI", 1e-4)), 1000, 3, 0, unk), 350), 10.672 * 294.18 / 2 * 1e-4, 0.002, "Cr(VI) pH 3: A(350 nm) theo NIST SRM 935a");
        unk.Clear();
        uv.Absorbers(Mix.Of(("CrVI", 1e-4)), 1000, 9, 0, unk);
        Check(unk.Count == 1, "cromat ở pH 9: báo ngoài thư viện, không bịa số", unk.Count > 0 ? unk[0] : "");
        unk.Clear();
        uv.Absorbers(Mix.Of(("Cu2+", 0.05)), 1000, 4, 0, unk);
        Check(unk.Count == 1, "Cu²⁺ (chưa có ε có nguồn): báo ngoài thư viện", unk.Count > 0 ? unk[0] : "");
        // tách hỗn hợp KMnO4 + Cr(VI) + [Fe(SCN)]2+ từ phổ 360 – 700 nm
        var mixAbs = uv.Absorbers(Mix.Of(("MnO4-", 2e-4), ("CrVI", 3e-4), ("FeSCN", 5e-5)), 1000, 2, 0, null);
        var spec = new List<(double, double)>();
        for (double l = 360; l <= 700; l += 2) spec.Add((l, UvVisLibrary.Absorbance(mixAbs, l)));
        var comps = new List<UvVisLibrary.Entry> { uv.Get("MnO4-"), uv.Get("CrVI"), uv.Get("FeSCN"), uv.Get("CuNH3") };
        var cs = UvVisLibrary.Unmix(comps, spec, 1, out double rms);
        Check(Math.Abs(cs[0] / 2e-4 - 1) < 0.02 && Math.Abs(cs[1] / 3e-4 - 1) < 0.02 && Math.Abs(cs[2] / 5e-5 - 1) < 0.02 && cs[3] < 1e-6 && rms < 1e-4,
              "tách hỗn hợp 3 chất màu từ phổ (đúng ±2%, chất không có = 0)", $"MnO₄⁻ {cs[0]:E2}, Cr {cs[1]:E2}, FeSCN {cs[2]:E2}, Cu {cs[3]:E1}, rms {rms:E1}");
        Check(uv.CuvetteAbsorbance("nhua", 260) > 2 && uv.CuvetteAbsorbance("thach_anh", 260) < 0.01 && uv.CuvetteAbsorbance("nhua", 500) < 0.01, "cuvet nhựa chặn tia tử ngoại, thạch anh thì không");

        var aas = AasLibrary.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "data", "may", "tinh_chat", "aas_nguyen_to.json")));
        var cuL = aas.Get("Cu");
        var std = cuL.Standards();
        Check(std.SequenceEqual(new[] { 0.5, 1.0, 2.0, 5.0 }) || std.SequenceEqual(new[] { 0.5, 1.0, 2.0, 4.0 }), "dãy chuẩn Cu theo khoảng làm việc", string.Join(" / ", std));
        var cal = new List<(double x, double y)> { (0, 0) };
        foreach (double s in std) cal.Add((s, cuL.Absorbance(s)));
        var fitCu = UvVisLibrary.Linear(cal);
        Check(fitCu.R2 > 0.995, "đường chuẩn AAS Cu: R² > 0,995", $"R² {fitCu.R2:0.0000}, A(4 mg/L) {cuL.Absorbance(4):0.000}");
        Near(cuL.Absorbance(10), 0.8, 0.06, "Cu ở giới hạn trên khoảng làm việc (10 mg/L): A ≈ 0,8 (sổ tay: 0,8 – 1,0)");
        Check(cuL.Absorbance(254) < 2.0 && cuL.Absorbance(254) > 1.5, "mẫu rất đặc: A bão hòa dưới 2 như máy thật", $"A(254 mg/L) = {cuL.Absorbance(254):0.00}");
        // nước thải mạ: Cu 0,004 M = 254 mg/L, pha loãng 100 lần rồi đọc theo đường chuẩn
        var plating = Mix.Of(("Cu2+", 0.004), ("Zn2+", 0.003), ("Ni2+", 0.002), ("SO4", 0.009));
        var el = AasLibrary.ElementMgL(plating, 1000);
        Near(el["Cu"], 254.2, 0.5, "tổng Cu trong nước thải mạ = 254 mg/L");
        double found = fitCu.X(cuL.Absorbance(el["Cu"] / 100)) * 100;
        Check(Math.Abs(found / el["Cu"] - 1) < 0.02, "pha loãng ×100 rồi đo: tìm lại Cu đúng ±2%", $"{found:0.0} mg/L");
        double over = fitCu.X(cuL.Absorbance(el["Cu"])) ;
        Check(over < el["Cu"] * 0.5, "không pha loãng (ngoài đường chuẩn): kết quả sai rất xa", $"{over:0.0} mg/L thay vì {el["Cu"]:0}");
        Check(AasLibrary.CountAtoms("Fe4[Fe(CN)6]3")["Fe"] == 7 && AasLibrary.CountAtoms("Cu(OH)2")["Cu"] == 1 && AasLibrary.CountAtoms("CrVI")["Cr"] == 1 && AasLibrary.CountAtoms("hc:468").Count == 0,
              "đếm nguyên tử trong mã chất (phức, kết tủa, họ axit)");
        var withPpt = Mix.Of(("Cu2+", 0.001), ("Cu(OH)2", 0.002));
        var solids = new Dictionary<string, double>();
        var elp = AasLibrary.ElementMgL(withPpt, 1000, solids);
        Check(Math.Abs(elp["Cu"] - 0.003 * 63.55 * 1000) < 0.5 && Math.Abs(solids["Cu"] - 0.002 * 63.55 * 1000) < 0.5, "Cu trong kết tủa được tách riêng (cảnh báo phải lọc / phá mẫu)");
        Check(aas.Get("Al").Absorbance(50, airAcetylene: true) < 0.001, "Al trong ngọn lửa không khí – axetilen: gần như không có tín hiệu");

        Console.WriteLine("Máy phân tích đợt 2: FTIR, GC-MS, HPLC, chuẩn độ điện thế");
        string Data(string f) => System.IO.File.ReadAllText(System.IO.Path.Combine(root, "data", "may", "tinh_chat", f));
        var meoh = ChemEngine.RegisterOrganic(467, "Methanol", "CH3OH", new[] { "ancol", "de_chay" }, 0, 0, 32.04, 0.792);
        var amyl = ChemEngine.RegisterOrganic(539, "Isoamyl axetat", "CH3COOC5H11", new[] { "este", "khong_tan" }, 0, 0, 130.19, 0.876);
        var gluc = ChemEngine.RegisterOrganic(578, "Glucozơ", "C6H12O6", new[] { "cacbohidrat", "poliol" }, 0, 0, 180.16, 1.5);
        var sucr = ChemEngine.RegisterOrganic(580, "Saccarozơ", "C12H22O11", new[] { "cacbohidrat", "poliol" }, 0, 0, 342.3, 1.5);
        ChemEngine.RegisterOrganic(579, "Fructozơ", "C6H12O6", new[] { "cacbohidrat", "poliol" }, 0, 0, 180.16, 1.5);

        // ---- FTIR ----
        var ir = FtirLibrary.Load(Data("ftir.json"));
        var irUnk = new List<string>();
        var wine = ir.Composition(Mix.Of(("C2H5OH", 5.2)), 1000, 7, irUnk);
        Near(wine.First(p => p.E.Id == "etanol").Amount, 5.2 * 46.07 / 789, 0.002, "FTIR: rượu 5,2 M = 30,4% thể tích etanol");
        Near(wine.First(p => p.E.Id == "nuoc").Amount, 1 - 5.2 * 46.07 / 789, 0.002, "FTIR: nước chiếm phần thể tích còn lại");
        var vinegarAcid = ir.Composition(Mix.Of(("Ac", 0.83)), 1000, 2.5, null);
        var vinegarSalt = ir.Composition(Mix.Of(("Ac", 0.83)), 1000, 9, null);
        double c1710a = FtirLibrary.Absorbance(vinegarAcid, 1710, false), c1550a = FtirLibrary.Absorbance(vinegarAcid, 1550, false);
        double c1710b = FtirLibrary.Absorbance(vinegarSalt, 1710, false), c1550b = FtirLibrary.Absorbance(vinegarSalt, 1550, false);
        Check(c1710a > 3 * c1550a && c1550b > 3 * c1710b, "FTIR: axit axetic (pH 2,5) có C=O 1710 cm⁻¹, ion axetat (pH 9) có COO⁻ 1550 cm⁻¹",
              $"pH 2,5: {c1710a:0.000}/{c1550a:0.000}; pH 9: {c1710b:0.000}/{c1550b:0.000}");
        // dầu chuối (isoamyl axetat + etanol) trong nước: trừ phổ nước, tách hỗn hợp đúng thành phần
        var banana = ir.Composition(Mix.Of(("hc:539", 0.4), ("C2H5OH", 0.5)), 1000, 7, null);
        var irSpec = new List<(double nu, double a)>();
        for (double nu = 3800; nu >= 650; nu -= 4) irSpec.Add((nu, FtirLibrary.Absorbance(banana, nu, false)));
        var irMix = ir.Unmix(irSpec, true, out double irRms);
        var amylPart = irMix.FirstOrDefault(p => p.E.Id == "isoamyl_axetat");
        var etPart = irMix.FirstOrDefault(p => p.E.Id == "etanol");
        Check(amylPart.E != null && etPart.E != null && Math.Abs(amylPart.Amount / (0.4 * 130.19 / 876) - 1) < 0.03 && Math.Abs(etPart.Amount / (0.5 * 46.07 / 789) - 1) < 0.03 && irRms < 1e-4,
              "FTIR: tách phổ dầu chuối ra isoamyl axetat + etanol đúng ±3%", $"{irMix.Count} chất, rms {irRms:E1}");
        var hit = ir.Search(irSpec.Where(p => p.nu < 1800).ToList());
        Check(hit[0].p.E.Id == "isoamyl_axetat", "FTIR: tra thư viện vùng vân tay ra isoamyl axetat đầu bảng", $"{hit[0].p.Label} HQI {hit[0].hqi:0}");
        Check(ir.Assign(1738).Contains("este") && ir.Assign(2350).Contains("CO₂"), "FTIR: gán dải 1738 cm⁻¹ = C=O este, 2350 = CO₂");

        // ---- GC-MS ----
        var gc = GcLibrary.Load(Data("gc_ms.json"));
        var gm = new GcLibrary.Method();
        double tMe = gc.Retention(gc.ById("metanol").RI, gm).tr, tEt = gc.Retention(gc.ById("etanol").RI, gm).tr, tAm = gc.Retention(gc.ById("isoamyl_axetat").RI, gm).tr;
        double tC10 = gc.Retention(1000, gm).tr, tC16 = gc.Retention(1600, gm).tr;
        Check(tMe < tEt && tEt < tAm, "GC: metanol ra trước etanol, etanol trước isoamyl axetat", $"{tMe:0.00} / {tEt:0.00} / {tAm:0.00} phút");
        Check(tC10 > 7.5 && tC10 < 10 && tC16 > 16 && tC16 < 20, "GC: n-C10, n-C16 ra ở thời gian hợp lý (40 °C 2 phút, 10 °C/phút)", $"C10 {tC10:0.00}, C16 {tC16:0.00} phút");
        Near(gc.RetentionIndex(tAm, gm), 876, 6, "GC: tính ngược chỉ số lưu từ thời gian lưu (van den Dool – Kratz)");
        Near(gc.RetentionIndex(tMe, gm), 360, 15, "GC: chỉ số lưu của metanol (ra trước n-C5) tính ngược đúng");
        var hot = new GcLibrary.Method { T0 = 80 };
        double dCold = gc.Retention(445, gm).tr - gc.Retention(360, gm).tr, dHot = gc.Retention(445, hot).tr - gc.Retention(360, hot).tr;
        Check(dHot < dCold * 0.6, "GC: lò bắt đầu nóng (80 °C) làm metanol, etanol dồn sát nhau", $"Δt {dCold * 60:0.0} s → {dHot * 60:0.0} s");
        var gunk = new List<string>(); var gnv = new List<string>();
        var spirit = Mix.Of(("C2H5OH", 5.2), ("hc:467", 0.05));
        var gp = gc.Peaks(spirit, 1000, gm, 10, gunk, gnv);
        Check(gp.Count == 2 && gp[0].E.Id == "metanol" && !gp[1].Overloaded, "GC: rượu pha loãng ×10 cho 2 pic, etanol không quá tải", string.Join(", ", gp.Select(p => $"{p.E.Name} {p.Tr:0.00} ({p.AmountNg:0} ng)")));
        var gpRaw = gc.Peaks(spirit, 1000, gm, 1, null, null);
        Check(gpRaw.First(p => p.E.Id == "etanol").Overloaded && gpRaw.First(p => p.E.Id == "etanol").Saturated, "GC: rượu không pha loãng: pic etanol quá tải, detector bão hòa");
        var ms = gc.SpectrumAt(gp, gp[0].Tr, gm);
        var found1 = gc.Search(ms, gm.ScanLo);
        Check(found1[0].e.Id == "metanol" && found1[0].match > 800, "GC-MS: phổ ở đỉnh pic 1 tra thư viện ra metanol (độ khớp > 800)", $"{found1[0].e.Name} {found1[0].match:0}, kế: {found1[1].e.Name} {found1[1].match:0}");
        var ms2 = gc.SpectrumAt(gp, gp[1].Tr, gm);
        Check(gc.Search(ms2, gm.ScanLo)[0].e.Id == "etanol", "GC-MS: pic 2 là etanol");
        var calGc = new List<(double x, double y)>();
        foreach (double mg in new[] { 20.0, 50, 100, 200, 500 })
            calGc.Add((mg, GcLibrary.MeasuredArea(gc.Peaks(Mix.Of(("hc:467", mg / 32040.0), ("C2H5OH", 0.52)), 1000, gm, 1, null, null).First(p => p.E.Id == "metanol"))));
        var fGc = UvVisLibrary.Linear(calGc);
        double meFound = fGc.X(GcLibrary.MeasuredArea(gp[0])) * 10;
        Check(fGc.R2 > 0.999 && Math.Abs(meFound / (0.05 * 32040) - 1) < 0.02, "GC: định lượng metanol trong rượu đúng ±2% (dãy chuẩn 20 – 500 mg/L, mẫu pha loãng ×10)", $"{meFound:0} mg/L, R² {fGc.R2:0.0000}");
        double perPureEthanol = meFound / (5.2 * 46.07 / 789);
        Check(perPureEthanol > 100 * 40, "GC: metanol tính theo cồn 100° vượt rất xa 100 mg/L", $"{perPureEthanol:0} mg/L cồn 100°");

        // ---- HPLC ----
        var hp = HplcLibrary.Load(Data("hplc.json"));
        var acids = hp.Method("axit_huu_co");
        double p0 = hp.Pressure("axetonitril", 0, 1.0, 30);
        Check(p0 > 90 && p0 < 150, "HPLC: áp suất cột 250 × 4,6 mm, nước 1 mL/phút ≈ 100 – 150 bar", $"{p0:0} bar");
        Check(hp.Pressure("metanol", 0.4, 2.0, 30) > HplcLibrary.MaxBar * 0.9, "HPLC: metanol 40%, 2 mL/phút gần / vượt giới hạn 400 bar", $"{hp.Pressure("metanol", 0.4, 2.0, 30):0} bar");
        var oj = Mix.Of(("Cit", 0.03), ("hc:578", 0.25), ("hc:580", 0.1));
        var ns = new List<string>();
        var pa = hp.Peaks(acids, oj, 1000, 10, acids.Phi, acids.Ph, 1.0, 10, ns);
        Check(pa.Count == 1 && pa[0].C.Id == "citric" && ns.Count == 2, "HPLC axit hữu cơ (UV 210): nước cam chỉ có pic axit xitric, đường không hấp thụ UV", string.Join(", ", pa.Select(p => $"{p.C.Name} {p.Tr:0.00}")) + " | không thấy: " + string.Join(", ", ns));
        double trCit25 = pa[0].Tr;
        var paPh7 = hp.Peaks(acids, oj, 1000, 10, acids.Phi, 7.0, 1.0);
        Check(paPh7[0].Tr < hp.DeadTime(1.0) * 1.1 && trCit25 > hp.DeadTime(1.0) * 2, "HPLC: pha động pH 7 (axit ion hóa) làm axit xitric ra sát t₀, không tách", $"pH 2,5: {trCit25:0.00}; pH 7: {paPh7[0].Tr:0.00}; t₀ {hp.DeadTime(1.0):0.00} phút");
        var sug = hp.Method("duong");
        var ps = hp.Peaks(sug, oj, 1000, 10, sug.Phi, sug.Ph, 1.0);
        Check(ps.Count == 2 && ps[0].C.Id == "glucozo" && ps[1].C.Id == "saccarozo" && HplcLibrary.Resolution(ps[0], ps[1]) > 1.5, "HPLC đường (NH₂, RID): glucozơ ra trước saccarozơ, tách hoàn toàn", string.Join(", ", ps.Select(p => $"{p.C.Name} {p.Tr:0.00}")));
        var fruit = Mix.Of(("hc:579", 0.05), ("hc:578", 0.05), ("hc:580", 0.05));
        var ps3 = hp.Peaks(sug, fruit, 1000, 1, sug.Phi, sug.Ph, 1.0);
        Check(ps3.Select(p => p.C.Id).SequenceEqual(new[] { "fructozo", "glucozo", "saccarozo" }), "HPLC: thứ tự ra fructozơ, glucozơ, saccarozơ như tài liệu Supelco");
        // định lượng saccarozơ theo chuẩn ngoài
        var calLc = new List<(double x, double y)>();
        foreach (double gL in new[] { 1.0, 2, 4, 6, 8 }) calLc.Add((gL, hp.Peaks(sug, Mix.Of(("hc:580", gL / 342.3)), 1000, 1, sug.Phi, sug.Ph, 1.0)[0].Area));
        var fLc = UvVisLibrary.Linear(calLc);
        double sucFound = fLc.X(ps[1].Area) * 10;
        Check(Math.Abs(sucFound / (0.1 * 342.3) - 1) < 0.01, "HPLC: saccarozơ trong nước cam đúng ±1% (pha loãng ×10)", $"{sucFound:0.0} g/L");
        var pres = hp.Method("bao_quan");
        var soda = Mix.Of(("Bz", 0.0012), ("Cit", 0.01));
        var pb = hp.Peaks(pres, soda, 1000, 1, pres.Phi, pres.Ph, 1.0);
        var bz = pb.First(p => p.C.Id == "benzoic");
        double bz50 = hp.Peaks(pres, soda, 1000, 1, 0.5, pres.Ph, 1.0).First(p => p.C.Id == "benzoic").Tr;
        Check(bz.Tr > 6 && bz.Tr < 12 && bz50 < bz.Tr * 0.75, "HPLC: axit benzoic ra 6 – 12 phút; tăng metanol 40 → 50% thì ra sớm hơn", $"{bz.Tr:0.00} → {bz50:0.00} phút");

        // ---- chuẩn độ điện thế ----
        var calPts = PotentiometricTitration.Buffers.Select(ph => (ph, PotentiometricTitration.ElectrodeMv(ph, 0.965, 12))).ToList();
        var (sl, off) = PotentiometricTitration.Calibrate(calPts);
        Check(Math.Abs(sl - 0.965) < 1e-6 && Math.Abs(off - 12) < 1e-6, "chuẩn độ: hiệu chuẩn 3 đệm tìm lại độ dốc 96,5%, thế lệch +12 mV");
        double rawPh = PotentiometricTitration.DisplayPh(PotentiometricTitration.ElectrodeMv(10.0, 0.965, 12), 1, 0);
        Check(Math.Abs(rawPh - 10.0) > 0.2, "chuẩn độ: chưa hiệu chuẩn thì pH hiển thị lệch", $"pH thật 10,00 → hiển thị {rawPh:0.00}");
        // KHP 1,000 mmol với NaOH thật 0,0982 M: V_tđ = 10,18 mL
        var khp = new List<(double v, double mv)>();
        for (double v = 0; v <= 14; v += 0.05) khp.Add((v, PotentiometricTitration.ElectrodeMv(PotentiometricTitration.KhpPh(1e-3, 0.05, 0.0982, v / 1000), 0.965, 12)));
        var epK = PotentiometricTitration.Endpoints(khp);
        Check(epK.Count == 1 && Math.Abs(epK[0].v - 10.183) < 0.03, "chuẩn độ KHP: một điểm tương đương ở 10,18 mL → hệ số chuẩn NaOH 0,982", epK.Count > 0 ? $"{epK[0].v:0.000} mL" : "không thấy");
        // giấm 2 mL (0,83 M) trong 50 mL nước, NaOH 0,0982 M: V_tđ = 16,90 mL
        var vin = Mix.Of(("Ac", 0.83 * 0.002));
        var curve = new List<(double v, double mv)>();
        var phCurve = new List<(double v, double ph)>();
        for (double v = 0; v <= 22; v += 0.05)
        {
            double ph = PotentiometricTitration.SamplePh(vin, 0.052, true, 0.0982, v / 1000);
            curve.Add((v, PotentiometricTitration.ElectrodeMv(ph, 0.965, 12)));
            phCurve.Add((v, ph));
        }
        var epV = PotentiometricTitration.Endpoints(curve);
        double veq = 0.83 * 2 / 0.0982;
        Check(epV.Count == 1 && Math.Abs(epV[0].v - veq) < 0.05, "chuẩn độ giấm: điểm tương đương đúng ±0,05 mL", epV.Count > 0 ? $"{epV[0].v:0.000} mL (cần {veq:0.000})" : "không thấy");
        double phHalf = phCurve.OrderBy(p => Math.Abs(p.v - veq / 2)).First().ph;
        Near(phHalf, 4.74, 0.08, "chuẩn độ giấm: pH ở nửa thể tích tương đương ≈ pKa axit axetic");
        var gran = PotentiometricTitration.Gran(phCurve, veq * 0.2, veq * 0.8);
        Check(Math.Abs(gran.veq / veq - 1) < 0.01 && gran.r2 > 0.999, "chuẩn độ: đồ thị Gran cắt trục ở V_tđ (±1%)", $"{gran.veq:0.000} mL, R² {gran.r2:0.0000}");
        // H3PO4 cho 2 điểm tương đương
        var phos = Mix.Of(("PO4", 0.1 * 0.01));
        var cP = new List<(double v, double mv)>();
        for (double v = 0; v <= 24; v += 0.05) cP.Add((v, PotentiometricTitration.ElectrodeMv(PotentiometricTitration.SamplePh(phos, 0.06, true, 0.1, v / 1000), 1, 0)));
        var epP = PotentiometricTitration.Endpoints(cP, 100);
        Check(epP.Count == 2 && Math.Abs(epP[0].v - 10) < 0.15 && Math.Abs(epP[1].v - 20) < 0.25, "chuẩn độ H₃PO₄ 0,1 M (10 mL): hai điểm tương đương ở 10 và 20 mL", string.Join(" / ", epP.Select(e => $"{e.v:0.00}")));

        Console.WriteLine($"\n{passed} đạt, {failed} sai");
        return failed == 0 ? 0 : 1;
    }
}
