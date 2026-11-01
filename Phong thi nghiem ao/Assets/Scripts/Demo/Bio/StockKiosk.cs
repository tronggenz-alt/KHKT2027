// Tách từ StorageTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.3.4. Sổ kho điện tử (màn hình cảm ứng cạnh cửa kho sinh phẩm) — mã: so_kho_dien_tu
    /// </summary>
    [DisallowMultipleComponent]
    public class StockKiosk : MonoBehaviour, IInteractable
    {
        public int selectedItemIndex = 0;
        public bool isOnlineMode = false;
        public StockProgram program = new StockProgram();

        void Awake()
        {
            if (program == null) program = new StockProgram();
            program.kiosk = this;
        }

        public BioStockItemEntry CurrentItem
        {
            get
            {
                BioCatalogLoader.EnsureLoaded();
                if (BioCatalogLoader.StockItems.Count == 0) return null;
                return BioCatalogLoader.StockItems[selectedItemIndex % BioCatalogLoader.StockItems.Count];
            }
        }

        public string UseHint
        {
            get
            {
                var item = CurrentItem;
                string desc = item != null
                    ? $"{item.ten} (Tủ: {item.tu}/{item.ngan} · Tồn: {BioFeedback.FormatVi(item.so_luong, "0.#")} {item.don_vi} · {item.cap_an_toan})"
                    : "35 sinh phẩm BSL-1";
                return $"Sổ kho điện tử [{desc}] · Bấm: ghi phiếu lấy/trả sinh phẩm & chuyển mục";
            }
        }

        public void CheckoutItem(string ma, float qty)
        {
            BioCatalogLoader.EnsureLoaded();
            for (int i = 0; i < BioCatalogLoader.StockItems.Count; i++)
            {
                var it = BioCatalogLoader.StockItems[i];
                if (it.ma == ma)
                {
                    it.so_luong = Mathf.Max(0f, it.so_luong - qty);
                    BioFeedback.PlaySuccess();
                    BioFeedback.LogAction("so_kho_dien_tu", "xuat_kho_sinh_pham",
                        $"Phiếu xuất kho: Lấy {BioFeedback.FormatVi(qty, "0.#")} {it.don_vi} '{it.ten}' ({it.ma}) từ {it.tu}/{it.ngan} — Còn lại: {BioFeedback.FormatVi(it.so_luong, "0.#")} {it.don_vi}.");
                    return;
                }
            }
        }

        public void Use(GameObject user)
        {
            BioCatalogLoader.EnsureLoaded();
            var item = CurrentItem;
            if (item != null)
            {
                CheckoutItem(item.ma, 1f);
                selectedItemIndex = (selectedItemIndex + 1) % BioCatalogLoader.StockItems.Count;
            }
        }
    }
}
