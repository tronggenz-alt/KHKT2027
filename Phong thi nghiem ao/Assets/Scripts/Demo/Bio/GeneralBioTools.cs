// -----------------------------------------------------------------------------
// GeneralBioTools.cs — Các dụng cụ và thiết bị phòng thí nghiệm Sinh học chung
// (Mục 5.1.7 – 5.1.11, 5.1.15 – 5.1.21 PROMPT_DUNG_CU_GIAO_DIEN.md).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{

    /// <summary>
    /// Màn hình vận hành Tủ an toàn sinh học cấp II A2 (5.2.1) — kế thừa InstrumentProgram.
    /// </summary>
    public class BscProgram : InstrumentProgram
    {
        public override string Code => "tu_an_toan_sinh_hoc";
        public BiosafetyCabinetController cabinet;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Quạt HEPA (Bật/Tắt)", 0, 0, () =>
            {
                if (cabinet != null)
                {
                    cabinet.fanOn = !cabinet.fanOn;
                    cabinet.SyncGlobalAsepticFlags();
                }
                S.Done("chay_quat");
            });

            S.AddButton("Kính trước: 20 cm", 1, 0, () =>
            {
                if (cabinet != null) cabinet.SetSashHeight(20f);
                S.Done("ha_kinh");
            });

            S.AddButton("Lau cồn 70%", 2, 0, () =>
            {
                if (cabinet != null) cabinet.CleanSurfaceWithEthanol();
                S.Done("lau_con");
            });

            S.AddButton("Tua làm sạch (+5 ph)", 0, 1, () =>
            {
                if (cabinet != null) cabinet.FastForwardPurge(5f);
                S.Done("thoi_gian_lam_sach");
            });

            S.SetPlotAxes("VẬN TỐC DÒNG KHÍ TỦ BSC A2 (m/s)", "Vị trí", "Vận tốc", "0", "1", "0", "1");
            Refresh();
        }

        public override void Refresh()
        {
            bool fan = cabinet != null && cabinet.fanOn;
            float sash = cabinet != null ? cabinet.sashHeightCm : 20f;
            float purge = cabinet != null ? cabinet.purgeMinutes : 0f;

            S.SetResults(
                $"<b>TỦ AN TOÀN SINH HỌC CẤP II LOẠI A2 (70/30)</b>\n" +
                $"• Quạt HEPA: <b>{(fan ? "ĐANG CHẠY" : "TẮT")}</b>\n" +
                $"• Dòng khí đi xuống (Downflow): <b>{BioFeedback.FormatVi(cabinet != null ? cabinet.downflowVelocityMs : 0f, "0.00")} m/s</b> (Chuẩn: 0,35 m/s)\n" +
                $"• Dòng khí bảo vệ (Inflow): <b>{BioFeedback.FormatVi(cabinet != null ? cabinet.inflowVelocityMs : 0f, "0.00")} m/s</b> (Chuẩn: 0,52 m/s)\n" +
                $"• Độ mở kính sash: <b>{BioFeedback.FormatVi(sash, "0.0")} cm</b> {(sash <= 20.5f ? "(Đúng chuẩn 20 cm)" : "<color=#FF6666>(CẢNH BÁO QUÁ CAO!)</color>")}\n" +
                $"• Thời gian chạy làm sạch: <b>{BioFeedback.FormatVi(purge, "0.0")} phút</b> {(purge >= 5f ? "(Đạt vô trùng)" : "<color=#FFC966>(Cần >= 5 phút)</color>")}");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(10, 24, 20, 255));
            p.FillRect(30, 20, 240, 160, new Color32(20, 60, 50, 255));
            p.FillRect(30, 80, 20, 100, new Color32(100, 200, 240, 160));
            p.FillRect(30, 20, 240, 12, new Color32(180, 190, 200, 255));
            p.Line(120, 160, 120, 40, new Color32(80, 220, 180, 240), 3);
            p.Line(180, 160, 180, 40, new Color32(80, 220, 180, 240), 3);
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(8, 18, 16, 255));
            p.Line(40, 50, 240, 50, new Color32(80, 220, 180, 255), 2);
            p.Line(40, 90, 240, 90, new Color32(100, 180, 255, 255), 2);
        }
    }

    /// <summary>
    /// Màn hình vận hành Tủ ấm vi sinh (5.2.4) — kế thừa InstrumentProgram.
    /// </summary>
    public class IncubatorProgram : InstrumentProgram
    {
        public override string Code => "tu_am_vi_sinh";
        public IncubatorController incubator;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Nhiệt độ +1 °C", 0, 0, () =>
            {
                if (incubator != null) incubator.OnScroll(1f);
                S.Done("cai_dat_nhiet_do");
            });

            S.AddButton("Nhiệt độ -1 °C", 1, 0, () =>
            {
                if (incubator != null) incubator.OnScroll(-1f);
                S.Done("cai_dat_nhiet_do");
            });

            S.AddButton("Tua ủ 24 giờ", 2, 0, () =>
            {
                if (incubator != null) incubator.FastForwardHours(24f);
                S.Done("u_mau");
            });

            S.AddButton("Tua ủ 48 giờ", 0, 1, () =>
            {
                if (incubator != null) incubator.FastForwardHours(48f);
                S.Done("u_mau");
            });

            S.SetPlotAxes("NHIỆT ĐỘ BUỒNG Ủ THEO THỜI GIAN", "Thời gian (h)", "Nhiệt độ (°C)", "0", "48", "20", "60");
            Refresh();
        }

        public override void Refresh()
        {
            float tSet = incubator != null ? incubator.targetTempC : 37f;
            float tCur = incubator != null ? incubator.currentTempC : 37f;
            float hrs = incubator != null ? incubator.incubatedHours : 0f;
            int count = incubator != null ? incubator.dishesInside.Count : 0;

            S.SetResults(
                $"<b>TỦ ẤM VI SINH (INCUBATOR)</b>\n" +
                $"• Nhiệt độ cài đặt: <b>{BioFeedback.FormatVi(tSet, "0.0")} °C</b>\n" +
                $"• Nhiệt độ thực tế: <b>{BioFeedback.FormatVi(tCur, "0.0")} °C</b> (Sai số ±0,5 °C)\n" +
                $"• Thời gian đã ủ: <b>{BioFeedback.FormatVi(hrs, "0.0")} giờ</b>\n" +
                $"• Số đĩa Petri trong tủ: <b>{count} đĩa</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(24, 18, 12, 255));
            p.FillRect(40, 30, 220, 150, new Color32(60, 45, 30, 255));
            p.FillRect(50, 40, 200, 130, new Color32(35, 25, 18, 255));
            // Các giá inox
            p.FillRect(55, 75, 190, 4, new Color32(180, 170, 160, 255));
            p.FillRect(55, 120, 190, 4, new Color32(180, 170, 160, 255));
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(20, 14, 10, 255));
            // Đường nhiệt độ ổn định ở 37 độ
            p.Line(40, 90, 240, 90, new Color32(245, 166, 35, 255), 2);
        }
    }

}
