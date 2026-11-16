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
    /// 5.1.16 & 5.4.6. Kiểm tra cân tải đối xứng cho Máy ly tâm (kết hợp Centrifuge tầng 1) — mã: may_ly_tam_de_ban, may_ly_tam_mini
    /// Công thức: RCF = 1,118 × 10^-5 × r(cm) × rpm²
    /// </summary>
    [DisallowMultipleComponent]
    public class CentrifugeLoad : MonoBehaviour, IInteractable, IScrollAdjustable, IMachinePanel
    {
        public string centrifugeId = "may_ly_tam_mini";
        public int rotorSlots = 12;
        public float rotorRadiusCm = 6.5f;
        public int rpm = 6000;
        public bool lidClosed = true;
        public readonly float[] slotMassGrams = new float[24];

        float SafeRadiusCm => (!float.IsNaN(rotorRadiusCm) && !float.IsInfinity(rotorRadiusCm) && rotorRadiusCm > 0f) ? rotorRadiusCm : 6.5f;

        /// <summary>Tính lực ly tâm tương đối RCF (×g) an toàn từ tốc độ RPM và bán kính rôto.</summary>
        public float RcfForRpm(float targetRpm)
        {
            if (float.IsNaN(targetRpm) || float.IsInfinity(targetRpm) || targetRpm <= 0f) return 0f;
            double rcf = Centrifugation.Rcf(SafeRadiusCm, targetRpm);
            if (double.IsNaN(rcf) || double.IsInfinity(rcf) || rcf < 0.0) return 0f;
            return (float)rcf;
        }

        /// <summary>Quy đổi ngược RCF (×g) sang tốc độ quay (rpm) trong phạm vi 1 000..14 000 rpm.</summary>
        public int RpmForRcf(float targetRcf)
        {
            if (float.IsNaN(targetRcf) || float.IsInfinity(targetRcf) || targetRcf <= 0f) return 1000;
            double rawRpm = Centrifugation.Rpm(SafeRadiusCm, targetRcf);
            if (double.IsNaN(rawRpm) || double.IsInfinity(rawRpm)) return rpm;
            return Mathf.Clamp(Mathf.RoundToInt((float)rawRpm), 1000, 14000);
        }

        /// <summary>Đặt tốc độ máy theo giá trị lực ly tâm mục tiêu (×g).</summary>
        public int SetRcf(float targetRcf)
        {
            rpm = RpmForRcf(targetRcf);
            return rpm;
        }

        public float CalculateRcf() => RcfForRpm(rpm);

        public string UseHint => $"Máy ly tâm ({centrifugeId}) [{rpm} rpm ≈ {BioFeedback.FormatVi(CalculateRcf(), "0")} ×g · Nắp: {(lidClosed ? "ĐÓNG" : "MỞ")}] · Bấm: kiểm tra cân tải đối xứng & quay ly tâm";

        public void SetBalancedPair(int slotA, float massGrams)
        {
            Array.Clear(slotMassGrams, 0, slotMassGrams.Length);
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            int slotB = (slotA + n / 2) % n;
            slotMassGrams[slotA % n] = massGrams;
            slotMassGrams[slotB] = massGrams;
        }

        public void SetUnbalancedSingle(int slotA, float massGrams)
        {
            Array.Clear(slotMassGrams, 0, slotMassGrams.Length);
            slotMassGrams[slotA % Mathf.Clamp(rotorSlots, 2, 24)] = massGrams;
        }

        public bool IsBalanced(out float imbalanceGrams)
        {
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            var loadList = new List<(int slot, double grams)>();
            float fx = 0f, fy = 0f;
            for (int i = 0; i < n; i++)
            {
                if (slotMassGrams[i] > 0f)
                    loadList.Add((i + 1, slotMassGrams[i]));
                float angle = (2f * Mathf.PI * i) / n;
                fx += slotMassGrams[i] * Mathf.Cos(angle);
                fy += slotMassGrams[i] * Mathf.Sin(angle);
            }
            imbalanceGrams = Mathf.Sqrt(fx * fx + fy * fy);
            return Centrifugation.IsBalanced(loadList, n, out _);
        }

        public bool RunSpin(BioSample sample = null)
        {
            if (!lidClosed)
            {
                BioFeedback.RecordMistake(centrifugeId, "chay_ly_tam_chua_dong_nap",
                    "Bấm chạy máy ly tâm khi chưa đóng nắp an toàn!",
                    "Khóa liên động nắp máy ly tâm ngăn không cho rotor quay khi nắp mở.");
                return false;
            }
            if (!IsBalanced(out float diffG))
            {
                BioFeedback.RecordMistake(centrifugeId, "ly_tam_lech_tai",
                    $"Mất cân bằng tải rotor (lệch {BioFeedback.FormatVi(diffG, "0.00")} g > 0,10 g)! Máy rung mạnh và tự ngắt khẩn cấp!",
                    "Phải luôn đặt các ống Eppendorf có khối lượng tương đương (chênh ≤ 0,1 g) vào các vị trí đối xứng qua tâm rotor (thêm 1 ống đối trọng chứa nước nếu số mẫu lẻ).");
                return false;
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction(centrifugeId, "ly_tam_thanh_cong",
                $"Đã ly tâm cân bằng tại {rpm} rpm ({BioFeedback.FormatVi(CalculateRcf(), "0")} ×g): cặn lắng xuống đáy, dịch nổi tách pha rõ, giọt bám thành đã gom hết xuống đáy.");
            return true;
        }

        [NonSerialized] public float panelMinutes = 5f;
        [NonSerialized] public int panelLoad = 0;

        const int RotorVisualSize = 520;
        Painter rotorPainter;
        int cachedRotorSignature;
        bool hasRenderedRotor;

        public MachineSpec PanelSpec()
        {
            if (!HasAnyTubeLoaded()) ApplyPanelLoadPreset(panelLoad);
            float rCm = SafeRadiusCm;
            var s = new MachineSpec
            {
                code = "MÁY · LY TÂM",
                title = centrifugeId == "may_ly_tam_mini" ? "Máy ly tâm mini" : "Máy ly tâm để bàn",
                subtitle = $"Rotor {rotorSlots} vị trí, rôto {BioFeedback.FormatVi(rCm, "0.0")} cm · RCF = 1,118 × 10^-5 × r × rpm²",
                presetKey = "centrifuge_" + (string.IsNullOrEmpty(centrifugeId) ? "default" : centrifugeId),
                visual = RenderRotorVisual
            };

            var speedParam = MachineParam.Slider("Tốc độ quay", "rpm", 1000f, 14000f, 100f, () => rpm, v => rpm = Mathf.Clamp(Mathf.RoundToInt(v), 1000, 14000), "0",
                v => $"≈ {BioFeedback.FormatVi(RcfForRpm(v), "0")} ×g (rôto {BioFeedback.FormatVi(SafeRadiusCm, "0.0")} cm)" + (v < 3000f ? " · gom giọt bám thành" : v < 9000f ? " · lắng tế bào vi khuẩn" : " · tủa ADN, tách pha"));
            speedParam.altUnit = "×g";
            speedParam.altFormat = "0";
            speedParam.altMin = Mathf.Max(50f, Mathf.Ceil(RcfForRpm(1000f) / 50f) * 50f);
            speedParam.altMax = Mathf.Max(speedParam.altMin + 50f, Mathf.Floor(RcfForRpm(14000f) / 50f) * 50f);
            speedParam.altStep = 50f;
            speedParam.toAlt = RcfForRpm;
            speedParam.fromAlt = g => RpmForRcf(g);
            s.parameters.Add(speedParam);

            s.parameters.Add(MachineParam.Slider("Thời gian", "phút", 1f, 30f, 1f, () => panelMinutes, v => panelMinutes = v, "0"));
            s.parameters.Add(MachineParam.Choice("Cách đặt ống", new[] { "Đối xứng 2 ống", "Chỉ 1 ống", "Đối xứng 6 ống" }, () => panelLoad, i =>
            {
                panelLoad = i;
                ApplyPanelLoadPreset(i);
            },
                i => i < 0.5f ? "Hai ống cùng khối lượng đặt đối xứng qua tâm rotor"
                    : i < 1.5f ? "Một ống không có đối trọng: rotor mất cân bằng"
                    : "Sáu ống cùng khối lượng phân bố đều đối xứng qua tâm rotor"));
            s.parameters.Add(MachineParam.Choice("Nắp máy", new[] { "Mở", "Đóng" }, () => lidClosed ? 1 : 0, i => lidClosed = i == 1));

            s.presets.Add(new MachinePreset("Gom giọt", 2000f, 1f, 0f, 1f));
            s.presets.Add(new MachinePreset("Tách tế bào", RpmForRcf(2000f), 5f, 0f, 1f));
            s.presets.Add(new MachinePreset("Tủa ADN", RpmForRcf(12000f), 10f, 2f, 1f));

            s.runLabel = () => "Quay ly tâm";
            s.status = () =>
            {
                int loaded = CountLoadedTubes();
                bool bal = loaded == 0 || IsBalanced(out _);
                string balText = loaded == 0 ? "Rotor trống" : (bal ? $"Cân bằng ({loaded}/{Mathf.Clamp(rotorSlots, 2, 24)} ống)" : "Lệch tải!");
                return $"Đặt: {rpm} rpm ≈ {BioFeedback.FormatVi(CalculateRcf(), "0")} ×g (rôto {BioFeedback.FormatVi(SafeRadiusCm, "0.0")} cm)\nNắp: {(lidClosed ? "ĐÓNG" : "MỞ")} · {balText}";
            };
            s.run = () =>
            {
                ApplyPanelLoadPreset(panelLoad);
                if (!lidClosed) { RunSpin(); return "Máy không quay: nắp an toàn chưa đóng (khóa liên động)."; }
                if (!IsBalanced(out float diff)) { RunSpin(); return $"Máy rung mạnh và tự ngắt: rotor lệch tải {BioFeedback.FormatVi(diff, "0.00")} g.\nĐặt thêm một ống đối trọng chứa nước cùng khối lượng ở vị trí đối diện."; }
                RunSpin();
                return $"Đã ly tâm {BioFeedback.FormatVi(panelMinutes, "0")} phút ở {rpm} rpm ({BioFeedback.FormatVi(CalculateRcf(), "0")} ×g).\nCặn lắng gọn dưới đáy ống, dịch nổi trong.";
            };
            return s;
        }

        void ApplyPanelLoadPreset(int preset)
        {
            if (preset == 1)
            {
                SetUnbalancedSingle(0, 1.5f);
                return;
            }
            if (preset == 2)
            {
                Array.Clear(slotMassGrams, 0, slotMassGrams.Length);
                int n = Mathf.Clamp(rotorSlots, 2, 24);
                int step = Mathf.Max(1, n / 6);
                int placed = 0;
                for (int i = 0; i < n && placed < 6; i += step)
                {
                    slotMassGrams[i] = 1.5f;
                    placed++;
                }
                return;
            }
            SetBalancedPair(0, 1.5f);
        }

        bool HasAnyTubeLoaded()
        {
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            for (int i = 0; i < n; i++)
                if (slotMassGrams[i] > 0f) return true;
            return false;
        }

        int CountLoadedTubes()
        {
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            int c = 0;
            for (int i = 0; i < n; i++)
                if (slotMassGrams[i] > 0f) c++;
            return c;
        }

        Texture RenderRotorVisual()
        {
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            int sig;
            unchecked
            {
                sig = (int)UiTheme.Current;
                sig = sig * 397 ^ n;
                sig = sig * 397 ^ (lidClosed ? 1 : 0);
                for (int i = 0; i < n; i++)
                    sig = sig * 397 ^ Mathf.RoundToInt(slotMassGrams[i] * 100f);
            }

            if (hasRenderedRotor && rotorPainter != null && rotorPainter.Tex != null && sig == cachedRotorSignature)
                return rotorPainter.Tex;

            if (rotorPainter == null || rotorPainter.Tex == null)
                rotorPainter = new Painter(RotorVisualSize, RotorVisualSize);
            cachedRotorSignature = sig;
            hasRenderedRotor = true;

            Color surf = GlassKit.SurfaceA(1f);
            Color well = GlassKit.Well;
            Color bg = Color.Lerp(surf, new Color(well.r, well.g, well.b, 1f), well.a);
            rotorPainter.Clear(bg);

            Color line = GlassKit.Line;
            Color ringOuter = new Color(line.r, line.g, line.b, Mathf.Max(line.a, 0.42f));
            Color socketRim = Color.Lerp(GlassKit.Dim, line, 0.3f);
            socketRim.a = 0.78f;
            Color rotorPlate = Color.Lerp(bg, surf, 0.6f);

            bool hasLoad = HasAnyTubeLoaded();
            bool balanced = !hasLoad || IsBalanced(out _);
            Color activeFill = balanced ? GlassKit.Accent : GlassKit.Bad;
            Color spokeCol = new Color(activeFill.r, activeFill.g, activeFill.b, 0.42f);

            float cx = RotorVisualSize * 0.5f;
            float cy = RotorVisualSize * 0.5f;
            float rChamber = 228f;
            float rRotor = 198f;
            float rOrbit = 148f;
            float rInner = 84f;
            float rSpindle = 34f;
            float socketR = n <= 12 ? 22f : Mathf.Clamp(190f / n, 12f, 22f);

            // Outer housing ring & lid lock aura
            if (!lidClosed)
            {
                Color warn = GlassKit.Warn;
                rotorPainter.Ring(cx, cy, rChamber + 8f, new Color(warn.r, warn.g, warn.b, 0.65f), 2f);
            }
            rotorPainter.Disc(cx, cy, rRotor, rotorPlate);
            rotorPainter.Ring(cx, cy, rChamber, ringOuter, 2.2f);
            rotorPainter.Ring(cx, cy, rRotor, socketRim, 2.5f);
            rotorPainter.Ring(cx, cy, rOrbit, ringOuter, 1.5f);
            rotorPainter.Ring(cx, cy, rInner, ringOuter, 1.5f);

            // Top index pointer line from central spindle to outer chamber rim
            Color ptrCol = GlassKit.Accent;
            rotorPainter.Line(cx, cy + rSpindle, cx, cy + rChamber + 10f, new Color(ptrCol.r, ptrCol.g, ptrCol.b, 0.65f), 2.5f);
            rotorPainter.Disc(cx, cy + rChamber + 10f, 4.5f, ptrCol);

            // Tube sockets & spoke indicators
            for (int i = 0; i < n; i++)
            {
                float angle = Mathf.PI * 0.5f - (2f * Mathf.PI * i) / n;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float sx = cx + cos * rOrbit;
                float sy = cy + sin * rOrbit;

                if (slotMassGrams[i] > 0f)
                {
                    rotorPainter.Line(cx + cos * (rSpindle + 2f), cy + sin * (rSpindle + 2f),
                        cx + cos * (rOrbit - socketR - 4f), cy + sin * (rOrbit - socketR - 4f), spokeCol, 2.2f);
                    rotorPainter.Disc(sx, sy, socketR + 2.5f, surf);
                    rotorPainter.Disc(sx, sy, socketR, activeFill);
                    rotorPainter.Ring(sx, sy, socketR * 0.62f, new Color(1f, 1f, 1f, 0.52f), 1.6f);
                    rotorPainter.Ring(sx, sy, socketR + 0.5f, activeFill, 2f);
                }
                else
                {
                    rotorPainter.Disc(sx, sy, socketR, surf);
                    rotorPainter.Ring(sx, sy, socketR, socketRim, 2f);
                    rotorPainter.Disc(sx, sy, 3.5f, new Color(socketRim.r, socketRim.g, socketRim.b, 0.45f));
                }
            }

            // Central spindle hub
            rotorPainter.Disc(cx, cy, rSpindle, surf);
            rotorPainter.Ring(cx, cy, rSpindle, socketRim, 2.5f);
            rotorPainter.Disc(cx, cy, 10f, GlassKit.Dim);

            rotorPainter.Apply();
            return rotorPainter.Tex;
        }

        void OnDestroy()
        {
            if (rotorPainter != null && rotorPainter.Tex != null)
                Destroy(rotorPainter.Tex);
            rotorPainter = null;
        }

        public void Use(GameObject user)
        {
            SetBalancedPair(0, 1.5f);
            RunSpin();
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            rpm = Mathf.Clamp(rpm + (delta > 0 ? 1000 : -1000), 1000, 14000);
        }
    }
}
