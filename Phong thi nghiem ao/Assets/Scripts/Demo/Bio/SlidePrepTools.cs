// -----------------------------------------------------------------------------
// SlidePrepTools.cs — Bộ dụng cụ làm tiêu bản hiển vi, hộp tiêu bản cố định
// và bảng điều khiển lớp học (Mục 5.1.3 – 5.1.6, 5.1.12 – 5.1.14 PROMPT_DUNG_CU_GIAO_DIEN.md).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{

    /// <summary>
    /// Chương trình màn hình vận hành Bảng điều khiển lớp học (5.1.14) — kế thừa InstrumentProgram.
    /// </summary>
    public class ClassroomProgram : InstrumentProgram
    {
        public override string Code => "bang_dieu_khien_lop";
        public ClassroomConsole console;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Đổi bài tiếp theo", 0, 0, () =>
            {
                if (console != null) console.Use(null);
                S.Done("chon_bai");
            });

            S.AddButton("Đèn phòng (Bật/Giảm)", 1, 0, () =>
            {
                if (console != null) console.roomLightsOn = !console.roomLightsOn;
                S.Done("dieu_khien_den");
            });

            S.AddButton("Đồng bộ 9 bàn", 2, 0, () =>
            {
                if (console != null) console.Use(null);
                S.Say("Đã đồng bộ bài thí nghiệm tới 9/9 bàn học sinh.", 1);
            });

            S.SetPlotAxes("TIẾN ĐỘ THÍ NGHIỆM 9 BÀN HỌC SINH (%)", "Bàn", "Tiến độ (%)", "1", "9", "0", "100");
            Refresh();
        }

        public override void Refresh()
        {
            var exp = console != null ? console.GetCurrentExperiment() : null;
            string code = exp != null ? exp.ma : "te_bao_hanh";
            string title = exp != null ? exp.ten : "Quan sát tế bào biểu bì vảy hành";
            int lop = exp != null ? exp.lop : 10;

            S.SetResults(
                $"<b>BẢNG ĐIỀU KHIỂN LỚP HỌC (9 BÀN)</b>\n" +
                $"• Bài đang phát: <b>{code}</b> (Lớp {lop})\n" +
                $"• Tên bài: <b>{title}</b>\n" +
                $"• Đèn phòng: <b>{(console == null || console.roomLightsOn ? "BẬT (100%)" : "GIẢM SÁNG SOI KÍNH")}</b>\n" +
                $"• Trạng thái: <b>9/9 bàn học sinh đã đồng bộ</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(18, 32, 48, 255));
            // Vẽ lưới 9 bàn học sinh
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    p.FillRect(40 + c * 80, 30 + r * 50, 60, 35, new Color32(35, 75, 110, 255));
                    p.FillRect(45 + c * 80, 35 + r * 50, 50, 8, new Color32(80, 210, 160, 255));
                }
            }
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(12, 22, 34, 255));
            // Vẽ 9 cột tiến độ
            for (int i = 0; i < 9; i++)
            {
                int h = 40 + (i * 13) % 90;
                p.FillRect(40 + i * 26, 30, 18, h, new Color32(75, 195, 145, 230));
            }
        }
    }
}

