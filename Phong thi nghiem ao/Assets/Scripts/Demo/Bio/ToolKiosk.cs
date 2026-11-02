// Tách từ GreenhouseAndCommonTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.6. Ki-ốt tra cứu dụng cụ sinh học & dẫn đường bằng vạch sáng trên sàn hành lang 3,6 m — mã: kiosk_tra_cuu
    /// </summary>
    [DisallowMultipleComponent]
    public class ToolKiosk : MonoBehaviour, IInteractable
    {
        public int currentToolIdx = 0;
        public bool floorGuideActive = false;
        public float guideTimer = 0f;
        public GameObject floorGuideLineVisual;

        public BioToolCatalogEntry SelectedTool
        {
            get
            {
                BioCatalogLoader.EnsureLoaded();
                if (BioCatalogLoader.ToolsList.Count == 0) return null;
                return BioCatalogLoader.ToolsList[currentToolIdx % BioCatalogLoader.ToolsList.Count];
            }
        }

        public string UseHint
        {
            get
            {
                var t = SelectedTool;
                string info = t != null ? $"{t.ten} ({t.ma} · Phòng: {t.phong})" : "108 dụng cụ sinh học";
                return $"Ki-ốt tra cứu dụng cụ [{info}{(floorGuideActive ? " · ĐANG BẬT VẠCH SÁNG DẪN ĐƯỜNG" : "")}] · Bấm: tra cứu & bật mũi tên dẫn đường trên sàn";
            }
        }

        public void ActivateFloorWayfinding(string toolId)
        {
            floorGuideActive = true;
            guideTimer = 0f;
            if (floorGuideLineVisual != null) floorGuideLineVisual.SetActive(true);
            enabled = true;
            var t = BioCatalogLoader.GetTool(toolId) ?? SelectedTool;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("kiosk_tra_cuu", "dan_duong_dung_cu",
                $"Ki-ốt tra cứu: Hiển thị thông số '{t?.ten}' ({t?.ma}) và bật vạch sáng dẫn đường trên sàn hành lang 3,6 m tới khu '{t?.phong}' (tự tắt sau 20 s).");
        }

        public void Use(GameObject user)
        {
            BioCatalogLoader.EnsureLoaded();
            if (BioCatalogLoader.ToolsList.Count > 0)
            {
                var t = SelectedTool;
                ActivateFloorWayfinding(t.ma);
                currentToolIdx = (currentToolIdx + 1) % BioCatalogLoader.ToolsList.Count;
            }
        }

        void Update()
        {
            if (!floorGuideActive) { enabled = false; return; }
            guideTimer += Time.deltaTime;
            if (guideTimer >= 20f)
            {
                floorGuideActive = false;
                if (floorGuideLineVisual != null) floorGuideLineVisual.SetActive(false);
                enabled = false;
            }
        }
    }
}
