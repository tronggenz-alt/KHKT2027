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
    /// 5.2.3. Que trang thủy tinh hình chữ L (Cell Spreader) — mã: que_trang
    /// </summary>
    [DisallowMultipleComponent]
    public class CellSpreader : MonoBehaviour, IInteractable
    {
        public bool sterilizedWithEthanolFlame = false;
        public bool cooledDown = true;

        public string UseHint => sterilizedWithEthanolFlame
            ? "Que trang thủy tinh chữ L [Đã nhúng cồn 70 %, hơ lửa xanh 1 s và để nguội] · Bấm đĩa Petri để dàn đều 0,1 mL dịch khuẩn"
            : "Que trang thủy tinh chữ L · Bấm để nhúng cồn, hơ lửa khử trùng và để nguội trước khi trang đĩa";

        public void SterilizeAndSpread(PetriDish targetDish)
        {
            sterilizedWithEthanolFlame = true;
            cooledDown = true;
            if (targetDish != null)
            {
                targetDish.SpreadSurfaceUniformly();
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("que_trang", "trang_deu_dia_thach",
                "Đã khử trùng que trang chữ L bằng cồn + lửa và xoay đĩa dàn đều 0,1 mL dịch pha loãng khắp mặt thạch.");
        }

        /// <summary>Bấm que trang: chỉ khử trùng khi có đèn cồn đang cháy gần (60 cm); đã khử trùng thì trang lên đĩa Petri gần nhất.</summary>
        public void Use(GameObject user)
        {
            if (!sterilizedWithEthanolFlame)
            {
                var den = BioGanNhat.Tim<SpiritLamp>(transform.position, 0.6f, d => d.isLit);
                if (den == null) { BioFeedback.ShowToast("Que trang chưa khử trùng: cần nhúng cồn và hơ qua đèn cồn đang cháy ở gần.", true); return; }
                sterilizedWithEthanolFlame = true; cooledDown = true;
                BioFeedback.PlayClick();
                BioFeedback.ShowToast("Đã nhúng cồn 70 %, hơ qua lửa đèn cồn và để nguội.");
                return;
            }
            var dia = BioGanNhat.Tim<PetriDish>(transform.position, 0.4f);
            if (dia == null) { BioFeedback.ShowToast("Que trang đã khử trùng. Đưa tới đĩa Petri đã nhỏ dịch khuẩn để trang."); return; }
            SterilizeAndSpread(dia);
            sterilizedWithEthanolFlame = false; // đã chạm mẫu: lần sau phải khử trùng lại
        }
    }
}
