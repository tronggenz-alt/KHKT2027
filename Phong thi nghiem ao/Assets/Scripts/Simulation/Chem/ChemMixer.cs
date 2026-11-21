using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Phần "vật lý của bình" dùng chung cho LabContainer (Unity) và kiểm thử: trộn hai lượng chất (cân bằng, nhiệt
    /// trung hòa, pha loãng axit đặc, bộ quan sát), chạy theo thời gian (kể cả cân bằng lại khi nhiệt độ đổi), và
    /// theo dõi nhiệt phản ứng để báo "nóng lên / lạnh đi".
    /// </summary>
    public static class ChemMixer
    {
        /// <summary>
        /// Trộn s (mlS, ppS giọt phenolphtalein) vào a. Trả về thành phần sau cân bằng; heatJ là nhiệt tỏa ra (J).
        /// Hiện tượng (kể cả thẻ của bộ quan sát khi không có dấu hiệu rõ) ghi vào events.
        /// </summary>
        public static Mix Merge(Mix a, double mlA, float ppA, Mix s, double mlS, float ppS, ChemEnv env, List<ChemEvent> events, out double heatJ)
        {
            a = a ?? Mix.Empty;
            s = s ?? Mix.Empty;
            var m = a + s;
            double ml = mlA + mlS;
            heatJ = 0;
            if (ml < 0.002) return m;
            double V = ml / 1000.0;
            double hA = 0, ohA = 0, hB = 0, ohB = 0;
            bool liquidBoth = mlA > 0.002 && mlS > 0.002;
            if (liquidBoth)
            {
                ChemEngine.FreeIons(a, mlA / 1000.0, out hA, out ohA);
                ChemEngine.FreeIons(s, mlS / 1000.0, out hB, out ohB);
            }
            double sulfuricBefore = mlA > 0.01 ? a.Get("SO4") / (mlA / 1000.0) : 0;
            double sulfuricAdded = mlS > 0.01 ? s.Get("SO4") / (mlS / 1000.0) : 0;
            var b = m.ToBuilder();
            int first = events?.Count ?? 0;
            var res = ChemEngine.Equilibrate(b, V, env, events);
            var result = b.ToMix();
            heatJ = res.HeatJ;
            if (liquidBoth)
            {
                ChemEngine.FreeIons(result, V, out double hM, out double ohM);
                heatJ += ChemEngine.NeutralizationJ(hA, ohA, hB, ohB, hM, ohM);
            }
            // Pha loãng axit sunfuric đặc tỏa rất nhiều nhiệt; đổ nước vào axit đặc thì sôi bắn
            double sulfuricAfter = result.Get("SO4") / V;
            // rót axit đặc vào nước (đúng cách) cũng tỏa nhiều nhiệt
            if (sulfuricAdded > 6 && sulfuricAfter < sulfuricAdded * 0.97)
                heatJ += 60000 * s.Get("SO4") * (1 - sulfuricAfter / sulfuricAdded);
            if (sulfuricBefore > 6 && sulfuricAfter < sulfuricBefore * 0.97)
            {
                double mol = a.Get("SO4");
                heatJ += 60000 * mol * (1 - sulfuricAfter / sulfuricBefore);
                if (mlS > 0.5 && s.Get("SO4") / (mlS / 1000.0) < 3)
                    events?.Add(new ChemEvent
                    {
                        Kind = EventKind.Hazard, Severity = Severity.Danger, Hazard = "ban_be", Key = "nguyhiem:nuocvaoaxit",
                        Title = "Nước sôi bắn ra! Không được đổ nước vào axit đặc",
                        Detail = "Pha loãng H₂SO₄ đặc: rót từ từ axit vào nước và khuấy đều, không làm ngược lại.",
                    });
            }
            if (events != null) ChemObserver.Explain(a, mlA, ppA, s, mlS, ppS, result, ml, env, events, first);
            return result;
        }

        /// <summary>
        /// Chạy theo thời gian dt: quá trình chậm (kim loại tan, chất rắn tan, đun nóng...) và cân bằng lại khi nhiệt độ
        /// đổi quá 3 °C so với lần cân bằng trước (đun NH₄⁺ + OH⁻ thoát NH₃, đun KMnO₄ + H₂C₂O₄...).
        /// eqTempC: nhiệt độ lần cân bằng trước (vật chứa giữ). Trả về thành phần mới (hoặc chính mix nếu không đổi).
        /// </summary>
        public static Mix Tick(Mix mix, double ml, double dt, ChemEnv env, ref float pp, ref double eqTempC, List<ChemEvent> events, out double heatJ, out bool changed)
        {
            heatJ = 0;
            changed = false;
            if (mix == null || ml <= 0.002) return mix;
            double V = ml / 1000.0;
            if (ChemEngine.NeedsStep(mix, env.TempC))
            {
                var b = mix.ToBuilder();
                var res = ChemEngine.Step(b, V, dt, env, events, ref pp);
                heatJ += res.HeatJ;
                if (res.Changed || (events != null && events.Count > 0))
                {
                    mix = b.ToMix();
                    changed = true;
                }
                if (res.Changed) eqTempC = env.TempC;
            }
            if (Math.Abs(env.TempC - eqTempC) > 3)
            {
                var b = mix.ToBuilder();
                var res = ChemEngine.Equilibrate(b, V, env, events);
                heatJ += res.HeatJ;
                mix = b.ToMix();
                changed = true;
                eqTempC = env.TempC;
            }
            return mix;
        }
    }

    /// <summary>
    /// Theo dõi nhiệt do phản ứng / hòa tan (không tính nhiệt đèn cồn): cộng dồn độ tăng nhiệt trong một đợt, đủ
    /// 6 °C thì báo "nóng lên", giảm đủ 3 °C thì báo "lạnh đi" (hòa tan thu nhiệt: NH₄NO₃, NH₄Cl, KNO₃...).
    /// Tỏa nhiệt từng chút (vôi sống tan dần) cũng được báo, khác với kiểm tra theo từng bước.
    /// </summary>
    public sealed class HeatWatch
    {
        double acc, lastTime = -1e9;

        /// <summary>dT: độ biến thiên nhiệt độ do phản ứng vừa rồi. Trả về sự kiện cần báo, hoặc null.</summary>
        public ChemEvent Add(double dT, double now, double tempC)
        {
            if (dT == 0) return null;
            if (now - lastTime > 20) acc = 0;
            lastTime = now;
            if (Math.Sign(dT) != Math.Sign(acc)) acc = 0;
            acc += dT;
            if (acc > 6)
            {
                acc = 0;
                return new ChemEvent
                {
                    Kind = EventKind.Heat, Severity = tempC > 80 ? Severity.Warning : Severity.Info, Hazard = tempC > 80 ? "bong" : null,
                    Key = "nhiet", Title = $"Dung dịch nóng lên (≈ {tempC:0} °C)", Detail = "Phản ứng tỏa nhiệt (trung hòa, hòa tan, kim loại tác dụng với nước...).",
                };
            }
            if (acc < -3)
            {
                acc = 0;
                return new ChemEvent
                {
                    Kind = EventKind.Heat, Key = "nhiet_lanh", Title = $"Dung dịch lạnh đi (≈ {tempC:0} °C)",
                    Detail = "Quá trình hòa tan thu nhiệt: phá vỡ mạng tinh thể cần nhiều năng lượng hơn năng lượng tỏa ra khi ion bị hiđrat hóa.",
                };
            }
            return null;
        }
    }
}
