// Tách từ GeneralBioTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
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
    /// 5.1.15 & 5.2.1. Tủ an toàn sinh học cấp II loại A2 — mã: tu_an_toan_sinh_hoc
    /// </summary>
    [DisallowMultipleComponent]
    public class BiosafetyCabinetController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public bool fanOn = false;
        public bool lightOn = true;
        public bool uvOn = false;
        public float sashHeightCm = 20.0f; // Chuẩn làm việc: 20 cm
        public float purgeMinutes = 0f;    // Cần chạy quạt >= 5 phút trước khi thao tác
        public bool grilleBlocked = false;
        public bool surfaceCleanedWithEthanol = false;
        public bool showAirflowParticles = false;

        public float downflowVelocityMs => fanOn ? 0.35f : 0f;
        public float inflowVelocityMs => fanOn ? 0.52f : 0f;
        public BscProgram program = new BscProgram();

        void Awake()
        {
            if (program == null) program = new BscProgram();
            program.cabinet = this;
        }

        public string UseHint
        {
            get
            {
                string sashState = sashHeightCm > 20.5f ? "CẢNH BÁO KÍNH CAO > 20 cm!" : (sashHeightCm <= 0.5f ? "Kính đóng kín" : "Đúng vạch 20 cm");
                return $"Tủ an toàn sinh học cấp II A2 [Quạt: {(fanOn ? $"BẬT ({BioFeedback.FormatVi(purgeMinutes, "0.0")} phút)" : "TẮT")} · Kính: {BioFeedback.FormatVi(sashHeightCm, "0")} cm ({sashState}) · UV: {(uvOn ? "BẬT" : "TẮT")}] · Bấm: bật quạt & chuẩn bị vô trùng · Con lăn: kéo kính";
            }
        }

        public void SetSashHeight(float cm)
        {
            sashHeightCm = Mathf.Clamp(cm, 0f, 45f);
            if (sashHeightCm > 0.5f && uvOn)
            {
                uvOn = false;
                BioFeedback.ShowToast("Khóa liên động: Mở kính cửa trước -> Đèn UV tự động tắt để bảo vệ mắt và da!");
                BioFeedback.LogAction("tu_an_toan_sinh_hoc", "khoa_lien_dong_uv", "Mở kính trước khi UV đang bật -> Hệ thống khóa liên động tự tắt đèn UV.");
            }
            if (sashHeightCm > 20.5f && fanOn)
            {
                BioFeedback.RecordMistake("tu_an_toan_sinh_hoc", "mo_kinh_bsc_qua_vach_20cm",
                    $"Kéo kính tủ BSC lên {BioFeedback.FormatVi(sashHeightCm, "0")} cm vượt quá vạch an toàn 20 cm!",
                    "Khi mở kính cao quá 20 cm, vận tốc màn khí hút vào lưới trước giảm dưới 0,5 m/s làm rò rỉ khí và tăng mạnh nguy cơ tạp nhiễm.");
            }
            SyncGlobalAsepticFlags();
        }

        public void ToggleUv(bool turnOn)
        {
            if (turnOn && sashHeightCm > 0.5f)
            {
                BioFeedback.RecordMistake("tu_an_toan_sinh_hoc", "bat_uv_khi_kinh_mo",
                    "Cố bật đèn UV diệt khuẩn khi kính trước chưa đóng kín!",
                    "Tia UV-C (254 nm) gây bỏng giác mạc và tổn thương da. Chỉ bật UV khi đã đóng kín kính chắn.");
                return;
            }
            uvOn = turnOn;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", uvOn ? "bat_uv_bsc" : "tat_uv_bsc", $"Đèn UV tủ BSC: {(uvOn ? "BẬT khử trùng" : "TẮT")}.");
        }

        public void WipeSurfaceWithEthanol()
        {
            surfaceCleanedWithEthanol = true;
            BioTransfer.SurfaceClean = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", "lau_con_70_mat_ban", "Đã xịt cồn 70 % và lau sạch mặt bàn inox trong tủ an toàn sinh học.");
            SyncGlobalAsepticFlags();
        }

        public void CleanSurfaceWithEthanol() => WipeSurfaceWithEthanol();

        public void FastForwardPurge(float minutes)
        {
            if (!fanOn) fanOn = true;
            purgeMinutes += minutes;
            SyncGlobalAsepticFlags();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", "tua_thoi_gian_quat_bsc",
                $"Đã chạy quạt HEPA tủ BSC được {BioFeedback.FormatVi(purgeMinutes, "0.0")} phút (Dòng xuống {BioFeedback.FormatVi(downflowVelocityMs, "0.00")} m/s, Dòng vào {BioFeedback.FormatVi(inflowVelocityMs, "0.00")} m/s).");
        }

        public void Use(GameObject user)
        {
            fanOn = true;
            uvOn = false;
            sashHeightCm = 20.0f;
            if (purgeMinutes < 5.0f) purgeMinutes = 5.0f;
            surfaceCleanedWithEthanol = true;
            grilleBlocked = false;
            SyncGlobalAsepticFlags();
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", "chuan_bi_bsc_chuan",
                "Tủ BSC cấp II A2 sẵn sàng: Quạt HEPA chạy đủ 5 phút (dòng xuống 0,35 m/s, dòng vào 0,52 m/s), kính ở vạch 20 cm, mặt bàn đã lau cồn 70 %.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            SetSashHeight(sashHeightCm + Mathf.Sign(delta) * 2.5f);
        }

        public void SyncGlobalAsepticFlags()
        {
            BioTransfer.InsideBsc = fanOn;
            BioTransfer.BscRunning = fanOn;
            BioTransfer.BscPurgeMinutes = purgeMinutes;
            BioTransfer.BscSashHeightCm = sashHeightCm;
            BioTransfer.BscGrilleBlocked = grilleBlocked;
            BioTransfer.SurfaceClean = surfaceCleanedWithEthanol;
        }
    }
}
