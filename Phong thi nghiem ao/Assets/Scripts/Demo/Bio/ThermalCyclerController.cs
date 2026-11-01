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
    public class ThermalCyclerController : MonoBehaviour, IInteractable, IBioTarget
    {
        public const int MaxTubes = 8;
        public readonly List<MicroTube> loaded = new List<MicroTube>();
        public double annealC = 55.0;
        public int cycles = 30;
        public bool lidHeated = true;
        public bool running;
        public bool finished;
        public double simSeconds;
        public float speed = 120f;          // 1 giờ chạy máy ≈ 30 giây thật
        public string machineName = "Máy PCR";
        PcrProgram program;

        public string TargetName => machineName;
        public string UseHint => running ? $"{machineName}: đang chạy ({Phase})" : $"{machineName} ({loaded.Count} ống) · Bấm: mở màn hình điều khiển";

        public string Phase
        {
            get
            {
                var (phase, cyc, _) = PcrProfile.At(simSeconds, annealC, cycles);
                return cyc > 0 ? $"chu kỳ {cyc}/{cycles}, {phase}" : phase;
            }
        }

        public void Use(GameObject user)
        {
            program ??= new PcrProgram { cycler = this };
            InstrumentScreen.Open(null, program, null);
        }

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var tube = tool != null ? tool.GetComponent<MicroTube>() : null;
            if (tube == null) return false;
            if (running) prompt = "Máy đang chạy, không mở nắp";
            else if (tube.tubeTypeId != "ong_pcr") prompt = "Ống 1,5 mL không vừa giếng 0,2 mL của khối nhiệt";
            else if (loaded.Count >= MaxTubes) prompt = "Khối nhiệt đã đầy";
            else prompt = $"Đặt ống {tube.labelText} vào khối nhiệt";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var tube = tool != null ? tool.GetComponent<MicroTube>() : null;
            if (tube == null || running || tube.tubeTypeId != "ong_pcr" || loaded.Count >= MaxTubes) return;
            if (tube.mix.IsEmpty) MolUi.Say($"Ống {tube.labelText} còn trống: vẫn đặt vào nhưng sẽ không có sản phẩm", true);
            var hand = HandController.Active;
            if (hand == null || hand.Held == null || hand.Held.gameObject != tube.gameObject) return;
            hand.ReleaseHeldInto(transform, SlotPosition(loaded.Count), transform.rotation);
            AddTube(tube);
        }

        void AddTube(MicroTube tube)
        {
            loaded.Add(tube);
            finished = false;
            var g = tube.GetComponent<Grabbable>();
            if (g != null) g.HeldChanged += held => { if (held) RemoveTube(tube); };
            BioFeedback.PlayClick();
            MolUi.Say($"Đã đặt ống {tube.labelText} vào {machineName} ({loaded.Count} ống). Bấm vào máy để cài chu trình và chạy.");
            program?.S?.Done("nap_ong");
        }

        void RemoveTube(MicroTube tube)
        {
            if (!loaded.Remove(tube)) return;
            for (int i = 0; i < loaded.Count; i++) loaded[i].transform.position = SlotPosition(i);
        }

        /// <summary>Giếng trên khối nhiệt (nhìn thấy qua nắp mở): hàng ngang ở mặt trên máy.</summary>
        Vector3 SlotPosition(int i) => transform.TransformPoint(new Vector3(-0.09f + i * 0.026f, 0.233f, -0.04f));

        public void StartRun()
        {
            if (running) return;
            if (loaded.Count == 0) { program?.S?.Say("Chưa có ống nào trong khối nhiệt", 2); return; }
            running = true;
            finished = false;
            simSeconds = 0;
            BioFeedback.PlayButton();
            MolUi.MachineStatus("may_pcr", "dang_chay", $"{machineName}: bắt đầu {cycles} chu kỳ", 0f);
        }

        void Update()
        {
            if (!running) return;
            double total = PcrProfile.TotalSeconds(cycles);
            int before = PcrProfile.At(simSeconds, annealC, cycles).cycle;
            simSeconds += Time.deltaTime * speed;
            int after = PcrProfile.At(simSeconds, annealC, cycles).cycle;
            if (after != before && after % 5 == 0)
                MolUi.MachineStatus("may_pcr", "dang_chay", $"{machineName}: chu kỳ {after}/{cycles}", (float)(simSeconds / total));
            if (simSeconds >= total) Finish();
        }

        public void SkipToEnd() { if (running) simSeconds = PcrProfile.TotalSeconds(cycles); }

        void Finish()
        {
            running = false;
            finished = true;
            var sb = new StringBuilder();
            var bands = new List<DnaBand>();
            foreach (var tube in loaded)
            {
                // vết khuôn lọt vào do dùng lại đầu côn (vd ống đối chứng âm) cũng có mặt trong mix và được nhân lên
                var setup = tube.BuildPcrSetup(annealC, cycles, lidHeated);
                Pcr.Run(setup, tube.labelText.GetHashCode(), bands);
                tube.amplified = true;
                tube.pcrCycles = cycles;
                foreach (var b in bands) tube.mix.AddBand(new BioDnaBandInfo { bp = b.bp, ng = b.ng, smear = b.smear });
                tube.Changed();
                sb.Append($"Ống {tube.labelText}: ").Append(bands.Count == 0 ? "không có sản phẩm" : string.Join(", ", bands.ConvertAll(b => $"{b.bp} bp ({b.ng:0} ng)"))).Append(". ");
            }
            BioFeedback.PlaySuccess();
            MolUi.Say($"{machineName} chạy xong, giữ 4 °C. Lấy ống ra để điện di kiểm tra.");
            MolUi.MachineStatus("may_pcr", "san_sang", $"{machineName}: xong {cycles} chu kỳ. {sb}", 1f);
            BioFeedback.LogAction("may_pcr", "hoan_tat_pcr", sb.ToString());
            program?.OnFinished(sb.ToString());
        }
    }
}
