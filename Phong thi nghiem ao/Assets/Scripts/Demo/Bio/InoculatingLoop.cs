// Tách từ MicrobioTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Rendering;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.2.3. Que cấy vòng (Ø 3 mm), que cấy thẳng, que cấy nhựa dùng 1 lần — mã: que_cay_vong, que_cay_thang, que_cay_nhua
    /// </summary>
    [DisallowMultipleComponent]
    public class InoculatingLoop : MonoBehaviour, IInteractable
    {
        public string loopToolId = "que_cay_vong";
        public bool isDisposablePlastic = false;
        public bool isSterile = false;
        public float wireTempC = 25.0f; // >= 750 °C khi đốt đỏ rực; phải để nguội < 42 °C trước khi chạm vi khuẩn
        public string carriedOrganismId = "";
        public double carriedCfuPerMl = 0.0;
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = loopToolId;
            bioTool.toolDisplayName = isDisposablePlastic ? "Que cấy nhựa vô trùng dùng 1 lần" : "Que cấy vòng kim loại Ø 3 mm";
            bioTool.kind = BioToolKind.InoculationLoop;
            if (isDisposablePlastic)
            {
                isSterile = true;
                bioTool.isSterile = true;
            }
        }

        public string UseHint
        {
            get
            {
                if (isDisposablePlastic)
                    return $"Que cấy nhựa dùng 1 lần [Vô trùng sẵn · Mẫu: {(string.IsNullOrEmpty(carriedOrganismId) ? "Chưa lấy" : carriedOrganismId)}] · Dùng xong bỏ thùng nguy hại";
                string state = !isSterile ? "CHƯA KHỬ TRÙNG" : (wireTempC > 45f ? $"ĐANG NÓNG ({BioFeedback.FormatVi(wireTempC, "0")} °C — chờ nguội!)" : "Đã vô trùng & nguội");
                return $"Que cấy ({loopToolId}) [{state} · Mẫu: {(string.IsNullOrEmpty(carriedOrganismId) ? "Trống" : carriedOrganismId)}] · Bấm: hơ đỏ trên đèn cồn & để nguội 8 s";
            }
        }

        public void FlameSterilize(bool waitCoolDown = true)
        {
            if (isDisposablePlastic)
            {
                BioFeedback.RecordMistake("que_cay_nhua", "dot_que_cay_nhua_tren_den_con",
                    "Đưa que cấy nhựa dùng 1 lần vào ngọn lửa đèn cồn làm nhựa chảy sun và bốc khói!",
                    "Que cấy nhựa đã được tiệt trùng sẵn bằng tia gamma trong túi kín; chỉ dùng 1 lần rồi bỏ vào thùng rác nguy hại, tuyệt đối không đốt.");
                return;
            }
            isSterile = true;
            wireTempC = waitCoolDown ? 32.0f : 650.0f;
            carriedOrganismId = "";
            carriedCfuPerMl = 0.0;
            if (bioTool != null)
            {
                bioTool.isSterile = true;
                bioTool.temperatureC = wireTempC;
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction(loopToolId, "khu_trung_que_cay",
                waitCoolDown
                    ? $"Đã nung đỏ đầu {loopToolId} trên ngọn lửa đèn cồn (≥ 1,5 s) và chờ nguội 8 s xuống {BioFeedback.FormatVi(wireTempC, "0")} °C (sterile = true)."
                    : $"Đã nung đỏ đầu {loopToolId} lên {BioFeedback.FormatVi(wireTempC, "0")} °C (CHƯA ĐỂ NGUỘI!).");
        }

        public bool PickInoculumFromCulture(string organismId, double cfuPerMl)
        {
            if (!isSterile)
            {
                BioFeedback.RecordMistake(loopToolId, "lay_giong_bang_que_chua_dot",
                    "Dùng que cấy chưa khử trùng nhúng vào ống giống vi sinh vật!",
                    "Làm nhiễm bẩn ống giống gốc. Phải nung đỏ dây que cấy và để nguội trước khi lấy mẫu.");
                return false;
            }
            if (wireTempC > 45.0f)
            {
                carriedOrganismId = organismId;
                carriedCfuPerMl = 0.0; // Vi khuẩn chết sạch do sốc nhiệt!
                BioFeedback.RecordMistake(loopToolId, "cham_que_cay_nong_giet_vi_khuan",
                    $"Chạm que cấy còn nóng ({BioFeedback.FormatVi(wireTempC, "0")} °C) vào giống '{organismId}' phát tiếng xèo — làm chết toàn bộ vi khuẩn!",
                    "Sau khi nung đỏ trên đèn cồn, phải đợi 5–10 giây cho dây hợp kim nguội hẳn mới được lấy sinh khối vi khuẩn.");
                return false;
            }
            carriedOrganismId = organismId;
            carriedCfuPerMl = cfuPerMl;
            if (bioTool != null)
            {
                bioTool.reagentOrSampleId = organismId;
                bioTool.reagentDisplayName = organismId;
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction(loopToolId, "lay_mau_vi_khuan",
                $"Vòng que cấy (~10 µL) đã lấy mẫu chủng '{organismId}' (mật độ {cfuPerMl:E2} CFU/mL).");
            return true;
        }

        public void Use(GameObject user)
        {
            FlameSterilize(true);
            if (string.IsNullOrEmpty(carriedOrganismId))
            {
                PickInoculumFromCulture("e_coli_k12", 1.0e8);
            }
        }
    }
}
