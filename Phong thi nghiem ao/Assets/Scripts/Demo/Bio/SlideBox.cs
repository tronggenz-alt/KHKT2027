// Tách từ SlidePrepTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.1.13. Hộp tiêu bản cố định (10 khe, nắp bản lề) — mã: hop_tieu_ban
    /// </summary>
    [DisallowMultipleComponent]
    public class SlideBox : MonoBehaviour, IInteractable
    {
        public static readonly string[] FixedSlides =
        {
            "co_dinh_phe_nang",
            "co_dinh_nephron",
            "co_dinh_synapse",
            "co_dinh_sarcomere",
            "co_dinh_da_day",
            "co_dinh_gan",
            "co_dinh_mau_nguoi",
            "co_dinh_nguyen_phan_hanh"
        };

        public bool isOpen = false;
        public int selectedIndex = 0;
        public BioHinge lidHinge;

        public string UseHint
        {
            get
            {
                string id = FixedSlides[selectedIndex % FixedSlides.Length];
                var spec = BioCatalogLoader.GetSlide(id);
                string title = spec != null ? spec.ten : id;
                return isOpen
                    ? $"Hộp tiêu bản cố định [Đang chọn khe {selectedIndex + 1}/8: {title}] · Bấm: lấy đặt lên kính hiển vi gần nhất / đổi khe"
                    : "Hộp tiêu bản cố định (8 tiêu bản H&E / nguyên phân) · Bấm để mở nắp hộp";
            }
        }

        public void Use(GameObject user)
        {
            if (!isOpen)
            {
                isOpen = true;
                if (lidHinge != null) lidHinge.SetOpen(true);
                BioFeedback.PlayClick();
                BioFeedback.LogAction("hop_tieu_ban", "mo_hop_tieu_ban", "Đã mở nắp hộp tiêu bản cố định (8 tiêu bản chuẩn).");
                return;
            }

            string slideId = FixedSlides[selectedIndex % FixedSlides.Length];
            var spec = BioCatalogLoader.GetSlide(slideId);
            string slideTitle = spec != null ? spec.ten : slideId;

            var scopes = FindObjectsByType<MicroscopeController>(FindObjectsSortMode.None);
            MicroscopeController nearest = null;
            float bestD = 3.5f;
            for (int i = 0; i < scopes.Length; i++)
            {
                float d = Vector3.Distance(transform.position, scopes[i].transform.position);
                if (d < bestD) { bestD = d; nearest = scopes[i]; }
            }
            if (nearest != null)
            {
                nearest.LoadSlideById(slideId);
            }

            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("hop_tieu_ban", "lay_tieu_ban_co_dinh",
                $"Đã lấy tiêu bản cố định '{slideTitle}' ({slideId}) từ khe {selectedIndex + 1} đặt lên bàn kính hiển vi.");
            selectedIndex = (selectedIndex + 1) % FixedSlides.Length;
        }
    }
}
