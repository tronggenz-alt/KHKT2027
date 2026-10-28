using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Khkt.Simulation.Chem;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.Demo.Instruments.UvVisProgram;

namespace Khkt.Demo.Instruments
{
    /// <summary>
    /// Máy cất nước hai lần: nước ra được kiểm tra bằng độ dẫn điện, xếp loại theo TCVN 4851 (ISO 3696). Lỗi mô phỏng: đun khi chưa có nước cấp
    /// (ngắt nhiệt), không mở nước làm mát (hơi thoát, không có nước cất), không bỏ phần đầu (độ dẫn cao), để bình hở lâu (hấp thụ CO₂).
    /// </summary>
    public class DistillerProgram : InstrumentProgram
    {
        public override string Code => "may_cat_nuoc";
        static readonly string[] optional = { "ket_thuc" };
        public override ICollection<string> Optional => optional;

        public bool Cooling { get; private set; }
        public bool Feed { get; private set; }
        public bool Heater { get; private set; }
        public int Stages { get; private set; } = 2;
        public double ProducedMl { get; private set; }
        public double DiscardedMl { get; private set; }
        public double BottleMl { get; private set; }
        public bool BottleOpen { get; private set; }
        public double OpenMin { get; private set; }
        public double LastUs { get; private set; } = double.NaN;
        readonly List<(double ml, double us, string label)> readings = new List<(double, double, string)>();
        float warm;
        bool tripped;
        readonly System.Random rng = new System.Random(43);

        Button bCool, bFeed, bHeat, bStages, bDiscard, bCollect, bMeasure, bOpen, bWait, bOff;
        Text status;

        public override void Build()
        {
            bCool = S.AddButton("Mở nước làm mát", 0, 0, () => { Cooling = !Cooling; if (Cooling && Feed) S.Done("cap_nuoc"); }, false, 1, 13);
            bFeed = S.AddButton("Mở nước cấp", 1, 0, () => { Feed = !Feed; if (Cooling && Feed) S.Done("cap_nuoc"); }, false, 1, 13);
            bHeat = S.AddButton("Bật đun", 2, 0, ToggleHeat, true);
            bStages = S.AddButton("", 0, 1, () => Stages = Stages == 2 ? 1 : 2, false, 2, 13);
            bDiscard = S.AddButton("Bỏ phần nước đầu", 2, 1, Discard, false, 1, 13);
            bCollect = S.AddButton("Hứng vào bình chứa", 0, 2, Collect, false, 1, 13);
            bMeasure = S.AddButton("Đo độ dẫn điện", 1, 2, Measure, true, 1, 13);
            bOpen = S.AddButton("", 2, 2, () => BottleOpen = !BottleOpen, false, 1, 13);
            bWait = S.AddButton("Để 2 giờ", 0, 3, () => { OpenMin += BottleOpen ? 120 : 10; S.Say(BottleOpen ? "Để bình hở 2 giờ ngoài không khí." : "Để bình đậy kín 2 giờ.", 0); });
            bOff = S.AddButton("Tắt đun, xả cặn", 1, 3, TurnOff, false, 2, 13);
            status = S.AddText("", 4, 0, 3, 13);
            S.SetResults("Mở nước làm mát và nước cấp trước, rồi bật đun. Bỏ phần nước cất đầu, đo độ dẫn điện để xếp loại nước.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bCool, Cooling ? "Nước làm mát: mở" : "Mở nước làm mát", true, Cooling);
            InstrumentScreen.SetButton(bFeed, Feed ? "Nước cấp: mở" : "Mở nước cấp", true, Feed);
            InstrumentScreen.SetButton(bHeat, tripped ? "Ngắt nhiệt (cạn nước)!" : Heater ? "Đang đun" : "Bật đun", true, Heater);
            InstrumentScreen.SetButton(bStages, Stages == 2 ? "Lấy nước: cất hai lần" : "Lấy nước: cất một lần", true);
            InstrumentScreen.SetButton(bDiscard, null, ProducedMl > 0);
            InstrumentScreen.SetButton(bCollect, null, Producing);
            InstrumentScreen.SetButton(bMeasure, null, BottleMl > 0 || ProducedMl > 0);
            InstrumentScreen.SetButton(bOpen, BottleOpen ? "Bình chứa: để hở" : "Bình chứa: đậy kín", BottleMl > 0, !BottleOpen);
            InstrumentScreen.SetButton(bWait, null, BottleMl > 0);
            InstrumentScreen.SetButton(bOff, null, Heater || tripped);
            status.text = $"Nước ra {Fmt(ProducedMl, "0")} mL · đã bỏ {Fmt(DiscardedMl, "0")} mL · bình chứa {Fmt(BottleMl, "0")} mL · tốc độ khoảng 4 L/giờ (lab ảo tăng tốc)";
            S.SetResults(BuildResults());
        }

        bool Producing => Heater && Cooling && Feed && warm >= 2f && !tripped;

        public void ToggleHeat()
        {
            if (Heater) { Heater = false; return; }
            if (!S.Require("bat_dun")) return;
            if (!Feed) { tripped = true; S.Say("Bật đun khi chưa có nước cấp: dây đốt nóng đỏ, rơ-le nhiệt ngắt! Mở nước cấp trước.", 3); return; }
            tripped = false;
            Heater = true;
            warm = 0;
            S.Done("bat_dun");
            S.Say("Đang đun sôi nước trong bình đun…", 0);
            if (!Cooling) S.Say("Chưa mở nước làm mát: hơi nước không ngưng tụ, thoát ra ngoài, không có nước cất.", 2);
        }

        public void SetCooling(bool on) { Cooling = on; if (Cooling && Feed) S.Done("cap_nuoc"); }
        public void SetFeed(bool on) { Feed = on; if (Cooling && Feed) S.Done("cap_nuoc"); }
        public void SetStages(int n) => Stages = n;
        public void SetOpen(bool open) => BottleOpen = open;
        public void Wait(double min) => OpenMin += BottleOpen ? min : min / 12;

        public void Discard()
        {
            if (ProducedMl <= 0) return;
            DiscardedMl += ProducedMl;
            ProducedMl = 0;
            S.Done("bo_phan_dau");
            S.Say($"Đã bỏ phần nước đầu (tổng {Fmt(DiscardedMl, "0")} mL) để rửa đường ống, sinh hàn.", 0);
        }

        public void Collect()
        {
            if (!Producing) return;
            if (!S.Require("lay_nuoc") && S.Guided) return;
            BottleMl += ProducedMl;
            ProducedMl = 0;
            OpenMin = 0;
            S.Done("lay_nuoc");
            S.Say($"Hứng nước cất vào bình chứa sạch ({Fmt(BottleMl, "0")} mL).", 0);
        }

        public void Measure()
        {
            if (BottleMl <= 0 && ProducedMl <= 0) return;
            if (!S.Require("kiem_tra") && S.Guided) return;
            double us = Distiller.Conductivity(Stages, DiscardedMl, BottleMl > 0 ? OpenMin : 0) * (1 + (rng.NextDouble() - 0.5) * 0.04);
            LastUs = us;
            int g = Distiller.Grade(us);
            string where = BottleMl > 0 ? $"bình chứa ({(BottleOpen ? "hở" : "kín")}, {OpenMin:0} phút)" : "nước đang chảy ra";
            readings.Add((DiscardedMl, us, where));
            S.Done("kiem_tra");
            S.Say($"Độ dẫn điện ({where}): {Fmt(us, us < 1 ? "0.00" : "0.0")} µS/cm ở 25 °C → " + (g == 0 ? "không đạt loại nào" : $"nước loại {g}"), g == 2 || g == 1 ? 1 : 2);
        }

        public void TurnOff()
        {
            Heater = false;
            tripped = false;
            if (S.DoneSteps.Contains("kiem_tra")) S.Done("ket_thuc");
            S.Say("Tắt dây đốt, để nguội, xả nước đặc trong bình đun, khóa nước làm mát.", 0);
        }

        public override void Tick(float dt)
        {
            if (Heater && !tripped)
            {
                if (warm < 2f) warm += dt;
                if (!Feed) { tripped = true; Heater = false; S.Say("Nước cấp bị khóa khi đang đun: bình đun cạn, rơ-le nhiệt ngắt dây đốt.", 3); }
                if (Producing) ProducedMl += dt * 120;  // tăng tốc: 120 mL mỗi giây
            }
        }

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Nước cấp (nước máy):</color> khoảng {Distiller.TapUs:0} µS/cm\n");
            if (readings.Count > 0)
            {
                sb.Append("\n<b>Các lần đo độ dẫn</b>\n");
                foreach (var (ml, us, label) in readings) sb.Append($"{label}, đã bỏ {ml:0} mL đầu: <b>{Fmt(us, us < 1 ? "0.00" : "0.0")} µS/cm</b> → {(Distiller.Grade(us) == 0 ? "không đạt" : "loại " + Distiller.Grade(us))}\n");
                sb.Append("\n<b>Phân tích chuyên sâu</b>\n");
                sb.Append("TCVN 4851 (ISO 3696): loại 1 ≤ 0,1; loại 2 ≤ 1,0; loại 3 ≤ 5,0 µS/cm (25 °C)\n");
                sb.Append($"Mỗi lần cất còn khoảng {Distiller.Carryover:P1} ion bị giọt nước cuốn theo hơi: nước máy {Distiller.TapUs:0} → cất 1 lần ≈ {Distiller.TapUs * Distiller.Carryover:0.0} → cất 2 lần ≈ {Distiller.TapUs * Distiller.Carryover * Distiller.Carryover:0.000} µS/cm (chưa tính CO₂)\n");
                sb.Append("CO₂ không khí tan vào nước cất (CO₂ + H₂O ⇌ H⁺ + HCO₃⁻): độ dẫn tăng tới khoảng 1 µS/cm, pH về khoảng 5,6. Nước cất thường không đạt loại 1; loại 1 cần trao đổi ion, màng lọc.\n");
            }
            return sb.ToString();
        }

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            float ph = time * 60;
            var f1 = new Vector2(170, 110);
            var f2 = new Vector2(540, 110);
            foreach (var (f, active) in new[] { (f1, Heater && Feed), (f2, Heater && Feed && Stages == 2) })
            {
                p.Disc(f.x, f.y, 55, new Color(0.7f, 0.85f, 1f, 0.2f));
                p.Ring(f.x, f.y, 55, new Color(0.85f, 0.92f, 1f, 0.7f), 2);
                p.FillRect(f.x - 45, f.y - 50, 90, 45, Feed ? new Color(0.35f, 0.6f, 0.95f, 0.5f) : new Color(0.3f, 0.3f, 0.3f, 0.3f));
                if (active && warm >= 2f) for (int i = 0; i < 4; i++) p.Disc(f.x - 30 + i * 20, f.y - 40 + Mathf.Repeat(time * 60 + i * 20, 40), 4, new Color(0.9f, 0.95f, 1f, 0.8f));
                p.Line(f.x - 30, f.y - 60, f.x + 30, f.y - 60, Heater && !tripped ? new Color(1f, 0.45f, 0.2f) : SchemKit.Off, 5);
                // sinh hàn chéo
                p.Line(f.x + 30, f.y + 45, f.x + 180, f.y + 170, new Color(0.85f, 0.92f, 1f, 0.6f), 10);
                SchemKit.Pipe(p, new[] { new Vector2(f.x + 60, f.y + 70), new Vector2(f.x + 160, f.y + 165) }, Cooling, ph, new Color(0.4f, 0.75f, 1f), 4);
            }
            if (Producing) p.Disc(f2.x + 180 + 5, f2.y + 120 - Mathf.Repeat(time * 80, 60), 5, new Color(0.8f, 0.9f, 1f));
            SchemKit.Vessel(p, 760, 20, 120, 150, (float)Math.Min(1, BottleMl / 2000), new Color(0.8f, 0.9f, 1f, 0.45f));
            if (!Cooling && Heater) for (int i = 0; i < 5; i++) p.Disc(f1.x + 190 + i * 8, f1.y + 180 + Mathf.Repeat(time * 50 + i * 15, 60), 7, new Color(1, 1, 1, 0.35f));
            var d = Doc;
            S.SchemLabel("b1", d.PartName("binh_dun_1"), f1.x, f1.y - 80, 11);
            S.SchemLabel("b2", d.PartName("binh_dun_2") + (Stages == 2 ? "" : " (bỏ qua)"), f2.x, f2.y - 80, 11);
            S.SchemLabel("sh", d.PartName("sinh_han_1") + (Cooling ? "" : " <color=#FFC966>(chưa có nước làm mát)</color>"), f1.x + 110, f1.y + 190, 11);
            S.SchemLabel("chua", d.PartName("binh_chua") + $"\n{Fmt(BottleMl, "0")} mL", 820, 190, 11);
            S.SchemLabel("dd", d.PartName("do_dan") + (double.IsNaN(LastUs) ? "" : $"\n<color=#73F29E>{Fmt(LastUs, LastUs < 1 ? "0.00" : "0.0")} µS/cm</color>"), 820, 300, 11);
            S.SchemLabel("trang_thai", tripped ? "<color=#FF7A7A>NGẮT NHIỆT</color>" : Producing ? "<color=#61DBFF>ĐANG CẤT</color>" : Heater ? "<color=#FFC966>ĐANG ĐUN</color>" : "<color=#73F29E>SẴN SÀNG</color>", 420, 334, 14);
        }

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            S.SetPlotAxes("Độ dẫn điện các lần đo (thang log) và ngưỡng loại nước TCVN 4851", "Lần đo", "µS/cm (log)", "", "", "0,01", "1000");
            double L(double us) => (Math.Log10(Math.Max(0.01, us)) + 2) / 5;  // 0.01 → 0, 1000 → 1
            foreach (var (lim, col) in new[] { (0.1, new Color(0.45f, 0.95f, 0.62f, 0.5f)), (1.0, new Color(0.45f, 0.86f, 1f, 0.5f)), (5.0, new Color(1f, 0.8f, 0.3f, 0.5f)) })
                p.FillRect(0, (float)(L(lim) * (p.H - 8)) + 3, p.W, 2, col);
            var tap = (float)(L(Distiller.TapUs) * (p.H - 8)) + 3;
            p.FillRect(10, 0, 40, tap, new Color(0.6f, 0.6f, 0.65f, 0.6f));
            for (int i = 0; i < readings.Count; i++)
            {
                float h = (float)(L(readings[i].us) * (p.H - 8)) + 3;
                p.FillRect(80 + i * 70, 0, 50, h, Distiller.Grade(readings[i].us) == 2 ? SchemKit.Good : new Color(1f, 0.7f, 0.35f));
            }
        }
    }

    /// <summary>
    /// Nồi hấp tiệt trùng: áp suất hơi bão hòa theo IAPWS-IF97, không khí còn lại làm nhiệt độ thấp hơn áp suất chỉ, bào tử chỉ thị giảm theo D(T).
    /// Lỗi mô phỏng: không đuổi khí, giữ không đủ thời gian, mở nắp khi còn áp suất (khóa liên động chặn; chế độ Thực hành báo nguy hiểm).
    /// </summary>
    public class AutoclaveProgram : InstrumentProgram
    {
        public override string Code => "noi_hap";
        static readonly string[] optional = { };
        public override ICollection<string> Optional => optional;

        public const double D121 = 1.8, Z = 10, N0Log = 6, SetGauge = 1.05, Atm = 1.01325;
        public bool Water { get; private set; }
        public bool Loaded { get; private set; }
        public bool LidClosed { get; private set; }
        public bool VentOpen { get; private set; } = true;
        public bool Heater { get; private set; }
        public double T { get; private set; } = 25;
        public double AirBar { get; private set; } = Atm;   // áp suất riêng của không khí (quy về 25 °C)
        public double Minutes { get; private set; }
        public double HoldMin { get; private set; }
        public double LogKill { get; private set; }
        public double HoldTarget { get; private set; } = 15;
        public string BiResult { get; private set; } = "";
        public readonly List<(double t, double T, double pg, double logN)> Trace = new List<(double, double, double, double)>();
        bool cooling;

        public double GaugeBar => LidClosed && !VentOpen ? Math.Max(0, (T >= 100 ? SteamTable.PsatBar(T) : SteamTable.PsatBar(Math.Max(1, T))) + AirBar * (T + 273.15) / 298.15 - Atm) : 0;

        Button bWater, bLoad, bLid, bVent, bHeat, bHold, bCool, bOpen, bBi;
        Text status;

        public override void Build()
        {
            bWater = S.AddButton("Châm nước đáy nồi", 0, 0, AddWater, false, 1, 13);
            bLoad = S.AddButton("Xếp đồ + chỉ thị", 1, 0, Load, false, 1, 13);
            bLid = S.AddButton("Đóng nắp", 2, 0, CloseLid);
            bVent = S.AddButton("", 0, 1, ToggleVent, false, 1, 13);
            bHeat = S.AddButton("Bật đun", 1, 1, StartHeat, true);
            bHold = S.AddButton("", 2, 1, () => HoldTarget = HoldTarget == 15 ? 5 : HoldTarget == 5 ? 30 : 15, false, 1, 13);
            bCool = S.AddButton("Tắt đun, chờ hạ áp", 0, 2, StopHeat, false, 1, 13);
            bOpen = S.AddButton("Mở nắp", 1, 2, OpenLid);
            bBi = S.AddButton("Ủ, đọc chỉ thị sinh học", 2, 2, ReadBi, false, 1, 12);
            status = S.AddText("", 3, 0, 3, 13);
            S.SetResults("Châm nước, xếp đồ kèm ống chỉ thị sinh học, đóng nắp, mở van xả, bật đun; đóng van khi hơi phun đều; giữ 121 °C trong 15 phút.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bWater, Water ? "Đã châm nước" : "Châm nước đáy nồi", !LidClosed, Water);
            InstrumentScreen.SetButton(bLoad, Loaded ? "Đã xếp đồ + chỉ thị" : "Xếp đồ + chỉ thị", !LidClosed, Loaded);
            InstrumentScreen.SetButton(bLid, LidClosed ? "Nắp đã đóng" : "Đóng nắp", !LidClosed && Water, LidClosed);
            InstrumentScreen.SetButton(bVent, VentOpen ? "Van xả: MỞ (đóng lại)" : "Van xả: đóng (mở ra)", LidClosed);
            InstrumentScreen.SetButton(bHeat, Heater ? "Đang đun" : "Bật đun", LidClosed && !Heater, Heater);
            InstrumentScreen.SetButton(bHold, $"Thời gian giữ: {HoldTarget:0} phút", !Heater);
            InstrumentScreen.SetButton(bCool, null, Heater);
            InstrumentScreen.SetButton(bOpen, null, LidClosed && !Heater);
            InstrumentScreen.SetButton(bBi, null, Loaded && !LidClosed && Minutes > 0 && BiResult == "");
            status.text = $"Nhiệt độ {T:0.0} °C · đồng hồ {GaugeBar:0.00} bar (dư) · không khí còn {AirBar / Atm:P0} · đã giữ ≥ 121 °C: {HoldMin:0.0} / {HoldTarget:0} phút";
            S.SetResults(BuildResults());
        }

        public void AddWater() { if (LidClosed) return; Water = true; S.Done("cho_nuoc"); }
        public void Load() { if (LidClosed) return; Loaded = true; S.Done("xep_do"); }
        public void CloseLid() { if (!Water || LidClosed) return; LidClosed = true; VentOpen = true; S.Done("dong_nap"); }
        public void SetHold(double m) => HoldTarget = m;

        public void ToggleVent()
        {
            if (!LidClosed) return;
            if (VentOpen && S.Guided && AirBar >= 0.03 * Atm) { S.Say($"Chưa đuổi hết không khí (còn {AirBar / Atm:P0}): để van xả mở đến khi hơi phun đều.", 2); return; }
            VentOpen = !VentOpen;
            if (!VentOpen)
            {
                if (AirBar < 0.03 * Atm) S.Done("duoi_khi");
                S.Say(AirBar < 0.03 * Atm ? "Đóng van xả: hơi đã phun đều, không khí đã được đuổi gần hết." : $"Đóng van xả khi còn {AirBar / Atm:P0} không khí: nhiệt độ sẽ thấp hơn áp suất chỉ!", AirBar < 0.03 * Atm ? 1 : 2);
            }
        }

        public void StartHeat()
        {
            if (!LidClosed || Heater) return;
            if (!S.Require("dong_nap")) return;
            Heater = true;
            cooling = false;
            S.Say(VentOpen ? "Đun sôi, hơi nước đẩy không khí ra qua van xả…" : "Đun với van xả đóng.", 0);
        }

        public void StopHeat()
        {
            if (!Heater) return;
            Heater = false;
            cooling = true;
            if (HoldMin >= HoldTarget) S.Done("giu_nhiet");
            S.Say("Tắt đun, để nồi tự hạ áp và nguội.", 0);
        }

        public void OpenLid()
        {
            if (!LidClosed || Heater) return;
            if (GaugeBar > 0.05 || T > 80)
            {
                if (S.Guided) { S.Say($"Khóa nắp liên động: còn {GaugeBar:0.00} bar, {T:0} °C. Chờ áp suất về 0 và dưới 80 °C.", 2); return; }
                S.Say($"NGUY HIỂM: mở nắp khi còn {GaugeBar:0.00} bar, {T:0} °C: hơi nóng phụt ra, chất lỏng sôi trào gây bỏng!", 3);
            }
            LidClosed = false;
            VentOpen = true;
            AirBar = Atm;
            S.Done("ha_ap");
        }

        public void ReadBi()
        {
            if (!Loaded || LidClosed || BiResult != "") return;
            double survivorsLog = N0Log - LogKill;
            bool sterile = survivorsLog < -1;  // dưới 0,1 bào tử sống dự kiến
            BiResult = sterile ? "không đổi màu (tím): ĐẠT" : "chuyển vàng (bào tử còn sống phát triển): KHÔNG ĐẠT";
            S.Done("kiem_tra");
            S.Say($"Ủ chỉ thị 48 giờ ở 56 °C: {BiResult}", sterile ? 1 : 3);
        }

        public override void Tick(float dt)
        {
            if (!LidClosed && !Heater) return;
            double dtMin = dt * 2;  // 1 giây thật = 2 phút
            Minutes += dtMin;
            if (Heater)
            {
                if (VentOpen)
                {
                    // đun tới sôi ở áp suất khí quyển; hơi đẩy không khí ra theo hàm mũ khi đã sôi
                    T = Math.Min(100, T + 12 * dtMin);
                    if (T >= 99.5) AirBar *= Math.Exp(-dtMin / 1.2);
                }
                else
                {
                    // bộ điều khiển giữ áp suất đồng hồ 1,05 bar: nhiệt độ là nghiệm của p_hơi(T) + p_không khí = p đặt
                    double target = ControlTemp();
                    T += Math.Sign(target - T) * Math.Min(Math.Abs(target - T), 4 * dtMin);
                }
            }
            else if (cooling) T = 25 + (T - 25) * Math.Exp(-dtMin / 18);
            if (!LidClosed) T = Math.Max(25, T - 5 * dtMin);
            double k = Sterilization.LogReduction(T, dtMin, D121, Z);
            LogKill += k;
            if (Heater && !VentOpen && GaugeBar >= SetGauge - 0.02) { HoldMin += dtMin; if (HoldMin >= HoldTarget) { StopHeat(); } }
            Trace.Add((Minutes, T, GaugeBar, Math.Max(-3, N0Log - LogKill)));
        }

        /// <summary>Nhiệt độ mà tại đó p_hơi(T) + p_không khí(T) = áp suất đặt (bộ điều khiển giữ áp suất đồng hồ).</summary>
        double ControlTemp()
        {
            double lo = 25, hi = 140;
            for (int i = 0; i < 50; i++)
            {
                double m = (lo + hi) / 2;
                double p = SteamTable.PsatBar(m) + AirBar * (m + 273.15) / 298.15;
                if (p < SetGauge + Atm) lo = m; else hi = m;
            }
            return (lo + hi) / 2;
        }

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Hơi bão hòa (IAPWS-IF97):</color> 121 °C ↔ {SteamTable.PsatBar(121):0.000} bar tuyệt đối = {SteamTable.PsatBar(121) - Atm:0.00} bar dư\n");
            if (Minutes > 0)
            {
                double pAirNow = AirBar * (T + 273.15) / 298.15;
                sb.Append($"\nĐồng hồ {GaugeBar:0.00} bar dư = p hơi {(LidClosed ? SteamTable.PsatBar(Math.Max(1, T)) : 0):0.00} + p không khí {pAirNow:0.00} − p khí quyển {Atm:0.00} bar\n");
                sb.Append($"Nhiệt độ thật: <b>{T:0.0} °C</b>" + (AirBar > 0.05 && !VentOpen ? $" <color=#FFC966>(còn không khí: thấp hơn {SteamTable.TsatC(SetGauge + Atm):0.0} °C ứng với áp suất chỉ)</color>" : "") + "\n");
                sb.Append($"\n<b>Phân tích chuyên sâu: diệt bào tử</b>\nD(T) = {D121} × 10^((121 − T)/{Z}) phút · đã giảm <b>{LogKill:0.0} bậc</b> (10⁶ bào tử → còn khoảng 10^{N0Log - LogKill:0.0})\n");
                sb.Append($"Giữ {HoldMin:0.0} phút ở áp suất đặt. Cần giảm ≥ 7 bậc (còn dưới 0,1 bào tử) để chỉ thị đạt.\n");
                if (BiResult != "") sb.Append($"Chỉ thị sinh học: <b>{BiResult}</b>\n");
            }
            return sb.ToString();
        }

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            var c = new Vector2(300, 170);
            p.FillRect(c.x - 120, c.y - 140, 240, 270, new Color(0.55f, 0.58f, 0.62f));
            p.FillRect(c.x - 105, c.y - 125, 210, 240, new Color(0.1f, 0.12f, 0.15f));
            if (Water) p.FillRect(c.x - 105, c.y - 125, 210, 40, new Color(0.35f, 0.6f, 0.95f, 0.6f));
            if (Heater) p.Line(c.x - 90, c.y - 132, c.x + 90, c.y - 132, new Color(1f, 0.45f, 0.2f), 5);
            float steam = Mathf.Clamp01((float)(T - 90) / 30);
            for (int i = 0; i < 18; i++) p.Disc(c.x - 90 + (i * 37) % 180, c.y - 70 + Mathf.Repeat(time * 30 + i * 23, 170), 5, new Color(1, 1, 1, 0.25f * steam));
            float air = (float)(AirBar / Atm);
            for (int i = 0; i < (int)(air * 20); i++) p.Disc(c.x - 80 + (i * 53) % 160, c.y + 90 - (i * 31) % 120, 4, new Color(0.6f, 0.9f, 0.6f, 0.7f));
            if (Loaded) { SchemKit.Box(p, c.x - 60, c.y - 60, 120, 90, new Color(0.25f, 0.27f, 0.3f)); p.FillRect(c.x + 20, c.y - 40, 14, 40, new Color(0.6f, 0.3f, 0.8f)); }
            p.FillRect(c.x - 130, c.y + 130, 260, LidClosed ? 14 : 4, new Color(0.7f, 0.72f, 0.75f));
            // van xả
            p.FillRect(c.x + 80, c.y + 144, 12, 30, VentOpen ? new Color(1f, 0.8f, 0.3f) : SchemKit.Off);
            if (VentOpen && T > 99) for (int i = 0; i < 4; i++) p.Disc(c.x + 86, c.y + 180 + Mathf.Repeat(time * 60 + i * 10, 40), 4, new Color(1, 1, 1, 0.5f));
            // đồng hồ
            var g = new Vector2(700, 200);
            p.Ring(g.x, g.y, 70, SchemKit.Edge, 4);
            float a = Mathf.Lerp(220, -40, (float)(GaugeBar / 2.5)) * Mathf.Deg2Rad;
            p.Line(g.x, g.y, g.x + Mathf.Cos(a) * 60, g.y + Mathf.Sin(a) * 60, GaugeBar > 1.6 ? new Color(1f, 0.4f, 0.4f) : SchemKit.Good, 4);
            var d = Doc;
            S.SchemLabel("noi", d.PartName("noi") + $"\n<color=#FFB27A>{T:0.0} °C</color>", c.x, c.y + 205 > 340 ? 20 : c.y + 205, 12);
            S.SchemLabel("van", d.PartName("van_xa") + (VentOpen ? " · mở" : " · đóng"), c.x + 200, c.y + 150, 11);
            S.SchemLabel("dh", d.PartName("dong_ho") + $"\n<color=#73F29E>{GaugeBar:0.00} bar</color>", g.x, g.y - 95, 12);
            S.SchemLabel("kk", $"<color=#9AE6A8>● không khí còn {AirBar / Atm:P0}</color>", c.x, 18, 11);
            S.SchemLabel("trang_thai", !LidClosed ? "<color=#73F29E>NẮP MỞ</color>" : Heater ? (VentOpen ? "<color=#FFC966>ĐANG ĐUỔI KHÍ</color>" : "<color=#61DBFF>ĐANG TIỆT TRÙNG</color>") : GaugeBar > 0.05 ? "<color=#FFC966>ĐANG HẠ ÁP</color>" : "<color=#73F29E>SẴN SÀNG</color>", 700, 330, 14);
        }

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            double tMax = Math.Max(60, Minutes * 1.05);
            S.SetPlotAxes("Nhiệt độ (cam), áp suất dư ×50 (xanh), lg số bào tử ×20 (tím) theo thời gian", "Thời gian (phút)", "°C", "0", Fmt(tMax, "0"), "0", "140");
            if (Trace.Count < 2) return;
            p.Poly(Trace.Select(q => InstrumentScreen.Map(p, q.t, q.T, 0, tMax, 0, 140)).ToList(), new Color(1f, 0.6f, 0.35f), 2);
            p.Poly(Trace.Select(q => InstrumentScreen.Map(p, q.t, q.pg * 50, 0, tMax, 0, 140)).ToList(), new Color(0.45f, 0.86f, 1f), 2);
            p.Poly(Trace.Select(q => InstrumentScreen.Map(p, q.t, Math.Max(0, q.logN + 3) * 20, 0, tMax, 0, 140)).ToList(), new Color(0.75f, 0.55f, 1f), 2);
            var r = InstrumentScreen.Map(p, 0, 121, 0, tMax, 0, 140);
            p.FillRect(0, r.y, p.W, 1, new Color(1, 1, 1, 0.3f));
        }
    }
}
