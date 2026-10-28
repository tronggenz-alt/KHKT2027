using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Kỹ năng thí nghiệm của robot (gọi từ kế hoạch AI, lệnh tại chỗ hoặc thí nghiệm mẫu):
    ///   chuyển chất  : rót, nhỏ giọt, xúc, hút pipet, tia nước cất, sục khí, đổ bỏ vào cốc thải
    ///   xử lý        : lắc, khuấy từ, đun nóng (hơ trên lửa hoặc đặt lên bếp), ly tâm, đưa vào tủ hút
    ///   đo, quan sát : pH, nhiệt độ, độ dẫn điện, giấy quỳ, giấy pH, cân, cân chất rắn, máy phân tích, quan sát
    ///   quy trình    : chuẩn độ trọn vẹn, thư viện thí nghiệm mẫu (RobotDemos)
    /// Robot tự đi lấy dụng cụ (máy đo, pipet, bình tia, giấy thử...) gần nhất và dùng xong thì cất đúng chỗ cũ.
    /// Mọi kết quả (số đo, màu, kết tủa) do bộ máy hóa học tính, robot chỉ đọc ra và nói lại.
    /// </summary>
    public partial class LabRobot
    {
        struct Spot { public Vector3 pos; public Quaternion rot; public PlaceSocket socket; }

        /// <summary>Chỗ cũ của các vật robot đang mượn (dùng xong đặt lại đúng chỗ đó).</summary>
        readonly Dictionary<Grabbable, Spot> borrowed = new Dictionary<Grabbable, Spot>();
        /// <summary>Mã giữ chỗ trong thí nghiệm mẫu: @ong1 (ống nghiệm trống), @coc1, @nhan:Tên, @may:Aas.</summary>
        readonly Dictionary<string, Component> placeholders = new Dictionary<string, Component>();

        static Spot SpotOf(Grabbable g) => new Spot { pos = g.transform.position, rot = g.transform.rotation, socket = g.Socket };
        Quaternion Upright => Quaternion.Euler(0, transform.eulerAngles.y + 180f, 0);
        Vector3 UserPos => viewerCamera != null ? viewerCamera.position : transform.position;

        static T NearestTo<T>(Vector3 p, Func<T, bool> ok = null, float maxDist = 1000f) where T : Component
        {
            T best = null;
            float bd = maxDist;
            foreach (var x in Khkt.Rendering.ZoneRoot.FindAll<T>())
            {
                if (ok != null && !ok(x)) continue;
                float d = Vector3.Distance(x.transform.position, p);
                if (d < bd) { bd = d; best = x; }
            }
            return best;
        }

        static bool Free(Component c)
        {
            var g = c.GetComponent<Grabbable>();
            return g != null && !g.IsHeld;
        }

        static LabContainer C(Component c) => c != null ? c.GetComponentInParent<LabContainer>() : null;

        static void Fault(Res r, string why)
        {
            r.ok = false;
            r.why = why;
        }

        // ------------------------------------------------------------------
        // Mã vật
        // ------------------------------------------------------------------

        Component Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!id.StartsWith("@")) return RobotWorld.Resolve(id);
            if (placeholders.TryGetValue(id, out var c) && c != null) return c;
            c = ResolvePlaceholder(id);
            if (c != null) placeholders[id] = c;
            return c;
        }

        Component ResolvePlaceholder(string id)
        {
            var used = new HashSet<Component>(placeholders.Values);
            if (id.StartsWith("@ong") || id.StartsWith("@coc"))
            {
                string kind = id.StartsWith("@ong") ? "ong_nghiem" : "coc";
                return NearestTo<LabContainer>(UserPos, x =>
                {
                    var g = x.GetComponent<Grabbable>();
                    return g != null && g.kind == kind && !g.IsHeld && x.reactive && x.VolumeMl < 0.05f && !used.Contains(x)
                           && x.GetComponent<ChemicalBottle>() == null && !x.label.Contains("thải");
                });
            }
            if (id.StartsWith("@nhan:"))
            {
                string label = id.Substring(6);
                return NearestTo<LabContainer>(UserPos, x => x.label == label);
            }
            if (id.StartsWith("@may:"))
            {
                string k = id.Substring(5);
                return NearestTo<AnalyzerInstrument>(UserPos, x => x.kind.ToString() == k);
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Mượn - trả
        // ------------------------------------------------------------------

        /// <summary>Bỏ vật đang cầm: vật mượn thì đặt lại đúng chỗ cũ, lọ hóa chất về kệ, còn lại đặt xuống bàn gần đó.</summary>
        IEnumerator Release(Res r)
        {
            if (Holding == null) yield break;
            if (borrowed.TryGetValue(Holding, out var s)) yield return PutBack(s, r);
            else if (Holding.GetComponent<ChemicalBottle>() != null) yield return ReturnHome(Holding, r);
            else yield return PutDownNearby(r);
        }

        IEnumerator PutBack(Spot s, Res r)
        {
            if (Holding == null) yield break;
            if (s.socket != null && s.socket.Accepts(Holding)) { yield return PlaceAt(s.socket.transform.position, s.socket, r); yield break; }
            yield return WalkNear(s.pos, r);
            if (!r.ok) yield break;
            yield return Reach(s.pos + Vector3.up * Holding.gripHeightM, 0.5f);
            var g = Holding;
            Holding = null;
            borrowed.Remove(g);
            g.SetHeld(false);
            g.transform.SetPositionAndRotation(s.pos, s.rot);
            yield return Retract();
        }

        /// <summary>Giữ vật đang cầm sao cho điểm local (trong hệ tọa độ vật) nằm tại world, hướng rot.</summary>
        IEnumerator HoldAt(Vector3 world, Vector3 local, Quaternion rot, float seconds)
        {
            if (Holding == null) yield break;
            holdRot = rot;
            Vector3 objPos = world - rot * local;
            Vector3 to = objPos + rot * (Vector3.up * Holding.gripHeightM);
            Vector3 from = handR != null ? handR.position : to;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                handTargetR = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            handTargetR = to;
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { Face(UserPos, 3f); yield return null; }
        }

        // ------------------------------------------------------------------
        // Bộ chia kỹ năng
        // ------------------------------------------------------------------

        IEnumerator StepMore(RobotStep s, Res r, Component obj, Component dest)
        {
            float n = s.so_luong;
            switch (s.ky_nang)
            {
                case "lac": yield return Shake(C(obj), n > 0 ? Mathf.Min(n, 30f) : 4f, r); break;
                case "doi": yield return Wait(Mathf.Clamp(n <= 0 ? 5f : n, 1f, 60f)); break;
                case "quan_sat": yield return Observe(C(obj), r); break;
                case "tia_nuoc": yield return Rinse(C(dest != null ? dest : obj), n > 0 ? Mathf.Min(n, 100f) : 5f, r); break;
                case "hut_pipet": yield return PipetteTransfer(C(obj), C(dest), n, r); break;
                case "suc_khi": yield return Bubble(obj != null ? obj.GetComponentInParent<ChemicalBottle>() : null, C(dest), n > 0 ? Mathf.Min(n, 30f) : 5f, r); break;
                case "dun_nong": yield return Heat(C(obj), n > 0 ? Mathf.Clamp(n, 30f, 100f) : 70f, dest != null ? dest.GetComponentInParent<HeatSource>() : null, r); break;
                case "khuay": yield return Stir(C(obj), n > 0 ? Mathf.Min(n, 120f) : 15f, r); break;
                case "do_ph": yield return MeasureWith(MeterProbe.Kind.PH, C(obj), r); break;
                case "do_nhiet_do": yield return MeasureWith(MeterProbe.Kind.Temperature, C(obj), r); break;
                case "do_do_dan": yield return MeasureWith(MeterProbe.Kind.Conductivity, C(obj), r); break;
                case "thu_quy": yield return PaperTest(C(obj), TestPaper.Kind.Litmus, r); break;
                case "thu_giay_ph": yield return PaperTest(C(obj), TestPaper.Kind.Universal, r); break;
                case "can": yield return Weigh(obj != null ? obj.GetComponentInParent<Grabbable>() : null, r); break;
                case "can_chat_ran": yield return WeighSolid(obj != null ? obj.GetComponentInParent<ChemicalBottle>() : null, C(dest), n > 0 ? Mathf.Min(n, 20f) : 1f, r); break;
                case "ly_tam": yield return Spin(C(obj), r); break;
                case "phan_tich": yield return Analyze(C(obj), dest, r); break;
                case "do_bo": yield return Discard(C(obj), n, r); break;
                case "dua_vao_tu_hut": yield return MoveToHood(C(obj), r); break;
                case "chuan_do": yield return Titration(r); break;
                case "thi_nghiem_mau": yield return Demo(s.doi_tuong, r); break;
                default: Fault(r, "kỹ năng chưa hỗ trợ"); break;
            }
        }

        // ------------------------------------------------------------------
        // Lệnh nhanh từ khung trò chuyện
        // ------------------------------------------------------------------

        public void RunDemo(string key)
        {
            if (RobotDemos.Title(key) == null) { Say("Mình chưa có thí nghiệm mẫu này."); return; }
            ExecutePlan(new List<RobotStep> { new RobotStep { ky_nang = "thi_nghiem_mau", doi_tuong = key } });
        }

        IEnumerator Demo(string key, Res r)
        {
            var plan = RobotDemos.Build(key);
            if (plan == null) { Fault(r, "không có thí nghiệm mẫu '" + key + "'"); yield break; }
            placeholders.Clear();
            Say("Thí nghiệm mẫu: " + RobotDemos.Title(key) + ". Bạn quan sát nhé.");
            for (int i = 0; i < plan.Count; i++)
            {
                var st = plan[i];
                var rr = new Res();
                StatusText = $"{RobotDemos.Title(key)}: bước {i + 1}/{plan.Count} ({st.ky_nang})";
                yield return Step(st, rr);
                if (!rr.ok) { Fault(r, $"bước {i + 1} ({st.ky_nang}) {rr.why}"); yield break; }
                yield return new WaitForSeconds(0.2f);
            }
        }

        // ------------------------------------------------------------------
        // Xử lý
        // ------------------------------------------------------------------

        IEnumerator Shake(LabContainer c, float seconds, Res r)
        {
            if (c == null) { Fault(r, "không thấy bình cần lắc"); yield break; }
            var g = c.GetComponent<Grabbable>();
            if (g != null && g.Socket != null && c.swirl != null)
            {
                // bình nón đang đặt dưới buret: lắc tại chỗ
                yield return WalkNear(c.transform.position, r);
                if (!r.ok) yield break;
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    c.swirl.ManualIntensity = 1f;
                    handTargetR = MouthOf(c) + new Vector3(Mathf.Cos(t * 9f), 0, Mathf.Sin(t * 9f)) * 0.015f;
                    yield return null;
                }
                c.swirl.ManualIntensity = 0;
                yield return Retract();
                yield break;
            }
            if (g == null) { Fault(r, "vật này không cầm lên lắc được"); yield break; }
            yield return Grab(g, r);
            if (!r.ok) yield break;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                c.ExternalStir = 1f;
                holdRot = Upright * Quaternion.Euler(14f * Mathf.Sin(t * 13f), 0, 14f * Mathf.Cos(t * 13f));
                yield return null;
            }
            holdRot = Upright;
            yield return Release(r);
        }

        IEnumerator Stir(LabContainer c, float seconds, Res r)
        {
            if (c == null) { Fault(r, "không thấy bình cần khuấy"); yield break; }
            var st = NearestTo<MagneticStirrer>(c.transform.position, x => x.toggle != null);
            if (st == null) { yield return Shake(c, Mathf.Min(seconds, 8f), r); yield break; }
            var g = c.GetComponent<Grabbable>();
            var home = SpotOf(g);
            yield return Grab(g, r);
            if (!r.ok) yield break;
            Transform plate = st.plate != null ? st.plate : st.transform;
            yield return PlaceAt(SurfaceUnder(plate.position, st.zoneSize.y), null, r);
            if (!r.ok) yield break;
            if (!st.toggle.isOn) st.toggle.Use();
            Say($"Khuấy từ {c.label} trong {seconds:0} giây.");
            yield return Wait(seconds);
            if (st.toggle.isOn) st.toggle.Use();
            yield return Grab(g, r);
            if (r.ok) yield return PutBack(home, r);
        }

        /// <summary>Điểm mặt bàn ngay dưới p (trong khoảng ±halfH), để đặt vật lên mặt bếp, mặt máy khuấy.</summary>
        static Vector3 SurfaceUnder(Vector3 p, float halfH)
        {
            if (Physics.Raycast(p + Vector3.up * 0.15f, Vector3.down, out var hit, 0.4f, ~0, QueryTriggerInteraction.Ignore)
                && hit.point.y >= p.y - halfH && hit.point.y <= p.y + halfH)
                return hit.point;
            return p;
        }

        IEnumerator Heat(LabContainer c, float targetC, HeatSource preferred, Res r)
        {
            if (c == null) { Fault(r, "không thấy vật cần đun"); yield break; }
            var hs = preferred != null ? preferred : NearestTo<HeatSource>(c.transform.position, h => h.toggle != null || h.alwaysOn);
            if (hs == null) { Fault(r, "không có bếp hay đèn cồn"); yield break; }
            var g = c.GetComponent<Grabbable>();
            if (g == null) { Fault(r, "vật này không cầm được"); yield break; }
            Transform z = hs.zone != null ? hs.zone : hs.transform;
            bool flame = z.position.y - hs.transform.position.y > 0.1f;  // đèn cồn, Bunsen: hơ trên ngọn lửa
            var home = SpotOf(g);
            yield return Grab(g, r);
            if (!r.ok) yield break;
            yield return WalkNear(hs.transform.position, r);
            if (!r.ok) yield break;
            if (hs.toggle != null && !hs.toggle.isOn) hs.toggle.Use();
            Say(flame ? $"Hơ {c.label} trên ngọn lửa tới khoảng {targetC:0} °C, miệng ống hướng ra chỗ không có người."
                      : $"Đặt {c.label} lên bếp, đun tới khoảng {targetC:0} °C.");
            if (flame) yield return HoldAt(z.position, Vector3.zero, Upright * Quaternion.Euler(0, 0, 20f), 0.8f);
            else
            {
                yield return PlaceAt(SurfaceUnder(z.position, hs.zoneSize.y), null, r);
                if (!r.ok) yield break;
            }
            float guard = 0;
            while (c.TemperatureC < targetC && guard < 90f)
            {
                guard += Time.deltaTime;
                if (flame) holdRot = Upright * Quaternion.Euler(0, 0, 20f + 6f * Mathf.Sin(guard * 3f));  // hơ đều
                yield return null;
            }
            if (hs.toggle != null && hs.toggle.isOn) hs.toggle.Use();
            Say($"{c.label} đã tới {c.TemperatureC:0} °C, mình tắt {(flame ? "lửa" : "bếp")}." + (flame ? "" : " Bình còn nóng, để nguội trên bếp, đừng cầm tay."));
            if (flame)
            {
                holdRot = Upright;
                yield return PutBack(home, r);
            }
        }

        IEnumerator Spin(LabContainer c, Res r)
        {
            if (c == null) { Fault(r, "không thấy ống cần ly tâm"); yield break; }
            var g = c.GetComponent<Grabbable>();
            var cf = NearestTo<Centrifuge>(c.transform.position);
            if (cf == null || cf.slots == null) { Fault(r, "không có máy ly tâm"); yield break; }
            PlaceSocket slot = null;
            foreach (var s in cf.slots) if (s != null && s.Accepts(g)) { slot = s; break; }
            if (slot == null) { Fault(r, "máy ly tâm chỉ nhận ống nghiệm, hoặc đã đầy ổ"); yield break; }
            var home = SpotOf(g);
            yield return Grab(g, r);
            if (!r.ok) yield break;
            yield return PlaceAt(slot.transform.position, slot, r);
            if (!r.ok) yield break;
            Say("Ly tâm 3000 vòng/phút. Máy thật cần đặt ống đối trọng cùng khối lượng ở ổ đối diện.");
            yield return Reach(cf.transform.position + Vector3.up * 0.2f, 0.4f);
            cf.Use();
            yield return Retract();
            float guard = 0;
            while (cf.Spinning && guard < 20f) { guard += Time.deltaTime; yield return null; }
            Say(c.Total.Describe(5).Contains("↓") || c.Settled > 0.5f ? "Kết tủa đã lắng gọn xuống đáy ống, phần dung dịch phía trên trong lại." : "Trong ống không có kết tủa để tách.");
            yield return Grab(g, r);
            if (r.ok) yield return PutBack(home, r);
        }

        IEnumerator MoveToHood(LabContainer c, Res r)
        {
            if (c == null) { Fault(r, "không thấy vật cần đưa vào tủ hút"); yield break; }
            var hood = FumeHoodZone.Nearest(c.transform.position);
            if (hood == null) { Fault(r, "không có tủ hút"); yield break; }
            Vector3 spot = hood.transform.position;
            if (Physics.Raycast(spot + Vector3.up * 0.2f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore) && hood.Contains(hit.point + Vector3.up * 0.05f))
                spot = hit.point;
            var g = c.GetComponent<Grabbable>();
            yield return Grab(g, r);
            if (!r.ok) yield break;
            yield return PlaceAt(spot, null, r);
            Ventilate(true);
            Say($"Đã đặt {c.label} vào tủ hút và bật quạt hút.");
        }

        // ------------------------------------------------------------------
        // Chuyển chất
        // ------------------------------------------------------------------

        IEnumerator Rinse(LabContainer to, float ml, Res r)
        {
            if (to == null) { Fault(r, "không thấy bình cần thêm nước cất"); yield break; }
            var wb = NearestTo<WashBottle>(to.transform.position, Free);
            if (wb == null) { Fault(r, "không có bình tia nước cất"); yield break; }
            yield return Grab(wb.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return WalkNear(to.transform.position, r);
            if (!r.ok) yield break;
            var tilt = Quaternion.AngleAxis(-55f, transform.right) * Upright;
            Vector3 nozzleLocal = wb.nozzle != null ? wb.transform.InverseTransformPoint(wb.nozzle.position) : Vector3.up * 0.2f;
            yield return HoldAt(MouthOf(to) + Vector3.up * 0.02f, nozzleLocal, tilt, 0.6f);
            float added = 0, guard = 0;
            while (added < ml && to.FreeMl > 0.1f && guard < 40f)
            {
                float before = to.VolumeMl;
                wb.SprayInto(to, Time.deltaTime);
                added += to.VolumeMl - before;
                guard += Time.deltaTime;
                yield return null;
            }
            holdRot = Upright;
            yield return Retract();
            yield return Release(r);
        }

        IEnumerator PipetteTransfer(LabContainer from, LabContainer to, float ml, Res r)
        {
            if (from == null || to == null) { Fault(r, "thiếu bình lấy hoặc bình nhận"); yield break; }
            var pip = NearestTo<Pipette>(from.transform.position, Free);
            if (pip == null) { Fault(r, "không có pipet"); yield break; }
            yield return Grab(pip.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            Vector3 tip = pip.tip != null ? pip.transform.InverseTransformPoint(pip.tip.position) : Vector3.zero;
            yield return WalkNear(from.transform.position, r);
            if (!r.ok) yield break;
            yield return HoldAt(from.transform.position + Vector3.up * 0.012f, tip, Upright, 0.7f);
            bool toMark = ml <= 0 || Mathf.Abs(ml - pip.nominalMl) < 0.5f;
            float guard = 0;
            if (toMark)
            {
                while (pip.OffsetFromMarkMm < 3f && from.VolumeMl > 0.2f && guard < 25f) { pip.Suck(from, Time.deltaTime); guard += Time.deltaTime; yield return null; }
                // nhấc đầu pipet lên khỏi mặt chất lỏng, xả bớt về bình cũ cho đáy mặt khum chạm vạch
                yield return HoldAt(MouthOf(from) + Vector3.up * 0.01f, tip, Upright, 0.4f);
                while (pip.OffsetFromMarkMm > 0.15f && !pip.IsEmpty && guard < 45f) { pip.Drain(from, Time.deltaTime, trim: true); guard += Time.deltaTime; yield return null; }
            }
            else
            {
                float got = 0;
                while (got < ml - 0.01f && from.VolumeMl > 0.2f && guard < 25f) { got += pip.Suck(from, Time.deltaTime); guard += Time.deltaTime; yield return null; }
            }
            if (pip.IsEmpty) { Fault(r, "bình lấy đã hết dung dịch"); yield return Release(new Res()); yield break; }
            float vol = pip.VolumeMl;
            yield return WalkNear(to.transform.position, r);
            if (!r.ok) yield break;
            yield return HoldAt(MouthOf(to) + Vector3.down * 0.015f, tip, Upright, 0.7f);
            guard = 0;
            while (!pip.IsEmpty && guard < 30f) { pip.Drain(to, Time.deltaTime); guard += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.6f);  // chạm đầu pipet vào thành bình cho giọt cuối chảy hết
            Say($"Đã chuyển {vol:0.00} ml từ {from.label} sang {to.label} bằng pipet.");
            holdRot = Upright;
            yield return Retract();
            yield return Release(r);
        }

        IEnumerator Bubble(ChemicalBottle gas, LabContainer to, float seconds, Res r)
        {
            if (gas == null || !gas.IsGas) { Fault(r, "cần một bình khí"); yield break; }
            if (to == null || to.VolumeMl < 0.5f) { Fault(r, "bình nhận chưa có dung dịch để sục khí"); yield break; }
            if (gas.Entry.HasHazard("doc") && !to.InFumeHood)
            {
                Say($"{gas.DisplayName} là khí độc: mình đưa bình vào tủ hút trước.");
                yield return MoveToHood(to, r);
                if (!r.ok) yield break;
            }
            yield return Grab(gas.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return WalkNear(to.transform.position, r);
            if (!r.ok) yield break;
            yield return HoldAt(MouthOf(to) + Vector3.up * 0.06f, Vector3.zero, Quaternion.AngleAxis(-35f, transform.right) * Upright, 0.6f);
            Say($"Sục khí {gas.DisplayName} vào {to.label} trong {seconds:0} giây.");
            for (float t = 0; t < seconds; t += Time.deltaTime) { gas.BubbleInto(to, Time.deltaTime); yield return null; }
            holdRot = Upright;
            yield return Retract();
            yield return Release(r);
        }

        IEnumerator Discard(LabContainer c, float ml, Res r)
        {
            if (c == null || c.VolumeMl < 0.05f) { Fault(r, "bình đã trống"); yield break; }
            var g = c.GetComponent<Grabbable>();
            if (g == null) { Fault(r, "vật này không cầm được"); yield break; }
            float amount = ml > 0 ? Mathf.Min(ml, c.VolumeMl) : c.VolumeMl;
            var waste = NearestTo<LabContainer>(c.transform.position, x => x != c && x.label.Contains("thải") && x.FreeMl > amount, 8f);
            var home = SpotOf(g);
            if (waste != null)
            {
                yield return Pour(c, waste, amount, r);
            }
            else
            {
                var bin = NearestTo<EquipmentInfo>(c.transform.position, e => e.name.StartsWith("Thùng chất thải"));
                yield return Grab(g, r);
                if (!r.ok) yield break;
                if (bin != null) { yield return WalkNear(bin.transform.position, r); if (!r.ok) yield break; }
                yield return HoldAt((bin != null ? bin.transform.position : transform.position + transform.forward * 0.5f) + Vector3.up * 0.95f,
                                    Vector3.zero, Quaternion.AngleAxis(-110f, transform.right) * Upright, 0.6f);
                c.Take(amount);
                yield return new WaitForSeconds(1f);
                holdRot = Upright;
            }
            Say($"Đã đổ bỏ {amount:0.#} ml vào chỗ chứa chất thải hóa học (không đổ xuống bồn rửa).");
            if (Holding == g) yield return PutBack(home, r);
        }

        // ------------------------------------------------------------------
        // Đo, quan sát
        // ------------------------------------------------------------------

        IEnumerator MeasureWith(MeterProbe.Kind kind, LabContainer c, Res r)
        {
            if (c == null || c.VolumeMl < 0.3f) { Fault(r, "bình chưa có dung dịch để đo"); yield break; }
            MeterProbe probe = Holding != null ? Holding.GetComponent<MeterProbe>() : null;
            if (probe == null || probe.kind != kind)
            {
                // ưu tiên máy cầm tay (màn hình liền thân) hơn đầu đo của máy để bàn
                probe = null;
                float best = float.MaxValue;
                foreach (var m in Khkt.Rendering.ZoneRoot.FindAll<MeterProbe>())
                {
                    if (m.kind != kind || !Free(m)) continue;
                    float d = Vector3.Distance(m.transform.position, c.transform.position) + (m.display != null && !m.display.transform.IsChildOf(m.transform) ? 4f : 0f);
                    if (d < best) { best = d; probe = m; }
                }
                if (probe == null) { Fault(r, "không có máy đo phù hợp"); yield break; }
                yield return Grab(probe.GetComponent<Grabbable>(), r);
                if (!r.ok) yield break;
            }
            yield return WalkNear(c.transform.position, r);
            if (!r.ok) yield break;
            Vector3 tip = probe.tip != null ? probe.transform.InverseTransformPoint(probe.tip.position) : Vector3.zero;
            yield return HoldAt(c.transform.position + Vector3.up * 0.015f, tip, Upright, 0.6f);
            for (float t = 0; t < 1.5f; t += Time.deltaTime) { probe.Read(c); yield return null; }  // chờ số ổn định
            string read = probe.Read(c);
            Say($"{c.label}: {read}.");
            holdRot = Upright;
            yield return Retract();
            // giữ máy đo nếu bước kế tiếp còn đo; cuối kế hoạch tự cất
        }

        IEnumerator PaperTest(LabContainer c, TestPaper.Kind kind, Res r)
        {
            if (c == null || c.VolumeMl < 0.2f) { Fault(r, "bình chưa có dung dịch để thử"); yield break; }
            var paper = NearestTo<TestPaper>(c.transform.position, p => p.kind == kind && Free(p));
            if (paper == null) paper = NearestTo<TestPaper>(c.transform.position, p => p.kind != TestPaper.Kind.CobaltChloride && Free(p));
            if (paper == null) { Fault(r, "không có giấy thử"); yield break; }
            yield return Grab(paper.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return WalkNear(c.transform.position, r);
            if (!r.ok) yield break;
            yield return HoldAt(MouthOf(c), Vector3.up * 0.08f, Quaternion.Euler(180f, transform.eulerAngles.y, 0), 0.6f);  // chấm đầu giấy vào dung dịch
            string res = paper.Dip(c);
            yield return new WaitForSeconds(1f);
            Say($"{c.label}: {res}.");
            holdRot = Upright;
            yield return Retract();
            yield return Release(r);
            paper.Renew();  // lần sau dùng mẩu giấy mới
        }

        IEnumerator Weigh(Grabbable g, Res r)
        {
            if (g == null) { Fault(r, "không thấy vật cần cân"); yield break; }
            var bal = NearestTo<BalanceScale>(g.transform.position, b => b.decimals == 2 && b.pan != null);
            if (bal == null) bal = NearestTo<BalanceScale>(g.transform.position, b => b.pan != null);
            if (bal == null) { Fault(r, "không có cân"); yield break; }
            var home = SpotOf(g);
            yield return Grab(g, r);
            if (!r.ok) yield break;
            yield return PlaceAt(bal.pan.position, null, r);
            if (!r.ok) yield break;
            yield return new WaitForSeconds(1.2f);
            var c = g.GetComponent<LabContainer>();
            string inside = c != null && c.VolumeMl > 0.05f ? $", trong đó chất chứa khoảng {BalanceScale.ContentsGrams(c):0.00} g" : "";
            Say($"{(c != null ? c.label : g.name)} nặng {bal.Grams:0.00} g (cả vỏ{inside}).");
            yield return Grab(g, r);
            if (r.ok) yield return PutBack(home, r);
        }

        IEnumerator WeighSolid(ChemicalBottle cb, LabContainer to, float grams, Res r)
        {
            if (cb == null || !cb.IsSolid) { Fault(r, "cần lọ chất rắn"); yield break; }
            if (to == null) { Fault(r, "không thấy vật chứa để cân vào"); yield break; }
            var bal = NearestTo<BalanceScale>(to.transform.position, b => b.decimals == 2 && b.pan != null);
            if (bal == null) { Fault(r, "không có cân"); yield break; }
            var tg = to.GetComponent<Grabbable>();
            var home = SpotOf(tg);
            yield return Grab(tg, r);
            if (!r.ok) yield break;
            yield return PlaceAt(bal.pan.position, null, r);
            if (!r.ok) yield break;
            yield return new WaitForSeconds(0.8f);
            bal.Use();  // trừ bì
            float tare = bal.Grams;
            Say($"Đặt {to.label} lên cân, trừ bì về 0, rồi xúc {cb.DisplayName} tới {grams:0.##} g.");
            yield return Grab(cb.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return HoldOver(to, false, r);
            if (!r.ok) yield break;
            float guard = 0;
            while (bal.Grams - tare < grams - cb.scoopGrams * 0.5f && guard < 60f)
            {
                if (!cb.ScoopInto(to)) { Say("Lọ đã hết hóa chất."); break; }
                guard += 0.45f;
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(0.6f);
            Say($"Cân được {bal.Grams - tare:0.00} g {cb.DisplayName}.");
            holdRot = Upright;
            yield return Retract();
            yield return Release(r);
            yield return Grab(tg, r);
            if (r.ok) yield return PutBack(home, r);
        }

        IEnumerator Analyze(LabContainer c, Component machine, Res r)
        {
            if (c == null || c.VolumeMl < 0.3f) { Fault(r, "mẫu chưa có dung dịch"); yield break; }
            var an = machine != null ? machine.GetComponentInParent<AnalyzerInstrument>() : null;
            var spec = machine != null ? machine.GetComponentInParent<Spectrophotometer>() : null;
            if (an == null && spec == null) an = NearestTo<AnalyzerInstrument>(c.transform.position);
            PlaceSocket slot = an != null ? an.slot : spec != null ? spec.slot : null;
            if (slot == null) { Fault(r, "không có máy phân tích"); yield break; }
            var g = c.GetComponent<Grabbable>();
            if (g == null || Array.IndexOf(slot.accepts, g.kind) < 0)
            {
                // mẫu đang ở cốc, bình: chuyển 3 ml sang ống nghiệm trống để đặt vào ổ máy
                var tube = NearestTo<LabContainer>(c.transform.position, x => { var tg = x.GetComponent<Grabbable>(); return tg != null && tg.kind == "ong_nghiem" && !tg.IsHeld && x.reactive && x.VolumeMl < 0.05f; });
                if (tube == null) { Fault(r, "không có ống nghiệm trống để lấy mẫu"); yield break; }
                Say($"Lấy 3 ml {c.label} sang ống nghiệm để đặt vào máy.");
                yield return Pour(c, tube, 3f, r);
                if (!r.ok) yield break;
                yield return Release(r);
                c = tube;
                g = tube.GetComponent<Grabbable>();
            }
            if (slot.Occupant != null && slot.Occupant != g)
            {
                yield return Grab(slot.Occupant, r);
                if (!r.ok) yield break;
                yield return PutDownNearby(r);
            }
            yield return Grab(g, r);
            if (!r.ok) yield break;
            yield return PlaceAt(slot.transform.position, slot, r);
            if (!r.ok) yield break;
            if (an != null)
            {
                Say($"Chạy máy {AnalyzerInstrument.KindName(an.kind)} với {c.label}.");
                an.RunQuick();  // robot chạy nhanh, không mở màn hình vận hành của người dùng
                float guard = 0;
                yield return null;
                while (an.Running && guard < 20f) { guard += Time.deltaTime; yield return null; }
                Say($"{AnalyzerInstrument.KindName(an.kind)}: {an.LastReport.Replace("\n", "; ")}.");
            }
            else
            {
                yield return new WaitForSeconds(1.2f);
                Say($"Máy quang phổ: {spec.Reading}.");
            }
        }

        IEnumerator Observe(LabContainer c, Res r)
        {
            if (c == null) { Fault(r, "không thấy bình cần quan sát"); yield break; }
            if (Vector3.Distance(transform.position, c.transform.position) > 1.6f) { yield return WalkNear(c.transform.position, r, 1.2f); if (!r.ok) yield break; }
            yield return Turn(c.transform.position);
            yield return Point(c.transform.position + Vector3.up * 0.06f, r);
            Say(DescribeFor(c));
        }

        /// <summary>Lời tả bình theo kết quả mô phỏng: thành phần, pH, nhiệt độ, kết tủa, khí, hiện tượng gần nhất.</summary>
        public static string DescribeFor(LabContainer c)
        {
            if (c.VolumeMl < 0.05f) return $"{c.label} đang trống.";
            var sb = new StringBuilder($"{c.label}: {c.Total.Describe(5)}, pH {c.BulkPh:0.0}, {c.TemperatureC:0} °C");
            if (c.Look.Turbidity > 0.25f) sb.Append(", vẩn đục / có kết tủa");
            if (c.GasRate > 1e-6f && !string.IsNullOrEmpty(c.GasId)) sb.Append($", đang sủi khí {c.GasId}");
            var ann = ReactionAnnotator.Instance;
            if (ann != null)
                for (int i = ann.History.Count - 1; i >= 0; i--)
                    if (ann.History[i].EndsWith("trong " + c.label)) { sb.Append(". Hiện tượng: ").Append(ann.History[i]); break; }
            return sb.Append('.').ToString();
        }

        // ------------------------------------------------------------------
        // Chuẩn độ trọn vẹn (dùng bộ chuẩn độ ở bàn 1, đúng bài đang chọn trong menu)
        // ------------------------------------------------------------------

        public void DemoTitration() => ExecutePlan(new List<RobotStep> { new RobotStep { ky_nang = "chuan_do" } });

        IEnumerator Titration(Res r)
        {
            var proc = FindAnyObjectByType<LabProcedure>();
            if (proc == null || proc.buret == null) { Fault(r, "không có bộ chuẩn độ"); yield break; }
            var hand = FindAnyObjectByType<HandController>();
            if (hand != null && hand.Held != null) hand.ReturnHome();
            if (Holding != null) { yield return Release(r); if (!r.ok) yield break; }
            if (!proc.Active) proc.Begin(proc.Cfg); else proc.NewRun();
            var cfg = proc.Cfg;
            Say($"Mình làm mẫu chuẩn độ {cfg.analyteShort} bằng {cfg.titrantShort} {cfg.titrantC:0.0#} M, chỉ thị {cfg.indicatorName}. Bạn xem bảng các bước bên trái nhé.");
            var buret = proc.buret;

            StatusText = "Chuẩn độ: lắp buret";
            yield return Grab(buret.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return PlaceAt(proc.clampSocket.transform.position, proc.clampSocket, r);
            if (!r.ok) yield break;

            StatusText = "Chuẩn độ: nạp dung dịch chuẩn";
            Say($"Rót {cfg.titrantShort} vào buret, quá vạch 0 một chút.");
            yield return Pour(proc.naohStock, buret.container, buret.zeroVolumeMl - buret.container.VolumeMl + 1.2f, r);
            if (!r.ok) yield break;
            yield return Release(r);

            StatusText = "Chuẩn độ: đưa về vạch 0";
            Say("Đặt cốc thải dưới buret, mở khóa xả bọt khí ở đầu buret và đưa mặt khum về vạch 0.");
            var wasteG = proc.wasteBeaker.GetComponent<Grabbable>();
            yield return Grab(wasteG, r);
            if (!r.ok) yield break;
            yield return PlaceAt(proc.underSocket.transform.position, proc.underSocket, r);
            if (!r.ok) yield break;
            handTargetL = buret.transform.position + Vector3.up * 0.05f;
            buret.AdjustOpening(0.55f);
            float guard = 0;
            while (buret.ReadingMl < 0f && guard < 40f) { guard += Time.deltaTime; yield return null; }
            buret.Close();
            handTargetL = null;
            yield return new WaitForSeconds(0.6f);
            float v0 = Mathf.Round(buret.ReadingMl / 0.05f) * 0.05f;
            proc.RecordReading(v0);
            Say($"Mắt ngang vạch, đọc ở đáy mặt khum: V đầu = {v0:0.00} ml.");
            borrowed.Remove(wasteG);
            yield return ReturnHome(wasteG, r);

            StatusText = "Chuẩn độ: lấy mẫu bằng pipet";
            Say($"Dùng pipet bầu lấy đúng {proc.pipette.nominalMl:0} ml {cfg.analyteShort} cho vào bình nón.");
            yield return PipetteTransfer(proc.hclStock, proc.flask, proc.pipette.nominalMl, r);
            if (!r.ok) yield break;

            if (!cfg.redox && proc.indicatorDropper != null)
            {
                StatusText = "Chuẩn độ: nhỏ chỉ thị";
                Say($"Nhỏ 2 giọt {cfg.indicatorName}.");
                yield return DropInto(proc.indicatorDropper, proc.flask, 2, r);
                if (!r.ok) yield break;
                yield return Release(r);
            }

            var flaskG = proc.flask.GetComponent<Grabbable>();
            yield return Grab(flaskG, r);
            if (!r.ok) yield break;
            yield return PlaceAt(proc.underSocket.transform.position, proc.underSocket, r);
            if (!r.ok) yield break;

            StatusText = "Chuẩn độ: đang chuẩn độ";
            yield return TitrateFlask(proc, r);
            if (!r.ok) yield break;

            float v1 = Mathf.Round(buret.ReadingMl / 0.05f) * 0.05f;
            proc.RecordReading(v1);
            if (proc.LastResult.HasValue)
            {
                var res = proc.LastResult.Value;
                Say($"V cuối = {v1:0.00} ml. Thể tích {cfg.titrantShort} đã dùng {res.volumeRead:0.00} ml, nên C({cfg.analyteShort}) = {res.concentrationRead:0.0000} M"
                    + (res.hiddenConcentration ? "." : $" (đúng {res.concentrationTrue:0.0000} M, sai số {res.errorPercent:0.0}%)."));
            }
            else Say($"V cuối = {v1:0.00} ml.");
        }

        /// <summary>
        /// Chuẩn độ như người làm: mở khóa cho chảy thành dòng và lắc đều; thấy màu chỉ thị thoáng hiện quanh chỗ dung dịch
        /// rơi xuống thì chảy chậm lại; màu thoáng hiện ở cả dòng chậm thì nhỏ từng giọt; màu bền thì khóa và chờ 30 giây,
        /// màu mất thì nhỏ thêm giọt nữa. Chỉ dựa vào màu nhìn thấy (bộ máy hóa học tính), không dùng thể tích tương đương.
        /// </summary>
        IEnumerator TitrateFlask(LabProcedure proc, Res r)
        {
            var b = proc.buret;
            var f = proc.flask;
            yield return WalkNear(b.transform.position, r, 0.7f);
            if (!r.ok) yield break;
            bool pp = proc.Cfg.indicatorStt == 394 && !proc.Cfg.redox;
            var start = f.Look.Liquid;
            float Changed()
            {
                var now = f.Look.Liquid;
                float d = Mathf.Abs(now.r - start.r) + Mathf.Abs(now.g - start.g) + Mathf.Abs(now.b - start.b);
                return pp ? Mathf.Max(f.PinkVisible, d) : d;
            }
            Say($"Mở khóa cho {proc.Cfg.titrantShort} chảy thành dòng, tay kia lắc đều bình nón.");
            int phase = 0;  // 0 dòng nhanh, 1 dòng chậm, 2 từng giọt, 3 khóa chờ màu bền
            float timer = 0, guard = 0;
            b.Close();
            b.AdjustOpening(1f);
            while (guard < 400f)
            {
                float dt = Time.deltaTime;
                guard += dt;
                if (proc.swirl != null) proc.swirl.ManualIntensity = 1f;
                handTargetR = MouthOf(f) + new Vector3(Mathf.Cos(guard * 8f), 0, Mathf.Sin(guard * 8f)) * 0.015f;
                handTargetL = b.transform.position + Vector3.up * 0.05f;
                if (!b.HasLiquid) { Fault(r, "buret đã hết dung dịch"); break; }
                float ch = Changed();
                switch (phase)
                {
                    case 0:
                        if (ch > 0.15f) { b.Close(); b.AdjustOpening(0.45f); phase = 1; Say("Màu chỉ thị thoáng hiện rồi mất: sắp tới điểm tương đương, cho chảy chậm lại."); }
                        break;
                    case 1:
                        if (ch > 0.15f) { b.Close(); b.AdjustOpening(0.22f); phase = 2; Say("Màu mất chậm hơn: nhỏ từng giọt, lắc kỹ sau mỗi giọt."); }
                        break;
                    case 2:
                        timer = proc.EndpointPersistS > 0.6f ? timer + dt : 0f;
                        if (timer > 0.4f) { b.Close(); phase = 3; timer = 0; Say("Màu không mất nữa: khóa buret, lắc tiếp và chờ màu bền 30 giây."); }
                        break;
                    case 3:
                        if (proc.EndpointPersistS >= 30f) phase = 4;
                        else if (proc.EndpointPersistS <= 0f) { timer += dt; if (timer > 2f) { b.AdjustOpening(0.22f); phase = 2; timer = 0; } }
                        else timer = 0;
                        break;
                }
                if (phase == 4) break;
                yield return null;
            }
            b.Close();
            if (proc.swirl != null) proc.swirl.ManualIntensity = 0;
            handTargetL = null;
            yield return Retract();
            if (r.ok && phase == 4) Say("Màu chỉ thị bền 30 giây: đã tới điểm cuối.");
            else if (r.ok) Fault(r, "quá lâu chưa thấy điểm cuối");
        }
    }
}
