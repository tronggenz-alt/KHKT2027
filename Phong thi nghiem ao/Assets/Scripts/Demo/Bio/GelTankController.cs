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
    public class GelTankController : MonoBehaviour, IInteractable, IBioTarget
    {
        public bool gelPlaced;
        public float agarosePercent = 1.0f;
        public readonly List<GelLane> lanes = new List<GelLane>();
        public bool running;
        public bool hasCompletedRun;
        public double runMinutes;
        public double volts = 100;
        public PowerSupplyController psu;
        const float RealSecondsPer30Min = 12f;

        readonly List<Transform> dyeMarks = new List<Transform>();
        readonly List<Vector3> dyeHome = new List<Vector3>();
        Transform gelSlab;

        public string TargetName => "Bể điện di";
        public string UseHint => !gelPlaced ? "Bể điện di đã có đệm TAE 1×. Cầm khay gel đã đông rồi bấm vào bể để đặt gel."
            : running ? $"Đang điện di {volts:0} V: {runMinutes:0}/{psu?.timerMinutes ?? 30} phút"
            : $"Gel {BioFeedback.FormatVi(agarosePercent, "0.0")} % · {lanes.Count}/{GelCastingTray.Wells} giếng đã nạp{(hasCompletedRun ? " · đã chạy xong" : "")}. Cầm micropipet có mẫu rồi bấm vào bể để nạp giếng kế tiếp.";

        void Awake()
        {
            gelSlab = transform.Find("Bản gel");
            foreach (Transform c in transform)
                if (c.name == "Vạch thuốc nhuộm") { dyeMarks.Add(c); dyeHome.Add(c.localPosition); }
            if (psu == null) psu = GetComponentInChildren<PowerSupplyController>();
            ShowGel(false);
        }

        void ShowGel(bool on)
        {
            if (gelSlab != null) gelSlab.gameObject.SetActive(on);
            foreach (var d in dyeMarks) d.gameObject.SetActive(on && lanes.Count > 0);
        }

        public void Use(GameObject user) => MolUi.Say(UseHint);

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            if (tool == null) return false;
            var tray = tool.GetComponent<GelCastingTray>();
            if (tray != null)
            {
                prompt = gelPlaced ? "Bể đã có gel" : !tray.gelReady ? "Khay chưa có gel (đổ agarose ở lò vi sóng trước)" : "Đặt gel vào bể, giếng ở phía cực âm";
                return true;
            }
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            prompt = !gelPlaced ? "Chưa có gel trong bể" : running ? "Đang chạy điện di" : p.load.IsEmpty ? "Đầu côn trống: hút mẫu trước"
                : lanes.Count >= GelCastingTray.Wells ? "Đã nạp đủ 8 giếng" : $"Nạp {BioFeedback.FormatVi(p.load.TotalUl, "0.0")} µL vào giếng {lanes.Count + 1}";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            if (tool == null) return;
            var tray = tool.GetComponent<GelCastingTray>();
            if (tray != null)
            {
                if (gelPlaced || !tray.gelReady) return;
                var hand = HandController.Active;
                if (hand == null || hand.Held == null || hand.Held.gameObject != tray.gameObject) return;
                hand.ReleaseHeldInto(transform, transform.TransformPoint(new Vector3(0, 0.03f, 0.2f)), transform.rotation);
                tray.gameObject.SetActive(false);  // gel đã nằm trong bể (dùng bản gel của mô hình bể)
                gelPlaced = true;
                agarosePercent = tray.agarosePercent;
                lanes.Clear();
                hasCompletedRun = false;
                runMinutes = 0;
                ShowGel(true);
                BioFeedback.PlayClick();
                MolUi.Say("Đã đặt gel vào bể, đệm TAE ngập gel 3 – 5 mm. Nạp thang chuẩn vào giếng 1, mẫu vào các giếng sau.");
                return;
            }
            var p = MolUi.Pipette(tool);
            if (p == null || !gelPlaced || running || p.load.IsEmpty || lanes.Count >= GelCastingTray.Wells) return;
            var m = p.DispenseAll();
            var lane = new GelLane { label = Label(m) };
            if (!m.HasLoadingDye)
            {
                lane.floated = true;
                BioFeedback.RecordMistake("be_dien_di", "nap_mau_khong_co_chat_nap", "Nạp mẫu không trộn đệm nạp mẫu (loading dye)",
                    "Mẫu ADN nhẹ hơn đệm nên nổi lên khỏi giếng và trôi mất. Trộn mẫu với đệm nạp mẫu 6× (có glixerol nặng) trước khi nạp.");
            }
            else lane.bands.AddRange(m.dna);
            lanes.Add(lane);
            hasCompletedRun = false;
            runMinutes = 0;
            ShowGel(true);
            MolUi.Say($"Đã nạp giếng {lanes.Count}: {lane.label}. Nhúng đầu côn vừa chạm miệng giếng, không chọc thủng đáy giếng.");
        }

        static string Label(MolMix m)
        {
            if (m.Get(MolCodes.Ladder) > 0.05f) return "Thang 100 bp";
            if (m.dna.Count == 0) return $"{BioFeedback.FormatVi(m.TotalUl, "0")} µL mẫu (không có ADN thấy được)";
            return $"{BioFeedback.FormatVi(m.TotalUl, "0")} µL mẫu ADN";
        }

        public bool StartRun(double v)
        {
            if (!gelPlaced) { MolUi.Say("Chưa có gel trong bể", true); return false; }
            if (lanes.Count == 0) { MolUi.Say("Chưa nạp giếng nào", true); return false; }
            volts = v;
            running = true;
            if (hasCompletedRun) { runMinutes = 0; hasCompletedRun = false; }
            MolUi.MachineStatus("be_dien_di", "dang_chay", $"Điện di {v:0} V", 0f);
            return true;
        }

        public void StopRun()
        {
            if (!running) return;
            running = false;
            MolUi.Say($"Đã tắt nguồn sau {runMinutes:0} phút điện di.");
        }

        void Update()
        {
            if (!running) return;
            double timer = psu != null ? psu.timerMinutes : 30;
            runMinutes += Time.deltaTime * 30.0 / RealSecondsPer30Min;
            float f = (float)Math.Min(1.0, Electrophoresis.TrackingDyeDistanceCm(volts, runMinutes) / 6.0);
            for (int i = 0; i < dyeMarks.Count; i++)
                dyeMarks[i].localPosition = dyeHome[i] + new Vector3(0.09f * f, 0, 0);  // thuốc nhuộm chạy về cực dương (+x)
            if (runMinutes >= timer)
            {
                runMinutes = timer;
                running = false;
                hasCompletedRun = true;
                if (psu != null) psu.isRunningOutput = false;
                BioFeedback.PlaySuccess();
                MolUi.Say($"Điện di xong ({volts:0} V, {timer:0} phút). Tắt nguồn, chuyển gel sang buồng chụp gel.");
                MolUi.MachineStatus("be_dien_di", "san_sang", $"Điện di xong {volts:0} V {timer:0} phút", 1f);
            }
        }

        /// <summary>Quãng đường (cm) của một đoạn ADN trong lần chạy này (âm nghĩa là đã chạy ra khỏi gel).</summary>
        public double DistanceCm(int bp) => Electrophoresis.MigrationDistanceCm(bp, volts, runMinutes, agarosePercent);
    }
}
