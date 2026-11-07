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
    public class Micropipette : MonoBehaviour, IScrollAdjustable
    {
        public string pipetteToolId = "micropipet_p10"; // micropipet_p10 | micropipet_p200 | micropipet_p1000
        public float minUl = 0.5f, maxUl = 10f, stepUl = 0.1f;
        public float volumeSetUl = 10f;
        public bool tipAttached;
        public MolMix load = new MolMix();
        /// <summary>Dịch hút từ bình thường (BioVessel), tách khỏi hỗn hợp phản ứng PCR.</summary>
        public readonly Khkt.Simulation.Bio.BioMix genericLoad = new Khkt.Simulation.Bio.BioMix();
        /// <summary>Mọi chất đầu côn hiện tại đã chạm vào (để phát hiện dùng lại đầu côn gây nhiễm chéo).</summary>
        public readonly HashSet<string> tipHistory = new HashSet<string>();

        public string Model => pipetteToolId == "micropipet_p1000" ? "P1000" : pipetteToolId == "micropipet_p200" ? "P200" : "P10";
        public string TipKind => pipetteToolId == "micropipet_p1000" ? "xanh" : "vang";
        public string VolumeText => BioFeedback.FormatVi(volumeSetUl, maxUl <= 10f ? "0.0" : "0") + " µL";

        void Awake() => ConfigureRangeById(pipetteToolId);

        public void ConfigureRangeById(string id)
        {
            pipetteToolId = id;
            if (id == "micropipet_p1000") { minUl = 100f; maxUl = 1000f; stepUl = 10f; }
            else if (id == "micropipet_p200") { minUl = 20f; maxUl = 200f; stepUl = 1f; }
            else { pipetteToolId = "micropipet_p10"; minUl = 0.5f; maxUl = 10f; stepUl = 0.5f; }
            volumeSetUl = Mathf.Clamp(volumeSetUl, minUl, maxUl);
            Refresh();
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            if (!load.IsEmpty) { MolUi.Say("Không vặn núm thể tích khi đầu côn đang chứa dịch", true); return; }
            volumeSetUl = Mathf.Clamp(Mathf.Round((volumeSetUl + Mathf.Sign(delta) * stepUl) / stepUl) * stepUl, minUl, maxUl);
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"{Model}: đặt {VolumeText} (dải {BioFeedback.FormatVi(minUl, "0.#")} – {BioFeedback.FormatVi(maxUl, "0")} µL)");
        }

        public bool AttachTip(TipBox box)
        {
            if (tipAttached) { MolUi.Say("Đã có đầu côn. Thải đầu côn cũ vào thùng rác đầu côn trước khi lấy cái mới.", true); return false; }
            if (box.tipKind != TipKind)
            {
                BioFeedback.RecordMistake(pipetteToolId, "gan_dau_con_sai_co", $"Gắn đầu côn {box.tipKind} vào {Model}",
                    "Đầu côn sai cỡ không kín khí: hút thiếu hoặc rơi đầu côn. P10 / P200 dùng côn vàng, P1000 dùng côn xanh.");
                return false;
            }
            if (!box.TakeOne()) return false;
            tipAttached = true;
            load.Clear();
            tipHistory.Clear();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã gắn đầu côn vô trùng vào {Model}");
            return true;
        }

        public void EjectTip(TipWaste waste)
        {
            if (!tipAttached) return;
            tipAttached = false;
            load.Clear();
            tipHistory.Clear();
            waste.Receive();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã đẩy đầu côn của {Model} vào thùng rác đầu côn");
        }

        public void AspirateStock(ReagentTube stock)
        {
            if (!tipAttached)
            {
                BioFeedback.RecordMistake(pipetteToolId, "hut_khong_gan_dau_con", "Hút dịch khi chưa gắn đầu côn",
                    "Dịch tràn vào thân piston: nhiễm bẩn và hỏng micropipet. Luôn gắn đầu côn mới trước khi hút.");
                return;
            }
            if (!load.IsEmpty) { MolUi.Say("Đầu côn đang có dịch: nhả vào ống phản ứng trước", true); return; }
            // đầu côn đã chạm chất khác mà lại nhúng vào ống gốc: ống gốc bị nhiễm (lỗi kinh điển làm đối chứng âm có băng)
            bool dirty = false;
            foreach (var c in tipHistory)
                if (c != stock.reagentCode && !stock.contaminants.Contains(c)) { stock.contaminants.Add(c); dirty = true; }
            if (dirty)
                BioFeedback.RecordMistake(pipetteToolId, "dung_lai_dau_con", $"Dùng lại đầu côn đã chạm chất khác để hút {stock.TargetName}",
                    "Ống thuốc thử gốc đã bị nhiễm vết ADN / thuốc thử lạ: mọi ống pha sau đó đều nhiễm, đối chứng âm sẽ có băng. Mỗi thuốc thử một đầu côn mới.");
            if (stock.stockUl < volumeSetUl) { MolUi.Say($"{stock.TargetName} sắp hết", true); }
            stock.Draw(load, volumeSetUl);
            tipHistory.Add(stock.reagentCode);
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã hút {VolumeText} {stock.TargetName}");
        }

        public void AspirateTube(MicroTube tube)
        {
            if (!tipAttached) { MolUi.Say("Gắn đầu côn trước khi hút", true); return; }
            if (!load.IsEmpty) { DispenseTube(tube); return; }
            float avail = tube.mix.TotalUl;
            if (avail < 0.2f) { MolUi.Say($"Ống {tube.labelText} trống", true); return; }
            float take = Mathf.Min(volumeSetUl, avail);
            load.TakeFrom(tube.mix, take, true);
            foreach (var c in tube.mix.codes) tipHistory.Add(c);
            tipHistory.Add("ong_" + tube.labelText);
            tube.Changed();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã hút {BioFeedback.FormatVi(take, "0.0")} µL từ ống {tube.labelText}{(take < volumeSetUl - 0.05f ? " (ống không đủ thể tích đã đặt)" : "")}");
        }

        public void DispenseTube(MicroTube tube)
        {
            if (load.IsEmpty) return;
            float v = load.TotalUl;
            tube.mix.TakeFrom(load, v, true);
            load.Clear();
            foreach (var c in tube.mix.codes) tipHistory.Add(c);
            tipHistory.Add("ong_" + tube.labelText);
            tube.Changed();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã nhả {BioFeedback.FormatVi(v, "0.0")} µL vào ống {tube.labelText} (ấn tới nấc 2 để đẩy hết giọt cuối)");
        }

        /// <summary>Nhả toàn bộ đầu côn ra (vào giếng gel...); trả về hỗn hợp vừa nhả.</summary>
        public MolMix DispenseAll()
        {
            var m = new MolMix();
            m.TakeFrom(load, load.TotalUl, true);
            load.Clear();
            tipHistory.Add("gel");
            BioFeedback.PlayClick();
            Refresh();
            return m;
        }

        public void Refresh()
        {
            string tip = tipAttached ? $"đầu côn {TipKind}" : "chưa gắn đầu côn";
            string content = load.IsEmpty ? "" : " · đang chứa " + load.Describe();
            MolUi.Info(this, $"{Model} đặt {VolumeText} · {tip}{content}. Cầm lên rồi lăn chuột để chỉnh thể tích; trỏ vào hộp đầu côn để gắn, vào ống để hút / nhả, vào thùng rác đầu côn để thải.");
        }
    }
}
