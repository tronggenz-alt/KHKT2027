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
    public class PowerSupplyController : MonoBehaviour, IInteractable, IScrollAdjustable, IMachinePanel
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

        public MachineSpec PanelSpec()
        {
            var s = new MachineSpec { code = "MÁY · NGUỒN ĐIỆN DI", title = "Bộ nguồn điện di", subtitle = "Cấp điện một chiều cho bể điện di gel agarose" };
            s.parameters.Add(MachineParam.Slider("Điện áp", "V", 50f, 150f, 5f, () => voltageV, v => { if (!isRunningOutput) voltageV = v; }, "0",
                v => v < 80f ? "Điện áp thấp: băng sắc nét nhưng chạy lâu" : v <= 120f ? "80–120 V: mức thường dùng cho gel 1 %" : "Trên 120 V: gel nóng lên, băng ADN bị nhòe và cong"));
            s.parameters.Add(MachineParam.Slider("Hẹn giờ", "phút", 10f, 60f, 5f, () => timerMinutes, v => timerMinutes = Mathf.RoundToInt(v), "0"));
            s.runLabel = () => isRunningOutput ? "Tắt nguồn" : "Bật nguồn";
            s.status = () => connectedTank == null ? "Chưa nối với bể điện di."
                : isRunningOutput ? $"ĐANG CẤP ĐIỆN {voltageV:0} V\nADN (mang điện âm) đang chạy về cực dương (đỏ)." : $"Sẵn sàng · {voltageV:0} V · hẹn {timerMinutes} phút\nKiểm tra: gel nằm trong bể, giếng ở phía cực âm (đen), đệm TAE ngập gel.";
            s.run = () =>
            {
                bool was = isRunningOutput;
                Use(null);
                if (connectedTank == null) return "Bộ nguồn chưa nối với bể điện di.";
                if (was) return "Đã tắt nguồn. Lấy gel ra, đặt lên buồng chụp gel để xem băng ADN.";
                return isRunningOutput ? $"Đã bật {voltageV:0} V. Theo dõi vạch thuốc nhuộm xanh chạy được khoảng 2/3 gel thì tắt nguồn." : "Chưa bật được nguồn: bể điện di chưa sẵn sàng (thiếu gel hoặc chưa nạp mẫu).";
            };
            return s;
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
