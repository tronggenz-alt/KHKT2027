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
    /// 5.3.1 & 5.3.2. Tủ mát bảo quản sinh phẩm 4 °C & Tủ đông sâu −20 °C — mã: tu_mat_4, tu_dong_am20
    /// </summary>
    [DisallowMultipleComponent]
    public class FridgeController : MonoBehaviour, IInteractable
    {
        public string fridgeId = "tu_mat_4"; // tu_mat_4 | tu_dong_am20
        public bool isDeepFreezer = false;
        public float setTempC = 4.0f;
        public float currentTempC = 4.0f;
        public bool doorOpen = false;
        public float doorOpenSeconds = 0f;
        public bool alarmTriggered = false;
        public bool taqLeftAtRoomTempMistake = false;
        public float taqActivityRetention = 1.0f; // 1.0 khi giữ trong hộp đá; giảm nếu để ngoài nhiệt độ phòng
        public BioHinge leftDoorHinge;
        public BioHinge rightDoorHinge;

        void Awake()
        {
            if (fridgeId == "tu_dong_am20" || isDeepFreezer)
            {
                isDeepFreezer = true;
                setTempC = -20.0f;
                currentTempC = -20.0f;
            }
        }

        public string UseHint => $"Tủ bảo quản ({fridgeId}) [Đặt: {BioFeedback.FormatVi(setTempC, "0.0")} °C · Thực: {BioFeedback.FormatVi(currentTempC, "0.0")} °C · Cửa: {(doorOpen ? $"ĐANG MỞ ({BioFeedback.FormatVi(doorOpenSeconds, "0")} s)" : "ĐÓNG KÍN")}{(alarmTriggered ? " · CÒI BÁO ĐỎ (> 60 s)!" : "")}] · Bấm: mở/đóng cửa & lấy sinh phẩm";

        public void SimulateDoorLeftOpen(float openSec)
        {
            doorOpen = true;
            doorOpenSeconds = openSec;
            currentTempC = isDeepFreezer
                ? Mathf.Min(-2.0f, setTempC + openSec * 0.12f)
                : Mathf.Min(18.0f, setTempC + openSec * 0.08f);

            if (openSec > 60.0f)
            {
                alarmTriggered = true;
                BioFeedback.RecordMistake(fridgeId, "mo_cua_tu_lanh_qua_60_giay",
                    $"Mở cửa {fridgeId} quá lâu ({BioFeedback.FormatVi(openSec, "0")} s > 60 s) làm nhiệt độ vọt lên {BioFeedback.FormatVi(currentTempC, "0.1")} °C!",
                    "Màn LED đỏ nhấp nháy và còi báo động kêu! Nhiệt độ tăng vượt ngưỡng an toàn làm giảm hoạt tính enzyme/kháng sinh và hỏng môi trường bảo quản.");
            }
        }

        public void TakeEnzymeOutOfFreezer(bool placedOnIceImmediately)
        {
            if (!placedOnIceImmediately)
            {
                taqLeftAtRoomTempMistake = true;
                taqActivityRetention = 0.25f;
                BioFeedback.RecordMistake("tu_dong_am20", "lay_enzyme_taq_khong_dat_hop_da",
                    "Lấy enzyme Taq polymerase từ tủ đông −20 °C ra bàn ở nhiệt độ phòng mà KHÔNG đặt vào hộp đá!",
                    "Enzyme và mồi PCR bị giảm hoạt tính nhanh ở nhiệt độ phòng làm hiệu suất nhân bản PCR tụt mạnh.");
            }
            else
            {
                taqLeftAtRoomTempMistake = false;
                taqActivityRetention = 1.0f;
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("tu_dong_am20", "lay_enzyme_dat_hop_da",
                    "Đã lấy bộ sinh phẩm PCR (Taq polymerase, dNTPs, mồi) từ tủ đông −20 °C đặt ngay vào hộp đá 0–4 °C.");
            }
        }

        public void Use(GameObject user)
        {
            doorOpen = !doorOpen;
            if (!doorOpen)
            {
                doorOpenSeconds = 0f;
                alarmTriggered = false;
                currentTempC = setTempC;
            }
            if (leftDoorHinge != null) leftDoorHinge.SetOpen(doorOpen);
            if (rightDoorHinge != null) rightDoorHinge.SetOpen(doorOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction(fridgeId, doorOpen ? "mo_cua_tu" : "dong_cua_tu",
                $"{fridgeId}: {(doorOpen ? "Mở cửa lấy sinh phẩm" : $"Đóng kín cửa, nhiệt độ ổn định ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C")}.");
        }
    }

    /// <summary>
    /// 5.3.3. Kệ inox trưng bày mẫu ngâm bảo quản — mã: ke_mau
    /// </summary>
    [DisallowMultipleComponent]
    public class SpecimenShelf : MonoBehaviour, IInteractable
    {
        public static readonly string[] Specimens =
        {
            "Mẫu ngâm Cá xương (Lớp Osteichthyes — hệ tuần hoàn đơn)",
            "Mẫu ngâm Ếch đồng (Lớp Amphibia — tim 3 ngăn, hô hấp da & phổi)",
            "Mẫu ngâm Thằn lằn bóng (Lớp Reptilia — vảy sừng chống mất nước)",
            "Mẫu ngâm Giun đất & Tôm sông (Ngành Annelida & Arthropoda)",
            "Mẫu tiêu bản lá & quả các họ Thực vật hạt kín (Magnoliophyta)"
        };
        public int currentIndex = 0;

        public string UseHint => $"Kệ mẫu ngâm bảo quản [Đang xem lọ {currentIndex + 1}/{Specimens.Length}: {Specimens[currentIndex]}] · Bấm: xoay xem & đọc thẻ phân loại";

        public void Use(GameObject user)
        {
            string item = Specimens[currentIndex];
            currentIndex = (currentIndex + 1) % Specimens.Length;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("ke_mau", "xem_mau_ngam", $"Quan sát mẫu ngâm kho sinh phẩm: {item}.");
        }
    }

    /// <summary>
    /// 5.3.3. Mô hình dạy học trực quan (ADN xoắn kép Watson–Crick, Tế bào nhân thực, Bộ xương thu nhỏ) — mã: mo_hinh_day_hoc
    /// </summary>
    [DisallowMultipleComponent]
    public class TeachingModel : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public static readonly string[] BasePairDetails =
        {
            "Cặp base A – T (Adenine liên kết với Thymine bằng 2 liên kết hiđrô)",
            "Cặp base G – C (Guanine liên kết với Cytosine bằng 3 liên kết hiđrô — bền nhiệt hơn)",
            "Khung đường Deoxyribose – Photphat (mạch полиnucleotit chạy ngược chiều 5'→3' và 3'→5')",
            "Chu kỳ xoắn phải B-DNA: 10 cặp base / vòng xoắn, dài 3,4 nm (34 Å), đường kính 2,0 nm"
        };
        public int index = 0;
        public float yawAngle = 0f;

        public string UseHint => $"Mô hình cấu trúc ADN xoắn kép & Tế bào [{BasePairDetails[index]}] · Bấm: xem chi tiết cặp base · Con lăn: xoay mô hình";

        public void Use(GameObject user)
        {
            string info = BasePairDetails[index];
            index = (index + 1) % BasePairDetails.Length;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("mo_hinh_day_hoc", "chon_cap_base_adn", $"Mô hình ADN: {info}.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            yawAngle = (yawAngle + Mathf.Sign(delta) * 18f) % 360f;
            transform.localRotation = Quaternion.Euler(0f, yawAngle, 0f);
        }
    }

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

