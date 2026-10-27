using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.Interaction;
using Khkt.Simulation;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Robot trợ lý hình người (cao ~1,75 m): đi bộ tìm đường trên sàn (NavGrid), với tay bằng IK hai khớp,
    /// cầm - đặt - rót - nhỏ giọt - xúc - bật máy, đi theo người dùng, tự xử lý sự cố.
    ///
    /// Mô hình AI không điều khiển khớp: AI chỉ gửi kế hoạch gồm các kỹ năng tham chiếu vật bằng mã trong ảnh chụp
    /// cảnh (RobotWorld); robot này thực hiện từng kỹ năng bằng code xác định (đi tới đâu, với tay tới điểm nào,
    /// nghiêng chai bao nhiêu độ, rót bao nhiêu ml) - giống robot thật: "não" lập kế hoạch, "thân" thực thi.
    /// </summary>
    public partial class LabRobot : MonoBehaviour
    {
        [Header("Khung xương")]
        public Transform pelvis;
        public Transform torso, head;
        public Transform shoulderL, shoulderR, elbowL, elbowR, handL, handR;
        public Transform hipL, hipR, kneeL, kneeR;
        public Transform carryPoint;
        public float upperArm = 0.37f, foreArm = 0.36f, thigh = 0.43f, shin = 0.43f;

        [Header("Khác")]
        public Transform viewerCamera;
        public Renderer[] eyes;
        public Renderer ring;
        public Material puddleMaterial;
        public float walkSpeed = 1.1f, runSpeed = 1.8f;

        public static LabRobot Instance { get; private set; }
        public bool Busy => task != null;
        public string StatusText { get; private set; } = "Sẵn sàng";
        public Grabbable Holding { get; private set; }
        /// <summary>Robot nói (khung trò chuyện hiện lên).</summary>
        public event Action<string> Spoke;

        Coroutine task;
        float phase, speedNow, lean, alertUntil, followCheck;
        int lastStepFrame = -10;
        Color alertColor;
        Vector3? handTargetR, handTargetL;
        Quaternion holdRot = Quaternion.identity;
        float pelvisBaseY;
        List<Vector3> followPath;
        int followIndex;
        readonly List<GameObject> puddles = new List<GameObject>();
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake() => Instance = this;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            pelvisBaseY = pelvis != null ? pelvis.localPosition.y : 0.86f;
            LabContainer.Spilled += OnSpill;
        }

        void OnDestroy() => LabContainer.Spilled -= OnSpill;

        public void Say(string text) => Spoke?.Invoke(text);

        // ==================================================================
        // Thân: đi, IK tay, dáng chân
        // ==================================================================

        void Update()
        {
            if (task == null) Follow();
            else if (lastStepFrame < Time.frameCount - 1) speedNow = Mathf.MoveTowards(speedNow, 0, Time.deltaTime * 3f);  // đứng làm việc: dừng chân
            Eyes();
        }

        void LateUpdate()
        {
            Pose();
            if (Holding != null && carryPoint != null)
            {
                var t = Holding.transform;
                t.rotation = holdRot;
                t.position = carryPoint.position - holdRot * (Vector3.up * Holding.gripHeightM);
            }
        }

        void Follow()
        {
            if (viewerCamera == null) return;
            Vector3 user = viewerCamera.position;
            Vector3 f = viewerCamera.forward; f.y = 0; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 want = new Vector3(user.x, 0, user.z) + Vector3.Cross(Vector3.up, f) * 0.9f + f * 0.4f;
            float d = Flat(want - transform.position).magnitude;
            followCheck -= Time.deltaTime;
            if (d > 1.8f && followCheck <= 0)
            {
                followCheck = 1.2f;
                followPath = NavGrid.Path(transform.position, want);
                followIndex = 1;
                Khkt.Rendering.ZoneStreamer.Instance?.KeepPath(this, followPath, 6f);  // tải trước các khu trên đường đi theo
            }
            if (d > 1.0f && followPath != null && followIndex < followPath.Count)
            {
                if (StepAlong(followPath, ref followIndex, d > 5f ? runSpeed : walkSpeed)) followPath = null;
            }
            else
            {
                speedNow = Mathf.MoveTowards(speedNow, 0, Time.deltaTime * 3f);
                Face(user, 3f);
            }
        }

        /// <summary>Đi một bước dọc đường. Trả true khi tới điểm cuối.</summary>
        bool StepAlong(List<Vector3> path, ref int i, float speed)
        {
            if (i >= path.Count) return true;
            lastStepFrame = Time.frameCount;
            Vector3 p = transform.position;
            Vector3 target = new Vector3(path[i].x, p.y, path[i].z);
            if (Khkt.Rendering.AutoDoor.Blocks(p, target))  // cửa trước mặt chưa mở (khu bên kia đang tải): đứng chờ
            {
                speedNow = Mathf.MoveTowards(speedNow, 0, Time.deltaTime * 4f);
                Face(target, 8f);
                return false;
            }
            Vector3 d = target - p;
            float dist = d.magnitude;
            speedNow = Mathf.MoveTowards(speedNow, Mathf.Min(speed, 0.5f + dist * 2f), Time.deltaTime * 3f);
            float stepLen = speedNow * Time.deltaTime;
            if (dist <= stepLen + 0.02f)
            {
                transform.position = target;
                i++;
                return i >= path.Count;
            }
            transform.position = p + d / dist * stepLen;
            Face(target, 8f);
            return false;
        }

        void Face(Vector3 point, float rate)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void Pose()
        {
            if (pelvis == null) return;
            phase += speedNow * Time.deltaTime / 0.65f * Mathf.PI;
            float s = speedNow > 0.05f ? Mathf.Sin(phase) : 0;
            float amp = Mathf.Clamp01(speedNow / walkSpeed);
            pelvis.localPosition = new Vector3(0, pelvisBaseY + 0.02f * Mathf.Abs(Mathf.Cos(phase)) * amp, 0);
            float swing = 28f * s * amp;
            hipL.localRotation = Quaternion.Euler(-swing, 0, 0);
            hipR.localRotation = Quaternion.Euler(swing, 0, 0);
            kneeL.localRotation = Quaternion.Euler(40f * Mathf.Max(0, Mathf.Sin(phase + 1.2f)) * amp, 0, 0);
            kneeR.localRotation = Quaternion.Euler(40f * Mathf.Max(0, Mathf.Sin(phase + 1.2f + Mathf.PI)) * amp, 0, 0);
            // cúi người khi với xuống thấp / ra xa
            float wantLean = 0;
            if (handTargetR.HasValue)
            {
                Vector3 local = transform.InverseTransformPoint(handTargetR.Value);
                wantLean = Mathf.Clamp((local.z - 0.3f) * 40f + (1.15f - local.y) * 35f, 0, 28f);
            }
            lean = Mathf.MoveTowards(lean, wantLean, 60f * Time.deltaTime);
            torso.localRotation = Quaternion.Euler(lean, 0, 0);
            if (head != null && viewerCamera != null && task == null)
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(Flat(viewerCamera.position - head.position).normalized + Vector3.up * 0.1f), 0.1f);
            else if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(15, 0, 0), 0.1f);

            float armSwing = 22f * s * amp;
            Arm(shoulderR, elbowR, handR, handTargetR ?? Rest(shoulderR, +1, -armSwing), +1);
            Arm(shoulderL, elbowL, handL, handTargetL ?? Rest(shoulderL, -1, armSwing), -1);
        }

        Vector3 Rest(Transform shoulder, int side, float swingDeg)
        {
            if (Holding != null && side > 0) return transform.TransformPoint(new Vector3(0.18f, 1.05f, 0.32f));  // ôm vật trước ngực
            Vector3 dir = Quaternion.AngleAxis(swingDeg, transform.right) * (-transform.up + transform.right * 0.12f * side);
            return shoulder.position + dir.normalized * (upperArm + foreArm - 0.03f);
        }

        /// <summary>IK hai khớp: đặt vai và khuỷu để cổ tay tới target (khuỷu hướng ra ngoài - xuống - sau).</summary>
        void Arm(Transform shoulder, Transform elbow, Transform hand, Vector3 target, int side)
        {
            if (shoulder == null) return;
            Vector3 S = shoulder.position;
            Vector3 toT = target - S;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, upperArm + foreArm - 0.001f);
            Vector3 dir = toT.sqrMagnitude > 1e-6f ? toT.normalized : -transform.up;
            float cosA = Mathf.Clamp((upperArm * upperArm + d * d - foreArm * foreArm) / (2 * upperArm * d), -1, 1);
            float sinA = Mathf.Sqrt(1 - cosA * cosA);
            Vector3 pole = transform.right * side * 0.6f - transform.forward * 0.5f - transform.up * 0.6f;
            Vector3 ortho = Vector3.ProjectOnPlane(pole, dir);
            ortho = ortho.sqrMagnitude > 1e-6f ? ortho.normalized : Vector3.Cross(dir, transform.right).normalized;
            Vector3 E = S + dir * upperArm * cosA + ortho * upperArm * sinA;
            Vector3 W = S + dir * d;
            SetDown(shoulder, (E - S).normalized);
            SetDown(elbow, (W - E).normalized);
            if (hand != null) hand.rotation = elbow.rotation;
        }

        void SetDown(Transform t, Vector3 v)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, v);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(transform.up, v);
            t.rotation = Quaternion.LookRotation(fwd.normalized, -v);
        }

        void Eyes()
        {
            Color eye = Time.time < alertUntil ? alertColor : Busy ? new Color(0.4f, 1f, 0.7f) : new Color(0.35f, 0.9f, 1f);
            float glow = 1.5f + 0.5f * Mathf.Sin(Time.time * (Time.time < alertUntil ? 10f : 2f));
            if (eyes != null)
                foreach (var e in eyes)
                {
                    if (e == null) continue;
                    e.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, eye);
                    mpb.SetColor(EmissionId, eye * glow);
                    e.SetPropertyBlock(mpb);
                }
            if (ring != null)
            {
                ring.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, eye * 0.8f);
                ring.SetPropertyBlock(mpb);
            }
        }

        void Alert(Color c, float seconds)
        {
            alertColor = c;
            alertUntil = Time.time + seconds;
        }

        // ==================================================================
        // Kỹ năng cơ bản (coroutine, đặt ok = false khi thất bại)
        // ==================================================================

        class Res { public bool ok = true; public string why = ""; }

        IEnumerator Fail(Res r, string why)
        {
            r.ok = false;
            r.why = why;
            yield break;
        }

        /// <summary>Đi tới chỗ đứng cách điểm p khoảng 0,45-0,9 m (ô trống gần robot nhất), quay mặt về p.</summary>
        IEnumerator WalkNear(Vector3 p, Res r, float maxR = 0.9f)
        {
            if (!NavGrid.Ready) NavGrid.Build();
            Vector3 me = transform.position;
            if (Flat(p - me).magnitude <= maxR && Flat(p - me).magnitude >= 0.3f) { yield return Turn(p); yield break; }
            // Khu đích phải đang tải để tìm chỗ đứng trống (tủ, bàn ở khu đã gỡ không còn va chạm)
            var zones = Khkt.Rendering.ZoneStreamer.Instance;
            if (zones != null)
            {
                zones.Keep(this, 30f, me, p);
                yield return Khkt.Rendering.ZoneStreamer.WaitReadyAt(p);
            }
            Vector3 best = Vector3.zero;
            float bestCost = float.MaxValue;
            foreach (float rad in new[] { 0.45f, 0.55f, 0.7f, maxR })
            {
                for (int a = 0; a < 24; a++)
                {
                    float ang = a * Mathf.PI * 2 / 24;
                    Vector3 c = new Vector3(p.x + Mathf.Cos(ang) * rad, 0, p.z + Mathf.Sin(ang) * rad);
                    if (!NavGrid.Walkable(c) || !NavGrid.Free(c, 0.18f)) continue;
                    float cost = Flat(c - me).magnitude + rad * 2;
                    if (cost < bestCost) { bestCost = cost; best = c; }
                }
                if (bestCost < float.MaxValue) break;
            }
            if (bestCost == float.MaxValue) { r.ok = false; r.why = "không tìm được chỗ đứng gần đó"; yield break; }
            var path = NavGrid.Path(me, best);
            if (path == null) { r.ok = false; r.why = "không tìm được đường đi"; yield break; }
            zones?.KeepPath(this, path);
            int i = 1;
            float total = 0;
            for (int k = 1; k < path.Count; k++) total += Vector3.Distance(path[k - 1], path[k]);
            float speed = total > 6f ? runSpeed : walkSpeed;
            float guard = 0;
            while (!StepAlong(path, ref i, speed) && guard < 60f) { guard += Time.deltaTime; yield return null; }
            yield return Turn(p);
        }

        IEnumerator Turn(Vector3 p)
        {
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { Face(p, 10f); yield return null; }
        }

        /// <summary>Đưa cổ tay phải tới điểm trong khoảng thời gian (nội suy mượt).</summary>
        IEnumerator Reach(Vector3 point, float seconds = 0.5f)
        {
            Vector3 from = handR != null ? handR.position : point;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                handTargetR = Vector3.Lerp(from, point, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            handTargetR = point;
        }

        IEnumerator Retract()
        {
            if (!handTargetR.HasValue) yield break;
            Vector3 from = handTargetR.Value;
            Vector3 rest = Rest(shoulderR, 1, 0);
            for (float t = 0; t < 0.4f; t += Time.deltaTime)
            {
                handTargetR = Vector3.Lerp(from, rest, t / 0.4f);
                yield return null;
            }
            handTargetR = null;
        }

        bool InReach(Vector3 p) => shoulderR != null && Vector3.Distance(shoulderR.position, p) <= upperArm + foreArm + 0.18f;

        IEnumerator Grab(Grabbable g, Res r)
        {
            if (g == null) { r.ok = false; r.why = "không thấy vật cần cầm"; yield break; }
            if (Holding == g) yield break;
            if (g.IsHeld) { r.ok = false; r.why = "vật đang được người dùng cầm"; yield break; }
            if (Holding != null) { yield return Release(r); if (!r.ok) yield break; }
            yield return WalkNear(g.transform.position, r);
            if (!r.ok) yield break;
            Vector3 grip = g.transform.position + g.transform.up * g.gripHeightM;
            yield return Reach(grip);
            if (!InReach(grip)) { r.ok = false; r.why = "không với tới"; yield return Retract(); yield break; }
            if (!borrowed.ContainsKey(g)) borrowed[g] = SpotOf(g);  // nhớ chỗ cũ để dùng xong đặt lại
            g.SetSocket(null);
            g.SetHeld(true);
            Holding = g;
            holdRot = Quaternion.Euler(0, transform.eulerAngles.y + 180f, 0);
            yield return Retract();
        }

        IEnumerator PlaceAt(Vector3 spot, PlaceSocket socket, Res r)
        {
            if (Holding == null) { r.ok = false; r.why = "robot không cầm gì"; yield break; }
            yield return WalkNear(spot, r);
            if (!r.ok) yield break;
            yield return Reach(spot + Vector3.up * Holding.gripHeightM);
            var g = Holding;
            Holding = null;
            borrowed.Remove(g);  // đã đặt có chủ đích: chỗ mới là chỗ của nó
            g.SetHeld(false);
            if (socket != null && socket.Accepts(g)) socket.Put(g);
            else if (g.liesOnTable)  // buret, pipet, nhiệt kế: đặt nằm ngang trên bàn
            {
                Vector3 d = transform.right;
                g.transform.SetPositionAndRotation(spot + Vector3.up * g.lyingRadiusM - d * g.lengthM / 2, Quaternion.FromToRotation(Vector3.up, d));
            }
            else g.transform.SetPositionAndRotation(spot + Vector3.up * 0.001f, Quaternion.Euler(0, transform.eulerAngles.y + 180f, 0));
            yield return Retract();
        }

        IEnumerator PutDownNearby(Res r)
        {
            Vector3 spot = SurfaceSpotNear(transform.position + transform.forward * 0.5f, 1.2f);
            yield return PlaceAt(spot, null, r);
        }

        /// <summary>Chỗ trống trên mặt bàn gần điểm p.</summary>
        static Vector3 SurfaceSpotNear(Vector3 p, float radius)
        {
            for (float rr = 0; rr <= radius; rr += 0.12f)
                for (int a = 0; a < (rr == 0 ? 1 : 12); a++)
                {
                    float ang = a * Mathf.PI / 6;
                    Vector3 from = new Vector3(p.x + Mathf.Cos(ang) * rr, 1.6f, p.z + Mathf.Sin(ang) * rr);
                    if (!Physics.Raycast(from, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponentInParent<PlaceSurface>() == null) continue;
                    if (Physics.CheckSphere(hit.point + Vector3.up * 0.08f, 0.055f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return hit.point;
                }
            return new Vector3(p.x, 0.8f, p.z);
        }

        /// <summary>Chỗ trống trên mặt bàn trước mặt người dùng.</summary>
        Vector3 FreeSpotNearUser()
        {
            Vector3 c = viewerCamera.position;
            Vector3 f = viewerCamera.forward; f.y = 0; f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            foreach (float dz in new[] { 0.15f, -0.15f, 0f, 0.3f, -0.3f, 0.45f, -0.45f })
                foreach (float df in new[] { 0.5f, 0.4f, 0.65f })
                {
                    Vector3 from = c + f * df + r * dz + Vector3.up * 0.3f;
                    if (!Physics.Raycast(from, Vector3.down, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponentInParent<PlaceSurface>() == null) continue;
                    if (Physics.CheckSphere(hit.point + Vector3.up * 0.08f, 0.055f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return hit.point;
                }
            return SurfaceSpotNear(c + f * 0.6f, 1.5f);
        }

        Vector3 MouthOf(LabContainer c) => c.mouth != null ? c.mouth.position : c.transform.position + Vector3.up * 0.1f;

        IEnumerator Pour(LabContainer from, LabContainer to, float ml, Res r)
        {
            var g = from.GetComponent<Grabbable>();
            yield return Grab(g, r);
            if (!r.ok) yield break;
            yield return WalkNear(to.transform.position, r);
            if (!r.ok) yield break;
            Vector3 mouthLocal = Holding.transform.InverseTransformPoint(MouthOf(from));
            Vector3 axis = transform.right;
            float tilt = 0, moved = 0, guard = 0;
            Quaternion upright = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            while (guard < 25f)
            {
                guard += Time.deltaTime;
                bool pouring = moved < ml - 0.01f && from.VolumeMl > 0.01f && to.FreeMl > 0.05f;
                tilt = Mathf.MoveTowards(tilt, pouring ? 100f : 0f, 120f * Time.deltaTime);
                holdRot = Quaternion.AngleAxis(-tilt, axis) * upright;
                Vector3 want = MouthOf(to) + Vector3.up * (0.04f + 0.03f * (1 - tilt / 100f)) - transform.forward * 0.01f;
                Vector3 objPos = want - holdRot * mouthLocal;
                handTargetR = objPos + holdRot * (Vector3.up * Holding.gripHeightM);
                if (pouring && tilt >= 75f)
                {
                    float d = Mathf.Min(5f * Time.deltaTime, ml - moved);
                    moved += d;
                    to.Add(from.Take(d));
                }
                if (!pouring && tilt <= 0.5f) break;
                yield return null;
            }
            holdRot = Quaternion.Euler(0, transform.eulerAngles.y + 180f, 0);
            yield return Retract();
            if (moved < 0.05f) { r.ok = false; r.why = from.VolumeMl < 0.05f ? "chai đã hết" : "bình nhận đã đầy"; }
        }

        IEnumerator HoldOver(LabContainer to, bool invert, Res r)
        {
            yield return WalkNear(to.transform.position, r);
            if (!r.ok) yield break;
            holdRot = invert ? Quaternion.Euler(180f, transform.eulerAngles.y, 0) : Quaternion.AngleAxis(-60f, transform.right) * Quaternion.Euler(0, transform.eulerAngles.y, 0);
            yield return Reach(MouthOf(to) + Vector3.up * (invert ? 0.03f : 0.12f) + holdRot * (Vector3.up * Holding.gripHeightM) * (invert ? 1 : 0.4f), 0.5f);
        }

        IEnumerator DropInto(Dropper d, LabContainer to, int n, Res r)
        {
            yield return Grab(d.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return HoldOver(to, true, r);
            if (!r.ok) yield break;
            for (int i = 0; i < n; i++) { d.DropInto(to); yield return new WaitForSeconds(0.4f); }
            holdRot = Quaternion.Euler(0, transform.eulerAngles.y + 180f, 0);
            yield return Retract();
        }

        IEnumerator ScoopInto(ChemicalBottle cb, LabContainer to, int n, Res r)
        {
            yield return Grab(cb.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return HoldOver(to, false, r);
            if (!r.ok) yield break;
            for (int i = 0; i < n; i++) { cb.ScoopInto(to); yield return new WaitForSeconds(0.5f); }
            holdRot = Quaternion.Euler(0, transform.eulerAngles.y + 180f, 0);
            yield return Retract();
        }

        IEnumerator Point(Vector3 p, Res r)
        {
            if (Flat(p - transform.position).magnitude > 3f) { yield return WalkNear(p, r, 1.6f); if (!r.ok) yield break; }
            yield return Turn(p);
            Vector3 dir = (p - shoulderR.position).normalized;
            yield return Reach(shoulderR.position + dir * (upperArm + foreArm - 0.02f), 0.4f);
            yield return new WaitForSeconds(2f);
            yield return Retract();
        }

        IEnumerator Use(Component c, Res r)
        {
            yield return WalkNear(c.transform.position, r);
            if (!r.ok) yield break;
            yield return Reach(c.transform.position + Vector3.up * 0.05f, 0.4f);
            if (c is IDemoUsable u) u.Use();
            else if (c.GetComponent<IDemoUsable>() is IDemoUsable u2) u2.Use();
            yield return Retract();
        }

        IEnumerator ReturnHome(Grabbable g, Res r)
        {
            yield return Grab(g, r);
            if (!r.ok) yield break;
            if (g.HomeSocket != null && g.HomeSocket.Occupant == null) yield return PlaceAt(g.HomeSocket.transform.position, g.HomeSocket, r);
            else yield return PlaceAt(g.HomePosition, null, r);
        }

        IEnumerator Fetch(int stt, Res r)
        {
            var search = ChemSearchUI.Instance;
            var b = search != null ? search.FindBottle(stt) : null;
            if (b == null) { r.ok = false; r.why = "không tìm thấy lọ trong kho"; yield break; }
            search.Mark(b.Entry, false);
            yield return Grab(b.GetComponent<Grabbable>(), r);
            if (!r.ok) yield break;
            yield return PlaceAt(FreeSpotNearUser(), null, r);
        }

        // ==================================================================
        // Thực hiện kế hoạch (từ AI hoặc lệnh tại chỗ)
        // ==================================================================

        public void ExecutePlan(List<RobotStep> plan)
        {
            if (plan == null || plan.Count == 0) return;
            Run(PlanRoutine(plan), "Đang làm theo kế hoạch");
        }

        IEnumerator PlanRoutine(List<RobotStep> plan)
        {
            placeholders.Clear();
            for (int i = 0; i < plan.Count; i++)
            {
                var s = plan[i];
                var r = new Res();
                StatusText = $"Bước {i + 1}/{plan.Count}: {s.ky_nang}";
                yield return Step(s, r);
                if (!r.ok)
                {
                    Say($"Mình dừng ở bước {i + 1} ({s.ky_nang}): {r.why}.");
                    yield break;
                }
                yield return new WaitForSeconds(0.2f);
            }
            // xong kế hoạch mà còn cầm lọ hóa chất, máy đo, pipet... thì cất đúng chỗ cũ, trừ khi bước cuối là "cầm"
            if (Holding != null && plan[plan.Count - 1].ky_nang != "cam")
            {
                StatusText = "Cất dụng cụ";
                yield return Release(new Res());
            }
        }

        IEnumerator Step(RobotStep s, Res r)
        {
            var obj = Find(s.doi_tuong);
            var dest = Find(s.dich);
            int n = Mathf.Max(1, Mathf.RoundToInt(s.so_luong));
            switch (s.ky_nang)
            {
                case "noi":
                    Say(s.loi);
                    yield return new WaitForSeconds(Mathf.Clamp(s.loi.Length * 0.04f, 1f, 4f));
                    break;
                case "di_toi":
                    if (obj == null) { r.ok = false; r.why = "không thấy " + s.doi_tuong; break; }
                    yield return WalkNear(obj.transform.position, r);
                    break;
                case "chi_vao":
                    if (obj == null) { r.ok = false; r.why = "không thấy " + s.doi_tuong; break; }
                    yield return Point(obj.transform.position + Vector3.up * 0.08f, r);
                    break;
                case "cam":
                    yield return Grab(obj != null ? obj.GetComponentInParent<Grabbable>() : null, r);
                    break;
                case "dat":
                {
                    if (Holding == null) { r.ok = false; r.why = "robot không cầm gì"; break; }
                    var socket = dest as PlaceSocket;
                    Vector3 spot = socket != null ? socket.transform.position
                                 : dest != null ? SurfaceSpotNear(dest.transform.position + transform.right * 0.14f, 0.5f)
                                 : FreeSpotNearUser();
                    yield return PlaceAt(spot, socket, r);
                    break;
                }
                case "rot":
                {
                    var from = obj != null ? obj.GetComponentInParent<LabContainer>() : null;
                    var to = dest != null ? dest.GetComponentInParent<LabContainer>() : null;
                    if (from == null || to == null) { r.ok = false; r.why = "thiếu chai rót hoặc bình nhận"; break; }
                    yield return Pour(from, to, Mathf.Max(0.5f, s.so_luong), r);
                    break;
                }
                case "nho_giot":
                {
                    var d = obj != null ? obj.GetComponentInParent<Dropper>() : null;
                    var to = dest != null ? dest.GetComponentInParent<LabContainer>() : null;
                    if (d == null || to == null)
                    {
                        // chai thường: rót rất ít thay cho nhỏ giọt
                        var from = obj != null ? obj.GetComponentInParent<LabContainer>() : null;
                        if (from == null || to == null) { r.ok = false; r.why = "thiếu lọ nhỏ giọt hoặc bình nhận"; break; }
                        yield return Pour(from, to, n * Solution.DropMl, r);
                        break;
                    }
                    yield return DropInto(d, to, n, r);
                    break;
                }
                case "xuc":
                {
                    var cb = obj != null ? obj.GetComponentInParent<ChemicalBottle>() : null;
                    var to = dest != null ? dest.GetComponentInParent<LabContainer>() : null;
                    if (cb == null || !cb.IsSolid || to == null) { r.ok = false; r.why = "thiếu lọ chất rắn hoặc bình nhận"; break; }
                    yield return ScoopInto(cb, to, n, r);
                    break;
                }
                case "lay_hoa_chat":
                    yield return Fetch(s.stt, r);
                    if (r.ok) RobotWorld.Ids["hoa_chat:" + s.stt] = Holding != null ? (Component)Holding : RobotWorld.Resolve("hoa_chat:" + s.stt);
                    break;
                case "tra_ve":
                    yield return ReturnHome(obj != null ? obj.GetComponentInParent<Grabbable>() : null, r);
                    break;
                case "bat_tat":
                    if (obj == null) { r.ok = false; r.why = "không thấy thiết bị"; break; }
                    yield return Use(obj, r);
                    break;
                case "bat_tu_hut": Ventilate(); break;
                case "tat_lua": Extinguish(); break;
                case "don_ban": yield return CleanRoutine(); break;
                case "rua_mat": yield return EyewashRoutine(); break;
                default: yield return StepMore(s, r, obj, dest); break;  // kỹ năng thí nghiệm: LabRobot.Skills.cs
            }
        }

        void Run(IEnumerator routine, string status)
        {
            if (task != null) StopCoroutine(task);
            handTargetR = null;
            StatusText = status;
            task = StartCoroutine(Wrap(routine));
        }

        IEnumerator Wrap(IEnumerator r)
        {
            yield return r;
            task = null;
            StatusText = "Sẵn sàng";
            handTargetR = null;
        }

        // ==================================================================
        // Lệnh nhanh (khung trò chuyện, tra cứu, xử lý sự cố)
        // ==================================================================

        public void LocateChemical(int stt)
        {
            var search = ChemSearchUI.Instance;
            var e = ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(stt) : null;
            var b = search != null ? search.FindBottle(stt) : null;
            if (e == null || b == null) { Say("Mình không tìm thấy hóa chất đó trong kho."); return; }
            search.Mark(e, false);
            Say($"{e.ten} ở {ChemicalCatalog.Instance.LocationText(e)}. Mình đi tới chỉ cho bạn (chỗ có cột sáng màu cam).");
            Run(LocateRoutine(b), "Đang chỉ vị trí " + e.ten);
        }

        IEnumerator LocateRoutine(ChemicalBottle b)
        {
            var r = new Res();
            yield return Point(b.transform.position, r);
            if (!r.ok) Say("Mình không tới được kệ: " + r.why);
        }

        public void BringChemical(int stt)
        {
            var e = ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(stt) : null;
            if (e == null) { Say("Mình không tìm thấy hóa chất đó trong kho."); return; }
            Say($"Mình đi lấy {e.ten} ({ChemicalCatalog.Instance.LocationText(e)}) mang ra bàn cho bạn.");
            Run(BringRoutine(stt, e), "Đang mang " + e.ten);
        }

        IEnumerator BringRoutine(int stt, ChemEntry e)
        {
            var r = new Res();
            yield return Fetch(stt, r);
            Say(r.ok ? $"Đã đặt {e.ten} lên bàn trước mặt bạn. Dùng xong nhớ trả về {e.ma_vi_tri} nhé." : $"Mình chưa lấy được {e.ten}: {r.why}.");
        }

        public void CleanBench()
        {
            Say("Mình dọn bàn: đổ dung dịch thừa vào thùng chất thải, trả dụng cụ về chỗ, lau chỗ tràn.");
            Run(CleanRoutine(), "Đang dọn bàn");
        }

        IEnumerator CleanRoutine()
        {
            var hand = FindAnyObjectByType<HandController>();
            if (hand != null && hand.Held != null) hand.ReturnHome();
            Vector3 c = viewerCamera.position;
            var todo = new List<Grabbable>();
            foreach (var g in Khkt.Rendering.ZoneRoot.FindAll<Grabbable>())
            {
                if (g.IsHeld || Vector3.Distance(g.transform.position, c) > 3.2f) continue;
                var lc = g.GetComponent<LabContainer>();
                if (lc != null && lc.reactive && lc.VolumeMl > 0 && g.GetComponent<ChemicalBottle>() == null) lc.SetContents(default);
                if ((g.transform.position - g.HomePosition).sqrMagnitude > 0.0025f) todo.Add(g);
            }
            todo.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
            for (int i = 0; i < todo.Count && i < 8; i++)
            {
                var r = new Res();
                yield return ReturnHome(todo[i], r);
            }
            ClearSpills();
            Say("Bàn đã sạch.");
        }

        public void Ventilate() => Ventilate(false);

        void Ventilate(bool quiet)
        {
            int n = 0;
            foreach (var t in Khkt.Rendering.ZoneRoot.FindAll<ToggleEffect>())
            {
                bool fan = (t.fans != null && t.fans.Length > 0) || t.name == "Công tắc tủ hút";
                if (fan && !t.isOn) { t.Use(); n++; }
            }
            if (n > 0) Say("Đã bật quạt hút và quạt tủ hút. Thí nghiệm sinh khí độc nên làm trong tủ hút (phím 5).");
            else if (!quiet) Say("Quạt hút và tủ hút đều đang chạy.");
        }

        public void Extinguish()
        {
            int n = 0;
            foreach (var h in Khkt.Rendering.ZoneRoot.FindAll<HeatSource>())
                if (h.toggle != null && h.toggle.isOn) { h.toggle.Use(); n++; }
            Say(n > 0 ? $"Đã tắt {n} nguồn lửa / bếp. Đèn cồn phải tắt bằng nắp, không thổi." : "Không có ngọn lửa nào đang cháy.");
        }

        public void GuideEyewash()
        {
            Say("Hóa chất bắn vào mắt: tới ngay vòi rửa mắt cạnh cửa ra vào, giữ mắt mở rửa ít nhất 15 phút rồi báo giáo viên.");
            Run(EyewashRoutine(), "Đang dẫn tới vòi rửa mắt");
        }

        IEnumerator EyewashRoutine()
        {
            ToggleEffect eye = null;
            foreach (var t in Khkt.Rendering.ZoneRoot.FindAll<ToggleEffect>()) if (t.name == "Vòi rửa mắt") eye = t;
            if (eye == null) yield break;
            var r = new Res();
            yield return Use(eye, r);
            yield return new WaitForSeconds(3f);
        }

        public void HandleIncident(LabContainer c, ChemEvent e)
        {
            if (e.Severity < Severity.Warning) return;
            Alert(e.Severity == Severity.Danger ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.75f, 0.3f), 8f);
            switch (e.Hazard)
            {
                case "khi_doc":
                    if (c != null && c.InFumeHood) return;
                    Say($"Cảnh báo: {Lower(e.Title)}. Mình bật quạt hút. Hãy đưa thí nghiệm vào tủ hút, đứng xa, không ngửi trực tiếp.");
                    Ventilate(true);
                    break;
                case "chay_no":
                    Say($"Nguy hiểm: {Lower(e.Title)}. Đứng xa, không dùng nước nếu là kim loại kiềm cháy: dập bằng cát khô.");
                    Extinguish();
                    break;
                case "ban_be":
                    Say("Dừng lại! Dung dịch sôi bắn ra. Pha loãng axit đặc: rót từ từ axit vào nước, khuấy đều.");
                    break;
                case "bong":
                    Say("Dụng cụ đang rất nóng: dùng kẹp, không cầm tay trực tiếp.");
                    break;
                default:
                    Say("Chú ý: " + Lower(e.Title));
                    break;
            }
            if (c != null && task == null) Run(PointRoutine(c.transform.position), "Đang xử lý tình huống");
        }

        IEnumerator PointRoutine(Vector3 p)
        {
            var r = new Res();
            yield return Point(p, r);
        }

        static string Lower(string s) => string.IsNullOrEmpty(s) ? "" : char.ToLower(s[0]) + s.Substring(1);

        void OnSpill(LabContainer c, Solution s)
        {
            if (s.volumeMl < 0.5f) return;
            if (!Physics.Raycast(c.transform.position + Vector3.up * 0.05f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) return;
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "Vệt hóa chất tràn";
            Destroy(p.GetComponent<Collider>());
            float rad = Mathf.Clamp(0.03f + 0.012f * Mathf.Sqrt(s.volumeMl), 0.04f, 0.25f);
            p.transform.position = hit.point + Vector3.up * 0.0015f;
            p.transform.localScale = new Vector3(rad * 2, 0.0008f, rad * 1.7f);
            var rend = p.GetComponent<Renderer>();
            if (puddleMaterial != null) rend.sharedMaterial = puddleMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puddles.Add(p);
            double ph = s.Ph;
            if (s.volumeMl > 0.01f && (ph < 2 || ph > 12))
            {
                Alert(new Color(1f, 0.75f, 0.3f), 6f);
                Say(ph < 2 ? "Tràn dung dịch axit! Mình trung hòa bằng NaHCO₃ rồi lau sạch. Không chạm tay vào." : "Tràn dung dịch kiềm! Mình lau bằng nhiều nước. Không chạm tay vào.");
                Run(SpillRoutine(p), "Đang lau hóa chất tràn");
            }
            else LabMessages.Say("Có chất lỏng tràn ra bàn (nhờ robot \"dọn bàn\", phím T)", LabMessages.Tone.Warn);
        }

        IEnumerator SpillRoutine(GameObject p)
        {
            var r = new Res();
            yield return WalkNear(p.transform.position, r);
            yield return Reach(p.transform.position + Vector3.up * 0.04f, 0.5f);
            yield return new WaitForSeconds(1.2f);
            puddles.Remove(p);
            Destroy(p);
            yield return Retract();
            Say("Đã xử lý xong chỗ tràn.");
        }

        public void ClearSpills()
        {
            foreach (var p in puddles) if (p != null) Destroy(p);
            puddles.Clear();
        }
    }
}
