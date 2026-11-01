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
    /// 5.1.8. Đèn bàn LED (tối đa 2 đèn bật cùng lúc trong phòng để tiết kiệm điện & giữ FPS) — mã: den_ban_led
    /// </summary>
    [DisallowMultipleComponent]
    public class DeskLamp : MonoBehaviour, IInteractable
    {
        private static readonly List<DeskLamp> ActiveLamps = new List<DeskLamp>();
        public const int MaxSimultaneousLamps = 2;

        public bool isOn = false;
        public float armAngleDeg = 35f;
        public Light pointLight;

        public string UseHint => $"Đèn bàn LED [{(isOn ? "ĐANG BẬT" : "TẮT")} · góc cần {BioFeedback.FormatVi(armAngleDeg, "0")}°] · Bấm: bật/tắt (tối đa 2 đèn cùng lúc)";

        public void Use(GameObject user)
        {
            SetLamp(!isOn);
        }

        public void SetLamp(bool on)
        {
            if (on && !isOn)
            {
                ActiveLamps.RemoveAll(l => l == null || !l.isOn);
                if (ActiveLamps.Count >= MaxSimultaneousLamps)
                {
                    var oldest = ActiveLamps[0];
                    ActiveLamps.RemoveAt(0);
                    if (oldest != null)
                    {
                        oldest.isOn = false;
                        if (oldest.pointLight != null) oldest.pointLight.enabled = false;
                        BioFeedback.ShowToast("Tắt mờ đèn bàn xa nhất để tiết kiệm điện (tối đa 2 đèn bàn bật cùng lúc).");
                    }
                }
                ActiveLamps.Add(this);
            }
            else if (!on)
            {
                ActiveLamps.Remove(this);
            }

            isOn = on;
            if (pointLight != null) pointLight.enabled = isOn;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("den_ban_led", isOn ? "bat_den_ban" : "tat_den_ban",
                $"Đèn bàn LED: {(isOn ? "BẬT" : "TẮT")}.");
        }
    }
}
