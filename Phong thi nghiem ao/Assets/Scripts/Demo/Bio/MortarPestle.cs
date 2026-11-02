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
    /// 5.1.11. Chày cối sứ Ø 9 cm — mã: coi_chay_su
    /// </summary>
    [DisallowMultipleComponent]
    public class MortarPestle : MonoBehaviour, IInteractable
    {
        public string rawSampleName = "Chuối chín / Gan gà";
        public int grindStrokes = 0;
        public const int RequiredStrokes = 5;
        public bool hasExtractionBuffer = false;
        public bool isHomogenized => grindStrokes >= RequiredStrokes;
        public BioSample homogenateSample;

        void Awake()
        {
            homogenateSample = GetComponent<BioSample>();
            if (homogenateSample == null) homogenateSample = gameObject.AddComponent<BioSample>();
        }

        public string UseHint => isHomogenized
            ? $"Cối chày sứ [Đã nghiền nhuyễn '{rawSampleName}' ({grindStrokes}/{RequiredStrokes} vòng){(hasExtractionBuffer ? " + đệm tách chiết" : "")}] · Rót sang phễu lọc"
            : $"Cối chày sứ Ø 9 cm [Đang nghiền '{rawSampleName}': {grindStrokes}/{RequiredStrokes} vòng] · Bấm/giữ để nghiền mịn phá vỡ thành tế bào";

        public void AddExtractionBuffer()
        {
            hasExtractionBuffer = true;
            if (homogenateSample != null)
            {
                homogenateSample.sampleId = "dich_nghien_adn";
                homogenateSample.sampleName = $"Dịch nghiền {rawSampleName} + đệm ly giải";
                homogenateSample.volumeMl = 15f;
                homogenateSample.dna.Clear();
                homogenateSample.dna.Add(new BioDnaBandInfo { bp = 10000, ng = 120.0, smear = false });
                homogenateSample.SyncToData();
            }
            BioFeedback.LogAction("coi_chay_su", "them_dem_tach_chiet", "Đã thêm dung dịch tách chiết (nước rửa chén + NaCl) vào cối nghiền.");
        }

        public void Use(GameObject user)
        {
            grindStrokes = Mathf.Min(RequiredStrokes, grindStrokes + 2);
            if (!hasExtractionBuffer && grindStrokes >= RequiredStrokes)
            {
                AddExtractionBuffer();
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("coi_chay_su", "nghien_mau_coi_su",
                $"Nghiền mẫu '{rawSampleName}' trong cối sứ: {grindStrokes}/{RequiredStrokes} vòng ({(isHomogenized ? "Đã thành dịch nghiền mịn" : "Đang nghiền")}).");
        }
    }
}
