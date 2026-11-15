using System.Collections;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Kỹ năng của robot với dụng cụ sinh học tầng 2. Lab sinh là lab tự do: mỗi vật tự biết nó nhận dụng cụ nào và cho kết quả
    /// gì (IBioTarget), nên robot KHÔNG có kịch bản riêng cho từng dụng cụ. Robot làm đúng như bàn tay người học
    /// (HandController, nhánh Act.BioTarget): cầm dụng cụ, đưa tới vật đích, hỏi vật đích "dùng được không", giữ đủ thời gian
    /// rồi hoàn tất. Kết quả do BioRules / BioVessel tính; robot chỉ nói lại thông báo thực hành vừa hiện.
    ///   dung_dung_cu : dùng dụng cụ (doi_tuong) lên vật đích (dich), lặp so_luong lần (rót, hút, nhỏ, gắp, thái, khuấy, đo, cấy...)
    ///   lay_dung_cu  : lấy một dụng cụ mới từ hộp cấp (ống nghiệm, cốc, lam kính...), sau đó gọi nó là "vat_vua_lay"
    ///   chinh_num    : vặn núm chỉnh (nhiệt độ bể ổn nhiệt, điện áp điện di...) về giá trị so_luong
    ///   bat_tat      : bấm nút, mở nắp, bật thiết bị sinh học (dùng chung với thiết bị tầng 1)
    ///   quan_sat     : nói trong bình sinh học / mẫu / thiết bị có gì
    /// </summary>
    public partial class LabRobot
    {
        string bioToast;
        bool bioToastError;
        Grabbable lastTaken;

        void OnBioToast(string message, bool isError)
        {
            if (task == null || string.IsNullOrEmpty(message)) return;  // chỉ nghe trong lúc robot đang thao tác
            bioToast = message;
            bioToastError = isError;
        }

        static IBioTarget TargetOf(Component c)
        {
            if (c == null) return null;
            if (c is IBioTarget direct) return direct;
            var t = c.GetComponent<IBioTarget>();
            return t ?? c.GetComponentInParent<IBioTarget>();
        }

        /// <summary>Điểm trên cùng của vật (miệng bình, mặt lam kính, mặt đĩa) để đưa đầu dụng cụ tới.</summary>
        static Vector3 TopOf(Component c)
        {
            var cols = c.GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return c.transform.position + Vector3.up * 0.05f;
            var b = cols[0].bounds;
            foreach (var col in cols) b.Encapsulate(col.bounds);
            if (b.size.y > 0.5f) return c.transform.position + Vector3.up * 0.1f;  // máy lớn: với vào thân máy
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        /// <summary>
        /// Dùng dụng cụ lên vật đích, như người học cầm dụng cụ rồi bấm / giữ chuột vào vật đích. Lặp times lần
        /// (mỗi lần là một thao tác: một lần rót, một giọt, một lần gắp...).
        /// </summary>
        IEnumerator ApplyTool(Component toolObj, Component targetObj, int times, Res r)
        {
            var target = TargetOf(targetObj);
            if (target == null) { Fault(r, "vật đích không nhận dụng cụ sinh học"); yield break; }
            var targetComp = (Component)target;
            Grabbable tg = toolObj != null ? toolObj.GetComponentInParent<Grabbable>() : Holding;
            BioTool tool = tg != null ? tg.GetComponent<BioTool>() : null;
            if (tool == null) { Fault(r, "cần một dụng cụ sinh học cầm được (kẹp, ống nhỏ giọt, que cấy, bình, micropipet...)"); yield break; }
            var targetGrab = targetComp.GetComponentInParent<Grabbable>();
            if (targetGrab == tg) { Fault(r, "dụng cụ và vật đích là một"); yield break; }
            if (targetGrab != null && targetGrab.IsHeld && Holding != targetGrab) { Fault(r, $"{target.TargetName} đang được người dùng cầm"); yield break; }
            if (Holding == targetGrab && targetGrab != null) { yield return PutDownNearby(r); if (!r.ok) yield break; }  // robot đang cầm chính vật đích: đặt xuống trước
            yield return Grab(tg, r);
            if (!r.ok) yield break;
            yield return WalkNear(targetComp.transform.position, r);
            if (!r.ok) yield break;
            if (!target.CanApplyTool(tool, out string prompt))
            {
                Fault(r, $"{tool.displayName} không dùng được lên {target.TargetName}");
                yield break;
            }
            // Bình cầm trên tay rót vào bình đích thì nghiêng; lấy từ chai gốc hoặc hứng vào bình đang cầm thì giữ thẳng
            var heldVessel = tool.GetComponent<BioVessel>();
            var targetVessel = targetComp.GetComponent<BioVessel>();
            bool pourOut = heldVessel != null && targetVessel != null && !targetVessel.isStock && !(heldVessel.mix.IsEmpty && !targetVessel.mix.IsEmpty);
            Quaternion rot = pourOut ? Quaternion.AngleAxis(-80f, transform.right) * Upright : Upright;
            Vector3 tipLocal = tool.ToolTip != null ? tg.transform.InverseTransformPoint(tool.ToolTip.position)
                             : pourOut ? Vector3.up * (tg.gripHeightM + 0.03f) : Vector3.zero;
            yield return HoldAt(TopOf(targetComp) + Vector3.up * (pourOut ? 0.05f : 0.02f), tipLocal, rot, 0.6f);
            string said = prompt;
            int done = 0;
            for (int k = 0; k < times; k++)
            {
                if (k > 0 && !target.CanApplyTool(tool, out prompt)) break;
                bioToast = null;
                bioToastError = false;
                float req = target.GetRequiredHoldSeconds(tool);
                if (req > 0.01f)
                {
                    for (float t = 0; t < req; t += Time.deltaTime)
                    {
                        target.OnToolApplyStep(tool, Time.deltaTime, Mathf.Clamp01(t / req));
                        yield return null;
                    }
                }
                target.OnToolApplyComplete(tool);
                done++;
                yield return new WaitForSeconds(0.35f);
                if (!string.IsNullOrEmpty(bioToast)) said = bioToast;
                if (bioToastError) { Fault(r, said); break; }  // thao tác sai quy trình: dừng, nói lý do
            }
            holdRot = Upright;
            yield return Retract();
            if (r.ok) Say($"{tool.displayName} → {target.TargetName}: {said}{(done > 1 ? $" (làm {done} lần)" : "")}.");
        }

        /// <summary>Lấy một dụng cụ mới từ hộp cấp (không đưa vào tay người dùng). Sau đó vật này có mã "vat_vua_lay".</summary>
        IEnumerator TakeFromDispenser(BioDispenser d, Res r)
        {
            if (d == null) { Fault(r, "không thấy hộp cấp dụng cụ"); yield break; }
            if (Holding != null) { yield return Release(r); if (!r.ok) yield break; }
            yield return WalkNear(d.transform.position, r);
            if (!r.ok) yield break;
            yield return Reach(d.transform.position + Vector3.up * 0.08f, 0.4f);
            var go = d.Spawn(false);
            var g = go != null ? go.GetComponent<Grabbable>() : null;
            if (g == null) { Fault(r, $"hộp cấp đã hết {d.itemName}"); yield return Retract(); yield break; }
            g.SetSocket(null);
            g.SetHeld(true);
            Holding = g;
            lastTaken = g;
            holdRot = Upright;
            yield return Retract();
            Say($"Mình đã lấy một {d.itemName} mới.");
        }

        /// <summary>Vặn núm chỉnh của thiết bị sinh học về một giá trị (trong khoảng cho phép của núm).</summary>
        IEnumerator SetKnob(Component obj, float value, Res r)
        {
            var knob = obj != null ? (obj as BioKnob ?? obj.GetComponentInChildren<BioKnob>() ?? obj.GetComponentInParent<BioKnob>()) : null;
            if (knob == null) { Fault(r, "không thấy núm chỉnh"); yield break; }
            yield return WalkNear(knob.transform.position, r);
            if (!r.ok) yield break;
            yield return Reach(knob.transform.position, 0.4f);
            bioToast = null;
            bioToastError = false;
            knob.SetValue(value);
            yield return new WaitForSeconds(0.4f);
            yield return Retract();
            if (bioToastError && !string.IsNullOrEmpty(bioToast)) { Fault(r, bioToast); yield break; }
            Say($"Đã chỉnh {knob.label}: {knob.FormattedValue()}.");
        }

        /// <summary>Tả một bình sinh học, mẫu hay thiết bị theo đúng trạng thái mô phỏng (không đoán).</summary>
        IEnumerator ObserveBio(Component obj, Res r)
        {
            Vector3 p = obj.transform.position;
            if (Vector3.Distance(transform.position, p) > 1.6f) { yield return WalkNear(p, r, 1.2f); if (!r.ok) yield break; }
            yield return Point(p + Vector3.up * 0.06f, r);
            Say(DescribeBio(obj));
        }

        /// <summary>Lời tả vật sinh học: thành phần, nhiệt độ, hiện tượng (BioRules), hoặc phần mô tả của thiết bị.</summary>
        public static string DescribeBio(Component obj)
        {
            var v = obj.GetComponentInParent<BioVessel>();
            if (v != null)
            {
                if (v.isStock) return $"{v.label}: sinh phẩm gốc, còn {v.mix.Of(v.stockId):0.#} {v.unit}.";
                if (v.mix.IsEmpty) return $"{v.label} đang trống.";
                string ph = BioRules.Phenomena(v.mix);
                return $"{v.label}: {v.mix.Describe()}, {v.TemperatureC:0} °C{(ph.Length > 0 ? ". Hiện tượng: " + ph : "")}.";
            }
            var s = obj.GetComponentInParent<BioSample>();
            if (s != null) return $"{obj.name}: {s.DescribeSummary()}.";
            var tool = obj.GetComponentInParent<BioTool>();
            if (tool != null) return tool.GetStatusSummary() + ".";
            var info = obj.GetComponentInParent<EquipmentInfo>();
            if (info != null) return $"{info.title}: {info.description}";
            var usable = obj.GetComponentInParent<IDemoUsable>();
            return usable != null ? usable.UseHint : obj.name;
        }
    }
}
