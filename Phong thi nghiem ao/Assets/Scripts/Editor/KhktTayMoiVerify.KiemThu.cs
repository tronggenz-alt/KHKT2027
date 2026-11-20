using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.Avatar;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Input;
using Khkt.Interaction;
using Khkt.Rendering;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>Các kiểm thử tay + cánh tay mới (gọi bởi KhktTayMoiVerify.Run).</summary>
    public static partial class KhktTayMoiVerify
    {
        static IEnumerator CoRun()
        {
            Application.logMessageReceived += OnUnityLog;
            yield return WaitSceneReady();

            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            var hand = HandController.Active != null ? HandController.Active : UnityEngine.Object.FindAnyObjectByType<HandController>();
            var arm = UnityEngine.Object.FindAnyObjectByType<ArmRig>();
            Camera cam = hand != null && hand.viewCamera != null ? hand.viewCamera : Camera.main;
            Check(viewer != null && hand != null && hand.hand != null && arm != null && cam != null, "Cảnh có DesktopViewer, HandController, HandRig, ArmRig, Camera");
            if (viewer == null || hand == null || hand.hand == null || arm == null || cam == null) { FinishVerify(true); yield break; }

            HandRig rig = hand.hand;
            HandRig.WearGloves = true;
            HardwareProfile.Dat(MucDoHoa.Cao);
            float tm = Time.realtimeSinceStartup;
            while ((!rig.MeshReady || rig.MeshPending || arm.LeftHand == null || !arm.LeftHand.MeshReady) && Time.realtimeSinceStartup - tm < 40f) yield return null;
            Check(rig.MeshReady && arm.LeftHand != null && arm.LeftHand.MeshReady, "Lưới hai bàn tay dựng xong trên luồng nền (không chặn khung hình)",
                $"chờ thêm sau khi cảnh sẵn sàng {Time.realtimeSinceStartup - tm:0.0} s");
            yield return WaitFrames(4);
            var tempObjects = new List<GameObject>();
            Vector3 basePos = viewer.transform.position;
            float baseYaw = viewer.transform.eulerAngles.y;

            try
            {
                // ---------------- 1. Cấu trúc lưới ----------------
                var smr = rig.SkinRenderer;
                Mesh mesh = smr != null ? smr.sharedMesh : null;
                Check(mesh != null && smr.bones.Length == TayNguoiLuoi.HandBoneCount,
                    "Bàn tay là một lưới da liền khối có skinning 17 xương (5 ngón, ngón cái 3 khớp, cổ tay)",
                    $"đỉnh={rig.MeshVertexCount} tam giác={(mesh != null ? mesh.triangles.Length / 3 : 0)} dựng={rig.LastMeshBuildMs:0} ms");
                Check(rig.MeshVertexCount >= 6000, "Lưới mức Cao đủ mịn (≥ 6000 đỉnh)", $"đỉnh={rig.MeshVertexCount}");
                int nails = 0;
                foreach (var mf in rig.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name.StartsWith("Móng") && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 20) nails++;
                Check(nails == 5 && rig.FingerNailsCount == 5, "5 móng tay cong bám mặt mu đốt xa", $"móng={nails}");
                Check(rig.ForearmBone != null && rig.WristAttach != null, "Có xương cổ tay ảo + miệng cổ tay áo");
                var s0 = arm.SleeveRenderer(0);
                var s1 = arm.SleeveRenderer(1);
                Check(s0 != null && s1 != null && s0.bones.Length == 5 && s0.sharedMesh != null && s0.sharedMesh.blendShapeCount >= 1,
                    "Hai ống tay áo blouse skinning 5 xương + blendshape gập khuỷu",
                    s0 != null && s0.sharedMesh != null ? $"đỉnh tay áo={s0.sharedMesh.vertexCount}" : "");
                Check(arm.LeftHand != null && arm.LeftHand.SkinRenderer != null, "Tay trái cũng là lưới skinning");
                Check(Shader.Find("Khkt/TayNguoi") != null && smr.sharedMaterial != null && smr.sharedMaterial.shader.name == "Khkt/TayNguoi",
                    "Vật liệu Khkt/TayNguoi (nạp qua Resources để có trong bản dựng)", smr.sharedMaterial != null ? smr.sharedMaterial.shader.name : "null");

                // ---------------- 2. Chiều mặt tam giác + trọng số ----------------
                if (mesh != null)
                {
                    var v = mesh.vertices; var n = mesh.normals; var t = mesh.triangles; var bw = mesh.boneWeights;
                    int good = 0, tc = t.Length / 3;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        if (Vector3.Dot(fn, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) > 0f) good++;
                    }
                    Check(good >= tc * 0.995f, "Mặt tam giác hướng ra ngoài, khớp pháp tuyến SDF", $"{good}/{tc}");
                    float worst = 0f;
                    foreach (var w in bw) worst = Mathf.Max(worst, Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f));
                    Check(worst < 1e-3f, "Trọng số skinning chuẩn hóa", $"lệch tối đa={worst:0.00000}");

                    // Ngón giữa gập hết cỡ không được kéo da ngón trỏ
                    var baked = new Mesh();
                    HandPoseData open = HandPoseTable.Get(HandPoseId.Xoe);
                    rig.ApplyPoseNow(open);
                    smr.BakeMesh(baked, true);
                    var va = baked.vertices;
                    HandPoseData bent = open;
                    bent.middle = new FingerPose(85f, 0f, 100f, 70f);
                    rig.ApplyPoseNow(bent);
                    smr.BakeMesh(baked, true);
                    var vb = baked.vertices;
                    float idxMove = 0f, midMove = 0f;
                    for (int i = 0; i < bw.Length; i++)
                    {
                        float d = (vb[i] - va[i]).magnitude;
                        int b0 = bw[i].boneIndex0;
                        if (bw[i].weight0 > 0.98f && b0 >= TayNguoiLuoi.BoneFinger(0, 0) && b0 <= TayNguoiLuoi.BoneFinger(0, 2)) idxMove = Mathf.Max(idxMove, d);
                        if (b0 == TayNguoiLuoi.BoneFinger(1, 2)) midMove = Mathf.Max(midMove, d);
                    }
                    UnityEngine.Object.Destroy(baked);
                    Check(idxMove < 0.0001f && midMove > 0.02f, "Gập ngón giữa không kéo da ngón trỏ (trọng số chỉ trộn cha–con quanh khớp)",
                        $"ngón trỏ dịch={idxMove * 1000f:0.000} mm, đầu ngón giữa dịch={midMove * 1000f:0} mm");
                    rig.SetTargetPose(HandPoseId.Xoe, immediate: true);
                }

                // ---------------- 3. Găng / da ----------------
                HandRig.WearGloves = false;
                float modeSkin = smr.sharedMaterial.GetFloat("_Mode");
                HandRig.WearGloves = true;
                float modeGlove = smr.sharedMaterial.GetFloat("_Mode");
                Check(Mathf.Approximately(modeSkin, 0f) && Mathf.Approximately(modeGlove, 1f), "Công tắc găng nitrile ⇄ da hoạt động");

                // ---------------- 4. Mức chi tiết theo HardwareProfile ----------------
                var lodVerts = new int[4];
                var lodMs = new float[4];
                MucDoHoa[] levels = { MucDoHoa.SieuNet, MucDoHoa.Cao, MucDoHoa.Vua, MucDoHoa.Thap };
                for (int i = 0; i < 4; i++)
                {
                    HardwareProfile.Dat(levels[i]);
                    float tl = Time.realtimeSinceStartup;
                    while ((rig.MeshPending || rig.MeshLod != 3 - (int)levels[i]) && Time.realtimeSinceStartup - tl < 40f) yield return null;
                    yield return WaitFrames(2);
                    lodVerts[i] = rig.MeshVertexCount;
                    lodMs[i] = rig.LastMeshBuildMs;
                }
                HardwareProfile.Dat(MucDoHoa.Cao);
                yield return WaitFrames(2);
                rig.WaitMesh();
                Check(lodVerts[0] > lodVerts[1] && lodVerts[1] > lodVerts[2] && lodVerts[2] > lodVerts[3] && rig.MeshLod == 1,
                    "Mức chi tiết lưới theo HardwareProfile (Siêu nét > Cao > Vừa > Thấp)",
                    $"đỉnh={lodVerts[0]}/{lodVerts[1]}/{lodVerts[2]}/{lodVerts[3]} dựng ms={lodMs[0]:0}/{lodMs[1]:0}/{lodMs[2]:0}/{lodMs[3]:0}");

                // ---------------- 5. MediaPipe 21 điểm: khứ hồi chính xác ----------------
                {
                    var go = new GameObject("TAY_TestMediaPipe");
                    go.SetActive(false);
                    go.transform.position = new Vector3(0f, -60f, 0f);
                    var tr = go.AddComponent<HandRig>();
                    tr.side = 0;
                    tr.ownSleeve = false;
                    go.SetActive(true);
                    tr.SkipIdleUpdate = true;
                    tempObjects.Add(go);
                    var rnd = new System.Random(20112026);
                    var world = new Vector3[21];
                    var lmCam = new Vector3[21];
                    var expect = new Quaternion[15];
                    float maxF = 0f, maxT = 0f, maxRoot = 0f;
                    for (int it = 0; it < 60; it++)
                    {
                        var d = new HandPoseData
                        {
                            index = RandFinger(rnd), middle = RandFinger(rnd), ring = RandFinger(rnd), pinky = RandFinger(rnd),
                            thumb = new ThumbPose(R(rnd, -4f, 50f), R(rnd, -30f, 62f), R(rnd, 4f, 60f), R(rnd, 12f, 58f), R(rnd, 8f, 78f))
                        };
                        go.transform.rotation = UnityEngine.Random.rotationUniform;
                        tr.ApplyPoseNow(d);
                        Quaternion rootExp = go.transform.rotation;
                        JointRots(tr, expect);
                        tr.ReadLandmarks(world);
                        Quaternion camRot = UnityEngine.Random.rotationUniform;
                        Quaternion ic = Quaternion.Inverse(camRot);
                        for (int i = 0; i < 21; i++) lmCam[i] = ic * (world[i] - world[0]);
                        tr.ApplyMediaPipeLandmarks(lmCam, camRot);
                        maxRoot = Mathf.Max(maxRoot, Quaternion.Angle(rootExp, go.transform.rotation));
                        var got = new Quaternion[15];
                        JointRots(tr, got);
                        for (int j = 0; j < 12; j++) maxF = Mathf.Max(maxF, Quaternion.Angle(expect[j], got[j]));
                        for (int j = 12; j < 15; j++) maxT = Mathf.Max(maxT, Quaternion.Angle(expect[j], got[j]));
                    }
                    Check(maxRoot < 0.5f && maxF < 1.0f && maxT < 1.5f,
                        "Ánh xạ đủ 21 điểm MediaPipe: khứ hồi 60 tư thế ngẫu nhiên (cổ tay, 12 khớp ngón, CMC/MCP/IP ngón cái)",
                        $"sai số tối đa: gan tay={maxRoot:0.00}° ngón={maxF:0.00}° ngón cái={maxT:0.00}°");

                    // Không cấp phát khi ánh xạ
                    long b0 = GC.GetAllocatedBytesForCurrentThread();
                    for (int it = 0; it < 200; it++) tr.ApplyMediaPipeLandmarks(lmCam, Quaternion.identity);
                    long used = GC.GetAllocatedBytesForCurrentThread() - b0;
                    Check(used == 0, "Ánh xạ MediaPipe không cấp phát bộ nhớ", $"{used} byte / 200 lần");
                }

                // ---------------- 6. Nội suy tư thế độc lập FPS ----------------
                {
                    HandRig A = NewTestRig("TAY_TestA", tempObjects), B = NewTestRig("TAY_TestB", tempObjects);
                    A.SetTargetPose(HandPoseId.Xoe, true); B.SetTargetPose(HandPoseId.Xoe, true);
                    A.SetTargetPose(HandPoseId.ChumCam); B.SetTargetPose(HandPoseId.ChumCam);
                    var qa = new Quaternion[15]; var qb = new Quaternion[15]; var prev = new Quaternion[15];
                    JointRots(B, prev);
                    float maxStep = 0f;
                    for (int i = 0; i < 3; i++) A.StepPoseAndContact(1f / 30f);
                    for (int i = 0; i < 15; i++)
                    {
                        B.StepPoseAndContact(1f / 150f);
                        JointRots(B, qb);
                        for (int j = 0; j < 15; j++) maxStep = Mathf.Max(maxStep, Quaternion.Angle(prev[j], qb[j]));
                        Array.Copy(qb, prev, 15);
                    }
                    JointRots(A, qa); JointRots(B, qb);
                    float mid = MaxAngle(qa, qb);
                    for (int i = 0; i < 2; i++) A.StepPoseAndContact(1f / 30f);
                    for (int i = 0; i < 10; i++) B.StepPoseAndContact(1f / 150f);
                    JointRots(A, qa); JointRots(B, qb);
                    float end = MaxAngle(qa, qb);
                    Check(mid < 0.3f && end < 0.05f && Mathf.Approximately(HandPoseTable.TransitionDurationS, 0.15f),
                        "Ngón tay nội suy mượt 0,15 s cố định, 30 FPS và 150 FPS cho cùng tư thế",
                        $"lệch giữa chừng={mid:0.000}° cuối={end:0.000}° bước lớn nhất@150FPS={maxStep:0.0}°");
                    Check(maxStep < 12f, "Không giật: mỗi khung 150 FPS đổi < 12° ở khớp nào", $"{maxStep:0.0}°");

                    // Lò xo lắc tay: nghiệm đúng → cùng kết quả ở 30 và 150 FPS
                    Vector3 x1 = Vector3.zero, v1 = Vector3.zero, x2 = Vector3.zero, v2 = Vector3.zero;
                    float worst = 0f;
                    for (int i = 0; i < 30; i++)
                    {
                        Vector3 target = i < 10 ? Vector3.one * 0.02f : Vector3.zero;
                        ArmRig.Spring(ref x1, ref v1, target, 11f, 1f / 30f);
                        for (int k = 0; k < 5; k++) ArmRig.Spring(ref x2, ref v2, target, 11f, 1f / 150f);
                        worst = Mathf.Max(worst, (x1 - x2).magnitude);
                    }
                    Check(worst < 1e-5f, "Lò xo lắc/trễ cánh tay không phụ thuộc FPS", $"lệch tối đa={worst * 1000f:0.0000} mm");
                }

                // ---------------- 7. IK hai khớp: giới hạn sinh lý ----------------
                {
                    var rnd = new System.Random(7);
                    float worstLen = 0f, maxFlex = 0f, minFlex = 999f;
                    for (int i = 0; i < 2000; i++)
                    {
                        Vector3 dir = UnityEngine.Random.onUnitSphere;
                        Vector3 target = dir * R(rnd, 0.0f, 0.9f);
                        Vector3 pole = UnityEngine.Random.onUnitSphere;
                        ArmRig.SolveIk(Vector3.zero, target, pole, out Vector3 e, out Vector3 w);
                        worstLen = Mathf.Max(worstLen, Mathf.Abs(e.magnitude - ArmRig.UpperLen), Mathf.Abs((w - e).magnitude - ArmRig.ForeLen));
                        float flex = Vector3.Angle(e, w - e);
                        maxFlex = Mathf.Max(maxFlex, flex); minFlex = Mathf.Min(minFlex, flex);
                    }
                    Check(worstLen < 1e-4f && maxFlex <= ArmRig.ElbowMaxFlexDeg + 0.5f && minFlex >= 0f,
                        "IK vai–khuỷu–cổ tay: giữ chiều dài xương, khuỷu gập 0–150°, không duỗi quá (2000 điểm ngẫu nhiên)",
                        $"lệch dài={worstLen * 1000f:0.000} mm, gập {minFlex:0.0}–{maxFlex:0.0}°");
                }

                // ---------------- 8. Ảnh "sau" + IK dụng cụ + đo cánh tay ----------------
                ArmNotes.Clear();
                armMaxGapMm = armMaxFlex = armMaxDev = 0f; armMinFlex = 999f; armMaxPron = -999f; armMinPron = 999f;
                yield return CoCapture("sau", exitWhenDone: false);
                foreach (var kv in IkResult)
                    Check(kv.Value.pen == 0 && kv.Value.errMm <= 3.0f, $"IK bề mặt {kv.Key}: không xuyên, sai lệch ≤ 3 mm",
                        $"pen={kv.Value.pen} err={kv.Value.errMm:0.00} mm");
                Check(IkResult.Count == 4, "Đủ 4 tư thế cầm dụng cụ (bình nón, ống nghiệm, pipet, khóa buret)", $"{IkResult.Count}");
                Check(armMaxGapMm <= 1.0f, "Ống tay áo khít cổ tay ở mọi tư thế", $"hở tối đa={armMaxGapMm:0.00} mm");
                Check(armMinFlex >= 0f && armMaxFlex <= ArmRig.ElbowMaxFlexDeg && armMinPron >= ArmRig.PronationMinDeg && armMaxPron <= ArmRig.PronationMaxDeg,
                    "Khuỷu và xoay sấp/ngửa trong giới hạn sinh lý ở mọi tư thế",
                    $"khuỷu {armMinFlex:0}–{armMaxFlex:0}°, sấp {armMinPron:0}–{armMaxPron:0}°, cổ tay ≤ {armMaxDev:0}°");

                // ---------------- 9. Lắc khi xoay camera / khi đi ----------------
                {
                    // DesktopViewer đang ở chế độ ngồi (giữ chỗ) → tắt tạm, tự xoay/dời gốc người chơi theo thời gian thật
                    viewer.SetPose(basePos, baseYaw, 18f);
                    hand.OverrideAimPoint = null;
                    yield return WaitFrames(10);
                    viewer.enabled = false;
                    Transform vt = viewer.transform;
                    yield return WaitSeconds(1.2f);
                    float rest = arm.SwayOffset.magnitude;
                    float turnMax = 0f, t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 1.0f)
                    {
                        vt.rotation = Quaternion.Euler(0f, baseYaw + 120f * (Time.realtimeSinceStartup - t0), 0f);
                        yield return null;
                        turnMax = Mathf.Max(turnMax, arm.SwayOffset.magnitude);
                    }
                    yield return WaitSeconds(1.5f);
                    float settle = arm.SwayOffset.magnitude;
                    Check(turnMax > 0.004f && turnMax < 0.06f && settle < 0.003f, "Cánh tay trễ/nghiêng khi xoay camera (120°/s) rồi về lại",
                        $"đứng yên={rest * 1000f:0.0} mm, khi xoay={turnMax * 1000f:0.0} mm, sau 1,5 s={settle * 1000f:0.0} mm");

                    vt.rotation = Quaternion.Euler(0f, baseYaw, 0f);
                    yield return WaitSeconds(0.5f);
                    Vector3 fwd = Quaternion.Euler(0f, baseYaw, 0f) * Vector3.forward;
                    float yMin = 1f, yMax = -1f;
                    Vector3 p0 = vt.position;
                    t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 2.0f)
                    {
                        vt.position = p0 + fwd * (1.3f * (Time.realtimeSinceStartup - t0));
                        yield return null;
                        if (Time.realtimeSinceStartup - t0 > 0.8f)
                        {
                            yMin = Mathf.Min(yMin, arm.SwayOffset.y);
                            yMax = Mathf.Max(yMax, arm.SwayOffset.y);
                        }
                    }
                    viewer.enabled = true;
                    viewer.SetPose(basePos, baseYaw, 18f);
                    Check(yMax - yMin > 0.003f && yMax - yMin < 0.04f, "Cánh tay nhún nhẹ theo bước đi (1,3 m/s)", $"biên độ dọc={(yMax - yMin) * 1000f:0.0} mm");
                    yield return WaitFrames(10);
                }

                // ---------------- 10. Không cấp phát + chi phí CPU mỗi khung ----------------
                {
                    var flask = FindGrabbable("binh_non");
                    if (flask != null)
                    {
                        if (hand.Held != null) hand.ReturnHome();
                        hand.Pick(flask);
                        hand.OverrideAimPoint = Aim(cam, 0.44f, -0.08f, 0.05f);
                        yield return WaitSeconds(0.4f);
                        for (int i = 0; i < 5; i++) arm.Step(1f / 60f);
                        long b0 = GC.GetAllocatedBytesForCurrentThread();
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 200; i++) arm.Step(1f / 60f);
                        sw.Stop();
                        long used = GC.GetAllocatedBytesForCurrentThread() - b0;
                        float msPer = (float)sw.Elapsed.TotalMilliseconds / 200f;
                        Check(used == 0, "Cập nhật hai cánh tay + bàn tay + IK dụng cụ không cấp phát bộ nhớ", $"{used} byte / 200 khung");
                        Check(msPer < 1.5f, "Chi phí CPU tay + cánh tay mỗi khung (đang cầm bình nón, IK bật)", $"{msPer:0.000} ms/khung (Editor)");
                        hand.ReturnHome();
                        hand.OverrideAimPoint = null;
                    }
                    else Check(false, "Tìm thấy bình nón để đo cấp phát");
                }
            }
            finally
            {
                foreach (var go in tempObjects) if (go != null) UnityEngine.Object.Destroy(go);
                viewer.enabled = true;
                HandRig.WearGloves = true;
                HardwareProfile.DatTuDong();
                hand.OverrideAimPoint = null;
                viewer.SetPose(basePos, baseYaw, 10f);
            }

            Application.logMessageReceived -= OnUnityLog;
            FinishVerify(true);
        }

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static FingerPose RandFinger(System.Random r) =>
            new FingerPose(R(r, 0f, 85f), R(r, -14f, 14f), R(r, 0f, 100f), R(r, 0f, 75f));

        static HandRig NewTestRig(string name, List<GameObject> temp)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.position = new Vector3(0f, -70f, 0f);
            var r = go.AddComponent<HandRig>();
            r.side = 0;
            r.ownSleeve = false;
            go.SetActive(true);
            r.SkipIdleUpdate = true;
            temp.Add(go);
            return r;
        }

        static void JointRots(HandRig r, Quaternion[] q)
        {
            for (int f = 0; f < 4; f++)
            {
                r.GetFingerJoints(f, out var m, out var p, out var d, out _);
                q[f * 3] = m.localRotation; q[f * 3 + 1] = p.localRotation; q[f * 3 + 2] = d.localRotation;
            }
            r.GetThumbJoints(out var c, out var tm, out var ti, out _);
            q[12] = c.localRotation; q[13] = tm.localRotation; q[14] = ti.localRotation;
        }

        static float MaxAngle(Quaternion[] a, Quaternion[] b)
        {
            float m = 0f;
            for (int i = 0; i < a.Length; i++) m = Mathf.Max(m, Quaternion.Angle(a[i], b[i]));
            return m;
        }
    }
}
