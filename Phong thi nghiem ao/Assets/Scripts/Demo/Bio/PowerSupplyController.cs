// Tách từ MolecularTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    [DisallowMultipleComponent]
    public class PowerSupplyController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float voltageV = 100f;
        public int timerMinutes = 30;
        public bool isRunningOutput;
        public GelTankController connectedTank;

        public string UseHint => $"Bộ nguồn điện di {voltageV:0} V · hẹn {timerMinutes} phút · {(isRunningOutput ? "ĐANG CẤP ĐIỆN (bấm để tắt)" : "Bấm: bật nguồn")} · Lăn chuột: chỉnh điện áp";

        void Start()
        {
            if (connectedTank == null) connectedTank = GetComponentInParent<GelTankController>();
        }

        public void Use(GameObject user)
        {
            if (connectedTank == null) { MolUi.Say("Bộ nguồn chưa nối với bể điện di", true); return; }
            if (isRunningOutput) { isRunningOutput = false; connectedTank.StopRun(); BioFeedback.PlayButton(); return; }
            if (!connectedTank.StartRun(voltageV)) return;
            isRunningOutput = true;
            BioFeedback.PlayButton();
            MolUi.Say($"Đậy nắp, nối dây đỏ (+) đỏ, đen (−) đen, bật {voltageV:0} V: bọt khí nhỏ nổi ở điện cực là đúng. ADN chạy từ cực âm về cực dương.");
        }

        public void OnScroll(float delta)
        {
            if (isRunningOutput || Mathf.Abs(delta) < 0.001f) return;
            voltageV = Mathf.Clamp(voltageV + Mathf.Sign(delta) * 10f, 50f, 150f);
            BioFeedback.PlayClick();
            MolUi.Say($"Điện áp đặt: {voltageV:0} V" + (voltageV > 120 ? " (cao: gel nóng, băng nhòe)" : ""));
        }
    }
}
