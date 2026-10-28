using System.Collections.Generic;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Demo.Instruments
{
    /// <summary>Hồ sơ một máy (data/may/&lt;ma&gt;.json): nguyên lý, bộ phận, thông số có nguồn, quy trình, an toàn, câu hỏi ôn tập.</summary>
    public sealed class InstrumentDoc
    {
        public sealed class Step { public string Code, Name, Action, Why, Mistake; }

        public string Code, Name, Model;
        public readonly List<string> Principle = new List<string>(), Formulas = new List<string>(), Safety = new List<string>(), Questions = new List<string>();
        public readonly List<(string code, string name, string role)> Parts = new List<(string, string, string)>();
        public readonly List<(string name, string value, string kind, string source)> Specs = new List<(string, string, string, string)>();
        public readonly List<(string term, string meaning)> Terms = new List<(string, string)>();
        public readonly List<Step> Steps = new List<Step>();

        public static InstrumentDoc Parse(string json)
        {
            var r = MiniJson.Obj(MiniJson.Parse(json));
            var d = new InstrumentDoc { Code = MiniJson.S(r, "ma"), Name = MiniJson.S(r, "ten"), Model = MiniJson.S(r, "may_mau") };
            foreach (var o in MiniJson.A(r, "nguyen_ly")) d.Principle.Add(o as string);
            foreach (var o in MiniJson.A(r, "cong_thuc")) d.Formulas.Add(o as string);
            foreach (var o in MiniJson.A(r, "an_toan")) d.Safety.Add(o as string);
            foreach (var o in MiniJson.A(r, "cau_hoi_on_tap")) d.Questions.Add(o as string);
            foreach (var o in MiniJson.A(r, "bo_phan")) { var b = MiniJson.Obj(o); d.Parts.Add((MiniJson.S(b, "ma"), MiniJson.S(b, "ten"), MiniJson.S(b, "chuc_nang"))); }
            foreach (var o in MiniJson.A(r, "thong_so"))
            {
                var t = MiniJson.Obj(o);
                d.Specs.Add((MiniJson.S(t, "ten"), MiniJson.S(t, "gia_tri"), MiniJson.S(t, "loai"), MiniJson.B(t, "mo_phong") ? "mô phỏng" : MiniJson.S(t, "nguon")));
            }
            foreach (var o in MiniJson.A(r, "thuat_ngu")) { var t = MiniJson.Obj(o); d.Terms.Add((MiniJson.S(t, "tu"), MiniJson.S(t, "nghia"))); }
            foreach (var o in MiniJson.A(r, "quy_trinh"))
            {
                var s = MiniJson.Obj(o);
                d.Steps.Add(new Step { Code = MiniJson.S(s, "ma"), Name = MiniJson.S(s, "ten"), Action = MiniJson.S(s, "viec"), Why = MiniJson.S(s, "ly_do"), Mistake = MiniJson.S(s, "loi") });
            }
            return d;
        }

        public string PartName(string code)
        {
            foreach (var p in Parts) if (p.code == code) return p.name;
            return code;
        }
    }

    /// <summary>Dữ liệu các máy phân tích (bản chép của data/may trong Assets/Lab/Data/May), trình dựng cảnh gán sẵn.</summary>
    public class InstrumentData : MonoBehaviour
    {
        public static InstrumentData Instance { get; private set; }
        public TextAsset uvVisDoc, aasDoc, uvVisBands, aasElements;
        public TextAsset ftirDoc, gcMsDoc, hplcDoc, titratorDoc, ftirLib, gcMsLib, hplcLib;
        public TextAsset xrdDoc, semDoc, nmrDoc, calorimeterDoc, xrdLib, edsLib, nmrLib, calorimetryLib;
        public TextAsset ovenDoc, furnaceDoc, distillerDoc, autoclaveDoc;

        UvVisLibrary uv;
        AasLibrary aas;
        FtirLibrary ftir;
        GcLibrary gc;
        HplcLibrary hplc;
        XrdLibrary xrd;
        EdsLibrary eds;
        NmrLibrary nmr;
        CalorimetryLibrary cal;
        readonly Dictionary<string, InstrumentDoc> docs = new Dictionary<string, InstrumentDoc>();

        void Awake() => Instance = this;

        public UvVisLibrary Uv => uv ??= uvVisBands != null ? UvVisLibrary.Load(uvVisBands.text) : new UvVisLibrary();
        public AasLibrary Aas => aas ??= aasElements != null ? AasLibrary.Load(aasElements.text) : new AasLibrary();
        public FtirLibrary Ftir => ftir ??= ftirLib != null ? FtirLibrary.Load(ftirLib.text) : new FtirLibrary();
        public GcLibrary Gc => gc ??= gcMsLib != null ? GcLibrary.Load(gcMsLib.text) : new GcLibrary();
        public HplcLibrary Hplc => hplc ??= hplcLib != null ? HplcLibrary.Load(hplcLib.text) : new HplcLibrary();
        public XrdLibrary Xrd => xrd ??= xrdLib != null ? XrdLibrary.Load(xrdLib.text) : new XrdLibrary();
        public EdsLibrary Eds => eds ??= edsLib != null ? EdsLibrary.Load(edsLib.text) : new EdsLibrary();
        public NmrLibrary Nmr => nmr ??= nmrLib != null ? NmrLibrary.Load(nmrLib.text) : new NmrLibrary();
        public CalorimetryLibrary Calorimetry => cal ??= calorimetryLib != null ? CalorimetryLibrary.Load(calorimetryLib.text) : new CalorimetryLibrary();

        public InstrumentDoc Doc(string code)
        {
            if (docs.TryGetValue(code, out var d)) return d;
            var ta = code switch { "uv_vis" => uvVisDoc, "aas" => aasDoc, "ftir" => ftirDoc, "gc_ms" => gcMsDoc, "hplc" => hplcDoc, "chuan_do_dien_the" => titratorDoc, "xrd" => xrdDoc, "sem" => semDoc, "nmr" => nmrDoc, "nhiet_luong" => calorimeterDoc, "tu_say" => ovenDoc, "lo_nung" => furnaceDoc, "may_cat_nuoc" => distillerDoc, "noi_hap" => autoclaveDoc, _ => null };
            d = ta != null ? InstrumentDoc.Parse(ta.text) : new InstrumentDoc { Code = code, Name = code };
            docs[code] = d;
            return d;
        }
    }
}
