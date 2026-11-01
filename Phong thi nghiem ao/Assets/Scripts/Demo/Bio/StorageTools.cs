// -----------------------------------------------------------------------------
// StorageTools.cs — Toàn bộ thiết bị và dụng cụ Kho sinh phẩm
// (Mục 5.3 PROMPT_DUNG_CU_GIAO_DIEN.md & Mục 2 HOP_DONG_GIAO_DIEN_BACKEND.md).
// -----------------------------------------------------------------------------
using System;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{

    /// <summary>
    /// Màn hình vận hành Sổ kho điện tử (5.3.4) — kế thừa InstrumentProgram.
    /// </summary>
    public class StockProgram : InstrumentProgram
    {
        public override string Code => "so_kho_dien_tu";
        public StockKiosk kiosk;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Mục tiếp theo", 0, 0, () =>
            {
                if (kiosk != null) kiosk.Use(null);
                S.Done("tra_cuu");
            });

            S.AddButton("Xuất kho (1 đơn vị)", 1, 0, () =>
            {
                if (kiosk != null)
                {
                    var it = kiosk.CurrentItem;
                    if (it != null) kiosk.CheckoutItem(it.ma, 1f);
                }
                S.Done("xuat_kho");
            });

            S.AddButton("Đổi chế độ Đồng bộ", 2, 0, () =>
            {
                if (kiosk != null) kiosk.isOnlineMode = !kiosk.isOnlineMode;
                S.Say($"Chế độ kho: {(kiosk != null && kiosk.isOnlineMode ? "Đồng bộ Web" : "Ngoại tuyến")}", 1);
            });

            S.SetPlotAxes("CƠ CẤU 35 SINH PHẨM TRONG KHO THEO VỊ TRÍ", "Khu lưu trữ", "Số mặt hàng", "1", "4", "0", "15");
            Refresh();
        }

        public override void Refresh()
        {
            var it = kiosk != null ? kiosk.CurrentItem : null;
            if (it == null)
            {
                S.SetResults("Đang nạp danh mục sinh phẩm BSL-1...");
                return;
            }

            S.SetResults(
                $"<b>SỔ KHO ĐIỆN TỬ SINH PHẨM BSL-1</b>\n" +
                $"• Mã hàng: <b>{it.ma}</b>\n" +
                $"• Tên mặt hàng: <b>{it.ten}</b>\n" +
                $"• Vị trí lưu trữ: <b>{it.tu} / {it.ngan}</b>\n" +
                $"• Tồn kho: <b>{BioFeedback.FormatVi(it.so_luong, "0.#")} {it.don_vi}</b>\n" +
                $"• Cấp an toàn: <b>{it.cap_an_toan}</b> · Số lô: {it.soLo}\n" +
                $"• Trạng thái: <b>{(kiosk != null && kiosk.isOnlineMode ? "Đồng bộ Web (/lab/sinh)" : "Lưu tạm ngoại tuyến")}</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(20, 32, 45, 255));
            // Vẽ sơ đồ tủ mát 4C, tủ đông -20C, kệ kho
            p.FillRect(30, 30, 70, 150, new Color32(60, 120, 180, 255)); // Tủ mát
            p.FillRect(120, 30, 70, 150, new Color32(40, 80, 150, 255));  // Tủ đông
            p.FillRect(210, 30, 80, 150, new Color32(80, 100, 110, 255)); // Kệ kho
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(12, 20, 30, 255));
            // 4 cột phân bố: Tủ mát (12), Tủ đông (10), Kệ hóa chất (8), Vật tư tiêu hao (5)
            int[] counts = { 12, 10, 8, 5 };
            for (int i = 0; i < counts.Length; i++)
            {
                p.FillRect(40 + i * 65, 30, 45, counts[i] * 10, new Color32(65, 180, 225, 230));
            }
        }
    }
}

