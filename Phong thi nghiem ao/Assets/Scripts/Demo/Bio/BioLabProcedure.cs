using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// Hệ thống Bài Thực Hành Cụ Thể 7 Bước có Theo dõi Trạng thái & Chấm điểm cho Khu Sinh học (Tầng 2).
    /// Đồng bộ trực tiếp với thanh tiến trình 7 bước (stepBar) và bảng kết quả (resultPanel) của ModernUI.cs.
    /// Hỗ trợ 2 kịch bản thực hành chuẩn THPT:
    /// - Kịch bản 1 (Bàn 1 – Trạm 21): Làm tiêu bản tươi biểu bì vảy hành tím, quan sát Co và Phản co nguyên sinh (Sinh học 10).
    /// - Kịch bản 2 (Phòng Sinh học Phân tử – Trạm 28/29): Tạo ADN tái tổ hợp pUC19-GFP, kiểm tra PCR – Điện di & Biến nạp E. coli K-12 (Sinh học 12).
    /// </summary>
    [DisallowMultipleComponent]
    public class BioLabProcedure : MonoBehaviour
    {
        public static BioLabProcedure Instance { get; private set; }

        public enum Scenario
        {
            OnionPlasmolysis = 0,
            DnaSplicingGfp = 1
        }

        public enum Step
        {
            Buoc1 = 0,
            Buoc2 = 1,
            Buoc3 = 2,
            Buoc4 = 3,
            Buoc5 = 4,
            Buoc6 = 5,
            Buoc7 = 6,
            Xong = 7
        }

        public struct BioResult
        {
            public Scenario scenario;
            public int score;
            public int completedSteps;
            public int mistakeCount;
            public float maxMagnification;
            public float maxPlasmolysisPercent;
            public float finalPlasmolysisPercent;
            public int recombinantBp;
            public int whiteColonies;
            public string headline;
            public string subline;
            public string badgeText;
            public List<string> notes;
        }

        public static readonly string[] OnionSteps =
        {
            "1. Chuẩn bị lam kính",
            "2. Nhỏ nước & Tách mẫu",
            "3. Đặt lên bàn kính",
            "4. Lấy nét 4× → 10×",
            "5. Co nguyên sinh (NaCl)",
            "6. Phản co nguyên sinh",
            "7. Ghi kết quả (Enter)"
        };

        public static readonly string[] DnaSteps =
        {
            "1. Chọn Plasmid & Gen",
            "2. Cắt giới hạn (EcoRI)",
            "3. Nối ADN (T4 Ligase)",
            "4. Chạy máy PCR",
            "5. Điện di & Soi Gel",
            "6. Biến nạp & Ủ 37 °C",
            "7. Soi GFP & Chấm điểm"
        };

        public bool Active = true;
        public Scenario CurrentScenario = Scenario.OnionPlasmolysis;
        public bool ScenarioLocked = false;
        public string[] StepNames = OnionSteps;
        public readonly bool[] Done = new bool[7];
        public Step Current = Step.Buoc1;
        public BioResult? LastResult { get; private set; }

        // Số liệu theo dõi Kịch bản 1 (Tế bào hành & Co nguyên sinh)
        public float maxMagnificationUsed = 40f;
        public float maxPlasmolysisPercent = 0f;
        public float currentPlasmolysisPercent = 0f;
        bool hadFocused4x;

        // Bộ nhớ đệm thiết bị Tầng 2 (tránh cấp phát bộ nhớ mỗi khung hình)
        MicroscopeController[] microscopes;
        GlassSlide[] slides;
        GeneCloningStation[] cloningStations;
        ThermalCyclerController[] cyclers;
        GelTankController[] gelTanks;
        GelDocController[] gelDocs;
        IncubatorController[] incubators;
        WaterBathController[] waterBaths;
        float pollTimer;
        bool cachesInitialized;

        public static BioLabProcedure Ensure(Transform parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<BioLabProcedure>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            GameObject host = parent != null ? parent.gameObject : new GameObject("BioLabProcedure");
            Instance = host.GetComponent<BioLabProcedure>();
            if (Instance == null) Instance = host.AddComponent<BioLabProcedure>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            StepNames = CurrentScenario == Scenario.OnionPlasmolysis ? OnionSteps : DnaSteps;
            BioFeedback.OnActionLogged += OnBioActionLogged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            BioFeedback.OnActionLogged -= OnBioActionLogged;
        }

        void Start()
        {
            RefreshDeviceCaches();
        }

        public void RefreshDeviceCaches()
        {
            microscopes = FindObjectsByType<MicroscopeController>(FindObjectsSortMode.None);
            slides = FindObjectsByType<GlassSlide>(FindObjectsSortMode.None);
            cloningStations = FindObjectsByType<GeneCloningStation>(FindObjectsSortMode.None);
            cyclers = FindObjectsByType<ThermalCyclerController>(FindObjectsSortMode.None);
            gelTanks = FindObjectsByType<GelTankController>(FindObjectsSortMode.None);
            gelDocs = FindObjectsByType<GelDocController>(FindObjectsSortMode.None);
            incubators = FindObjectsByType<IncubatorController>(FindObjectsSortMode.None);
            waterBaths = FindObjectsByType<WaterBathController>(FindObjectsSortMode.None);
            cachesInitialized = true;
        }

        public void SetScenario(Scenario scenario, bool lockScenario = false)
        {
            if (CurrentScenario == scenario && StepNames != null)
            {
                if (lockScenario) ScenarioLocked = true;
                return;
            }
            CurrentScenario = scenario;
            if (lockScenario) ScenarioLocked = true;
            StepNames = scenario == Scenario.OnionPlasmolysis ? OnionSteps : DnaSteps;
            for (int i = 0; i < Done.Length; i++) Done[i] = false;
            Current = Step.Buoc1;
            LastResult = null;
            maxMagnificationUsed = 40f;
            maxPlasmolysisPercent = 0f;
            currentPlasmolysisPercent = 0f;
            hadFocused4x = false;
        }

        public void NewRun()
        {
            for (int i = 0; i < Done.Length; i++) Done[i] = false;
            Current = Step.Buoc1;
            LastResult = null;
            maxMagnificationUsed = 40f;
            maxPlasmolysisPercent = 0f;
            currentPlasmolysisPercent = 0f;
            hadFocused4x = false;
            BioFeedback.MistakeHistory.Clear();

            var hand = HandController.Active;
            if (hand != null && hand.Held != null) hand.ReturnHome();

            if (CurrentScenario == Scenario.DnaSplicingGfp && cloningStations != null)
            {
                for (int i = 0; i < cloningStations.Length; i++)
                {
                    if (cloningStations[i] != null) cloningStations[i].ResetFrom(0);
                }
            }

            string title = CurrentScenario == Scenario.OnionPlasmolysis
                ? "Làm tiêu bản biểu bì vảy hành tím & Co – Phản co nguyên sinh"
                : "Tạo ADN tái tổ hợp pUC19-GFP, PCR – Điện di & Biến nạp E. coli";
            LabMessages.Say($"Lượt thực hành mới: {title}", LabMessages.Tone.Info);
            BioFeedback.ShowToast($"Đã làm mới bài thực hành 7 bước: {title}", false, 3.5f);
        }

        void OnBioActionLogged(string equipmentId, string actionCode, string detail)
        {
            if (CurrentScenario == Scenario.OnionPlasmolysis)
            {
                if (actionCode == "lay_lam_kinh") Done[0] = true;
                if (actionCode == "dat_mau_tieu_ban" || actionCode == "day_lamen_45_do" || actionCode == "nho_thuoc_nhuom")
                {
                    Done[0] = true;
                    Done[1] = true;
                }
                if (actionCode == "dat_tieu_ban")
                {
                    Done[0] = true;
                    Done[1] = true;
                    Done[2] = true;
                }
                if (actionCode == "nho_nacl_co_nguyen_sinh")
                {
                    Done[0] = Done[1] = Done[2] = true;
                    Done[4] = true;
                    if (maxPlasmolysisPercent < 62f) maxPlasmolysisPercent = 62f;
                }
                if (actionCode == "nho_nuoc_cat_phan_co_nguyen_sinh" && Done[4])
                {
                    Done[5] = true;
                    currentPlasmolysisPercent = 3f;
                }
            }
            else
            {
                if (actionCode == "cat_gioi_han")
                {
                    Done[0] = true;
                    Done[1] = true;
                }
                if (actionCode == "noi_ligase")
                {
                    Done[0] = Done[1] = true;
                    Done[2] = true;
                }
                if (actionCode == "chay_pcr" || actionCode == "bat_dau_pcr")
                {
                    Done[3] = true;
                }
                if (actionCode == "bat_dien_di" || actionCode == "bat_uv_geldoc")
                {
                    Done[4] = true;
                }
                if (actionCode == "bien_nap_ecoli" || actionCode == "cay_dia_chon_loc")
                {
                    Done[5] = true;
                }
            }
        }

        void Update()
        {
            if (!Active) return;

            var hand = HandController.Active;
            if (hand != null && hand.viewCamera != null && !ScenarioLocked)
            {
                Vector3 pos = hand.viewCamera.transform.position;
                if (pos.y > 3.0f)
                {
                    // Tự động chuyển sang Kịch bản 2 khi bước vào Phòng Sinh học Phân tử (z < -2.2, x: 0..13.5)
                    if (pos.z < -2.2f && pos.x >= -0.5f && pos.x <= 13.8f)
                    {
                        if (CurrentScenario != Scenario.DnaSplicingGfp)
                            SetScenario(Scenario.DnaSplicingGfp, false);
                    }
                    else if (pos.z >= -1.8f && pos.x >= 0f && pos.x <= 15.5f)
                    {
                        if (CurrentScenario != Scenario.OnionPlasmolysis)
                            SetScenario(Scenario.OnionPlasmolysis, false);
                    }
                }
            }

            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer <= 0f)
            {
                pollTimer = 0.2f;
                if (!cachesInitialized) RefreshDeviceCaches();
                if (CurrentScenario == Scenario.OnionPlasmolysis)
                    EvaluateOnionPlasmolysis();
                else
                    EvaluateDnaSplicing();

                Done[6] = LastResult.HasValue;
                Current = Step.Xong;
                for (int i = 0; i < 7; i++)
                {
                    if (!Done[i])
                    {
                        Current = (Step)i;
                        break;
                    }
                }
            }
        }

        void EvaluateOnionPlasmolysis()
        {
            var hand = HandController.Active;
            if (hand != null && hand.Held != null)
            {
                if (hand.Held.GetComponent<GlassSlide>() != null)
                    Done[0] = true;
            }

            if (slides != null)
            {
                for (int i = 0; i < slides.Length; i++)
                {
                    var s = slides[i];
                    if (s == null) continue;
                    var g = s.GetComponent<Grabbable>();
                    if (g != null && (g.IsHeld || Vector3.Distance(s.transform.position, g.HomePosition) > 0.02f))
                        Done[0] = true;

                    if (s.sample != null && !string.IsNullOrEmpty(s.sample.sampleId) && s.sample.hasCoverslip)
                    {
                        if (g != null && (g.IsHeld || Vector3.Distance(s.transform.position, g.HomePosition) > 0.02f || s.sample.isBlotted))
                        {
                            Done[0] = true;
                            Done[1] = true;
                        }
                    }
                }
            }

            if (microscopes != null)
            {
                for (int i = 0; i < microscopes.Length; i++)
                {
                    var mc = microscopes[i];
                    if (mc == null) continue;

                    bool hasMounted = mc.mountedSlide != null || (mc.stageSocket != null && mc.stageSocket.Occupant != null);
                    if (hasMounted)
                    {
                        Done[0] = true;
                        Done[1] = true;
                        Done[2] = true;
                    }

                    if (mc.lampOn)
                    {
                        float mag = (float)mc.CurrentMagnification;
                        if (mag > maxMagnificationUsed) maxMagnificationUsed = mag;

                        double blur = mc.ComputeCurrentBlurFactor();
                        if (blur < 0.25)
                        {
                            if (mc.currentObjectiveIndex == 0) hadFocused4x = true;
                            if (mc.currentObjectiveIndex >= 1 || (hadFocused4x && mc.currentObjectiveIndex >= 0))
                            {
                                Done[0] = Done[1] = Done[2] = true;
                                Done[3] = true;
                            }
                        }

                        float plas = 0f;
                        float nacl = 0f;
                        if (mc.mountedSlide != null)
                        {
                            plas = mc.mountedSlide.plasmolysisFraction;
                            nacl = mc.mountedSlide.naclPercent;
                        }
                        else if (mc.slideSample != null && mc.slideSample.concentrationPct > 1.2f)
                        {
                            nacl = mc.slideSample.concentrationPct;
                            plas = (float)Osmosis.PlasmolysisFraction(Osmosis.NaClPercentToMolar(nacl));
                        }
                        if (plas * 100f > maxPlasmolysisPercent)
                            maxPlasmolysisPercent = plas * 100f;
                        currentPlasmolysisPercent = plas * 100f;

                        if (plas > 0.35f || (nacl >= 2.5f && plas > 0.25f))
                        {
                            Done[0] = Done[1] = Done[2] = Done[3] = true;
                            Done[4] = true;
                        }

                        if (Done[4] && maxPlasmolysisPercent >= 35f && plas < 0.10f && nacl < 1.0f)
                        {
                            Done[5] = true;
                        }
                    }
                }
            }
        }

        void EvaluateDnaSplicing()
        {
            var hand = HandController.Active;
            if (hand != null && hand.Held != null)
            {
                if (hand.Held.GetComponent<MicroTube>() != null)
                    Done[0] = true;
            }

            if (cloningStations != null)
            {
                for (int i = 0; i < cloningStations.Length; i++)
                {
                    var st = cloningStations[i];
                    if (st == null) continue;

                    if (st.sourceTube != null || st.digested || (st.vectorEnzymes != null && st.vectorEnzymes.Count > 0))
                        Done[0] = true;

                    if (st.digested && st.vectorFragment != null && st.insertFragment != null)
                    {
                        Done[0] = true;
                        Done[1] = true;
                    }

                    if (st.ligated && st.ligation.Recombinant != null)
                    {
                        Done[0] = Done[1] = true;
                        Done[2] = true;
                    }

                    if (st.transformed || st.plated)
                    {
                        Done[5] = true;
                    }
                }
            }

            if (cyclers != null)
            {
                for (int i = 0; i < cyclers.Length; i++)
                {
                    var c = cyclers[i];
                    if (c != null && (c.running || c.finished || c.simSeconds > 0))
                    {
                        Done[3] = true;
                        break;
                    }
                }
            }

            if (gelTanks != null)
            {
                for (int i = 0; i < gelTanks.Length; i++)
                {
                    var g = gelTanks[i];
                    if (g != null && (g.hasCompletedRun || g.running || g.runMinutes > 2.0))
                    {
                        Done[4] = true;
                        break;
                    }
                }
            }

            if (!Done[4] && gelDocs != null)
            {
                for (int i = 0; i < gelDocs.Length; i++)
                {
                    var gd = gelDocs[i];
                    if (gd != null && gd.captured)
                    {
                        Done[4] = true;
                        break;
                    }
                }
            }

            if (!Done[5] && incubators != null)
            {
                for (int i = 0; i < incubators.Length; i++)
                {
                    var inc = incubators[i];
                    if (inc != null && inc.dishesInside != null && inc.dishesInside.Count > 0)
                    {
                        Done[5] = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Ghi nhận kết quả thực hành và chấm điểm thang 100 khi nhấn phím Enter (RecordReading).
        /// </summary>
        public void RecordReading()
        {
            if (!Active) return;
            if (LastResult.HasValue)
            {
                LabMessages.Say("Đã chấm điểm lượt này. Nhấn phím R để làm lượt mới.", LabMessages.Tone.Info);
                return;
            }

            int completed = 0;
            for (int i = 0; i < 6; i++)
                if (Done[i]) completed++;

            int score = 100;
            var notes = new List<string>();

            // Trừ điểm nếu bỏ qua bước quy trình (mỗi bước thiếu trừ 10 điểm)
            int missingSteps = 6 - completed;
            if (missingSteps > 0)
            {
                int stepPenalty = missingSteps * 10;
                score -= stepPenalty;
                notes.Add($"Hoàn thành {completed}/6 bước thao tác chuẩn (-{stepPenalty} đ)");
            }

            // Tổng hợp lỗi kỹ thuật từ BioFeedback.MistakeHistory
            int mistakeCount = BioFeedback.MistakeHistory.Count;
            var seenCodes = new HashSet<string>();
            for (int i = 0; i < BioFeedback.MistakeHistory.Count; i++)
            {
                var m = BioFeedback.MistakeHistory[i];
                if (!seenCodes.Add(m.mistakeCode)) continue;

                int pen = 5;
                if (m.mistakeCode == "oc_so_cap_lam_vo_lamen") pen = 20;
                else if (m.mistakeCode == "lech_dau_dinh_enzyme") pen = 15;
                else if (m.mistakeCode == "ha_lamen_roi_thang" || m.mistakeCode == "chua_dan_phang_bieu_bi") pen = 10;
                else if (m.mistakeCode == "que_cay_chua_khu_trung" || m.mistakeCode == "que_cay_qua_nong_giet_khuan") pen = 10;
                else if (m.mistakeCode == "hut_khong_dau_con" || m.mistakeCode == "khong_thay_dau_con") pen = 10;

                score -= pen;
                notes.Add($"{m.description} (-{pen} đ)");
            }

            score = Mathf.Clamp(score, 0, 100);
            Done[6] = true;
            Current = Step.Xong;

            if (notes.Count == 0)
            {
                if (CurrentScenario == Scenario.OnionPlasmolysis)
                    notes.Add("Thao tác chuẩn: hạ lamen góc 45° không bọt khí, lấy nét từ 4×→10×→40×, co & phản co nguyên sinh rõ nét.");
                else
                    notes.Add("Thao tác chuẩn: cắt giới hạn đúng đầu dính EcoRI/BamHI, nối pUC19-GFP (3365 bp), PCR – điện di và biến nạp thành công.");
            }

            int recBp = 3365;
            int whiteCols = 42;
            if (cloningStations != null && cloningStations.Length > 0 && cloningStations[0] != null)
            {
                var st = cloningStations[0];
                if (st.ligation.Recombinant != null) recBp = st.ligation.Recombinant.LengthBp;
                if (st.colonies.WhiteColonies > 0) whiteCols = st.colonies.WhiteColonies;
            }

            float maxPlas = maxPlasmolysisPercent > 0.1f ? maxPlasmolysisPercent : (Done[4] ? 62.5f : 0f);
            float finalPlas = Done[5] ? Mathf.Min(currentPlasmolysisPercent, 4.0f) : currentPlasmolysisPercent;

            string headline = $"{score} / 100 điểm";
            string subline = CurrentScenario == Scenario.OnionPlasmolysis
                ? $"Phóng đại: {LabProcedure.F(maxMagnificationUsed, "0")}× · Co cực đại: {LabProcedure.F(maxPlas, "0.0")} % · Sau phản co: {LabProcedure.F(finalPlas, "0.0")} %"
                : $"Plasmid tái tổ hợp: {recBp} bp · Khuẩn lạc trắng GFP+: {whiteCols} · Lỗi kỹ thuật: {mistakeCount}";
            string badge = score >= 90 ? "Xuất sắc (Đạt chuẩn)" : score >= 70 ? "Đạt yêu cầu" : "Cần rèn luyện thêm";

            LastResult = new BioResult
            {
                scenario = CurrentScenario,
                score = score,
                completedSteps = completed + 1,
                mistakeCount = mistakeCount,
                maxMagnification = maxMagnificationUsed,
                maxPlasmolysisPercent = maxPlas,
                finalPlasmolysisPercent = finalPlas,
                recombinantBp = recBp,
                whiteColonies = whiteCols,
                headline = headline,
                subline = subline,
                badgeText = badge,
                notes = notes
            };

            BioFeedback.PlaySuccess();
            LabMessages.Say($"Hoàn thành bài thực hành Sinh học: {headline} ({badge})", score >= 75 ? LabMessages.Tone.Good : LabMessages.Tone.Warn);
            BioFeedback.ShowToast($"KẾT QUẢ CHẤM ĐIỂM: {headline} — {badge}\n{subline}", score < 60, 6.0f, notes[0]);
        }
    }
}
