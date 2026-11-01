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
    /// 5.2.5. Đĩa Petri Ø 90 mm (nắp nhấc nghiêng 30° kiểu "vỏ sò", ria 4 vùng hoặc cấy trang) — mã: dia_petri
    /// Gọi trực tiếp StreakPlate.Colonies, Dilution.SampleColonies và ContaminationRules.Roll của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class PetriDish : MonoBehaviour, IInteractable
    {
        public string mediaId = "thach_lb"; // thach_dinh_duong | thach_lb | thach_sabouraud
        public string labelText = "E. coli K-12 · 10⁻⁵";
        public bool lidClamshell30Deg = true; // Mở kiểu vỏ sò 30° chuẩn; nếu mở toang 100% lâu ngoài vùng vô trùng -> nhiễm
        public float openExposureSeconds = 2.0f;
        public bool isInverted = true;        // Úp ngược khi đặt tủ ấm
        public bool hasCondensationSpread = false;

        public double platedVolumeMl = 0.1;
        public double dilutionFactor = 1e-5;
        public bool isSpreadPlate = false;
        public int countedColonies = 0;

        public readonly List<StreakStroke> streakStrokes = new List<StreakStroke>();
        public readonly List<ColonySpot> grownColonies = new List<ColonySpot>();
        public BioSample sample;

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
            sample.sterile = true;
        }

        public string UseHint => $"Đĩa Petri Ø 90 mm ({mediaId}) [{labelText} · {(isInverted ? "Úp ngược" : "Đặt ngửa")} · Khuẩn lạc: {(grownColonies.Count > 0 ? grownColonies.Count : countedColonies)}{(sample != null && sample.isContaminated ? " · TẠP NHIỄM!" : "")}] · Bấm: ria 4 vùng / lật úp đĩa";

        /// <summary>
        /// Thực hiện cấy ria 4 vùng (Streak Plate) có kiểm tra đốt que cấy giữa các vùng và tạp nhiễm.
        /// </summary>
        public void PerformFourZoneStreak(InoculatingLoop loop, bool flameBetweenZones = true, float lidOpenSec = 3.0f)
        {
            if (sample == null) return;
            openExposureSeconds = lidOpenSec;
            bool loopSterile = loop == null || loop.isSterile;
            string org = (loop != null && !string.IsNullOrEmpty(loop.carriedOrganismId)) ? loop.carriedOrganismId : "e_coli_k12";
            double inoc = (loop != null) ? loop.carriedCfuPerMl : 1e8;

            sample.organismId = org;
            sample.sampleId = org;
            sample.sampleName = $"Đĩa ria 4 vùng ({org})";
            sample.cfuPerMl = inoc;
            sample.streakZonesCompleted = 4;

            if (!flameBetweenZones)
            {
                BioFeedback.RecordMistake("dia_petri", "ria_4_vung_quen_dot_que_giua_cac_vung",
                    "Ria liên tiếp 4 vùng mà KHÔNG đốt khử trùng que cấy giữa các vùng!",
                    "Mật độ vi khuẩn không được pha loãng bậc thang nên vùng 3 và vùng 4 vẫn mọc dày đặc, không tách được khuẩn lạc đơn.");
            }

            streakStrokes.Clear();
            for (int z = 1; z <= 4; z++)
            {
                bool sterilizedBefore = (z == 1) ? loopSterile : flameBetweenZones;
                for (int s = 0; s < 3; s++)
                {
                    streakStrokes.Add(new StreakStroke
                    {
                        zone = z,
                        x0 = z * 15f + s * 3f,
                        y0 = 10f + s * 8f,
                        x1 = z * 15f + s * 3f + 18f,
                        y1 = 35f + s * 8f,
                        loopSterilizedBefore = sterilizedBefore
                    });
                }
            }

            int seed = labelText.GetHashCode() ^ (int)(lidOpenSec * 100);
            BioTransfer.CheckContamination(loopSterile, lidOpenSec, seed, sample);
            sample.SyncToData();

            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("dia_petri", "cay_ria_4_vung",
                $"Đã cấy ria 4 vùng chủng '{org}' lên đĩa {mediaId} (đốt que giữa các vùng: {(flameBetweenZones ? "CÓ" : "KHÔNG")}).");
        }

        /// <summary>
        /// Nhỏ dịch pha loãng (vd 0,1 mL ở 10^-5) để cấy trang đếm khuẩn lạc.
        /// </summary>
        public void InoculateSpreadPlate(string organismId, double tubeCfuPerMl, double volMl, double dilution, float lidOpenSec = 3.0f)
        {
            if (sample == null) return;
            isSpreadPlate = true;
            platedVolumeMl = volMl;
            dilutionFactor = dilution;
            openExposureSeconds = lidOpenSec;

            sample.organismId = organismId;
            sample.sampleId = organismId;
            sample.sampleName = $"Đĩa cấy trang {organismId} ({dilution:E0})";
            sample.cfuPerMl = tubeCfuPerMl;
            sample.volumeMl = (float)volMl;

            int seed = Math.Abs(organismId.GetHashCode() ^ (int)(dilution * 1e9) ^ (int)(lidOpenSec * 10));
            BioTransfer.CheckContamination(true, lidOpenSec, seed, sample);
            sample.SyncToData();
        }

        public void SpreadSurfaceUniformly()
        {
            isSpreadPlate = true;
        }

        /// <summary>
        /// Ủ đĩa trong tủ ấm và sinh khuẩn lạc bằng StreakPlate.Colonies / Dilution.SampleColonies của Backend.
        /// </summary>
        public void Incubate(float hours, float tempC)
        {
            if (sample == null) return;
            sample.incubationHours += hours;
            int seed = Math.Abs((sample.organismId ?? "ecoli").GetHashCode() ^ 1337);

            if (streakStrokes.Count > 0)
            {
                StreakPlate.Colonies(streakStrokes, sample.cfuPerMl, sample.organismId, sample.incubationHours, tempC, seed, grownColonies);
                countedColonies = grownColonies.Count;
            }
            else if (isSpreadPlate && sample.cfuPerMl > 0.0)
            {
                double expected = sample.cfuPerMl * platedVolumeMl;
                countedColonies = Dilution.SampleColonies(expected, seed);
                grownColonies.Clear();
                double dia = ColonyGrowth.ColonyDiameterMm(sample.incubationHours, 2.0);
                var rng = new System.Random(seed);
                int visualCap = Mathf.Min(countedColonies, 320);
                for (int i = 0; i < visualCap; i++)
                {
                    grownColonies.Add(new ColonySpot
                    {
                        x = (float)(rng.NextDouble() * 70.0 + 10.0),
                        y = (float)(rng.NextDouble() * 70.0 + 10.0),
                        diameterMm = (float)dia,
                        organismId = sample.organismId
                    });
                }
            }
            sample.colonyCount = countedColonies;
        }

        public void Use(GameObject user)
        {
            if (streakStrokes.Count == 0 && !isSpreadPlate)
            {
                PerformFourZoneStreak(null, true, 2.0f);
            }
            else
            {
                isInverted = !isInverted;
                BioFeedback.PlayClick();
                BioFeedback.LogAction("dia_petri", "lat_dia_petri", $"Đã lật đĩa Petri: {(isInverted ? "Úp ngược (đúng kỹ thuật ủ)" : "Đặt ngửa")}.");
            }
        }
    }
}
